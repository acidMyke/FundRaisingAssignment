// Request + result types for the canonical ICampaignService.DonateAsync flow.
// Lifted out of the deleted DonationService.cs so all four donation entry points
// (Razor page, campaign details page, anonymous donate page, Web API controller)
// can share one input record and one discriminated-union result.

using FundRaisingAssignment.Application.Models;

// ─────────────────────────────────────────────────────────────────────────────
// User Story:   DN03 – Make a Donation to a Campaign        Owner: Shared
// BCE Role:     Control (DTO contracts)
// Description:  Input record (MakeDonationInput) and discriminated-union
//               result (DonationResult.Success / CampaignNotFound /
//               CampaignNotActive / DeadlinePassed / InvalidAmount) shared
//               by every DN03 boundary.
// Notes:        Consolidated contracts; the deleted DonationService.cs used
//               to host these. Contributors: Josh, Karthik (consolidated).
// ─────────────────────────────────────────────────────────────────────────────

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
