using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MeetingScheduler.API.Data;
using MeetingScheduler.API.Models;
using MeetingScheduler.API.Services;

namespace MeetingScheduler.API.Controllers
{
    public class AddMeetingParticipantRequest
    {
        public int? MeetingId { get; set; }
        public int? UserId { get; set; }
        public bool IsMandatory { get; set; } = true;
    }

    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class MeetingParticipantsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly EmailService _emailService;
        private readonly NotificationService _notificationService;
        private readonly IConfiguration _configuration;

        public MeetingParticipantsController(
            AppDbContext context,
            EmailService emailService,
            NotificationService notificationService,
            IConfiguration configuration)
        {
            _context = context;
            _emailService = emailService;
            _notificationService = notificationService;
            _configuration = configuration;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllParticipants()
        {
            var currentUserId = User.GetUserId();

            if (currentUserId == null)
            {
                return Unauthorized();
            }

            if (User.IsAdmin())
            {
                var allParticipants = await _context.MeetingParticipants
                    .ToListAsync();

                return Ok(allParticipants);
            }

            var participants = await _context.MeetingParticipants
                .Where(p => _context.MeetingParticipants.Any(mine =>
                    mine.UserId == currentUserId.Value &&
                    mine.MeetingId == p.MeetingId))
                .ToListAsync();

            return Ok(participants);
        }

        [HttpGet("meeting/{meetingId}")]
        public async Task<IActionResult> GetParticipantsByMeeting(int meetingId)
        {
            var currentUserId = User.GetUserId();

            if (currentUserId == null)
            {
                return Unauthorized();
            }

            if (!User.IsAdmin())
            {
                var isParticipant = await _context.MeetingParticipants
                    .AnyAsync(p =>
                        p.MeetingId == meetingId &&
                        p.UserId == currentUserId.Value);

                if (!isParticipant)
                {
                    return NotFound("Meeting not found");
                }
            }

            var participants = await _context.MeetingParticipants
                .Where(p => p.MeetingId == meetingId)
                .ToListAsync();

            return Ok(participants);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetParticipantById(int id)
        {
            var currentUserId = User.GetUserId();

            if (currentUserId == null)
            {
                return Unauthorized();
            }

            var participant = await _context.MeetingParticipants
                .FindAsync(id);

            if (participant == null)
            {
                return NotFound("Participant not found");
            }

            if (!User.IsAdmin())
            {
                var canAccess = await _context.MeetingParticipants
                    .AnyAsync(p =>
                        p.MeetingId == participant.MeetingId &&
                        p.UserId == currentUserId.Value);

                if (!canAccess)
                {
                    return NotFound("Participant not found");
                }
            }

            return Ok(participant);
        }

        [HttpPost]
        public async Task<IActionResult> AddParticipant(
            AddMeetingParticipantRequest request)
        {
            if (!User.IsAdmin())
            {
                return Forbid();
            }

            // Check if user exists
            var user = await _context.Users
                .FindAsync(request.UserId);

            if (user == null)
            {
                return NotFound(
                    "User is not registered. Please ask them to create an account first."
                );
            }

            if (!user.IsVerified)
            {
                return BadRequest(
                    "User has not verified their account yet. Please complete email verification first."
                );
            }

            // Check if meeting exists
            var meeting = await _context.Meetings
                .FindAsync(request.MeetingId);

            if (meeting == null)
            {
                return NotFound("Meeting not found");
            }

            // Check if participant is already added
            var alreadyAdded = await _context.MeetingParticipants
                .AnyAsync(p =>
                    p.MeetingId == request.MeetingId &&
                    p.UserId == request.UserId);

            if (alreadyAdded)
            {
                return BadRequest(
                    "This participant has already been added to the meeting."
                );
            }

            var participant = new MeetingParticipant
            {
                MeetingId = request.MeetingId,
                UserId = request.UserId,
                IsMandatory = request.IsMandatory
            };

            // The participant is persisted only after the registered and verified checks above.
            _context.MeetingParticipants.Add(participant);

            await _context.SaveChangesAsync();

            var meetingTitle = meeting.Title ?? "a meeting";
            var requiresAvailability =
                !meeting.MeetingTime.HasValue ||
                meeting.MeetingTime.Value == TimeSpan.Zero;
            var notificationMessage = requiresAvailability
                ? $"You were added to {meetingTitle}. Please submit your availability."
                : $"You were added to {meetingTitle}.";

            if (user.Id is > 0)
            {
                try
                {
                    await _notificationService.NotifyUsersAsync(
                        new[] { user.Id.Value },
                        "Added to Meeting",
                        notificationMessage,
                        meeting.Id,
                        "Participant");
                }
                catch
                {
                }
            }

            // Get frontend URL from Railway environment variable
            var clientBaseUrl = _configuration["Client:BaseUrl"];

            // Fallback for local testing
            if (string.IsNullOrWhiteSpace(clientBaseUrl))
            {
                clientBaseUrl = "http://localhost:4200";
            }

            clientBaseUrl = clientBaseUrl.TrimEnd('/');

            // Availability page link
            var inviteLink =
                $"{clientBaseUrl}/submit-availability/{meeting.Id}";

            var subject =
                $"You're invited to: {meeting.Title}";

            var body = $@"
            <h2>Hi {user.Name},</h2>

            <p>
                You have been invited to participate in a meeting.
            </p>

            <p>
                <strong>Meeting:</strong> {meeting.Title}
            </p>

            <p>
                <strong>Date:</strong>
                {meeting.MeetingDate?.ToString("MMMM dd, yyyy")}
            </p>

            <p>
                Please submit your availability by clicking the button below.
            </p>

            <p>
                <a href='{inviteLink}'
                   style='background-color:#4CAF50;
                          color:white;
                          padding:10px 20px;
                          text-decoration:none;
                          border-radius:5px;'>
                    Submit Availability
                </a>
            </p>

            <p>
                Or copy this link:
            </p>

            <p>
                <a href='{inviteLink}'>
                    {inviteLink}
                </a>
            </p>

            <br>

            <p>
                Thanks,<br>
                <strong>SyncUp Team</strong>
            </p>
        ";

            // Try sending email
            try
            {
                await _emailService.SendEmailAsync(
                    user.Email!,
                    subject,
                    body
                );

                return Created(
                    "api/meetingparticipants/" + participant.Id,
                    new
                    {
                        participant,
                        message =
                            "Participant added successfully and invitation email sent."
                    }
                );
            }
            catch
            {
                // IMPORTANT:
                // Participant is already added successfully.
                // Email failure should NOT make participant adding fail.

                return Ok(new
                {
                    participant,
                    message =
                        "Participant added successfully, but invitation email could not be sent."
                });
            }
        }

        // DELETE: api/meetingparticipants/1
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteParticipant(int id)
        {
            if (!User.IsAdmin())
            {
                return Forbid();
            }

            var participant = await _context.MeetingParticipants
                .FindAsync(id);

            if (participant == null)
            {
                return NotFound("Participant not found");
            }

            _context.MeetingParticipants.Remove(participant);

            await _context.SaveChangesAsync();

            return Ok(
                "Participant deleted successfully"
            );
        }
    }

}
