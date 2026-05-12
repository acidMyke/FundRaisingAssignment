using FundRaisingAssignment.Application.Interfaces;
using FundRaisingAssignment.Application.Interfaces.Repositories;
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

}
