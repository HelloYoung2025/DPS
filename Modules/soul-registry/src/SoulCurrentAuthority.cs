using System.Diagnostics;
using Dps.SoulRegistry.Contracts;
using Npgsql;

namespace Dps.SoulRegistry;

internal sealed class SoulCurrentDeadline : IDisposable
{
    private readonly long _started = Stopwatch.GetTimestamp();
    private readonly TimeSpan _duration;
    private readonly CancellationTokenSource _cancellation;
    internal DateTimeOffset ExpiresAt { get; }
    internal CancellationToken Token { get { RequireLive(); return _cancellation.Token; } }
    internal SoulCurrentDeadline(CancellationToken token, DateTimeOffset? expiresAt = null)
    {
        var now = DateTimeOffset.UtcNow;
        ExpiresAt = expiresAt ?? now.AddSeconds(5);
        _duration = ExpiresAt - now;
        if (_duration <= TimeSpan.Zero || _duration > TimeSpan.FromSeconds(5)) throw SoulCurrentInstallation.Unavailable();
        _cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        _cancellation.CancelAfter(_duration);
    }
    internal void RequireLive()
    {
        _cancellation.Token.ThrowIfCancellationRequested();
        if (Stopwatch.GetElapsedTime(_started) >= _duration || DateTimeOffset.UtcNow >= ExpiresAt)
            throw new OperationCanceledException("Soul current deadline expired.", _cancellation.Token);
    }
    public void Dispose() => _cancellation.Dispose();
}

internal sealed class SoulCurrentAuthority(SoulCurrentInstallation installation)
{
    internal async Task<Lease> AcquireAsync(SoulCurrentLeaseSessionV1 request, SoulCurrentSigningProtocolV1 proof,
        SoulCurrentDeadline deadline)
    {
        SoulCurrentOwnerLease? owner = null;
        try
        {
            var query = request.Query; var instance = request.InstanceId; var session = Guid.Parse(request.SessionId);
            installation.Revalidate(); installation.VerifyLoadedArtifacts(); installation.RequireQuery(query);
            SoulCurrentInstallation.Verify(installation.Configuration.HostAuthSpkiBase64, proof.CanonicalBytes(), proof.SignatureBase64);
            var connectionString = installation.ReadDatabaseConnection(false);
            await RequireReadOnlyRoleAsync(connectionString, installation.Configuration.DatabaseSchema,
                installation.Configuration.DatabaseRuntimeRole, deadline.Token);
            owner = await SoulCurrentOwnerLease.AcquireAsync(connectionString, installation.Configuration.DatabaseSchema,
                query.TenantRef, query.SoulId, deadline.Token);
            var alias = await owner.ReadAliasAsync(deadline.Token);
            var signing = new SoulCurrentSigningClient(installation);
            var status = await signing.StatusAsync(query, instance, session, proof, deadline, request.RequestId, request.Nonce);
            var preSign = await signing.StatusAsync(query, instance, session, proof, deadline);
            RequireSameAuthority(status, preSign);
            var resolution = await signing.SignCurrentAsync(query, instance, session, proof, preSign, deadline);
            if (resolution.ResolutionRevision != owner.Snapshot.Revision || resolution.RevocationEpoch != owner.Snapshot.RevocationEpoch ||
                resolution.Status != owner.Snapshot.Status || resolution.Resolution.AliasKind != alias.Kind ||
                resolution.Resolution.AliasDigest != alias.Digest || resolution.Resolution.AliasKeyId != alias.KeyId)
                throw SoulCurrentInstallation.Unavailable();
            await owner.RevalidateAsync(deadline.Token); deadline.RequireLive();
            return new Lease(installation, signing, owner, query, instance, session, proof, status, resolution, deadline);
        }
        catch
        {
            if (owner is not null) await owner.DisposeAsync();
            throw;
        }
    }

    internal static void RequireSameAuthority(SoulCurrentStatusV1 a, SoulCurrentStatusV1 b)
    {
        if (a.Status != "ACTIVE" || b.Status != "ACTIVE" || a.AuthorizationRevision != b.AuthorizationRevision ||
            a.TrustEpoch != b.TrustEpoch || a.KeyRevocationEpoch != b.KeyRevocationEpoch ||
            a.ConfigurationSha256 != b.ConfigurationSha256 || a.ReleaseBomSha256 != b.ReleaseBomSha256 ||
            a.ReleaseGeneration != b.ReleaseGeneration || a.HostAuthKeyId != b.HostAuthKeyId ||
            a.CurrentKeyId != b.CurrentKeyId || a.StatusKeyId != b.StatusKeyId)
            throw SoulCurrentInstallation.Unavailable();
    }

    internal static async Task RequireReadOnlyRoleAsync(string connectionString, string schema,
        string expectedRole, CancellationToken token)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(token);
        await using var command = new NpgsqlCommand("""
            SELECT current_user = @role AND NOT r.rolsuper AND NOT r.rolinherit AND NOT r.rolcreaterole
              AND NOT r.rolcreatedb AND NOT r.rolreplication AND NOT r.rolbypassrls AND r.rolcanlogin
              AND NOT EXISTS (SELECT 1 FROM pg_catalog.pg_auth_members m WHERE m.member = r.oid)
              AND NOT has_database_privilege(current_user, current_database(), 'CREATE,TEMP')
              AND NOT has_schema_privilege(current_user, @schema, 'CREATE')
              AND NOT EXISTS (SELECT 1 FROM pg_catalog.pg_class c JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace
                  WHERE n.nspname = @schema AND c.relkind IN ('r','p')
                  AND (c.relowner = r.oid OR has_table_privilege(current_user, c.oid, 'INSERT,UPDATE,DELETE,TRUNCATE,REFERENCES,TRIGGER,MAINTAIN')
                      OR has_any_column_privilege(current_user, c.oid, 'INSERT,UPDATE,REFERENCES')))
              AND has_table_privilege(current_user, quote_ident(@schema) || '.souls', 'SELECT')
              AND has_table_privilege(current_user, quote_ident(@schema) || '.soul_current_state', 'SELECT')
              AND has_table_privilege(current_user, quote_ident(@schema) || '.identity_authority_aliases', 'SELECT')
            FROM pg_catalog.pg_roles r WHERE r.rolname = current_user
            """, connection);
        command.Parameters.AddWithValue("role", expectedRole); command.Parameters.AddWithValue("schema", schema);
        if (await command.ExecuteScalarAsync(token) is not true) throw SoulCurrentInstallation.Unavailable();
    }

    internal sealed class Lease(SoulCurrentInstallation installation, SoulCurrentSigningClient signing,
        SoulCurrentOwnerLease owner, SoulCurrentQueryV1 query, Guid instance, Guid session,
        SoulCurrentSigningProtocolV1 proof, SoulCurrentStatusV1 initialStatus,
        SoulCurrentResolutionV1 snapshot, SoulCurrentDeadline deadline) : IAsyncDisposable
    {
        private readonly SemaphoreSlim _operation = new(1, 1);
        private int _terminal;
        public SoulCurrentResolutionV1 Snapshot => Volatile.Read(ref _terminal) == 0 ? snapshot : throw SoulCurrentInstallation.Unavailable();
        internal SoulCurrentStatusV1 InitialStatus => initialStatus;
        internal async Task<SoulCurrentStatusV1> RevalidateAsync(Guid request, string nonce)
        {
            await _operation.WaitAsync(deadline.Token);
            try
            {
                if (Volatile.Read(ref _terminal) != 0) throw SoulCurrentInstallation.Unavailable();
                installation.Revalidate(); installation.VerifyLoadedArtifacts();
                await owner.RevalidateAsync(deadline.Token);
                var current = await signing.StatusAsync(query, instance, session, proof, deadline, request, nonce);
                RequireSameAuthority(initialStatus, current);
                await owner.RevalidateAsync(deadline.Token);
                snapshot.RequireValidWindow(DateTimeOffset.UtcNow); deadline.RequireLive();
                if (Volatile.Read(ref _terminal) != 0) throw SoulCurrentInstallation.Unavailable();
                return current;
            }
            catch { Interlocked.Exchange(ref _terminal, 1); throw; }
            finally { _operation.Release(); }
        }
        internal async Task ReleaseAsync(CancellationToken token)
        {
            if (Interlocked.Exchange(ref _terminal, 1) != 0) throw SoulCurrentInstallation.Unavailable();
            await _operation.WaitAsync(token);
            try { await owner.ReleaseConfirmedAsync(token); }
            finally { _operation.Release(); }
        }
        public ValueTask DisposeAsync()
        {
            Interlocked.Exchange(ref _terminal, 1);
            return owner.DisposeAsync();
        }
    }
}
