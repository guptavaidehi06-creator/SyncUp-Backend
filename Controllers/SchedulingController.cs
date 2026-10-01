using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using MeetingScheduler.API.Data;
using MeetingScheduler.API.Services;

namespace MeetingScheduler.API.Controllers
{
    public class SuggestSlotRequest
    {
        public int MeetingId { get; set; }
        public int DurationMinutes { get; set; }
    }

    public class ConfirmSlotRequest
    {
        public int MeetingId { get; set; }
        public DateTime MeetingDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public int DurationMinutes { get; set; }
    }

    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class SchedulingController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly NotificationService _notificationService;

        public SchedulingController(AppDbContext context, NotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        [HttpPost("suggest")]
        public async Task<IActionResult> SuggestSlot(SuggestSlotRequest request)
        {
            if (!User.IsAdmin()) return Forbid();

            if (request.DurationMinutes <= 0 || request.DurationMinutes > 24 * 60)
            {
                return BadRequest("A valid meeting duration is required.");
            }

            var meeting = await _context.Meetings.FindAsync(request.MeetingId);
            if (meeting == null || meeting.MeetingDate == null)
            {
                return BadRequest("A meeting with a date is required to find a slot.");
            }

            if (IsUnavailableForScheduling(meeting.Status))
            {
                return Conflict("This meeting is already scheduled or is no longer eligible for scheduling.");
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

            // Availability must belong to this meeting and its exact date.
            var mandatoryAvailabilities = await _context.Availabilities
                .Where(a => a.MeetingId == request.MeetingId &&
                    mandatoryUserIds.Contains(a.UserId ?? -1) &&
                    a.SpecificDate.HasValue && a.SpecificDate.Value.Date == meetingDate)
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
            var duration = TimeSpan.FromMinutes(request.DurationMinutes);

            foreach (var start in validRanges.Select(a => a.StartTime!.Value).Distinct())
            {
                var end = start + duration;
                if (end > TimeSpan.FromDays(1)) continue;
                var everyoneCanAttend = mandatoryUserIds.All(userId => validRanges.Any(a =>
                    a.UserId == userId && a.StartTime <= start && a.EndTime >= end));
                if (everyoneCanAttend && (!bestStart.HasValue || start < bestStart.Value))
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
                .Where(a => a.MeetingId == request.MeetingId &&
                    optionalUserIds.Contains(a.UserId ?? -1) &&
                    a.SpecificDate.HasValue && a.SpecificDate.Value.Date == meetingDate)
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
                durationMinutes = request.DurationMinutes,
                mandatoryAttendees = mandatoryUserIds,
                optionalAttendeesWhoCanJoin = optionalAttendeeIds,
                message = "Common time slot found for all mandatory participants."
            });
        }

        [HttpPost("confirm")]
        public async Task<IActionResult> ConfirmSlot(ConfirmSlotRequest request)
        {
            if (!User.IsAdmin()) return Forbid();

            if (request.DurationMinutes <= 0 || request.StartTime < TimeSpan.Zero ||
                request.EndTime > TimeSpan.FromDays(1) || request.EndTime <= request.StartTime ||
                request.EndTime - request.StartTime != TimeSpan.FromMinutes(request.DurationMinutes))
            {
                return BadRequest("The confirmed start time, end time, and duration must match.");
            }

            var meeting = await _context.Meetings.FindAsync(request.MeetingId);
            if (meeting == null || !meeting.MeetingDate.HasValue ||
                meeting.MeetingDate.Value.Date != request.MeetingDate.Date)
            {
                return BadRequest("The confirmed date must match the meeting date.");
            }

            if (meeting.MeetingDate.Value.Date <= DateTime.UtcNow.Date)
            {
                return BadRequest("The meeting date must be tomorrow or later.");
            }

            if (IsUnavailableForScheduling(meeting.Status))
            {
                return Conflict("This meeting has already been scheduled or is no longer eligible for confirmation.");
            }

            var mandatoryUserIds = await _context.MeetingParticipants
                .Where(p => p.MeetingId == request.MeetingId && p.IsMandatory && p.UserId != null)
                .Select(p => p.UserId!.Value)
                .ToListAsync();
            if (mandatoryUserIds.Count == 0) return BadRequest("No mandatory participants found for this meeting.");

            var meetingDate = meeting.MeetingDate.Value.Date;
            var ranges = await _context.Availabilities
                .Where(a => a.MeetingId == request.MeetingId &&
                    mandatoryUserIds.Contains(a.UserId ?? -1) &&
                    a.SpecificDate.HasValue && a.SpecificDate.Value.Date == meetingDate &&
                    a.StartTime.HasValue && a.EndTime.HasValue && a.StartTime < a.EndTime)
                .ToListAsync();
            var canConfirm = mandatoryUserIds.All(userId => ranges.Any(a =>
                a.UserId == userId && a.StartTime <= request.StartTime && a.EndTime >= request.EndTime));
            if (!canConfirm)
            {
                return BadRequest("The selected slot is no longer available for all mandatory participants.");
            }

            meeting.MeetingTime = request.StartTime;
            meeting.MeetingEndTime = request.EndTime;
            meeting.DurationMinutes = request.DurationMinutes;
            meeting.Status = "Scheduled";
            await _context.SaveChangesAsync();

            try
            {
                await _notificationService.NotifyMeetingRecipientsAsync(
                    meeting,
                    "Meeting Confirmed",
                    $"Meeting {meeting.Title ?? "your meeting"} has been confirmed.",
                    "MeetingConfirmed",
                    MeetingEmailKind.Confirmed);
            }
            catch { }

            return Ok(meeting);
        }

        private static bool IsUnavailableForScheduling(string? status)
        {
            return string.Equals(status, "Scheduled", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(status, "Confirmed", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase);
        }
    }
}
