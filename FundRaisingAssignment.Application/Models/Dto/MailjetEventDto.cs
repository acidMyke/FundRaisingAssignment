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

    [JsonPropertyName("CustomID")]
    public long? CustomID { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}