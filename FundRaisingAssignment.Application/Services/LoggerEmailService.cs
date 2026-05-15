using FundRaisingAssignment.Application.Interfaces;
using FundRaisingAssignment.Application.Models.ProcessingModels;

namespace FundRaisingAssignment.Application.Services
{
    public class LoggerEmailService : IEmailService
    {
        private readonly ILogger<LoggerEmailService> _logger;
        private readonly EmailEventHub _emailEventHub;
        private readonly List<(string Email, string Subject, string Message)> _sentEmails = new();

        public LoggerEmailService(ILogger<LoggerEmailService> logger, EmailEventHub hub)
        {
            _logger = logger;
            _emailEventHub = hub;
        }

        public Task SendEmailAsync(string email, string subject, string htmlMessage) => SendEmailAsync(email, subject, htmlMessage, Guid.NewGuid().ToString());
        public async Task SendEmailAsync(string email, string subject, string htmlMessage, string messageId)
        {
            _logger.LogInformation("--- MOCK EMAIL SENT ---");
            _logger.LogInformation("To: {Email}", email);
            _logger.LogInformation("Subject: {Subject}", subject);
            _logger.LogInformation("Content: {Message}", htmlMessage);
            _logger.LogInformation("-----------------------");

            lock (_sentEmails)
            {
                _sentEmails.Add((email, subject, htmlMessage));
            }

            await Task.Delay(5000);
            await _emailEventHub.PublishAsync(new(email, EmailStatus.Sent, "Logger"));
        }

        /// <summary>
        /// Returns a list of all emails "sent" through this service instance.
        /// </summary>
        public IReadOnlyList<(string Email, string Subject, string Message)> SentEmails
        {
            get
            {
                lock (_sentEmails)
                {
                    return _sentEmails.AsReadOnly();
                }
            }
        }
    }
}
