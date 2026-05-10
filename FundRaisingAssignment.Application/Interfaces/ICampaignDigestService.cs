using FundRaisingAssignment.Application.Models;

namespace FundRaisingAssignment.Application.Interfaces;

public interface ICampaignDigestService
{
    Task TriggerDigestProcessingAsync();
    double CalculateCampaignUrgencyScore(Campaign campaign, DateTime now);
}
