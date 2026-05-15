using System.Linq.Expressions;
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
    private readonly Mock<IDigestJobQueue> _mockJobQueue;
    private readonly CampaignDigestService _service;

    public CampaignDigestServiceTests()
    {
        _mockRepository = new Mock<ICampaignDigestRepository>();
        _mockLogger = new Mock<ILogger<CampaignDigestService>>();
        _mockTemplateService = new Mock<ICampaignDigestEmailTemplateService>();
        _mockEmailService = new Mock<IEmailService>();
        _mockJobQueue = new Mock<IDigestJobQueue>();

        _service = new CampaignDigestService(
            _mockRepository.Object,
            _mockLogger.Object,
            _mockTemplateService.Object,
            _mockEmailService.Object,
            _mockJobQueue.Object
        );
    }

    [Fact]
    public async Task ProcessAsync_NoUsers_UpdatesBatchStatusToFailed()
    {
        var batchId = Guid.NewGuid();
        var batch = new DigestBatch { Id = batchId, Status = DigestBatchStatus.Pending };
        _mockRepository.Setup(r => r.GetDigestBatchByIdAsync(batchId)).ReturnsAsync(batch);
        _mockRepository.Setup(r => r.GetUsersEligibleForDigestAsync(It.IsAny<DateTime>(), 10))
            .ReturnsAsync([]);

        await _service.ProcessAsync(batchId);

        _mockRepository.Verify(r => r.GetActiveCampaignsAsync(), Times.Never);
        _mockRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
        Assert.Equal(DigestBatchStatus.Failed, batch.Status);
        Assert.Equal(0, batch.UserCount);
    }

    [Fact]
    public async Task ProcessAsync_NoCampaigns_UpdatesBatchStatusToFailed()
    {
        var batchId = Guid.NewGuid();
        var batch = new DigestBatch { Id = batchId, Status = DigestBatchStatus.Pending };
        _mockRepository.Setup(r => r.GetDigestBatchByIdAsync(batchId)).ReturnsAsync(batch);
        _mockRepository.Setup(r => r.GetUsersEligibleForDigestAsync(It.IsAny<DateTime>(), 10))
            .ReturnsAsync([new ApplicationUser { Id = Guid.NewGuid() }]);
        _mockRepository.Setup(r => r.GetActiveCampaignsAsync())
            .ReturnsAsync([]);

        await _service.ProcessAsync(batchId);

        _mockRepository.Verify(r => r.GetPastVisitsForUsersAsync(It.IsAny<IEnumerable<Guid>>()), Times.Never);
        _mockRepository.Verify(r => r.GetPastDonationsForUsersAsync(It.IsAny<IEnumerable<Guid>>()), Times.Never);
        _mockRepository.Verify(r => r.GetCampaignSummariesAsync(It.IsAny<IEnumerable<Guid>>()), Times.Never);
        _mockRepository.Verify(r => r.SaveChangesAsync(), Times.AtLeastOnce);
        Assert.Equal(DigestBatchStatus.Failed, batch.Status);
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
        var profile = _service.BuildProfile([], new Dictionary<Guid, CampaignSummaryContext>());

        Assert.Empty(profile.CategoryAffinities);
        Assert.Empty(profile.OwnerAffinities);
    }

    [Fact]
    public void BuildProfile_WithVisits_CalculatesScoresCorrectly()
    {
        var campaignId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        var summaries = new Dictionary<Guid, CampaignSummaryContext>
        {
            { campaignId, new CampaignSummaryContext { Id = campaignId, Category = CampaignCategory.Education, OwnerId = ownerId } }
        };
        var interactions = new List<UserCampaignInteractionDto> { new() { CampaignId = campaignId, VisitCount = 3 } };

        var profile = _service.BuildProfile(interactions, summaries);

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

        var summaries = new Dictionary<Guid, CampaignSummaryContext>
        {
            { campaignId, new CampaignSummaryContext { Id = campaignId, Category = CampaignCategory.Medical, OwnerId = ownerId } }
        };
        var interactions = new List<UserCampaignInteractionDto> { new() { CampaignId = campaignId, DonationAmount = 100m } };

        var profile = _service.BuildProfile(interactions, summaries);

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

        var summaries = new Dictionary<Guid, CampaignSummaryContext>
        {
            { campaign1Id, new CampaignSummaryContext { Id = campaign1Id, Category = CampaignCategory.Environment, OwnerId = owner1Id } },
            { campaign2Id, new CampaignSummaryContext { Id = campaign2Id, Category = CampaignCategory.Environment, OwnerId = owner2Id } }
        };

        var interactions = new List<UserCampaignInteractionDto>
        {
            new() { CampaignId = campaign1Id, VisitCount = 2 },
            new() { CampaignId = campaign2Id, DonationAmount = 50m }
        };

        var profile = _service.BuildProfile(interactions, summaries);

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
        var interactions = new List<UserCampaignInteractionDto>
        {
            new() { CampaignId = Guid.NewGuid(), VisitCount = 3 },
            new() { CampaignId = Guid.NewGuid(), DonationAmount = 100m }
        };

        var profile = _service.BuildProfile(interactions, new Dictionary<Guid, CampaignSummaryContext>());

        Assert.Empty(profile.CategoryAffinities);
        Assert.Empty(profile.OwnerAffinities);
    }

    [Fact]
    public void CalculateAffinityScore_MatchesCategoryAndOwner_ReturnsSum()
    {
        var ownerId = Guid.NewGuid();
        var campaignId = Guid.NewGuid();

        var summaries = new Dictionary<Guid, CampaignSummaryContext>
        {
            { campaignId, new CampaignSummaryContext { Id = campaignId, Category = CampaignCategory.Education, OwnerId = ownerId } }
        };
        var interactions = new List<UserCampaignInteractionDto> { new() { CampaignId = campaignId, VisitCount = 3 } };

        var profile = _service.BuildProfile(interactions, summaries);

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
        var profile = _service.BuildProfile([], new Dictionary<Guid, CampaignSummaryContext>());
        var candidateCampaign = new Campaign
        {
            Category = CampaignCategory.Education,
            OwnerId = Guid.NewGuid()
        };

        var score = _service.CalculateAffinityScore(profile, candidateCampaign);

        Assert.Equal(0.0, score);
    }

    [Fact]
    public void GetTopCampaignsForUser_ReturnsTopThreeByScore()
    {
        // Arrange
        var profile = new CampaignDigestService.UserAffinityProfile();
        var campaigns = Enumerable.Range(1, 5).Select(i => new Campaign
        {
            Id = Guid.NewGuid(),
            Title = $"C{i}",
            Category = (CampaignCategory)(i % 3)
        }).ToList();

        var urgencyScores = campaigns.ToDictionary(c => c.Id, c => (double)campaigns.IndexOf(c));

        // Act
        var result = _service.GetTopCampaignsForUser(profile, campaigns, urgencyScores).ToList();

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal("C5", result[0].Title); // Score 4
        Assert.Equal("C4", result[1].Title); // Score 3
        Assert.Equal("C3", result[2].Title); // Score 2
    }

    [Fact]
    public void GetTopCampaignsForUser_ExcludesZeroOrNegativeScores()
    {
        // Arrange
        var profile = new CampaignDigestService.UserAffinityProfile();
        var campaigns = Enumerable.Range(1, 3).Select(i => new Campaign { Id = Guid.NewGuid(), Title = $"C{i}" }).ToList();
        var urgencyScores = new Dictionary<Guid, double>
        {
            { campaigns[0].Id, 10.0 },
            { campaigns[1].Id, 0.0 },
            { campaigns[2].Id, -5.0 }
        };

        // Act
        var result = _service.GetTopCampaignsForUser(profile, campaigns, urgencyScores).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal("C1", result[0].Title);
    }

    [Fact]
    public async Task SendDigestEmailAsync_EmptyCampaigns_DoesNotSendEmail()
    {
        // Arrange
        var emailId = Guid.NewGuid();
        var user = new ApplicationUser { Email = "" };

        // Act
        await _service.SendDigestEmailAsync(user, [], emailId);

        // Assert
        _mockEmailService.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), emailId.ToString()), Times.Never);
    }

    [Fact]
    public async Task SendDigestEmailAsync_WithCampaigns_SendsEmailWithCorrectDetails()
    {
        // Arrange
        var emailId = Guid.NewGuid();
        const string EMAIL = "test@example.com";
        const string SUBJECT = "Test Subject";
        const string BODY = "Test Body";
        var user = new ApplicationUser { Email = EMAIL };
        var campaigns = new List<Campaign> { new() { Id = Guid.NewGuid(), Title = "", Description = "" } };

        _mockTemplateService.Setup(t => t.GenerateSubject(It.IsAny<CampaignDigestEmailViewModel>())).Returns(SUBJECT);
        _mockTemplateService.Setup(t => t.RenderHtmlBody(It.IsAny<CampaignDigestEmailViewModel>())).Returns(BODY);

        // Act
        await _service.SendDigestEmailAsync(user, campaigns, emailId);

        // Assert
        _mockEmailService.Verify(e => e.SendEmailAsync(EMAIL, SUBJECT, BODY, emailId.ToString()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_GoldenFlow_UpdatesBatchStatusToProcessingAndSendsEmails()
    {
        // Arrange
        const string EMAIL = "test@example.com";
        const string SUBJECT = "Test Subject";
        const string BODY = "Test Body";
        var userId = Guid.NewGuid();
        var campaignId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var batchId = Guid.NewGuid();
        var batch = new DigestBatch { Id = batchId, Status = DigestBatchStatus.Pending };

        var users = new List<ApplicationUser>
        {
            new ApplicationUser { Id = userId, Email = EMAIL }
        };

        var campaigns = new List<Campaign>
        {
            new Campaign
            {
                Id = campaignId,
                Title = "",
                Description = "",
                FundingGoal = 1000m,
                CurrentAmount = 500m,
                TargetAmount = 1000m,
                EndDate = now.AddHours(10)
            }
        };

        var visits = new List<UserCampaignInteractionDto>();
        var donations = new List<UserCampaignInteractionDto>();
        var summaries = new Dictionary<Guid, CampaignSummaryContext>();

        _mockRepository.Setup(r => r.GetDigestBatchByIdAsync(batchId)).ReturnsAsync(batch);
        _mockRepository.Setup(r => r.GetUsersEligibleForDigestAsync(It.IsAny<DateTime>(), 10)).ReturnsAsync(users);
        _mockRepository.Setup(r => r.GetActiveCampaignsAsync()).ReturnsAsync(campaigns);

        Expression<Func<IEnumerable<Guid>, bool>> containingUserId = ids => ids != null && ids.Contains(userId);
        Expression<Func<IEnumerable<Guid>, bool>> containingCampaignId = ids => ids != null && ids.Contains(campaignId);

        _mockRepository.Setup(r => r.GetPastVisitsForUsersAsync(It.Is(containingUserId))).ReturnsAsync(visits);
        _mockRepository.Setup(r => r.GetPastDonationsForUsersAsync(It.Is(containingUserId))).ReturnsAsync(donations);
        _mockRepository.Setup(r => r.GetCampaignSummariesAsync(It.Is(containingCampaignId))).ReturnsAsync(summaries);
        _mockTemplateService.Setup(t => t.GenerateSubject(It.IsAny<CampaignDigestEmailViewModel>())).Returns(SUBJECT);
        _mockTemplateService.Setup(t => t.RenderHtmlBody(It.IsAny<CampaignDigestEmailViewModel>())).Returns(BODY);

        // Act
        await _service.ProcessAsync(batchId);

        // Assert
        _mockEmailService.Verify(e => e.SendEmailAsync(EMAIL, SUBJECT, BODY, It.IsAny<string>()), Times.Once);
        _mockRepository.Verify(r => r.SaveChangesAsync(), Times.AtLeastOnce);
        Assert.Equal(DigestBatchStatus.Processed, batch.Status);
    }

    [Fact]
    public async Task ValidateAndEnqueueAsync_Positive_ShouldQueueJobAndReturnJobId()
    {
        // Arrange
        const string EMAIL = "test@example.com";
        var userId = Guid.NewGuid();
        var campaignId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var users = new List<ApplicationUser>
        {
            new ApplicationUser { Id = userId, Email = EMAIL }
        };

        var campaigns = new List<Campaign>
        {
            new Campaign
            {
                Id = campaignId,
                Title = "",
                Description = "",
                FundingGoal = 1000m,
                CurrentAmount = 500m,
                TargetAmount = 1000m,
                EndDate = now.AddHours(10)
            }
        };

        _mockRepository.Setup(r => r.GetUsersEligibleForDigestAsync(It.IsAny<DateTime>(), 1)).ReturnsAsync(users);
        _mockRepository.Setup(r => r.GetActiveCampaignsAsync()).ReturnsAsync(campaigns);
        _mockJobQueue.Setup(t => t.QueueJob(It.IsAny<Guid>())).Returns(true);

        // Act
        var batchId = await _service.ValidateAndEnqueueAsync();

        // Assert
        _mockRepository.Verify(r => r.AddDigestBatchRecord(It.Is<DigestBatch>(b => b.Id == batchId)), Times.Once);
        _mockRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
        _mockJobQueue.Verify(e => e.QueueJob(batchId), Times.Once);
    }

    [Fact]
    public async Task ValidateAndEnqueueAsync_Negative_WhenNoUserFound_ShouldThrowError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var campaignId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        List<ApplicationUser> users = [];

        List<Campaign> campaigns =
        [
            new Campaign
            {
                Id = campaignId,
                Title = "",
                Description = "",
                FundingGoal = 1000m,
                CurrentAmount = 500m,
                TargetAmount = 1000m,
                EndDate = now.AddHours(10)
            }
        ];

        _mockRepository.Setup(r => r.GetUsersEligibleForDigestAsync(It.IsAny<DateTime>(), 1)).ReturnsAsync(users);
        _mockRepository.Setup(r => r.GetActiveCampaignsAsync()).ReturnsAsync(campaigns);
        _mockJobQueue.Setup(t => t.QueueJob(It.IsAny<Guid>())).Returns(true);
        // Act
        var exception = await Assert.ThrowsAsync<DomainException>(() => _service.ValidateAndEnqueueAsync());
        // Assert
        _mockJobQueue.Verify(e => e.QueueJob(It.IsAny<Guid>()), Times.Never);
        _mockRepository.Verify(r => r.AddDigestBatchRecord(It.IsAny<DigestBatch>()), Times.Never);
        _mockRepository.Verify(r => r.SaveChangesAsync(), Times.Never);
        Assert.Equal("No users eligible for digest processing at this time.", exception.Message);
    }

    [Fact]
    public async Task ValidateAndEnqueueAsync_Negative_WhenNoCampaignFound_ShouldThrowError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var campaignId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        List<ApplicationUser> users = [new ApplicationUser() { Id = userId, Email = "" }];
        List<Campaign> campaigns = [];

        _mockRepository.Setup(r => r.GetUsersEligibleForDigestAsync(It.IsAny<DateTime>(), 1)).ReturnsAsync(users);
        _mockRepository.Setup(r => r.GetActiveCampaignsAsync()).ReturnsAsync(campaigns);
        _mockJobQueue.Setup(t => t.QueueJob(It.IsAny<Guid>())).Returns(true);
        // Act
        var exception = await Assert.ThrowsAsync<DomainException>(() => _service.ValidateAndEnqueueAsync());
        // Assert
        _mockJobQueue.Verify(e => e.QueueJob(It.IsAny<Guid>()), Times.Never);
        _mockRepository.Verify(r => r.AddDigestBatchRecord(It.IsAny<DigestBatch>()), Times.Never);
        _mockRepository.Verify(r => r.SaveChangesAsync(), Times.Never);
        Assert.Equal("No active campaigns to include in digest.", exception.Message);
    }

    [Fact]
    public async Task UpdateDigestBatch_WithCampaigns_AddsCorrectEntries()
    {
        // Arrange
        var batch = new DigestBatch { Id = Guid.NewGuid() };
        var user = new ApplicationUser { Id = Guid.NewGuid() };
        var emailId = Guid.NewGuid();
        var campaigns = new List<Campaign>
        {
            new Campaign { Id = Guid.NewGuid(), Title = "C1", Description = "" },
            new Campaign { Id = Guid.NewGuid(), Title = "C2", Description = "" }
        };

        // Act
        await _service.UpdateDigestBatch(batch, user, campaigns, emailId);

        // Assert
        Assert.Equal(2, batch.Entries.Count);
        Assert.All(batch.Entries, e => Assert.Equal(batch.Id, e.DigestBatchId));
        Assert.All(batch.Entries, e => Assert.Equal(user.Id, e.UserId));
        Assert.All(batch.Entries, e => Assert.Equal(emailId, e.EmailId));
        Assert.All(batch.Entries, e => Assert.Equal(DigestEmailStatus.Initial, e.EmailStatus));
        Assert.All(batch.Entries, e => Assert.NotNull(e.EmailId));
        Assert.Contains(batch.Entries, e => e.CampaignId == campaigns[0].Id);
        Assert.Contains(batch.Entries, e => e.CampaignId == campaigns[1].Id);
    }

    [Fact]
    public async Task UpdateDigestBatch_NoCampaigns_AddsBypassEntry()
    {
        // Arrange
        var batch = new DigestBatch { Id = Guid.NewGuid() };
        var user = new ApplicationUser { Id = Guid.NewGuid() };
        var emailId = Guid.NewGuid();

        // Act
        await _service.UpdateDigestBatch(batch, user, [], emailId);

        // Assert
        Assert.Single(batch.Entries);
        var entry = batch.Entries[0];
        Assert.Equal(batch.Id, entry.DigestBatchId);
        Assert.Equal(user.Id, entry.UserId);
        Assert.Null(entry.EmailId);
        Assert.Null(entry.CampaignId);
        Assert.Null(entry.SentAt);
        Assert.Equal(DigestEmailStatus.Bypass, entry.EmailStatus);
    }
}
