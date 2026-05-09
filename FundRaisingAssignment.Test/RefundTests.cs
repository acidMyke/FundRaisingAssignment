using FundRaisingAssignment.Application.Models;
using FundRaisingAssignment.Application.Services;

namespace FundRaisingAssignment.Test;

public class RefundTests
{
    private static (Donation, Campaign) Sample(
        decimal donationAmount = 100m,
        decimal campaignCurrent = 500m,
        decimal campaignTarget = 1000m,
        CampaignStatus status = CampaignStatus.Active,
        DonationStatus donationStatus = DonationStatus.Completed)
    {
        var campaign = new Campaign
        {
            Id            = Guid.NewGuid(),
            Title         = "Test campaign",
            Description   = "Desc",
            FundingGoal   = campaignTarget,
            TargetAmount  = campaignTarget,
            CurrentAmount = campaignCurrent,
            Status        = status,
            OwnerId       = Guid.NewGuid(),
        };
        var donation = new Donation
        {
            Id         = Guid.NewGuid(),
            CampaignId = campaign.Id,
            Amount     = donationAmount,
            Status     = donationStatus,
        };
        return (donation, campaign);
    }

    // ---- ApplyRefund: state mutations -------------------------------------

    [Fact]
    public void ApplyRefund_SetsStatusToRefunded_AndDeductsFromCampaign()
    {
        var (donation, campaign) = Sample(donationAmount: 100m, campaignCurrent: 500m);

        DonationService.ApplyRefund(donation, campaign);

        Assert.Equal(DonationStatus.Refunded, donation.Status);
        Assert.Equal(400m, campaign.CurrentAmount);
    }

    [Fact]
    public void ApplyRefund_FloorsCampaignAmountAtZero()
    {
        var (donation, campaign) = Sample(donationAmount: 1000m, campaignCurrent: 50m);

        DonationService.ApplyRefund(donation, campaign);

        Assert.Equal(0m, campaign.CurrentAmount);
    }

    [Fact]
    public void ApplyRefund_ReopensCompletedCampaignWhenItFallsBelowTarget()
    {
        var (donation, campaign) = Sample(
            donationAmount: 200m,
            campaignCurrent: 1050m,
            campaignTarget: 1000m,
            status: CampaignStatus.Completed);

        DonationService.ApplyRefund(donation, campaign);

        Assert.Equal(850m, campaign.CurrentAmount);
        Assert.Equal(CampaignStatus.Active, campaign.Status);
    }

    [Fact]
    public void ApplyRefund_DoesNotReopenCompletedCampaignThatStaysAboveTarget()
    {
        var (donation, campaign) = Sample(
            donationAmount: 50m,
            campaignCurrent: 2000m,
            campaignTarget: 1000m,
            status: CampaignStatus.Completed);

        DonationService.ApplyRefund(donation, campaign);

        Assert.Equal(1950m, campaign.CurrentAmount);
        Assert.Equal(CampaignStatus.Completed, campaign.Status);
    }

    [Fact]
    public void ApplyRefund_LeavesNotesUntouched()
    {
        var (donation, campaign) = Sample();
        donation.Notes = "Existing donor message";

        DonationService.ApplyRefund(donation, campaign);

        // Audit goes to RefundLog, not Notes — so the donor's note is preserved.
        Assert.Equal("Existing donor message", donation.Notes);
    }

    [Fact]
    public void ApplyRefund_HandlesMissingCampaignGracefully()
    {
        var (donation, _) = Sample();

        DonationService.ApplyRefund(donation, null);

        Assert.Equal(DonationStatus.Refunded, donation.Status);
    }

    // ---- BuildRefundLog: snapshot of the audit row ------------------------

    [Fact]
    public void BuildRefundLog_CapturesAdminAmountAndReason()
    {
        var (donation, _) = Sample(donationAmount: 75m);
        var adminId = Guid.NewGuid();
        var when = new DateTime(2026, 5, 9, 10, 0, 0, DateTimeKind.Utc);

        var log = DonationService.BuildRefundLog(
            donation, adminId, "admin@example.com", "  donor request  ", when);

        Assert.Equal(donation.Id, log.DonationId);
        Assert.Equal(donation.CampaignId, log.CampaignId);
        Assert.Equal(adminId, log.AdminId);
        Assert.Equal("admin@example.com", log.AdminLabel);
        Assert.Equal(75m, log.Amount);
        Assert.Equal("donor request", log.Reason); // trimmed
        Assert.Equal(when, log.RefundedAt);
        Assert.NotEqual(Guid.Empty, log.Id);
    }

    [Fact]
    public void BuildRefundLog_NullReason_StoresNull()
    {
        var (donation, _) = Sample();

        var log = DonationService.BuildRefundLog(donation, null, "admin", null, DateTime.UtcNow);

        Assert.Null(log.Reason);
        Assert.Null(log.AdminId);
    }

    [Fact]
    public void BuildRefundLog_WhitespaceReason_StoresNull()
    {
        var (donation, _) = Sample();

        var log = DonationService.BuildRefundLog(donation, null, "admin", "   ", DateTime.UtcNow);

        Assert.Null(log.Reason);
    }
}
