using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MeetingScheduler.API.Data;
using MeetingScheduler.API.Models;
using MeetingScheduler.API.Services;

namespace MeetingScheduler.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MeetingParticipantsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly EmailService _emailService;
        private readonly IConfiguration _configuration;
    public MeetingParticipantsController(
        AppDbContext context,
        EmailService emailService,
        IConfiguration configuration)
        {
            _context = context;
            _emailService = emailService;
            _configuration = configuration;
        }

        // GET: api/meetingparticipants
        [HttpGet]
        public async Task<IActionResult> GetAllParticipants()
        {
            var participants = await _context.MeetingParticipants
                .ToListAsync();

            return Ok(participants);
        }

        // GET: api/meetingparticipants/meeting/1
        [HttpGet("meeting/{meetingId}")]
        public async Task<IActionResult> GetParticipantsByMeeting(int meetingId)
        {
            var participants = await _context.MeetingParticipants
                .Where(p => p.MeetingId == meetingId)
                .ToListAsync();

            return Ok(participants);
        }

        // POST: api/meetingparticipants
        [HttpPost]
        public async Task<IActionResult> AddParticipant(
            MeetingParticipant participant)
        {
            // Check if user exists
            var user = await _context.Users
                .FindAsync(participant.UserId);

            if (user == null)
            {
                return NotFound("User not found");
            }

            // Check if meeting exists
            var meeting = await _context.Meetings
                .FindAsync(participant.MeetingId);

            if (meeting == null)
            {
                return NotFound("Meeting not found");
            }

            // Check if participant is already added
            var alreadyAdded = await _context.MeetingParticipants
                .AnyAsync(p =>
                    p.MeetingId == participant.MeetingId &&
                    p.UserId == participant.UserId);

            if (alreadyAdded)
            {
                return BadRequest(
                    "This participant has already been added to the meeting."
                );
            }

            // FIRST save participant successfully
            _context.MeetingParticipants.Add(participant);

            await _context.SaveChangesAsync();

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
                    user.Email,
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
            catch (Exception ex)
            {
                // IMPORTANT:
                // Participant is already added successfully.
                // Email failure should NOT make participant adding fail.

                return Ok(new
                {
                    participant,
                    message =
                        "Participant added successfully, but invitation email could not be sent.",
                    emailError = ex.Message
                });
            }
        }

        // DELETE: api/meetingparticipants/1
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteParticipant(int id)
        {
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
