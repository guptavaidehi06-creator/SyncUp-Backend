namespace MeetingScheduler.API.Models
{
    public class User
    {
        public int? Id { get; set; }
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Password { get; set; }
        public bool IsAdmin { get; set; } = false;
        public bool IsVerified { get; set; } = false;
        public string? VerificationCode { get; set; }
        public DateTime? ResetCodeExpiresAt { get; set; }
        public DateTime? CreatedAt { get; set; } = DateTime.Now;
    }
}