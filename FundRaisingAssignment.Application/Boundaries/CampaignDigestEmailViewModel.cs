
using FundRaisingAssignment.Application.Models;

namespace FundRaisingAssignment.Application.Boundaries;

public class CampaignDigestEmailViewModel(ApplicationUser user, List<Campaign> campaigns)
{
    public ApplicationUser User { get; } = user;
    public List<Campaign> Campaigns { get; } = campaigns;

}
