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

        if (historyContext.PastVisits != null)
        {
            foreach (var visit in historyContext.PastVisits)
            {
                if (campaignDetailsDict.TryGetValue(visit.CampaignId, out var campaignInfo))
                {
                    double score = visit.VisitCount * VisitWeight;

                    AddScore(profile.CategoryAffinities, campaignInfo.Category, score);
                    AddScore(profile.OwnerAffinities, campaignInfo.OwnerId, score);
                }
            }
        }

        if (historyContext.PastDonations != null)
        {
            foreach (var donation in historyContext.PastDonations)
            {
                if (campaignDetailsDict.TryGetValue(donation.CampaignId, out var campaignInfo))
                {
                    // Base points for donating + slight scaling based on the donation size
                    double amountScore = Convert.ToDouble(donation.Amount) * DonationAmountMultiplier;
                    double score = DonationBaseWeight + amountScore;

                    AddScore(profile.CategoryAffinities, campaignInfo.Category, score);
                    AddScore(profile.OwnerAffinities, campaignInfo.OwnerId, score);
                }
            }
        }

        return profile;
    }

    private static void AddScore<TKey>(Dictionary<TKey, double> dictionary, TKey key, double scoreToAdd) where TKey : notnull
    {
        if (dictionary.ContainsKey(key))
            dictionary[key] += scoreToAdd;
        else
            dictionary[key] = scoreToAdd;
    }
}