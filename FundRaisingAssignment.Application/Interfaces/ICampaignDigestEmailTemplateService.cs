using FundRaisingAssignment.Application.Boundaries;

namespace FundRaisingAssignment.Application.Interfaces;

public interface ICampaignDigestEmailTemplateService
{
    string GenerateSubject(CampaignDigestEmailViewModel viewModel);
    string RenderHtmlBody(CampaignDigestEmailViewModel viewModel);
}