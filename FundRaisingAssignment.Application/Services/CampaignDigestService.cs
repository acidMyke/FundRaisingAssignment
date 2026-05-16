using FundRaisingAssignment.Application.Interfaces;
using FundRaisingAssignment.Application.Interfaces.Repositories;
using FundRaisingAssignment.Application.Models;
using FundRaisingAssignment.Application.Models.ProcessingModels;
using FundRaisingAssignment.Application.Models.ViewModels;

namespace FundRaisingAssignment.Application.Services;

public class CampaignDigestService(ICampaignDigestRepository repository,
                                   ILogger<CampaignDigestService> logger,
                                   ICampaignDigestEmailTemplateService templateService,
                                   IEmailService emailService,
                                   IDigestJobQueue digestJobQueue) : ICampaignDigestService, IEmailEventListener
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

    #region UI Business Logic

    public async Task<Guid> ValidateAndEnqueueAsync()
    {
        var executionTime = DateTime.UtcNow;

        var users = await repository.GetUsersEligibleForDigestAsync(executionTime, 1);
        if (users.Count == 0)
        {
            throw new DomainException("No users eligible for digest processing at this time.");
        }

        var activeCampaigns = await repository.GetActiveCampaignsAsync();
        if (activeCampaigns.Count == 0)
        {
            throw new DomainException("No active campaigns to include in digest.");
        }

        var batchId = Guid.NewGuid();
        var digestBatch = new DigestBatch { Id = batchId };
        repository.AddDigestBatchRecord(digestBatch);
        await repository.SaveChangesAsync();
        digestJobQueue.QueueJob(batchId);
        return batchId;
    }

    private static string GetBatchStatusBadgeClass(DigestBatchStatus status) => status switch
    {
        DigestBatchStatus.Pending => "bg-warning",
        DigestBatchStatus.Processing => "bg-info",
        DigestBatchStatus.Processed => "bg-success",
        DigestBatchStatus.Failed => "bg-danger",
        _ => "bg-secondary"
    };

    private static string GetEmailStatusBadgeClass(DigestEmailStatus status) => status switch
    {
        DigestEmailStatus.Sent => "bg-primary",
        DigestEmailStatus.Open => "bg-success",
        DigestEmailStatus.Click => "bg-success",
        DigestEmailStatus.Bypass => "bg-secondary",
        DigestEmailStatus.Bounce => "bg-danger",
        DigestEmailStatus.Spam => "bg-danger",
        _ => "bg-info text-dark"
    };

    public async Task<List<DigestBatchSummaryViewModel>> GetAllDigestBatchesAsync()
    {
        var batches = await repository.GetAllDigestBatchesAsync();
        return batches.Select(b => new DigestBatchSummaryViewModel
        {
            Id = b.Id,
            DisplayStatus = b.Status.ToString(),
            StatusBadgeClass = GetBatchStatusBadgeClass(b.Status),
            DisplayUserCount = b.UserCount?.ToString() ?? "-",
            DisplayCampaignCount = b.CampaignCount?.ToString() ?? "-",
            DisplayTriggeredAt = b.TriggeredAt.ToString("g"),
            DisplayStatusUpdatedAt = b.StatusUpdatedAt?.ToString("g") ?? "-"
        }).ToList();
    }

    public async Task<DigestBatchDetailsViewModel?> GetDigestBatchDetailsAsync(Guid batchId)
    {
        var batch = await repository.GetDigestBatchWithDetailsAsync(batchId);
        if (batch == null) return null;

        var viewModel = new DigestBatchDetailsViewModel
        {
            BatchId = batch.Id,
            DisplayStatus = batch.Status.ToString(),
            StatusBadgeClass = GetBatchStatusBadgeClass(batch.Status),
            DisplayTriggeredAt = batch.TriggeredAt.ToString("g"),
            UserGroups = batch.Entries
                .GroupBy(e => e.UserId)
                .Select(g =>
                {
                    var firstEntry = g.First();
                    return new DigestUserGroupViewModel
                    {
                        UserId = g.Key,
                        UserName = firstEntry.User.UserName ?? string.Empty,
                        UserEmail = firstEntry.User.Email ?? string.Empty,
                        DisplayEmailId = firstEntry.EmailId?.ToString() ?? "N/A",
                        DisplayEmailStatus = firstEntry.EmailStatus.ToString(),
                        EmailStatusBadgeClass = GetEmailStatusBadgeClass(firstEntry.EmailStatus),
                        EmailReason = firstEntry.EmailReason,
                        Entries = g.OrderBy(e => e.Sequence).Select(e => new DigestEntryViewModel
                        {
                            EntryId = e.Id,
                            IsBypass = e.Sequence == 0,
                            HasCampaign = e.CampaignId.HasValue,
                            CampaignTitle = e.Campaign?.Title,
                            DisplayAffinityScore = e.Sequence > 0 ? "0.00" : "-"
                        }).ToList()
                    };
                }).ToList()
        };

        return viewModel;
    }

    #endregion

    #region Batch processing business logic

    public async Task ProcessAsync(Guid batchId)
    {
        var executionTime = DateTime.UtcNow;

        var digestBatchInfo = await repository.GetDigestBatchByIdAsync(batchId) ?? throw new InvalidOperationException("Unable to find batch record in DB");

        var users = await repository.GetUsersEligibleForDigestAsync(executionTime, 10);
        digestBatchInfo.UserCount = users.Count;
        if (users.Count == 0)
        {
            logger.LogInformation("No users eligible for digest processing at this time.");
            digestBatchInfo.Status = DigestBatchStatus.Failed;
            digestBatchInfo.StatusUpdatedAt = executionTime;
            await repository.SaveChangesAsync();
            return;
        }

        var activeCampaigns = await repository.GetActiveCampaignsAsync();
        digestBatchInfo.CampaignCount = activeCampaigns.Count;
        if (activeCampaigns.Count == 0)
        {
            logger.LogInformation("No active campaigns to include in digest.");
            digestBatchInfo.Status = DigestBatchStatus.Failed;
            digestBatchInfo.StatusUpdatedAt = executionTime;
            await repository.SaveChangesAsync();
            return;
        }

        digestBatchInfo.Status = DigestBatchStatus.Processing;
        await repository.SaveChangesAsync();

        var userIds = users.Select(u => u.Id).ToList();

        var allPastVisits = await repository.GetPastVisitsForUsersAsync(userIds);
        var allPastDonations = await repository.GetPastDonationsForUsersAsync(userIds);

        var relevantCampaignIds = new HashSet<Guid>(
            allPastVisits.Select(v => v.CampaignId)
            .Concat(allPastDonations.Select(d => d.CampaignId))
        );
        var campaignSummaries = await repository.GetCampaignSummariesAsync(relevantCampaignIds);
        var campaignUrgencyScores = activeCampaigns.ToDictionary(c => c.Id, c => CalculateCampaignUrgencyScore(c, executionTime));

        var visitsGrouped = allPastVisits.ToLookup(v => v.UserId);
        var donationsGrouped = allPastDonations.ToLookup(d => d.UserId);

        foreach (var user in users)
        {
            try
            {
                var userInteractions = visitsGrouped[user.Id].Concat(donationsGrouped[user.Id]);
                var affinityProfile = BuildProfile(userInteractions, campaignSummaries);
                var digestCampaigns = GetTopCampaignsForUser(affinityProfile, activeCampaigns, campaignUrgencyScores);

                var emailId = Guid.NewGuid();
                await UpdateDigestBatch(digestBatchInfo, user, digestCampaigns, emailId);
                await SendDigestEmailAsync(user, digestCampaigns, emailId);
                await Task.Delay(250);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process digest for user {UserId}", user.Id);
            }
        }

        digestBatchInfo.Status = DigestBatchStatus.Processed;
        digestBatchInfo.StatusUpdatedAt = executionTime;
        await repository.SaveChangesAsync();
    }

    public async Task UpdateDigestBatch(DigestBatch digestBatchInfo, ApplicationUser user, IEnumerable<Campaign> digestCampaigns, Guid emailId)
    {
        if (!digestCampaigns.Any())
        {
            digestBatchInfo.Entries.Add(new DigestEntry
            {
                Id = Guid.NewGuid(),
                DigestBatchId = digestBatchInfo.Id,
                UserId = user.Id,
                CampaignId = null,
                EmailId = null,
                EmailStatus = DigestEmailStatus.Bypass,
                Sequence = 0
            });
        }
        else
        {
            int seq = 1;
            foreach (var campaign in digestCampaigns)
            {
                digestBatchInfo.Entries.Add(new DigestEntry
                {
                    Id = Guid.NewGuid(),
                    DigestBatchId = digestBatchInfo.Id,
                    UserId = user.Id,
                    CampaignId = campaign.Id,
                    EmailId = emailId,
                    EmailStatus = DigestEmailStatus.Initial,
                    SentAt = DateTime.UtcNow,
                    Sequence = seq++
                });
            }
        }

        await repository.SaveChangesAsync();
    }


    public IEnumerable<Campaign> GetTopCampaignsForUser(UserAffinityProfile affinityProfile,
                                                        List<Campaign> activeCampaigns,
                                                        Dictionary<Guid, double> campaignUrgencyScores)
    {
        return activeCampaigns.Select(campaign => new CampaignScore
        {
            Campaign = campaign,
            Score = campaignUrgencyScores[campaign.Id] + CalculateAffinityScore(affinityProfile, campaign)
        })
        .OrderByDescending(cs => cs.Score)
        .Where(cs => cs.Score > 0)
        .Take(3)
        .Select(cs => cs.Campaign);
    }

    public async Task SendDigestEmailAsync(ApplicationUser user, IEnumerable<Campaign> digestCampaigns, Guid emailId)
    {
        if (!digestCampaigns.Any())
        {
            return;
        }

        var viewModel = new CampaignDigestEmailViewModel { Campaigns = digestCampaigns.Select(MapCampaignToDisplayItem) };
        var subject = templateService.GenerateSubject(viewModel);
        var htmlBody = templateService.RenderHtmlBody(viewModel);
        await emailService.SendEmailAsync(user.Email!, subject, htmlBody, emailId.ToString());
    }

    public UserAffinityProfile BuildProfile(IEnumerable<UserCampaignInteractionDto> interactions,
                                            Dictionary<Guid, CampaignSummaryContext> campaignSummaries)
    {
        var profile = new UserAffinityProfile();

        foreach (var interaction in interactions)
        {
            if (campaignSummaries.TryGetValue(interaction.CampaignId, out var campaignInfo))
            {
                double score = 0;

                if (interaction.VisitCount > 0)
                {
                    score += interaction.VisitCount * VisitWeight;
                }

                if (interaction.DonationAmount > 0)
                {
                    double amountScore = Convert.ToDouble(interaction.DonationAmount) * DonationAmountMultiplier;
                    score += DonationBaseWeight + amountScore;
                }

                if (score > 0)
                {
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

    private static void AddScore<TKey>(Dictionary<TKey, double> dictionary, TKey key, double scoreToAdd) where TKey : notnull
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

    #endregion

    #region Email event & status updating

    public async Task OnEmailReceivedAsync(EmailEvent e)
    {
        if (string.IsNullOrEmpty(e.MessageId) || !Guid.TryParse(e.MessageId, out var emailId))
            return;

        var status = e.Status switch
        {
            EmailStatus.Sent => DigestEmailStatus.Sent,
            EmailStatus.Delivered => DigestEmailStatus.Sent,
            EmailStatus.Opened => DigestEmailStatus.Open,
            EmailStatus.Clicked => DigestEmailStatus.Click,
            EmailStatus.Bounced => DigestEmailStatus.Bounce,
            EmailStatus.Spam => DigestEmailStatus.Spam,
            _ => DigestEmailStatus.Unknown
        };

        await repository.UpdateDigestEntryStatusAsync(emailId, status, e.Reason);
    }

    #endregion
}
