using FundRaisingAssignment.Application.Data;
using FundRaisingAssignment.Application.Interfaces.Repositories;
using FundRaisingAssignment.Application.Models;
using FundRaisingAssignment.Application.Models.ProcessingModels;
using Microsoft.EntityFrameworkCore;

namespace FundRaisingAssignment.Application.Repositories;

public class CampaignDigestRepository(ApplicationDbContext dbContext) : ICampaignDigestRepository
{
    public Task<List<ApplicationUser>> GetUsersEligibleForDigestAsync(DateTime executionTime, int? limit)
    {
        var query = dbContext.Users
            .Where(u => u.ReceiveCampaignDigest &&
                        u.Email != null &&
                        !u.IsEmailBounced &&
                        (!u.UnsubscribeCooldownUntil.HasValue || u.UnsubscribeCooldownUntil.Value <= executionTime));

        if (limit.HasValue) query = query.Take(limit.Value);

        return query.OrderBy(u => u.LastCampaignUpdateSent).ToListAsync();
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

    public void AddDigestBatchRecord(DigestBatch record)
    {
        dbContext.Add(record);
    }

    public Task<DigestBatch?> GetDigestBatchByIdAsync(Guid id) => dbContext.DigestBatches.Where(b => b.Id == id).FirstOrDefaultAsync();
    
    public async Task AddDigestEntriesAsync(IEnumerable<DigestEntry> entries)
    {
        var entryList = entries.ToList();
        if (entryList.Count == 0) return;

        bool originalAutoDetect = dbContext.ChangeTracker.AutoDetectChangesEnabled;
        try
        {
            dbContext.ChangeTracker.AutoDetectChangesEnabled = false;
            dbContext.DigestEntries.AddRange(entryList);
            await dbContext.SaveChangesAsync();
        }
        finally
        {
            dbContext.ChangeTracker.AutoDetectChangesEnabled = originalAutoDetect;
        }

        foreach (var entry in entryList)
        {
            dbContext.Entry(entry).State = EntityState.Detached;
        }
    }

    public Task UpdateDigestEntryStatusAsync(Guid emailId, DigestEmailStatus status, string? reason) =>
        dbContext.DigestEntries
                 .Where(e => e.EmailId == emailId)
                 .ExecuteUpdateAsync(s => s.SetProperty(e => e.EmailStatus, status).SetProperty(e => e.EmailReason, reason));


    public Task<DigestBatch?> GetDigestBatchWithDetailsAsync(Guid batchId) =>
        dbContext.DigestBatches
                 .Include(b => b.Entries).ThenInclude(e => e.User)
                 .Include(b => b.Entries).ThenInclude(e => e.Campaign)
                 .AsSplitQuery()
                 .FirstOrDefaultAsync(b => b.Id == batchId);

    public Task<List<DigestBatch>> GetAllDigestBatchesAsync() =>
        dbContext.DigestBatches.OrderByDescending(b => b.TriggeredAt).ToListAsync();

}
