using System.Diagnostics;
using System.Net.Sockets;
using System.Security.Cryptography;
using Dps.SoulRegistry.Contracts;

namespace Dps.ControlPlaneHost;

internal sealed class ControlPlaneSoulCurrentDeadline : IDisposable
{
    private readonly long _start = Stopwatch.GetTimestamp();
    private readonly CancellationTokenSource _stop;
    internal DateTimeOffset ExpiresAt { get; } = DateTimeOffset.UtcNow.AddMilliseconds(5000);
    internal CancellationToken Token { get { RequireLive(); return _stop.Token; } }
    internal ControlPlaneSoulCurrentDeadline(CancellationToken token)
    { _stop = CancellationTokenSource.CreateLinkedTokenSource(token); _stop.CancelAfter(5000); }
    internal void RequireLive()
    {
        _stop.Token.ThrowIfCancellationRequested();
        if (Stopwatch.GetElapsedTime(_start).TotalMilliseconds >= 5000 || DateTimeOffset.UtcNow >= ExpiresAt)
            throw new OperationCanceledException("Soul current deadline expired.");
    }
    public void Dispose() => _stop.Dispose();
}

internal sealed class ControlPlaneSoulCurrentIpcClient(ControlPlaneSoulCurrentInstallation installation,
    ControlPlaneSoulCurrentDeadline deadline) : ISoulCurrentClientV1
{
    private readonly ControlPlaneSoulCurrentDeadline _deadline = deadline;
    private SoulCurrentStatusV1? _finalStatus;
    private int _acquired;
    internal static string Nonce() => "nonce_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
    internal void RequireSuccessWindow()
    {
        _deadline.RequireLive();
        var status = _finalStatus ?? throw ControlPlaneSoulCurrentInstallation.Unavailable();
        ControlPlaneSoulCurrentInstallation.RequireWindow(status.IssuedAt, status.ExpiresAt);
    }
    public async Task<SoulCurrentResolutionV1> ResolveCurrentAsync(SoulCurrentQueryV1 query, CancellationToken token)
    {
        await using var lease = await AcquireCurrentAsync(query, token);
        var result = lease.Snapshot; await lease.RevalidateAsync(token); await lease.DisposeAsync();
        RequireSuccessWindow(); token.ThrowIfCancellationRequested(); result.RequireValidWindow(DateTimeOffset.UtcNow); return result;
    }
    public async Task<ISoulCurrentLeaseV1> AcquireCurrentAsync(SoulCurrentQueryV1 query, CancellationToken token)
    {
        if (Interlocked.Exchange(ref _acquired, 1) != 0) throw ControlPlaneSoulCurrentInstallation.Unavailable();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_deadline.Token, token);
        RequireInstallation(query);
        var c = installation.Configuration;
        if (ControlPlaneSoulCurrentNative.geteuid() != c.HostUid || ControlPlaneSoulCurrentNative.getegid() != c.HostGid)
            throw ControlPlaneSoulCurrentInstallation.Unavailable();
        var socket = await ConnectAsync(false, linked.Token);
        try
        {
            var stream = new NetworkStream(socket, false);
            var begin = new SoulCurrentLeaseSessionV1
            {
                ProducerModule = "control-plane-host", Phase = "acquire/begin", Query = query, RequestId = Guid.NewGuid(), Nonce = Nonce(),
                InstanceId = Guid.NewGuid(), SessionId = "", Sequence = 1, ConfigurationSha256 = installation.ConfigurationSha256,
                ReleaseBomSha256 = installation.Composition.ReleaseBomSha256, ReleaseGeneration = installation.Composition.ReleaseGeneration, ExpiresAt = _deadline.ExpiresAt
            };
            await SoulCurrentLeaseSessionV1.WriteAsync(stream, begin, linked.Token);
            var challenge = await SoulCurrentLeaseSessionV1.ReadAsync(stream, linked.Token); begin.RequireResponse(challenge, "acquire/challenge", true);
            var proof = await PossessionAsync(challenge, linked.Token);
            var complete = challenge with { Phase = "acquire/complete", ProducerModule = "control-plane-host", Sequence = 3, SigningBase64 = Convert.ToBase64String(proof.ToUtf8Json()) };
            await SoulCurrentLeaseSessionV1.WriteAsync(stream, complete, linked.Token);
            var response = await SoulCurrentLeaseSessionV1.ReadAsync(stream, linked.Token); complete.RequireResponse(response, "acquire/acquired");
            var status = RequireStatus(response);
            var snapshot = SoulCurrentResolutionV1.FromUtf8Json(Convert.FromBase64String(response.ResolutionBase64));
            snapshot.RequireMatches(query); snapshot.RequireValidWindow(DateTimeOffset.UtcNow);
            if (snapshot.TrustEpoch != status.TrustEpoch || snapshot.KeyId != c.CurrentKeyId || snapshot.Status != "ACTIVE") throw ControlPlaneSoulCurrentInstallation.Unavailable();
            ControlPlaneSoulCurrentInstallation.Verify(c.CurrentSpkiBase64, snapshot.CanonicalBytes(), snapshot.SignatureBase64);
            _deadline.RequireLive(); linked.Token.ThrowIfCancellationRequested();
            return new Lease(this, socket, stream, response, snapshot, status);
        }
        catch { socket.Dispose(); throw; }
    }
    private void RequireInstallation(SoulCurrentQueryV1 query)
    { installation.Revalidate(); installation.VerifyLoadedArtifacts(); installation.RequireQuery(query); _deadline.RequireLive(); }
    private SoulCurrentStatusV1 RequireStatus(SoulCurrentLeaseSessionV1 response)
    {
        var value = SoulCurrentStatusV1.FromUtf8Json(Convert.FromBase64String(response.StatusBase64));
        var c = installation.Configuration;
        // Envelope codec already checked request/nonce/query/session/config/BOM; validate the authority here.
        if (value.Status != "ACTIVE" || value.PrincipalId != c.PrincipalId || value.Permission != c.Permission ||
            value.HostAuthKeyId != c.HostAuthKeyId || value.CurrentKeyId != c.CurrentKeyId || value.StatusKeyId != c.StatusKeyId)
            throw ControlPlaneSoulCurrentInstallation.Unavailable();
        ControlPlaneSoulCurrentInstallation.Verify(c.StatusSpkiBase64, value.CanonicalBytes(), value.SignatureBase64);
        ControlPlaneSoulCurrentInstallation.RequireWindow(value.IssuedAt, value.ExpiresAt); return value;
    }
    private async Task<SoulCurrentSigningProtocolV1> PossessionAsync(SoulCurrentLeaseSessionV1 challenge, CancellationToken token)
    {
        var p = new SoulCurrentSigningProtocolV1
        {
            Operation = "host-possession", MessageType = "challenge-request", RequestId = Guid.NewGuid(), InstanceId = challenge.InstanceId,
            SessionId = Guid.Parse(challenge.SessionId), QueryBase64 = Convert.ToBase64String(challenge.Query.ToUtf8Json()), QuerySha256 = challenge.Query.ComputeSha256(),
            ConfigurationSha256 = challenge.ConfigurationSha256, ReleaseBomSha256 = challenge.ReleaseBomSha256, ReleaseGeneration = challenge.ReleaseGeneration,
            ExpiresAt = challenge.ExpiresAt, ChallengeNonce = "", PossessionBase64 = "", StatusBase64 = "", ResolutionBase64 = "", ErrorCode = "", SignatureBase64 = ""
        };
        using var socket = await ConnectAsync(true, token); using var stream = new NetworkStream(socket, false);
        var request = challenge with { ProducerModule = "control-plane-host", Phase = "acquire/possession-request", Sequence = 1, RequestId = p.RequestId, Nonce = Nonce(), SigningBase64 = Convert.ToBase64String(p.ToUtf8Json()) };
        await SoulCurrentLeaseSessionV1.WriteAsync(stream, request, token);
        var response = await SoulCurrentLeaseSessionV1.ReadAsync(stream, token); request.RequireResponse(response, "acquire/possession-challenge");
        var signedChallenge = SoulCurrentSigningProtocolV1.FromUtf8Json(Convert.FromBase64String(response.SigningBase64));
        if (signedChallenge.RequestId != p.RequestId) throw ControlPlaneSoulCurrentInstallation.Unavailable();
        var answer = response with { ProducerModule = "control-plane-host", Phase = "acquire/possession-answer", Sequence = 3,
            SigningBase64 = Convert.ToBase64String((signedChallenge with { MessageType = "answer" }).ToUtf8Json()) };
        await SoulCurrentLeaseSessionV1.WriteAsync(stream, answer, token);
        var final = await SoulCurrentLeaseSessionV1.ReadAsync(stream, token); answer.RequireResponse(final, "acquire/possession-proof");
        var proof = SoulCurrentSigningProtocolV1.FromUtf8Json(Convert.FromBase64String(final.SigningBase64));
        if (proof.RequestId != p.RequestId || proof.ChallengeNonce != signedChallenge.ChallengeNonce) throw ControlPlaneSoulCurrentInstallation.Unavailable();
        ControlPlaneSoulCurrentInstallation.Verify(installation.Configuration.HostAuthSpkiBase64, proof.CanonicalBytes(), proof.SignatureBase64);
        _deadline.RequireLive(); return proof;
    }
    private async Task<Socket> ConnectAsync(bool auth, CancellationToken token)
    {
        var c = installation.Configuration; var uid = (uint)(auth ? c.HostAuthUid : c.ProviderUid); var gid = (uint)(auth ? c.HostAuthGid : c.ProviderGid);
        var directory = Path.Combine(installation.Location.SocketRoot, auth ? "host-auth" : "provider");
        using var parent = ControlPlaneSoulCurrentNative.OpenDirectory(directory, installation.Location);
        var before = parent.Socket("service.sock", uid, gid); var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            await socket.ConnectAsync(new UnixDomainSocketEndPoint(Path.Combine(directory, "service.sock")), token);
            ControlPlaneSoulCurrentNative.RequirePeer(checked((int)socket.Handle), uid, gid);
            if (before != parent.Socket("service.sock", uid, gid)) throw ControlPlaneSoulCurrentInstallation.Unavailable();
            return socket;
        }
        catch { socket.Dispose(); throw; }
    }
    private sealed class Lease(ControlPlaneSoulCurrentIpcClient client, Socket socket, NetworkStream stream,
        SoulCurrentLeaseSessionV1 acquired, SoulCurrentResolutionV1 snapshot, SoulCurrentStatusV1 initial) : ISoulCurrentLeaseV1
    {
        private readonly SemaphoreSlim _operation = new(1, 1);
        private readonly object _cleanupGate = new();
        private Task? _cleanup;
        private int _terminal;
        private long _sequence = 5;
        public SoulCurrentResolutionV1 Snapshot => Volatile.Read(ref _terminal) == 0 ? snapshot : throw ControlPlaneSoulCurrentInstallation.Unavailable();
        private SoulCurrentLeaseSessionV1 Request(string phase) => acquired with
        {
            ProducerModule = "control-plane-host", Phase = phase, Sequence = _sequence, RequestId = Guid.NewGuid(), Nonce = Nonce(), ResolutionBase64 = "", StatusBase64 = ""
        };
        public async Task RevalidateAsync(CancellationToken cancellationToken)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(client._deadline.Token, cancellationToken);
            var held = false;
            try
            {
                await _operation.WaitAsync(linked.Token); held = true;
                if (Volatile.Read(ref _terminal) != 0) throw ControlPlaneSoulCurrentInstallation.Unavailable();
                client.RequireInstallation(acquired.Query);
                var request = Request("revalidate/request");
                await SoulCurrentLeaseSessionV1.WriteAsync(stream, request, linked.Token);
                var response = await SoulCurrentLeaseSessionV1.ReadAsync(stream, linked.Token); request.RequireResponse(response, "revalidate/response");
                var status = client.RequireStatus(response);
                if (status.AuthorizationRevision != initial.AuthorizationRevision || status.TrustEpoch != initial.TrustEpoch ||
                    status.KeyRevocationEpoch != initial.KeyRevocationEpoch || Volatile.Read(ref _terminal) != 0) throw ControlPlaneSoulCurrentInstallation.Unavailable();
                client._finalStatus = status; _sequence = checked(_sequence + 2); client._deadline.RequireLive(); linked.Token.ThrowIfCancellationRequested();
            }
            catch { Interlocked.Exchange(ref _terminal, 1); socket.Dispose(); throw; }
            finally { if (held) _operation.Release(); }
        }
        public async ValueTask DisposeAsync()
        {
            Task cleanup;
            lock (_cleanupGate) cleanup = _cleanup ??= CloseAsync();
            _ = cleanup.ContinueWith(t => { _ = t.Exception; }, CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
            try { await cleanup.WaitAsync(TimeSpan.FromMilliseconds(5000)); }
            catch { Interlocked.Exchange(ref _terminal, 1); socket.Dispose(); throw; }
        }
        private async Task CloseAsync()
        {
            var alreadyTerminal = Interlocked.Exchange(ref _terminal, 1) != 0;
            using var cleanup = new CancellationTokenSource(5000);
            try
            {
                await _operation.WaitAsync(cleanup.Token);
                try
                {
                    if (alreadyTerminal) throw ControlPlaneSoulCurrentInstallation.Unavailable();
                    var request = Request("dispose/request");
                    await SoulCurrentLeaseSessionV1.WriteAsync(stream, request, client._deadline.Token);
                    var response = await SoulCurrentLeaseSessionV1.ReadAsync(stream, client._deadline.Token); request.RequireResponse(response, "dispose/ack");
                    client._deadline.RequireLive();
                }
                finally { _operation.Release(); }
            }
            finally { socket.Dispose(); stream.Dispose(); }
        }
    }
}
