namespace FundRaisingAssignment.Application.Interfaces;

public interface IDigestJobQueue
{
    bool QueueJob(Guid batchId);
    IAsyncEnumerable<Guid> DequeueJobs(CancellationToken cancellationToken);
}