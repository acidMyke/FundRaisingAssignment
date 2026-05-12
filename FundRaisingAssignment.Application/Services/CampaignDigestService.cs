using FundRaisingAssignment.Application.Boundaries;
using FundRaisingAssignment.Application.Interfaces;
using FundRaisingAssignment.Application.Interfaces.Repositories;
using FundRaisingAssignment.Application.Models;
using FundRaisingAssignment.Application.Models.ProcessingModels;
using Microsoft.AspNetCore.Identity;

namespace FundRaisingAssignment.Application.Services;

public class CampaignDigestService(ICampaignDigestRepository repository,
                                   ILogger<CampaignDigestService> logger,
                                   ICampaignDigestEmailTemplateService templateService,
                                   IEmailService emailService) : ICampaignDigestService
{
    public class CampaignScore
    {
        public Campaign Campaign { get; set; } = null!;
        public double Score { get; set; }
    }

    public class UserAffinityProfile
    {
        public Dictionary<CampaignCategory, double> CategoryAffinities { get; } = [];
        public Dictionary<Guid, double> OwnerAffinities { get; } = [];
    }

    private const double VisitWeight = 1.0;
    private const double DonationBaseWeight = 10.0;
    private const double DonationAmountMultiplier = 0.02;

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

        var historyContexts = await repository.GetHistoryContextsForUsersAsync(users.Select(u => u.Id));
        var campaignUgencyScores = activeCampaigns.ToDictionary(c => c.Id, c => CalculateCampaignUrgencyScore(c, executionTime));

        foreach (var user in users)
        {
            try
            {
                var affinityProfile = BuildProfile(historyContexts[user.Id]);
                var digestCampaigns = activeCampaigns.Select(campaign => new CampaignScore
                {
                    Campaign = campaign,
                    Score = campaignUgencyScores[campaign.Id] + CalculateAffinityScore(affinityProfile, campaign)
                })
                .OrderByDescending(cs => cs.Score)
                .Where(cs => cs.Score > 0)
                .Take(3)
                .Select(cs => MapCampaignToDisplayItem(cs.Campaign))
                .ToList();

                if (digestCampaigns.Count == 0)
                {
                    continue;
                }

                var viewModel = new CampaignDigestEmailViewModel
                {
                    Campaigns = digestCampaigns
                };

                var subject = templateService.GenerateSubject(viewModel);
                var htmlBody = templateService.RenderHtmlBody(viewModel);
                await emailService.SendEmailAsync(user.Email!, subject, htmlBody);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process digest for user {UserId}", user.Id);
            }
        }

        await repository.SaveChangesAsync();
    }

    public UserAffinityProfile BuildProfile(UserHistoryContext historyContext)
    {
        var profile = new UserAffinityProfile();

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
                    double amountScore = Convert.ToDouble(donation.Amount) * DonationAmountMultiplier;
                    double score = DonationBaseWeight + amountScore;

                    AddScore(profile.CategoryAffinities, campaignInfo.Category, score);
                    AddScore(profile.OwnerAffinities, campaignInfo.OwnerId, score);
                }
            }
        }

        return profile;
    }

    public double CalculateAffinityScore(UserAffinityProfile profile, Campaign candidateCampaign)
    {
        double totalScore = 0;

        if (profile.CategoryAffinities.TryGetValue(candidateCampaign.Category, out double categoryScore))
        {
            totalScore += categoryScore;
        }

        if (profile.OwnerAffinities.TryGetValue(candidateCampaign.OwnerId, out double ownerScore))
        {
            totalScore += ownerScore;
        }

        return totalScore;
    }

    private void AddScore<TKey>(Dictionary<TKey, double> dictionary, TKey key, double scoreToAdd) where TKey : notnull
    {
        if (dictionary.ContainsKey(key))
            dictionary[key] += scoreToAdd;
        else
            dictionary[key] = scoreToAdd;
    }

    public double CalculateCampaignUrgencyScore(Campaign campaign, DateTime now)
    {
        double score = 0;
        if (campaign.EndDate.HasValue)
        {
            var timeRemaining = campaign.EndDate.Value - now;
            if (timeRemaining.TotalHours <= 24 && timeRemaining.TotalHours > 0) score += 50;
            else if (timeRemaining.TotalHours <= 72 && timeRemaining.TotalHours > 0) score += 30;
            else if (timeRemaining.TotalDays <= 7 && timeRemaining.TotalHours > 0) score += 10;
        }
        if (campaign.TargetAmount > 0)
        {
            var percent = campaign.CurrentAmount / campaign.TargetAmount;
            if (percent >= 0.75m && percent < 1.0m) score += 35;
        }
        return score;
    }

    public CampaignDisplayItem MapCampaignToDisplayItem(Campaign campaign)
    {
        return new CampaignDisplayItem
        {
            Id = campaign.Id,
            Title = campaign.Title,
            SummaryText = !string.IsNullOrEmpty(campaign.ShortDescription)
                                    ? campaign.ShortDescription
                                    : (campaign.Description.Length > 150 ? string.Concat(campaign.Description.AsSpan(0, 147), "...") : campaign.Description),
            FormattedGoal = campaign.FundingGoal.ToString("N0") + " USD",
            FormattedRaised = campaign.CurrentAmount.ToString("N0") + " USD",
            ProgressPercentage = campaign.GetProgressPercentage()
        };
    }
}
