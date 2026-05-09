using FundRaisingAssignment.Application.Data;
using FundRaisingAssignment.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace FundRaisingAssignment.Application.Services;

/// <summary>
/// Merged CampaignService: Karthik's SearchCampaigns + Josh's full campaign lifecycle.
/// Implements ICampaignService (interface).
/// </summary>
public class CampaignService(ApplicationDbContext db, ILogger<CampaignService> logger) : ICampaignService
{
    private readonly ApplicationDbContext _db = db;
    private readonly ILogger<CampaignService> _logger = logger;

    // ── Campaign CRUD ──────────────────────────────────────────────────────────

    public async Task<Campaign> CreateCampaignAsync(Campaign campaign)
    {
        campaign.Id = Guid.NewGuid();
        campaign.CreatedAt = DateTime.UtcNow;
        campaign.Status = CampaignStatus.Draft;
        campaign.TargetAmount = campaign.FundingGoal;
        _db.Campaigns.Add(campaign);
        await _db.SaveChangesAsync();
        return campaign;
    }

    public async Task<Campaign> UpdateCampaignDetailsAsync(Campaign updated)
    {
        var c = await _db.Campaigns.FindAsync(updated.Id)
            ?? throw new InvalidOperationException("Campaign not found.");
        c.Title = updated.Title;
        c.ShortDescription = updated.ShortDescription;
        c.Description = updated.Description;
        c.Category = updated.Category;
        c.Location = updated.Location;
        c.CoverImageUrl = updated.CoverImageUrl;
        await _db.SaveChangesAsync();
        return c;
    }

    public async Task<Campaign> SaveGoalAndDeadlineAsync(
        decimal goalAmount, DateTime deadlineDate, Guid ownerId)
    {
        var c = new Campaign
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Title = $"Campaign {DateTime.Now:yyyy-MM-dd HH:mm}",
            ShortDescription = "Created via Set Funding Goal.",
            Description = "Created via Set Funding Goal.",
            Status = CampaignStatus.Draft,
            StartDate = DateTime.Now,
            CreatedAt = DateTime.UtcNow,
        };
        c.UpdateGoalAndDeadline(goalAmount, deadlineDate);
        _db.Campaigns.Add(c);
        await _db.SaveChangesAsync();
        return c;
    }

    public async Task<Campaign> UpdateGoalAndDeadlineAsync(
        Guid campaignId, decimal goalAmount, DateTime deadlineDate, Guid ownerId)
    {
        var c = await _db.Campaigns
            .FirstOrDefaultAsync(x => x.Id == campaignId && x.OwnerId == ownerId)
            ?? throw new InvalidOperationException("Campaign not found or access denied.");
        c.UpdateGoalAndDeadline(goalAmount, deadlineDate);
        await _db.SaveChangesAsync();
        return c;
    }

    // ── Search (Karthik's SearchCampaigns adapted to async) ───────────────────

    public async Task<IReadOnlyList<Campaign>> SearchCampaignsAsync(
        string? keyword, string? category, string? location)
    {
        keyword = keyword?.Trim();
        category = category?.Trim();
        location = location?.Trim();

        var query = _db.Campaigns.Include(c => c.Owner).AsQueryable();

        if (!string.IsNullOrEmpty(keyword))
            query = query.Where(c => c.Title.Contains(keyword) || c.Description.Contains(keyword));

        if (!string.IsNullOrEmpty(location))
            query = query.Where(c => (c.Location ?? "").Contains(location));

        // Category search: match by display name or enum value string
        if (!string.IsNullOrEmpty(category))
        {
            if (Enum.TryParse<CampaignCategory>(category, true, out var catEnum))
                query = query.Where(c => c.Category == catEnum);
        }

        return await query.ToListAsync();
    }

    // ── Fundraiser workflow ────────────────────────────────────────────────────

    public async Task<Campaign> SubmitForReviewAsync(Guid campaignId, Guid ownerId)
    {
        var c = await _db.Campaigns
            .FirstOrDefaultAsync(x => x.Id == campaignId && x.OwnerId == ownerId)
            ?? throw new InvalidOperationException("Campaign not found or access denied.");
        c.SubmitForReview();
        await _db.SaveChangesAsync();
        return c;
    }

    // ── Admin workflow ─────────────────────────────────────────────────────────

    public async Task<Campaign> PublishCampaignAsync(Guid campaignId)
    {
        var c = await _db.Campaigns.FindAsync(campaignId)
            ?? throw new InvalidOperationException("Campaign not found.");
        c.PublishCampaign();
        await _db.SaveChangesAsync();
        return c;
    }

    public async Task<Campaign> FlagCampaignByAdminAsync(Guid campaignId, string reason)
    {
        var c = await _db.Campaigns.FindAsync(campaignId)
            ?? throw new InvalidOperationException("Campaign not found.");
        c.FlagCampaignByAdmin(reason);
        await _db.SaveChangesAsync();
        return c;
    }

    public async Task<Campaign> PauseCampaignAsync(Guid campaignId, string reason)
    {
        var c = await _db.Campaigns.FindAsync(campaignId)
            ?? throw new InvalidOperationException("Campaign not found.");
        c.PauseCampaign(reason);
        await _db.SaveChangesAsync();
        return c;
    }

    public async Task<Campaign> TerminateCampaignAsync(Guid campaignId, string reason)
    {
        var c = await _db.Campaigns.FindAsync(campaignId)
            ?? throw new InvalidOperationException("Campaign not found.");
        c.TerminateCampaign(reason);
        await _db.SaveChangesAsync();
        return c;
    }

    public async Task<Campaign> ReleaseCampaignAsync(Guid campaignId)
    {
        var c = await _db.Campaigns.FindAsync(campaignId)
            ?? throw new InvalidOperationException("Campaign not found.");
        c.ReleaseCampaign();
        await _db.SaveChangesAsync();
        return c;
    }

    // ── Queries ────────────────────────────────────────────────────────────────

    public Task<Campaign?> GetCampaignAsync(Guid id) =>
        _db.Campaigns.Include(c => c.Owner).FirstOrDefaultAsync(c => c.Id == id);

    public Task<Campaign?> GetCampaignDetailsAsync(Guid id) =>
        _db.Campaigns.Include(c => c.Owner).FirstOrDefaultAsync(c => c.Id == id);

    public async Task<IReadOnlyList<Campaign>> GetCampaignsByOwnerAsync(Guid ownerId) =>
        await _db.Campaigns
            .Where(c => c.OwnerId == ownerId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

    public async Task<IReadOnlyList<Campaign>> GetAllCampaignsAsync() =>
        await _db.Campaigns.Include(c => c.Owner)
            .OrderByDescending(c => c.CreatedAt).ToListAsync();

    public async Task<IReadOnlyList<Campaign>> GetPublicCampaignsAsync() =>
        await _db.Campaigns.Include(c => c.Owner)
            .Where(c => c.Status == CampaignStatus.Active)
            .OrderByDescending(c => c.PublishedAt ?? c.CreatedAt)
            .ToListAsync();

    public async Task<IReadOnlyList<Campaign>> GetPendingReviewCampaignsAsync() =>
        await _db.Campaigns.Include(c => c.Owner)
            .Where(c => c.Status == CampaignStatus.PendingReview)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();

    public async Task<IReadOnlyList<Campaign>> GetFlaggedCampaignsAsync() =>
        await _db.Campaigns.Include(c => c.Owner)
            .Where(c => c.Status == CampaignStatus.Flagged)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

    // ── BCE Diagram 2 ──────────────────────────────────────────────────────────

    public async Task<Campaign> ApproveCampaignAsync(Guid id)
    {
        var c = await _db.Campaigns.FindAsync(id)
            ?? throw new InvalidOperationException("Campaign not found.");
        c.ApproveCampaignStatus();
        await _db.SaveChangesAsync();
        return c;
    }

    public async Task<Campaign> RemoveCampaignAsync(Guid id, string reason)
    {
        var c = await _db.Campaigns.FindAsync(id)
            ?? throw new InvalidOperationException("Campaign not found.");
        c.RemoveCampaignStatus(reason);
        await _db.SaveChangesAsync();
        return c;
    }

    public async Task<Campaign> FlagCampaignAsync(Guid id, string reason)
    {
        var c = await _db.Campaigns.FindAsync(id)
            ?? throw new InvalidOperationException("Campaign not found.");
        c.FlagCampaignByAdmin(reason);
        await _db.SaveChangesAsync();
        return c;
    }

    public async Task<FundRaiserNotification> NotifyFundRaiserAsync(Guid campaignId, string outcome)
    {
        var n = new FundRaiserNotification { CampaignId = campaignId };
        n.SendReviewNotification(outcome);
        _db.FundRaiserNotifications.Add(n);
        await _db.SaveChangesAsync();
        return n;
    }

    // ── Reviews ────────────────────────────────────────────────────────────────

    public async Task<CampaignReview> AddReviewAsync(
        Guid campaignId, Guid reviewerId, string reviewerEmail, int stars, string? comment)
    {
        var review = new CampaignReview
        {
            CampaignId = campaignId,
            ReviewerId = reviewerId,
            ReviewerEmail = reviewerEmail,
            Stars = stars,
            Comment = comment,
            CreatedAt = DateTime.UtcNow
        };
        _db.CampaignReviews.Add(review);

        var campaign = await _db.Campaigns.FindAsync(campaignId)
            ?? throw new InvalidOperationException("Campaign not found.");

        await _db.SaveChangesAsync();

        var stats = await _db.CampaignReviews
            .Where(r => r.CampaignId == campaignId)
            .GroupBy(r => r.CampaignId)
            .Select(g => new { Avg = g.Average(r => (double)r.Stars), Count = g.Count() })
            .FirstOrDefaultAsync();

        if (stats != null)
            campaign.RecalculateRating(stats.Avg, stats.Count);

        campaign.FlagFromLowReview(stars, reviewerEmail);

        await _db.SaveChangesAsync();
        return review;
    }

    public async Task<IReadOnlyList<CampaignReview>> GetCampaignReviewsAsync(Guid campaignId) =>
        await _db.CampaignReviews
            .Where(r => r.CampaignId == campaignId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

    public Task<bool> HasUserReviewedAsync(Guid campaignId, Guid userId) =>
        _db.CampaignReviews.AnyAsync(r => r.CampaignId == campaignId && r.ReviewerId == userId);

    // ── Donations (canonical consolidated flow) ───────────────────────────────

    public async Task<DonationResult> DonateAsync(MakeDonationInput input, CancellationToken ct = default)
    {
        if (input.Amount <= 0m)
            return new DonationResult.InvalidAmount("Donation amount must be greater than $0.");
        if (input.Amount > 1_000_000m)
            return new DonationResult.InvalidAmount("Donation amount cannot exceed $1,000,000.");

        var campaign = await _db.Campaigns
            .FirstOrDefaultAsync(c => c.Id == input.CampaignId, ct);

        if (campaign is null)
            return new DonationResult.CampaignNotFound();

        if (campaign.Status != CampaignStatus.Active)
            return new DonationResult.CampaignNotActive(campaign.Status);

        if (campaign.EndDate.HasValue && campaign.EndDate.Value < DateTime.UtcNow)
            return new DonationResult.DeadlinePassed();

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var donation = new Donation
            {
                Id = Guid.NewGuid(),
                CampaignId = campaign.Id,
                UserId = input.UserId,
                DonorEmail = input.IsAnonymous
                    ? "Anonymous"
                    : (string.IsNullOrWhiteSpace(input.DonorEmail) ? "Guest" : input.DonorEmail),
                Amount = input.Amount,
                Message = input.Message,
                IsAnonymous = input.IsAnonymous,
                Status = DonationStatus.Completed,
                CreatedAt = DateTime.UtcNow,
                ReceiptNumber = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
            };

            await _db.Donations.AddAsync(donation, ct);
            campaign.CurrentAmount += input.Amount;

            bool goalReached = false;
            if (campaign.CurrentAmount >= campaign.TargetAmount
                && campaign.Status == CampaignStatus.Active)
            {
                campaign.Status = CampaignStatus.Completed;
                goalReached = true;
                _logger.LogInformation(
                    "Campaign {CampaignId} reached its goal and was auto-completed.", campaign.Id);
            }

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            _logger.LogInformation(
                "Donation {DonationId} of {Amount} recorded for campaign {CampaignId} by donor {DonorId} ({DonorEmail}).",
                donation.Id, donation.Amount, campaign.Id,
                input.UserId?.ToString() ?? "guest", donation.DonorEmail);

            // Attach the loaded campaign so callers can read title/etc. without a reload.
            donation.Campaign = campaign;
            return new DonationResult.Success(donation, goalReached);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            _logger.LogError(ex,
                "Failed to process donation for campaign {CampaignId} by donor {DonorId}.",
                input.CampaignId, input.UserId?.ToString() ?? "guest");
            throw;
        }
    }

    public async Task<RefundResult> RefundDonationAsync(
        Guid donationId, Guid? adminId, string adminLabel, string? reason, CancellationToken ct = default)
    {
        var donation = await _db.Donations
            .FirstOrDefaultAsync(d => d.Id == donationId, ct);

        if (donation is null)
            return new RefundResult.DonationNotFound();

        if (donation.Status != DonationStatus.Completed)
            return new RefundResult.NotRefundable(donation.Status);

        var campaign = await _db.Campaigns
            .FirstOrDefaultAsync(c => c.Id == donation.CampaignId, ct);

        var now = DateTime.UtcNow;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            Refund.ApplyRefund(donation, campaign);

            var log = Refund.BuildRefundLog(donation, adminId, adminLabel, reason, now);
            await _db.RefundLogs.AddAsync(log, ct);

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            _logger.LogInformation(
                "Donation {DonationId} ({Amount}) refunded by {Admin}; campaign {CampaignId} adjusted; refund log {RefundId}.",
                donation.Id, donation.Amount, adminLabel, donation.CampaignId, log.Id);

            return new RefundResult.Success(donation, campaign, log);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            _logger.LogError(ex,
                "Failed to refund donation {DonationId} by {Admin}.",
                donationId, adminLabel);
            return new RefundResult.TransactionFailed(ex);
        }
    }

    public async Task<IReadOnlyList<Donation>> GetCampaignDonationsAsync(Guid campaignId) =>
        await _db.Donations
            .Where(d => d.CampaignId == campaignId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();

    public async Task<IReadOnlyList<Donation>> GetDonationsByUserAsync(Guid userId) =>
        await _db.Donations
            .Include(d => d.Campaign)
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();

    public Task<decimal> GetTotalDonatedAsync(Guid campaignId) =>
        _db.Donations.Where(d => d.CampaignId == campaignId).SumAsync(d => d.Amount);

    /// Gets the top 10 donations for a campaign, ordered by amount (highest first), including user info.
    public async Task<IReadOnlyList<Donation>> GetTopDonationsAsync(Guid campaignId, int count = 10) =>
        await _db.Donations
            .Where(d => d.CampaignId == campaignId)
            .Include(d => d.User)
            .OrderByDescending(d => d.Amount)
            .ThenBy(d => d.CreatedAt)
            .Take(count)
            .ToListAsync();
}
