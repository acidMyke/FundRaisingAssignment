namespace FundRaisingAssignment.Application.Interfaces;

public interface ICampaignDigestService
{
    Task<Guid> ValidateAndEnqueueAsync();
    Task ProcessAsync(Guid batchId);
}
