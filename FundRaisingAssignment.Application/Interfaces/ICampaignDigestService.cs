namespace FundRaisingAssignment.Application.Interfaces;

public interface ICampaignDigestService
{
    Task<Guid> ValidateAndEnqueue();
    Task ProcessAsync(Guid batchId);
}
