using FundRaisingAssignment.Application.Models;
using FundRaisingAssignment.Application.Models.ProcessingModels;

namespace FundRaisingAssignment.Application.Interfaces.Repositories;

public interface ICampaignDigestRepository
{
    Task SaveChangesAsync();
    Task<List<ApplicationUser>> GetUsersEligibleForDigestAsync(DateTime executionTime, int? limit);
    Task<List<Campaign>> GetActiveCampaignsAsync();
    Task<List<UserCampaignInteractionDto>> GetPastDonationsForUsersAsync(IEnumerable<Guid> userIds);
    Task<List<UserCampaignInteractionDto>> GetPastVisitsForUsersAsync(IEnumerable<Guid> userIds);
    Task<Dictionary<Guid, CampaignSummaryContext>> GetCampaignSummariesAsync(IEnumerable<Guid> campaignIds);
    void AddDigestBatchRecord(DigestBatch record);
    Task<DigestBatch?> GetDigestBatchByIdAsync(Guid id);
    Task UpdateDigestEntryStatusAsync(Guid emailId, DigestEmailStatus status, string? reason);
    Task<DigestBatch?> GetDigestBatchWithDetailsAsync(Guid batchId);
    Task<List<DigestBatch>> GetAllDigestBatchesAsync();
}
