using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MeetingScheduler.API.Data;
using MeetingScheduler.API.Models;
using MeetingScheduler.API.Services;

namespace MeetingScheduler.API.Controllers
{
    public class CreateMeetingRequest
    {
        public string? Title { get; set; }
        public DateTime? MeetingDate { get; set; }
        public string? Priority { get; set; }
    }

    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class MeetingsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly NotificationService _notificationService;

        public MeetingsController(
            AppDbContext context,
            NotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllMeetings()
        {
            var currentUserId = User.GetUserId();

            if (currentUserId == null)
            {
                return Unauthorized();
            }

            if (User.IsAdmin())
            {
                var allMeetings = await _context.Meetings.ToListAsync();
                return Ok(allMeetings);
            }

            var meetings = await _context.Meetings
                .Where(m => _context.MeetingParticipants.Any(p =>
                    p.MeetingId == m.Id &&
                    p.UserId == currentUserId.Value))
                .ToListAsync();

            return Ok(meetings);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetMeetingById(int id)
        {
            var currentUserId = User.GetUserId();

            if (currentUserId == null)
            {
                return Unauthorized();
            }

            var meeting = await _context.Meetings.FindAsync(id);

            if (meeting == null)
            {
                return NotFound("Meeting not found");
            }

            if (!User.IsAdmin())
            {
                var isParticipant = await _context.MeetingParticipants
                    .AnyAsync(p =>
                        p.MeetingId == id &&
                        p.UserId == currentUserId.Value);

                if (!isParticipant)
                {
                    return NotFound("Meeting not found");
                }
            }

            return Ok(meeting);
        }

        [HttpPost]
        public async Task<IActionResult> AddMeeting(CreateMeetingRequest request)
        {
            if (!User.IsAdmin())
            {
                return Forbid();
            }

            var currentUserId = User.GetUserId();
            if (currentUserId == null)
            {
                return Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return BadRequest("Meeting title is required.");
            }

            if (!request.MeetingDate.HasValue)
            {
                return BadRequest("A preferred meeting date is required.");
            }

            var meeting = new Meeting
            {
                Title = request.Title.Trim(),
                MeetingDate = request.MeetingDate.Value.Date,
                // New meetings are availability-based. Keep this null so the
                // scheduler can determine the final time from responses.
                MeetingTime = null,
                Priority = request.Priority ?? "Medium",
                Status = "Upcoming",
                CreatedBy = currentUserId.Value
            };

            _context.Meetings.Add(meeting);
            await _context.SaveChangesAsync();

            if (meeting.CreatedBy is > 0)
            {
                var meetingTitle = meeting.Title ?? "a meeting";

                try
                {
                    await _notificationService.NotifyUsersAsync(
                        new[] { meeting.CreatedBy.Value },
                        "Meeting Created",
                        $"Meeting {meetingTitle} was created successfully.",
                        meeting.Id,
                        "Meeting");
                }
                catch
                {
                }
            }

            return Created("api/meetings/" + meeting.Id, meeting);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateMeeting(int id, Meeting updatedMeeting)
        {
            if (!User.IsAdmin())
            {
                return Forbid();
            }

            var meeting = await _context.Meetings.FindAsync(id);

            if (meeting == null)
            {
                return NotFound("Meeting not found");
            }

            var previousStatus = meeting.Status;
            var previousDate = meeting.MeetingDate;
            var previousTime = meeting.MeetingTime;

            meeting.Title = updatedMeeting.Title;
            meeting.MeetingDate = updatedMeeting.MeetingDate;
            meeting.MeetingTime = updatedMeeting.MeetingTime;
            meeting.Priority = updatedMeeting.Priority;
            meeting.Status = updatedMeeting.Status;

            await _context.SaveChangesAsync();

            var meetingTitle = meeting.Title ?? "a meeting";
            var wasCancelled = string.Equals(
                previousStatus,
                "Cancelled",
                StringComparison.OrdinalIgnoreCase);
            var isCancelled = string.Equals(
                meeting.Status,
                "Cancelled",
                StringComparison.OrdinalIgnoreCase);
            var isRescheduledStatus = string.Equals(
                meeting.Status,
                "Rescheduled",
                StringComparison.OrdinalIgnoreCase);
            var scheduleChanged =
                previousDate != meeting.MeetingDate ||
                previousTime != meeting.MeetingTime;

            if (isCancelled && !wasCancelled)
            {
                try
                {
                    await _notificationService.NotifyMeetingRecipientsAsync(
                        meeting,
                        "Meeting Cancelled",
                        $"Meeting {meetingTitle} has been cancelled.",
                        "Meeting",
                        MeetingEmailKind.Cancelled);
                }
                catch
                {
                }
            }
            else if (!isCancelled && (isRescheduledStatus || scheduleChanged))
            {
                try
                {
                    await _notificationService.NotifyMeetingRecipientsAsync(
                        meeting,
                        "Meeting Rescheduled",
                        $"Meeting {meetingTitle} has been rescheduled.",
                        "Meeting",
                        MeetingEmailKind.Rescheduled);
                }
                catch
                {
                }
            }

            return Ok(meeting);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMeeting(int id)
        {
            if (!User.IsAdmin())
            {
                return Forbid();
            }

            var meeting = await _context.Meetings.FindAsync(id);

            if (meeting == null)
            {
                return NotFound("Meeting not found");
            }

            _context.Meetings.Remove(meeting);
            await _context.SaveChangesAsync();

            return Ok("Meeting deleted successfully");
        }
    }
}
