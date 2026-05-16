using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace FundRaisingAssignment.Application.Hubs;

public record DigestSyncData(
    Guid BatchId,
    string DisplayStatus,
    string StatusBadgeClass
);

public interface IDigestSyncPublisher
{
    void PublishBatchSync(DigestSyncData data);
    void PublishDetailsSync(Guid batchId);
}

public class DigestSyncPublisher(IHubContext<CampaignDigestHub> hubContext) : IDigestSyncPublisher, IDisposable
{
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _debouncers = new();
    private readonly TimeSpan _throttlePeriod = TimeSpan.FromMilliseconds(500);

    public void PublishBatchSync(DigestSyncData data)
    {
        Debounce(CampaignDigestHub.DigestGroup, action: () => hubContext.Clients.Group(CampaignDigestHub.DigestGroup).SendAsync("digest-batch-sync", data));
    }

    public void PublishDetailsSync(Guid batchId)
    {
        var groupName = CampaignDigestHub.GetBatchGroupName(batchId);
        Debounce(groupName, () => hubContext.Clients.Group(groupName).SendAsync("digest-details-sync"));
    }

    private void Debounce(string key, Func<Task> action)
    {
        var cts = new CancellationTokenSource();

        var existing = _debouncers.AddOrUpdate(
            key,
            _ => cts,
            (_, oldCts) =>
            {
                oldCts.Cancel();
                oldCts.Dispose();
                return cts;
            });

        _ = RunDebounceAsync(key, cts, action);
    }

    private async Task RunDebounceAsync(string key, CancellationTokenSource cts, Func<Task> action)
    {
        try
        {
            await Task.Delay(_throttlePeriod, cts.Token);

            if (!cts.IsCancellationRequested)
            {
                _debouncers.TryRemove(key, out _);
                await action();
            }
        }
        catch (TaskCanceledException) { }
        finally { cts.Dispose(); }
    }

    public void Dispose()
    {
        foreach (var cts in _debouncers.Values)
        {
            cts.Cancel();
            cts.Dispose();
        }

        _debouncers.Clear();
        GC.SuppressFinalize(this);
    }
}