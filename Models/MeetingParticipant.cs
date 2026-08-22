namespace MeetingScheduler.API.Models
{
    public class MeetingParticipant
    {
        public int? Id { get; set; }
        public int? MeetingId { get; set; }
        public int? UserId { get; set; }
        public bool IsMandatory { get; set; } = true;

        public Meeting? Meeting { get; set; }
        public User? User { get; set; }
    }
}