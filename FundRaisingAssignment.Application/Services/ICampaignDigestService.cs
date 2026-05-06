using FundRaisingAssignment.Application.Models;

namespace FundRaisingAssignment.Application.Services
{
    public interface ICampaignDigestService
    {
        Task TriggerDigestProcessingAsync();
        Task ProcessCampaignAsync(Campaign campaign, DateTime executionTime);
        bool ShouldSkipUser(ApplicationUser user, DateTime executionTime);
        EmailType DetermineEmailType(Campaign campaign, DateTime executionTime);
        (string Subject, string HtmlBody) RenderEmail(EmailType type, Campaign campaign, ApplicationUser user);
    }
}
