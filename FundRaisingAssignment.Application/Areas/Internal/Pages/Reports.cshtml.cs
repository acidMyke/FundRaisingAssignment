using System.Globalization;
using System.Text;
using FundRaisingAssignment.Application.Data;
using FundRaisingAssignment.Application.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
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
    private readonly ILogger<ReportsModel> _logger;

    /// <summary>True after the EPPlus license has been initialised once.</summary>
    private static bool _epplusLicenseInitialised;
    private static readonly object _epplusLock = new();

    public ReportsModel(ApplicationDbContext db, ILogger<ReportsModel> logger)
    {
        _db = db;
        _logger = logger;
    }

    [BindProperty]
    public ReportInputModel Input { get; set; } = new();

    public PlatformReport? PreviewReport { get; private set; }

    [TempData]
    public string? StatusMessage { get; set; }

    // ======================================================================
    //  GET — empty form
    // ======================================================================
    public IActionResult OnGet() => Page();

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
    // ======================================================================
    public async Task<IActionResult> OnPostExportAsync()
    {
        if (!ModelState.IsValid) return Page();

        try
        {
            var report = await GenerateReportAsync(Input.StartDate, Input.EndDate);
            var stamp  = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");

            return Input.Format switch
            {
                ExportFormat.Csv => File(
                    fileContents:     ExportToCsv(report),
                    contentType:      "text/csv",
                    fileDownloadName: $"platform-report-{stamp}.csv"),

                ExportFormat.Xlsx => File(
                    fileContents:     ExportToXlsx(report),
                    contentType:      "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    fileDownloadName: $"platform-report-{stamp}.xlsx"),

                _ => BadRequest("Unsupported format."),
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Report export failed");
            StatusMessage = "Export failed — please try again.";
            return Page();
        }
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
        var startUtc = DateTime.SpecifyKind(start.Date,          DateTimeKind.Utc);
        var endUtc   = DateTime.SpecifyKind(end.Date.AddDays(1), DateTimeKind.Utc);

        var campaignsQ = _db.Campaigns.AsNoTracking();
        var donationsQ = _db.Donations.AsNoTracking()
            .Where(d => d.CreatedAt >= startUtc && d.CreatedAt < endUtc);

        // ---- Headline numbers ----
        var totalCampaigns       = await campaignsQ.CountAsync();
        var newCampaignsInPeriod = await campaignsQ
            .CountAsync(c => c.CreatedAt >= startUtc && c.CreatedAt < endUtc);

        var donationCount = await donationsQ.CountAsync();
        var totalRaised   = await donationsQ.SumAsync(d => (decimal?)d.Amount) ?? 0m;
        var largest       = await donationsQ.MaxAsync(d => (decimal?)d.Amount) ?? 0m;
        var uniqueDonors  = await donationsQ.Select(d => d.UserId).Distinct().CountAsync();
        var avg           = donationCount > 0 ? Math.Round(totalRaised / donationCount, 2) : 0m;

        // ---- By campaign status ----
        // Step A: donations per status — use the navigation property d.Campaign
        //         instead of a manual join. EF Core translates this into a
        //         server-side JOIN automatically using the FK relationship.
        var donationsByStatus = await donationsQ
            .Where(d => d.Campaign != null)
            .GroupBy(d => d.Campaign!.Status)
            .Select(g => new
            {
                Status        = g.Key,
                DonationCount = g.Count(),
                TotalRaised   = g.Sum(d => d.Amount),
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
                    Status        = cs.Status.ToString() ?? "Unknown",
                    CampaignCount = cs.Count,
                    DonationCount = dStat?.DonationCount ?? 0,
                    TotalRaised   = dStat?.TotalRaised   ?? 0m,
                };
            })
            .OrderByDescending(s => s.TotalRaised)
            .ToList();

        // ---- Daily totals ----
        var daily = await donationsQ
            .GroupBy(d => d.CreatedAt.Date)
            .Select(g => new DailyStat
            {
                Date          = g.Key,
                DonationCount = g.Count(),
                TotalRaised   = g.Sum(d => d.Amount),
            })
            .OrderBy(d => d.Date)
            .ToListAsync();

        // ---- Top campaigns (also uses navigation property) ----
        var top = await donationsQ
            .Where(d => d.Campaign != null)
            .GroupBy(d => new { d.Campaign!.Id, d.Campaign.Title, d.Campaign.Status })
            .Select(g => new TopCampaignStat
            {
                CampaignId    = g.Key.Id,
                Title         = g.Key.Title,
                Status        = g.Key.Status.ToString() ?? "Unknown",
                DonationCount = g.Count(),
                TotalRaised   = g.Sum(d => d.Amount),
            })
            .OrderByDescending(t => t.TotalRaised)
            .Take(20)
            .ToListAsync();

        var report = new PlatformReport
        {
            StartDate            = start,
            EndDate              = end,
            TotalCampaigns       = totalCampaigns,
            NewCampaignsInPeriod = newCampaignsInPeriod,
            TotalDonations       = donationCount,
            TotalRaised          = totalRaised,
            UniqueDonors         = uniqueDonors,
            AverageDonation      = avg,
            LargestDonation      = largest,
            ByStatus             = byStatus,
            DailyTotals          = daily,
            TopCampaigns         = top,
        };

        _logger.LogInformation(
            "Generated report {Start:d}–{End:d}: {Donations} donations, total {Total:C}",
            start, end, donationCount, totalRaised);

        return report;
    }

    // ======================================================================
    //  CSV EXPORT  (Karthik's Step 10a)
    // ======================================================================
    private static byte[] ExportToCsv(PlatformReport r)
    {
        var sb = new StringBuilder();
        var inv = CultureInfo.InvariantCulture;

        WriteRow(sb, "Givvn Platform Report");
        WriteRow(sb, $"Period: {r.StartDate:yyyy-MM-dd} to {r.EndDate:yyyy-MM-dd}");
        WriteRow(sb, $"Generated: {r.GeneratedAtUtc:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();

        WriteRow(sb, "SUMMARY");
        WriteRow(sb, "Metric", "Value");
        WriteRow(sb, "Total Campaigns",          r.TotalCampaigns.ToString(inv));
        WriteRow(sb, "New Campaigns in Period",  r.NewCampaignsInPeriod.ToString(inv));
        WriteRow(sb, "Total Donations",          r.TotalDonations.ToString(inv));
        WriteRow(sb, "Total Raised",             r.TotalRaised.ToString("F2", inv));
        WriteRow(sb, "Unique Donors",            r.UniqueDonors.ToString(inv));
        WriteRow(sb, "Average Donation",         r.AverageDonation.ToString("F2", inv));
        WriteRow(sb, "Largest Donation",         r.LargestDonation.ToString("F2", inv));
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
    private static byte[] ExportToXlsx(PlatformReport r)
    {
        EnsureEpplusLicense();

        using var pkg = new ExcelPackage();

        // ---- Sheet 1: Summary --------------------------------------------
        var s1 = pkg.Workbook.Worksheets.Add("Summary");

        s1.Cells["A1"].Value           = "Givvn Platform Report";
        s1.Cells["A1"].Style.Font.Size = 16;
        s1.Cells["A1"].Style.Font.Bold = true;
        s1.Cells["A2"].Value           = $"Period: {r.StartDate:yyyy-MM-dd} -> {r.EndDate:yyyy-MM-dd}";
        s1.Cells["A3"].Value           = $"Generated: {r.GeneratedAtUtc:yyyy-MM-dd HH:mm} UTC";

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