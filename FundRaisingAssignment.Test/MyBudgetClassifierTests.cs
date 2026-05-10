using FundRaisingAssignment.Application.Models;
using FundRaisingAssignment.Application.Pages;

// ─────────────────────────────────────────────────────────────────────────────
// Test plan: 10.8 Donation budget & target
// User Story: DN02 – Set Donation Budget and Target
// Backs: MyBudgetModel.ClassifyBudget / ClassifyTarget / ComputePeriodWindow
//        — pure helpers exposed via [InternalsVisibleTo].
// ─────────────────────────────────────────────────────────────────────────────

namespace FundRaisingAssignment.Test;

public class MyBudgetClassifierTests
{
    // ---- ClassifyBudget --------------------------------------------------

    [Fact]
    public void ClassifyBudget_NullLimit_IsNotSet()
    {
        Assert.Equal(BudgetStatus.NotSet, MyBudgetModel.ClassifyBudget(0m, null));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(50, -10)]
    public void ClassifyBudget_ZeroOrNegativeLimit_IsNotSet(decimal total, decimal limit)
    {
        Assert.Equal(BudgetStatus.NotSet, MyBudgetModel.ClassifyBudget(total, limit));
    }

    [Theory]
    [InlineData(0, 100)]   // 0%
    [InlineData(40, 100)]   // 40%
    [InlineData(79.99, 100)]  // 79.99%
    public void ClassifyBudget_BelowEightyPercent_IsWithinBudget(decimal total, decimal limit)
    {
        Assert.Equal(BudgetStatus.WithinBudget, MyBudgetModel.ClassifyBudget(total, limit));
    }

    [Theory]
    [InlineData(80, 100)]
    [InlineData(95, 100)]
    [InlineData(99.99, 100)]
    public void ClassifyBudget_BetweenEightyAndOneHundredPercent_IsNearLimit(decimal total, decimal limit)
    {
        Assert.Equal(BudgetStatus.NearLimit, MyBudgetModel.ClassifyBudget(total, limit));
    }

    [Fact]
    public void ClassifyBudget_ExactlyAtLimit_IsReached()
    {
        Assert.Equal(BudgetStatus.Reached, MyBudgetModel.ClassifyBudget(100m, 100m));
    }

    [Theory]
    [InlineData(101, 100)]
    [InlineData(150, 100)]
    public void ClassifyBudget_OverLimit_IsExceeded(decimal total, decimal limit)
    {
        Assert.Equal(BudgetStatus.Exceeded, MyBudgetModel.ClassifyBudget(total, limit));
    }

    // ---- ClassifyTarget --------------------------------------------------

    [Fact]
    public void ClassifyTarget_NullTarget_IsNotSet()
    {
        Assert.Equal(TargetStatus.NotSet, MyBudgetModel.ClassifyTarget(0m, null));
    }

    [Theory]
    [InlineData(50, 0)]
    [InlineData(50, -1)]
    public void ClassifyTarget_ZeroOrNegativeTarget_IsNotSet(decimal total, decimal target)
    {
        Assert.Equal(TargetStatus.NotSet, MyBudgetModel.ClassifyTarget(total, target));
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(24.99, 100)]
    public void ClassifyTarget_BelowTwentyFivePercent_IsFarFromTarget(decimal total, decimal target)
    {
        Assert.Equal(TargetStatus.FarFromTarget, MyBudgetModel.ClassifyTarget(total, target));
    }

    [Theory]
    [InlineData(25, 100)]
    [InlineData(50, 100)]
    [InlineData(74.99, 100)]
    public void ClassifyTarget_BetweenTwentyFiveAndSeventyFivePercent_IsInProgress(decimal total, decimal target)
    {
        Assert.Equal(TargetStatus.InProgress, MyBudgetModel.ClassifyTarget(total, target));
    }

    [Theory]
    [InlineData(75, 100)]
    [InlineData(99.99, 100)]
    public void ClassifyTarget_BetweenSeventyFiveAndOneHundredPercent_IsNearTarget(decimal total, decimal target)
    {
        Assert.Equal(TargetStatus.NearTarget, MyBudgetModel.ClassifyTarget(total, target));
    }

    [Theory]
    [InlineData(100, 100)]
    [InlineData(150, 100)]
    public void ClassifyTarget_AtOrAboveTarget_IsAchieved(decimal total, decimal target)
    {
        Assert.Equal(TargetStatus.Achieved, MyBudgetModel.ClassifyTarget(total, target));
    }

    // ---- ComputePeriodWindow --------------------------------------------

    [Fact]
    public void Window_Monthly_StartsOnFirstOfMonth_AndExcludesNextMonth()
    {
        var now = new DateTime(2026, 5, 15, 12, 0, 0, DateTimeKind.Utc);
        var (start, end) = MyBudgetModel.ComputePeriodWindow(GoalPeriod.Monthly, now);

        Assert.Equal(new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc), start);
        Assert.Equal(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), end);
    }

    [Theory]
    [InlineData(1, 1, 4)]   // Jan -> Q1 starts in Jan, ends Apr
    [InlineData(5, 4, 7)]   // May -> Q2 starts in Apr, ends Jul
    [InlineData(9, 7, 10)]  // Sep -> Q3 starts in Jul, ends Oct
    [InlineData(12, 10, 1)] // Dec -> Q4 starts in Oct, ends Jan(next year)
    public void Window_Quarterly_AlignsToCalendarQuarter(int month, int qStartMonth, int qEndMonth)
    {
        var now = new DateTime(2026, month, 10, 12, 0, 0, DateTimeKind.Utc);
        var (start, end) = MyBudgetModel.ComputePeriodWindow(GoalPeriod.Quarterly, now);

        Assert.Equal(qStartMonth, start!.Value.Month);
        Assert.Equal(qEndMonth, end!.Value.Month);
    }

    [Fact]
    public void Window_Yearly_StartsOnJanuaryFirst_AndExcludesNextYear()
    {
        var now = new DateTime(2026, 7, 4, 12, 0, 0, DateTimeKind.Utc);
        var (start, end) = MyBudgetModel.ComputePeriodWindow(GoalPeriod.Yearly, now);

        Assert.Equal(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), start);
        Assert.Equal(new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc), end);
    }

    [Fact]
    public void Window_Lifetime_HasNoBoundaries()
    {
        var (start, end) = MyBudgetModel.ComputePeriodWindow(GoalPeriod.Lifetime, DateTime.UtcNow);
        Assert.Null(start);
        Assert.Null(end);
    }
}
