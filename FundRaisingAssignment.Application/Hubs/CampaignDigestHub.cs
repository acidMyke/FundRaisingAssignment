using Microsoft.AspNetCore.SignalR;

namespace FundRaisingAssignment.Application.Hubs;

public class CampaignDigestHub : Hub
{
    public const string DigestGroup = "DigestSync";
    public static string GetBatchGroupName(Guid batchId) => $"Batch_{batchId}";

    public async Task JoinDigestGroup() => await Groups.AddToGroupAsync(Context.ConnectionId, DigestGroup);
    public async Task LeaveDigestGroup() => await Groups.RemoveFromGroupAsync(Context.ConnectionId, DigestGroup);
    public async Task JoinBatchGroup(Guid batchId) => await Groups.AddToGroupAsync(Context.ConnectionId, GetBatchGroupName(batchId));
    public async Task LeaveBatchGroup(Guid batchId) => await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetBatchGroupName(batchId));
}
