using FundRaisingAssignment.Application.Interfaces;
using FundRaisingAssignment.Application.Interfaces.Repositories;

namespace FundRaisingAssignment.Application.Services;

public class CampaignDigestService(ICampaignDigestRepository repository) : ICampaignDigestService
{
    public async Task TriggerDigestProcessingAsync()
    {
    }

}
