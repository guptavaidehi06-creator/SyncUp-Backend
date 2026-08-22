using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MeetingScheduler.API.Data;
using MeetingScheduler.API.Models;

namespace MeetingScheduler.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AvailabilityController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AvailabilityController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllAvailabilities()
        {
            var availabilities = await _context.Availabilities.ToListAsync();
            return Ok(availabilities);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetAvailabilityById(int id)
        {
            var availability = await _context.Availabilities.FindAsync(id);

            if (availability == null)
            {
                return NotFound("Availability not found");
            }

            return Ok(availability);
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
        public async Task<IActionResult> AddAvailability(Availability availability)
        {
            _context.Availabilities.Add(availability);
            await _context.SaveChangesAsync();

            return Created("api/availability/" + availability.Id, availability);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateAvailability(int id, Availability updatedAvailability)
        {
            var availability = await _context.Availabilities.FindAsync(id);

            if (availability == null)
            {
                return NotFound("Availability not found");
            }

            availability.DayOfWeek = updatedAvailability.DayOfWeek;
            availability.StartTime = updatedAvailability.StartTime;
            availability.EndTime = updatedAvailability.EndTime;

            await _context.SaveChangesAsync();

            return Ok(availability);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAvailability(int id)
        {
            var availability = await _context.Availabilities.FindAsync(id);

            if (availability == null)
            {
                return NotFound("Availability not found");
            }

            _context.Availabilities.Remove(availability);
            await _context.SaveChangesAsync();

            return Ok("Availability deleted successfully");
        }
    }
}