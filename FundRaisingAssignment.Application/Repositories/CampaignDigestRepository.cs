using FundRaisingAssignment.Application.Data;
using FundRaisingAssignment.Application.Interfaces;
using FundRaisingAssignment.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace FundRaisingAssignment.Application.Repositories
{
    public class CampaignDigestRepository(ApplicationDbContext dbContext) : ICampaignDigestRepository
    {
        public Task<List<Campaign>> GetCampaignsNeedingDigestAsync(DateTime executionTime)
        {
            return dbContext.Campaigns
                .Where(c => c.Status == CampaignStatus.Active &&
                            (!c.LastDigestSent.HasValue || c.LastDigestSent.Value < executionTime.AddHours(-24)))
                .ToListAsync();
        }

        public Task<List<Guid>> GetCampaignPastDonorIdsAsync(Guid campaignId)
        {
            return dbContext.DonationRecords
                .Where(dr => dr.CampaignId == campaignId)
                .Select(dr => dr.DoneeId)
                .Distinct()
                .ToListAsync();
        }

        public Task<List<Guid>> GetCampaignVisitorIdsAsync(Guid campaignId)
        {
            return dbContext.CampaignVisits
                .Where(cv => cv.CampaignId == campaignId)
                .Select(cv => cv.UserId)
                .Distinct()
                .ToListAsync();
        }

        public Task<List<ApplicationUser>> GetUsersByIdsAsync(IEnumerable<Guid> userIds)
        {
            return dbContext.Users
                .Where(u => userIds.Contains(u.Id))
                .ToListAsync();
        }

        public Task<List<DonationRecord>> GetUserDonationsForCampaignAsync(Guid userId, Guid campaignId)
        {
            return dbContext.DonationRecords
                .Where(dr => dr.DoneeId == userId && dr.CampaignId == campaignId)
                .ToListAsync();
        }

        public Task SaveChangesAsync()
        {
            return dbContext.SaveChangesAsync();
        }
    }
}
