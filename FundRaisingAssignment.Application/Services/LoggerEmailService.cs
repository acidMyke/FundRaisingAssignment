using FundRaisingAssignment.Application.Interfaces;

namespace FundRaisingAssignment.Application.Services
{
    public class LoggerEmailService : IEmailService
    {
        private readonly ILogger<LoggerEmailService> _logger;
        private readonly List<(string Email, string Subject, string Message)> _sentEmails = new();

        public LoggerEmailService(ILogger<LoggerEmailService> logger)
        {
            _logger = logger;
        }

        public Task SendEmailAsync(string email, string subject, string htmlMessage)
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

            return Task.CompletedTask;
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
