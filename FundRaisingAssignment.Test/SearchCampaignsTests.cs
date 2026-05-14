using FundRaisingAssignment.Application.Models;
using FundRaisingAssignment.Application.Services;
using Microsoft.Extensions.Logging.Abstractions;

// ─────────────────────────────────────────────────────────────────────────────
// Test plan: 10.12 Search fundraising campaigns
// User Story: DN01 – Search Fundraising Campaigns
// Backs: ICampaignService.SearchCampaignsAsync
// ─────────────────────────────────────────────────────────────────────────────

namespace FundRaisingAssignment.Test;

public class SearchCampaignsTests
{
    private static CampaignService CreateService(TestDb db) =>
        new(db.Context, NullLogger<CampaignService>.Instance);

    private static void SeedFixtures(TestDb db)
    {
        TestSeedHelpers.SeedCampaign(db,
            title: "Build a Library", description: "Books for kids in rural villages",
            category: CampaignCategory.Education, location: "Singapore");
        TestSeedHelpers.SeedCampaign(db,
            title: "Heart Surgery for Anna", description: "Urgent medical treatment",
            category: CampaignCategory.Medical, location: "Malaysia");
        TestSeedHelpers.SeedCampaign(db,
            title: "Reforest the Hills", description: "Plant 10,000 trees",
            category: CampaignCategory.Environment, location: "Singapore");
        TestSeedHelpers.SeedCampaign(db,
            title: "Coding bootcamp scholarship", description: "Free tuition for underprivileged students",
            category: CampaignCategory.Education, location: null);
    }

    [Fact]
    public async Task NoFilters_ReturnsAllCampaigns()
    {
        using var db = new TestDb();
        SeedFixtures(db);
        var sut = CreateService(db);

        var results = await sut.SearchCampaignsAsync(null, null, null);

        Assert.Equal(4, results.Count);
    }

    [Fact]
    public async Task Keyword_MatchesTitleOrDescription_CaseSensitiveContains()
    {
        using var db = new TestDb();
        SeedFixtures(db);
        var sut = CreateService(db);

        var byTitle = await sut.SearchCampaignsAsync("Library", null, null);
        Assert.Single(byTitle);
        Assert.Contains(byTitle, c => c.Title.Contains("Library"));

        var byDescription = await sut.SearchCampaignsAsync("trees", null, null);
        Assert.Single(byDescription);
        Assert.Contains(byDescription, c => c.Description.Contains("trees"));
    }

    [Fact]
    public async Task Category_FiltersByEnumName()
    {
        using var db = new TestDb();
        SeedFixtures(db);
        var sut = CreateService(db);

        var results = await sut.SearchCampaignsAsync(null, "Education", null);

        Assert.Equal(2, results.Count);
        Assert.All(results, c => Assert.Equal(CampaignCategory.Education, c.Category));
    }

    [Fact]
    public async Task Category_UnknownEnumName_IsIgnored_AndReturnsAll()
    {
        using var db = new TestDb();
        SeedFixtures(db);
        var sut = CreateService(db);

        var results = await sut.SearchCampaignsAsync(null, "NotARealCategory", null);

        Assert.Equal(4, results.Count);
    }

    [Fact]
    public async Task Location_FiltersByContains_NullLocationsExcluded()
    {
        using var db = new TestDb();
        SeedFixtures(db);
        var sut = CreateService(db);

        var results = await sut.SearchCampaignsAsync(null, null, "Singapore");

        Assert.Equal(2, results.Count);
        Assert.All(results, c => Assert.Equal("Singapore", c.Location));
    }

    [Fact]
    public async Task CombinedFilters_AreAndApplied()
    {
        using var db = new TestDb();
        SeedFixtures(db);
        var sut = CreateService(db);

        var results = await sut.SearchCampaignsAsync("Library", "Education", "Singapore");

        Assert.Single(results);
        Assert.Equal("Build a Library", results[0].Title);
    }

    [Fact]
    public async Task Keyword_NoMatch_ReturnsEmpty()
    {
        using var db = new TestDb();
        SeedFixtures(db);
        var sut = CreateService(db);

        var results = await sut.SearchCampaignsAsync("zzznevermatched", null, null);

        Assert.Empty(results);
    }

    [Fact]
    public async Task Keyword_WhitespaceTrimmedBeforeFiltering()
    {
        using var db = new TestDb();
        SeedFixtures(db);
        var sut = CreateService(db);

        var results = await sut.SearchCampaignsAsync("   Library   ", null, null);

        Assert.Single(results);
    }
}
