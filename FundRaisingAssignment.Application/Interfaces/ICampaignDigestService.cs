using FundRaisingAssignment.Application.Models.ViewModels;

namespace FundRaisingAssignment.Application.Interfaces;

public interface ICampaignDigestService
{
    Task<Guid> ValidateAndEnqueueAsync();
    Task ProcessAsync(Guid batchId);
    Task<List<DigestBatchSummaryViewModel>> GetAllDigestBatchesAsync();
    Task<DigestBatchDetailsViewModel?> GetDigestBatchDetailsAsync(Guid batchId);
}
