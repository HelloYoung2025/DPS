using System.Data;
using System.Net.Sockets;
using System.Security.Cryptography;
using Dps.SoulRegistry.Contracts;
using Npgsql;

namespace Dps.SoulRegistry;

/// <summary>Typed independent signer handlers. Provisioning/listener registration is separate.</summary>
public sealed class SoulCurrentSigningService : IDisposable
{
    private readonly SoulCurrentInstallation _installation;
    private readonly bool _hostAuth;
    private readonly ECDsa _primaryKey;
    private readonly ECDsa? _statusKey;
    private readonly Dictionary<string, DateTimeOffset> _used = new(StringComparer.Ordinal);
    private readonly object _stateLock = new();
    private SoulCurrentAuthorityStateV1? _lastState;

    private SoulCurrentSigningService(SoulCurrentInstallation installation, bool hostAuth,
        ECDsa primaryKey, ECDsa? statusKey)
    {
        _installation = installation; _hostAuth = hostAuth; _primaryKey = primaryKey; _statusKey = statusKey;
        installation.VerifyLoadedArtifacts();
        var c = installation.Configuration;
        if (Convert.ToBase64String(primaryKey.ExportSubjectPublicKeyInfo()) !=
            (hostAuth ? c.HostAuthSpkiBase64 : c.CurrentSpkiBase64) ||
            (hostAuth ? statusKey is not null : statusKey is null || Convert.ToBase64String(statusKey.ExportSubjectPublicKeyInfo()) != c.StatusSpkiBase64))
            throw SoulCurrentInstallation.Unavailable();
    }

    public static SoulCurrentSigningService OpenHostAuthenticationService() => throw SoulCurrentInstallation.Unavailable();
    public static SoulCurrentSigningService OpenCurrentService() => throw SoulCurrentInstallation.Unavailable();
    internal static SoulCurrentSigningService ForSyntheticProcess(SoulInstallationLocation location,
        bool hostAuth, ECDsa primary, ECDsa? status)
    {
        if (System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name != "Dps.SoulCurrent.SignerFixture" ||
            location.SyntheticBoundary is null) throw SoulCurrentInstallation.Unavailable();
        return new(SoulCurrentInstallation.Load(location), hostAuth, primary, status);
    }

    // The deployment host owns its listener. An accepted transport never bypasses peer/scope/key checks.
    public async Task HandleConnectionAsync(Socket socket, CancellationToken cancellationToken)
    {
        var c = _installation.Configuration;
        if (SoulCurrentNative.geteuid() != (_hostAuth ? c.HostAuthUid : c.CurrentServiceUid) ||
            SoulCurrentNative.getegid() != (_hostAuth ? c.HostAuthGid : c.CurrentServiceGid)) throw SoulCurrentInstallation.Unavailable();
        SoulCurrentNative.RequirePeer(checked((int)socket.Handle), checked((uint)(_hostAuth ? c.HostUid : c.ProviderUid)), checked((uint)(_hostAuth ? c.HostGid : c.ProviderGid)));
        using var stream = new NetworkStream(socket, false);
        using var initialTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        initialTimeout.CancelAfter(TimeSpan.FromSeconds(5));
        SoulCurrentSigningProtocolV1? request = null;
        SoulCurrentLeaseSessionV1? envelope = null;
        try
        {
            if (_hostAuth)
            {
                envelope = await SoulCurrentLeaseSessionV1.ReadAsync(stream, initialTimeout.Token);
                if (envelope.Phase != "acquire/possession-request" || envelope.Sequence != 1) throw SoulCurrentInstallation.Unavailable();
                request = SoulCurrentSigningProtocolV1.FromUtf8Json(Convert.FromBase64String(envelope.SigningBase64));
            }
            else request = await SoulCurrentSigningClient.ReadAsync(stream, initialTimeout.Token);
            using var deadline = new SoulCurrentDeadline(initialTimeout.Token, request.ExpiresAt);
            RequireRequest(request);
            if (_hostAuth)
            {
                if (request.Operation != "host-possession" || request.MessageType != "challenge-request")
                    throw SoulCurrentInstallation.Unavailable();
                var challenge = request with { MessageType = "challenge", ChallengeNonce = SoulCurrentSigningClient.Nonce() };
                await SoulCurrentLeaseSessionV1.WriteAsync(stream, envelope!.Reply("acquire/possession-challenge") with
                { SigningBase64 = Convert.ToBase64String(challenge.ToUtf8Json()) }, deadline.Token);
                var answerEnvelope = await SoulCurrentLeaseSessionV1.ReadAsync(stream, deadline.Token);
                envelope.RequireBinding(answerEnvelope);
                if (answerEnvelope.Phase != "acquire/possession-answer" || answerEnvelope.Sequence != 3 ||
                    answerEnvelope.RequestId != envelope.RequestId || answerEnvelope.Nonce != envelope.Nonce) throw SoulCurrentInstallation.Unavailable();
                var answer = SoulCurrentSigningProtocolV1.FromUtf8Json(Convert.FromBase64String(answerEnvelope.SigningBase64));
                SoulCurrentSigningClient.RequireAssociation(challenge, answer);
                if (answer.Operation != "host-possession" || answer.MessageType != "answer" ||
                    answer.ChallengeNonce != challenge.ChallengeNonce) throw SoulCurrentInstallation.Unavailable();
                RequireRequest(answer); MarkUsed("host:" + answer.ChallengeNonce, answer.ExpiresAt);
                var proof = answer with { MessageType = "proof", SignatureBase64 = Convert.ToBase64String(new byte[64]) };
                proof = proof with { SignatureBase64 = SignHostProof(proof) };
                deadline.RequireLive(); await SoulCurrentLeaseSessionV1.WriteAsync(stream, answerEnvelope.Reply("acquire/possession-proof") with
                { SigningBase64 = Convert.ToBase64String(proof.ToUtf8Json()) }, deadline.Token);
            }
            else
            {
                if (request.MessageType != "request" || request.Operation is not ("read-current-status" or "sign-current"))
                    throw SoulCurrentInstallation.Unavailable();
                RequirePossession(request);
                MarkUsed("request:" + request.RequestId.ToString("N"), request.ExpiresAt);
                MarkUsed("nonce:" + request.ChallengeNonce, request.ExpiresAt);
                var state = ReadCurrentState();
                SoulCurrentSigningProtocolV1 response;
                if (request.Operation == "read-current-status")
                {
                    var status = Status(request, state, deadline);
                    response = request with { MessageType = "response", PossessionBase64 = "", StatusBase64 = Convert.ToBase64String(status.ToUtf8Json()) };
                }
                else
                {
                    if (state.Status != "ACTIVE") throw SoulCurrentInstallation.Unavailable();
                    MarkUsed("sign:" + request.InstanceId.ToString("N") + request.SessionId.ToString("N"), request.ExpiresAt);
                    RequirePreviousStatus(request, state);
                    var resolution = await ReadAndSignCurrentAsync(request, state, deadline);
                    response = request with { MessageType = "response", PossessionBase64 = "", StatusBase64 = "",
                        ResolutionBase64 = Convert.ToBase64String(resolution.ToUtf8Json()) };
                }
                deadline.RequireLive(); await SoulCurrentSigningClient.WriteAsync(stream, response, deadline.Token);
            }
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            // No server exception/connection string/secret crosses this boundary; no late error write after cancellation.
            if (request is not null && !initialTimeout.IsCancellationRequested)
            {
                var response = request with { Operation = "error", MessageType = "response", PossessionBase64 = "",
                    StatusBase64 = "", ResolutionBase64 = "", SignatureBase64 = "", ErrorCode = "AUTHORITY_UNAVAILABLE" };
                try
                {
                    if (_hostAuth && envelope is not null)
                        await SoulCurrentLeaseSessionV1.WriteAsync(stream, envelope.Reply("error/response") with { ErrorCode = "AUTHORITY_UNAVAILABLE" }, initialTimeout.Token);
                    else await SoulCurrentSigningClient.WriteAsync(stream, response, initialTimeout.Token);
                }
                catch (Exception writeError) when (writeError is not OutOfMemoryException) { }
            }
        }
    }

    private void RequireRequest(SoulCurrentSigningProtocolV1 request)
    {
        _installation.Revalidate(); _installation.VerifyLoadedArtifacts(); _installation.RequireQuery(request.ReadQuery());
        if (request.ConfigurationSha256 != _installation.ConfigurationSha256 ||
            request.ReleaseBomSha256 != _installation.Composition.ReleaseBomSha256 ||
            request.ReleaseGeneration != _installation.Composition.ReleaseGeneration ||
            request.ExpiresAt <= DateTimeOffset.UtcNow || request.ExpiresAt > DateTimeOffset.UtcNow.AddSeconds(5))
            throw SoulCurrentInstallation.Unavailable();
    }
    private void RequirePossession(SoulCurrentSigningProtocolV1 request)
    {
        var proof = SoulCurrentSigningProtocolV1.FromUtf8Json(Convert.FromBase64String(request.PossessionBase64));
        if (proof.Operation != "host-possession" || proof.MessageType != "proof" || proof.InstanceId != request.InstanceId ||
            proof.SessionId != request.SessionId || proof.QueryBase64 != request.QueryBase64 ||
            proof.ConfigurationSha256 != request.ConfigurationSha256 || proof.ReleaseBomSha256 != request.ReleaseBomSha256 ||
            proof.ReleaseGeneration != request.ReleaseGeneration || proof.ExpiresAt != request.ExpiresAt)
            throw SoulCurrentInstallation.Unavailable();
        SoulCurrentInstallation.Verify(_installation.Configuration.HostAuthSpkiBase64, proof.CanonicalBytes(), proof.SignatureBase64);
    }
    private void MarkUsed(string token, DateTimeOffset expires)
    {
        lock (_stateLock)
        {
            foreach (var key in _used.Where(p => p.Value <= DateTimeOffset.UtcNow).Select(p => p.Key).ToArray()) _used.Remove(key);
            if (_used.Count >= 4096 || !_used.TryAdd(token, expires)) throw SoulCurrentInstallation.Unavailable();
        }
    }
    private SoulCurrentAuthorityStateV1 ReadCurrentState()
    {
        var state = _installation.ReadAuthorityState();
        lock (_stateLock)
        {
            if (_lastState is not null && (state.AuthorizationRevision < _lastState.AuthorizationRevision ||
                state.TrustEpoch < _lastState.TrustEpoch || state.KeyRevocationEpoch < _lastState.KeyRevocationEpoch ||
                (state.Status != _lastState.Status && state.AuthorizationRevision <= _lastState.AuthorizationRevision)))
                throw SoulCurrentInstallation.Unavailable();
            _lastState = state;
        }
        return state;
    }
    private SoulCurrentStatusV1 Status(SoulCurrentSigningProtocolV1 request, SoulCurrentAuthorityStateV1 state,
        SoulCurrentDeadline deadline)
    {
        var now = DateTimeOffset.UtcNow;
        var value = new SoulCurrentStatusV1
        {
            RequestId = request.RequestId, Nonce = request.ChallengeNonce, QuerySha256 = request.ReadQuery().ComputeSha256(),
            InstanceId = request.InstanceId, SessionId = request.SessionId, ConfigurationSha256 = state.ConfigurationSha256,
            TenantRef = state.TenantRef, SoulId = state.SoulId, PrincipalId = state.PrincipalId, Permission = state.Permission,
            Status = state.Status, AuthorizationRevision = state.AuthorizationRevision, TrustEpoch = state.TrustEpoch,
            KeyRevocationEpoch = state.KeyRevocationEpoch, ReleaseBomSha256 = state.ReleaseBomSha256,
            ReleaseGeneration = state.ReleaseGeneration, HostAuthKeyId = state.HostAuthKeyId,
            CurrentKeyId = state.CurrentKeyId, StatusKeyId = state.StatusKeyId, IssuedAt = now,
            ExpiresAt = now.AddSeconds(2) < deadline.ExpiresAt ? now.AddSeconds(2) : deadline.ExpiresAt,
            SignatureAlgorithm = "ES256-P1363-SHA256", SignatureBase64 = Convert.ToBase64String(new byte[64])
        };
        lock (_statusKey!) return value with { SignatureBase64 = Convert.ToBase64String(_statusKey!.SignData(value.CanonicalBytes(),
            HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation)) };
    }
    private void RequirePreviousStatus(SoulCurrentSigningProtocolV1 request, SoulCurrentAuthorityStateV1 state)
    {
        var previous = SoulCurrentStatusV1.FromUtf8Json(Convert.FromBase64String(request.StatusBase64));
        var signing = new SoulCurrentSigningClient(_installation);
        signing.RequireStatus(previous, request.ReadQuery(), request.InstanceId, request.SessionId, previous.RequestId, previous.Nonce);
        if (previous.AuthorizationRevision != state.AuthorizationRevision || previous.TrustEpoch != state.TrustEpoch ||
            previous.KeyRevocationEpoch != state.KeyRevocationEpoch || previous.Status != state.Status)
            throw SoulCurrentInstallation.Unavailable();
    }
    private string SignHostProof(SoulCurrentSigningProtocolV1 value)
    {
        if (!_hostAuth || value.Operation != "host-possession" || value.MessageType != "proof") throw SoulCurrentInstallation.Unavailable();
        lock (_primaryKey) return Convert.ToBase64String(_primaryKey.SignData(value.CanonicalBytes(), HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
    }
    private async Task<SoulCurrentResolutionV1> ReadAndSignCurrentAsync(SoulCurrentSigningProtocolV1 request,
        SoulCurrentAuthorityStateV1 status, SoulCurrentDeadline deadline)
    {
        var query = request.ReadQuery(); var c = _installation.Configuration;
        var connectionString = _installation.ReadDatabaseConnection(true);
        await SoulCurrentAuthority.RequireReadOnlyRoleAsync(connectionString, c.DatabaseSchema, c.SignerDatabaseRuntimeRole, deadline.Token);
        await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync(deadline.Token);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, deadline.Token);
        await using (var readOnly = new NpgsqlCommand("SET TRANSACTION READ ONLY", connection, transaction))
            await readOnly.ExecuteNonQueryAsync(deadline.Token);
        var owner = await SoulCurrentOwnerLease.ReadStateAsync(connection, transaction, c.DatabaseSchema, query.TenantRef, query.SoulId, deadline.Token);
        if (owner is null || owner.Status != "ACTIVE") throw SoulCurrentInstallation.Unavailable();
        var alias = await SoulCurrentOwnerLease.ReadAliasAsync(connection, transaction, c.DatabaseSchema, query.TenantRef, query.SoulId, deadline.Token);
        var nested = new SoulResolved(query.SoulId, query.DeviceBindingId, query.PlatformAccountId, query.TraceId,
            query.IdempotencyKey, query.OccurredAt, alias.Kind, alias.Digest, alias.KeyId);
        var now = DateTimeOffset.UtcNow;
        var value = new SoulCurrentResolutionV1
        {
            ProofId = Guid.NewGuid(), TenantRef = query.TenantRef, SoulId = query.SoulId, DeviceBindingId = query.DeviceBindingId,
            PlatformAccountId = query.PlatformAccountId, TraceId = query.TraceId, IdempotencyKey = query.IdempotencyKey,
            OccurredAt = query.OccurredAt, QuerySha256 = query.ComputeSha256(), Status = owner.Status, ResolutionRevision = owner.Revision,
            ResolutionSha256 = SoulResolvedCanonicalV1.ComputeLedgerAuditSha256(nested), Resolution = nested,
            Audience = "control-plane-host", KeyId = c.CurrentKeyId, TrustEpoch = status.TrustEpoch, RevocationEpoch = owner.RevocationEpoch,
            ReleaseBomSha256 = status.ReleaseBomSha256, ReleaseGeneration = status.ReleaseGeneration,
            IssuedAt = now, ExpiresAt = deadline.ExpiresAt, Nonce = query.Nonce, SignatureBase64 = Convert.ToBase64String(new byte[64])
        };
        var finalState = ReadCurrentState();
        if (finalState != status) throw SoulCurrentInstallation.Unavailable();
        value.RequireMatches(query); deadline.RequireLive();
        lock (_primaryKey) value = value with { SignatureBase64 = Convert.ToBase64String(_primaryKey.SignData(value.CanonicalBytes(),
            HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation)) };
        await transaction.CommitAsync(deadline.Token); deadline.RequireLive();
        return value;
    }
    public void Dispose() { _primaryKey.Dispose(); _statusKey?.Dispose(); }
}
