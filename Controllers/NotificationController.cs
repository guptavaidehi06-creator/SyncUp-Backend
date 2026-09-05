using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MeetingScheduler.API.Data;
using MeetingScheduler.API.Models;

namespace MeetingScheduler.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NotificationController : ControllerBase
    {
        private readonly AppDbContext _context;

        public NotificationController(AppDbContext context)
        {
            _context = context;
        }

        // Get all notifications
        [HttpGet]
        public async Task<IActionResult> GetAllNotifications()
        {
            var notifications = await _context.Notifications
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            return Ok(notifications);
        }

        // Get notifications of a specific user
        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetNotificationsByUser(int userId)
        {
            var notifications = await _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            return Ok(notifications);
        }

        // Add notification
        [HttpPost]
        public async Task<IActionResult> AddNotification(
            Notification notification)
        {
            notification.CreatedAt = DateTime.UtcNow;

            _context.Notifications.Add(notification);

            await _context.SaveChangesAsync();

            return Created(
                $"api/notification/{notification.Id}",
                notification
            );
        }

        // Mark notification as read
        [HttpPut("{id}/read")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var notification = await _context.Notifications
                .FindAsync(id);

            if (notification == null)
            {
                return NotFound("Notification not found");
            }

            notification.IsRead = true;

            await _context.SaveChangesAsync();

            return Ok(notification);
        }

        // Mark all notifications of a user as read
        [HttpPut("user/{userId}/read-all")]
        public async Task<IActionResult> MarkAllAsRead(int userId)
        {
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

        // Delete notification
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteNotification(int id)
        {
            var notification = await _context.Notifications
                .FindAsync(id);

            if (notification == null)
            {
                return NotFound("Notification not found");
            }

            _context.Notifications.Remove(notification);

            await _context.SaveChangesAsync();

            return Ok(
                "Notification deleted successfully"
            );
        }
    }
}