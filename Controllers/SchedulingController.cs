using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MeetingScheduler.API.Data;

namespace MeetingScheduler.API.Controllers
{
    public class SuggestSlotRequest
    {
        public int MeetingId { get; set; }
        public string DayOfWeek { get; set; } = "";
    }

    [ApiController]
    [Route("api/[controller]")]
    public class SchedulingController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SchedulingController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost("suggest")]
        public async Task<IActionResult> SuggestSlot(SuggestSlotRequest request)
        {
            // Get all participants for this meeting
            var participants = await _context.MeetingParticipants
                .Where(p => p.MeetingId == request.MeetingId)
                .ToListAsync();

            if (participants.Count == 0)
            {
                return BadRequest("No participants found for this meeting.");
            }

            var mandatoryUserIds = participants
                .Where(p => p.IsMandatory)
                .Select(p => p.UserId ?? -1)
                .ToList();

            var optionalUserIds = participants
                .Where(p => !p.IsMandatory)
                .Select(p => p.UserId ?? -1)
                .ToList();

            if (mandatoryUserIds.Count == 0)
            {
                return BadRequest("No mandatory participants found for this meeting.");
            }

            // Get availability for mandatory users on the given day
            var mandatoryAvailabilities = await _context.Availabilities
                .Where(a => mandatoryUserIds.Contains(a.UserId ?? -1) && a.DayOfWeek == request.DayOfWeek)
                .ToListAsync();

            var usersWithAvailability = mandatoryAvailabilities.Select(a => a.UserId).Distinct().ToList();
            var missingMandatoryUsers = mandatoryUserIds.Except(usersWithAvailability.Cast<int>()).ToList();

            if (missingMandatoryUsers.Count > 0)
            {
                return Ok(new
                {
                    success = false,
                    message = "Some mandatory participants have not submitted availability for this day.",
                    missingUserIds = missingMandatoryUsers
                });
            }

            // Find overlapping time window among mandatory participants
            var latestStart = mandatoryAvailabilities.Max(a => a.StartTime);
            var earliestEnd = mandatoryAvailabilities.Min(a => a.EndTime);

            if (latestStart == null || earliestEnd == null || latestStart >= earliestEnd)
            {
                return Ok(new
                {
                    success = false,
                    message = "No common time slot found among mandatory participants.",
                    fallback = "Consider suggesting a different day or splitting into two meetings."
                });
            }

            // Check how many optional participants can also attend
            var optionalAvailabilities = await _context.Availabilities
                .Where(a => optionalUserIds.Contains(a.UserId ?? -1) && a.DayOfWeek == request.DayOfWeek)
                .ToListAsync();

            var optionalAttendeeIds = optionalAvailabilities
                .Where(a => a.StartTime <= latestStart && a.EndTime >= earliestEnd)
                .Select(a => a.UserId)
                .ToList();

            return Ok(new
            {
                success = true,
                dayOfWeek = request.DayOfWeek,
                suggestedStartTime = latestStart,
                suggestedEndTime = earliestEnd,
                mandatoryAttendees = mandatoryUserIds,
                optionalAttendeesWhoCanJoin = optionalAttendeeIds,
                message = "Common time slot found for all mandatory participants."
            });
        }
    }
}