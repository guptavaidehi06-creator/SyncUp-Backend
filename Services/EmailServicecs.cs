using System.Net;
using System.Net.Mail;

namespace MeetingScheduler.API.Services
{
    public class EmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;

        public EmailService(
            IConfiguration config,
            ILogger<EmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task SendEmailAsync(
            string toEmail,
            string subject,
            string body)
        {
            var smtpHost = _config["Email:SmtpHost"];
            var smtpPort = int.Parse(
                _config["Email:SmtpPort"] ?? "587"
            );

            var senderEmail = _config["Email:SenderEmail"];
            var senderPassword = _config["Email:SenderPassword"];
            var senderName = _config["Email:SenderName"];

            if (string.IsNullOrWhiteSpace(smtpHost) ||
                string.IsNullOrWhiteSpace(senderEmail) ||
                string.IsNullOrWhiteSpace(senderPassword))
            {
                throw new InvalidOperationException(
                    "Email is not configured properly."
                );
            }

            using var client = new SmtpClient(smtpHost, smtpPort)
            {
                Credentials = new NetworkCredential(
                    senderEmail,
                    senderPassword
                ),
                EnableSsl = true
            };

            using var mailMessage = new MailMessage
            {
                From = new MailAddress(
                    senderEmail,
                    senderName
                ),

                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            mailMessage.To.Add(toEmail);

            try
            {
                await client.SendMailAsync(mailMessage);

                _logger.LogInformation(
                    "Email sent successfully to {Recipient}",
                    toEmail
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Email sending failed for {Recipient}",
                    toEmail
                );

                throw;
            }
        }
    }
}