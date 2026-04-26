using System;

namespace FundRaisingAssignment.Application.Models;

public class AuditHistory
{
    public Guid Id { get; set; }
    public string OldData { get; set; }
    public string Action { get; set; }
    public Guid UpdatedBy { get; set; }

}
