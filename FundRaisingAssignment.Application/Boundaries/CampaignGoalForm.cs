using System.ComponentModel.DataAnnotations;

namespace FundRaisingAssignment.Application.Boundaries;

/// <summary>
/// Boundary input model for the Set / Edit Funding Goal page.
/// Maps to «boundary» CampaignGoalForm in BCE Diagram 1.
/// </summary>
public class CampaignGoalForm
{
    /// <summary>
    /// When set, the form updates an existing campaign.
    /// When null/empty, a new standalone campaign is created (legacy flow).
    /// </summary>
    public Guid? CampaignId { get; set; }

    [Required(ErrorMessage = "Funding goal is required.")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Goal must be a positive amount.")]
    [Display(Name = "Funding Goal (USD)")]
    public decimal GoalAmount { get; set; }

    [Required(ErrorMessage = "Deadline date is required.")]
    [DataType(DataType.Date)]
    [Display(Name = "Funding Deadline")]
    public DateTime DeadlineDate { get; set; } = DateTime.Today.AddDays(30);

    // ── UML boundary methods ──────────────────────────────────────────────────
    public static string NavigateToCampaignSettings() => "/Dashboard";
    public void EnterFundingGoal(decimal goalAmount) => GoalAmount = goalAmount;
    public void EnterDeadlineDate(DateTime d) => DeadlineDate = d;
    public void Submit() { /* handled by IndexModel.OnPost */ }
}
