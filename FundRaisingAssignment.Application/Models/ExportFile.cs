using System.ComponentModel.DataAnnotations;

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
