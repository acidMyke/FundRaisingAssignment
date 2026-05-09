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

public sealed class DonationService(
    ApplicationDbContext context,
    ILogger<DonationService> logger)
{
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
                Id          = Guid.NewGuid(),          // merged model uses Id as PK
                CampaignId  = campaign.Id,
                UserId      = donorUserId,              // merged model uses UserId
                Amount      = input.Amount,
                Message     = input.Message,
                IsAnonymous = input.IsAnonymous,
                DonorEmail  = input.IsAnonymous ? "Anonymous" : string.Empty,
                Status      = DonationStatus.Completed,
                CreatedAt   = DateTime.UtcNow
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
}
