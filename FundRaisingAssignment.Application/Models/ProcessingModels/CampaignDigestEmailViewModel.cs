
namespace FundRaisingAssignment.Application.Models.ProcessingModels;

public class CampaignDigestEmailViewModel
{
    public required IEnumerable<CampaignDisplayItem> Campaigns { get; set; }
}

public class CampaignDisplayItem
{
    public required Guid Id { get; set; }
    public required string Title { get; set; }
    public required string SummaryText { get; set; }
    public required string FormattedGoal { get; set; }
    public required string FormattedRaised { get; set; }
    public decimal ProgressPercentage { get; set; }
}
