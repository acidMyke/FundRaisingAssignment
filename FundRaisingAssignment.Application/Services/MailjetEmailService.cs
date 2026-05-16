using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FundRaisingAssignment.Application.Interfaces;
using FundRaisingAssignment.Application.Models;
using FundRaisingAssignment.Application.Models.Dto;
using FundRaisingAssignment.Application.Models.ProcessingModels;
using Microsoft.Extensions.Options;

namespace FundRaisingAssignment.Application.Services
{
    public class MailjetEmailService(IOptions<EmailSettings> settings, HttpClient httpClient, EmailEventHub hub, ILogger<MailjetEmailService> logger) : IEmailService
    {
        private readonly EmailSettings _settings = settings.Value;
        private readonly HttpClient _httpClient = httpClient;
        private readonly EmailEventHub _emailEventHub = hub;
        private readonly ILogger<MailjetEmailService> _logger = logger;

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

        public async Task ProcessMailjetEventAsync(MailjetEventDto dto)
        {
            _logger.LogInformation("Processing Mailjet event: {Event} for {Email} (MessageId: {MessageId})", dto.Event, dto.Email, dto.CustomID);
            ArgumentNullException.ThrowIfNull(dto);
            ArgumentException.ThrowIfNullOrEmpty(dto.Email);
            if (!dto.CustomID.HasValue)
            {
                throw new ArgumentNullException(nameof(dto), "MessageId cannot be null");
            }

            var status = dto.Event?.ToLower() switch
            {
                "sent" => EmailStatus.Sent,
                "open" => EmailStatus.Opened,
                "click" => EmailStatus.Clicked,
                "bounce" => EmailStatus.Bounced,
                "spam" => EmailStatus.Spam,
                _ => EmailStatus.Unknown
            };

            var emailEvent = new EmailEvent(dto.Email, status, "Mailjet")
            {
                Timestamp = DateTimeOffset.FromUnixTimeSeconds(dto.Time).UtcDateTime,
                MessageId = dto.CustomID.ToString(),
                Reason = dto.Error
            };

            await _emailEventHub.PublishAsync(emailEvent);
            _logger.LogDebug("Published Mailjet email event: {Status} for {Email}", status, dto.Email);
        }
    }

    public static class MailjetWebhookExtensions
    {
        public static IEndpointRouteBuilder MapMailjetWebhookIfRegistered(this IEndpointRouteBuilder app)
        {
            using var scope = app.ServiceProvider.CreateScope();
            var emailService = scope.ServiceProvider.GetService<IEmailService>();
            if (emailService is MailjetEmailService)
            {
                app.MapPost("/webhooks/mailjet", (IServiceProvider serviceProvider, HttpContext context, MailjetEventDto dto) =>
                {
                    context.Response.OnCompleted(async () =>
                    {
                        using var innerScope = serviceProvider.CreateScope();
                        var svc = innerScope.ServiceProvider.GetRequiredService<MailjetEmailService>();
                        await svc.ProcessMailjetEventAsync(dto);
                    });
                    return Results.Ok();
                });
            }

            return app;
        }
    }
}
