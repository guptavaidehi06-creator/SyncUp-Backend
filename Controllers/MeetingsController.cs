using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MeetingScheduler.API.Data;
using MeetingScheduler.API.Models;

namespace MeetingScheduler.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MeetingsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public MeetingsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllMeetings()
        {
            var meetings = await _context.Meetings.ToListAsync();
            return Ok(meetings);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetMeetingById(int id)
        {
            var meeting = await _context.Meetings.FindAsync(id);

            if (meeting == null)
            {
                return NotFound("Meeting not found");
            }

            return Ok(meeting);
        }

        [HttpPost]
        public async Task<IActionResult> AddMeeting(Meeting meeting)
        {
            _context.Meetings.Add(meeting);
            await _context.SaveChangesAsync();

            return Created("api/meetings/" + meeting.Id, meeting);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateMeeting(int id, Meeting updatedMeeting)
        {
            var meeting = await _context.Meetings.FindAsync(id);

            if (meeting == null)
            {
                return NotFound("Meeting not found");
            }

            meeting.Title = updatedMeeting.Title;
            meeting.MeetingDate = updatedMeeting.MeetingDate;
            meeting.MeetingTime = updatedMeeting.MeetingTime;
            meeting.Priority = updatedMeeting.Priority;
            meeting.Status = updatedMeeting.Status;

            await _context.SaveChangesAsync();

            return Ok(meeting);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMeeting(int id)
        {
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