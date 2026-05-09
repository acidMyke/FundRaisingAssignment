using System.IO.Compression;
using System.Text;
using FundRaisingAssignment.Application.Areas.Internal.Pages;
using FundRaisingAssignment.Application.Models;

namespace FundRaisingAssignment.Test;

public class ReportExporterTests
{
    private static PlatformReport SampleReport() => new()
    {
        StartDate = new DateTime(2026, 4, 1),
        EndDate = new DateTime(2026, 4, 30),
        GeneratedAtUtc = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc),
        TotalCampaigns = 5,
        NewCampaignsInPeriod = 2,
        TotalDonations = 10,
        TotalRaised = 1234.56m,
        UniqueDonors = 7,
        AverageDonation = 123.46m,
        LargestDonation = 500m,
        ByStatus =
        {
            new CampaignStatusStat { Status = "Active",    CampaignCount = 3, DonationCount = 8, TotalRaised = 1000m },
            new CampaignStatusStat { Status = "Completed", CampaignCount = 2, DonationCount = 2, TotalRaised = 234.56m },
        },
        DailyTotals =
        {
            new DailyStat { Date = new DateTime(2026, 4, 1),  DonationCount = 4, TotalRaised = 400m  },
            new DailyStat { Date = new DateTime(2026, 4, 15), DonationCount = 6, TotalRaised = 834.56m },
        },
        TopCampaigns =
        {
            new TopCampaignStat
            {
                CampaignId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Title = "Help \"Friends\", with, commas",
                Status = "Active",
                DonationCount = 8,
                TotalRaised = 1000m,
            },
        }
    };

    [Fact]
    public void ExportToCsv_ContainsExpectedSectionsAndFigures()
    {
        var bytes = ReportsModel.ExportToCsv(SampleReport());

        Assert.NotEmpty(bytes);

        var text = Encoding.UTF8.GetString(bytes);
        Assert.Contains("Givvn Platform Report", text);
        Assert.Contains("SUMMARY", text);
        Assert.Contains("BY STATUS", text);
        Assert.Contains("DAILY TOTALS", text);
        Assert.Contains("TOP CAMPAIGNS", text);

        // Headline figure flows through
        Assert.Contains("1234.56", text);

        // Quoting/escaping: title contains commas + quotes
        Assert.Contains("\"Help \"\"Friends\"\", with, commas\"", text);
    }

    [Fact]
    public void ExportToCsv_ListsEveryByStatusAndDailyRow()
    {
        var r = SampleReport();
        var text = Encoding.UTF8.GetString(ReportsModel.ExportToCsv(r));

        foreach (var s in r.ByStatus)
            Assert.Contains(s.Status, text);

        foreach (var d in r.DailyTotals)
            Assert.Contains(d.Date.ToString("yyyy-MM-dd"), text);
    }

    [Fact]
    public void ExportToXlsx_ProducesValidZipPackage_WithFourWorksheets()
    {
        var bytes = ReportsModel.ExportToXlsx(SampleReport());

        Assert.NotEmpty(bytes);

        // XLSX is a ZIP — first 2 bytes "PK"
        Assert.Equal((byte)'P', bytes[0]);
        Assert.Equal((byte)'K', bytes[1]);

        using var ms = new MemoryStream(bytes);
        using var archive = new ZipArchive(ms, ZipArchiveMode.Read);

        // EPPlus writes one xml per worksheet under xl/worksheets/
        var sheetEntries = archive.Entries
            .Where(e => e.FullName.StartsWith("xl/worksheets/sheet", StringComparison.Ordinal)
                     && e.FullName.EndsWith(".xml", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(4, sheetEntries.Count);

        // Workbook part lists sheet names — assert all four expected names appear
        var workbook = archive.GetEntry("xl/workbook.xml");
        Assert.NotNull(workbook);
        using var reader = new StreamReader(workbook!.Open());
        var workbookXml = reader.ReadToEnd();

        Assert.Contains("Summary", workbookXml);
        Assert.Contains("By Status", workbookXml);
        Assert.Contains("Daily Totals", workbookXml);
        Assert.Contains("Top Campaigns", workbookXml);
    }
}
