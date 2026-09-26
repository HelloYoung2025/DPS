using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dps.SoulRegistry.Contracts;
using Npgsql;

namespace Dps.SoulRegistry.Tests;

// Synthetic installation/UID boundary; the children run the actual product handlers and Host façade.
[System.Runtime.Versioning.SupportedOSPlatform("macos")]
public sealed class SoulCurrentProcessFixture : IAsyncDisposable
{
    private readonly string _admin;
    private readonly string _database;
    private readonly string _ownerConnection;
    private readonly ECDsa _rootKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly ECDsa _aliasSigner = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly ECDsa _aliasVerifier;
    private readonly AliasHmacKey _aliasKey;
    private readonly AliasEvidence _evidence = new();
    private readonly PostgresSoulRegistry _registry;
    private readonly SoulIdentityAuthority _authority;
    private readonly List<Process> _services = [];
    private readonly string _providerRole;
    private readonly string _signerRole;
    private readonly string _generation;
    public string Root { get; }
    public string Schema { get; } = "soul_current";
    public SoulCurrentQueryV1 Query { get; private set; } = null!;
    public SoulCurrentConfigurationV2 Configuration { get; private set; } = null!;
    public SoulCurrentAuthorityStateV1 State { get; private set; } = null!;
    public SoulCurrentCompositionV1 Composition { get; private set; } = null!;
    public int[] SignerProcessIds => _services.Take(2).Select(p => p.Id).ToArray();
    public int[] ServiceProcessIds => _services.Take(3).Select(p => p.Id).ToArray();

    private SoulCurrentProcessFixture(string admin)
    {
        _admin = admin; var id = Guid.NewGuid().ToString("N");
        _database = "p05_current_" + id; _providerRole = "p05_reader_" + id; _signerRole = "p05_signer_" + id;
        _ownerConnection = new NpgsqlConnectionStringBuilder(admin) { Database = _database, Pooling = false }.ConnectionString;
        Root = "/private/tmp/dps-p05-" + id[..12];
        Directory.CreateDirectory(Root); File.SetUnixFileMode(Root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        foreach (var relative in new[] { "installation", "installation/releases", "installation/releases/1", "installation/secrets", "run", "run/host-auth", "run/current", "run/provider" })
        {
            var path = Path.Combine(Root, relative); Directory.CreateDirectory(path);
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        _generation = Path.Combine(Root, "installation/releases/1");
        _aliasKey = new AliasHmacKey("synthetic-alias", RandomNumberGenerator.GetBytes(32));
        _registry = new PostgresSoulRegistry(new SoulRegistryOptions(_ownerConnection, Schema, "synthetic-alias", [_aliasKey]));
        _aliasVerifier = ECDsa.Create(_aliasSigner.ExportParameters(false));
        _authority = new SoulIdentityAuthority(_registry, new SoulAuthorityBindings(_evidence, _aliasVerifier,
            bytes => _aliasSigner.SignData(bytes, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation),
            "synthetic-alias-authority", 1, 1, Hash("synthetic-alias-bom"), 1, TimeProvider.System));
    }

    public static async Task<SoulCurrentProcessFixture> CreateAsync(CancellationToken token)
    {
        var admin = Environment.GetEnvironmentVariable("DPS_TEST_POSTGRES") ?? throw new InvalidOperationException("DPS_TEST_POSTGRES required.");
        var fixture = new SoulCurrentProcessFixture(admin);
        try { await fixture.InitializeAsync(token); return fixture; }
        catch { await fixture.DisposeAsync(); throw; }
    }

    private async Task InitializeAsync(CancellationToken token)
    {
        await using (var connection = new NpgsqlConnection(_admin))
        {
            await connection.OpenAsync(token);
            await using var version = new NpgsqlCommand("SHOW server_version_num", connection);
            if (await version.ExecuteScalarAsync(token) is not "180004") throw new InvalidOperationException("PG18.4 required.");
            await using var create = new NpgsqlCommand($"CREATE DATABASE {_database}", connection);
            await create.ExecuteNonQueryAsync(token);
        }
        await _registry.InitializeAsync(token);
        var request = AliasRequest("create", null); _evidence.Issue(request, "owned-synthetic@example.invalid");
        var proof = await _authority.VerifyAliasAsync(request, token);
        var soul = await _authority.ApplyVerifiedAliasAsync(proof, token);
        Query = new SoulCurrentQueryV1
        {
            ProducerModule = "control-plane-host", RequestId = Guid.NewGuid(), TenantRef = request.TenantRef, SoulId = soul.SoulId,
            DeviceBindingId = request.DeviceBindingId, PlatformAccountId = request.PlatformAccountId, TraceId = request.TraceId,
            IdempotencyKey = "idem_" + Hash(Guid.NewGuid().ToString()), OccurredAt = DateTimeOffset.UtcNow,
            Purpose = "identity-read", ExpectedResolutionRevision = 1, Nonce = "nonce_" + Hash(Guid.NewGuid().ToString())
        };
        var providerPassword = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        var signerPassword = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        await using (var connection = new NpgsqlConnection(_ownerConnection))
        {
            await connection.OpenAsync(token);
            // Names/passwords are locally generated lowercase identifiers/hex, never caller values.
            await using var roles = new NpgsqlCommand($"""
                REVOKE ALL ON DATABASE {_database} FROM PUBLIC;
                REVOKE ALL ON SCHEMA public FROM PUBLIC;
                CREATE ROLE {_providerRole} LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS NOINHERIT PASSWORD '{providerPassword}';
                CREATE ROLE {_signerRole} LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS NOINHERIT PASSWORD '{signerPassword}';
                GRANT CONNECT ON DATABASE {_database} TO {_providerRole}, {_signerRole};
                GRANT USAGE ON SCHEMA {Schema} TO {_providerRole}, {_signerRole};
                GRANT SELECT ON {Schema}.souls, {Schema}.soul_current_state, {Schema}.identity_authority_aliases TO {_providerRole}, {_signerRole};
                """, connection);
            await roles.ExecuteNonQueryAsync(token);
        }
        Write("secrets/provider", Encoding.UTF8.GetBytes(providerPassword));
        Write("secrets/signer", Encoding.UTF8.GetBytes(signerPassword));
        var output = Path.Combine(RepositoryRoot(), "Modules/soul-registry/tests/SignerProcess/bin/Release/net10.0");
        if (!File.Exists(Path.Combine(output, "Dps.SoulCurrent.SignerFixture.dll"))) throw new InvalidOperationException("Build the signer fixture first.");
        var hostOutput = Path.Combine(RepositoryRoot(), "Modules/control-plane-host/tests/SoulCurrentHostProcess/bin/Release/net10.0");
        if (!File.Exists(Path.Combine(hostOutput, "Dps.SoulCurrent.HostFixture.dll"))) throw new InvalidOperationException("Build the Host fixture first.");
        // Freeze the merged generation before ANY child starts. Common artifacts must be byte-identical.
        SoulCurrentGenerationFixture.Merge(output, hostOutput, _generation);
        // Darwin process-start observations can be coarser than vnode timestamps. Cross the frozen second before starting.
        var frozenSecond = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        while (DateTimeOffset.UtcNow.ToUnixTimeSeconds() <= frozenSecond) await Task.Delay(20, token);
        var hostAuth = Start("host-auth"); var current = Start("current"); _services.Add(hostAuth); _services.Add(current);
        var hostKeys = await PublicKeysAsync(hostAuth, token); var currentKeys = await PublicKeysAsync(current, token);
        if (hostAuth.Id == current.Id || hostAuth.Id == Environment.ProcessId || current.Id == Environment.ProcessId)
            throw new InvalidOperationException("Independent signer processes required.");
        var adminBuilder = new NpgsqlConnectionStringBuilder(_admin);
        Configuration = new SoulCurrentConfigurationV2
        {
            ConfigurationId = Guid.NewGuid(), TenantRef = Query.TenantRef, SoulId = Query.SoulId, PrincipalId = "synthetic-host",
            ConsumerModule = "control-plane-host", Permission = "soul-current.identity-read", HostUid = SoulCurrentNative.geteuid(), HostGid = SoulCurrentNative.getegid(),
            ProviderUid = SoulCurrentNative.geteuid(), ProviderGid = SoulCurrentNative.getegid(),
            HostAuthUid = SoulCurrentNative.geteuid(), HostAuthGid = SoulCurrentNative.getegid(), HostAuthKeyId = "synthetic-host-auth",
            HostAuthSpkiBase64 = hostKeys.Primary, CurrentServiceUid = SoulCurrentNative.geteuid(), CurrentServiceGid = SoulCurrentNative.getegid(),
            CurrentKeyId = "synthetic-current", CurrentSpkiBase64 = currentKeys.Primary, StatusKeyId = "synthetic-status", StatusSpkiBase64 = currentKeys.Status,
            DatabaseHost = "127.0.0.1", DatabasePort = adminBuilder.Port, DatabaseName = _database, DatabaseSchema = Schema,
            DatabaseRuntimeRole = _providerRole, DatabaseSecretId = "provider", SignerDatabaseRuntimeRole = _signerRole, SignerDatabaseSecretId = "signer", DeadlineMs = 5000
        };
        State = new SoulCurrentAuthorityStateV1
        {
            ConfigurationSha256 = Hash(Configuration.ToUtf8Json()), TenantRef = Query.TenantRef, SoulId = Query.SoulId,
            PrincipalId = Configuration.PrincipalId, Permission = Configuration.Permission, Status = "ACTIVE", AuthorizationRevision = 1,
            TrustEpoch = 1, KeyRevocationEpoch = 0, ReleaseBomSha256 = Hash("synthetic-current-bom"), ReleaseGeneration = 1,
            HostAuthKeyId = Configuration.HostAuthKeyId, CurrentKeyId = Configuration.CurrentKeyId, StatusKeyId = Configuration.StatusKeyId
        };
        WriteInstallation();
        var provider = Start("provider"); _services.Add(provider);
        _ = await PublicKeysAsync(provider, token);
        foreach (var process in _services)
        {
            await process.StandardInput.WriteLineAsync("START"); await process.StandardInput.FlushAsync(token);
            var ready = await process.StandardOutput.ReadLineAsync(token);
            if (ready != "READY") throw new InvalidOperationException("Synthetic service did not become ready: " + ready);
        }
    }

    public void WriteInstallation(SoulCurrentConfigurationV2? configuration = null)
    {
        if (configuration is not null) Configuration = configuration;
        var config = Configuration.ToUtf8Json();
        State = State with { ConfigurationSha256 = Hash(config) };
        Composition = new SoulCurrentCompositionV1
        {
            ConfigurationSha256 = Hash(config), HostArtifactSha256 = Hash(File.ReadAllBytes(Path.Combine(_generation, "Dps.ControlPlaneHost.dll"))),
            SoulArtifactSha256 = Hash(File.ReadAllBytes(Path.Combine(_generation, "Dps.SoulRegistry.dll"))),
            ContractsArtifactSha256 = Hash(File.ReadAllBytes(Path.Combine(_generation, "Dps.SoulRegistry.Contracts.dll"))),
            ReleaseBomSha256 = State.ReleaseBomSha256, ReleaseGeneration = State.ReleaseGeneration,
            Issuer = "dps-soul-current-installation", KeyRole = "installation-root", KeyId = "synthetic-root", IssuedAt = DateTimeOffset.UtcNow.AddSeconds(-1),
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10), SignatureAlgorithm = "ES256-P1363-SHA256", SignatureBase64 = Convert.ToBase64String(new byte[64])
        };
        Composition = Composition with { SignatureBase64 = Convert.ToBase64String(_rootKey.SignData(Composition.CanonicalBytes(),
            HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation)) };
        var bootstrap = new SoulCurrentBootstrapV1
        {
            InstallationId = Guid.NewGuid(), Enabled = true, RootKeyId = "synthetic-root", RootSpkiBase64 = Convert.ToBase64String(_rootKey.ExportSubjectPublicKeyInfo()),
            ActiveConfigurationSha256 = Hash(config), ActiveReleaseBomSha256 = State.ReleaseBomSha256, ActiveReleaseGeneration = State.ReleaseGeneration
        };
        Write("configuration.v2.json", config); Write("composition.v1.json", Composition.ToUtf8Json());
        Write("bootstrap.v1.json", bootstrap.ToUtf8Json()); Write("authority-state.v1.json", State.ToUtf8Json());
    }

    public void SetAuthorityState(SoulCurrentAuthorityStateV1 state) { State = state; Write("authority-state.v1.json", state.ToUtf8Json()); }
    public void Write(string relative, byte[] bytes)
    {
        var path = Path.Combine(Root, "installation", relative); File.WriteAllBytes(path, bytes);
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
    public async Task<(int Exit, string Output)> ReadHostAsync(SoulCurrentQueryV1? query, CancellationToken token, string mode = "host-read")
    {
        using var process = Start(mode);
        await process.StandardInput.WriteLineAsync(Convert.ToBase64String((query ?? Query).ToUtf8Json()));
        await process.StandardInput.FlushAsync(token);
        var output = await process.StandardOutput.ReadToEndAsync(token); var error = await process.StandardError.ReadToEndAsync(token);
        await process.WaitForExitAsync(token);
        if (error.Length != 0) throw new InvalidOperationException("Synthetic child emitted stderr.");
        return (process.ExitCode, output.Trim());
    }
    public async Task<Process> DelayCurrentResponseAsync(CancellationToken token)
    {
        var directory = Path.Combine(Root, "run/current");
        File.Move(Path.Combine(directory, "service.sock"), Path.Combine(directory, "backend.sock"));
        var proxy = Start("late-current"); _services.Add(proxy);
        if (await proxy.StandardOutput.ReadLineAsync(token) != "READY") throw new InvalidOperationException("Owned delay proxy failed.");
        return proxy;
    }
    public async Task<Process> ProxyProviderAsync(string fault, CancellationToken token)
    {
        var directory = Path.Combine(Root, "run/provider");
        File.Move(Path.Combine(directory, "service.sock"), Path.Combine(directory, "backend.sock"));
        var proxy = Start("provider-proxy", fault); _services.Add(proxy);
        if (await proxy.StandardOutput.ReadLineAsync(token) != "READY") throw new InvalidOperationException("Owned provider proxy failed.");
        return proxy;
    }
    public Process Start(string mode, string argument = "synthetic-same-uid")
    {
        var executable = Path.Combine(Environment.GetEnvironmentVariable("DOTNET_ROOT") ?? throw new InvalidOperationException("Pinned DOTNET_ROOT required."), "dotnet");
        var start = new ProcessStartInfo(executable) { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, WorkingDirectory = _generation };
        start.ArgumentList.Add(Path.Combine(_generation, mode.StartsWith("host-", StringComparison.Ordinal) && mode != "host-auth"
            ? "Dps.SoulCurrent.HostFixture.dll" : "Dps.SoulCurrent.SignerFixture.dll"));
        start.ArgumentList.Add(mode); start.ArgumentList.Add(Root); start.ArgumentList.Add(argument);
        // The child gets no bootstrap-admin database credential.
        start.Environment.Remove("DPS_TEST_POSTGRES");
        return Process.Start(start) ?? throw new InvalidOperationException("Child start failed.");
    }
    public async Task<Process> AcquireLeaseProcessAsync(CancellationToken token)
    {
        var process = Start("host-lease");
        await process.StandardInput.WriteLineAsync(Convert.ToBase64String(Query.ToUtf8Json()));
        await process.StandardInput.FlushAsync(token);
        var response = await process.StandardOutput.ReadLineAsync(token);
        if (response != "LEASED")
        {
            await process.WaitForExitAsync(token); process.Dispose();
            throw new InvalidOperationException("Actual lease acquisition failed: " + response);
        }
        return process;
    }
    public async Task<long> AdvisoryLockCountAsync(bool granted, CancellationToken token)
    {
        await using var connection = new NpgsqlConnection(_ownerConnection); await connection.OpenAsync(token);
        await using var command = new NpgsqlCommand("SELECT count(*) FROM pg_catalog.pg_locks l JOIN pg_catalog.pg_stat_activity a ON a.pid=l.pid WHERE a.datname=current_database() AND l.locktype='advisory' AND l.granted=@granted", connection);
        command.Parameters.AddWithValue("granted", granted);
        return (long)(await command.ExecuteScalarAsync(token))!;
    }
    public async Task TerminateProviderAsync(CancellationToken token)
    {
        await using var connection = new NpgsqlConnection(_ownerConnection); await connection.OpenAsync(token);
        await using var command = new NpgsqlCommand("SELECT pg_terminate_backend(pid) FROM pg_catalog.pg_stat_activity WHERE datname=current_database() AND usename=@role", connection);
        command.Parameters.AddWithValue("role", _providerRole);
        if (await command.ExecuteScalarAsync(token) is not true) throw new InvalidOperationException("No owned provider session.");
    }
    public async Task SetRuntimePrivilegeAsync(string grantee, string privilege, bool grant, CancellationToken token)
    {
        var role = grantee switch { "provider" => _providerRole, "signer" => _signerRole, "PUBLIC" => "PUBLIC", _ => throw new ArgumentException("Unknown test role.") };
        var permission = privilege switch
        {
            "INSERT" or "UPDATE" or "REFERENCES" => privilege + " (status)",
            "MAINTAIN" => privilege,
            _ => throw new ArgumentException("Unknown test privilege.")
        };
        await using var connection = new NpgsqlConnection(_ownerConnection); await connection.OpenAsync(token);
        await using var command = new NpgsqlCommand($"{(grant ? "GRANT" : "REVOKE")} {permission} ON {Schema}.soul_current_state {(grant ? "TO" : "FROM")} {role}", connection);
        await command.ExecuteNonQueryAsync(token);
    }

    public async Task<bool> HasRuntimePrivilegeAsync(bool signer, string privilege, CancellationToken token)
    {
        var function = privilege switch
        {
            "INSERT" or "UPDATE" or "REFERENCES" => "has_column_privilege(@role, @table, 'status', @privilege)",
            "MAINTAIN" => "has_table_privilege(@role, @table, @privilege)",
            _ => throw new ArgumentException("Unknown test privilege.")
        };
        await using var connection = new NpgsqlConnection(_ownerConnection); await connection.OpenAsync(token);
        await using var command = new NpgsqlCommand("SELECT " + function, connection);
        command.Parameters.AddWithValue("role", signer ? _signerRole : _providerRole);
        command.Parameters.AddWithValue("table", Schema + ".soul_current_state");
        command.Parameters.AddWithValue("privilege", privilege);
        return (bool)(await command.ExecuteScalarAsync(token))!;
    }

    public async Task<string> DeniedRuntimeWriteAsync(bool signer, CancellationToken token)
    {
        var location = new SoulInstallationLocation(Path.Combine(Root, "installation"), Path.Combine(Root, "run"), SoulCurrentNative.geteuid(), Root);
        var connectionString = SoulCurrentInstallation.Load(location).ReadDatabaseConnection(signer);
        await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync(token);
        await using var command = new NpgsqlCommand($"UPDATE {Schema}.soul_current_state SET status=status", connection);
        try { await command.ExecuteNonQueryAsync(token); return "UNEXPECTED_WRITE"; }
        catch (PostgresException error) { return error.SqlState; }
    }

    private static async Task<(string Primary, string Status)> PublicKeysAsync(Process process, CancellationToken token)
    {
        using var json = JsonDocument.Parse(await process.StandardOutput.ReadLineAsync(token) ?? throw new InvalidOperationException("Missing public material."));
        if (json.RootElement.GetProperty("pid").GetInt32() != process.Id) throw new InvalidOperationException("Wrong signer PID.");
        return (json.RootElement.GetProperty("primary").GetString()!, json.RootElement.GetProperty("status").GetString()!);
    }
    public async Task<string> BusinessSnapshotAsync(CancellationToken token)
    {
        await using var connection = new NpgsqlConnection(_ownerConnection); await connection.OpenAsync(token);
        var rows = new List<string>();
        foreach (var table in new[] { "souls", "identity_aliases", "resolution_receipts", "mutation_receipts", "identity_verifications", "identity_authority_aliases", "soul_current_state", "identity_authority_receipts", "identity_authority_events" })
        {
            await using var command = new NpgsqlCommand($"SELECT coalesce(jsonb_agg(x ORDER BY x::text), '[]'::jsonb)::text FROM (SELECT to_jsonb(t) x FROM {Schema}.{table} t) q", connection);
            rows.Add(table + ":" + (string)(await command.ExecuteScalarAsync(token))!);
        }
        return string.Join("\n", rows);
    }
    public async Task RevokeAliasAsync(CancellationToken token)
    {
        var request = AliasRequest("revoke", Query.SoulId); _evidence.Issue(request, "owned-synthetic@example.invalid");
        var proof = await _authority.VerifyAliasAsync(request, token); _ = await _authority.ApplyVerifiedAliasAsync(proof, token);
    }
    private SoulAliasVerificationRequestV1 AliasRequest(string operation, string? target) => new()
    {
        RequestId = Guid.NewGuid(), TenantRef = "tenant_" + Hash("synthetic-tenant"), Operation = operation, AliasKind = "email",
        ProofRef = "proof_" + Hash(Guid.NewGuid().ToString()), TargetSoulId = target, DeviceBindingId = "db_" + Hash("synthetic-device")[..32],
        PlatformAccountId = "pa_" + Hash("synthetic-account")[..32], TraceId = "trace_" + Hash(Guid.NewGuid().ToString())[..32],
        IdempotencyKey = "idem_" + Hash(Guid.NewGuid().ToString()), OccurredAt = DateTimeOffset.UtcNow, Nonce = "nonce_" + Hash(Guid.NewGuid().ToString())
    };
    public static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "global.json"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root missing.");
    }
    private static string Hash(string value) => Hash(Encoding.UTF8.GetBytes(value));
    private static string Hash(byte[] value) => Convert.ToHexStringLower(SHA256.HashData(value));
    public async ValueTask DisposeAsync()
    {
        foreach (var process in _services)
        {
            if (!process.HasExited)
            {
                await process.StandardInput.WriteLineAsync("STOP"); await process.StandardInput.FlushAsync();
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                try { await process.WaitForExitAsync(timeout.Token); }
                catch (OperationCanceledException) { process.Kill(true); await process.WaitForExitAsync(); }
            }
            process.Dispose();
        }
        _services.Clear(); _registry.Dispose(); _aliasKey.Dispose(); _aliasSigner.Dispose(); _aliasVerifier.Dispose(); _rootKey.Dispose(); _evidence.Dispose();
        await using var connection = new NpgsqlConnection(_admin); await connection.OpenAsync(CancellationToken.None);
        foreach (var sql in new[] { $"DROP DATABASE IF EXISTS {_database} WITH (FORCE)", $"DROP ROLE IF EXISTS {_providerRole}", $"DROP ROLE IF EXISTS {_signerRole}" })
        {
            await using var drop = new NpgsqlCommand(sql, connection);
            await drop.ExecuteNonQueryAsync(CancellationToken.None);
        }
        if (Directory.Exists(Root)) Directory.Delete(Root, true);
    }
    private sealed class AliasEvidence : ISoulAliasEvidenceAuthority, IDisposable
    {
        private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        private readonly Dictionary<string, (string Hash, string Alias, string? Control, byte[] Signature)> _issued = [];
        internal void Issue(SoulAliasVerificationRequestV1 request, string alias)
        {
            var hash = request.ComputeSha256(); var control = request.TargetSoulId is null ? null : Hash("controlled:" + request.TargetSoulId);
            var signature = _key.SignData(Encoding.UTF8.GetBytes(hash + "\n" + alias + "\n" + control), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            _issued.Add(request.ProofRef, (hash, alias, control, signature));
        }
        public Task<VerifiedAliasEvidence> AuthenticateAndVerifyAsync(SoulAliasVerificationRequestV1 request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_issued.TryGetValue(request.ProofRef, out var proof) || proof.Hash != request.ComputeSha256() ||
                !_key.VerifyData(Encoding.UTF8.GetBytes(proof.Hash + "\n" + proof.Alias + "\n" + proof.Control), proof.Signature,
                    HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation)) throw new SoulRegistryException("Synthetic alias evidence rejected.");
            return Task.FromResult(new VerifiedAliasEvidence(proof.Hash, proof.Alias, request.TargetSoulId, proof.Control));
        }
        public void Dispose() => _key.Dispose();
    }
}

internal static class SoulCurrentGenerationFixture
{
    internal static void Merge(string first, string second, string generation)
    {
        foreach (var path in Directory.GetFiles(first).Concat(Directory.GetFiles(second)))
        {
            var destination = Path.Combine(generation, Path.GetFileName(path));
            if (File.Exists(destination))
            {
                if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(File.ReadAllBytes(destination))) throw new InvalidOperationException("Launcher artifact mismatch.");
                continue;
            }
            File.Copy(path, destination);
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(destination, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }
}
