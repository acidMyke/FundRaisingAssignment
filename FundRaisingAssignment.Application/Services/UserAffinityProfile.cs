using FundRaisingAssignment.Application.Models;
using FundRaisingAssignment.Application.Models.ProcessingModels;

namespace FundRaisingAssignment.Application.Services;

public class UserAffinityProfile
{
    private UserAffinityProfile() { }

    public Dictionary<CampaignCategory, double> CategoryAffinities { get; } = [];

    public Dictionary<Guid, double> OwnerAffinities { get; } = [];

    /// <summary>
    /// Calculates how relevant a campaign is to the user based on their historical affinity.
    /// </summary>
    public double CalculateAffinityScore(Campaign candidateCampaign)
    {
        double totalScore = 0;

        if (CategoryAffinities.TryGetValue(candidateCampaign.Category, out double categoryScore))
        {
            totalScore += categoryScore;
        }

        if (OwnerAffinities.TryGetValue(candidateCampaign.OwnerId, out double ownerScore))
        {
            totalScore += ownerScore;
        }

        return totalScore;
    }

    private const double VisitWeight = 1.0;
    private const double DonationBaseWeight = 10.0;
    private const double DonationAmountMultiplier = 0.02;

    public static UserAffinityProfile BuildProfile(UserHistoryContext historyContext)
    {
        var profile = new UserAffinityProfile();

        // Create a quick lookup dictionary for campaign summaries to resolve Category/OwnerId O(1)
        var campaignDetailsDict = historyContext.CampaignSummaryContexts?
            .ToDictionary(c => c.Id, c => c) ?? [];

        //TODO: Based on weights and populate Dictionary

        return profile;
    }
}