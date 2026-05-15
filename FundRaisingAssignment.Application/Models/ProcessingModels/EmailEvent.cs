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

public sealed class EmailEvent
{
    public string Email { get; init; } = string.Empty;
    public EmailStatus Status { get; init; } = EmailStatus.Unknown;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string? MessageId { get; init; }
    public string? Reason { get; init; }
    public string Provider { get; init; } = "Internal";

    public EmailEvent(string email, EmailStatus status, string provider)
    {
        Email = email;
        Status = status;
        Provider = provider;
    }

    public EmailEvent() { }
}