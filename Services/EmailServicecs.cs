using System.Net.Http.Json;

namespace MeetingScheduler.API.Services
{
    public class EmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;
        private readonly HttpClient _httpClient;

        public EmailService(
            IConfiguration config,
            ILogger<EmailService> logger,
            HttpClient httpClient)
        {
            _config = config;
            _logger = logger;
            _httpClient = httpClient;
        }

        public async Task SendEmailAsync(
            string toEmail,
            string subject,
            string body)
        {
            try
            {
                var apiKey = _config["Brevo:ApiKey"];
                var senderEmail = _config["Brevo:SenderEmail"];
                var senderName = _config["Brevo:SenderName"];

                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    throw new InvalidOperationException(
                        "Brevo API Key is missing."
                    );
                }

                if (string.IsNullOrWhiteSpace(senderEmail))
                {
                    throw new InvalidOperationException(
                        "Sender Email is missing."
                    );
                }

                var emailData = new
                {
                    sender = new
                    {
                        name = string.IsNullOrWhiteSpace(senderName)
                            ? "SyncUp"
                            : senderName,

                        email = senderEmail
                    },

                    to = new[]
                    {
                        new
                        {
                            email = toEmail
                        }
                    },

                    subject = subject,

                    htmlContent = body
                };

                var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    "https://api.brevo.com/v3/smtp/email"
                );

                request.Headers.Add(
                    "api-key",
                    apiKey
                );

                request.Content = JsonContent.Create(emailData);

                _logger.LogInformation(
                    "Attempting to send email to {Recipient} using Brevo API",
                    toEmail
                );

                var response =
                    await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var error =
                        await response.Content.ReadAsStringAsync();

                    _logger.LogError(
                        "Brevo email error: {Error}",
                        error
                    );

                    throw new Exception(
                        $"Email API failed: {error}"
                    );
                }

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