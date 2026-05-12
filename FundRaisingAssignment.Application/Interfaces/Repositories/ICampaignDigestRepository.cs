using FundRaisingAssignment.Application.Models;
using FundRaisingAssignment.Application.Models.ProcessingModels;

namespace FundRaisingAssignment.Application.Interfaces.Repositories;

public interface ICampaignDigestRepository
{
    Task SaveChangesAsync();
    Task<List<ApplicationUser>> GetUsersEligibleForDigestAsync(DateTime executionTime);
    Task<List<Campaign>> GetActiveCampaignsAsync();
    Task<Dictionary<Guid, UserHistoryContext>> GetHistoryContextsForUsersAsync(IEnumerable<Guid> userIds);
    Task<List<UserCampaignInteractionDto>> GetPastDonationsForUsersAsync(IEnumerable<Guid> userIds);
    Task<List<UserCampaignInteractionDto>> GetPastVisitsForUsersAsync(IEnumerable<Guid> userIds);
    Task<Dictionary<Guid, CampaignSummaryContext>> GetCampaignSummariesAsync(IEnumerable<Guid> campaignIds);
}
