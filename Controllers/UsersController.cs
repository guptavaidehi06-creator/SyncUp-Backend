using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MeetingScheduler.API.Data;
using MeetingScheduler.API.Models;

namespace MeetingScheduler.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _context;

        public UsersController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllUsers()
        {
            if (!MeetingScheduler.API.Services.ClaimsPrincipalExtensions.IsAdmin(User))
            {
                return Forbid();
            }

            var users = await _context.Users
                .Where(user => user.IsVerified)
                .Select(user => new
                {
                    user.Id,
                    user.Name,
                    user.Email
                })
                .ToListAsync();
            return Ok(users);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetUserById(int id)
        {
            if (!MeetingScheduler.API.Services.ClaimsPrincipalExtensions.IsAdmin(User))
            {
                return Forbid();
            }

            var user = await _context.Users.FindAsync(id);

            if (user == null)
            {
                return NotFound("User not found");
            }

            return Ok(new
            {
                user.Id,
                user.Name,
                user.Email,
                user.IsVerified,
                user.IsAdmin,
                user.CreatedAt
            });
        }

        [HttpPost]
        public async Task<IActionResult> AddUser(User user)
        {
            if (!MeetingScheduler.API.Services.ClaimsPrincipalExtensions.IsAdmin(User))
            {
                return Forbid();
            }

            return BadRequest(
                "Please create accounts through the signup flow."
            );
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateUser(int id, User updatedUser)
        {
            if (!MeetingScheduler.API.Services.ClaimsPrincipalExtensions.IsAdmin(User))
            {
                return Forbid();
            }

            var user = await _context.Users.FindAsync(id);

            if (user == null)
            {
                return NotFound("User not found");
            }

            user.Name = updatedUser.Name;
            user.Email = updatedUser.Email;
            user.Password = updatedUser.Password;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                user.Id,
                user.Name,
                user.Email,
                user.IsVerified,
                user.IsAdmin,
                user.CreatedAt
            });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            if (!MeetingScheduler.API.Services.ClaimsPrincipalExtensions.IsAdmin(User))
            {
                return Forbid();
            }

            var user = await _context.Users.FindAsync(id);

            if (user == null)
            {
                return NotFound("User not found");
            }

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return Ok("User deleted successfully");

        }
        [HttpPost("make-admin/{id}")]
        public async Task<IActionResult> MakeAdmin(int id)
        {
            if (!MeetingScheduler.API.Services.ClaimsPrincipalExtensions.IsAdmin(User))
            {
                return Forbid();
            }

            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound("User not found");
            }

            user.IsAdmin = true;
            await _context.SaveChangesAsync();

            return Ok("User is now an admin");
        }
    }
}
