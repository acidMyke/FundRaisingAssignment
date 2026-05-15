using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FundRaisingAssignment.Application.Interfaces;
using FundRaisingAssignment.Application.Models;
using Microsoft.Extensions.Options;

namespace FundRaisingAssignment.Application.Services
{
    public class MailjetEmailService(IOptions<EmailSettings> settings, HttpClient httpClient) : IEmailService
    {
        private readonly EmailSettings _settings = settings.Value;
        private readonly HttpClient _httpClient = httpClient;

        public Task SendEmailAsync(string email, string subject, string htmlMessage) => SendEmailAsync(email, subject, htmlMessage, Guid.NewGuid().ToString());

        public async Task SendEmailAsync(string email, string subject, string htmlMessage, string messageId)
        {
            if (string.IsNullOrEmpty(_settings.ApiKey) || string.IsNullOrEmpty(_settings.ApiSecret))
            {
                throw new InvalidOperationException("Mailjet API Key or Secret is not configured.");
            }

            var requestBody = new
            {
                Messages = new[]
                {
                    new
                    {
                        From = new
                        {
                            Email = _settings.FromEmail ?? "noreply@example.com",
                            Name = _settings.FromName ?? "FundRaising App"
                        },
                        To = new[]
                        {
                            new
                            {
                                Email = email,
                                Name = email
                            }
                        },
                        Subject = subject,
                        HTMLPart = htmlMessage,
                        CustomId = messageId,
                    }
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var authToken = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_settings.ApiKey}:{_settings.ApiSecret}"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authToken);

            var response = await _httpClient.PostAsync("https://api.mailjet.com/v3.1/send", content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Failed to send email via Mailjet: {response.StatusCode} - {error}");
            }
        }
    }
}
