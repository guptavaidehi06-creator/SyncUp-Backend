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
    public class AvailabilityController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly NotificationService _notificationService;

        public AvailabilityController(
            AppDbContext context,
            NotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllAvailabilities()
        {
            if (!User.IsAdmin()) return Forbid();

            var availabilities = await _context.Availabilities
                .ToListAsync();

            return Ok(availabilities);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetAvailabilityById(int id)
        {
            var availability = await _context.Availabilities
                .FindAsync(id);

            if (availability == null)
            {
                return NotFound("Availability not found");
            }

            if (!User.IsAdmin() && availability.UserId != User.GetUserId())
            {
                return NotFound("Availability not found");
            }

            return Ok(availability);
        }

        [HttpGet("meeting/{meetingId}")]
        public async Task<IActionResult> GetAvailabilityByMeeting(int meetingId)
        {
            if (!User.IsAdmin()) return Forbid();

            var availabilities = await _context.Availabilities
                .Where(a => a.MeetingId == meetingId)
                .ToListAsync();

            return Ok(availabilities);
        }

        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetAvailabilityByUser(int userId)
        {
            if (!User.IsAdmin() && userId != User.GetUserId()) return Forbid();

            var availabilities = await _context.Availabilities
                .Where(a => a.UserId == userId)
                .ToListAsync();

            return Ok(availabilities);
        }

        [HttpPost]
        public async Task<IActionResult> AddAvailability(
            Availability availability)
        {
            var currentUserId = User.GetUserId();
            if (currentUserId == null) return Unauthorized();

            if (!User.IsAdmin() && availability.UserId != currentUserId)
            {
                return Forbid();
            }

            var meeting = await _context.Meetings.FindAsync(availability.MeetingId);
            if (meeting == null) return NotFound("Meeting not found.");

            var isParticipant = await _context.MeetingParticipants.AnyAsync(p =>
                p.MeetingId == availability.MeetingId && p.UserId == availability.UserId);
            if (!isParticipant) return Forbid();

            var dateError = ValidateMeetingDate(availability, meeting.MeetingDate);
            if (dateError != null) return BadRequest(dateError);

            var validationError = ValidateTimeRange(availability);
            if (validationError != null)
            {
                return BadRequest(validationError);
            }

            _context.Availabilities.Add(availability);

            await _context.SaveChangesAsync();

            try
            {
                await _notificationService.NotifyAvailabilitySubmittedAsync(
                    availability);
            }
            catch
            {
                // Availability is already saved. Notification failure
                // should not fail the submit request.
            }

            return Created(
                "api/availability/" + availability.Id,
                availability
            );
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateAvailability(
            int id,
            Availability updatedAvailability)
        {
            var validationError = ValidateTimeRange(updatedAvailability);
            if (validationError != null)
            {
                return BadRequest(validationError);
            }

            var availability = await _context.Availabilities
                .FindAsync(id);

            if (availability == null)
            {
                return NotFound("Availability not found");
            }

            var currentUserId = User.GetUserId();
            if (!User.IsAdmin() &&
                (availability.UserId != currentUserId || updatedAvailability.UserId != currentUserId))
            {
                return Forbid();
            }

            var meeting = await _context.Meetings.FindAsync(updatedAvailability.MeetingId);
            if (meeting == null) return NotFound("Meeting not found.");

            var isParticipant = await _context.MeetingParticipants.AnyAsync(p =>
                p.MeetingId == updatedAvailability.MeetingId && p.UserId == updatedAvailability.UserId);
            if (!isParticipant) return Forbid();

            var dateError = ValidateMeetingDate(updatedAvailability, meeting.MeetingDate);
            if (dateError != null) return BadRequest(dateError);

            availability.MeetingId =
                updatedAvailability.MeetingId;

            availability.UserId =
                updatedAvailability.UserId;

            availability.DayOfWeek =
                updatedAvailability.DayOfWeek;

            availability.SpecificDate =
                updatedAvailability.SpecificDate;

            availability.StartTime =
                updatedAvailability.StartTime;

            availability.EndTime =
                updatedAvailability.EndTime;

            await _context.SaveChangesAsync();

            return Ok(availability);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAvailability(int id)
        {
            var availability = await _context.Availabilities
                .FindAsync(id);

            if (availability == null)
            {
                return NotFound("Availability not found");
            }

            if (!User.IsAdmin() && availability.UserId != User.GetUserId())
            {
                return Forbid();
            }

            _context.Availabilities.Remove(availability);

            await _context.SaveChangesAsync();

            return Ok(
                "Availability deleted successfully"
            );
        }

        private static string? ValidateTimeRange(Availability availability)
        {
            if (!availability.StartTime.HasValue || !availability.EndTime.HasValue)
            {
                return "Both start time and end time are required.";
            }

            return availability.EndTime.Value <= availability.StartTime.Value
                ? "End time must be after start time."
                : null;
        }

        private static string? ValidateMeetingDate(Availability availability, DateTime? meetingDate)
        {
            if (!availability.SpecificDate.HasValue || !meetingDate.HasValue ||
                availability.SpecificDate.Value.Date != meetingDate.Value.Date)
            {
                return "Availability date must match the meeting date.";
            }

            return null;
        }
    }
}
