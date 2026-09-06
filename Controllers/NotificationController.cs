using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MeetingScheduler.API.Data;
using MeetingScheduler.API.Models;
using MeetingScheduler.API.Services;

namespace MeetingScheduler.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class NotificationController : ControllerBase
    {
        private readonly AppDbContext _context;

        public NotificationController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllNotifications()
        {
            var currentUserId = User.GetUserId();

            if (currentUserId == null)
            {
                return Unauthorized();
            }

            var notifications = await _context.Notifications
                .Where(n => n.UserId == currentUserId.Value)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            return Ok(notifications);
        }

        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetNotificationsByUser(int userId)
        {
            var forbidden = ForbidIfNotCurrentUser(userId);

            if (forbidden != null)
            {
                return forbidden;
            }

            var notifications = await _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            return Ok(notifications);
        }

        [HttpPost]
        public async Task<IActionResult> AddNotification(
            Notification notification)
        {
            var currentUserId = User.GetUserId();

            if (currentUserId == null)
            {
                return Unauthorized();
            }

            if (notification.UserId != currentUserId.Value)
            {
                return Forbid();
            }

            notification.CreatedAt = DateTime.UtcNow;

            _context.Notifications.Add(notification);

            await _context.SaveChangesAsync();

            return Created(
                $"api/notification/{notification.Id}",
                notification
            );
        }

        [HttpPut("{id}/read")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var notification = await _context.Notifications
                .FindAsync(id);

            if (notification == null)
            {
                return NotFound("Notification not found");
            }

            var forbidden = ForbidIfNotCurrentUser(notification.UserId);

            if (forbidden != null)
            {
                return forbidden;
            }

            notification.IsRead = true;

            await _context.SaveChangesAsync();

            return Ok(notification);
        }

        [HttpPut("user/{userId}/read-all")]
        public async Task<IActionResult> MarkAllAsRead(int userId)
        {
            var forbidden = ForbidIfNotCurrentUser(userId);

            if (forbidden != null)
            {
                return forbidden;
            }

            var notifications = await _context.Notifications
                .Where(n =>
                    n.UserId == userId &&
                    !n.IsRead
                )
                .ToListAsync();

            foreach (var notification in notifications)
            {
                notification.IsRead = true;
            }

            await _context.SaveChangesAsync();

            return Ok(notifications);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteNotification(int id)
        {
            var notification = await _context.Notifications
                .FindAsync(id);

            if (notification == null)
            {
                return NotFound("Notification not found");
            }

            var forbidden = ForbidIfNotCurrentUser(notification.UserId);

            if (forbidden != null)
            {
                return forbidden;
            }

            _context.Notifications.Remove(notification);

            await _context.SaveChangesAsync();

            return Ok(
                "Notification deleted successfully"
            );
        }

        private IActionResult? ForbidIfNotCurrentUser(int userId)
        {
            var currentUserId = User.GetUserId();

            if (currentUserId == null)
            {
                return Unauthorized();
            }

            if (currentUserId.Value != userId)
            {
                return Forbid();
            }

            return null;
        }
    }
}
