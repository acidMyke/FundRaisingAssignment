// Request + result types for the canonical ICampaignService.DonateAsync flow.
// Lifted out of the deleted DonationService.cs so all four donation entry points
// (Razor page, campaign details page, anonymous donate page, Web API controller)
// can share one input record and one discriminated-union result.

using FundRaisingAssignment.Application.Models;

namespace FundRaisingAssignment.Application.Services;

public sealed record MakeDonationInput(
    Guid CampaignId,
    decimal Amount,
    string? Message,
    bool IsAnonymous,
    Guid? UserId = null,
    string DonorEmail = "Guest");

public abstract record DonationResult
{
    public sealed record Success(Donation Donation, bool GoalReached) : DonationResult;
    public sealed record CampaignNotFound : DonationResult;
    public sealed record CampaignNotActive(CampaignStatus CurrentStatus) : DonationResult;
    public sealed record DeadlinePassed : DonationResult;
    public sealed record InvalidAmount(string Reason) : DonationResult;
}
