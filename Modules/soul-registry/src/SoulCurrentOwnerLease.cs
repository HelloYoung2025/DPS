using System.Data;
using System.Text.RegularExpressions;
using Npgsql;

namespace Dps.SoulRegistry;

// The connection owns the existing Soul advisory lock; no receipt or DTO can reconstruct it.
internal sealed class SoulCurrentOwnerLease : IAsyncDisposable
{
    internal sealed record OwnerState(long Revision, long RevocationEpoch, string Status);
    internal sealed record AliasState(string Kind, string Digest, string KeyId);
    private readonly NpgsqlConnection _connection;
    private readonly string _schema;
    private readonly string _tenant;
    private readonly string _soul;
    private readonly string _lockName;
    private readonly SemaphoreSlim _operation = new(1, 1);
    private bool _disposed;
    internal OwnerState Snapshot { get; }
    internal int BackendProcessId => _connection.ProcessID;

    private SoulCurrentOwnerLease(NpgsqlConnection connection, string schema, string tenant,
        string soul, OwnerState snapshot)
    {
        _connection = connection; _schema = schema; _tenant = tenant; _soul = soul;
        _lockName = $"soul-current:{tenant}:{soul}"; Snapshot = snapshot;
    }

    internal static async Task<SoulCurrentOwnerLease> AcquireAsync(string connectionString,
        string schema, string tenant, string soul, CancellationToken cancellationToken)
    {
        if (!Regex.IsMatch(schema, "^[a-z][a-z0-9_]{0,62}$", RegexOptions.CultureInvariant) ||
            string.IsNullOrWhiteSpace(tenant) || string.IsNullOrWhiteSpace(soul))
            throw new ArgumentException("A scoped Soul identity and schema are required.");
        var connection = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(connectionString)
        { Pooling = false }.ConnectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            await using (var command = new NpgsqlCommand(
                "SELECT pg_advisory_lock(hashtextextended(@lock_name, 0))", connection))
            {
                command.CommandTimeout = 5;
                command.Parameters.AddWithValue("lock_name", $"soul-current:{tenant}:{soul}");
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            var state = await ReadStateAsync(connection, null, schema, tenant, soul, cancellationToken);
            if (state is null || state.Status != "ACTIVE") throw new AliasNotFoundException();
            return new SoulCurrentOwnerLease(connection, schema, tenant, soul, state);
        }
        catch { await connection.DisposeAsync(); throw; }
    }

    internal static async Task<OwnerState?> ReadStateAsync(NpgsqlConnection connection,
        NpgsqlTransaction? transaction, string schema, string tenant, string soul, CancellationToken token)
    {
        await using var command = new NpgsqlCommand($"""
            SELECT s.resolution_revision, s.revocation_epoch, s.status
            FROM {schema}.soul_current_state s JOIN {schema}.souls i
              ON s.tenant_ref = i.tenant_id AND s.soul_id = i.soul_id
            WHERE s.tenant_ref = @tenant AND s.soul_id = @soul AND i.tombstoned_at IS NULL
            """, connection, transaction);
        command.Parameters.AddWithValue("tenant", tenant); command.Parameters.AddWithValue("soul", soul);
        await using var reader = await command.ExecuteReaderAsync(token);
        return await reader.ReadAsync(token)
            ? new OwnerState(reader.GetInt64(0), reader.GetInt64(1), reader.GetString(2)) : null;
    }

    internal static async Task<AliasState> ReadAliasAsync(NpgsqlConnection connection,
        NpgsqlTransaction? transaction, string schema, string tenant, string soul, CancellationToken token)
    {
        await using var command = new NpgsqlCommand($"""
            SELECT alias_kind, alias_digest, alias_key_id FROM {schema}.identity_authority_aliases
            WHERE tenant_ref = @tenant AND soul_id = @soul AND revoked_at IS NULL
            ORDER BY alias_kind COLLATE "C", alias_key_id COLLATE "C", alias_digest COLLATE "C" LIMIT 1
            """, connection, transaction);
        command.Parameters.AddWithValue("tenant", tenant); command.Parameters.AddWithValue("soul", soul);
        await using var reader = await command.ExecuteReaderAsync(token);
        if (!await reader.ReadAsync(token)) throw new AliasNotFoundException();
        return new AliasState(reader.GetString(0), reader.GetString(1), reader.GetString(2));
    }

    internal async Task<AliasState> ReadAliasAsync(CancellationToken token)
    {
        await _operation.WaitAsync(token);
        try
        {
            await RequireLiveAsync(token);
            return await ReadAliasAsync(_connection, null, _schema, _tenant, _soul, token);
        }
        finally { _operation.Release(); }
    }

    internal async Task RevalidateAsync(CancellationToken cancellationToken)
    {
        await _operation.WaitAsync(cancellationToken);
        try { await RequireLiveAsync(cancellationToken); }
        finally { _operation.Release(); }
    }

    private async Task RequireLiveAsync(CancellationToken token)
    {
        if (_disposed || _connection.State != ConnectionState.Open)
            throw new SoulRegistryException("Soul owner lease was lost.");
        await using var command = new NpgsqlCommand("""
            SELECT EXISTS (SELECT 1 FROM pg_catalog.pg_locks
              WHERE locktype = 'advisory' AND pid = pg_backend_pid() AND granted AND objsubid = 1
              AND classid = ((hashtextextended(@lock_name, 0) >> 32) & 4294967295)::oid
              AND objid = (hashtextextended(@lock_name, 0) & 4294967295)::oid)
            """, _connection);
        command.Parameters.AddWithValue("lock_name", _lockName);
        if (await command.ExecuteScalarAsync(token) is not true ||
            await ReadStateAsync(_connection, null, _schema, _tenant, _soul, token) != Snapshot)
            throw new SoulRegistryException("Soul owner lock or state changed during lease.");
    }

    public async ValueTask DisposeAsync()
    {
        await _operation.WaitAsync(CancellationToken.None);
        try
        {
            if (_disposed) return;
            _disposed = true;
            // Closing the nonpooled session releases its lock, including cancellation/error paths.
            await _connection.DisposeAsync();
        }
        finally { _operation.Release(); }
    }
}
