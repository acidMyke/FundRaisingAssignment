using FundRaisingAssignment.Application.Models.ProcessingModels;
using FundRaisingAssignment.Application.Services;
using Xunit;

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
        var campaigns = new List<CampaignDisplayItem>
        {
            new() { Id = Guid.NewGuid(), Title = "Save the Wetlands", SummaryText = "", FormattedGoal = "", FormattedRaised = "", ProgressPercentage = 0 },
            new() { Id = Guid.NewGuid(), Title = "Build a Community Library", SummaryText = "", FormattedGoal = "", FormattedRaised = "", ProgressPercentage = 0 }
        };
        var viewModel = new CampaignDigestEmailViewModel { Campaigns = campaigns };

        // Act
        var result = _service.GenerateSubject(viewModel);

        // Assert
        Assert.Equal("We found a campaign you'll love: \"Save the Wetlands\"", result);
    }

    [Fact]
    public void RenderHtmlBody_RendersCampaignDetailsCorrectly()
    {
        // Arrange
        var campaigns = new List<CampaignDisplayItem>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Title = "Test Campaign",
                SummaryText = "Test Summary",
                FormattedGoal = "10,000 USD",
                FormattedRaised = "5,000 USD",
                ProgressPercentage = 50m
            }
        };
        var viewModel = new CampaignDigestEmailViewModel { Campaigns = campaigns };

        // Act
        var html = _service.RenderHtmlBody(viewModel);

        // Assert
        Assert.Contains("Test Campaign", html);
        Assert.Contains("Test Summary", html);
        Assert.Contains("Goal: 10,000 USD | Raised: 5,000 USD (50%)", html);
    }

    [Fact]
    public void RenderHtmlBody_IncludesCampaignUrl()
    {
        // Arrange
        var campaignId = Guid.NewGuid();
        var campaigns = new List<CampaignDisplayItem>
        {
            new() { Id = campaignId, Title = "Link Test", SummaryText = "", FormattedGoal = "", FormattedRaised = "", ProgressPercentage = 0 }
        };
        var viewModel = new CampaignDigestEmailViewModel { Campaigns = campaigns };

        // Act
        var html = _service.RenderHtmlBody(viewModel);

        // Assert
        Assert.Contains($"http://givehive.acidmyke.link/Dashboard/CampaignPage/{campaignId}", html);
    }
}