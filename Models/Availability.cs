namespace MeetingScheduler.API.Models
{
    public class Availability
    {
        public int? Id { get; set; }
        public int? UserId { get; set; }
        public string? DayOfWeek { get; set; }
        public DateTime? SpecificDate { get; set; }
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }

        public User? User { get; set; }
    }
}