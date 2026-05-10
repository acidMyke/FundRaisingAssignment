using FundRaisingAssignment.Application.Models;

namespace FundRaisingAssignment.Application.Interfaces.Repositories;

public interface ICampaignDigestRepository
{
    Task SaveChangesAsync();
    Task<List<ApplicationUser>> GetUsersEligibleForDigestAsync(DateTime executionTime);
    Task<List<Campaign>> GetActiveCampaignsAsync();
}
