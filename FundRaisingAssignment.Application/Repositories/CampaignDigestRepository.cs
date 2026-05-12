using FundRaisingAssignment.Application.Data;
using FundRaisingAssignment.Application.Interfaces.Repositories;
using FundRaisingAssignment.Application.Models;
using FundRaisingAssignment.Application.Models.ProcessingModels;
using Microsoft.EntityFrameworkCore;

namespace FundRaisingAssignment.Application.Repositories;

public class CampaignDigestRepository(ApplicationDbContext dbContext) : ICampaignDigestRepository
{
    public Task<List<ApplicationUser>> GetUsersEligibleForDigestAsync(DateTime executionTime)
    {
        return dbContext.Users
            .Where(u => u.ReceiveCampaignDigest &&
                        u.Email != null &&
                        !u.IsEmailBounced &&
                        (!u.UnsubscribeCooldownUntil.HasValue || u.UnsubscribeCooldownUntil.Value <= executionTime) &&
                        (!u.LastCampaignUpdateSent.HasValue || u.LastCampaignUpdateSent.Value <= executionTime.AddDays(-7)))
            .ToListAsync();
    }

    public Task<List<Campaign>> GetActiveCampaignsAsync()
    {
        return dbContext.Campaigns.Where(c => c.Status == CampaignStatus.Active).ToListAsync();
    }

    public Task<List<UserCampaignInteractionDto>> GetPastDonationsForUsersAsync(IEnumerable<Guid> userIds)
    {
        var userIdsList = userIds.ToList();
        return dbContext.Donations
            .Where(d => d.UserId.HasValue && userIdsList.Contains(d.UserId.Value))
            .Select(d => new UserCampaignInteractionDto
            {
                UserId = d.UserId!.Value,
                CampaignId = d.CampaignId,
                DonationAmount = d.Amount
            })
            .ToListAsync();
    }

    public Task<List<UserCampaignInteractionDto>> GetPastVisitsForUsersAsync(IEnumerable<Guid> userIds)
    {
        var userIdsList = userIds.ToList();
        return dbContext.CampaignVisits
            .Where(v => userIdsList.Contains(v.UserId))
            .Select(v => new UserCampaignInteractionDto
            {
                UserId = v.UserId,
                CampaignId = v.CampaignId,
                VisitCount = v.VisitCount
            })
            .ToListAsync();
    }

    public async Task<Dictionary<Guid, CampaignSummaryContext>> GetCampaignSummariesAsync(IEnumerable<Guid> campaignIds)
    {
        var campaignIdsList = campaignIds.ToList();
        var campaigns = await dbContext.Campaigns
            .Where(c => campaignIdsList.Contains(c.Id))
            .Select(c => new CampaignSummaryContext
            {
                Id = c.Id,
                Category = c.Category,
                OwnerId = c.OwnerId
            })
            .ToListAsync();

        return campaigns.ToDictionary(c => c.Id);
    }

    public Task SaveChangesAsync()
    {
        return dbContext.SaveChangesAsync();
    }
}
