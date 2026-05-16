using System;
using System.Collections.Generic;

namespace FundRaisingAssignment.Application.Models.ViewModels;

public class DigestBatchDetailsViewModel
{
    public Guid BatchId { get; set; }
    public string DisplayStatus { get; set; } = string.Empty;
    public string StatusBadgeClass { get; set; } = string.Empty;
    public string DisplayTriggeredAt { get; set; } = string.Empty;
    public List<DigestUserGroupViewModel> UserGroups { get; set; } = new();
}

public class DigestUserGroupViewModel
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string DisplayEmailId { get; set; } = string.Empty;
    public string DisplayEmailStatus { get; set; } = string.Empty;
    public string EmailStatusBadgeClass { get; set; } = string.Empty;
    public string? EmailReason { get; set; }
    public List<DigestEntryViewModel> Entries { get; set; } = new();
}

public class DigestEntryViewModel
{
    public Guid EntryId { get; set; }
    public bool IsBypass { get; set; }
    public bool HasCampaign { get; set; }
    public string? CampaignTitle { get; set; }
    public string DisplayAffinityScore { get; set; } = string.Empty;
}
