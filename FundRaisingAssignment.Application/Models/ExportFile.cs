using System.ComponentModel.DataAnnotations;

// ─────────────────────────────────────────────────────────────────────────────
// User Story:   UA02 – Export Platform Performance and Financial Report
//                                                          Owner: Unnikrishna Pillai Karthik
// BCE Role:     Entity
// Description:  Persisted row representing a generated export (CSV / XLSX),
//               including its bytes, content type, the admin who created it,
//               and the date range covered. Backs the post-export "download"
//               sub-flow on the Reports page.
// ─────────────────────────────────────────────────────────────────────────────

namespace FundRaisingAssignment.Application.Models;

public class ExportFile
{
    public Guid Id { get; set; }

    public Guid CreatedByAdminId { get; set; }
    public ApplicationUser? CreatedByAdmin { get; set; }

    public ExportFormat Format { get; set; }

    [MaxLength(260)]
    public string FileName { get; set; } = "";

    [MaxLength(128)]
    public string ContentType { get; set; } = "";

    public byte[] Content { get; set; } = Array.Empty<byte>();

    public long Size { get; set; }

    public DateTime RangeStart { get; set; }
    public DateTime RangeEnd { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
