using System;
using System.Collections.Generic;

namespace FundRaisingAssignment.Application.Models.ViewModels;

public class DigestBatchDetailsViewModel
{
    public Guid BatchId { get; set; }
    public DigestBatchStatus BatchStatus { get; set; }
    public DateTime TriggeredAt { get; set; }
    public List<DigestUserGroupViewModel> UserGroups { get; set; } = new();
}

public class DigestUserGroupViewModel
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public Guid? EmailId { get; set; }
    public DigestEmailStatus EmailStatus { get; set; }
    public string? EmailReason { get; set; }
    public List<DigestEntryViewModel> Entries { get; set; } = new();
}

public class DigestEntryViewModel
{
    public Guid EntryId { get; set; }
    public int Sequence { get; set; }
    public Guid? CampaignId { get; set; }
    public string? CampaignTitle { get; set; }
    public double AffinityScore { get; set; } // Placeholder for future telemetry
}
