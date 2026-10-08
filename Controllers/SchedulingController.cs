using System.Data;
using MeetingScheduler.API.Data;
using MeetingScheduler.API.Models;
using MeetingScheduler.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
                .Where(userId => userId > 0)
                .Distinct()
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
            var relevantParticipantIds = participants
                .Where(p => p.UserId.HasValue && p.UserId.Value > 0)
                .Select(p => p.UserId!.Value)
                .Distinct()
                .ToList();
            var conflicts = await LoadScheduledConflictsAsync(meetingDate, request.MeetingId, relevantParticipantIds);

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

            var candidateStarts = validRanges.Select(a => a.StartTime!.Value)
                .Concat(conflicts.Meetings
                    .Select(GetStoredMeetingEndTime)
                    .Where(endTime => endTime.HasValue)
                    .Select(endTime => endTime!.Value))
                .Distinct();

            foreach (var start in candidateStarts)
            {
                var end = start + duration;
                if (end > TimeSpan.FromDays(1)) continue;
                var everyoneCanAttend = mandatoryUserIds.All(userId => validRanges.Any(a =>
                    a.UserId == userId && a.StartTime <= start && a.EndTime >= end));
                var mandatoryParticipantsAreFree = !HasScheduledConflict(
                    meetingDate, start, end, mandatoryUserIds, conflicts.Meetings, conflicts.Participants);
                if (everyoneCanAttend && mandatoryParticipantsAreFree && (!bestStart.HasValue || start < bestStart.Value))
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
                .Where(a => !HasScheduledConflict(
                    meetingDate, bestStart!.Value, bestEnd!.Value,
                    new[] { a.UserId ?? -1 }, conflicts.Meetings, conflicts.Participants))
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
            try
            {
                return await ConfirmSlotCore(request);
            }
            catch (MySqlConnector.MySqlException exception) when (exception.Number is 1205 or 1213)
            {
                return Conflict("The selected slot could not be confirmed because another meeting was scheduled concurrently. Find a new best slot.");
            }
        }

        private async Task<IActionResult> ConfirmSlotCore(ConfirmSlotRequest request)
        {
            if (!User.IsAdmin()) return Forbid();

            if (request.DurationMinutes <= 0 || request.StartTime < TimeSpan.Zero ||
                request.EndTime > TimeSpan.FromDays(1) || request.EndTime <= request.StartTime ||
                request.EndTime - request.StartTime != TimeSpan.FromMinutes(request.DurationMinutes))
            {
                return BadRequest("The confirmed start time, end time, and duration must match.");
            }

            // Serialize confirmations so concurrent requests cannot both pass the
            // conflict check and schedule overlapping meetings.
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

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
                .Distinct()
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

            var conflicts = await LoadScheduledConflictsAsync(meetingDate, request.MeetingId, mandatoryUserIds);
            if (HasScheduledConflict(
                meetingDate, request.StartTime, request.EndTime,
                mandatoryUserIds, conflicts.Meetings, conflicts.Participants))
            {
                return Conflict("The selected slot overlaps another scheduled meeting for a required participant. Find a new best slot.");
            }

            meeting.MeetingTime = request.StartTime;
            meeting.MeetingEndTime = request.EndTime;
            meeting.DurationMinutes = request.DurationMinutes;
            meeting.Status = "Scheduled";
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

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

        private async Task<(List<Meeting> Meetings, List<MeetingParticipant> Participants)> LoadScheduledConflictsAsync(
            DateTime meetingDate,
            int currentMeetingId,
            List<int> participantIds)
        {
            if (participantIds.Count == 0)
            {
                return (new List<Meeting>(), new List<MeetingParticipant>());
            }

            var meetings = await _context.Meetings
                .AsNoTracking()
                .Where(existing => existing.Id != currentMeetingId &&
                    existing.MeetingDate.HasValue && existing.MeetingDate.Value.Date == meetingDate.Date &&
                    existing.Status != null &&
                    (existing.Status.ToLower() == "scheduled" || existing.Status.ToLower() == "confirmed"))
                .ToListAsync();

            var meetingIds = meetings
                .Where(existing => existing.Id.HasValue)
                .Select(existing => existing.Id!.Value)
                .ToList();
            if (meetingIds.Count == 0)
            {
                return (meetings, new List<MeetingParticipant>());
            }

            var participants = await _context.MeetingParticipants
                .AsNoTracking()
                .Where(participant => participant.MeetingId.HasValue &&
                    meetingIds.Contains(participant.MeetingId.Value) &&
                    participant.UserId.HasValue && participantIds.Contains(participant.UserId.Value))
                .ToListAsync();

            return (meetings, participants);
        }

        private static bool HasScheduledConflict(
            DateTime meetingDate,
            TimeSpan candidateStart,
            TimeSpan candidateEnd,
            IEnumerable<int> participantIds,
            List<Meeting> meetings,
            List<MeetingParticipant> participants)
        {
            var participantSet = participantIds.ToHashSet();
            foreach (var existing in meetings)
            {
                if (!existing.Id.HasValue || !existing.MeetingDate.HasValue ||
                    existing.MeetingDate.Value.Date != meetingDate.Date)
                {
                    continue;
                }

                var sharesParticipant = participants.Any(participant =>
                    participant.MeetingId == existing.Id && participant.UserId.HasValue &&
                    participantSet.Contains(participant.UserId.Value));
                if (!sharesParticipant) continue;

                // Unknown or inconsistent end values occupy the rest of the date;
                // fail closed rather than scheduling an unsafe overlap.
                if (!existing.MeetingTime.HasValue) return true;

                var existingStart = existing.MeetingTime.Value;
                var existingEnd = GetStoredMeetingEndTime(existing);

                if (!existingEnd.HasValue || existingEnd.Value <= existingStart) return true;

                if (candidateStart < existingEnd.Value && candidateEnd > existingStart)
                {
                    return true;
                }
            }

            return false;
        }

        private static TimeSpan? GetStoredMeetingEndTime(Meeting meeting)
        {
            if (meeting.MeetingEndTime.HasValue) return meeting.MeetingEndTime.Value;
            if (meeting.MeetingTime.HasValue && meeting.DurationMinutes is > 0)
            {
                return meeting.MeetingTime.Value + TimeSpan.FromMinutes(meeting.DurationMinutes.Value);
            }

            return null;
        }
    }
}
