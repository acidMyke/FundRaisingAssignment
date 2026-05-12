using FundRaisingAssignment.Application.Interfaces;
using FundRaisingAssignment.Application.Interfaces.Repositories;
using FundRaisingAssignment.Application.Models;
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

}
