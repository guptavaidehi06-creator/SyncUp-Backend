using Microsoft.EntityFrameworkCore;
using MeetingScheduler.API.Models;

namespace MeetingScheduler.API.Data
{
	public class AppDbContext : DbContext
	{
		public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

		public DbSet<User> Users { get; set; }
		public DbSet<Availability> Availabilities { get; set; }
		public DbSet<Meeting> Meetings { get; set; }
		public DbSet<MeetingParticipant> MeetingParticipants { get; set; }
	}
}
