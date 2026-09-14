namespace Dps.WindowsEdgeWorker;

/// <summary>
/// Compatibility tombstone for the withdrawn Executor-owned
/// native.stop.proof/v1 producer.  It exposes no request, result, stop,
/// identity, signing, verification, or persistence surface.  Stale callers
/// therefore fail at composition, while an explicit probe fails closed here.
/// </summary>
internal sealed class WorkerNativeStopProofIssuer
{
    internal const string QuarantineReason =
        "native.stop.proof/v1 is quarantine-only and cannot authorize UNKNOWN_SUBMISSION; " +
        "a frozen Policy-owned v2 challenge/proof contract, verified route assignment, " +
        "and Release-BOM authority context are required";

    public Task IssueAsync(CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        return Task.FromException(
            new NotSupportedException(QuarantineReason));
    }
}
