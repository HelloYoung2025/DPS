using System.Net.Sockets;
using Dps.SoulRegistry.Contracts;

namespace Dps.SoulRegistry;

/// <summary>Quarantined owner service. No production listener or activation is provided.</summary>
public sealed class SoulCurrentProviderService
{
    private readonly SoulCurrentInstallation _installation;
    private SoulCurrentProviderService(SoulCurrentInstallation installation)
    {
        _installation = installation; installation.VerifyLoadedArtifacts();
    }
    public static SoulCurrentProviderService Open() => throw SoulCurrentInstallation.Unavailable();
    internal static SoulCurrentProviderService ForSyntheticProcess(SoulInstallationLocation location)
    {
        if (System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name != "Dps.SoulCurrent.SignerFixture" || location.SyntheticBoundary is null)
            throw SoulCurrentInstallation.Unavailable();
        return new(SoulCurrentInstallation.Load(location));
    }
    public async Task HandleConnectionAsync(Socket socket, CancellationToken cancellationToken)
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        stop.CancelAfter(5000);
        var work = HandleAsync(socket, stop.Token);
        try { await work.WaitAsync(stop.Token); }
        finally
        {
            stop.Cancel(); socket.Dispose();
            _ = work.ContinueWith(t => { _ = t.Exception; }, CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
            // Independent cleanup allowance; it cannot produce a successful business response.
            try { await work.WaitAsync(TimeSpan.FromMilliseconds(5000)); }
            catch (Exception e) when (e is not OutOfMemoryException) { }
        }
    }
    private async Task HandleAsync(Socket socket, CancellationToken token)
    {
        var c = _installation.Configuration;
        if (SoulCurrentNative.geteuid() != c.ProviderUid || SoulCurrentNative.getegid() != c.ProviderGid) throw SoulCurrentInstallation.Unavailable();
        SoulCurrentNative.RequirePeer(checked((int)socket.Handle), (uint)c.HostUid, (uint)c.HostGid);
        using var stream = new NetworkStream(socket, false);
        var begin = await SoulCurrentLeaseSessionV1.ReadAsync(stream, token);
        if (begin.Phase != "acquire/begin") throw SoulCurrentInstallation.Unavailable();
        using var deadline = new SoulCurrentDeadline(token, begin.ExpiresAt);
        RequireInstallation(begin);
        var challenge = begin.Reply("acquire/challenge") with { SessionId = Guid.NewGuid().ToString("D") };
        await SoulCurrentLeaseSessionV1.WriteAsync(stream, challenge, deadline.Token);
        var complete = await SoulCurrentLeaseSessionV1.ReadAsync(stream, deadline.Token);
        challenge.RequireBinding(complete);
        if (complete.Phase != "acquire/complete" || complete.Sequence != 3 || complete.RequestId != begin.RequestId || complete.Nonce != begin.Nonce)
            throw SoulCurrentInstallation.Unavailable();
        RequireInstallation(complete);
        var proof = SoulCurrentSigningProtocolV1.FromUtf8Json(Convert.FromBase64String(complete.SigningBase64));
        // Codec binds proof to this randomly issued session; only Host can obtain its signature.
        SoulCurrentInstallation.Verify(c.HostAuthSpkiBase64, proof.CanonicalBytes(), proof.SignatureBase64);
        await using var lease = await ConnectedAsync(socket, new SoulCurrentAuthority(_installation).AcquireAsync(complete, proof, deadline), token);
        var acquired = complete.Reply("acquire/acquired") with
        {
            ResolutionBase64 = Convert.ToBase64String(lease.Snapshot.ToUtf8Json()),
            StatusBase64 = Convert.ToBase64String(lease.InitialStatus.ToUtf8Json())
        };
        await SoulCurrentLeaseSessionV1.WriteAsync(stream, acquired, deadline.Token);
        long sequence = 5;
        var requests = new HashSet<Guid> { begin.RequestId };
        var nonces = new HashSet<string>(StringComparer.Ordinal) { begin.Nonce };
        while (true)
        {
            var request = await SoulCurrentLeaseSessionV1.ReadAsync(stream, deadline.Token);
            challenge.RequireBinding(request);
            if (request.Sequence != sequence || !requests.Add(request.RequestId) || !nonces.Add(request.Nonce)) throw SoulCurrentInstallation.Unavailable();
            if (request.Phase == "dispose/request")
            {
                await ConnectedAsync(socket, ReleaseAsync(), token);
                deadline.RequireLive();
                await SoulCurrentLeaseSessionV1.WriteAsync(stream, request.Reply("dispose/ack"), deadline.Token);
                return;
            }
            if (request.Phase != "revalidate/request") throw SoulCurrentInstallation.Unavailable();
            RequireInstallation(request);
            var status = await ConnectedAsync(socket, lease.RevalidateAsync(request.RequestId, request.Nonce), token);
            var response = request.Reply("revalidate/response") with { StatusBase64 = Convert.ToBase64String(status.ToUtf8Json()) };
            await SoulCurrentLeaseSessionV1.WriteAsync(stream, response, deadline.Token);
            sequence = checked(sequence + 2);
        }
        async Task<bool> ReleaseAsync() { await lease.ReleaseAsync(deadline.Token); return true; }
    }
    private void RequireInstallation(SoulCurrentLeaseSessionV1 value)
    {
        _installation.Revalidate(); _installation.VerifyLoadedArtifacts(); _installation.RequireQuery(value.Query);
        if (value.ConfigurationSha256 != _installation.ConfigurationSha256 || value.ReleaseBomSha256 != _installation.Composition.ReleaseBomSha256 ||
            value.ReleaseGeneration != _installation.Composition.ReleaseGeneration) throw SoulCurrentInstallation.Unavailable();
    }
    private static async Task<T> ConnectedAsync<T>(Socket socket, Task<T> operation, CancellationToken token)
    {
        using var peekStop = CancellationTokenSource.CreateLinkedTokenSource(token);
        var peek = socket.ReceiveAsync(new byte[1], SocketFlags.Peek, peekStop.Token).AsTask();
        try
        {
            if (await Task.WhenAny(operation, peek) != operation)
            {
                // EOF or pipelined bytes: neither may authorize a result. The caller closes the session.
                _ = operation.ContinueWith(async t =>
                {
                    if (t.IsCompletedSuccessfully && t.Result is IAsyncDisposable disposable) await disposable.DisposeAsync();
                    else _ = t.Exception;
                }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default).Unwrap()
                .ContinueWith(t => { _ = t.Exception; }, TaskScheduler.Default);
                throw SoulCurrentInstallation.Unavailable();
            }
            return await operation;
        }
        finally
        {
            peekStop.Cancel();
            try { await peek; } catch (Exception e) when (e is not OutOfMemoryException) { }
        }
    }
}
