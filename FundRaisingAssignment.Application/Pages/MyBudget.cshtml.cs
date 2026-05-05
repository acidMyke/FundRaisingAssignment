using System.ComponentModel.DataAnnotations;
using FundRaisingAssignment.Application.Data;
using FundRaisingAssignment.Application.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FundRaisingAssignment.Application.Pages;


[Authorize]
public class MyBudgetModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<MyBudgetModel> _logger;

    public MyBudgetModel(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        ILogger<MyBudgetModel> logger)
    {
        _db = db;
        _userManager = userManager;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public StatusViewModel Status { get; private set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }


    public async Task<IActionResult> OnGetAsync()
    {
        var userIdStr = _userManager.GetUserId(User);
        if (userIdStr is null) return Challenge();
        var userId = Guid.Parse(userIdStr);

        try
        {
            var (goal, donationCount) = await EvaluateAndPersistAsync(userId);

            if (goal is not null)
            {
                Input.BudgetLimit  = goal.BudgetLimit;
                Input.TargetAmount = goal.TargetAmount;
                Input.Period       = goal.Period;
            }

            Status = BuildStatus(goal, donationCount);
        }
        catch (Exception ex)
        {
            // alt-flow 15a: retrieval/update failure — show a friendly banner and an empty status.
            _logger.LogError(ex, "Failed to load donation goal status for {UserId}", userId);
            ModelState.AddModelError(string.Empty,
                "We couldn't load your donation status right now. Please try again later.");
            Status = BuildStatus(null, 0);
        }

        return Page();
    }


    public async Task<IActionResult> OnPostSaveAsync()
    {
        var userIdStr = _userManager.GetUserId(User);
        if (userIdStr is null) return Challenge();
        var userId = Guid.Parse(userIdStr);

        // alt-flows 3a / 6a: explicit non-negative checks alongside the [Range] attributes.
        if (Input.BudgetLimit is < 0)
            ModelState.AddModelError(nameof(Input.BudgetLimit),
                "Budget must be a non-negative number.");
        if (Input.TargetAmount is < 0)
            ModelState.AddModelError(nameof(Input.TargetAmount),
                "Target must be a non-negative number.");

        if (!ModelState.IsValid)
        {
            Status = BuildStatus(await SafeLoadGoalAsync(userId), 0);
            return Page();
        }

        try
        {
            var goal = await _db.DonationGoals
                .FirstOrDefaultAsync(g => g.UserId == userId);

            var now = DateTime.UtcNow;

            if (goal is null)
            {
                goal = new DonationGoal { UserId = userId, CreatedAt = now };
                _db.DonationGoals.Add(goal);
            }

            // sub-flows 4a / 7a: only overwrite a field when the donee actually submitted a value.
            if (Input.BudgetLimit.HasValue)  goal.BudgetLimit  = Input.BudgetLimit;
            if (Input.TargetAmount.HasValue) goal.TargetAmount = Input.TargetAmount;
            goal.Period    = Input.Period;
            goal.UpdatedAt = now;

            await _db.SaveChangesAsync();
            await EvaluateAndPersistAsync(userId);

            _logger.LogInformation(
                "User {UserId} saved donation goal: budget={Budget}, target={Target}, period={Period}",
                userId, goal.BudgetLimit, goal.TargetAmount, goal.Period);

            StatusMessage = "Your goal has been saved.";
            return RedirectToPage();
        }
        catch (Exception ex)
        {
            // alt-flow 15a
            _logger.LogError(ex, "Failed to save donation goal for {UserId}", userId);
            ModelState.AddModelError(string.Empty,
                "We couldn't save your changes right now. Please try again later.");
            Status = BuildStatus(await SafeLoadGoalAsync(userId), 0);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostClearAsync()
    {
        var userIdStr = _userManager.GetUserId(User);
        if (userIdStr is null) return Challenge();
        var userId = Guid.Parse(userIdStr);

        try
        {
            var goal = await _db.DonationGoals
                .FirstOrDefaultAsync(g => g.UserId == userId);

            if (goal is not null)
            {
                _db.DonationGoals.Remove(goal);
                await _db.SaveChangesAsync();
                StatusMessage = "Your goal has been cleared.";
                _logger.LogInformation("User {UserId} cleared their donation goal.", userId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear donation goal for {UserId}", userId);
            StatusMessage = "We couldn't clear your goal right now. Please try again later.";
        }

        return RedirectToPage();
    }


    /// <summary>
    /// Use-case steps 9–14: load goal (tracked), recompute total + statuses from donation
    /// records, persist the snapshot back onto the DonationGoal row, and return it.
    /// EF only writes if a tracked field actually changed, so calling this on every GET is safe.
    /// </summary>
    private async Task<(DonationGoal? Goal, int DonationCount)> EvaluateAndPersistAsync(Guid userId)
    {
        var goal = await _db.DonationGoals.FirstOrDefaultAsync(g => g.UserId == userId);
        if (goal is null) return (null, 0);

        var (start, end) = ComputePeriodWindow(goal.Period, DateTime.UtcNow);

        var query = _db.Donations.AsNoTracking().Where(d => d.UserId == userId);
        if (start.HasValue) query = query.Where(d => d.CreatedAt >= start.Value);
        if (end.HasValue)   query = query.Where(d => d.CreatedAt <  end.Value);

        // alt-flow 9a: SumAsync over decimal? returns null when no rows; coalesce to 0.
        var count = await query.CountAsync();
        var total = await query.SumAsync(d => (decimal?)d.Amount) ?? 0m;

        goal.TotalDonated    = total;
        goal.BudgetStatus    = ClassifyBudget(total, goal.BudgetLimit);
        goal.TargetStatus    = ClassifyTarget(total, goal.TargetAmount);
        goal.LastEvaluatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return (goal, count);
    }

    private async Task<DonationGoal?> SafeLoadGoalAsync(Guid userId)
    {
        try
        {
            return await _db.DonationGoals.AsNoTracking()
                .FirstOrDefaultAsync(g => g.UserId == userId);
        }
        catch
        {
            return null;
        }
    }

    private StatusViewModel BuildStatus(DonationGoal? goal, int donationCount)
    {
        if (goal is null)
        {
            return new StatusViewModel
            {
                Period = GoalPeriod.Lifetime,
                BudgetStatus = BudgetStatus.NotSet,
                TargetStatus = TargetStatus.NotSet,
                DonationCount = donationCount,
            };
        }

        var (start, end) = ComputePeriodWindow(goal.Period, DateTime.UtcNow);
        return new StatusViewModel
        {
            TotalDonated         = goal.TotalDonated,
            DonationCount        = donationCount,
            BudgetLimit          = goal.BudgetLimit,
            TargetAmount         = goal.TargetAmount,
            Period               = goal.Period,
            PeriodStart          = start,
            PeriodEnd            = end,
            BudgetStatus         = goal.BudgetStatus,
            TargetStatus         = goal.TargetStatus,
            BudgetUsedPercent    = SafePercent(goal.TotalDonated, goal.BudgetLimit),
            TargetReachedPercent = SafePercent(goal.TotalDonated, goal.TargetAmount),
        };
    }

    private static (DateTime? Start, DateTime? End) ComputePeriodWindow(GoalPeriod period, DateTime nowUtc)
    {
        switch (period)
        {
            case GoalPeriod.Monthly:
                var monthStart = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                return (monthStart, monthStart.AddMonths(1));

            case GoalPeriod.Quarterly:
                var qMonth = ((nowUtc.Month - 1) / 3) * 3 + 1;
                var qStart = new DateTime(nowUtc.Year, qMonth, 1, 0, 0, 0, DateTimeKind.Utc);
                return (qStart, qStart.AddMonths(3));

            case GoalPeriod.Yearly:
                var yStart = new DateTime(nowUtc.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                return (yStart, yStart.AddYears(1));

            case GoalPeriod.Lifetime:
            default:
                return (null, null);
        }
    }

    private static BudgetStatus ClassifyBudget(decimal total, decimal? limit)
    {
        if (limit is null or <= 0) return Models.BudgetStatus.NotSet;
        if (total >  limit.Value)  return Models.BudgetStatus.Exceeded;
        if (total == limit.Value)  return Models.BudgetStatus.Reached;
        if (total >= limit.Value * 0.80m) return Models.BudgetStatus.NearLimit;
        return Models.BudgetStatus.WithinBudget;
    }

    private static TargetStatus ClassifyTarget(decimal total, decimal? target)
    {
        if (target is null or <= 0) return Models.TargetStatus.NotSet;
        if (total >= target.Value)  return Models.TargetStatus.Achieved;

        var pct = total / target.Value;
        if (pct >= 0.75m) return Models.TargetStatus.NearTarget;
        if (pct >= 0.25m) return Models.TargetStatus.InProgress;
        return Models.TargetStatus.FarFromTarget;
    }

    private static decimal SafePercent(decimal numerator, decimal? denominator)
        => denominator is null or <= 0
            ? 0m
            : Math.Round(numerator / denominator.Value * 100m, 2);

    public class InputModel
    {
        [Range(0, 99_999_999.99, ErrorMessage = "Budget must be a non-negative number.")]
        [Display(Name = "Budget limit")]
        public decimal? BudgetLimit { get; set; }

        [Range(0, 99_999_999.99, ErrorMessage = "Target must be a non-negative number.")]
        [Display(Name = "Donation target")]
        public decimal? TargetAmount { get; set; }

        [Required]
        [Display(Name = "Tracking period")]
        public GoalPeriod Period { get; set; } = GoalPeriod.Yearly;
    }

    public class StatusViewModel
    {
        public decimal TotalDonated { get; set; }
        public int DonationCount { get; set; }
        public decimal? BudgetLimit { get; set; }
        public decimal? TargetAmount { get; set; }
        public GoalPeriod Period { get; set; }
        public BudgetStatus BudgetStatus { get; set; }
        public TargetStatus TargetStatus { get; set; }
        public decimal BudgetUsedPercent { get; set; }
        public decimal TargetReachedPercent { get; set; }
        public DateTime? PeriodStart { get; set; }
        public DateTime? PeriodEnd { get; set; }
    }
}
