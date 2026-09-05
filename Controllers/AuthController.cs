using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MeetingScheduler.API.Data;
using MeetingScheduler.API.Models;
using MeetingScheduler.API.Services;
using System.Security.Cryptography;
using System.Text.Encodings.Web;

namespace MeetingScheduler.API.Controllers
{
    public class RegisterRequest
    {
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
    }

public class LoginRequest
    {
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
    }

    public class VerifyRequest
    {
        public string Email { get; set; } = "";
        public string Code { get; set; } = "";
    }

    public class ForgotPasswordRequest
    {
        public string Email { get; set; } = "";
    }

    public class ResetPasswordRequest
    {
        public string Email { get; set; } = "";
        public string Code { get; set; } = "";
        public string NewPassword { get; set; } = "";
    }

    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly JwtService _jwtService;
        private readonly EmailService _emailService;

        public AuthController(
            AppDbContext context,
            JwtService jwtService,
            EmailService emailService)
        {
            _context = context;
            _jwtService = jwtService;
            _emailService = emailService;
        }


        // ================= REGISTER =================

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterRequest request)
        {
            Console.WriteLine("=== REGISTER REQUEST ===");
            Console.WriteLine($"Name: {request.Name}");
            Console.WriteLine($"Email: {request.Email}");
            Console.WriteLine($"Password Length: {request.Password?.Length}");

            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == request.Email);

            if (existingUser != null)
            {
                Console.WriteLine("ERROR: Email already exists");

                return BadRequest(
                    "An account with this email already exists."
                );
            }

            if (string.IsNullOrWhiteSpace(request.Name) ||
                string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Password) ||
                request.Password.Length < 8)
            {
                Console.WriteLine("ERROR: Validation failed");

                return BadRequest(
                    "Name, email, and password of at least 8 characters are required."
                );
            }

            var hashedPassword =
                BCrypt.Net.BCrypt.HashPassword(request.Password);

            var verificationCode = GenerateCode();

            var user = new User
            {
                Name = request.Name,
                Email = request.Email,
                Password = hashedPassword,
                IsAdmin = false,
                IsVerified = false,
                VerificationCode = verificationCode,
                VerificationCodeExpiry = DateTime.UtcNow.AddMinutes(15),
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);

            await _context.SaveChangesAsync();

            try
            {
                await SendVerificationEmailAsync(user);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"EMAIL ERROR: {ex.Message}"
                );

                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    "Your account was created, but we could not send the verification email."
                );
            }

            return Ok(new
            {
                message =
                    "Registered successfully. Please check your email for a verification code.",

                email = user.Email
            });
        }


        // ================= RESEND VERIFICATION =================

        [HttpPost("resend-verification")]
        public async Task<IActionResult> ResendVerification(
            ForgotPasswordRequest request)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(
                    u => u.Email == request.Email
                );

            if (user == null || user.IsVerified)
            {
                return Ok(
                    "If the account needs verification, a new code has been sent."
                );
            }

            user.VerificationCode = GenerateCode();

            user.VerificationCodeExpiry =
                DateTime.UtcNow.AddMinutes(15);

            await _context.SaveChangesAsync();

            try
            {
                await SendVerificationEmailAsync(user);
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"RESEND EMAIL ERROR: {ex.Message}"
                );

                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    "Unable to send email right now. Please try again shortly."
                );
            }

            return Ok(
                "A new verification code has been sent."
            );
        }


        // ================= VERIFY EMAIL =================

        [HttpPost("verify")]
        public async Task<IActionResult> Verify(
            VerifyRequest request)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(
                    u => u.Email == request.Email
                );

            if (user == null)
            {
                return NotFound("User not found.");
            }

            if (user.IsVerified)
            {
                return Ok("Account already verified.");
            }

            if (user.VerificationCodeExpiry == null ||
                user.VerificationCodeExpiry < DateTime.UtcNow)
            {
                return BadRequest(
                    "This code has expired. Please request a new one."
                );
            }

            if (user.VerificationCode != request.Code)
            {
                return BadRequest(
                    "Invalid verification code."
                );
            }

            user.IsVerified = true;
            user.VerificationCode = null;
            user.VerificationCodeExpiry = null;

            await _context.SaveChangesAsync();

            var token =
                _jwtService.GenerateToken(user);

            return Ok(new
            {
                token,

                user = new
                {
                    user.Id,
                    user.Name,
                    user.Email,
                    user.IsAdmin
                }
            });
        }


        // ================= LOGIN =================

        [HttpPost("login")]
        public async Task<IActionResult> Login(
            LoginRequest request)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(
                    u => u.Email == request.Email
                );

            bool passwordValid = false;

            try
            {
                passwordValid =
                    user != null &&
                    user.Password != null &&
                    BCrypt.Net.BCrypt.Verify(
                        request.Password,
                        user.Password
                    );
            }
            catch
            {
                passwordValid = false;
            }

            if (user == null || !passwordValid)
            {
                return Unauthorized(
                    "Invalid email or password."
                );
            }

            if (!user.IsVerified && !user.IsAdmin)
            {
                return Unauthorized(
                    "Please verify your email before logging in."
                );
            }

            var token =
                _jwtService.GenerateToken(user);

            return Ok(new
            {
                token,

                user = new
                {
                    user.Id,
                    user.Name,
                    user.Email,
                    user.IsAdmin
                }
            });
        }


        // ================= FORGOT PASSWORD =================

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword(
            ForgotPasswordRequest request)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(
                    u => u.Email == request.Email
                );

            if (user == null)
            {
                return Ok(
                    "If this email exists, a reset code has been sent."
                );
            }

            var resetCode = GenerateCode();

            user.VerificationCode = resetCode;

            user.VerificationCodeExpiry =
                DateTime.UtcNow.AddMinutes(15);

            await _context.SaveChangesAsync();

            var subject =
                "Reset your SyncUp password";

            var body = $@"
            <h2>Hi {HtmlEncoder.Default.Encode(user.Name ?? "there")},</h2>

            <p>Your password reset code is:</p>

            <h1 style='letter-spacing: 4px;'>
                {resetCode}
            </h1>

            <p>This code expires in 15 minutes.</p>

            <br>

            <p>
                Thanks,<br>
                SyncUp Team
            </p>
        ";

            try
            {
                await _emailService.SendEmailAsync(
                    user.Email!,
                    subject,
                    body
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"FORGOT PASSWORD EMAIL ERROR: {ex.Message}"
                );

                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    "Unable to send the reset email right now. Please try again shortly."
                );
            }

            return Ok(
                "If this email exists, a reset code has been sent."
            );
        }


        // ================= RESET PASSWORD =================

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword(
            ResetPasswordRequest request)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(
                    u => u.Email == request.Email
                );

            if (user == null)
            {
                return BadRequest(
                    "Invalid or expired reset code."
                );
            }

            if (user.VerificationCodeExpiry == null ||
                user.VerificationCodeExpiry < DateTime.UtcNow)
            {
                return BadRequest(
                    "This code has expired. Please request a new one."
                );
            }

            if (user.VerificationCode != request.Code)
            {
                return BadRequest(
                    "Invalid or expired reset code."
                );
            }

            user.Password =
                BCrypt.Net.BCrypt.HashPassword(
                    request.NewPassword
                );

            user.VerificationCode = null;
            user.VerificationCodeExpiry = null;

            await _context.SaveChangesAsync();

            return Ok(
                "Password reset successfully. You can now log in."
            );
        }


        // ================= GENERATE 6 DIGIT CODE =================

        private static string GenerateCode()
        {
            return RandomNumberGenerator
                .GetInt32(100000, 1000000)
                .ToString();
        }


        // ================= SEND VERIFICATION EMAIL =================

        private async Task SendVerificationEmailAsync(
            User user)
        {
            var safeName =
                HtmlEncoder.Default.Encode(
                    user.Name ?? "there"
                );

            var subject =
                "Verify your SyncUp account";

            var body = $@"
            <h2>Hi {safeName},</h2>

            <p>Your verification code is:</p>

            <h1 style='letter-spacing: 4px;'>
                {user.VerificationCode}
            </h1>

            <p>This code expires in 15 minutes.</p>

            <br>

            <p>
                Thanks,<br>
                SyncUp Team
            </p>
        ";

            await _emailService.SendEmailAsync(
                user.Email!,
                subject,
                body
            );
        }
    }

}
