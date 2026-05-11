using System;
using FundRaisingAssignment.Application.Boundaries;
using FundRaisingAssignment.Application.Interfaces;

namespace FundRaisingAssignment.Application.Services;

public class CampaignDigestEmailTemplateService : ICampaignDigestEmailTemplateService
{
    public string GenerateSubject(CampaignDigestEmailViewModel viewModel)
    {
        throw new NotImplementedException();
    }

    public string RenderHtmlBody(CampaignDigestEmailViewModel viewModel)
    {
        throw new NotImplementedException();
    }
}
