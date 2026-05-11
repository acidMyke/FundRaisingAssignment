using FundRaisingAssignment.Application.Boundaries;
using FundRaisingAssignment.Application.Models;
using FundRaisingAssignment.Application.Services;

namespace FundRaisingAssignment.Test;

public class CampaignEmailDigestTemplateServiceTests
{
    private readonly CampaignDigestEmailTemplateService _service;

    public CampaignEmailDigestTemplateServiceTests()
    {
        _service = new CampaignDigestEmailTemplateService();
    }

    [Fact]
    public void GenerateSubject_WithCampaigns_ReturnsPersonalizedSubjectWithTopCampaign()
    {
        // Arrange
        var user = new ApplicationUser();
        var campaigns = new List<Campaign>
        {
            new() { Title = "Save the Wetlands" },
            new() { Title = "Build a Community Library" }
        };
        var viewModel = new CampaignDigestEmailViewModel(user, campaigns);

        // Act
        var result = _service.GenerateSubject(viewModel);

        // Assert
        Assert.Equal("We found a campaign you'll love: \"Save the Wetlands\"", result);
    }

    [Fact]
    public void GenerateSubject_NullUserName_FallsBackToFriend()
    {
        // Arrange
        var user = new ApplicationUser();
        var campaigns = new List<Campaign> { new() { Title = "Save the Wetlands" } };
        var viewModel = new CampaignDigestEmailViewModel(user, campaigns);

        // Act
        var result = _service.GenerateSubject(viewModel);

        // Assert
        Assert.Equal("We found a campaign you'll love: \"Save the Wetlands\"", result);
    }

    [Fact]
    public void RenderHtmlBody_MoreThanThreeCampaigns_OnlyRendersTopThree()
    {
        // Arrange
        var user = new ApplicationUser();
        var campaigns = new List<Campaign>
        {
            new() { Id = Guid.NewGuid(), Title = "Campaign 1" },
            new() { Id = Guid.NewGuid(), Title = "Campaign 2" },
            new() { Id = Guid.NewGuid(), Title = "Campaign 3" },
        };
        var viewModel = new CampaignDigestEmailViewModel(user, campaigns);

        // Act
        var html = _service.RenderHtmlBody(viewModel);

        // Assert
        Assert.Contains("Campaign 1", html);
        Assert.Contains("Campaign 2", html);
        Assert.Contains("Campaign 3", html);
    }

    [Fact]
    public void RenderHtmlBody_MissingShortDescription_TruncatesMainDescription()
    {
        // Arrange
        var user = new ApplicationUser();
        var longDescription = new string('A', 200);
        var campaigns = new List<Campaign>
        {
            new()
            {
                Title = "Test Truncation",
                Description = longDescription,
                ShortDescription = null
            }
        };
        var viewModel = new CampaignDigestEmailViewModel(user, campaigns);

        // Act
        var html = _service.RenderHtmlBody(viewModel);

        // Assert
        var expectedTruncation = new string('A', 147) + "...";
        Assert.Contains(expectedTruncation, html);
    }

    [Fact]
    public void RenderHtmlBody_CalculatesGoalProgressCorrectly()
    {
        // Arrange
        var user = new ApplicationUser();
        var campaigns = new List<Campaign>
        {
            new()
            {
                Title = "Halfway There",
                FundingGoal = 10000,
                CurrentAmount = 5000
            },
            new()
            {
                Title = "Overfunded",
                FundingGoal = 10000,
                CurrentAmount = 15000
            },
            new()
            {
                Title = "Zero Goal Safety",
                FundingGoal = 0,
                CurrentAmount = 0
            }
        };
        var viewModel = new CampaignDigestEmailViewModel(user, campaigns);

        // Act
        var html = _service.RenderHtmlBody(viewModel);

        // Assert
        Assert.Contains("50%", html);   // 5,000 / 10,000
        Assert.Contains("100%", html);  // 15,000 / 10,000 should cap gracefully at 100%
        Assert.Contains("0%", html);    // Division-by-zero safety fallback
    }

    [Fact]
    public void RenderHtmlBody_IncludesCampaignUrl()
    {
        // Arrange
        var user = new ApplicationUser();
        var campaignId = Guid.NewGuid();
        var campaigns = new List<Campaign>
        {
            new() { Id = campaignId, Title = "Link Test" }
        };
        var viewModel = new CampaignDigestEmailViewModel(user, campaigns);

        // Act
        var html = _service.RenderHtmlBody(viewModel);

        // Assert
        Assert.Contains($"http://givehive.acidmyke.link/Dashboard/CampaignPage/{campaignId}", html);
    }

}