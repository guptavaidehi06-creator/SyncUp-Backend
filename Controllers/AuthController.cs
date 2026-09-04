using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MeetingScheduler.API.Data;
using MeetingScheduler.API.Models;
using MeetingScheduler.API.Services;

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

        public AuthController(AppDbContext context, JwtService jwtService, EmailService emailService)
        {
            _context = context;
            _jwtService = jwtService;
            _emailService = emailService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterRequest request)
        {
            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (existingUser != null)
            {
                return BadRequest("An account with this email already exists.");
            }

            var hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);
            var verificationCode = new Random().Next(100000, 999999).ToString();

            var user = new User
            {
                Name = request.Name,
                Email = request.Email,
                Password = hashedPassword,
                IsAdmin = false,
                IsVerified = false,
                VerificationCode = verificationCode,
                CreatedAt = DateTime.Now
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var subject = "Verify your SyncUp account";
            var body = $@"
                <h2>Hi {user.Name},</h2>
                <p>Your verification code is:</p>
                <h1 style='letter-spacing: 4px;'>{verificationCode}</h1>
                <p>Enter this code to activate your account.</p>
                <br>
                <p>Thanks,<br>SyncUp Team</p>
            ";

            _ = _emailService.SendEmailAsync(user.Email!, subject, body);

            return Ok(new
            {
                message = "Registered successfully. Please check your email for a verification code.",
                email = user.Email,
                verificationCode
            });
        }

        [HttpPost("verify")]
        public async Task<IActionResult> Verify(VerifyRequest request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

            if (user == null)
            {
                return NotFound("User not found.");
            }

            if (user.IsVerified)
            {
                return Ok("Account already verified.");
            }

            if (user.VerificationCode != request.Code)
            {
                return BadRequest("Invalid verification code.");
            }

            user.IsVerified = true;
            user.VerificationCode = null;
            await _context.SaveChangesAsync();

            var token = _jwtService.GenerateToken(user);

            return Ok(new
            {
                token,
                user = new { user.Id, user.Name, user.Email, user.IsAdmin }
            });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequest request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

            bool passwordValid = false;
            try
            {
                passwordValid = user != null && user.Password != null && BCrypt.Net.BCrypt.Verify(request.Password, user.Password);
            }
            catch
            {
                passwordValid = false;
            }

            if (user == null || !passwordValid)
            {
                return Unauthorized("Invalid email or password.");
            }

            if (!user.IsVerified)
            {
                return Unauthorized("Please verify your email before logging in.");
            }

            var token = _jwtService.GenerateToken(user);

            return Ok(new
            {
                token,
                user = new { user.Id, user.Name, user.Email, user.IsAdmin }
            });
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword(VerifyRequest request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

            if (user == null)
            {
                return NotFound("No account was found with this email address.");
            }

            var resetCode = new Random().Next(100000, 999999).ToString();
            user.VerificationCode = resetCode;
            await _context.SaveChangesAsync();

            var subject = "Reset your SyncUp password";
            var body = $@"
                <h2>Hi {user.Name},</h2>
                <p>Your password reset code is:</p>
                <h1 style='letter-spacing: 4px;'>{resetCode}</h1>
                <p>Use this code to reset your password.</p>
                <br>
                <p>Thanks,<br>SyncUp Team</p>
            ";

            _ = _emailService.SendEmailAsync(user.Email!, subject, body);
            return Ok(new
            {
                message = "Reset code generated successfully.",
                resetCode
            });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

            if (user == null || user.VerificationCode != request.Code)
            {
                return BadRequest("Invalid reset code.");
            }

            user.Password = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            user.VerificationCode = null;
            await _context.SaveChangesAsync();

            return Ok("Password reset successfully.");
        }

        [HttpPost("fix-password/{userId}")]
        public async Task<IActionResult> FixPassword(int userId, [FromBody] string newPassword)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound("User not found.");
            }

            user.Password = BCrypt.Net.BCrypt.HashPassword(newPassword);
            await _context.SaveChangesAsync();

            return Ok("Password updated and hashed successfully.");
        }
    }
}