namespace MeetingScheduler.API.Models
{
    public class Meeting
    {
        public int? Id { get; set; }
        public string? Title { get; set; }
        public DateTime? MeetingDate { get; set; }
        public TimeSpan? MeetingTime { get; set; }
        public string? Priority { get; set; } = "Medium";
        public string? Status { get; set; } = "Scheduled";
        public int? CreatedBy { get; set; }
        public DateTime? CreatedAt { get; set; } = DateTime.Now;

        public User? Creator { get; set; }
    }
}