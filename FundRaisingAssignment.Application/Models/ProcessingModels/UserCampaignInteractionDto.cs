namespace FundRaisingAssignment.Application.Models.ProcessingModels;

public class UserCampaignInteractionDto
{
    public Guid UserId { get; set; }
    public Guid CampaignId { get; set; }
    public int VisitCount { get; set; }
    public decimal DonationAmount { get; set; }
    public DateTime InteractionDate { get; set; }
}
