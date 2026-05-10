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
                        !u.IsEmailBounced &&
                        (!u.UnsubscribeCooldownUntil.HasValue || u.UnsubscribeCooldownUntil.Value <= executionTime) &&
                        (!u.LastCampaignUpdateSent.HasValue || u.LastCampaignUpdateSent.Value <= executionTime.AddDays(-7)))
            .ToListAsync();
    }

    public Task<List<Campaign>> GetActiveCampaignsAsync()
    {
        return dbContext.Campaigns.Where(c => c.Status == CampaignStatus.Active).ToListAsync();
    }

    public Task SaveChangesAsync()
    {
        return dbContext.SaveChangesAsync();
    }
}
