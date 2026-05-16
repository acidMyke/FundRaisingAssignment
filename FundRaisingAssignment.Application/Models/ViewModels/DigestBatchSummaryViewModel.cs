namespace FundRaisingAssignment.Application.Models.ViewModels;

public class DigestBatchSummaryViewModel
{
    public Guid Id { get; set; }
    public DigestBatchStatus Status { get; set; }
    public int? UserCount { get; set; }
    public int? CampaignCount { get; set; }
    public DateTime TriggeredAt { get; set; }
    public DateTime? StatusUpdatedAt { get; set; }
}
