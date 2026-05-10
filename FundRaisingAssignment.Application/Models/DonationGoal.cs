using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

// ─────────────────────────────────────────────────────────────────────────────
// User Story:   DN02 – Set Donation Budget and Target       Owner: Unnikrishna Pillai Karthik
// BCE Role:     Entity
// Description:  Per-user donation goal snapshot: budget limit, target amount,
//               tracking period, and the cached classification (BudgetStatus
//               and TargetStatus) computed from donation history.
// Notes:        Snapshot fields (TotalDonated, BudgetStatus, TargetStatus,
//               LastEvaluatedAt) are recomputed by MyBudgetModel on every GET
//               and POST so display stays in sync with the donations table.
// ─────────────────────────────────────────────────────────────────────────────

namespace FundRaisingAssignment.Application.Models;


public class DonationGoal
{

    public int Id { get; set; }



    [Required]
    public Guid UserId { get; set; }

    public ApplicationUser? User { get; set; }



    [Column(TypeName = "numeric(12,2)")]
    public decimal? BudgetLimit { get; set; }

    [Column(TypeName = "numeric(12,2)")]
    public decimal? TargetAmount { get; set; }


    [Required]
    public GoalPeriod Period { get; set; } = GoalPeriod.Yearly;

    [Column(TypeName = "numeric(12,2)")]
    public decimal TotalDonated { get; set; }

    public BudgetStatus BudgetStatus { get; set; } = BudgetStatus.NotSet;

    public TargetStatus TargetStatus { get; set; } = TargetStatus.NotSet;

    public DateTime? LastEvaluatedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}


public enum GoalPeriod
{
    Monthly = 0,
    Quarterly = 1,
    Yearly = 2,
    Lifetime = 3
}


public enum BudgetStatus
{
    NotSet = 0,        // No budget configured
    WithinBudget = 1,  // < 80% of budget used
    NearLimit = 2,     // 80–99% of budget used
    Reached = 3,       // exactly equal to budget
    Exceeded = 4       // over budget — alt-flow 12a
}

public enum TargetStatus
{
    NotSet = 0,
    FarFromTarget = 1, // < 25% reached
    InProgress = 2,    // 25–74% reached
    NearTarget = 3,    // 75–99% reached
    Achieved = 4       // >= target
}