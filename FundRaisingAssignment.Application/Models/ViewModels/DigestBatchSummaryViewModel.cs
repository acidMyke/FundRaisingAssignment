namespace FundRaisingAssignment.Application.Models.ViewModels;

public class DigestBatchSummaryViewModel
{
    public Guid Id { get; set; }
    public string DisplayStatus { get; set; } = string.Empty;
    public string StatusBadgeClass { get; set; } = string.Empty;
    public string DisplayUserCount { get; set; } = string.Empty;
    public string DisplayCampaignCount { get; set; } = string.Empty;
    public string DisplayTriggeredAt { get; set; } = string.Empty;
    public string DisplayStatusUpdatedAt { get; set; } = string.Empty;
}
