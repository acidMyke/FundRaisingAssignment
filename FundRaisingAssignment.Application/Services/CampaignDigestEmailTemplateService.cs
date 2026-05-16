using System.Text;
using FundRaisingAssignment.Application.Models.ProcessingModels;
using FundRaisingAssignment.Application.Interfaces;

namespace FundRaisingAssignment.Application.Services;

public class CampaignDigestEmailTemplateService : ICampaignDigestEmailTemplateService
{
    public string GenerateSubject(CampaignDigestEmailViewModel viewModel)
    {
        var topCampaign = viewModel.Campaigns.First();
        return $"We found a campaign you'll love: \"{topCampaign.Title}\"";
    }

    public string RenderHtmlBody(CampaignDigestEmailViewModel viewModel)
    {
        var sb = new StringBuilder();

        sb.AppendLine("<html>");
        sb.AppendLine("<body>");
        sb.AppendLine($"<p>Hey Member,</p>");
        sb.AppendLine("<p>Here are some fundraising campaigns you might like:</p>");

        // Take only up to 3 campaigns
        var topCampaigns = viewModel.Campaigns.Take(3);
        foreach (var campaign in topCampaigns)
        {
            sb.AppendLine("<hr />");
            sb.AppendLine($"<h2>{campaign.Title}</h2>");
            sb.AppendLine($"<p>{campaign.SummaryText}</p>");
            sb.AppendLine($"<p>Goal: {campaign.FormattedGoal} | Raised: {campaign.FormattedRaised} ({campaign.ProgressPercentage}%)</p>");
            sb.AppendLine($"<p><a href=\"https://givehive.acidmyke.link/Dashboard/CampaignPage/{campaign.Id}\">View Campaign</a></p>");
        }

        sb.AppendLine("<hr />");
        sb.AppendLine("<p>Want to stop receiving updates? <a href=\"https://givehive.acidmyke.link/digest/unsubscribe\">Unsubscribe</a></p>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }
}
