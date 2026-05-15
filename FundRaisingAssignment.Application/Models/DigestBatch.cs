using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace FundRaisingAssignment.Application.Models;

public enum DigestBatchStatus
{
    Pending,
    Processing,
    Failed,
    Processed
}

public enum DigestEmailStatus
{
    Initial,
    Bypass,
    Sent,
    Open,
    Click,
    Bounce,
    Spam,
    Blocked,
    Unknown,
}

[Table("DigestBatches")]
public class DigestBatch
{
    public required Guid Id { get; set; }
    public DigestBatchStatus Status { get; set; } = DigestBatchStatus.Pending;
    public int UserCount { get; set; }
    public int CampaignCount { get; set; }
    public DateTime TriggeredAt { get; set; } = DateTime.Now;
    public DateTime? StatusUpdatedAt { get; set; }
    public List<DigestEntry> Entries { get; set; } = [];
}

[Index(nameof(DigestBatchId))]
[Index(nameof(UserId))]
[Index(nameof(CampaignId))]
[Table("DigestEntries")]
public class DigestEntry
{
    public Guid Id { get; set; }
    public Guid DigestBatchId { get; set; }
    public Guid UserId { get; set; }
    public Guid? CampaignId { get; set; }
    public Guid? EmailId { get; set; }
    public DigestEmailStatus EmailStatus { get; set; } = DigestEmailStatus.Initial;
    public string? EmailReason { get; set; }
    public DateTime? SentAt { get; set; }

    [ForeignKey(nameof(DigestBatchId))]
    public DigestBatch Batch { get; set; } = null!;

    [ForeignKey(nameof(UserId))]
    public ApplicationUser User { get; set; } = null!;
}