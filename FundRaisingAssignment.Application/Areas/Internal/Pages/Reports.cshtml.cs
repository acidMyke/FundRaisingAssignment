using System.Globalization;
using System.Text;
using FundRaisingAssignment.Application.Data;
using FundRaisingAssignment.Application.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using OfficeOpenXml.Drawing.Chart;
using OfficeOpenXml.Style;

namespace FundRaisingAssignment.Application.Areas.Internal.Pages;

/// <summary>
/// PageModel for /Internal/Reports — Karthik's user story 2 (User Admin).
///
/// Three handlers:
///   OnGet              → show the empty form
///   OnPostPreviewAsync → run aggregation, show results on screen
///   OnPostExportAsync  → run aggregation, return CSV or XLSX file
/// </summary>
[Authorize(Roles = AdminRole)]
public class ReportsModel : PageModel
{
    /// <summary>Identity role required to access this page.</summary>
    public const string AdminRole = "Admin";

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<ReportsModel> _logger;

    /// <summary>True after the EPPlus license has been initialised once.</summary>
    private static bool _epplusLicenseInitialised;
    private static readonly object _epplusLock = new();

    public ReportsModel(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        ILogger<ReportsModel> logger)
    {
        _db = db;
        _userManager = userManager;
        _logger = logger;
    }

    [BindProperty]
    public ReportInputModel Input { get; set; } = new();

    public PlatformReport? PreviewReport { get; private set; }

    /// <summary>Set after a successful export so the page can render file details + download link.</summary>
    public ExportFile? SuccessFile { get; private set; }

    [TempData]
    public string? StatusMessage { get; set; }

    // ======================================================================
    //  GET — empty form, or success view if redirected after export
    // ======================================================================
    public async Task<IActionResult> OnGetAsync(Guid? successId, CancellationToken ct)
    {
        if (successId.HasValue)
        {
            // 13a — retrieve the generated export file by export ID
            var file = await _db.ExportFiles
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == successId.Value, ct);

            if (file is null)
            {
                // 13b — file retrieval failure
                StatusMessage = "We couldn't retrieve the exported file. Please try again.";
            }
            else
            {
                SuccessFile = file;
            }
        }
        return Page();
    }

    // ======================================================================
    //  POST ?handler=Preview
    // ======================================================================
    public async Task<IActionResult> OnPostPreviewAsync()
    {
        if (!ModelState.IsValid) return Page();

        try
        {
            PreviewReport = await GenerateReportAsync(Input.StartDate, Input.EndDate);
            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Report preview failed");
            StatusMessage = "Couldn't generate the report — please try again.";
            return Page();
        }
    }

    // ======================================================================
    //  POST ?handler=Export
    //
    //  Persists the generated file as an ExportFile row, then redirects to
    //  the GET handler with ?successId=… so the admin sees a success card
    //  with file details and a Download button (use case steps 12–15).
    // ======================================================================
    public async Task<IActionResult> OnPostExportAsync(CancellationToken ct)
    {
        // 5a — invalid input
        if (!ModelState.IsValid) return Page();

        // 6a — verify admin identity (role is enforced by [Authorize] above)
        var admin = await _userManager.GetUserAsync(User);
        if (admin is null)
        {
            StatusMessage = "Authorization failed. Please sign in again.";
            return Page();
        }

        // 7 / 7a — generate the report
        PlatformReport report;
        try
        {
            report = await GenerateReportAsync(Input.StartDate, Input.EndDate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Report generation failed");
            StatusMessage = "Couldn't generate the report — please try again later.";
            return Page();
        }

        // 9–11 / 10b / 11b — convert to the selected format
        byte[] bytes;
        string contentType;
        string fileName;
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        try
        {
            switch (Input.Format)
            {
                case ExportFormat.Csv:
                    bytes = ExportToCsv(report);
                    contentType = "text/csv";
                    fileName = $"platform-report-{stamp}.csv";
                    break;

                case ExportFormat.Xlsx:
                    bytes = ExportToXlsx(report);
                    contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                    fileName = $"platform-report-{stamp}.xlsx";
                    break;

                default:
                    ModelState.AddModelError(nameof(Input.Format), "Unsupported export format.");
                    return Page();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Format} export failed", Input.Format);
            StatusMessage = $"{Input.Format} export failed — please try again later.";
            return Page();
        }

        // 12 — create the export file record
        var export = new ExportFile
        {
            Id = Guid.NewGuid(),
            CreatedByAdminId = admin.Id,
            Format = Input.Format,
            FileName = fileName,
            ContentType = contentType,
            Content = bytes,
            Size = bytes.LongLength,
            RangeStart = Input.StartDate,
            RangeEnd = Input.EndDate,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            _db.ExportFiles.Add(export);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // 15b — system processing failure
            _logger.LogError(ex, "Failed to persist export file record");
            StatusMessage = "Export failed — please try again later.";
            return Page();
        }

        _logger.LogInformation(
            "Export {ExportId} ({Format}, {Size} bytes) created by admin {AdminId}",
            export.Id, export.Format, export.Size, admin.Id);

        // 14–15 — return success and file details to the interface
        return RedirectToPage(new { successId = export.Id });
    }

    // ======================================================================
    //  GET ?handler=Download&id=… — sub-flow 13a / step 16
    // ======================================================================
    public async Task<IActionResult> OnGetDownloadAsync(Guid id, CancellationToken ct)
    {
        var file = await _db.ExportFiles
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id, ct);

        if (file is null)
        {
            // 13b — file retrieval failure
            StatusMessage = "Export file not found.";
            return RedirectToPage();
        }

        return File(file.Content, file.ContentType, file.FileName);
    }

    // ======================================================================
    //  AGGREGATION
    //
    //  Uses the Donation.Campaign NAVIGATION PROPERTY instead of manual joins.
    //  This sidesteps CS1941 entirely — EF Core figures out the join itself
    //  based on the foreign key configured in OnModelCreating, so it doesn't
    //  matter whether Campaign.Id is int / Guid / string / long.
    // ======================================================================
    private async Task<PlatformReport> GenerateReportAsync(DateTime start, DateTime end)
    {
        // End-of-day handling: include donations made any time on the end date
        // by using exclusive next-midnight as the upper bound.
        var startUtc = DateTime.SpecifyKind(start.Date, DateTimeKind.Utc);
        var endUtc = DateTime.SpecifyKind(end.Date.AddDays(1), DateTimeKind.Utc);

        var campaignsQ = _db.Campaigns.AsNoTracking();
        var donationsQ = _db.Donations.AsNoTracking()
            .Where(d => d.CreatedAt >= startUtc && d.CreatedAt < endUtc);

        // ---- Headline numbers ----
        var totalCampaigns = await campaignsQ.CountAsync();
        var newCampaignsInPeriod = await campaignsQ
            .CountAsync(c => c.CreatedAt >= startUtc && c.CreatedAt < endUtc);

        var donationCount = await donationsQ.CountAsync();
        var totalRaised = await donationsQ.SumAsync(d => (decimal?)d.Amount) ?? 0m;
        var largest = await donationsQ.MaxAsync(d => (decimal?)d.Amount) ?? 0m;
        var uniqueDonors = await donationsQ.Select(d => d.UserId).Distinct().CountAsync();
        var avg = donationCount > 0 ? Math.Round(totalRaised / donationCount, 2) : 0m;

        // ---- By campaign status ----
        // Step A: donations per status — use the navigation property d.Campaign
        //         instead of a manual join. EF Core translates this into a
        //         server-side JOIN automatically using the FK relationship.
        var donationsByStatus = await donationsQ
            .Where(d => d.Campaign != null)
            .GroupBy(d => d.Campaign!.Status)
            .Select(g => new
            {
                Status = g.Key,
                DonationCount = g.Count(),
                TotalRaised = g.Sum(d => d.Amount),
            })
            .ToListAsync();

        // Step B: campaigns per status (no join needed)
        var campaignsByStatus = await campaignsQ
            .GroupBy(c => c.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        // Step C: stitch the two together in memory
        var byStatus = campaignsByStatus
            .Select(cs =>
            {
                var dStat = donationsByStatus.FirstOrDefault(x => Equals(x.Status, cs.Status));
                return new CampaignStatusStat
                {
                    Status = cs.Status.ToString() ?? "Unknown",
                    CampaignCount = cs.Count,
                    DonationCount = dStat?.DonationCount ?? 0,
                    TotalRaised = dStat?.TotalRaised ?? 0m,
                };
            })
            .OrderByDescending(s => s.TotalRaised)
            .ToList();

        // ---- Daily totals ----
        var daily = await donationsQ
            .GroupBy(d => d.CreatedAt.Date)
            .Select(g => new DailyStat
            {
                Date = g.Key,
                DonationCount = g.Count(),
                TotalRaised = g.Sum(d => d.Amount),
            })
            .OrderBy(d => d.Date)
            .ToListAsync();

        // ---- Top campaigns (also uses navigation property) ----
        var top = await donationsQ
            .Where(d => d.Campaign != null)
            .GroupBy(d => new { d.Campaign!.Id, d.Campaign.Title, d.Campaign.Status })
            .Select(g => new TopCampaignStat
            {
                CampaignId = g.Key.Id,
                Title = g.Key.Title,
                Status = g.Key.Status.ToString() ?? "Unknown",
                DonationCount = g.Count(),
                TotalRaised = g.Sum(d => d.Amount),
            })
            .OrderByDescending(t => t.TotalRaised)
            .Take(20)
            .ToListAsync();

        // ---- By category ----
        var donationsByCategory = await donationsQ
            .Where(d => d.Campaign != null)
            .GroupBy(d => d.Campaign!.Category)
            .Select(g => new
            {
                Category = g.Key,
                DonationCount = g.Count(),
                TotalRaised = g.Sum(d => d.Amount),
            })
            .ToListAsync();

        var campaignsByCategory = await campaignsQ
            .GroupBy(c => c.Category)
            .Select(g => new { Category = g.Key, Count = g.Count() })
            .ToListAsync();

        var byCategory = campaignsByCategory
            .Select(cc =>
            {
                var dStat = donationsByCategory.FirstOrDefault(x => Equals(x.Category, cc.Category));
                return new CategoryStat
                {
                    Category = cc.Category.ToString() ?? "Other",
                    CampaignCount = cc.Count,
                    DonationCount = dStat?.DonationCount ?? 0,
                    TotalRaised = dStat?.TotalRaised ?? 0m,
                };
            })
            .OrderByDescending(c => c.TotalRaised)
            .ToList();

        // ---- By payment method ----
        var byPaymentMethodRaw = await donationsQ
            .GroupBy(d => d.PaymentMethod)
            .Select(g => new
            {
                Method = g.Key,
                DonationCount = g.Count(),
                TotalRaised = g.Sum(d => d.Amount),
            })
            .ToListAsync();

        var byPaymentMethod = byPaymentMethodRaw
            .Select(p => new PaymentMethodStat
            {
                PaymentMethod = string.IsNullOrWhiteSpace(p.Method) ? "Other" : p.Method,
                DonationCount = p.DonationCount,
                TotalRaised = p.TotalRaised,
            })
            .OrderByDescending(p => p.TotalRaised)
            .ToList();

        // ---- Campaign progress (campaigns with activity in or created in range) ----
        var donationCampaignIds = await donationsQ
            .Select(d => d.CampaignId)
            .Distinct()
            .ToListAsync();

        var relevantCampaigns = await campaignsQ
            .Where(c => donationCampaignIds.Contains(c.Id)
                     || (c.CreatedAt >= startUtc && c.CreatedAt < endUtc))
            .Select(c => new
            {
                c.Id,
                c.Title,
                c.Category,
                c.Status,
                c.FundingGoal,
                c.CurrentAmount,
                c.AverageRating,
                c.ReviewCount,
                c.EndDate,
                OwnerEmail = c.Owner != null ? c.Owner.Email : null,
            })
            .ToListAsync();

        var periodActivity = await donationsQ
            .GroupBy(d => d.CampaignId)
            .Select(g => new
            {
                CampaignId = g.Key,
                DonationCount = g.Count(),
                Raised = g.Sum(d => d.Amount),
            })
            .ToListAsync();

        var progress = relevantCampaigns
            .Select(c =>
            {
                var act = periodActivity.FirstOrDefault(p => p.CampaignId == c.Id);
                var pct = c.FundingGoal > 0
                    ? Math.Round(c.CurrentAmount * 100m / c.FundingGoal, 2)
                    : 0m;
                return new CampaignProgressStat
                {
                    CampaignId = c.Id,
                    Title = c.Title,
                    Category = c.Category.ToString() ?? "Other",
                    Status = c.Status.ToString() ?? "Unknown",
                    FundingGoal = c.FundingGoal,
                    CurrentAmount = c.CurrentAmount,
                    PercentFunded = pct,
                    DonationsInPeriod = act?.DonationCount ?? 0,
                    RaisedInPeriod = act?.Raised ?? 0m,
                    AverageRating = c.AverageRating,
                    ReviewCount = c.ReviewCount,
                    EndDate = c.EndDate,
                    OwnerEmail = c.OwnerEmail ?? "",
                };
            })
            .OrderByDescending(p => p.RaisedInPeriod)
            .ThenByDescending(p => p.CurrentAmount)
            .ToList();

        // ---- Top donors (Anonymous bucketed together) ----
        var donorsRaw = await donationsQ
            .Select(d => new { d.IsAnonymous, d.DonorEmail, d.Amount })
            .ToListAsync();

        var topDonors = donorsRaw
            .GroupBy(d => d.IsAnonymous
                          || string.IsNullOrWhiteSpace(d.DonorEmail)
                          || d.DonorEmail == "Anonymous"
                ? "Anonymous"
                : d.DonorEmail)
            .Select(g => new TopDonorStat
            {
                DonorLabel = g.Key,
                DonationCount = g.Count(),
                TotalGiven = g.Sum(x => x.Amount),
            })
            .OrderByDescending(d => d.TotalGiven)
            .Take(20)
            .ToList();

        // ---- Raw donations (capped to keep exports manageable) ----
        const int donationCap = 5000;
        var rawDonations = await donationsQ
            .OrderByDescending(d => d.CreatedAt)
            .Take(donationCap)
            .Select(d => new
            {
                d.CreatedAt,
                ReceiptNumber = d.ReceiptNumber ?? "",
                CampaignTitle = d.Campaign != null ? d.Campaign.Title : "",
                DonorLabel = d.IsAnonymous ? "Anonymous" : d.DonorEmail,
                d.Amount,
                d.PaymentMethod,
                d.Status,
                Message = d.Message ?? "",
            })
            .ToListAsync();

        var donationRows = rawDonations
            .Select(d => new DonationRow
            {
                CreatedAt = d.CreatedAt,
                ReceiptNumber = d.ReceiptNumber,
                CampaignTitle = d.CampaignTitle,
                DonorLabel = string.IsNullOrWhiteSpace(d.DonorLabel) ? "Anonymous" : d.DonorLabel,
                Amount = d.Amount,
                PaymentMethod = string.IsNullOrWhiteSpace(d.PaymentMethod) ? "Other" : d.PaymentMethod,
                Status = d.Status.ToString(),
                Message = d.Message,
            })
            .ToList();

        var report = new PlatformReport
        {
            StartDate = start,
            EndDate = end,
            TotalCampaigns = totalCampaigns,
            NewCampaignsInPeriod = newCampaignsInPeriod,
            TotalDonations = donationCount,
            TotalRaised = totalRaised,
            UniqueDonors = uniqueDonors,
            AverageDonation = avg,
            LargestDonation = largest,
            ByStatus = byStatus,
            DailyTotals = daily,
            TopCampaigns = top,
            ByCategory = byCategory,
            ByPaymentMethod = byPaymentMethod,
            CampaignProgress = progress,
            TopDonors = topDonors,
            Donations = donationRows,
        };

        _logger.LogInformation(
            "Generated report {Start:d}–{End:d}: {Donations} donations, total {Total:C}",
            start, end, donationCount, totalRaised);

        return report;
    }

    // ======================================================================
    //  CSV EXPORT  (Karthik's Step 10a)
    // ======================================================================
    internal static byte[] ExportToCsv(PlatformReport r)
    {
        var sb = new StringBuilder();
        var inv = CultureInfo.InvariantCulture;

        WriteRow(sb, "Givvn Platform Report");
        WriteRow(sb, $"Period: {r.StartDate:yyyy-MM-dd} to {r.EndDate:yyyy-MM-dd}");
        WriteRow(sb, $"Generated: {r.GeneratedAtUtc:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();

        WriteRow(sb, "SUMMARY");
        WriteRow(sb, "Metric", "Value");
        WriteRow(sb, "Total Campaigns", r.TotalCampaigns.ToString(inv));
        WriteRow(sb, "New Campaigns in Period", r.NewCampaignsInPeriod.ToString(inv));
        WriteRow(sb, "Total Donations", r.TotalDonations.ToString(inv));
        WriteRow(sb, "Total Raised", r.TotalRaised.ToString("F2", inv));
        WriteRow(sb, "Unique Donors", r.UniqueDonors.ToString(inv));
        WriteRow(sb, "Average Donation", r.AverageDonation.ToString("F2", inv));
        WriteRow(sb, "Largest Donation", r.LargestDonation.ToString("F2", inv));
        sb.AppendLine();

        WriteRow(sb, "BY STATUS");
        WriteRow(sb, "Status", "Campaigns", "Donations", "Total Raised");
        foreach (var s in r.ByStatus)
            WriteRow(sb, s.Status,
                          s.CampaignCount.ToString(inv),
                          s.DonationCount.ToString(inv),
                          s.TotalRaised.ToString("F2", inv));
        sb.AppendLine();

        WriteRow(sb, "DAILY TOTALS");
        WriteRow(sb, "Date", "Donations", "Total Raised");
        foreach (var d in r.DailyTotals)
            WriteRow(sb, d.Date.ToString("yyyy-MM-dd"),
                          d.DonationCount.ToString(inv),
                          d.TotalRaised.ToString("F2", inv));
        sb.AppendLine();

        WriteRow(sb, "TOP CAMPAIGNS");
        WriteRow(sb, "Campaign ID", "Title", "Status", "Donations", "Total Raised");
        foreach (var t in r.TopCampaigns)
            WriteRow(sb, t.CampaignId.ToString(),
                          t.Title,
                          t.Status,
                          t.DonationCount.ToString(inv),
                          t.TotalRaised.ToString("F2", inv));
        sb.AppendLine();

        WriteRow(sb, "BY CATEGORY");
        WriteRow(sb, "Category", "Campaigns", "Donations", "Total Raised");
        foreach (var c in r.ByCategory)
            WriteRow(sb, c.Category,
                          c.CampaignCount.ToString(inv),
                          c.DonationCount.ToString(inv),
                          c.TotalRaised.ToString("F2", inv));
        sb.AppendLine();

        WriteRow(sb, "BY PAYMENT METHOD");
        WriteRow(sb, "Payment Method", "Donations", "Total Raised");
        foreach (var p in r.ByPaymentMethod)
            WriteRow(sb, p.PaymentMethod,
                          p.DonationCount.ToString(inv),
                          p.TotalRaised.ToString("F2", inv));
        sb.AppendLine();

        WriteRow(sb, "TOP DONORS");
        WriteRow(sb, "Donor", "Donations", "Total Given");
        foreach (var d in r.TopDonors)
            WriteRow(sb, d.DonorLabel,
                          d.DonationCount.ToString(inv),
                          d.TotalGiven.ToString("F2", inv));
        sb.AppendLine();

        WriteRow(sb, "CAMPAIGN PROGRESS");
        WriteRow(sb, "Campaign ID", "Title", "Category", "Status",
                     "Funding Goal", "Current Amount", "% Funded",
                     "Donations In Period", "Raised In Period",
                     "Avg Rating", "Reviews", "End Date", "Owner");
        foreach (var p in r.CampaignProgress)
            WriteRow(sb, p.CampaignId.ToString(),
                          p.Title,
                          p.Category,
                          p.Status,
                          p.FundingGoal.ToString("F2", inv),
                          p.CurrentAmount.ToString("F2", inv),
                          p.PercentFunded.ToString("F2", inv),
                          p.DonationsInPeriod.ToString(inv),
                          p.RaisedInPeriod.ToString("F2", inv),
                          p.AverageRating.ToString("F2", inv),
                          p.ReviewCount.ToString(inv),
                          p.EndDate.HasValue ? p.EndDate.Value.ToString("yyyy-MM-dd") : "",
                          p.OwnerEmail);
        sb.AppendLine();

        WriteRow(sb, "DONATIONS");
        WriteRow(sb, "Date (UTC)", "Receipt #", "Campaign", "Donor",
                     "Amount", "Payment Method", "Status", "Message");
        foreach (var d in r.Donations)
            WriteRow(sb, d.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                          d.ReceiptNumber,
                          d.CampaignTitle,
                          d.DonorLabel,
                          d.Amount.ToString("F2", inv),
                          d.PaymentMethod,
                          d.Status,
                          d.Message);

        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(sb.ToString());
    }

    private static void WriteRow(StringBuilder sb, params string[] cells)
    {
        for (int i = 0; i < cells.Length; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(EscapeCsv(cells[i]));
        }
        sb.Append("\r\n");
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        bool needsQuotes = value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0;
        if (!needsQuotes) return value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    // ======================================================================
    //  XLSX EXPORT  (Karthik's Step 11a) — uses EPPlus 8
    // ======================================================================
    internal static byte[] ExportToXlsx(PlatformReport r)
    {
        EnsureEpplusLicense();

        using var pkg = new ExcelPackage();

        // ---- Sheet 1: Summary --------------------------------------------
        var s1 = pkg.Workbook.Worksheets.Add("Summary");

        s1.Cells["A1"].Value = "Givvn Platform Report";
        s1.Cells["A1"].Style.Font.Size = 16;
        s1.Cells["A1"].Style.Font.Bold = true;
        s1.Cells["A2"].Value = $"Period: {r.StartDate:yyyy-MM-dd} -> {r.EndDate:yyyy-MM-dd}";
        s1.Cells["A3"].Value = $"Generated: {r.GeneratedAtUtc:yyyy-MM-dd HH:mm} UTC";

        s1.Cells["A5"].Value = "Metric";
        s1.Cells["B5"].Value = "Value";
        StyleHeader(s1.Cells["A5:B5"]);

        var rows = new (string Label, object Value, string? Fmt)[]
        {
            ("Total Campaigns",         r.TotalCampaigns,        "#,##0"),
            ("New Campaigns in Period", r.NewCampaignsInPeriod,  "#,##0"),
            ("Total Donations",         r.TotalDonations,        "#,##0"),
            ("Total Raised",            r.TotalRaised,           "$#,##0.00"),
            ("Unique Donors",           r.UniqueDonors,          "#,##0"),
            ("Average Donation",        r.AverageDonation,       "$#,##0.00"),
            ("Largest Donation",        r.LargestDonation,       "$#,##0.00"),
        };

        for (int i = 0; i < rows.Length; i++)
        {
            var row = i + 6;
            s1.Cells[row, 1].Value = rows[i].Label;
            s1.Cells[row, 2].Value = rows[i].Value;
            if (rows[i].Fmt is not null)
                s1.Cells[row, 2].Style.Numberformat.Format = rows[i].Fmt;
        }
        s1.Cells.AutoFitColumns();

        // ---- Sheet 2: By Status ------------------------------------------
        var s2 = pkg.Workbook.Worksheets.Add("By Status");
        WriteSheetHeader(s2, "Status", "Campaigns", "Donations", "Total Raised");

        for (int i = 0; i < r.ByStatus.Count; i++)
        {
            var row = i + 2;
            var x = r.ByStatus[i];
            s2.Cells[row, 1].Value = x.Status;
            s2.Cells[row, 2].Value = x.CampaignCount;
            s2.Cells[row, 3].Value = x.DonationCount;
            s2.Cells[row, 4].Value = x.TotalRaised;
            s2.Cells[row, 4].Style.Numberformat.Format = "$#,##0.00";
        }
        s2.Cells.AutoFitColumns();

        if (r.ByStatus.Count > 0)
        {
            var lastRow = r.ByStatus.Count + 1;
            var statusChart = s2.Drawings.AddChart("StatusChart", eChartType.ColumnStacked);
            statusChart.Title.Text = "Campaigns vs donations by status";
            var sc1 = statusChart.Series.Add(s2.Cells[2, 2, lastRow, 2], s2.Cells[2, 1, lastRow, 1]);
            sc1.Header = "Campaigns";
            var sc2 = statusChart.Series.Add(s2.Cells[2, 3, lastRow, 3], s2.Cells[2, 1, lastRow, 1]);
            sc2.Header = "Donations";
            statusChart.SetPosition(1, 0, 5, 10);
            statusChart.SetSize(620, 360);
        }

        // ---- Sheet 3: Daily Totals ---------------------------------------
        var s3 = pkg.Workbook.Worksheets.Add("Daily Totals");
        WriteSheetHeader(s3, "Date", "Donations", "Total Raised");

        for (int i = 0; i < r.DailyTotals.Count; i++)
        {
            var row = i + 2;
            var d = r.DailyTotals[i];
            s3.Cells[row, 1].Value = d.Date;
            s3.Cells[row, 1].Style.Numberformat.Format = "yyyy-mm-dd";
            s3.Cells[row, 2].Value = d.DonationCount;
            s3.Cells[row, 3].Value = d.TotalRaised;
            s3.Cells[row, 3].Style.Numberformat.Format = "$#,##0.00";
        }
        s3.Cells.AutoFitColumns();

        if (r.DailyTotals.Count > 0)
        {
            var lastRow = r.DailyTotals.Count + 1;
            var dailyChart = s3.Drawings.AddChart("DailyChart", eChartType.Line);
            dailyChart.Title.Text = "Daily raised over period";
            var ds = dailyChart.Series.Add(s3.Cells[2, 3, lastRow, 3], s3.Cells[2, 1, lastRow, 1]);
            ds.Header = "Total raised";
            dailyChart.SetPosition(1, 0, 4, 10);
            dailyChart.SetSize(720, 320);
        }

        // ---- Sheet 4: Top Campaigns --------------------------------------
        var s4 = pkg.Workbook.Worksheets.Add("Top Campaigns");
        WriteSheetHeader(s4, "Campaign ID", "Title", "Status", "Donations", "Total Raised");

        for (int i = 0; i < r.TopCampaigns.Count; i++)
        {
            var row = i + 2;
            var t = r.TopCampaigns[i];
            s4.Cells[row, 1].Value = t.CampaignId;
            s4.Cells[row, 2].Value = t.Title;
            s4.Cells[row, 3].Value = t.Status;
            s4.Cells[row, 4].Value = t.DonationCount;
            s4.Cells[row, 5].Value = t.TotalRaised;
            s4.Cells[row, 5].Style.Numberformat.Format = "$#,##0.00";
        }
        s4.Cells.AutoFitColumns();

        if (r.TopCampaigns.Count > 0)
        {
            // Top 10 only — list is already sorted desc by TotalRaised
            var lastChartRow = Math.Min(r.TopCampaigns.Count, 10) + 1;
            var topChart = s4.Drawings.AddChart("TopCampaignsChart", eChartType.BarClustered);
            topChart.Title.Text = "Top campaigns by raised";
            var ts = topChart.Series.Add(s4.Cells[2, 5, lastChartRow, 5], s4.Cells[2, 2, lastChartRow, 2]);
            ts.Header = "Total raised";
            topChart.SetPosition(1, 0, 7, 10);
            topChart.SetSize(720, 380);
        }

        // ---- Sheet 5: By Category ----------------------------------------
        var s5 = pkg.Workbook.Worksheets.Add("By Category");
        WriteSheetHeader(s5, "Category", "Campaigns", "Donations", "Total Raised");

        for (int i = 0; i < r.ByCategory.Count; i++)
        {
            var row = i + 2;
            var c = r.ByCategory[i];
            s5.Cells[row, 1].Value = c.Category;
            s5.Cells[row, 2].Value = c.CampaignCount;
            s5.Cells[row, 3].Value = c.DonationCount;
            s5.Cells[row, 4].Value = c.TotalRaised;
            s5.Cells[row, 4].Style.Numberformat.Format = "$#,##0.00";
        }
        s5.Cells.AutoFitColumns();

        if (r.ByCategory.Any(c => c.TotalRaised > 0))
        {
            var lastRow = r.ByCategory.Count + 1;
            var catChart = s5.Drawings.AddChart("CategoryChart", eChartType.Doughnut);
            catChart.Title.Text = "Raised by category";
            var cs = catChart.Series.Add(s5.Cells[2, 4, lastRow, 4], s5.Cells[2, 1, lastRow, 1]);
            cs.Header = "Total raised";
            catChart.SetPosition(1, 0, 5, 10);
            catChart.SetSize(480, 360);
        }

        // ---- Sheet 6: By Payment Method ----------------------------------
        var s6 = pkg.Workbook.Worksheets.Add("By Payment Method");
        WriteSheetHeader(s6, "Payment Method", "Donations", "Total Raised");

        for (int i = 0; i < r.ByPaymentMethod.Count; i++)
        {
            var row = i + 2;
            var p = r.ByPaymentMethod[i];
            s6.Cells[row, 1].Value = p.PaymentMethod;
            s6.Cells[row, 2].Value = p.DonationCount;
            s6.Cells[row, 3].Value = p.TotalRaised;
            s6.Cells[row, 3].Style.Numberformat.Format = "$#,##0.00";
        }
        s6.Cells.AutoFitColumns();

        // ---- Sheet 7: Top Donors -----------------------------------------
        var s7 = pkg.Workbook.Worksheets.Add("Top Donors");
        WriteSheetHeader(s7, "Donor", "Donations", "Total Given");

        for (int i = 0; i < r.TopDonors.Count; i++)
        {
            var row = i + 2;
            var d = r.TopDonors[i];
            s7.Cells[row, 1].Value = d.DonorLabel;
            s7.Cells[row, 2].Value = d.DonationCount;
            s7.Cells[row, 3].Value = d.TotalGiven;
            s7.Cells[row, 3].Style.Numberformat.Format = "$#,##0.00";
        }
        s7.Cells.AutoFitColumns();

        // ---- Sheet 8: Campaign Progress ----------------------------------
        var s8 = pkg.Workbook.Worksheets.Add("Campaign Progress");
        WriteSheetHeader(s8,
            "Campaign ID", "Title", "Category", "Status",
            "Funding Goal", "Current Amount", "% Funded",
            "Donations In Period", "Raised In Period",
            "Avg Rating", "Reviews", "End Date", "Owner");

        for (int i = 0; i < r.CampaignProgress.Count; i++)
        {
            var row = i + 2;
            var p = r.CampaignProgress[i];
            s8.Cells[row, 1].Value = p.CampaignId;
            s8.Cells[row, 2].Value = p.Title;
            s8.Cells[row, 3].Value = p.Category;
            s8.Cells[row, 4].Value = p.Status;
            s8.Cells[row, 5].Value = p.FundingGoal;
            s8.Cells[row, 5].Style.Numberformat.Format = "$#,##0.00";
            s8.Cells[row, 6].Value = p.CurrentAmount;
            s8.Cells[row, 6].Style.Numberformat.Format = "$#,##0.00";
            s8.Cells[row, 7].Value = p.PercentFunded / 100m;
            s8.Cells[row, 7].Style.Numberformat.Format = "0.00%";
            s8.Cells[row, 8].Value = p.DonationsInPeriod;
            s8.Cells[row, 9].Value = p.RaisedInPeriod;
            s8.Cells[row, 9].Style.Numberformat.Format = "$#,##0.00";
            s8.Cells[row, 10].Value = p.AverageRating;
            s8.Cells[row, 10].Style.Numberformat.Format = "0.00";
            s8.Cells[row, 11].Value = p.ReviewCount;
            if (p.EndDate.HasValue)
            {
                s8.Cells[row, 12].Value = p.EndDate.Value;
                s8.Cells[row, 12].Style.Numberformat.Format = "yyyy-mm-dd";
            }
            s8.Cells[row, 13].Value = p.OwnerEmail;
        }
        s8.Cells.AutoFitColumns();

        // ---- Sheet 9: Donations (raw rows) -------------------------------
        var s9 = pkg.Workbook.Worksheets.Add("Donations");
        WriteSheetHeader(s9,
            "Date (UTC)", "Receipt #", "Campaign", "Donor",
            "Amount", "Payment Method", "Status", "Message");

        for (int i = 0; i < r.Donations.Count; i++)
        {
            var row = i + 2;
            var d = r.Donations[i];
            s9.Cells[row, 1].Value = d.CreatedAt;
            s9.Cells[row, 1].Style.Numberformat.Format = "yyyy-mm-dd hh:mm:ss";
            s9.Cells[row, 2].Value = d.ReceiptNumber;
            s9.Cells[row, 3].Value = d.CampaignTitle;
            s9.Cells[row, 4].Value = d.DonorLabel;
            s9.Cells[row, 5].Value = d.Amount;
            s9.Cells[row, 5].Style.Numberformat.Format = "$#,##0.00";
            s9.Cells[row, 6].Value = d.PaymentMethod;
            s9.Cells[row, 7].Value = d.Status;
            s9.Cells[row, 8].Value = d.Message;
        }
        s9.Cells.AutoFitColumns();

        return pkg.GetAsByteArray();
    }

    private static void WriteSheetHeader(ExcelWorksheet sheet, params string[] headers)
    {
        for (int i = 0; i < headers.Length; i++)
            sheet.Cells[1, i + 1].Value = headers[i];

        StyleHeader(sheet.Cells[1, 1, 1, headers.Length]);
        sheet.View.FreezePanes(2, 1);
    }

    private static void StyleHeader(ExcelRange range)
    {
        range.Style.Font.Bold = true;
        range.Style.Fill.PatternType = ExcelFillStyle.Solid;
        range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(230, 237, 233));
    }

    // ======================================================================
    //  EPPlus 8 license — clean direct API call (no reflection, no obsolete)
    // ======================================================================
    /// <summary>
    /// EPPlus 8+ replaced the old LicenseContext enum with the License object.
    /// Free-to-use scenarios: SetNonCommercialPersonal(name) for individuals,
    /// or SetNonCommercialOrganization(name) for non-profits / academic use.
    /// We use the personal flavour here — fine for an academic project.
    /// Called once per app lifetime, guarded by a flag.
    /// </summary>
    private static void EnsureEpplusLicense()
    {
        if (_epplusLicenseInitialised) return;

        lock (_epplusLock)
        {
            if (_epplusLicenseInitialised) return;
            ExcelPackage.License.SetNonCommercialPersonal("FundRaisingAssignment");
            _epplusLicenseInitialised = true;
        }
    }
}