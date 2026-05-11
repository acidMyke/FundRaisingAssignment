
namespace FundRaisingAssignment.Application.Models.ProcessingModels;

public class CampaignDigestEmailViewModel(ApplicationUser user, List<Campaign> campaigns)
{
    public ApplicationUser User { get; } = user;
    public List<Campaign> Campaigns { get; } = campaigns;

}
