using FundRaisingAssignment.Application.Models;

namespace FundRaisingAssignment.Application.Boundaries;

/// <summary>
/// Boundary view model for the Campaign Page.
/// Maps to «boundary» CampaignPageView in BCE Diagram 1.
/// Extended to carry the full campaign detail needed by the redesigned public view.
/// </summary>
public class CampaignPageView
{
    // ── BCE Diagram 1 core properties ────────────────────────────────────────
    public decimal FundingGoal { get; set; }
    public DateTime DeadlineDate { get; set; }
    public string? ErrorMessage { get; set; }
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public int DaysRemaining => Math.Max(0, (DeadlineDate.Date - DateTime.Today).Days);

    // ── Full campaign display ─────────────────────────────────────────────────
    public Guid CampaignId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ShortDesc { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? CoverImageUrl { get; set; }
    public decimal CurrentAmount { get; set; }
    public string OwnerEmail { get; set; } = string.Empty;
    public CampaignStatus Status { get; set; }
    public double AverageRating { get; set; }
    public int ReviewCount { get; set; }

    // ── Percentage raised ─────────────────────────────────────────────────────
    public double ProgressPercent =>
        FundingGoal > 0 ? Math.Min(100, (double)CurrentAmount / (double)FundingGoal * 100) : 0;

    // ── BCE Diagram 1 boundary methods ────────────────────────────────────────

    /// <summary>showGoalAndCountdown – populates from a full Campaign entity.</summary>
    public CampaignPageView ShowGoalAndCountdown(Campaign c)
    {
        CampaignId = c.Id;
        FundingGoal = c.FundingGoal;
        DeadlineDate = c.EndDate ?? default;
        Title = c.Title;
        ShortDesc = c.ShortDescription;
        Description = c.Description;
        Category = c.Category.ToString();
        Location = c.Location;
        CoverImageUrl = c.CoverImageUrl;
        CurrentAmount = c.CurrentAmount;
        OwnerEmail = c.Owner?.Email ?? string.Empty;
        Status = c.Status;
        AverageRating = c.AverageRating;
        ReviewCount = c.ReviewCount;
        ErrorMessage = null;
        return this;
    }

    /// <summary>Overload for backwards compatibility (goal + date only).</summary>
    public CampaignPageView ShowGoalAndCountdown(decimal goalAmount, DateTime deadlineDate)
    {
        FundingGoal = goalAmount;
        DeadlineDate = deadlineDate;
        ErrorMessage = null;
        return this;
    }

    public CampaignPageView ShowError(string errorMsg)
    {
        ErrorMessage = errorMsg;
        return this;
    }
}
