using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MeetingScheduler.API.Data;
using MeetingScheduler.API.Models;
using MeetingScheduler.API.Services;

namespace MeetingScheduler.API.Controllers
{
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

            return Ok(availability);
        }

        [HttpGet("meeting/{meetingId}")]
        public async Task<IActionResult> GetAvailabilityByMeeting(int meetingId)
        {
            var availabilities = await _context.Availabilities
                .Where(a => a.MeetingId == meetingId)
                .ToListAsync();

            return Ok(availabilities);
        }

        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetAvailabilityByUser(int userId)
        {
            var availabilities = await _context.Availabilities
                .Where(a => a.UserId == userId)
                .ToListAsync();

            return Ok(availabilities);
        }

        [HttpPost]
        public async Task<IActionResult> AddAvailability(
            Availability availability)
        {
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
            var availability = await _context.Availabilities
                .FindAsync(id);

            if (availability == null)
            {
                return NotFound("Availability not found");
            }

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

            _context.Availabilities.Remove(availability);

            await _context.SaveChangesAsync();

            return Ok(
                "Availability deleted successfully"
            );
        }
    }
}