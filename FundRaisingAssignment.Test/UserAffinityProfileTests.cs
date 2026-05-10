using FundRaisingAssignment.Application.Models;
using FundRaisingAssignment.Application.Models.ProcessingModels;
using FundRaisingAssignment.Application.Services;

namespace FundRaisingAssignment.Test
{
    public class UserAffinityProfileTests
    {
        [Fact]
        public void BuildProfile_EmptyContext_ReturnsEmptyProfile()
        {
            var context = new UserHistoryContext();
            var profile = UserAffinityProfile.BuildProfile(context);

            Assert.Empty(profile.CategoryAffinities);
            Assert.Empty(profile.OwnerAffinities);
        }

        [Fact]
        public void BuildProfile_WithVisits_CalculatesScoresCorrectly()
        {
            // Arrange
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

            // Act
            var profile = UserAffinityProfile.BuildProfile(context);

            // Assert
            Assert.Contains(CampaignCategory.Education, profile.CategoryAffinities);
            Assert.Contains(ownerId, profile.OwnerAffinities);

            // 3 visits * 1.0 = 3.0
            Assert.Equal(3.0, profile.CategoryAffinities[CampaignCategory.Education]);
            Assert.Equal(3.0, profile.OwnerAffinities[ownerId]);
        }

        [Fact]
        public void BuildProfile_WithDonations_CalculatesScoresCorrectly()
        {
            // Arrange
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

            // Act
            var profile = UserAffinityProfile.BuildProfile(context);

            // Assert
            Assert.Contains(CampaignCategory.Medical, profile.CategoryAffinities);
            Assert.Contains(ownerId, profile.OwnerAffinities);

            // Base 10.0 + (100 * 0.02) = 12.0
            Assert.Equal(12.0, profile.CategoryAffinities[CampaignCategory.Medical]);
            Assert.Equal(12.0, profile.OwnerAffinities[ownerId]);
        }

        [Fact]
        public void BuildProfile_AccumulatesScoresCorrectly()
        {
            // Arrange
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
                    new() { CampaignId = campaign1Id, VisitCount = 2 } // 2 points
                ],

                PastDonations =
                [
                    new() { CampaignId = campaign2Id, Amount = 50m } // 10 + 1 = 11 points
                ]
            };

            // Act
            var profile = UserAffinityProfile.BuildProfile(context);

            // Assert
            Assert.Contains(CampaignCategory.Environment, profile.CategoryAffinities);
            Assert.Contains(owner1Id, profile.OwnerAffinities);
            Assert.Contains(owner2Id, profile.OwnerAffinities);

            // Category CrisisRelief: 2 + 11 = 13
            Assert.Equal(13.0, profile.CategoryAffinities[CampaignCategory.Environment]);
            // Owner1: 2
            Assert.Equal(2.0, profile.OwnerAffinities[owner1Id]);
            // Owner2: 11
            Assert.Equal(11.0, profile.OwnerAffinities[owner2Id]);
        }

        [Fact]
        public void BuildProfile_UnknownCampaigns_Ignored()
        {
            // Arrange
            var context = new UserHistoryContext
            {
                CampaignSummaryContexts = [], // Empty lookup
                PastVisits =
                [
                    new() { CampaignId = Guid.NewGuid(), VisitCount = 3 }
                ],
                PastDonations =
                [
                    new() { CampaignId = Guid.NewGuid(), Amount = 100m }
                ]
            };

            // Act
            var profile = UserAffinityProfile.BuildProfile(context);

            // Assert
            Assert.Empty(profile.CategoryAffinities);
            Assert.Empty(profile.OwnerAffinities);
        }

        [Fact]
        public void CalculateAffinityScore_MatchesCategoryAndOwner_ReturnsSum()
        {
            // Arrange
            var ownerId = Guid.NewGuid();
            var context = new UserHistoryContext
            {
                CampaignSummaryContexts =
                [
                    new() { Id = Guid.NewGuid(), Category = CampaignCategory.Education, OwnerId = ownerId }
                ]
            };
            // Give 3 visits = 3 points
            context.PastVisits =
            [
                new() { CampaignId = context.CampaignSummaryContexts[0].Id, VisitCount = 3 }
            ];

            var profile = UserAffinityProfile.BuildProfile(context);

            var candidateCampaign = new Campaign
            {
                Category = CampaignCategory.Education,
                OwnerId = ownerId
            };

            // Act
            var score = profile.CalculateAffinityScore(candidateCampaign);

            // Assert
            // 3 (category) + 3 (owner) = 6
            Assert.Equal(6.0, score);
        }

        [Fact]
        public void CalculateAffinityScore_NoMatches_ReturnsZero()
        {
            // Arrange
            var context = new UserHistoryContext();
            var profile = UserAffinityProfile.BuildProfile(context);
            var candidateCampaign = new Campaign
            {
                Category = CampaignCategory.Education,
                OwnerId = Guid.NewGuid()
            };

            // Act
            var score = profile.CalculateAffinityScore(candidateCampaign);

            // Assert
            Assert.Equal(0.0, score);
        }
    }
}
