using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MeetingScheduler.API.Data;

namespace MeetingScheduler.API.Controllers
{
    public class SuggestSlotRequest
    {
        public int MeetingId { get; set; }
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
            var meeting = await _context.Meetings.FindAsync(request.MeetingId);
            if (meeting == null || meeting.MeetingDate == null)
            {
                return BadRequest("A meeting with a date is required to find a slot.");
            }

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

            var meetingDate = meeting.MeetingDate.Value.Date;
            var dayOfWeek = meetingDate.DayOfWeek.ToString();

            // New availability is saved against a specific meeting date. The day-of-week
            // check retains compatibility with availability records created by older builds.
            var mandatoryAvailabilities = await _context.Availabilities
                .Where(a => mandatoryUserIds.Contains(a.UserId ?? -1) &&
                    ((a.SpecificDate.HasValue && a.SpecificDate.Value.Date == meetingDate) ||
                     (!a.SpecificDate.HasValue && a.DayOfWeek == dayOfWeek)))
                .ToListAsync();

            var usersWithAvailability = mandatoryAvailabilities.Select(a => a.UserId).Distinct().ToList();
            var missingMandatoryUsers = mandatoryUserIds.Except(usersWithAvailability.Cast<int>()).ToList();

            if (missingMandatoryUsers.Count > 0)
            {
                return Ok(new
                {
                    success = false,
                    message = "Some mandatory participants have not submitted availability for this meeting date.",
                    missingUserIds = missingMandatoryUsers
                });
            }

            // Each person can submit multiple time windows. Test candidate intervals so a
            // valid later window is not discarded by an earlier non-overlapping window.
            var validRanges = mandatoryAvailabilities
                .Where(a => a.StartTime.HasValue && a.EndTime.HasValue && a.StartTime < a.EndTime)
                .ToList();
            TimeSpan? bestStart = null;
            TimeSpan? bestEnd = null;

            foreach (var start in validRanges.Select(a => a.StartTime!.Value).Distinct())
            foreach (var end in validRanges.Select(a => a.EndTime!.Value).Distinct())
            {
                if (start >= end) continue;
                var everyoneCanAttend = mandatoryUserIds.All(userId => validRanges.Any(a =>
                    a.UserId == userId && a.StartTime <= start && a.EndTime >= end));
                if (everyoneCanAttend && (!bestStart.HasValue || end - start > bestEnd!.Value - bestStart.Value))
                {
                    bestStart = start;
                    bestEnd = end;
                }
            }

            if (!bestStart.HasValue || !bestEnd.HasValue)
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
                .Where(a => optionalUserIds.Contains(a.UserId ?? -1) &&
                    ((a.SpecificDate.HasValue && a.SpecificDate.Value.Date == meetingDate) ||
                     (!a.SpecificDate.HasValue && a.DayOfWeek == dayOfWeek)))
                .ToListAsync();

            var optionalAttendeeIds = optionalAvailabilities
                .Where(a => a.StartTime <= bestStart && a.EndTime >= bestEnd)
                .Select(a => a.UserId)
                .ToList();

            return Ok(new
            {
                success = true,
                meetingDate,
                suggestedStartTime = bestStart,
                suggestedEndTime = bestEnd,
                mandatoryAttendees = mandatoryUserIds,
                optionalAttendeesWhoCanJoin = optionalAttendeeIds,
                message = "Common time slot found for all mandatory participants."
            });
        }
    }
}
