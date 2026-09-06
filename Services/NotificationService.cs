using Microsoft.EntityFrameworkCore;
using MeetingScheduler.API.Data;
using MeetingScheduler.API.Models;

namespace MeetingScheduler.API.Services
{
    public enum MeetingEmailKind
    {
        None,
        Rescheduled,
        Cancelled
    }

    public class NotificationService
    {
        private readonly AppDbContext _context;
        private readonly EmailService _emailService;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(
            AppDbContext context,
            EmailService emailService,
            ILogger<NotificationService> logger)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task NotifyWelcomeAsync(User user)
        {
            if (user.Id is not > 0)
            {
                return;
            }

            var alreadyExists = await _context.Notifications.AnyAsync(n =>
                n.UserId == user.Id &&
                n.Type == "Welcome");

            if (alreadyExists)
            {
                return;
            }

            await NotifyUsersAsync(
                new[] { user.Id.Value },
                "Welcome to SyncUp",
                "Welcome to SyncUp!",
                null,
                "Welcome");
        }

        public async Task NotifyAvailabilitySubmittedAsync(
            Availability availability)
        {
            if (availability.MeetingId is not > 0 ||
                availability.UserId is not > 0)
            {
                return;
            }

            var meeting = await _context.Meetings.FindAsync(
                availability.MeetingId);

            if (meeting?.CreatedBy is not > 0)
            {
                return;
            }

            var participant = await _context.Users.FindAsync(
                availability.UserId);

            var participantName = string.IsNullOrWhiteSpace(participant?.Name)
                ? "A participant"
                : participant.Name;

            var meetingTitle = meeting.Title ?? "a meeting";

            await NotifyUsersAsync(
                new[] { meeting.CreatedBy.Value },
                "Availability Submitted",
                $"{participantName} submitted availability for {meetingTitle}.",
                meeting.Id,
                "AvailabilitySubmitted",
                skipRecentDuplicates: true);
        }

        public async Task NotifyMeetingRecipientsAsync(
            Meeting meeting,
            string title,
            string message,
            string type,
            MeetingEmailKind emailKind)
        {
            if (meeting.Id is not > 0)
            {
                return;
            }

            var userIds = await _context.MeetingParticipants
                .Where(p =>
                    p.MeetingId == meeting.Id &&
                    p.UserId != null)
                .Select(p => p.UserId!.Value)
                .ToListAsync();

            if (meeting.CreatedBy is > 0)
            {
                userIds.Add(meeting.CreatedBy.Value);
            }

            await NotifyUsersAsync(
                userIds,
                title,
                message,
                meeting.Id,
                type,
                emailKind,
                meeting);
        }

        public async Task NotifyUsersAsync(
            IEnumerable<int> userIds,
            string title,
            string message,
            int? meetingId,
            string type,
            MeetingEmailKind emailKind = MeetingEmailKind.None,
            Meeting? meeting = null,
            bool skipRecentDuplicates = false)
        {
            var uniqueIds = userIds
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            if (uniqueIds.Count == 0)
            {
                return;
            }

            var users = await _context.Users
                .Where(u =>
                    u.Id != null &&
                    uniqueIds.Contains(u.Id.Value))
                .ToListAsync();

            var duplicateWindow = DateTime.UtcNow.AddMinutes(-3);
            var createdUserIds = new List<int>();

            foreach (var user in users)
            {
                if (user.Id is not > 0)
                {
                    continue;
                }

                var isDuplicate = false;

                if (type == "Welcome")
                {
                    isDuplicate = await _context.Notifications.AnyAsync(n =>
                        n.UserId == user.Id &&
                        n.Type == "Welcome");
                }
                else if (skipRecentDuplicates)
                {
                    isDuplicate = await _context.Notifications.AnyAsync(n =>
                        n.UserId == user.Id &&
                        n.MeetingId == meetingId &&
                        n.Type == type &&
                        n.Message == message &&
                        n.CreatedAt > duplicateWindow);
                }

                if (isDuplicate)
                {
                    continue;
                }

                _context.Notifications.Add(new Notification
                {
                    UserId = user.Id.Value,
                    MeetingId = meetingId,
                    Title = title,
                    Message = message,
                    Type = type,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                });

                createdUserIds.Add(user.Id.Value);
            }

            if (createdUserIds.Count > 0)
            {
                await _context.SaveChangesAsync();
            }

            if (emailKind == MeetingEmailKind.None || meeting == null)
            {
                return;
            }

            foreach (var user in users.Where(u =>
                u.Id is > 0 &&
                createdUserIds.Contains(u.Id.Value)))
            {
                if (string.IsNullOrWhiteSpace(user.Email))
                {
                    continue;
                }

                try
                {
                    if (emailKind == MeetingEmailKind.Rescheduled)
                    {
                        await _emailService.SendMeetingRescheduledEmailAsync(
                            user.Email,
                            user.Name ?? "there",
                            meeting.Title ?? "your meeting",
                            meeting.MeetingDate,
                            meeting.MeetingTime);
                    }
                    else if (emailKind == MeetingEmailKind.Cancelled)
                    {
                        await _emailService.SendMeetingCancelledEmailAsync(
                            user.Email,
                            user.Name ?? "there",
                            meeting.Title ?? "your meeting",
                            meeting.MeetingDate,
                            meeting.MeetingTime);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to send {EmailKind} email to {Email}",
                        emailKind,
                        user.Email);
                }
            }
        }
    }
}
