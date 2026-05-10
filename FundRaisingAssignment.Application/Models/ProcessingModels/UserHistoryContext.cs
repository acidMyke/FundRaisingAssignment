namespace FundRaisingAssignment.Application.Models.ProcessingModels;

public class UserHistoryContext
{
    public List<Donation> PastDonations { get; set; } = [];
    public List<CampaignVisit> PastVisits { get; set; } = [];
    public List<CampaignSummaryContext> CampaignSummaryContexts { get; set; } = [];
}
