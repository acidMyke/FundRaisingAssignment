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

    [Fact]
    public void ApplyRefund_SetsStatusToRefunded_AndDeductsFromCampaign()
    {
        var (donation, campaign) = Sample(donationAmount: 100m, campaignCurrent: 500m);

        DonationService.ApplyRefund(donation, campaign, "admin@example.com", "donor request",
            new DateTime(2026, 5, 9, 10, 0, 0, DateTimeKind.Utc));

        Assert.Equal(DonationStatus.Refunded, donation.Status);
        Assert.Equal(400m, campaign.CurrentAmount);
        Assert.Contains("Refunded by admin@example.com", donation.Notes ?? "");
        Assert.Contains("donor request",                 donation.Notes ?? "");
    }

    [Fact]
    public void ApplyRefund_FloorsCampaignAmountAtZero()
    {
        var (donation, campaign) = Sample(donationAmount: 1000m, campaignCurrent: 50m);

        DonationService.ApplyRefund(donation, campaign, "admin", null, DateTime.UtcNow);

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

        DonationService.ApplyRefund(donation, campaign, "admin", null, DateTime.UtcNow);

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

        DonationService.ApplyRefund(donation, campaign, "admin", null, DateTime.UtcNow);

        Assert.Equal(1950m, campaign.CurrentAmount);
        Assert.Equal(CampaignStatus.Completed, campaign.Status);
    }

    [Fact]
    public void ApplyRefund_AppendsNoteWithoutClobberingExisting()
    {
        var (donation, campaign) = Sample();
        donation.Notes = "Existing note";

        DonationService.ApplyRefund(donation, campaign, "admin", "reason",
            new DateTime(2026, 5, 9, 10, 0, 0, DateTimeKind.Utc));

        Assert.StartsWith("Existing note", donation.Notes);
        Assert.Contains("Refunded by admin", donation.Notes);
    }

    [Fact]
    public void ApplyRefund_HandlesMissingCampaignGracefully()
    {
        var (donation, _) = Sample();

        DonationService.ApplyRefund(donation, null, "admin", null, DateTime.UtcNow);

        Assert.Equal(DonationStatus.Refunded, donation.Status);
        Assert.Contains("Refunded by admin", donation.Notes ?? "");
    }
}
