// Refund-side counterpart to DonationContracts.cs. RefundResult variants and the
// pure state-transition helpers (ApplyRefund / BuildRefundLog) used to live on
// DonationService; they're moved here so the standalone DonationService.cs can
// be deleted while keeping refund tests and the admin refund page working.

using FundRaisingAssignment.Application.Models;

namespace FundRaisingAssignment.Application.Services;

public abstract record RefundResult
{
    public sealed record Success(Donation Donation, Campaign? Campaign, RefundLog Log) : RefundResult;
    public sealed record DonationNotFound : RefundResult;
    public sealed record NotRefundable(DonationStatus CurrentStatus) : RefundResult;
    public sealed record TransactionFailed(Exception Exception) : RefundResult;
}

internal static class Refund
{
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
