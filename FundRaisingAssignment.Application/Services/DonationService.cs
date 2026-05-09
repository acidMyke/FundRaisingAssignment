using FundRaisingAssignment.Application.Data;
using FundRaisingAssignment.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace FundRaisingAssignment.Application.Services;

public sealed record MakeDonationInput(
    Guid CampaignId,
    decimal Amount,
    string? Message,
    bool IsAnonymous);

public abstract record DonationResult
{
    public sealed record Success(Donation Donation, Campaign Campaign, bool GoalReached) : DonationResult;
    public sealed record CampaignNotFound(Guid CampaignId) : DonationResult;
    public sealed record CampaignNotActive(CampaignStatus CurrentStatus) : DonationResult;
    public sealed record DeadlinePassed : DonationResult;
    public sealed record TransactionFailed(Exception Exception) : DonationResult;
}

public abstract record RefundResult
{
    public sealed record Success(Donation Donation, Campaign? Campaign, RefundLog Log) : RefundResult;
    public sealed record DonationNotFound : RefundResult;
    public sealed record NotRefundable(DonationStatus CurrentStatus) : RefundResult;
    public sealed record TransactionFailed(Exception Exception) : RefundResult;
}

public sealed class DonationService
{
    private readonly ApplicationDbContext context;
    private readonly ILogger<DonationService> logger;

    public DonationService(ApplicationDbContext context, ILogger<DonationService> logger)
    {
        this.context = context;
        this.logger = logger;
    }

    /// <summary>
    /// Retrieves all donations for a given user, including related campaign info.
    /// </summary>
    public async Task<List<Donation>> GetDonationsByUserAsync(Guid userId, CancellationToken ct = default)
    {
        return await context.Donations
            .Where(d => d.UserId == userId)
            .Include(d => d.Campaign)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(ct);
    }
    public async Task<DonationResult> MakeDonationAsync(
        Guid donorUserId,
        MakeDonationInput input,
        CancellationToken ct)
    {
        var campaign = await context.Campaigns
            .FirstOrDefaultAsync(c => c.Id == input.CampaignId, ct);

        if (campaign is null)
            return new DonationResult.CampaignNotFound(input.CampaignId);

        if (campaign.Status != CampaignStatus.Active)
            return new DonationResult.CampaignNotActive(campaign.Status);

        // EndDate is nullable in merged model
        if (campaign.EndDate.HasValue && campaign.EndDate.Value < DateTime.UtcNow)
            return new DonationResult.DeadlinePassed();

        await using var tx = await context.Database.BeginTransactionAsync(ct);
        try
        {
            var donation = new Donation
            {
                Id = Guid.NewGuid(),          // merged model uses Id as PK
                CampaignId = campaign.Id,
                UserId = donorUserId,              // merged model uses UserId
                Amount = input.Amount,
                Message = input.Message,
                IsAnonymous = input.IsAnonymous,
                DonorEmail = input.IsAnonymous ? "Anonymous" : string.Empty,
                Status = DonationStatus.Completed,
                CreatedAt = DateTime.UtcNow
            };

            await context.Donations.AddAsync(donation, ct);
            campaign.CurrentAmount += input.Amount;

            bool goalReached = false;
            if (campaign.CurrentAmount >= campaign.TargetAmount
                && campaign.Status == CampaignStatus.Active)
            {
                campaign.Status = CampaignStatus.Completed;
                goalReached = true;
                logger.LogInformation(
                    "Campaign {CampaignId} reached its goal and was auto-completed.", campaign.Id);
            }

            await context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            logger.LogInformation(
                "Donation {DonationId} of {Amount} recorded for campaign {CampaignId} by donor {DonorId}.",
                donation.Id, donation.Amount, campaign.Id, donorUserId);

            return new DonationResult.Success(donation, campaign, goalReached);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            logger.LogError(ex,
                "Failed to process donation for campaign {CampaignId} by user {UserId}.",
                input.CampaignId, donorUserId);
            return new DonationResult.TransactionFailed(ex);
        }
    }

    /// <summary>
    /// Marks a Completed donation as Refunded, deducts the amount from the
    /// campaign's CurrentAmount (floored at 0), re-opens the campaign if it
    /// had been auto-Completed and now falls back below its target, and
    /// writes a structured RefundLog row for audit/reporting.
    /// </summary>
    public async Task<RefundResult> RefundDonationAsync(
        Guid donationId,
        Guid? adminId,
        string adminLabel,
        string? reason,
        CancellationToken ct)
    {
        var donation = await context.Donations
            .FirstOrDefaultAsync(d => d.Id == donationId, ct);

        if (donation is null)
            return new RefundResult.DonationNotFound();

        if (donation.Status != DonationStatus.Completed)
            return new RefundResult.NotRefundable(donation.Status);

        var campaign = await context.Campaigns
            .FirstOrDefaultAsync(c => c.Id == donation.CampaignId, ct);

        var now = DateTime.UtcNow;

        await using var tx = await context.Database.BeginTransactionAsync(ct);
        try
        {
            ApplyRefund(donation, campaign);

            var log = BuildRefundLog(donation, adminId, adminLabel, reason, now);
            await context.RefundLogs.AddAsync(log, ct);

            await context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            logger.LogInformation(
                "Donation {DonationId} ({Amount}) refunded by {Admin}; campaign {CampaignId} adjusted; refund log {RefundId}.",
                donation.Id, donation.Amount, adminLabel, donation.CampaignId, log.Id);

            return new RefundResult.Success(donation, campaign, log);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            logger.LogError(ex,
                "Failed to refund donation {DonationId} by {Admin}.",
                donationId, adminLabel);
            return new RefundResult.TransactionFailed(ex);
        }
    }

    /// <summary>
    /// Pure refund state-transition logic — extracted so unit tests can
    /// exercise it without spinning up a database. Mutates donation and
    /// campaign in place; caller is responsible for persisting + writing
    /// the audit log row.
    /// </summary>
    internal static void ApplyRefund(Donation donation, Campaign? campaign)
    {
        donation.Status = DonationStatus.Refunded;

        if (campaign is null) return;

        campaign.CurrentAmount = Math.Max(0m, campaign.CurrentAmount - donation.Amount);

        // If the refund drops a previously auto-Completed campaign back under
        // its target, re-open it so it can keep accepting donations.
        if (campaign.Status == CampaignStatus.Completed
            && campaign.CurrentAmount < campaign.TargetAmount)
        {
            campaign.Status = CampaignStatus.Active;
        }
    }

    internal static RefundLog BuildRefundLog(
        Donation donation,
        Guid? adminId,
        string adminLabel,
        string? reason,
        DateTime utcNow) => new()
        {
            Id = Guid.NewGuid(),
            DonationId = donation.Id,
            CampaignId = donation.CampaignId,
            AdminId = adminId,
            AdminLabel = adminLabel,
            Amount = donation.Amount,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            RefundedAt = utcNow,
        };
}
