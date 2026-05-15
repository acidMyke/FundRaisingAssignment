using FundRaisingAssignment.Application.Models.ProcessingModels;

namespace FundRaisingAssignment.Application.Interfaces;

public interface ICampaignDigestEmailTemplateService
{
    string GenerateSubject(CampaignDigestEmailViewModel viewModel);
    string RenderHtmlBody(CampaignDigestEmailViewModel viewModel);
}