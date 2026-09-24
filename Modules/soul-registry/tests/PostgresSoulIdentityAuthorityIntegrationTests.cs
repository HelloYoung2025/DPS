using System.Security.Cryptography;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Collections.Concurrent;
using Dps.SoulRegistry.Contracts;
using Npgsql;
using Xunit;

namespace Dps.SoulRegistry.Tests;

public sealed class PostgresSoulIdentityAuthorityIntegrationTests
{
    [Fact, Trait("Category", "Integration")]
    public async Task SignedCreatePersistsOwnerTruthAndExactReplay()
    {
        await using var rig = await Rig.CreateAsync();
        var request = rig.Issue("Alice@example.test", "create");
        var proof = await rig.Authority.VerifyAliasAsync(request, TestContext.Current.CancellationToken);
        var result = await rig.Authority.ApplyVerifiedAliasAsync(proof, TestContext.Current.CancellationToken);
        Assert.StartsWith("soul_", result.SoulId);
        Assert.Equal(result, await rig.Authority.ApplyVerifiedAliasAsync(proof, TestContext.Current.CancellationToken));
        Assert.Equal(proof, await rig.Authority.VerifyAliasAsync(request, TestContext.Current.CancellationToken));
        await using var lease = await rig.Authority.AcquireOwnerLeaseAsync(request.TenantRef,
            result.SoulId, TestContext.Current.CancellationToken);
        Assert.Equal(1, lease.Snapshot.Revision);
        Assert.Equal(0, lease.Snapshot.RevocationEpoch);
        await lease.RevalidateAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, await rig.CountAsync("identity_authority_aliases"));
        Assert.Equal(1, await rig.CountAsync("identity_authority_receipts"));
        Assert.Equal(1, await rig.CountAsync("identity_authority_events"));
    }

    [Fact, Trait("Category", "Integration")]
    public async Task RetainedKeyCollisionAndLegacyBooleanCannotCreateAuthority()
    {
        await using var rig = await Rig.CreateAsync();
        var first = rig.Issue("Alias@example.test", "create");
        var firstProof = await rig.Authority.VerifyAliasAsync(first, TestContext.Current.CancellationToken);
        var resolved = await rig.Authority.ApplyVerifiedAliasAsync(firstProof, TestContext.Current.CancellationToken);
        var colliding = rig.Issue("alias@EXAMPLE.test", "create");
        var secondProof = await rig.Authority.VerifyAliasAsync(colliding, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<AliasConflictException>(() => rig.Authority.ApplyVerifiedAliasAsync(
            secondProof, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<AliasConflictException>(() => rig.Registry.RegisterVerifiedAliasAsync(
            rig.LegacyRegistration("ALIAS@example.test"), TestContext.Current.CancellationToken));

        var legacy = await rig.Registry.RegisterVerifiedAliasAsync(
            rig.LegacyRegistration("legacy@example.test"), TestContext.Current.CancellationToken);
        Assert.NotEqual(resolved.SoulId, legacy.SoulId);
        await Assert.ThrowsAsync<AliasNotFoundException>(() => rig.Authority.AcquireOwnerLeaseAsync(
            "tenant_" + Rig.Hash("tenant"), legacy.SoulId, TestContext.Current.CancellationToken));
        var upgrade = rig.Issue("legacy@example.test", "create");
        var upgradeProof = await rig.Authority.VerifyAliasAsync(upgrade, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<AliasConflictException>(() => rig.Authority.ApplyVerifiedAliasAsync(
            upgradeProof, TestContext.Current.CancellationToken));
        Assert.Equal(1, await rig.CountAsync("identity_authority_receipts"));
    }

    [Fact, Trait("Category", "Integration")]
    public async Task RevokeWaitsForOwnerLeaseAndHistoricalReceiptNeverRestoresCurrent()
    {
        await using var rig = await Rig.CreateAsync();
        var create = rig.Issue("revoke@example.test", "create");
        var createProof = await rig.Authority.VerifyAliasAsync(create, TestContext.Current.CancellationToken);
        var result = await rig.Authority.ApplyVerifiedAliasAsync(createProof, TestContext.Current.CancellationToken);
        rig.AllowControl(result.SoulId);
        var revoke = rig.Issue("revoke@example.test", "revoke", result.SoulId);
        var revokeProof = await rig.Authority.VerifyAliasAsync(revoke, TestContext.Current.CancellationToken);
        var lease = await rig.Authority.AcquireOwnerLeaseAsync(create.TenantRef, result.SoulId,
            TestContext.Current.CancellationToken);
        try
        {
            using var shortWait = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => rig.Authority.ApplyVerifiedAliasAsync(
                revokeProof, shortWait.Token));
            Assert.Equal(1, await rig.CountAsync("identity_authority_receipts"));
        }
        finally { await lease.DisposeAsync(); }
        _ = await rig.Authority.ApplyVerifiedAliasAsync(revokeProof, TestContext.Current.CancellationToken);
        Assert.Equal(result, await rig.Authority.ApplyVerifiedAliasAsync(createProof,
            TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<AliasNotFoundException>(() => rig.Authority.AcquireOwnerLeaseAsync(
            create.TenantRef, result.SoulId, TestContext.Current.CancellationToken));
    }

    [Fact, Trait("Category", "Integration")]
    public async Task ChangedNonceAndTargetCannotReuseSignedEvidence()
    {
        await using var rig = await Rig.CreateAsync();
        var request = rig.Issue("bound@example.test", "create");
        await Assert.ThrowsAsync<SoulRegistryException>(() => rig.Authority.VerifyAliasAsync(
            request with { Nonce = "nonce_" + Rig.Hash("different") }, TestContext.Current.CancellationToken));
        var proof = await rig.Authority.VerifyAliasAsync(request, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<SoulRegistryException>(() => rig.Authority.ApplyVerifiedAliasAsync(
            proof with { DeviceBindingId = "db_" + Rig.Hash("victim")[..32] },
            TestContext.Current.CancellationToken));
        Assert.Equal(0, await rig.CountAsync("identity_authority_receipts"));
    }

    [Fact, Trait("Category", "Integration")]
    public async Task NonceAndIdempotencyCollisionsAreDurableAcrossRequests()
    {
        await using var rig = await Rig.CreateAsync();
        var first = rig.Issue("nonce@example.test", "create");
        var proof = await rig.Authority.VerifyAliasAsync(first, TestContext.Current.CancellationToken);
        Assert.Equal(proof, await rig.Authority.VerifyAliasAsync(first, TestContext.Current.CancellationToken));
        var sameNonce = rig.Issue("other@example.test", "create", nonce: first.Nonce);
        await Assert.ThrowsAsync<IdempotencyConflictException>(() => rig.Authority.VerifyAliasAsync(
            sameNonce, TestContext.Current.CancellationToken));
        var sameIdem = rig.Issue("other@example.test", "create", idem: first.IdempotencyKey);
        await Assert.ThrowsAsync<IdempotencyConflictException>(() => rig.Authority.VerifyAliasAsync(
            sameIdem, TestContext.Current.CancellationToken));
        Assert.Equal(1, await rig.CountAsync("identity_verifications"));
        Assert.Equal(0, await rig.CountAsync("identity_authority_receipts"));
    }

    [Fact, Trait("Category", "Integration")]
    public async Task PinnedTrustMetadataAndPersistedReferenceTamperingFailClosed()
    {
        await using var rig = await Rig.CreateAsync();
        var request = rig.Issue("tamper@example.test", "create");
        var proof = await rig.Authority.VerifyAliasAsync(request, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<SoulRegistryException>(() => rig.Authority.ApplyVerifiedAliasAsync(
            rig.Resign(proof with { ReleaseBomSha256 = Rig.Hash("other-bom") }),
            TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<SoulRegistryException>(() => rig.Authority.ApplyVerifiedAliasAsync(
            rig.Resign(proof with { TrustEpoch = 2 }), TestContext.Current.CancellationToken));
        foreach (var changed in new[]
        {
            proof with { KeyId = "unknown-key" },
            proof with { AliasKeyEpoch = 2 },
            proof with { RevocationEpoch = 1 },
            proof with { ReleaseGeneration = 2 }
        })
            await Assert.ThrowsAsync<SoulRegistryException>(() => rig.Authority.ApplyVerifiedAliasAsync(
                rig.Resign(changed), TestContext.Current.CancellationToken));
        await rig.TamperReferencesAsync(proof.ProofId);
        await Assert.ThrowsAsync<SoulRegistryException>(() => rig.Authority.ApplyVerifiedAliasAsync(
            proof, TestContext.Current.CancellationToken));
        Assert.Equal(0, await rig.CountAsync("identity_authority_aliases"));
        Assert.Equal(0, await rig.CountAsync("identity_authority_receipts"));
    }

    [Fact, Trait("Category", "Integration")]
    public async Task BackendLossReleasesOwnerLeaseAndRevokeCanRecover()
    {
        await using var rig = await Rig.CreateAsync();
        var create = rig.Issue("backend@example.test", "create");
        var created = await rig.Authority.ApplyVerifiedAliasAsync(
            await rig.Authority.VerifyAliasAsync(create, TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);
        rig.AllowControl(created.SoulId);
        var revoke = rig.Issue("backend@example.test", "revoke", created.SoulId);
        var proof = await rig.Authority.VerifyAliasAsync(revoke, TestContext.Current.CancellationToken);
        var lease = await rig.Authority.AcquireOwnerLeaseAsync(create.TenantRef, created.SoulId,
            TestContext.Current.CancellationToken);
        try
        {
            await rig.TerminateBackendAsync(lease.BackendProcessId);
            await Assert.ThrowsAnyAsync<Exception>(() => lease.RevalidateAsync(TestContext.Current.CancellationToken));
        }
        finally { try { await lease.DisposeAsync(); } catch (NpgsqlException) { } }
        _ = await rig.Authority.ApplyVerifiedAliasAsync(proof, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<AliasNotFoundException>(() => rig.Authority.AcquireOwnerLeaseAsync(
            create.TenantRef, created.SoulId, TestContext.Current.CancellationToken));
    }

    [Fact, Trait("Category", "Integration")]
    public async Task RealCommitAcknowledgementLossReconcilesExactReceipt()
    {
        await using var rig = await Rig.CreateAsync();
        var request = rig.Issue("ack@example.test", "create");
        var proof = await rig.Authority.VerifyAliasAsync(request, TestContext.Current.CancellationToken);
        await using var proxy = new CommitAckDroppingProxy(rig.SocketPath, rig.Port);
        using var proxiedRegistry = rig.CreateRegistry(proxy.ConnectionString(rig.ConnectionString));
        var proxiedAuthority = rig.CreateAuthority(proxiedRegistry);
        var result = await proxiedAuthority.ApplyVerifiedAliasAsync(proof, TestContext.Current.CancellationToken);
        Assert.True(proxy.DroppedCommitAcknowledgement);
        Assert.Equal(result, await rig.Authority.ApplyVerifiedAliasAsync(proof,
            TestContext.Current.CancellationToken));
        Assert.Equal(1, await rig.CountAsync("identity_authority_receipts"));
    }

    [Fact, Trait("Category", "Integration")]
    public async Task RealVerifyCommitAcknowledgementLossReturnsStoredSignedProof()
    {
        await using var rig = await Rig.CreateAsync();
        var request = rig.Issue("verify-ack@example.test", "create");
        await using var proxy = new CommitAckDroppingProxy(rig.SocketPath, rig.Port);
        using var proxiedRegistry = rig.CreateRegistry(proxy.ConnectionString(rig.ConnectionString));
        var proxiedAuthority = rig.CreateAuthority(proxiedRegistry);
        var proof = await proxiedAuthority.VerifyAliasAsync(request, TestContext.Current.CancellationToken);
        Assert.True(proxy.DroppedCommitAcknowledgement);
        Assert.Equal(proof, await rig.Authority.VerifyAliasAsync(request,
            TestContext.Current.CancellationToken));
        Assert.Equal(1, await rig.CountAsync("identity_verifications"));
    }

    [Fact, Trait("Category", "Integration")]
    public async Task BackendTerminationBeforeCommitRollsBackAndExactRetryRecovers()
    {
        await using var rig = await Rig.CreateAsync();
        var request = rig.Issue("crash@example.test", "create");
        var proof = await rig.Authority.VerifyAliasAsync(request, TestContext.Current.CancellationToken);
        await rig.InstallTerminateBeforeReceiptTriggerAsync();
        await Assert.ThrowsAnyAsync<Exception>(() => rig.Authority.ApplyVerifiedAliasAsync(
            proof, TestContext.Current.CancellationToken));
        Assert.Equal(0, await rig.CountAsync("identity_authority_aliases"));
        Assert.Equal(0, await rig.CountAsync("soul_current_state"));
        Assert.Equal(0, await rig.CountAsync("identity_authority_receipts"));
        await rig.RemoveTerminateBeforeReceiptTriggerAsync();
        var result = await rig.Authority.ApplyVerifiedAliasAsync(proof, TestContext.Current.CancellationToken);
        Assert.StartsWith("soul_", result.SoulId);
        Assert.Equal(1, await rig.CountAsync("identity_authority_receipts"));
    }

    [Fact, Trait("Category", "Integration")]
    public async Task Legacy001002UpgradeDoesNotBackfillTrustedCurrent()
    {
        await using var rig = await Rig.CreateAsync(initialize: false);
        var oldSoul = await rig.InstallLegacySchemaAndAliasAsync("prior@example.test");
        await rig.Registry.InitializeAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, await rig.CountAsync("soul_current_state"));
        Assert.Equal(0, await rig.CountAsync("identity_authority_aliases"));
        await Assert.ThrowsAsync<AliasNotFoundException>(() => rig.Authority.AcquireOwnerLeaseAsync(
            "tenant_" + Rig.Hash("tenant"), oldSoul, TestContext.Current.CancellationToken));
        var request = rig.Issue("prior@example.test", "create");
        var proof = await rig.Authority.VerifyAliasAsync(request, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<AliasConflictException>(() => rig.Authority.ApplyVerifiedAliasAsync(
            proof, TestContext.Current.CancellationToken));
    }

    [Fact, Trait("Category", "Integration")]
    public async Task PendingProofFailsClosedWhenRetainedKeyringChanges()
    {
        await using var rig = await Rig.CreateAsync();
        var request = rig.Issue("rotation@example.test", "create");
        var proof = await rig.Authority.VerifyAliasAsync(request, TestContext.Current.CancellationToken);
        using var narrowedRegistry = rig.CreateRegistry(rig.ConnectionString, includeOldKey: false);
        var narrowedAuthority = rig.CreateAuthority(narrowedRegistry);
        await Assert.ThrowsAsync<SoulRegistryException>(() => narrowedAuthority.ApplyVerifiedAliasAsync(
            proof, TestContext.Current.CancellationToken));
        Assert.Equal(0, await rig.CountAsync("identity_authority_receipts"));
        _ = await rig.Authority.ApplyVerifiedAliasAsync(proof, TestContext.Current.CancellationToken);
        Assert.Equal(1, await rig.CountAsync("identity_authority_receipts"));
    }

    [Fact, Trait("Category", "Integration")]
    public async Task AttachResolveAndPartialThenFinalRevokeAdvanceOwnerEpoch()
    {
        await using var rig = await Rig.CreateAsync();
        var create = rig.Issue("first@example.test", "create");
        var first = await rig.Authority.ApplyVerifiedAliasAsync(
            await rig.Authority.VerifyAliasAsync(create, TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);
        rig.AllowControl(first.SoulId);
        var attach = rig.Issue("second@example.test", "attach", first.SoulId);
        var attached = await rig.Authority.ApplyVerifiedAliasAsync(
            await rig.Authority.VerifyAliasAsync(attach, TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);
        Assert.Equal(first.SoulId, attached.SoulId);
        var resolve = rig.Issue("second@example.test", "resolve", first.SoulId);
        var resolved = await rig.Authority.ApplyVerifiedAliasAsync(
            await rig.Authority.VerifyAliasAsync(resolve, TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);
        Assert.Equal(first.SoulId, resolved.SoulId);
        var revokeFirst = rig.Issue("first@example.test", "revoke", first.SoulId);
        _ = await rig.Authority.ApplyVerifiedAliasAsync(
            await rig.Authority.VerifyAliasAsync(revokeFirst, TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);
        await using (var lease = await rig.Authority.AcquireOwnerLeaseAsync(create.TenantRef,
            first.SoulId, TestContext.Current.CancellationToken))
        {
            Assert.Equal(3, lease.Snapshot.Revision);
            Assert.Equal(1, lease.Snapshot.RevocationEpoch);
        }
        var revokedResolve = rig.Issue("first@example.test", "resolve", first.SoulId);
        var revokedProof = await rig.Authority.VerifyAliasAsync(revokedResolve,
            TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<AliasRevokedException>(() => rig.Authority.ApplyVerifiedAliasAsync(
            revokedProof, TestContext.Current.CancellationToken));
        var revokeSecond = rig.Issue("second@example.test", "revoke", first.SoulId);
        _ = await rig.Authority.ApplyVerifiedAliasAsync(
            await rig.Authority.VerifyAliasAsync(revokeSecond, TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<AliasNotFoundException>(() => rig.Authority.AcquireOwnerLeaseAsync(
            create.TenantRef, first.SoulId, TestContext.Current.CancellationToken));
    }

    private sealed class Rig : IAsyncDisposable
    {
        private readonly string _connectionString;
        private readonly string _schema;
        private readonly ECDsa _proofSigner;
        private readonly ECDsa _proofVerifier;
        private readonly AliasHmacKey[] _keys;
        private readonly SignedEvidence _evidence;
        private readonly SoulAuthorityBindings _bindings;
        internal PostgresSoulRegistry Registry { get; }
        internal SoulIdentityAuthority Authority { get; }

        private Rig(string connectionString, string schema)
        {
            _connectionString = connectionString; _schema = schema;
            _keys = [new AliasHmacKey("key-v1", Enumerable.Repeat((byte)0x21, 32).ToArray()),
                new AliasHmacKey("key-v2", Enumerable.Repeat((byte)0x22, 32).ToArray())];
            Registry = new PostgresSoulRegistry(new SoulRegistryOptions(connectionString, schema, "key-v2", _keys));
            _proofSigner = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            _proofVerifier = ECDsa.Create(_proofSigner.ExportParameters(false));
            _evidence = new SignedEvidence();
            _bindings = new SoulAuthorityBindings(
                _evidence, _proofVerifier,
                bytes => _proofSigner.SignData(bytes, HashAlgorithmName.SHA256,
                    DSASignatureFormat.IeeeP1363FixedFieldConcatenation),
                "test-soul-authority", 1, 1, Hash("test-bom"), 1, TimeProvider.System);
            Authority = new SoulIdentityAuthority(Registry, _bindings);
        }

        internal static async Task<Rig> CreateAsync(bool initialize = true)
        {
            var connectionString = Environment.GetEnvironmentVariable("DPS_TEST_POSTGRES");
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("DPS_TEST_POSTGRES is required for real integration.");
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using (var version = new NpgsqlCommand("SHOW server_version_num", connection))
                Assert.Equal("180004", await version.ExecuteScalarAsync(TestContext.Current.CancellationToken));
            var rig = new Rig(connectionString, "dps_p05_r1_" + Guid.NewGuid().ToString("N"));
            if (initialize) await rig.Registry.InitializeAsync(TestContext.Current.CancellationToken);
            return rig;
        }

        internal string ConnectionString => _connectionString;
        internal string SocketPath => new NpgsqlConnectionStringBuilder(_connectionString).Host
            ?? throw new InvalidOperationException("The test PostgreSQL socket path is missing.");
        internal int Port => new NpgsqlConnectionStringBuilder(_connectionString).Port;
        internal PostgresSoulRegistry CreateRegistry(string connectionString, bool includeOldKey = true) =>
            new(new SoulRegistryOptions(connectionString, _schema, "key-v2",
                includeOldKey ? _keys : [_keys[1]]));
        internal SoulIdentityAuthority CreateAuthority(PostgresSoulRegistry registry) => new(registry, _bindings);

        internal SoulAliasVerificationV1 Resign(SoulAliasVerificationV1 proof) => proof with
        {
            SignatureBase64 = Convert.ToBase64String(_proofSigner.SignData(proof.CanonicalBytes(),
                HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
        };

        internal SoulAliasVerificationRequestV1 Issue(string alias, string operation, string? target = null,
            string? nonce = null, string? idem = null)
        {
            var request = new SoulAliasVerificationRequestV1
            {
                RequestId = Guid.NewGuid(), TenantRef = "tenant_" + Hash("tenant"), Operation = operation,
                AliasKind = "email", ProofRef = "proof_" + Hash(Guid.NewGuid().ToString("N")),
                TargetSoulId = target, DeviceBindingId = "db_" + Hash("device")[..32],
                PlatformAccountId = "pa_" + Hash("account")[..32], TraceId = "trace_" + Hash(Guid.NewGuid().ToString("N"))[..32],
                IdempotencyKey = idem ?? "idem_" + Hash(Guid.NewGuid().ToString("N")),
                OccurredAt = DateTimeOffset.UtcNow, Nonce = nonce ?? "nonce_" + Hash(Guid.NewGuid().ToString("N"))
            };
            _evidence.Issue(request, alias);
            return request;
        }

        internal void AllowControl(string soulId) => _evidence.AllowControl(soulId);

        internal RegisterVerifiedAliasRequest LegacyRegistration(string alias) => new(
            "1.0.0", "tenant_" + Hash("tenant"), IdentityAliasKind.Email, alias,
            new AliasVerification("caller-reported", DateTimeOffset.UtcNow),
            "db_" + Hash("device")[..32], "pa_" + Hash("account")[..32],
            "trace_" + Hash(Guid.NewGuid().ToString("N"))[..32],
            "idem_" + Hash(Guid.NewGuid().ToString("N")), DateTimeOffset.UtcNow);

        internal async Task<long> CountAsync(string table)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = new NpgsqlCommand($"SELECT count(*) FROM {_schema}.{table}", connection);
            return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken) ?? 0L);
        }

        internal async Task TamperReferencesAsync(Guid proofId)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = new NpgsqlCommand($"""
                UPDATE {_schema}.identity_verifications SET alias_references = '[]'::jsonb
                WHERE proof_id = @proof_id
                """, connection);
            command.Parameters.AddWithValue("proof_id", proofId);
            Assert.Equal(1, await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));
        }

        internal async Task TerminateBackendAsync(int backendPid)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = new NpgsqlCommand("SELECT pg_terminate_backend(@pid)", connection);
            command.Parameters.AddWithValue("pid", backendPid);
            Assert.Equal(true, await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));
        }

        internal async Task InstallTerminateBeforeReceiptTriggerAsync()
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = new NpgsqlCommand($"""
                CREATE FUNCTION {_schema}.terminate_before_receipt() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    PERFORM pg_terminate_backend(pg_backend_pid());
                    RETURN NEW;
                END;
                $$;
                CREATE TRIGGER p05_terminate_before_receipt
                BEFORE INSERT ON {_schema}.identity_authority_receipts
                FOR EACH ROW EXECUTE FUNCTION {_schema}.terminate_before_receipt();
                """, connection);
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        internal async Task RemoveTerminateBeforeReceiptTriggerAsync()
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = new NpgsqlCommand($"""
                DROP TRIGGER p05_terminate_before_receipt ON {_schema}.identity_authority_receipts;
                DROP FUNCTION {_schema}.terminate_before_receipt();
                """, connection);
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        internal async Task<string> InstallLegacySchemaAndAliasAsync(string alias)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            var assembly = typeof(PostgresSoulRegistry).Assembly;
            foreach (var suffix in new[] { "001_create_soul_registry.sql", "002_record_receipt_hash_basis.sql" })
            {
                var name = assembly.GetManifestResourceNames().Single(item => item.EndsWith(suffix, StringComparison.Ordinal));
                await using var stream = assembly.GetManifestResourceStream(name)!;
                using var reader = new StreamReader(stream);
                var sql = (await reader.ReadToEndAsync(TestContext.Current.CancellationToken))
                    .Replace("__SCHEMA__", _schema, StringComparison.Ordinal);
                await using var migration = new NpgsqlCommand(sql, connection);
                await migration.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            }
            var soul = "soul_" + Hash("old-soul");
            var tenant = "tenant_" + Hash("tenant");
            var reference = Registry.ComputeAliasReferences(tenant, IdentityAliasKind.Email, alias)
                .Single(item => item.KeyId == "key-v2");
            await using var insert = new NpgsqlCommand($"""
                INSERT INTO {_schema}.souls (soul_id, tenant_id, created_at)
                VALUES (@soul, @tenant, now());
                INSERT INTO {_schema}.identity_aliases
                    (alias_id, tenant_id, alias_kind, alias_digest, alias_key_id, soul_id,
                     verification_evidence_sha256, verified_at, created_at)
                VALUES (@alias_id, @tenant, @kind, @digest, @key, @soul, @evidence, now(), now());
                """, connection);
            insert.Parameters.AddWithValue("soul", soul);
            insert.Parameters.AddWithValue("tenant", tenant);
            insert.Parameters.AddWithValue("alias_id", Guid.NewGuid());
            insert.Parameters.AddWithValue("kind", reference.Kind);
            insert.Parameters.AddWithValue("digest", reference.Digest);
            insert.Parameters.AddWithValue("key", reference.KeyId);
            insert.Parameters.AddWithValue("evidence", Hash("legacy-caller-boolean"));
            await insert.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            return soul;
        }

        internal static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

        public async ValueTask DisposeAsync()
        {
            _evidence.Dispose(); _proofVerifier.Dispose(); _proofSigner.Dispose(); Registry.Dispose();
            foreach (var key in _keys) key.Dispose();
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(CancellationToken.None);
            await using var command = new NpgsqlCommand($"DROP SCHEMA IF EXISTS {_schema} CASCADE", connection);
            await command.ExecuteNonQueryAsync(CancellationToken.None);
        }
    }

    // Test-owned loopback bridge: forwards the real PostgreSQL COMMIT to the
    // server, then drops only its wire acknowledgement before Npgsql sees it.
    private sealed class CommitAckDroppingProxy : IAsyncDisposable
    {
        private readonly string _socketPath;
        private readonly int _postgresPort;
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stop = new();
        private readonly ConcurrentBag<Task> _bridges = [];
        private readonly Task _accept;
        private int _dropNext = 1;
        private int _dropped;
        internal bool DroppedCommitAcknowledgement => Volatile.Read(ref _dropped) == 1;

        internal CommitAckDroppingProxy(string socketPath, int postgresPort)
        {
            _socketPath = socketPath; _postgresPort = postgresPort;
            _listener.Start();
            _accept = AcceptAsync();
        }

        internal string ConnectionString(string original)
        {
            var builder = new NpgsqlConnectionStringBuilder(original)
            {
                Host = "127.0.0.1", Port = ((IPEndPoint)_listener.LocalEndpoint).Port,
                Pooling = false, SslMode = SslMode.Disable
            };
            return builder.ConnectionString;
        }

        private async Task AcceptAsync()
        {
            try
            {
                while (!_stop.IsCancellationRequested)
                {
                    var client = await _listener.AcceptTcpClientAsync(_stop.Token);
                    _bridges.Add(BridgeAsync(client));
                }
            }
            catch (OperationCanceledException) { }
            catch (SocketException) when (_stop.IsCancellationRequested) { }
        }

        private async Task BridgeAsync(TcpClient client)
        {
            using (client)
            using (var backend = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified))
            {
                try
                {
                    await backend.ConnectAsync(new UnixDomainSocketEndPoint(
                        $"{_socketPath}/.s.PGSQL.{_postgresPort}"), _stop.Token);
                    await using var serverStream = new NetworkStream(backend, ownsSocket: false);
                    var clientStream = client.GetStream();
                    var sawCommit = 0;
                    async Task Pump(Stream source, Stream target, bool clientToServer)
                    {
                        var buffer = new byte[8192];
                        var tail = string.Empty;
                        while (!_stop.IsCancellationRequested)
                        {
                            var count = await source.ReadAsync(buffer, _stop.Token);
                            if (count == 0) break;
                            if (clientToServer)
                            {
                                var text = tail + Encoding.ASCII.GetString(buffer, 0, count);
                                if (text.Contains("COMMIT\0", StringComparison.OrdinalIgnoreCase))
                                    Volatile.Write(ref sawCommit, 1);
                                tail = text.Length > 6 ? text[^6..] : text;
                            }
                            else if (Volatile.Read(ref sawCommit) == 1 &&
                                     Interlocked.CompareExchange(ref _dropNext, 0, 1) == 1)
                            {
                                Volatile.Write(ref _dropped, 1);
                                client.Close();
                                break;
                            }
                            await target.WriteAsync(buffer.AsMemory(0, count), _stop.Token);
                        }
                    }
                    var outbound = Pump(clientStream, serverStream, true);
                    var inbound = Pump(serverStream, clientStream, false);
                    await Task.WhenAny(outbound, inbound);
                    client.Close();
                    backend.Dispose();
                    try { await Task.WhenAll(outbound, inbound); }
                    catch (Exception) when (!_stop.IsCancellationRequested) { }
                }
                catch (Exception) when (!_stop.IsCancellationRequested) { }
            }
        }

        public async ValueTask DisposeAsync()
        {
            _stop.Cancel(); _listener.Stop();
            await _accept;
            try { await Task.WhenAll(_bridges); } catch (Exception) { }
            _stop.Dispose();
        }
    }

    private sealed class SignedEvidence : ISoulAliasEvidenceAuthority, IDisposable
    {
        private readonly ECDsa _signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        private readonly ECDsa _verifier;
        private readonly Dictionary<string, (string RequestHash, string RawAlias, string? Control, byte[] Signature)> _proofs = [];
        private readonly HashSet<string> _controlledSouls = [];
        internal SignedEvidence() => _verifier = ECDsa.Create(_signer.ExportParameters(false));
        internal void AllowControl(string soulId) => _controlledSouls.Add(soulId);

        internal void Issue(SoulAliasVerificationRequestV1 request, string alias)
        {
            if (request.TargetSoulId is not null && !_controlledSouls.Contains(request.TargetSoulId))
                throw new SoulRegistryException("The test evidence issuer does not control this Soul.");
            var control = request.TargetSoulId is null ? null : Rig.Hash("control:" + request.TargetSoulId);
            var requestHash = request.ComputeSha256();
            var message = Encoding.UTF8.GetBytes(requestHash + "\n" + alias + "\n" + control);
            var signature = _signer.SignData(message, HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            _proofs.Add(request.ProofRef, (requestHash, alias, control, signature));
        }

        public Task<VerifiedAliasEvidence> AuthenticateAndVerifyAsync(
            SoulAliasVerificationRequestV1 request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_proofs.TryGetValue(request.ProofRef, out var evidence))
                throw new SoulRegistryException("Unknown external proof reference.");
            var message = Encoding.UTF8.GetBytes(evidence.RequestHash + "\n" + evidence.RawAlias + "\n" + evidence.Control);
            if (!SecretComparison.EqualsHex(request.ComputeSha256(), evidence.RequestHash) ||
                !_verifier.VerifyData(message, evidence.Signature, HashAlgorithmName.SHA256,
                    DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
                throw new SoulRegistryException("External identity evidence was changed.");
            return Task.FromResult(new VerifiedAliasEvidence(evidence.RequestHash, evidence.RawAlias,
                request.TargetSoulId, evidence.Control));
        }

        public void Dispose() { _verifier.Dispose(); _signer.Dispose(); }
    }
}
