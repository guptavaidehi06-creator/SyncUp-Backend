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
            try
            {
                var smtpHost = _config["Email:SmtpHost"];
                var smtpPortString = _config["Email:SmtpPort"];

                var senderEmail = _config["Email:SenderEmail"];
                var senderPassword = _config["Email:SenderPassword"];
                var senderName = _config["Email:SenderName"];

                if (string.IsNullOrWhiteSpace(smtpHost))
                {
                    throw new InvalidOperationException(
                        "SMTP Host is missing."
                    );
                }

                if (string.IsNullOrWhiteSpace(senderEmail))
                {
                    throw new InvalidOperationException(
                        "Sender Email is missing."
                    );
                }

                if (string.IsNullOrWhiteSpace(senderPassword))
                {
                    throw new InvalidOperationException(
                        "Sender Password is missing."
                    );
                }

                int smtpPort = 587;

                if (!string.IsNullOrWhiteSpace(smtpPortString))
                {
                    smtpPort = int.Parse(smtpPortString);
                }

                _logger.LogInformation(
                    "Attempting to send email to {Recipient} using SMTP host {Host} and port {Port}",
                    toEmail,
                    smtpHost,
                    smtpPort
                );

                using var client = new SmtpClient
                {
                    Host = smtpHost,
                    Port = smtpPort,
                    EnableSsl = true,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(
                        senderEmail,
                        senderPassword
                    ),
                    Timeout = 30000
                };

                using var mailMessage = new MailMessage
                {
                    From = new MailAddress(
                        senderEmail,
                        string.IsNullOrWhiteSpace(senderName)
                            ? "SyncUp"
                            : senderName
                    ),

                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };

                mailMessage.To.Add(toEmail);

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