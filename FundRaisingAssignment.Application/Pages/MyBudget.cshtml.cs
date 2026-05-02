using System.ComponentModel.DataAnnotations;
using FundRaisingAssignment.Application.Data;
using FundRaisingAssignment.Application.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FundRaisingAssignment.Application.Pages;


[Authorize]   // Identity already wired up — only logged-in donees can hit this page
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

    /// <summary>Computed status — populated on every GET, displayed in cards.</summary>
    public StatusViewModel Status { get; private set; } = new();

    /// <summary>One-shot success/info message; survives the redirect-after-post.</summary>
    [TempData]
    public string? StatusMessage { get; set; }


    public async Task<IActionResult> OnGetAsync()
    {
        var userId = _userManager.GetUserId(User);
        if (userId is null) return Challenge();   // [Authorize] should prevent this, but defence in depth

        // Pre-fill the form with the donee's currently-saved goal (if any).
        var goal = await _db.DonationGoals
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.UserId == userId);

        if (goal is not null)
        {
            Input.BudgetLimit  = goal.BudgetLimit;
            Input.TargetAmount = goal.TargetAmount;
            Input.Period       = goal.Period;
        }

        await ComputeStatusAsync(userId);
        return Page();
    }


    public async Task<IActionResult> OnPostSaveAsync()
    {
        var userId = _userManager.GetUserId(User);
        if (userId is null) return Challenge();


        if (Input.BudgetLimit is < 0)
            ModelState.AddModelError(nameof(Input.BudgetLimit),
                "Budget must be a non-negative number.");
        if (Input.TargetAmount is < 0)
            ModelState.AddModelError(nameof(Input.TargetAmount),
                "Target must be a non-negative number.");

        if (!ModelState.IsValid)
        {
            // Re-render the form with errors visible AND keep the status panel up-to-date.
            await ComputeStatusAsync(userId);
            return Page();
        }

        // Upsert pattern — load existing or create new, then save.
        var goal = await _db.DonationGoals
            .FirstOrDefaultAsync(g => g.UserId == userId);

        var now = DateTime.UtcNow;

        if (goal is null)
        {
            // First time saving — create the row.
            goal = new DonationGoal
            {
                UserId    = userId,
                CreatedAt = now,
            };
            _db.DonationGoals.Add(goal);
        }


        goal.BudgetLimit  = Input.BudgetLimit;
        goal.TargetAmount = Input.TargetAmount;
        goal.Period       = Input.Period;
        goal.UpdatedAt    = now;

        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "User {UserId} saved donation goal: budget={Budget}, target={Target}, period={Period}",
            userId, goal.BudgetLimit, goal.TargetAmount, goal.Period);

        StatusMessage = "Your goal has been saved.";

       
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostClearAsync()
    {
        var userId = _userManager.GetUserId(User);
        if (userId is null) return Challenge();

        var goal = await _db.DonationGoals
            .FirstOrDefaultAsync(g => g.UserId == userId);

        if (goal is not null)
        {
            _db.DonationGoals.Remove(goal);
            await _db.SaveChangesAsync();
            StatusMessage = "Your goal has been cleared.";
            _logger.LogInformation("User {UserId} cleared their donation goal.", userId);
        }

        return RedirectToPage();
    }


    private async Task ComputeStatusAsync(string userId)
    {
        
        var goal = await _db.DonationGoals
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.UserId == userId);

      
        var period = goal?.Period ?? GoalPeriod.Lifetime;
        var (start, end) = ComputePeriodWindow(period, DateTime.UtcNow);

        
        var query = _db.Donations.AsNoTracking()
            .Where(d => d.UserId == userId);

        if (start.HasValue) query = query.Where(d => d.CreatedAt >= start.Value);
        if (end.HasValue)   query = query.Where(d => d.CreatedAt <  end.Value);

        
        var donationCount = await query.CountAsync();
        var totalDonated  = await query.SumAsync(d => (decimal?)d.Amount) ?? 0m;

        Status = new StatusViewModel
        {
            TotalDonated         = totalDonated,
            DonationCount        = donationCount,
            BudgetLimit          = goal?.BudgetLimit,
            TargetAmount         = goal?.TargetAmount,
            Period               = period,
            PeriodStart          = start,
            PeriodEnd            = end,
            BudgetStatus         = ClassifyBudget(totalDonated, goal?.BudgetLimit),
            TargetStatus         = ClassifyTarget(totalDonated, goal?.TargetAmount),
            BudgetUsedPercent    = SafePercent(totalDonated, goal?.BudgetLimit),
            TargetReachedPercent = SafePercent(totalDonated, goal?.TargetAmount),
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
                // Quarters start in months 1, 4, 7, 10.
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
        if (total >  limit.Value)  return Models.BudgetStatus.Exceeded;     // alt-flow 12a
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