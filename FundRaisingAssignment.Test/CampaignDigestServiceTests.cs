using FundRaisingAssignment.Application.Interfaces;
using FundRaisingAssignment.Application.Models;
using FundRaisingAssignment.Application.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace FundRaisingAssignment.Test
{
    public class CampaignDigestServiceTests
    {
        private readonly Mock<ICampaignDigestRepository> _repoMock;
        private readonly Mock<IEmailService> _emailServiceMock;
        private readonly Mock<ILogger<CampaignDigestService>> _loggerMock;
        private readonly CampaignDigestService _service;

        public CampaignDigestServiceTests()
        {
            _repoMock = new Mock<ICampaignDigestRepository>();
            _emailServiceMock = new Mock<IEmailService>();
            _loggerMock = new Mock<ILogger<CampaignDigestService>>();

            _service = new CampaignDigestService(_repoMock.Object, _emailServiceMock.Object, _loggerMock.Object);
        }

        [Fact]
        public async Task TriggerDigestProcessingAsync_NoCampaigns_DoesNothing()
        {
            // Arrange
            _repoMock.Setup(r => r.GetCampaignsNeedingDigestAsync(It.IsAny<DateTime>()))
                .ReturnsAsync(new List<Campaign>());

            // Act
            await _service.TriggerDigestProcessingAsync();

            // Assert
            _repoMock.Verify(r => r.GetCampaignPastDonorIdsAsync(It.IsAny<Guid>()), Times.Never);
            _emailServiceMock.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task TriggerDigestProcessingAsync_WithCampaignAndDonor_SendsEmail()
        {
            // Arrange
            var campaignId = Guid.NewGuid();
            var campaign = new Campaign 
            { 
                Id = campaignId, 
                Title = "Test Campaign", 
                Status = CampaignStatus.Active,
                TargetAmount = 1000,
                CurrentAmount = 100
            };

            var userId = Guid.NewGuid();
            var user = new ApplicationUser 
            { 
                Id = userId, 
                Email = "test@example.com", 
                ReceiveCampaignUpdates = true 
            };

            var campaigns = new List<Campaign> { campaign };
            var donorIds = new List<Guid> { userId };
            var users = new List<ApplicationUser> { user };
            var donations = new List<DonationRecord> { new DonationRecord { DoneeId = userId, CampaignId = campaignId, Date = DateTime.UtcNow } };

            _repoMock.Setup(r => r.GetCampaignsNeedingDigestAsync(It.IsAny<DateTime>()))
                .ReturnsAsync(campaigns);
            _repoMock.Setup(r => r.GetCampaignPastDonorIdsAsync(campaignId))
                .ReturnsAsync(donorIds);
            _repoMock.Setup(r => r.GetCampaignVisitorIdsAsync(campaignId))
                .ReturnsAsync(new List<Guid>());
            _repoMock.Setup(r => r.GetUsersByIdsAsync(It.IsAny<IEnumerable<Guid>>()))
                .ReturnsAsync(users);
            _repoMock.Setup(r => r.GetUserDonationsForCampaignAsync(userId, campaignId))
                .ReturnsAsync(donations);

            // Act
            await _service.TriggerDigestProcessingAsync();

            // Assert
            _emailServiceMock.Verify(e => e.SendEmailAsync("test@example.com", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            _repoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task TriggerDigestProcessingAsync_UserFatigue_SkipsUser()
        {
            // Arrange
            var campaignId = Guid.NewGuid();
            var campaign = new Campaign 
            { 
                Id = campaignId, 
                Title = "Test Campaign", 
                Status = CampaignStatus.Active 
            };

            var userId = Guid.NewGuid();
            var user = new ApplicationUser 
            { 
                Id = userId, 
                Email = "fatigued@example.com", 
                ReceiveCampaignUpdates = true,
                LastCampaignUpdateSent = DateTime.UtcNow.AddDays(-1) // Received yesterday
            };

            _repoMock.Setup(r => r.GetCampaignsNeedingDigestAsync(It.IsAny<DateTime>()))
                .ReturnsAsync(new List<Campaign> { campaign });
            _repoMock.Setup(r => r.GetCampaignPastDonorIdsAsync(campaignId))
                .ReturnsAsync(new List<Guid> { userId });
            _repoMock.Setup(r => r.GetCampaignVisitorIdsAsync(campaignId))
                .ReturnsAsync(new List<Guid>());
            _repoMock.Setup(r => r.GetUsersByIdsAsync(It.IsAny<IEnumerable<Guid>>()))
                .ReturnsAsync(new List<ApplicationUser> { user });

            // Act
            await _service.TriggerDigestProcessingAsync();

            // Assert
            _emailServiceMock.Verify(e => e.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void ShouldSkipUser_UserOptedOut_ReturnsTrue()
        {
            // Arrange
            var user = new ApplicationUser { ReceiveCampaignUpdates = false };
            var executionTime = DateTime.UtcNow;

            // Act
            var result = _service.ShouldSkipUser(user, executionTime);

            // Assert
            Assert.True(result);
        }
    }
}
