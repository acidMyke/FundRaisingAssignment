using FundRaisingAssignment.Application.Models;

namespace FundRaisingAssignment.Application.Interfaces
{
    public interface ICampaignDigestRepository
    {
        Task<List<Campaign>> GetCampaignsNeedingDigestAsync(DateTime executionTime);
        Task<List<Guid>> GetCampaignPastDonorIdsAsync(Guid campaignId);
        Task<List<Guid>> GetCampaignVisitorIdsAsync(Guid campaignId);
        Task<List<ApplicationUser>> GetUsersByIdsAsync(IEnumerable<Guid> userIds);
        Task<List<DonationRecord>> GetUserDonationsForCampaignAsync(Guid userId, Guid campaignId);
        Task SaveChangesAsync();
    }
}
