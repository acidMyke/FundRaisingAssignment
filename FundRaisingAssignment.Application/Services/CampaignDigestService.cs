using FundRaisingAssignment.Application.Interfaces;
using FundRaisingAssignment.Application.Interfaces.Repositories;

namespace FundRaisingAssignment.Application.Services;

public class CampaignDigestService(ICampaignDigestRepository repository, ILogger<CampaignDigestService> logger) : ICampaignDigestService
{
    public async Task TriggerDigestProcessingAsync()
    {
        var executionTime = DateTime.UtcNow;

        var users = await repository.GetUsersEligibleForDigestAsync(executionTime);
        if (users.Count == 0)
        {
            logger.LogInformation("No users eligible for digest processing at this time.");
            return;
        }

        var activeCampaigns = await repository.GetActiveCampaignsAsync();
        if (activeCampaigns.Count == 0)
        {
            logger.LogInformation("No active campaigns to include in digest.");
            return;
        }

    }

}
