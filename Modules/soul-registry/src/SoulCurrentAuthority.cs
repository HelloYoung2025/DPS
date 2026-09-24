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

internal sealed class SoulCurrentAuthority(SoulCurrentInstallation installation) : ISoulCurrentClientV1
{
    private readonly Guid _instance = Guid.NewGuid();

    public async Task<SoulCurrentResolutionV1> ResolveCurrentAsync(SoulCurrentQueryV1 query, CancellationToken cancellationToken)
    {
        await using var lease = await AcquireCurrentAsync(query, cancellationToken);
        await lease.RevalidateAsync(cancellationToken);
        return lease.Snapshot;
    }

    public async Task<ISoulCurrentLeaseV1> AcquireCurrentAsync(SoulCurrentQueryV1 query, CancellationToken cancellationToken)
    {
        var deadline = new SoulCurrentDeadline(cancellationToken);
        SoulCurrentOwnerLease? owner = null;
        try
        {
            installation.Revalidate(); installation.VerifyLoadedArtifacts(); installation.RequireQuery(query);
            var connectionString = installation.ReadDatabaseConnection(false);
            await RequireReadOnlyRoleAsync(connectionString, installation.Configuration.DatabaseSchema,
                installation.Configuration.DatabaseRuntimeRole, deadline.Token);
            owner = await SoulCurrentOwnerLease.AcquireAsync(connectionString, installation.Configuration.DatabaseSchema,
                query.TenantRef, query.SoulId, deadline.Token);
            var alias = await owner.ReadAliasAsync(deadline.Token);
            var session = Guid.NewGuid(); var signing = new SoulCurrentSigningClient(installation);
            var proof = await signing.PossessionAsync(query, _instance, session, deadline);
            var status = await signing.StatusAsync(query, _instance, session, proof, deadline);
            var preSign = await signing.StatusAsync(query, _instance, session, proof, deadline);
            RequireSameAuthority(status, preSign);
            var resolution = await signing.SignCurrentAsync(query, _instance, session, proof, preSign, deadline);
            if (resolution.ResolutionRevision != owner.Snapshot.Revision || resolution.RevocationEpoch != owner.Snapshot.RevocationEpoch ||
                resolution.Status != owner.Snapshot.Status || resolution.Resolution.AliasKind != alias.Kind ||
                resolution.Resolution.AliasDigest != alias.Digest || resolution.Resolution.AliasKeyId != alias.KeyId)
                throw SoulCurrentInstallation.Unavailable();
            await owner.RevalidateAsync(deadline.Token);
            deadline.RequireLive();
            return new Lease(installation, signing, owner, query, _instance, session, proof, status, resolution, deadline);
        }
        catch
        {
            if (owner is not null) await owner.DisposeAsync();
            deadline.Dispose(); throw;
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
                  AND (c.relowner = r.oid OR has_table_privilege(current_user, c.oid, 'INSERT,UPDATE,DELETE,TRUNCATE,REFERENCES,TRIGGER')))
              AND has_table_privilege(current_user, quote_ident(@schema) || '.souls', 'SELECT')
              AND has_table_privilege(current_user, quote_ident(@schema) || '.soul_current_state', 'SELECT')
              AND has_table_privilege(current_user, quote_ident(@schema) || '.identity_authority_aliases', 'SELECT')
            FROM pg_catalog.pg_roles r WHERE r.rolname = current_user
            """, connection);
        command.Parameters.AddWithValue("role", expectedRole); command.Parameters.AddWithValue("schema", schema);
        if (await command.ExecuteScalarAsync(token) is not true) throw SoulCurrentInstallation.Unavailable();
    }

    private sealed class Lease(SoulCurrentInstallation installation, SoulCurrentSigningClient signing,
        SoulCurrentOwnerLease owner, SoulCurrentQueryV1 query, Guid instance, Guid session,
        SoulCurrentSigningProtocolV1 proof, SoulCurrentStatusV1 initialStatus,
        SoulCurrentResolutionV1 snapshot, SoulCurrentDeadline deadline) : ISoulCurrentLeaseV1
    {
        private readonly SemaphoreSlim _operation = new(1, 1);
        private bool _disposed;
        public SoulCurrentResolutionV1 Snapshot => !_disposed ? snapshot : throw SoulCurrentInstallation.Unavailable();
        public async Task RevalidateAsync(CancellationToken cancellationToken)
        {
            await _operation.WaitAsync(cancellationToken);
            try
            {
                if (_disposed) throw SoulCurrentInstallation.Unavailable();
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token, cancellationToken);
                installation.Revalidate(); installation.VerifyLoadedArtifacts();
                await owner.RevalidateAsync(linked.Token);
                var current = await signing.StatusAsync(query, instance, session, proof, deadline);
                RequireSameAuthority(initialStatus, current);
                await owner.RevalidateAsync(linked.Token);
                snapshot.RequireValidWindow(DateTimeOffset.UtcNow); deadline.RequireLive();
                linked.Token.ThrowIfCancellationRequested();
            }
            catch
            {
                _disposed = true;
                await owner.DisposeAsync(); deadline.Dispose(); throw;
            }
            finally { _operation.Release(); }
        }
        public async ValueTask DisposeAsync()
        {
            await _operation.WaitAsync(CancellationToken.None);
            try
            {
                if (_disposed) return;
                _disposed = true; await owner.DisposeAsync(); deadline.Dispose();
            }
            finally { _operation.Release(); }
        }
    }
}
