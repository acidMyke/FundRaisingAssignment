using FundRaisingAssignment.Application.Hubs;
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
                                   IDigestJobQueue digestJobQueue,
                                   IDigestSyncPublisher syncPublisher) : ICampaignDigestService, IEmailEventListener
{
    public class CampaignAffinityScore
    {
        public Campaign Campaign { get; set; } = null!;
        public double AffinityScore { get; set; }
        public double UrgencyScore { get; set; }
        public double TotalScore => AffinityScore + UrgencyScore;
    }

    public class UserAffinityProfile
    {
        public Dictionary<CampaignCategory, double> CategoryAffinities { get; } = [];
        public Dictionary<Guid, double> OwnerAffinities { get; } = [];
    }

    private const double MaxVisitScore = 10.0;
    private const double MaxDonationScore = 40.0;
    private const double DonationBaseScore = 15.0;
    private const double DonationMultiplier = 0.05;
    private const double VisitPointValue = 2.0;

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
                            DisplayAffinityScore = e.Sequence > 0 ? e.AffinityScore.ToString("F2") : "-"
                        }).ToList()
                    };
                }).ToList()
        };

        return viewModel;
    }

    private static DigestSyncData PrepareSyncData(DigestBatch batch) => new(batch.Id, batch.Status.ToString(), GetBatchStatusBadgeClass(batch.Status));

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
        syncPublisher.PublishBatchSync(PrepareSyncData(digestBatchInfo));

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
                var digestScores = GetTopCampaignsForUser(affinityProfile, activeCampaigns, campaignUrgencyScores).ToList();
                var digestCampaigns = digestScores.Select(ds => ds.Campaign).ToList();

                var emailId = Guid.NewGuid();
                await UpdateDigestBatch(digestBatchInfo, user, digestScores, emailId);
                syncPublisher.PublishDetailsSync(batchId);

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

        syncPublisher.PublishBatchSync(PrepareSyncData(digestBatchInfo));
    }

    public async Task UpdateDigestBatch(DigestBatch digestBatchInfo, ApplicationUser user, List<CampaignAffinityScore> digestScores, Guid emailId)
    {
        var entries = new List<DigestEntry>();
        if (digestScores.Count == 0)
        {
            entries.Add(new DigestEntry
            {
                Id = Guid.NewGuid(),
                DigestBatchId = digestBatchInfo.Id,
                UserId = user.Id,
                CampaignId = null,
                EmailId = null,
                EmailStatus = DigestEmailStatus.Bypass,
                Sequence = 0,
                AffinityScore = 0
            });
        }
        else
        {
            int seq = 1;
            foreach (var score in digestScores)
            {
                entries.Add(new DigestEntry
                {
                    Id = Guid.NewGuid(),
                    DigestBatchId = digestBatchInfo.Id,
                    UserId = user.Id,
                    CampaignId = score.Campaign.Id,
                    EmailId = emailId,
                    EmailStatus = DigestEmailStatus.Initial,
                    SentAt = DateTime.UtcNow,
                    Sequence = seq++,
                    AffinityScore = score.AffinityScore
                });
            }
        }

        await repository.AddDigestEntriesAsync(entries);
    }


    public IEnumerable<CampaignAffinityScore> GetTopCampaignsForUser(UserAffinityProfile affinityProfile,
                                                        List<Campaign> activeCampaigns,
                                                        Dictionary<Guid, double> campaignUrgencyScores)
    {
        return activeCampaigns.Select(campaign => new CampaignAffinityScore
        {
            Campaign = campaign,
            AffinityScore = CalculateAffinityScore(affinityProfile, campaign),
            UrgencyScore = campaignUrgencyScores[campaign.Id]
        })
        .Where(cs => cs.TotalScore > 0)
        .OrderByDescending(cs => cs.TotalScore)
        .Take(3);
    }

    public async Task SendDigestEmailAsync(ApplicationUser user, List<Campaign> digestCampaigns, Guid emailId)
    {
        if (digestCampaigns.Count == 0)
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
        var now = DateTime.UtcNow;

        foreach (var interaction in interactions)
        {
            if (campaignSummaries.TryGetValue(interaction.CampaignId, out var campaignInfo))
            {
                double score = 0;

                if (interaction.VisitCount > 0)
                {
                    score += Math.Min(MaxVisitScore, interaction.VisitCount * VisitPointValue);
                }

                if (interaction.DonationAmount > 0)
                {
                    double donationScore = DonationBaseScore + (double)interaction.DonationAmount * DonationMultiplier;
                    score += Math.Min(MaxDonationScore, donationScore);
                }

                if (score > 0)
                {
                    score *= CalculateTimeFactor(interaction.InteractionDate, now);

                    AddScore(profile.CategoryAffinities, campaignInfo.Category, score);
                    AddScore(profile.OwnerAffinities, campaignInfo.OwnerId, score);
                }
            }
        }

        return profile;
    }

    private static double CalculateTimeFactor(DateTime interactionDate, DateTime now)
    {
        var age = now - interactionDate;
        if (age.TotalDays <= 7) return 1.0;
        if (age.TotalDays <= 28) return 0.5;
        return 0.1;
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

        if ((now - campaign.CreatedAt).TotalDays <= 7) score += 20;

        if (campaign.EndDate.HasValue)
        {
            var timeRemaining = campaign.EndDate.Value - now;
            if (timeRemaining.TotalHours > 0 && timeRemaining.TotalDays <= 3) score += 20;
        }

        if (campaign.FundingGoal > 0)
        {
            var percent = campaign.CurrentAmount / campaign.FundingGoal;
            if (percent >= 0.75m && percent < 1.0m) score += 15;
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

        var batchId = await repository.GetDigestBatchIdByEmailIdAsync(emailId);
        if (batchId.HasValue)
        {
            await repository.UpdateDigestEntryStatusAsync(emailId, status, e.Reason);
            syncPublisher.PublishDetailsSync(batchId.Value);
        }
    }

    #endregion
}
