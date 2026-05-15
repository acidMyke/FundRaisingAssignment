using System.Text.Json.Serialization;

namespace FundRaisingAssignment.Application.Models.Dto;

public class MailjetEventDto
{
    [JsonPropertyName("event")]
    public string? Event { get; set; }

    [JsonPropertyName("time")]
    public long Time { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("MessageID")]
    public long? MessageId { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}