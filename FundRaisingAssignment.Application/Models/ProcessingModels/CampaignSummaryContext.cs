namespace FundRaisingAssignment.Application.Models.ProcessingModels;

public class CampaignSummaryContext
{
    public Guid Id { get; set; }
    public CampaignCategory Category { get; set; }
    public Guid OwnerId { get; set; }
}
