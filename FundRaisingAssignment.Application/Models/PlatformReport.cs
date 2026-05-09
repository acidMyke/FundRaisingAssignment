using System.ComponentModel.DataAnnotations;

namespace FundRaisingAssignment.Application.Models;


public class PlatformReport
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;

    // ---- Summary numbers ---------------------------------------------------
    public int TotalCampaigns { get; set; }
    public int NewCampaignsInPeriod { get; set; }

    public int TotalDonations { get; set; }
    public decimal TotalRaised { get; set; }
    public int UniqueDonors { get; set; }
    public decimal AverageDonation { get; set; }
    public decimal LargestDonation { get; set; }

    // ---- Breakdowns --------------------------------------------------------
    public List<CampaignStatusStat> ByStatus { get; set; } = new();
    public List<DailyStat> DailyTotals { get; set; } = new();
    public List<TopCampaignStat> TopCampaigns { get; set; } = new();

    public List<CategoryStat> ByCategory { get; set; } = new();
    public List<PaymentMethodStat> ByPaymentMethod { get; set; } = new();
    public List<CampaignProgressStat> CampaignProgress { get; set; } = new();
    public List<TopDonorStat> TopDonors { get; set; } = new();
    public List<DonationRow> Donations { get; set; } = new();
}

public class CategoryStat
{
    public string Category { get; set; } = "";
    public int CampaignCount { get; set; }
    public int DonationCount { get; set; }
    public decimal TotalRaised { get; set; }
}

public class PaymentMethodStat
{
    public string PaymentMethod { get; set; } = "";
    public int DonationCount { get; set; }
    public decimal TotalRaised { get; set; }
}

public class CampaignProgressStat
{
    public Guid CampaignId { get; set; }
    public string Title { get; set; } = "";
    public string Category { get; set; } = "";
    public string Status { get; set; } = "";
    public decimal FundingGoal { get; set; }
    public decimal CurrentAmount { get; set; }
    public decimal PercentFunded { get; set; }
    public int DonationsInPeriod { get; set; }
    public decimal RaisedInPeriod { get; set; }
    public double AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public DateTime? EndDate { get; set; }
    public string OwnerEmail { get; set; } = "";
}

public class TopDonorStat
{
    public string DonorLabel { get; set; } = "";
    public int DonationCount { get; set; }
    public decimal TotalGiven { get; set; }
}

public class DonationRow
{
    public DateTime CreatedAt { get; set; }
    public string ReceiptNumber { get; set; } = "";
    public string CampaignTitle { get; set; } = "";
    public string DonorLabel { get; set; } = "";
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = "";
    public string Status { get; set; } = "";
    public string Message { get; set; } = "";
}

public class CampaignStatusStat
{
    public string Status { get; set; } = "";
    public int CampaignCount { get; set; }
    public int DonationCount { get; set; }
    public decimal TotalRaised { get; set; }
}


public class DailyStat
{
    public DateTime Date { get; set; }
    public int DonationCount { get; set; }
    public decimal TotalRaised { get; set; }
}


public class TopCampaignStat
{
    public Guid CampaignId { get; set; }
    public string Title { get; set; } = "";
    public string Status { get; set; } = "";
    public int DonationCount { get; set; }
    public decimal TotalRaised { get; set; }
}

public class ReportInputModel : IValidatableObject
{
    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Start date")]
    public DateTime StartDate { get; set; } = DateTime.UtcNow.Date.AddDays(-30);

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "End date")]
    public DateTime EndDate { get; set; } = DateTime.UtcNow.Date;

    [Required]
    [Display(Name = "Format")]
    public ExportFormat Format { get; set; } = ExportFormat.Xlsx;


    public IEnumerable<ValidationResult> Validate(ValidationContext _)
    {
        if (EndDate < StartDate)
            yield return new ValidationResult(
                "End date must be on or after start date.",
                new[] { nameof(EndDate) });

        if (StartDate > DateTime.UtcNow.Date.AddDays(1))
            yield return new ValidationResult(
                "Start date cannot be in the future.",
                new[] { nameof(StartDate) });


        if ((EndDate - StartDate).TotalDays > 366 * 5)
            yield return new ValidationResult(
                "Date range cannot exceed 5 years.",
                new[] { nameof(EndDate) });
    }
}


public enum ExportFormat
{
    Csv = 0,
    Xlsx = 1
}