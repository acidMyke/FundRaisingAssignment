namespace FundRaisingAssignment.Application.Models.ProcessingModels;

public enum EmailStatus
{
    Unknown = 0,
    Sent,
    Delivered,
    Opened,
    Clicked,
    Bounced,
    Spam,
}

public class EmailEvent(string email, EmailStatus status, string provider)
{
    public string Email { get; init; } = email;
    public EmailStatus Status { get; init; } = status;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string? MessageId { get; init; }
    public string? Reason { get; init; }
    public string Provider { get; init; } = provider;
}