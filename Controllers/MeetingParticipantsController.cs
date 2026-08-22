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

        public MeetingParticipantsController(AppDbContext context, EmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllParticipants()
        {
            var participants = await _context.MeetingParticipants.ToListAsync();
            return Ok(participants);
        }

        [HttpGet("meeting/{meetingId}")]
        public async Task<IActionResult> GetParticipantsByMeeting(int meetingId)
        {
            var participants = await _context.MeetingParticipants
                .Where(p => p.MeetingId == meetingId)
                .ToListAsync();

            return Ok(participants);
        }

        [HttpPost]
        public async Task<IActionResult> AddParticipant(MeetingParticipant participant)
        {
            _context.MeetingParticipants.Add(participant);
            await _context.SaveChangesAsync();

            // Send invite email (fire and forget - doesn't block the response)
            var user = await _context.Users.FindAsync(participant.UserId);
            var meeting = await _context.Meetings.FindAsync(participant.MeetingId);

            if (user != null && meeting != null && !string.IsNullOrEmpty(user.Email))
            {
                var inviteLink = $"http://localhost:4200/submit-availability/{meeting.Id}";
                var subject = $"You're invited: {meeting.Title}";
                var body = $@"
                    <h2>Hi {user.Name},</h2>
                    <p>You have been invited to a meeting: <strong>{meeting.Title}</strong></p>
                    <p>Date: {meeting.MeetingDate?.ToString("MMMM dd, yyyy")}</p>
                    <p>Please submit your availability using the link below:</p>
                    <p><a href='{inviteLink}'>{inviteLink}</a></p>
                    <br>
                    <p>Thanks,<br>SyncUp Team</p>
                ";

                _ = _emailService.SendEmailAsync(user.Email, subject, body);
            }

            return Created("api/meetingparticipants/" + participant.Id, participant);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteParticipant(int id)
        {
            var participant = await _context.MeetingParticipants.FindAsync(id);

            if (participant == null)
            {
                return NotFound("Participant not found");
            }

            _context.MeetingParticipants.Remove(participant);
            await _context.SaveChangesAsync();

            return Ok("Participant deleted successfully");
        }
    }
}