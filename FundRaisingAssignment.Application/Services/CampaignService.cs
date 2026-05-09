using FundRaisingAssignment.Application.Data;
using FundRaisingAssignment.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace FundRaisingAssignment.Application.Services;

/// <summary>
/// Merged CampaignService: Karthik's SearchCampaigns + Josh's full campaign lifecycle.
/// Implements ICampaignService (interface).
/// </summary>
public class CampaignService(ApplicationDbContext db) : ICampaignService
{
    private readonly ApplicationDbContext _db = db;

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

    // ── Donations (Josh ICampaignService flow) ────────────────────────────────

    public async Task<Donation> DonateAsync(
        Guid campaignId, Guid? donorId, string donorEmail,
        decimal amount, string? message, bool isAnonymous)
    {
        if (amount <= 0)
            throw new ArgumentException("Donation amount must be greater than $0.");

        var campaign = await _db.Campaigns.FindAsync(campaignId)
            ?? throw new InvalidOperationException("Campaign not found.");

        if (!campaign.AcceptsDonations)
            throw new InvalidOperationException("This campaign is not currently accepting donations.");

        var donation = new Donation
        {
            Id = Guid.NewGuid(),
            CampaignId = campaignId,
            UserId = donorId,         // maps DonorId → UserId in merged model
            DonorEmail = isAnonymous ? "Anonymous" : donorEmail,
            Amount = amount,
            Message = message,
            IsAnonymous = isAnonymous,
            Status = DonationStatus.Completed,
            CreatedAt = DateTime.UtcNow
        };
        _db.Donations.Add(donation);
        campaign.CurrentAmount += amount;
        await _db.SaveChangesAsync();
        return donation;
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
}
