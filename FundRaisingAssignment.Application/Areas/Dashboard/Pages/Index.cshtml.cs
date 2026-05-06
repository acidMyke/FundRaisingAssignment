using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using FundRaisingAssignment.Application.Boundaries;
using FundRaisingAssignment.Application.Models;
using FundRaisingAssignment.Application.Services;

namespace FundRaisingAssignment.Application.Areas.Dashboard.Pages;

/// <summary>
/// BCE Diagram 1 – Set / Edit Funding Goal.
/// Boundary: CampaignGoalForm (input) → FundingGoalController → CampaignPageView (output).
///
/// GET  /Dashboard?campaignId=...  → edit existing campaign goal + deadline
/// GET  /Dashboard                 → create a new standalone campaign (legacy)
/// POST                            → validateGoal + validateDeadline + saveGoalAndDeadline
///                                   → displayGoalAndCountdown (redirect to CampaignPage)
/// </summary>
public class IndexModel : PageModel
{
    private readonly ICampaignService _svc;
    private readonly UserManager<ApplicationUser> _um;

    public IndexModel(ICampaignService svc, UserManager<ApplicationUser> um)
    {
        _svc = svc;
        _um  = um;
    }

    [BindProperty]
    public CampaignGoalForm GoalForm { get; set; } = new();

    /// <summary>Campaign being edited (null when creating a new standalone goal).</summary>
    public Campaign? EditingCampaign { get; private set; }

    // ── OnGet: navigateToCampaignSettings ─────────────────────────────────────
    public async Task<IActionResult> OnGetAsync(Guid? campaignId)
    {
        GoalForm.DeadlineDate = DateTime.Today.AddDays(30);
        GoalForm.CampaignId   = campaignId;

        if (campaignId.HasValue)
        {
            EditingCampaign = await _svc.GetCampaignAsync(campaignId.Value);
            if (EditingCampaign is null) return NotFound();

            var user = await _um.GetUserAsync(User);
            if (user is null) return Challenge();
            if (EditingCampaign.OwnerId != user.Id) return Forbid();

            GoalForm.GoalAmount   = EditingCampaign.FundingGoal;
            GoalForm.DeadlineDate = EditingCampaign.EndDate;
        }

        return Page();
    }

    // ── OnPost: submitGoalAndDeadline ─────────────────────────────────────────
    public async Task<IActionResult> OnPostAsync()
    {
        // validateGoal(goalAmount) : boolean
        if (!ValidateGoal(GoalForm.GoalAmount))
            ModelState.AddModelError($"{nameof(GoalForm)}.{nameof(GoalForm.GoalAmount)}",
                "Goal amount must be greater than zero.");

        // validateDeadline(deadlineDate) : boolean
        if (!ValidateDeadline(GoalForm.DeadlineDate))
            ModelState.AddModelError($"{nameof(GoalForm)}.{nameof(GoalForm.DeadlineDate)}",
                "Deadline must be a future date.");

        if (!ModelState.IsValid)
        {
            if (GoalForm.CampaignId.HasValue)
                EditingCampaign = await _svc.GetCampaignAsync(GoalForm.CampaignId.Value);
            return Page();
        }

        var user = await _um.GetUserAsync(User);
        if (user is null) return Challenge();

        Campaign campaign;

        if (GoalForm.CampaignId.HasValue)
        {
            // updateGoalAndDeadline on existing campaign
            campaign = await _svc.UpdateGoalAndDeadlineAsync(
                GoalForm.CampaignId.Value,
                GoalForm.GoalAmount,
                GoalForm.DeadlineDate,
                user.Id);
        }
        else
        {
            // saveGoalAndDeadline – create new standalone campaign
            campaign = await _svc.SaveGoalAndDeadlineAsync(
                GoalForm.GoalAmount,
                GoalForm.DeadlineDate,
                user.Id);
        }

        // displayGoalAndCountdown → redirect to CampaignPage
        return RedirectToPage("/CampaignPage", new { area = "Dashboard", id = campaign.Id });
    }

    private static bool ValidateGoal(decimal g)     => g > 0;
    private static bool ValidateDeadline(DateTime d) => d.Date > DateTime.Today;
}
