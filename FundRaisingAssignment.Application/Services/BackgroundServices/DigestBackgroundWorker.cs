using System.Threading.Channels;
using FundRaisingAssignment.Application.Interfaces;

namespace FundRaisingAssignment.Application.Services.BackgroundServices;

// Should be a siingleton service, only one shared instance push & read from same queue
public class DigestJobQueue : IDigestJobQueue
{
    private readonly Channel<Guid> _queue = Channel.CreateUnbounded<Guid>();
    public bool QueueJob(Guid batchId) => _queue.Writer.TryWrite(batchId);
    public IAsyncEnumerable<Guid> DequeueJobs(CancellationToken ct) => _queue.Reader.ReadAllAsync(ct);
}

public class DigestBackgroundWorker(DigestJobQueue queue, IServiceScopeFactory scopeFactory) : BackgroundService
{
    private readonly DigestJobQueue _queue = queue;
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var batchId in _queue.DequeueJobs(stoppingToken))
        {
            using var scope = _scopeFactory.CreateScope();
            var digestService = scope.ServiceProvider.GetRequiredService<ICampaignDigestService>();
            await digestService.ProcessAsync(batchId);
        }
    }
}
