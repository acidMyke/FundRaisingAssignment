using System.Security.Cryptography;
using System.Text;
using FundRaisingAssignment.Application.Interfaces;
using FundRaisingAssignment.Application.Models;

namespace FundRaisingAssignment.Application.Services
{
    public enum EmailType
    {
        Milestone,
        Urgency,
        StretchGoal,
        General
    }

    public class CampaignDigestService(ICampaignDigestRepository repository, IEmailService emailService, ILogger<CampaignDigestService> logger) : ICampaignDigestService
    {
        public async Task TriggerDigestProcessingAsync()
        {
            var executionTime = DateTime.UtcNow;

            var campaigns = await repository.GetCampaignsNeedingDigestAsync(executionTime);

            if (!campaigns.Any())
            {
                logger.LogInformation("No campaigns need digest processing at this time.");
                return;
            }

            foreach (var campaign in campaigns)
            {
                await ProcessCampaignAsync(campaign, executionTime);
                
                campaign.LastDigestSent = executionTime;
            }

            await repository.SaveChangesAsync();
        }

        public async Task ProcessCampaignAsync(Campaign campaign, DateTime executionTime)
        {
            var pastDonors = await repository.GetCampaignPastDonorIdsAsync(campaign.Id);
            var visitors = await repository.GetCampaignVisitorIdsAsync(campaign.Id);

            var allUserIds = pastDonors.Union(visitors).Distinct().ToList();

            if (!allUserIds.Any())
            {
                logger.LogWarning("Campaign {CampaignId} had 0 sendable recipients.", campaign.Id);
                return;
            }

            var users = await repository.GetUsersByIdsAsync(allUserIds);

            int sentCount = 0;
            int failedCount = 0;

            var selectedType = DetermineEmailType(campaign, executionTime);

            foreach (var user in users)
            {
                if (ShouldSkipUser(user, executionTime))
                {
                    continue;
                }

                var (subject, htmlBody) = RenderEmail(selectedType, campaign, user);

                try
                {
                    if (!string.IsNullOrEmpty(user.Email))
                    {
                        await emailService.SendEmailAsync(user.Email, subject, htmlBody);
                        user.LastCampaignUpdateSent = executionTime;
                        sentCount++;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to send email to {Email}", user.Email);
                    failedCount++;
                    if (ex.Message.Contains("bounce") || ex.Message.Contains("Invalid"))
                    {
                        user.IsBounced = true;
                    }
                }
            }

            logger.LogInformation("Finished campaign {CampaignId} digest. Sent: {Sent}, Failed: {Failed}", campaign.Id, sentCount, failedCount);
        }

        public bool ShouldSkipUser(ApplicationUser user, DateTime executionTime)
        {
            if (!user.ReceiveCampaignUpdates) return true;
            if (user.UnsubscribeCooldownUntil.HasValue && user.UnsubscribeCooldownUntil.Value > executionTime) return true;
            if (user.IsBounced) return true;

            // Fatigue guard: has donor received ANY digest email in the last 7 days?
            if (user.LastCampaignUpdateSent.HasValue && user.LastCampaignUpdateSent.Value > executionTime.AddDays(-7))
            {
                return true;
            }

            return false;
        }

        public EmailType DetermineEmailType(Campaign campaign, DateTime executionTime)
        {
            bool endingSoon = campaign.EndDate.HasValue && (campaign.EndDate.Value - executionTime).TotalHours < 72 && (campaign.EndDate.Value - executionTime).TotalHours > 0;
            bool overfunded = campaign.CurrentAmount > campaign.TargetAmount;
            bool milestoneCrossed = campaign.TargetAmount > 0 && campaign.CurrentAmount >= campaign.TargetAmount * 0.5m;

            if (endingSoon) return EmailType.Urgency;
            if (overfunded) return EmailType.StretchGoal;
            if (milestoneCrossed) return EmailType.Milestone;

            return EmailType.General;
        }

        public (string Subject, string HtmlBody) RenderEmail(EmailType type, Campaign campaign, ApplicationUser user)
        {
            decimal progressPercent = campaign.TargetAmount > 0 
                ? (campaign.CurrentAmount / campaign.TargetAmount) * 100 
                : 0;
            
            string progressText = progressPercent.ToString("0.##");

            string subject = "";
            string header = "";
            string cta = "View Campaign";

            switch (type)
            {
                case EmailType.Urgency:
                    subject = $"🚨 {campaign.Title} is ending soon!";
                    header = "We are in the final 72 hours!";
                    cta = "Donate Now Before It Ends";
                    break;
                case EmailType.StretchGoal:
                    subject = $"🎉 {campaign.Title} has been overfunded!";
                    header = "Stretch goal unlocked!";
                    cta = "See Our Next Steps";
                    break;
                case EmailType.Milestone:
                    subject = $"👏 Look what you helped achieve for {campaign.Title}!";
                    header = "We've crossed a major milestone!";
                    cta = "See The Impact";
                    break;
                default:
                    subject = $"Update from {campaign.Title}";
                    header = "Here is the latest update.";
                    cta = "Read More";
                    break;
            }

            string unsubscribeToken = GenerateUnsubscribeToken(user.Id.ToString());
            string unsubscribeLink = $"https://localhost:7196/api/digest/unsubscribe?token={unsubscribeToken}"; // Example URL

            string html = $@"
            <html>
            <body style='font-family: Arial, sans-serif; color: #333;'>
                <h2>Hello {user.UserName},</h2>
                <h3>{header}</h3>
                <p>The campaign <strong>{campaign.Title}</strong> is currently at {progressText}% of its goal.</p>
                <div style='background-color: #f3f3f3; border-radius: 4px; padding: 10px; margin: 20px 0;'>
                    <a href='https://localhost:7196/campaign/{campaign.Id}' style='background-color: #007bff; color: white; padding: 10px 20px; text-decoration: none; border-radius: 4px;'>{cta}</a>
                </div>
                <hr style='border: 1px solid #eee; margin-top: 40px;' />
                <p style='font-size: 12px; color: #999;'>
                    You are receiving this email because you opted into campaign updates.<br/>
                    <a href='{unsubscribeLink}' style='color: #999;'>Unsubscribe from these emails</a>
                </p>
            </body>
            </html>";

            return (subject, html);
        }

        private string GenerateUnsubscribeToken(string userId)
        {
            string secret = "SUPER_SECRET_KEY_12345";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(userId));
            var hashString = Convert.ToBase64String(hash);
            
            var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{userId}|{hashString}"));
            return payload;
        }
    }
}
