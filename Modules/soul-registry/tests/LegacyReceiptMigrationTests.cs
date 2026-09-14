using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dps.SoulRegistry.Contracts;
using Npgsql;
using NpgsqlTypes;
using Xunit;

namespace Dps.SoulRegistry.Tests;

public sealed partial class PostgresSoulRegistryIntegrationTests
{
    [Theory]
    [InlineData("register")]
    [InlineData("resolve")]
    [InlineData("revoke")]
    [Trait("Category", "Integration")]
    public async Task NativeLegacyReceiptsSurviveMigrationAndExplicitHistoricalKeyReplay(string operation)
    {
        await using var database = await TestDatabase.CreateAsync();
        await ImportLegacyRowsAsync(database, operation, initializeLegacySchema: true);
        var beforeMigration = JsonNode.Parse(await database.ReadStoredRowsAsync())!;
        using var originalKeys = database.CreateRegistry("key-v1", Key("key-v1", 0x21));
        await originalKeys.InitializeAsync(TestContext.Current.CancellationToken);
        var afterMigration = JsonNode.Parse(await database.ReadStoredRowsAsync())!;
        foreach (var table in new[] { "receipts", "mutations" })
        {
            if (afterMigration[table] is not JsonArray rows) continue;
            foreach (var row in rows)
            {
                Assert.Equal(1, row!["request_hash_version"]!.GetValue<int>());
                Assert.Null(row["request_hash_key_ids"]);
                row.AsObject().Remove("request_hash_version");
                row.AsObject().Remove("request_hash_key_ids");
            }
        }
        Assert.True(JsonNode.DeepEquals(beforeMigration, afterMigration));
        await AssertLegacyReplayAsync(database, originalKeys, operation);

        var beforeReplay = await database.ReadStoredRowsAsync();
        using var noHistory = database.CreateRegistry("key-v2", Key("key-v1", 0x21), Key("key-v2", 0x22));
        await Assert.ThrowsAsync<IdempotencyConflictException>(() => ReplayLegacyAsync(database, noHistory, operation));

        using var oldKey = new AliasHmacKey("key-v1", Enumerable.Repeat((byte)0x21, 32).ToArray());
        using var newKey = new AliasHmacKey("key-v2", Enumerable.Repeat((byte)0x22, 32).ToArray());
        var historicalSet = new List<string> { "key-v1" };
        var historicalSets = new List<IReadOnlyList<string>> { historicalSet };
        using var withHistory = new PostgresSoulRegistry(
            new SoulRegistryOptions(database.ConnectionString, database.SchemaName, "key-v2", [oldKey, newKey])
            { LegacyReceiptHashKeySets = historicalSets });
        historicalSet[0] = "key-v2";
        historicalSets.Clear();
        await AssertLegacyReplayAsync(database, withHistory, operation);
        // Re-running additive migrations must preserve every original fact and receipt hash.
        await withHistory.InitializeAsync(TestContext.Current.CancellationToken);
        Assert.Equal(beforeReplay, await database.ReadStoredRowsAsync());
        await database.AssertMutationRejectedAsync(
            $"UPDATE {database.SchemaName}.resolution_receipts SET request_hash_key_ids = ARRAY['key-v1']",
            PostgresErrorCodes.RaiseException);
        if (operation == "revoke")
            await database.AssertMutationRejectedAsync(
                $"UPDATE {database.SchemaName}.mutation_receipts SET request_hash_version = 2, request_hash_key_ids = ARRAY['key-v1']",
                PostgresErrorCodes.RaiseException);
        Assert.Equal(beforeReplay, await database.ReadStoredRowsAsync());
    }

    [Theory]
    [InlineData("register")]
    [InlineData("resolve")]
    [InlineData("revoke")]
    [Trait("Category", "Integration")]
    public async Task LegacyWriterColumnShapeStillDefaultsToVersionOne(string operation)
    {
        await using var database = await TestDatabase.CreateAsync();
        using var registry = database.CreateRegistry("key-v1", Key("key-v1", 0x21));
        await registry.InitializeAsync(TestContext.Current.CancellationToken);
        await ImportLegacyRowsAsync(database, operation, initializeLegacySchema: false);
        await AssertLegacyReplayAsync(database, registry, operation);
        var stored = JsonNode.Parse(await database.ReadStoredRowsAsync())!;
        Assert.All(stored["receipts"]!.AsArray(), row => Assert.Equal(1, row!["request_hash_version"]!.GetValue<int>()));
    }

    [Theory]
    [InlineData("register")]
    [InlineData("resolve")]
    [InlineData("revoke")]
    [Trait("Category", "Integration")]
    public async Task VersionTwoRequiresItsRecordedKeyMaterial(string operation)
    {
        await using var database = await TestDatabase.CreateAsync();
        using var registry = database.CreateRegistry("key-v1", Key("key-v1", 0x21));
        await registry.InitializeAsync(TestContext.Current.CancellationToken);
        var registration = Register("rotation.replay@example.test", "rotation-register");
        var resolved = await registry.RegisterVerifiedAliasAsync(registration, TestContext.Current.CancellationToken);
        if (operation == "resolve")
            await registry.ResolveAsync(Resolve(registration.RawAlias, "rotation-resolve"), TestContext.Current.CancellationToken);
        if (operation == "revoke")
            await registry.RevokeAliasAsync(Revoke(registration.RawAlias, resolved.SoulId, "rotation-revoke", "verified correction"), TestContext.Current.CancellationToken);
        var stored = await database.ReadStoredRowsAsync();
        foreach (var table in new[] { "receipts", "mutations" })
        {
            var rows = JsonNode.Parse(stored)![table] as JsonArray;
            if (rows is null) continue;
            Assert.All(rows, row =>
            {
                Assert.Equal(2, row!["request_hash_version"]!.GetValue<int>());
                Assert.Equal("key-v1", Assert.Single(row["request_hash_key_ids"]!.AsArray())!.GetValue<string>());
            });
        }
        using var keyMissing = database.CreateRegistry("key-v2", Key("key-v2", 0x22));
        await Assert.ThrowsAsync<ReceiptHashBasisUnavailableException>(() => ReplayLegacyAsync(database, keyMissing, operation));
        using var keyReplaced = database.CreateRegistry("key-v2", Key("key-v1", 0x99), Key("key-v2", 0x22));
        await Assert.ThrowsAsync<IdempotencyConflictException>(() => ReplayLegacyAsync(database, keyReplaced, operation));
        Assert.Equal(stored, await database.ReadStoredRowsAsync());
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task VersionTwoBindsAllRegistrationFieldsAndProtectsMetadata()
    {
        await using var database = await TestDatabase.CreateAsync();
        using var registry = database.CreateRegistry("key-v1", Key("key-v1", 0x21));
        await registry.InitializeAsync(TestContext.Current.CancellationToken);
        var request = Register("rotation.replay@example.test", "rotation-register");
        await registry.RegisterVerifiedAliasAsync(request, TestContext.Current.CancellationToken);
        var stored = await database.ReadStoredRowsAsync();
        var variants = new[]
        {
            request with { RawAlias = "changed@example.test" },
            request with { AliasKind = IdentityAliasKind.PlatformId },
            request with { DeviceBindingId = "db_" + new string('a', 32) },
            request with { PlatformAccountId = "pa_" + new string('a', 32) },
            request with { TraceId = "trace_" + new string('a', 32) },
            request with { OccurredAt = request.OccurredAt.AddSeconds(1) },
            request with { TargetSoulId = "soul_" + new string('a', 64) },
            request with { Verification = request.Verification with { EvidenceId = "changed-proof" } },
            request with { Verification = request.Verification with { VerifiedAt = request.Verification.VerifiedAt.AddSeconds(-1) } }
        };
        foreach (var variant in variants)
            await Assert.ThrowsAsync<IdempotencyConflictException>(() => registry.RegisterVerifiedAliasAsync(variant, TestContext.Current.CancellationToken));
        await database.AssertMutationRejectedAsync(
            $"UPDATE {database.SchemaName}.resolution_receipts SET request_hash_key_ids = ARRAY['key-v2']",
            PostgresErrorCodes.RaiseException);
        Assert.Equal(stored, await database.ReadStoredRowsAsync());
    }

    [Theory]
    [InlineData("register")]
    [InlineData("resolve")]
    [InlineData("revoke")]
    [Trait("Category", "Integration")]
    public async Task LegacyMultiKeyReceiptsRequireTheirCompleteHistoricalKeySet(string operation)
    {
        await using var database = await TestDatabase.CreateAsync();
        await ImportLegacyRowsAsync(database, operation, initializeLegacySchema: true, fixturePrefix: "multi-");
        using var originalKeys = database.CreateRegistry("key-v1", Key("key-v1", 0x21), Key("key-v2", 0x22));
        await originalKeys.InitializeAsync(TestContext.Current.CancellationToken);
        await AssertLegacyReplayAsync(database, originalKeys, operation, fixturePrefix: "multi-");
        var stored = await database.ReadStoredRowsAsync();

        using var firstKey = new AliasHmacKey("key-v1", Enumerable.Repeat((byte)0x21, 32).ToArray());
        using var secondKey = new AliasHmacKey("key-v2", Enumerable.Repeat((byte)0x22, 32).ToArray());
        using var thirdKey = new AliasHmacKey("key-v3", Enumerable.Repeat((byte)0x23, 32).ToArray());
        var options = new SoulRegistryOptions(database.ConnectionString, database.SchemaName,
            "key-v3", [firstKey, secondKey, thirdKey]);
        using var noHistory = new PostgresSoulRegistry(options);
        await Assert.ThrowsAsync<IdempotencyConflictException>(() => ReplayLegacyAsync(database, noHistory, operation));
        using var resultKeyOnly = new PostgresSoulRegistry(options with
            { LegacyReceiptHashKeySets = [["key-v1"]] });
        await Assert.ThrowsAsync<IdempotencyConflictException>(() => ReplayLegacyAsync(database, resultKeyOnly, operation));
        using var completeHistory = new PostgresSoulRegistry(options with
            { LegacyReceiptHashKeySets = [["key-v1", "key-v2"]] });
        await AssertLegacyReplayAsync(database, completeHistory, operation, fixturePrefix: "multi-");
        Assert.Equal(stored, await database.ReadStoredRowsAsync());
    }

    private static async Task AssertLegacyReplayAsync(
        TestDatabase database, PostgresSoulRegistry registry, string operation, string fixturePrefix = "")
    {
        var result = await ReplayLegacyAsync(database, registry, operation);
        if (operation == "revoke") { Assert.Null(result); return; }
        var expected = await ReadEmbeddedTextAsync(typeof(PostgresSoulRegistryIntegrationTests).Assembly,
            fixturePrefix + operation + ".resolved-receipt.json");
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(expected), JsonSerializer.SerializeToNode(result)));
    }

    private static async Task<SoulResolved?> ReplayLegacyAsync(TestDatabase database, PostgresSoulRegistry registry, string operation)
    {
        const string alias = "rotation.replay@example.test";
        if (operation == "register")
            return await registry.RegisterVerifiedAliasAsync(Register(alias, "rotation-register"), TestContext.Current.CancellationToken);
        if (operation == "resolve")
            return await registry.ResolveAsync(Resolve(alias, "rotation-resolve"), TestContext.Current.CancellationToken);
        var stored = JsonNode.Parse(await database.ReadStoredRowsAsync())!;
        var soulId = stored["souls"]![0]!["soul_id"]!.GetValue<string>();
        await registry.RevokeAliasAsync(Revoke(alias, soulId, "rotation-revoke", "verified correction"), TestContext.Current.CancellationToken);
        return null;
    }

    private static async Task ImportLegacyRowsAsync(
        TestDatabase database, string operation, bool initializeLegacySchema, string fixturePrefix = "")
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);
        if (initializeLegacySchema)
        {
            var migration = await ReadEmbeddedTextAsync(typeof(PostgresSoulRegistry).Assembly, "001_create_soul_registry.sql");
            await using var create = new NpgsqlCommand(migration.Replace("__SCHEMA__", database.SchemaName, StringComparison.Ordinal), connection, transaction);
            await create.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }
        var rows = JsonNode.Parse(await ReadEmbeddedTextAsync(typeof(PostgresSoulRegistryIntegrationTests).Assembly,
            fixturePrefix + operation + ".stored-rows.json"))!;
        var tables = new[]
        {
            ("souls", "souls", "soul_id, tenant_id, created_at, tombstoned_at"),
            ("aliases", "identity_aliases", "alias_id, tenant_id, alias_kind, alias_digest, alias_key_id, soul_id, verification_evidence_sha256, verified_at, created_at, revoked_at, revocation_reason_sha256"),
            ("receipts", "resolution_receipts", "tenant_id, idempotency_key, operation, request_sha256, soul_id, device_binding_id, platform_account_id, trace_id, occurred_at, alias_kind, alias_digest, alias_key_id, created_at"),
            ("mutations", "mutation_receipts", "tenant_id, idempotency_key, operation, request_sha256, entity_id, trace_id, occurred_at, created_at")
        };
        foreach (var (key, table, columns) in tables)
        {
            if (rows[key] is not JsonArray data) continue;
            await using var insert = new NpgsqlCommand(
                $"INSERT INTO {database.SchemaName}.{table} ({columns}) SELECT {columns} FROM jsonb_populate_recordset(NULL::{database.SchemaName}.{table}, @rows)",
                connection, transaction);
            insert.Parameters.AddWithValue("rows", NpgsqlDbType.Jsonb, data.ToJsonString());
            await insert.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }
        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<string> ReadEmbeddedTextAsync(Assembly assembly, string suffix)
    {
        var name = assembly.GetManifestResourceNames().Single(item => item.EndsWith("." + suffix, StringComparison.Ordinal));
        await using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException("Synthetic legacy fixture resource was not found.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
    }
}
