using FundRaisingAssignment.Application.Interfaces;
using FundRaisingAssignment.Application.Interfaces.Repositories;
using FundRaisingAssignment.Application.Models;
using FundRaisingAssignment.Application.Models.ProcessingModels;
using FundRaisingAssignment.Application.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace FundRaisingAssignment.Test;

public class CampaignDigestServiceTests
{
    private readonly Mock<ICampaignDigestRepository> _mockRepository;
    private readonly Mock<ILogger<CampaignDigestService>> _mockLogger;
    private readonly Mock<ICampaignDigestEmailTemplateService> _mockTemplateService;
    private readonly Mock<IEmailService> _mockEmailService;
    private readonly CampaignDigestService _service;

    public CampaignDigestServiceTests()
    {
        _mockRepository = new Mock<ICampaignDigestRepository>();
        _mockLogger = new Mock<ILogger<CampaignDigestService>>();
        _mockTemplateService = new Mock<ICampaignDigestEmailTemplateService>();
        _mockEmailService = new Mock<IEmailService>();

        _service = new CampaignDigestService(
            _mockRepository.Object,
            _mockLogger.Object,
            _mockTemplateService.Object,
            _mockEmailService.Object
        );
    }


    [Fact]
    public async Task TriggerDigestProcessingAsync_NoUsers_DoesNotFetchCampaigns()
    {
        _mockRepository.Setup(r => r.GetUsersEligibleForDigestAsync(It.IsAny<DateTime>()))
            .ReturnsAsync([]);

        await _service.TriggerDigestProcessingAsync();

        _mockRepository.Verify(r => r.GetActiveCampaignsAsync(), Times.Never);
        _mockRepository.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task TriggerDigestProcessingAsync_NoCampaigns_DoesNotFetchHistory()
    {
        _mockRepository.Setup(r => r.GetUsersEligibleForDigestAsync(It.IsAny<DateTime>()))
            .ReturnsAsync([new ApplicationUser { Id = Guid.NewGuid() }]);
        _mockRepository.Setup(r => r.GetActiveCampaignsAsync())
            .ReturnsAsync([]);

        await _service.TriggerDigestProcessingAsync();

        _mockRepository.Verify(r => r.GetHistoryContextsForUsersAsync(It.IsAny<IEnumerable<Guid>>()), Times.Never);
        _mockRepository.Verify(r => r.SaveChangesAsync(), Times.Never);
    }
    [Theory]
    [InlineData(12, 0, 1000, 50)]
    [InlineData(48, 0, 1000, 30)]
    [InlineData(120, 0, 1000, 10)]
    [InlineData(-1, 0, 1000, 0)]
    [InlineData(200, 800, 1000, 35)]
    [InlineData(12, 800, 1000, 85)]
    public void CalculateCampaignUrgencyScore_ReturnsExpectedScore(int hoursRemaining, decimal currentAmount, decimal targetAmount, double expectedScore)
    {
        var now = DateTime.UtcNow;
        var campaign = new Campaign
        {
            EndDate = now.AddHours(hoursRemaining),
            CurrentAmount = currentAmount,
            TargetAmount = targetAmount
        };

        var score = _service.CalculateCampaignUrgencyScore(campaign, now);

        Assert.Equal(expectedScore, score);
    }

    [Fact]
    public void MapCampaignToDisplayItem_WithShortDescription_UsesShortDescription()
    {
        var campaign = new Campaign
        {
            Id = Guid.NewGuid(),
            Title = "Test Title",
            ShortDescription = "Short summary",
            Description = new string('A', 200),
            FundingGoal = 2500m,
            CurrentAmount = 1250m
        };

        var result = _service.MapCampaignToDisplayItem(campaign);

        Assert.Equal(campaign.Id, result.Id);
        Assert.Equal("Test Title", result.Title);
        Assert.Equal("Short summary", result.SummaryText);
        Assert.Equal("2,500 USD", result.FormattedGoal);
        Assert.Equal("1,250 USD", result.FormattedRaised);
        Assert.Equal(50m, result.ProgressPercentage);
    }

    [Fact]
    public void MapCampaignToDisplayItem_NoShortDescription_UsesFullDescription()
    {
        var campaign = new Campaign
        {
            Id = Guid.NewGuid(),
            Title = "Test Title",
            ShortDescription = null,
            Description = "Full Description",
            FundingGoal = 1000m,
            CurrentAmount = 0m
        };

        var result = _service.MapCampaignToDisplayItem(campaign);

        Assert.Equal("Full Description", result.SummaryText);
    }

    [Fact]
    public void MapCampaignToDisplayItem_NoShortDescription_LongDescription_Truncates()
    {
        var campaign = new Campaign
        {
            Id = Guid.NewGuid(),
            Title = "Test Title",
            ShortDescription = null,
            Description = new string('A', 200),
            FundingGoal = 1000m,
            CurrentAmount = 0m
        };

        var result = _service.MapCampaignToDisplayItem(campaign);

        Assert.Equal(new string('A', 147) + "...", result.SummaryText);
    }

    [Fact]
    public void BuildProfile_EmptyContext_ReturnsEmptyProfile()
    {

        Assert.Empty(profile.CategoryAffinities);
        Assert.Empty(profile.OwnerAffinities);
    }

    [Fact]
    public void BuildProfile_WithVisits_CalculatesScoresCorrectly()
    {
        var campaignId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var context = new UserHistoryContext
        {
            CampaignSummaryContexts =
            [
                new() { Id = campaignId, Category = CampaignCategory.Education, OwnerId = ownerId }
            ],
            PastVisits =
            [
                new() { CampaignId = campaignId, VisitCount = 3 }
            ]
        };

        var profile = _service.BuildProfile(context);

        Assert.Contains(CampaignCategory.Education, profile.CategoryAffinities);
        Assert.Contains(ownerId, profile.OwnerAffinities);
        Assert.Equal(3.0, profile.CategoryAffinities[CampaignCategory.Education]);
        Assert.Equal(3.0, profile.OwnerAffinities[ownerId]);
    }

    [Fact]
    public void BuildProfile_WithDonations_CalculatesScoresCorrectly()
    {
        var campaignId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var context = new UserHistoryContext
        {
            CampaignSummaryContexts =
            [
                new() { Id = campaignId, Category = CampaignCategory.Medical, OwnerId = ownerId }
            ],
            PastDonations =
            [
                new() { CampaignId = campaignId, Amount = 100m }
            ]
        };

        var profile = _service.BuildProfile(context);

        Assert.Contains(CampaignCategory.Medical, profile.CategoryAffinities);
        Assert.Contains(ownerId, profile.OwnerAffinities);
        Assert.Equal(12.0, profile.CategoryAffinities[CampaignCategory.Medical]);
        Assert.Equal(12.0, profile.OwnerAffinities[ownerId]);
    }

    [Fact]
    public void BuildProfile_AccumulatesScoresCorrectly()
    {
        var campaign1Id = Guid.NewGuid();
        var campaign2Id = Guid.NewGuid();
        var owner1Id = Guid.NewGuid();
        var owner2Id = Guid.NewGuid();

        var context = new UserHistoryContext
        {
            CampaignSummaryContexts =
            [
                new() { Id = campaign1Id, Category = CampaignCategory.Environment, OwnerId = owner1Id },
                new() { Id = campaign2Id, Category = CampaignCategory.Environment, OwnerId = owner2Id }
            ],
            PastVisits =
            [
                new() { CampaignId = campaign1Id, VisitCount = 2 }
            ],
            PastDonations =
            [
                new() { CampaignId = campaign2Id, Amount = 50m }
            ]
        };

        var profile = _service.BuildProfile(context);

        Assert.Contains(CampaignCategory.Environment, profile.CategoryAffinities);
        Assert.Contains(owner1Id, profile.OwnerAffinities);
        Assert.Contains(owner2Id, profile.OwnerAffinities);

        Assert.Equal(13.0, profile.CategoryAffinities[CampaignCategory.Environment]);
        Assert.Equal(2.0, profile.OwnerAffinities[owner1Id]);
        Assert.Equal(11.0, profile.OwnerAffinities[owner2Id]);
    }

    [Fact]
    public void BuildProfile_UnknownCampaigns_Ignored()
    {
        var context = new UserHistoryContext
        {
            CampaignSummaryContexts = [],
            PastVisits =
            [
                new() { CampaignId = Guid.NewGuid(), VisitCount = 3 }
            ],
            PastDonations =
            [
                new() { CampaignId = Guid.NewGuid(), Amount = 100m }
            ]
        };

        var profile = _service.BuildProfile(context);

        Assert.Empty(profile.CategoryAffinities);
        Assert.Empty(profile.OwnerAffinities);
    }

    [Fact]
    public void CalculateAffinityScore_MatchesCategoryAndOwner_ReturnsSum()
    {
        var ownerId = Guid.NewGuid();
        var context = new UserHistoryContext
        {
            CampaignSummaryContexts =
            [
                new() { Id = Guid.NewGuid(), Category = CampaignCategory.Education, OwnerId = ownerId }
            ]
        };
        context.PastVisits =
        [
            new() { CampaignId = context.CampaignSummaryContexts[0].Id, VisitCount = 3 }
        ];

        var profile = _service.BuildProfile(context);

        var candidateCampaign = new Campaign
        {
            Category = CampaignCategory.Education,
            OwnerId = ownerId
        };

        var score = _service.CalculateAffinityScore(profile, candidateCampaign);

        Assert.Equal(6.0, score);
    }

    [Fact]
    public void CalculateAffinityScore_NoMatches_ReturnsZero()
    {
        var context = new UserHistoryContext();
        var profile = _service.BuildProfile(context);
        var candidateCampaign = new Campaign
        {
            Category = CampaignCategory.Education,
            OwnerId = Guid.NewGuid()
        };

        var score = _service.CalculateAffinityScore(profile, candidateCampaign);

        Assert.Equal(0.0, score);
    }
}
