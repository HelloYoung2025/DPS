using System.Data;
using Dps.GBrainProjector.Contracts;
using Dps.InterestReducer;
using Dps.MemoryEventLedger;
using Dps.MemoryEventLedger.Contracts;
using Dps.SoulRegistry.Contracts;
using Npgsql;
using NpgsqlTypes;
using Xunit;
using DeterministicInterestReducer = Dps.InterestReducer.InterestReducer;

namespace Dps.GBrainProjector.Tests;

public sealed class GBrainProjectionPostgresIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task RealPostgresPersistsUniqueBindingAndAppendOnlyRevisionAcrossRestart()
    {
        var environment = PostgresTestEnvironment.Require();

        var cancellationToken = TestContext.Current.CancellationToken;
        var ledgerSchema = $"dps_f2_gbrain_ledger_{Guid.NewGuid():N}";
        var projectorSchema = $"dps_f2_gbrain_projector_{Guid.NewGuid():N}";
        try
        {
            var ledger = new PostgresMemoryEventLedger(new MemoryEventLedgerOptions(environment.MigrationConnectionString, ledgerSchema));
            await ledger.InitializeAsync(cancellationToken);
            var migration = await MigrateAsync(environment, projectorSchema, cancellationToken);
            Assert.Equal(GBrainProjectorMigrationDisposition.Created, migration.Disposition);
            var options = new GBrainProjectorPostgresOptions(
                environment.RuntimeConnectionString,
                projectorSchema,
                migration.MigrationRole);
            var runtime = new GBrainProjectorPostgresRuntime(options);
            await runtime.InitializeAsync(cancellationToken);

            var eventTime = new DateTimeOffset(2026, 7, 14, 1, 0, 0, TimeSpan.Zero);
            var soul = CreateSoul('a', eventTime.AddMinutes(-1));
            var memoryEvent = CreateMemoryEvent(soul, eventTime);
            await ledger.AppendAsync(soul, memoryEvent, cancellationToken);
            var persistedEvents = await ledger.ReadSoulEventsAsync(soul.SoulId, cancellationToken);
            var snapshot = new DeterministicInterestReducer().Reduce(
                new InterestReductionRequest(
                    soul.SoulId,
                    soul.DeviceBindingId,
                    soul.PlatformAccountId,
                    soul.TraceId,
                    "reduce-gbrain-integration",
                    eventTime.AddDays(1),
                    persistedEvents),
                new InterestReducerOptions(86_400m));

            var first = await runtime.RenderAndAppendAsync(persistedEvents, snapshot, cancellationToken);
            var duplicate = await runtime.RenderAndAppendAsync(persistedEvents, snapshot, cancellationToken);
            Assert.Equal(RenderedRevisionDisposition.Inserted, first.Persistence.Disposition);
            Assert.Equal(RenderedRevisionDisposition.DuplicateNoOp, duplicate.Persistence.Disposition);
            Assert.Equal(first.Projection.SourceBindingChecksum, duplicate.Projection.SourceBindingChecksum);
            Assert.Equal(1, await CountAsync(environment.RuntimeConnectionString, projectorSchema, "source_bindings", cancellationToken));
            Assert.Equal(1, await CountAsync(environment.RuntimeConnectionString, projectorSchema, "rendered_revisions", cancellationToken));

            var restarted = new GBrainProjectorPostgresRuntime(options);
            await restarted.InitializeAsync(cancellationToken);
            var bindingAfterRestart = await restarted.ResolveSourceBindingAsync(soul.SoulId, cancellationToken);
            Assert.Equal(first.Projection.SourceId, bindingAfterRestart.SourceId);
            Assert.Equal(first.Projection.SourceBindingRevision, bindingAfterRestart.BindingRevision);
            Assert.Equal(first.Projection.SourceBindingChecksum, bindingAfterRestart.BindingChecksum);

            var tasks = Enumerable.Range(0, 32)
                .Select(_ => restarted.ResolveSourceBindingAsync(soul.SoulId, cancellationToken))
                .ToArray();
            var concurrent = await Task.WhenAll(tasks);
            Assert.Single(concurrent.Select(static value => value.SourceId).Distinct(StringComparer.Ordinal));

            await AssertAppendOnlyAndAclRejectMutations(
                environment,
                projectorSchema,
                soul.SoulId,
                cancellationToken);
        }
        finally
        {
            await DropSchemaAsync(environment.MigrationConnectionString, ledgerSchema);
            await DropSchemaAsync(environment.MigrationConnectionString, projectorSchema);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RealPostgresKnownLegacyCollisionPairNeverSharesSource()
    {
        var environment = PostgresTestEnvironment.Require();

        const string left = "soul_1234567890abcdef1234567890ab000000000000000000000000000000000000";
        const string right = "soul_1234567890abcdef1234567890abffffffffffffffffffffffffffffffffffff";
        var schema = $"dps_f2_gbrain_collision_{Guid.NewGuid():N}";
        var token = TestContext.Current.CancellationToken;
        try
        {
            var migration = await MigrateAsync(environment, schema, token);
            var runtime = new GBrainProjectorPostgresRuntime(
                new GBrainProjectorPostgresOptions(
                    environment.RuntimeConnectionString,
                    schema,
                    migration.MigrationRole));
            await runtime.InitializeAsync(token);
            var results = await Task.WhenAll(
                runtime.ResolveSourceBindingAsync(left, token),
                runtime.ResolveSourceBindingAsync(right, token));
            Assert.NotEqual(results[0].SourceId, results[1].SourceId);
            Assert.Equal(2, await CountAsync(environment.RuntimeConnectionString, schema, "source_bindings", token));
        }
        finally
        {
            await DropSchemaAsync(environment.MigrationConnectionString, schema);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SameCredentialIsRejectedBeforeAnyDdl()
    {
        var environment = PostgresTestEnvironment.Require();
        var schema = NewSchema("dps_gbrain_same_role_");
        try
        {
            var migrator = new GBrainProjectorPostgresMigrator();
            await Assert.ThrowsAsync<PostgresSchemaIntegrityException>(() => migrator.ApplyAsync(
                new GBrainProjectorMigrationOptions(
                    environment.MigrationConnectionString,
                    environment.MigrationConnectionString,
                    schema),
                TestContext.Current.CancellationToken));
            Assert.False(await SchemaExistsAsync(
                environment.MigrationConnectionString,
                schema,
                TestContext.Current.CancellationToken));
        }
        finally
        {
            await DropSchemaAsync(environment.MigrationConnectionString, schema);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RuntimeCredentialForAnotherDatabaseIsRejectedBeforeAnyDdl()
    {
        var environment = PostgresTestEnvironment.Require();
        var schema = NewSchema("dps_gbrain_cross_database_");
        var database = NewSchema("dps_gbrain_other_database_");
        var token = TestContext.Current.CancellationToken;
        try
        {
            var runtimeRole = await ReadCurrentUserAsync(environment.RuntimeConnectionString, token);
            await ExecuteAsync(
                environment.MigrationConnectionString,
                $"CREATE DATABASE {database}",
                token);
            await ExecuteAsync(
                environment.MigrationConnectionString,
                $"GRANT CONNECT ON DATABASE {database} TO {runtimeRole}",
                token);
            var otherDatabaseRuntime = new NpgsqlConnectionStringBuilder(
                environment.RuntimeConnectionString)
            {
                Database = database,
                Pooling = false
            }.ConnectionString;

            var migrator = new GBrainProjectorPostgresMigrator();
            await Assert.ThrowsAsync<PostgresSchemaIntegrityException>(() => migrator.ApplyAsync(
                new GBrainProjectorMigrationOptions(
                    environment.MigrationConnectionString,
                    otherDatabaseRuntime,
                    schema),
                token));
            Assert.False(await SchemaExistsAsync(
                environment.MigrationConnectionString,
                schema,
                token));
        }
        finally
        {
            await DropSchemaAsync(environment.MigrationConnectionString, schema);
            await DropDatabaseAsync(environment.MigrationConnectionString, database);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RealWrongVersionServerIsRejectedByMigrationAndEveryRuntimeConnectionBeforeDdl()
    {
        var environment = PostgresTestEnvironment.Require();
        var wrongVersionConnectionString = PostgresTestEnvironment.RequireWrongVersionConnectionString();
        var schema = NewSchema("dps_gbrain_wrong_pg_version_");
        var token = TestContext.Current.CancellationToken;
        var actualWrongVersion = await ReadServerVersionNumberAsync(
            wrongVersionConnectionString,
            token);
        if (string.Equals(
                actualWrongVersion,
                PostgresGBrainProjectorSchemaVerifier.RequiredServerVersionNumber,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "INFRA_ERROR: DPS_TEST_POSTGRES_WRONG_VERSION_URI must target a real PostgreSQL server whose server_version_num is not 180004; the wrong-version negative is NOT_RUN.");
        }

        try
        {
            var migrator = new GBrainProjectorPostgresMigrator();
            await Assert.ThrowsAsync<PostgresSchemaIntegrityException>(() => migrator.ApplyAsync(
                new GBrainProjectorMigrationOptions(
                    wrongVersionConnectionString,
                    environment.RuntimeConnectionString,
                    schema),
                token));
            Assert.False(await SchemaExistsAsync(
                environment.MigrationConnectionString,
                schema,
                token));

            await Assert.ThrowsAsync<PostgresSchemaIntegrityException>(() => migrator.ApplyAsync(
                new GBrainProjectorMigrationOptions(
                    environment.MigrationConnectionString,
                    wrongVersionConnectionString,
                    schema),
                token));
            Assert.False(await SchemaExistsAsync(
                environment.MigrationConnectionString,
                schema,
                token));

            var runtime = new GBrainProjectorPostgresRuntime(
                new GBrainProjectorPostgresOptions(
                    wrongVersionConnectionString,
                    schema,
                    "dps_expected_migration_owner"));
            for (var attempt = 0; attempt < 2; attempt++)
            {
                await Assert.ThrowsAsync<PostgresSchemaIntegrityException>(() =>
                    runtime.InitializeAsync(token));
            }
        }
        finally
        {
            await DropSchemaAsync(environment.MigrationConnectionString, schema);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task WeakPrecreatedTableIsRejectedWithoutRepair()
    {
        var environment = PostgresTestEnvironment.Require();
        var schema = NewSchema("dps_gbrain_weak_");
        var token = TestContext.Current.CancellationToken;
        try
        {
            await ExecuteAsync(
                environment.MigrationConnectionString,
                $"CREATE SCHEMA {schema}; CREATE TABLE {schema}.source_bindings (soul_id text)",
                token);
            await Assert.ThrowsAsync<PostgresSchemaIntegrityException>(() =>
                MigrateAsync(environment, schema, token));
            Assert.Equal(
                ["source_bindings"],
                await ReadTablesAsync(environment.MigrationConnectionString, schema, token));
        }
        finally
        {
            await DropSchemaAsync(environment.MigrationConnectionString, schema);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ExactSchemaRerunIsNoOpAndMigrationCredentialCannotStartRuntime()
    {
        var environment = PostgresTestEnvironment.Require();
        var schema = NewSchema("dps_gbrain_rerun_");
        var token = TestContext.Current.CancellationToken;
        try
        {
            var first = await MigrateAsync(environment, schema, token);
            var second = await MigrateAsync(environment, schema, token);
            Assert.Equal(GBrainProjectorMigrationDisposition.Created, first.Disposition);
            Assert.Equal(GBrainProjectorMigrationDisposition.VerifiedExisting, second.Disposition);
            Assert.Equal(first.RuntimeRole, second.RuntimeRole);
            await AssertExactRuntimePrivilegesAsync(
                environment.MigrationConnectionString,
                schema,
                first.RuntimeRole,
                token);

            var privilegedRuntime = new GBrainProjectorPostgresRuntime(
                new GBrainProjectorPostgresOptions(
                    environment.MigrationConnectionString,
                    schema,
                    first.MigrationRole));
            await Assert.ThrowsAsync<PostgresSchemaIntegrityException>(() =>
                privilegedRuntime.InitializeAsync(token));

            var wrongOwnerRuntime = new GBrainProjectorPostgresRuntime(
                new GBrainProjectorPostgresOptions(
                    environment.RuntimeConnectionString,
                    schema,
                    "dps_gbrain_untrusted_clone_owner"));
            await Assert.ThrowsAsync<PostgresSchemaIntegrityException>(() =>
                wrongOwnerRuntime.InitializeAsync(token));
        }
        finally
        {
            await DropSchemaAsync(environment.MigrationConnectionString, schema);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ExactSchemaWithUnexpectedViewOrStandaloneTypeIsRejectedWithoutRepair()
    {
        var environment = PostgresTestEnvironment.Require();
        var schema = NewSchema("dps_gbrain_extra_object_");
        var token = TestContext.Current.CancellationToken;
        try
        {
            await MigrateAsync(environment, schema, token);
            await ExecuteAsync(
                environment.MigrationConnectionString,
                $"CREATE VIEW {schema}.unexpected_view AS SELECT 1 AS value",
                token);
            await Assert.ThrowsAsync<PostgresSchemaIntegrityException>(() =>
                MigrateAsync(environment, schema, token));
            await ExecuteAsync(
                environment.MigrationConnectionString,
                $"DROP VIEW {schema}.unexpected_view; CREATE TYPE {schema}.unexpected_state AS ENUM ('unexpected')",
                token);
            await Assert.ThrowsAsync<PostgresSchemaIntegrityException>(() =>
                MigrateAsync(environment, schema, token));
        }
        finally
        {
            await DropSchemaAsync(environment.MigrationConnectionString, schema);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task HostileStartupSearchPathCannotShadowCatalogAttestation()
    {
        var environment = PostgresTestEnvironment.Require();
        var schema = NewSchema("dps_gbrain_safe_path_");
        var attackerSchema = NewSchema("dps_gbrain_attacker_path_");
        var token = TestContext.Current.CancellationToken;
        try
        {
            var runtimeRole = await ReadCurrentUserAsync(environment.RuntimeConnectionString, token);
            await ExecuteAsync(
                environment.MigrationConnectionString,
                $"""
                CREATE SCHEMA {attackerSchema};
                CREATE TABLE {attackerSchema}.pg_namespace (nspname name);
                GRANT USAGE ON SCHEMA {attackerSchema} TO {runtimeRole};
                GRANT SELECT ON TABLE {attackerSchema}.pg_namespace TO {runtimeRole};
                """,
                token);
            var hostileMigration = new NpgsqlConnectionStringBuilder(
                environment.MigrationConnectionString)
            {
                SearchPath = $"{attackerSchema}, pg_catalog",
                Pooling = false
            }.ConnectionString;
            var hostileRuntime = new NpgsqlConnectionStringBuilder(
                environment.RuntimeConnectionString)
            {
                SearchPath = $"{attackerSchema}, pg_catalog",
                Pooling = false
            }.ConnectionString;

            var migration = await new GBrainProjectorPostgresMigrator().ApplyAsync(
                new GBrainProjectorMigrationOptions(hostileMigration, hostileRuntime, schema),
                token);
            var runtime = new GBrainProjectorPostgresRuntime(
                new GBrainProjectorPostgresOptions(
                    hostileRuntime,
                    schema,
                    migration.MigrationRole));
            await runtime.InitializeAsync(token);
        }
        finally
        {
            await DropSchemaAsync(environment.MigrationConnectionString, schema);
            await DropSchemaAsync(environment.MigrationConnectionString, attackerSchema);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task NearExactCheckForeignKeyTriggerAndRewriteRuleAreRejected()
    {
        var environment = PostgresTestEnvironment.Require();
        var schema = NewSchema("dps_gbrain_near_exact_");
        var shadowSchema = schema + "_other";
        var token = TestContext.Current.CancellationToken;
        try
        {
            await MigrateAsync(environment, schema, token);
            await ExecuteAsync(
                environment.MigrationConnectionString,
                $"""
                ALTER TABLE {schema}.source_bindings
                    DROP CONSTRAINT source_bindings_algorithm_fixed;
                ALTER TABLE {schema}.source_bindings
                    ADD CONSTRAINT source_bindings_algorithm_fixed
                    CHECK (algorithm = 'DPS.GBRAIN-SOURCE-BINDING.SHA256-NONCE/V1');
                """,
                token);
            await Assert.ThrowsAsync<PostgresSchemaIntegrityException>(() =>
                MigrateAsync(environment, schema, token));

            await RecreateSchemaAsync(environment, schema, token);
            await ExecuteAsync(
                environment.MigrationConnectionString,
                $"""
                ALTER TABLE {schema}.source_bindings
                    DROP CONSTRAINT source_bindings_algorithm_fixed;
                ALTER TABLE {schema}.source_bindings
                    ADD CONSTRAINT source_bindings_algorithm_fixed
                    CHECK (algorithm = 'dps.gbrain-source-binding.sha256-nonce/v1::text');
                """,
                token);
            await Assert.ThrowsAsync<PostgresSchemaIntegrityException>(() =>
                MigrateAsync(environment, schema, token));

            await RecreateSchemaAsync(environment, schema, token);
            await ExecuteAsync(
                environment.MigrationConnectionString,
                $"""
                ALTER TABLE {schema}.source_bindings
                    DROP CONSTRAINT source_bindings_algorithm_fixed;
                ALTER TABLE {schema}.source_bindings
                    ADD CONSTRAINT source_bindings_algorithm_fixed
                    CHECK ((algorithm = 'dps.gbrain-source-binding.sha256-nonce/v1') =
                           (algorithm = 'dps.gbrain-source-binding.sha256-nonce/v1'));
                """,
                token);
            await Assert.ThrowsAsync<PostgresSchemaIntegrityException>(() =>
                MigrateAsync(environment, schema, token));

            await RecreateSchemaAsync(environment, schema, token);
            var internalTrigger = await ReadInternalTriggerNameAsync(
                environment.MigrationConnectionString,
                schema,
                "RI_FKey_check_ins",
                token);
            await ExecuteAsync(
                environment.MigrationConnectionString,
                $"ALTER TABLE {schema}.rendered_revisions DISABLE TRIGGER \"{internalTrigger}\"",
                token);
            await Assert.ThrowsAsync<PostgresSchemaIntegrityException>(() =>
                MigrateAsync(environment, schema, token));

            await RecreateSchemaAsync(environment, schema, token);
            await ExecuteAsync(
                environment.MigrationConnectionString,
                $"""
                CREATE SCHEMA {shadowSchema};
                CREATE TABLE {shadowSchema}.source_bindings (
                    soul_id text NOT NULL,
                    source_id text NOT NULL,
                    binding_revision character(64) NOT NULL,
                    binding_checksum character(64) NOT NULL,
                    UNIQUE (soul_id, source_id, binding_revision, binding_checksum)
                );
                ALTER TABLE {schema}.rendered_revisions
                    DROP CONSTRAINT rendered_revisions_source_binding_fkey;
                ALTER TABLE {schema}.rendered_revisions
                    ADD CONSTRAINT rendered_revisions_source_binding_fkey
                    FOREIGN KEY (soul_id, source_id, source_binding_revision, source_binding_checksum)
                    REFERENCES {shadowSchema}.source_bindings
                        (soul_id, source_id, binding_revision, binding_checksum);
                """,
                token);
            await Assert.ThrowsAsync<PostgresSchemaIntegrityException>(() =>
                MigrateAsync(environment, schema, token));
            await DropSchemaAsync(environment.MigrationConnectionString, shadowSchema);

            await RecreateSchemaAsync(environment, schema, token);
            await ExecuteAsync(
                environment.MigrationConnectionString,
                $"""
                ALTER TABLE {schema}.rendered_revisions
                    DROP CONSTRAINT rendered_revisions_source_binding_fkey;
                ALTER TABLE {schema}.rendered_revisions
                    ADD CONSTRAINT rendered_revisions_source_binding_fkey
                    FOREIGN KEY (soul_id, source_id, source_binding_revision, source_binding_checksum)
                    REFERENCES {schema}.source_bindings
                        (soul_id, source_id, binding_revision, binding_checksum)
                    ON UPDATE CASCADE;
                """,
                token);
            await Assert.ThrowsAsync<PostgresSchemaIntegrityException>(() =>
                MigrateAsync(environment, schema, token));

            await RecreateSchemaAsync(environment, schema, token);
            await ExecuteAsync(
                environment.MigrationConnectionString,
                $"""
                DROP TRIGGER source_bindings_append_only_rows ON {schema}.source_bindings;
                CREATE TRIGGER source_bindings_append_only_rows
                    BEFORE UPDATE OR DELETE ON {schema}.source_bindings
                    FOR EACH ROW WHEN (false)
                    EXECUTE FUNCTION {schema}.reject_gbrain_projector_mutation();
                """,
                token);
            await Assert.ThrowsAsync<PostgresSchemaIntegrityException>(() =>
                MigrateAsync(environment, schema, token));

            await RecreateSchemaAsync(environment, schema, token);
            await ExecuteAsync(
                environment.MigrationConnectionString,
                $"CREATE RULE unexpected_notify AS ON UPDATE TO {schema}.source_bindings DO ALSO NOTIFY dps_gbrain_projector_rule",
                token);
            await Assert.ThrowsAsync<PostgresSchemaIntegrityException>(() =>
                MigrateAsync(environment, schema, token));
        }
        finally
        {
            await DropSchemaAsync(environment.MigrationConnectionString, shadowSchema);
            await DropSchemaAsync(environment.MigrationConnectionString, schema);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RuntimeRoleMembershipInEitherDirectionIsRejected()
    {
        var environment = PostgresTestEnvironment.Require();
        var schema = NewSchema("dps_gbrain_role_graph_");
        var memberRole = NewSchema("dps_gbrain_member_");
        var token = TestContext.Current.CancellationToken;
        string? runtimeRole = null;
        try
        {
            var migration = await MigrateAsync(environment, schema, token);
            runtimeRole = migration.RuntimeRole;
            await ExecuteAsync(
                environment.MigrationConnectionString,
                $"CREATE ROLE {memberRole} NOLOGIN; GRANT {runtimeRole} TO {memberRole}",
                token);
            await Assert.ThrowsAsync<PostgresSchemaIntegrityException>(() =>
                MigrateAsync(environment, schema, token));

            await ExecuteAsync(
                environment.MigrationConnectionString,
                $"REVOKE {runtimeRole} FROM {memberRole}; GRANT {memberRole} TO {runtimeRole}",
                token);
            await Assert.ThrowsAsync<PostgresSchemaIntegrityException>(() =>
                MigrateAsync(environment, schema, token));
        }
        finally
        {
            if (runtimeRole is not null && await RoleExistsAsync(
                    environment.MigrationConnectionString,
                    memberRole,
                    CancellationToken.None))
            {
                await ExecuteAsync(
                    environment.MigrationConnectionString,
                    $"REVOKE {runtimeRole} FROM {memberRole}; REVOKE {memberRole} FROM {runtimeRole}; DROP ROLE IF EXISTS {memberRole}",
                    CancellationToken.None);
            }

            await DropSchemaAsync(environment.MigrationConnectionString, schema);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RuntimeCannotHoldOrInheritSessionReplicationRoleBypass()
    {
        var environment = PostgresTestEnvironment.Require();
        var schema = NewSchema("dps_gbrain_replication_role_");
        var token = TestContext.Current.CancellationToken;
        string? runtimeRole = null;
        try
        {
            var migration = await MigrateAsync(environment, schema, token);
            runtimeRole = migration.RuntimeRole;
            await ExecuteAsync(
                environment.MigrationConnectionString,
                $"GRANT SET ON PARAMETER session_replication_role TO {runtimeRole}",
                token);
            await Assert.ThrowsAsync<PostgresSchemaIntegrityException>(() =>
                MigrateAsync(environment, schema, token));

            await ExecuteAsync(
                environment.MigrationConnectionString,
                $"REVOKE SET ON PARAMETER session_replication_role FROM {runtimeRole}; " +
                $"ALTER ROLE {runtimeRole} SET session_replication_role = 'replica'",
                token);
            NpgsqlConnection.ClearAllPools();
            var unpooledRuntimeConnection = new NpgsqlConnectionStringBuilder(
                environment.RuntimeConnectionString)
            {
                Pooling = false
            }.ConnectionString;
            var runtime = new GBrainProjectorPostgresRuntime(
                new GBrainProjectorPostgresOptions(
                    unpooledRuntimeConnection,
                    schema,
                    migration.MigrationRole));
            await Assert.ThrowsAsync<PostgresSchemaIntegrityException>(() =>
                runtime.InitializeAsync(token));
        }
        finally
        {
            if (runtimeRole is not null)
            {
                await ExecuteAsync(
                    environment.MigrationConnectionString,
                    $"REVOKE SET ON PARAMETER session_replication_role FROM {runtimeRole}; " +
                    $"ALTER ROLE {runtimeRole} RESET session_replication_role",
                    CancellationToken.None);
                NpgsqlConnection.ClearAllPools();
            }

            await DropSchemaAsync(environment.MigrationConnectionString, schema);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SubMicrosecondProjectionTimeFailsBeforeAnyIdentityOrRevisionWrite()
    {
        var environment = PostgresTestEnvironment.Require();
        var schema = NewSchema("dps_gbrain_timestamp_");
        var token = TestContext.Current.CancellationToken;
        try
        {
            await MigrateAsync(environment, schema, token);
            var runtime = await StartRuntimeAsync(environment, schema, token);
            var eventTime = new DateTimeOffset(2026, 7, 14, 1, 0, 0, TimeSpan.Zero);
            var soul = CreateSoul('a', eventTime.AddMinutes(-1));
            var memoryEvent = CreateMemoryEvent(soul, eventTime);
            var snapshot = new DeterministicInterestReducer().Reduce(
                new InterestReductionRequest(
                    soul.SoulId,
                    soul.DeviceBindingId,
                    soul.PlatformAccountId,
                    soul.TraceId,
                    "reduce-gbrain-submicrosecond",
                    eventTime.AddDays(1).AddTicks(1),
                    [memoryEvent]),
                new InterestReducerOptions(86_400m));

            await Assert.ThrowsAsync<ProjectionInputConflictException>(() =>
                runtime.RenderAndAppendAsync([memoryEvent], snapshot, token));
            Assert.Equal(0, await CountAsync(environment.RuntimeConnectionString, schema, "source_bindings", token));
            Assert.Equal(0, await CountAsync(environment.RuntimeConnectionString, schema, "rendered_revisions", token));
        }
        finally
        {
            await DropSchemaAsync(environment.MigrationConnectionString, schema);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task BindingReadRejectsCanonicalJsonbAndRelationalProofSplits()
    {
        var environment = PostgresTestEnvironment.Require();
        var schema = NewSchema("dps_gbrain_binding_tamper_");
        var token = TestContext.Current.CancellationToken;
        try
        {
            await MigrateAsync(environment, schema, token);
            var runtime = await StartRuntimeAsync(environment, schema, token);
            var soulId = "soul_" + new string('a', 64);
            await runtime.ResolveSourceBindingAsync(soulId, token);
            await ExecuteAsync(
                environment.MigrationConnectionString,
                $"""
                ALTER TABLE {schema}.source_bindings
                    DISABLE TRIGGER source_bindings_append_only_rows;
                ALTER TABLE {schema}.source_bindings
                    DROP CONSTRAINT source_bindings_canonical_json_matches;
                UPDATE {schema}.source_bindings
                    SET canonical_json = canonical_json || ' ';
                """,
                token);
            await Assert.ThrowsAsync<SourceBindingIntegrityException>(() =>
                runtime.ResolveSourceBindingAsync(soulId, token));

            await ExecuteAsync(
                environment.MigrationConnectionString,
                $$"""
                UPDATE {{schema}}.source_bindings
                    SET canonical_json = left(canonical_json, length(canonical_json) - 1),
                        binding_json = jsonb_set(binding_json, '{nonce}', '1'::jsonb);
                """,
                token);
            await Assert.ThrowsAsync<SourceBindingIntegrityException>(() =>
                runtime.ResolveSourceBindingAsync(soulId, token));

            await ExecuteAsync(
                environment.MigrationConnectionString,
                $"""
                UPDATE {schema}.source_bindings
                    SET binding_json = canonical_json::jsonb,
                        binding_checksum = repeat('0', 64);
                """,
                token);
            await Assert.ThrowsAsync<SourceBindingIntegrityException>(() =>
                runtime.ResolveSourceBindingAsync(soulId, token));
        }
        finally
        {
            await DropSchemaAsync(environment.MigrationConnectionString, schema);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ProjectionReadRejectsJsonbAndRelationalProofSplits()
    {
        var environment = PostgresTestEnvironment.Require();
        var schema = NewSchema("dps_gbrain_projection_tamper_");
        var token = TestContext.Current.CancellationToken;
        try
        {
            await MigrateAsync(environment, schema, token);
            var runtime = await StartRuntimeAsync(environment, schema, token);
            var eventTime = new DateTimeOffset(2026, 7, 14, 1, 0, 0, TimeSpan.Zero);
            var soul = CreateSoul('a', eventTime.AddMinutes(-1));
            var memoryEvent = CreateMemoryEvent(soul, eventTime);
            var snapshot = new DeterministicInterestReducer().Reduce(
                new InterestReductionRequest(
                    soul.SoulId,
                    soul.DeviceBindingId,
                    soul.PlatformAccountId,
                    soul.TraceId,
                    "reduce-gbrain-tamper",
                    eventTime.AddDays(1),
                    [memoryEvent]),
                new InterestReducerOptions(86_400m));
            await runtime.RenderAndAppendAsync([memoryEvent], snapshot, token);

            await ExecuteAsync(
                environment.MigrationConnectionString,
                $$"""
                ALTER TABLE {{schema}}.rendered_revisions
                    DISABLE TRIGGER rendered_revisions_append_only_rows;
                ALTER TABLE {{schema}}.rendered_revisions
                    DROP CONSTRAINT rendered_revisions_canonical_json_matches;
                UPDATE {{schema}}.rendered_revisions
                    SET projection_json = jsonb_set(projection_json, '{source_event_count}', '999'::jsonb);
                """,
                token);
            await Assert.ThrowsAsync<ProjectionInputConflictException>(() =>
                runtime.RenderAndAppendAsync([memoryEvent], snapshot, token));

            await ExecuteAsync(
                environment.MigrationConnectionString,
                $"""
                UPDATE {schema}.rendered_revisions
                    SET projection_json = canonical_json::jsonb,
                        source_event_count = source_event_count + 1;
                """,
                token);
            await Assert.ThrowsAsync<ProjectionInputConflictException>(() =>
                runtime.RenderAndAppendAsync([memoryEvent], snapshot, token));
        }
        finally
        {
            await DropSchemaAsync(environment.MigrationConnectionString, schema);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task HighestEventCountWithTwoRevisionsFailsClosed()
    {
        var environment = PostgresTestEnvironment.Require();
        var schema = NewSchema("dps_gbrain_ambiguous_head_");
        var token = TestContext.Current.CancellationToken;
        try
        {
            await MigrateAsync(environment, schema, token);
            var runtime = await StartRuntimeAsync(environment, schema, token);
            var eventTime = new DateTimeOffset(2026, 7, 14, 1, 0, 0, TimeSpan.Zero);
            var soul = CreateSoul('a', eventTime.AddMinutes(-1));
            var memoryEvent = CreateMemoryEvent(soul, eventTime);
            var snapshot = new DeterministicInterestReducer().Reduce(
                new InterestReductionRequest(
                    soul.SoulId,
                    soul.DeviceBindingId,
                    soul.PlatformAccountId,
                    soul.TraceId,
                    "reduce-gbrain-ambiguous-head",
                    eventTime.AddDays(1),
                    [memoryEvent]),
                new InterestReducerOptions(86_400m));
            var first = await runtime.RenderAndAppendAsync([memoryEvent], snapshot, token);
            var withoutChecksum = first.Projection with
            {
                TraceId = "trace_" + new string('9', 32),
                IdempotencyKey = "idem_" + new string('8', 64),
                ProjectionRevision = new string('e', 64),
                ProjectionChecksum = new string('0', 64)
            };
            var ambiguous = withoutChecksum with
            {
                ProjectionChecksum = GBrainProjectionV2Canonicalizer.ComputeSha256(withoutChecksum)
            };
            ambiguous.Validate();
            await ExecuteAsync(
                environment.MigrationConnectionString,
                $"ALTER TABLE {schema}.rendered_revisions DROP CONSTRAINT rendered_revisions_soul_event_count_key",
                token);
            await InsertProjectionAsMigrationAsync(
                environment.MigrationConnectionString,
                schema,
                ambiguous,
                token);

            await Assert.ThrowsAsync<ProjectionInputConflictException>(() =>
                runtime.RenderAndAppendAsync([memoryEvent], snapshot, token));
        }
        finally
        {
            await DropSchemaAsync(environment.MigrationConnectionString, schema);
        }
    }

    private static SoulResolved CreateSoul(char marker, DateTimeOffset occurredAt) => new(
        "soul_" + new string(marker, 64),
        "db_" + new string('1', 32),
        "pa_" + new string('2', 32),
        "trace_" + new string('3', 32),
        "idem_" + new string('4', 64),
        occurredAt,
        "email",
        new string('c', 64),
        "integration-test-key-v1");

    private static MemoryEventV1 CreateMemoryEvent(SoulResolved soul, DateTimeOffset occurredAt) => new(
        MemoryEventV1.CurrentSchemaVersion,
        MemoryEventV1.CurrentContractId,
        MemoryEventV1.CurrentProducerModule,
        Guid.NewGuid(),
        soul.SoulId,
        soul.DeviceBindingId,
        soul.PlatformAccountId,
        soul.TraceId,
        "idem_5555555555555555555555555555555555555555555555555555555555555555",
        occurredAt,
        "personal",
        MemoryEventV1.ObservedContentEventType,
        new MemoryObservationV1(new string('d', 64), true, [new InterestSignalV1("deterministic systems", 0.9m)]));

    private static async Task<long> CountAsync(
        string connectionString,
        string schema,
        string table,
        CancellationToken token)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(token);
        await using var command = new NpgsqlCommand($"SELECT COUNT(*) FROM {schema}.{table}", connection);
        return (long)(await command.ExecuteScalarAsync(token)
            ?? throw new InvalidOperationException("PostgreSQL did not return a count."));
    }

    private static Task<GBrainProjectorMigrationResult> MigrateAsync(
        PostgresTestEnvironment environment,
        string schema,
        CancellationToken token) =>
        new GBrainProjectorPostgresMigrator().ApplyAsync(
            new GBrainProjectorMigrationOptions(
                environment.MigrationConnectionString,
                environment.RuntimeConnectionString,
                schema),
            token);

    private static async Task RecreateSchemaAsync(
        PostgresTestEnvironment environment,
        string schema,
        CancellationToken token)
    {
        await DropSchemaAsync(environment.MigrationConnectionString, schema);
        await MigrateAsync(environment, schema, token);
    }

    private static async Task<GBrainProjectorPostgresRuntime> StartRuntimeAsync(
        PostgresTestEnvironment environment,
        string schema,
        CancellationToken token)
    {
        var runtime = new GBrainProjectorPostgresRuntime(
            new GBrainProjectorPostgresOptions(
                environment.RuntimeConnectionString,
                schema,
                await ReadCurrentUserAsync(environment.MigrationConnectionString, token)));
        await runtime.InitializeAsync(token);
        return runtime;
    }

    private static async Task AssertAppendOnlyAndAclRejectMutations(
        PostgresTestEnvironment environment,
        string schema,
        string soulId,
        CancellationToken token)
    {
        await ExecuteAsync(
            environment.RuntimeConnectionString,
            $"""
            INSERT INTO {schema}.source_binding_quarantine
                (quarantine_id, soul_id, soul_hash, maximum_nonce, reason)
            VALUES ('{Guid.NewGuid()}', '{soulId}', '{soulId[5..]}', 1023, 'TEST_ONLY')
            """,
            token);
        var mutations = new[]
        {
            $"UPDATE {schema}.source_bindings SET nonce = nonce WHERE soul_id = '{soulId}'",
            $"DELETE FROM {schema}.source_bindings WHERE soul_id = '{soulId}'",
            $"TRUNCATE TABLE {schema}.source_bindings CASCADE",
            $"UPDATE {schema}.source_binding_quarantine SET reason = reason",
            $"DELETE FROM {schema}.source_binding_quarantine",
            $"TRUNCATE TABLE {schema}.source_binding_quarantine",
            $"UPDATE {schema}.rendered_revisions SET source_event_count = source_event_count",
            $"DELETE FROM {schema}.rendered_revisions",
            $"TRUNCATE TABLE {schema}.rendered_revisions"
        };
        foreach (var sql in mutations)
        {
            await AssertSqlStateAsync(
                environment.RuntimeConnectionString,
                sql,
                PostgresErrorCodes.InsufficientPrivilege,
                token);
            await AssertSqlStateAsync(
                environment.MigrationConnectionString,
                sql,
                PostgresErrorCodes.RaiseException,
                token);
        }
    }

    private static async Task AssertExactRuntimePrivilegesAsync(
        string migrationConnectionString,
        string schema,
        string runtimeRole,
        CancellationToken token)
    {
        await using var connection = new NpgsqlConnection(migrationConnectionString);
        await connection.OpenAsync(token);
        await using var command = new NpgsqlCommand(
            """
            SELECT
                has_schema_privilege(@role, @schema, 'USAGE'),
                has_schema_privilege(@role, @schema, 'CREATE'),
                COALESCE((
                    SELECT bool_and(
                        has_table_privilege(@role, c.oid, 'SELECT')
                        AND has_table_privilege(@role, c.oid, 'INSERT')
                        AND NOT has_table_privilege(@role, c.oid, 'UPDATE')
                        AND NOT has_table_privilege(@role, c.oid, 'DELETE')
                        AND NOT has_table_privilege(@role, c.oid, 'TRUNCATE')
                        AND NOT has_table_privilege(@role, c.oid, 'REFERENCES')
                        AND NOT has_table_privilege(@role, c.oid, 'TRIGGER')
                        AND NOT has_table_privilege(@role, c.oid, 'MAINTAIN'))
                    FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
                    WHERE n.nspname = @schema AND c.relkind IN ('r', 'p')
                ), false)
            """,
            connection);
        command.Parameters.AddWithValue("role", runtimeRole);
        command.Parameters.AddWithValue("schema", schema);
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, token);
        Assert.True(await reader.ReadAsync(token));
        Assert.True(reader.GetBoolean(0));
        Assert.False(reader.GetBoolean(1));
        Assert.True(reader.GetBoolean(2));
    }

    private static async Task AssertSqlStateAsync(
        string connectionString,
        string sql,
        string expectedSqlState,
        CancellationToken token)
    {
        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            ExecuteAsync(connectionString, sql, token));
        Assert.Equal(expectedSqlState, exception.SqlState);
    }

    private static async Task ExecuteAsync(
        string connectionString,
        string sql,
        CancellationToken token)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(token);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(token);
    }

    private static async Task InsertProjectionAsMigrationAsync(
        string connectionString,
        string schema,
        GBrainProjectionV2 projection,
        CancellationToken token)
    {
        var canonical = GBrainProjectionV2Canonicalizer.Serialize(projection);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(token);
        await using var command = new NpgsqlCommand(
            $"""
            INSERT INTO {schema}.rendered_revisions
                (soul_id, source_id, source_binding_revision, source_binding_checksum,
                 projection_revision, projection_checksum, source_event_count, occurred_at,
                 canonical_json, projection_json)
            VALUES
                (@soul_id, @source_id, @binding_revision, @binding_checksum,
                 @projection_revision, @projection_checksum, @source_event_count, @occurred_at,
                 @canonical_json, @projection_json)
            """,
            connection);
        command.Parameters.AddWithValue("soul_id", projection.SoulId);
        command.Parameters.AddWithValue("source_id", projection.SourceId);
        command.Parameters.AddWithValue("binding_revision", projection.SourceBindingRevision);
        command.Parameters.AddWithValue("binding_checksum", projection.SourceBindingChecksum);
        command.Parameters.AddWithValue("projection_revision", projection.ProjectionRevision);
        command.Parameters.AddWithValue("projection_checksum", projection.ProjectionChecksum);
        command.Parameters.AddWithValue("source_event_count", projection.SourceEventCount);
        command.Parameters.AddWithValue("occurred_at", projection.OccurredAt);
        command.Parameters.AddWithValue("canonical_json", canonical);
        command.Parameters.AddWithValue("projection_json", NpgsqlDbType.Jsonb, canonical);
        await command.ExecuteNonQueryAsync(token);
    }

    private static async Task<bool> SchemaExistsAsync(
        string connectionString,
        string schema,
        CancellationToken token)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(token);
        await using var command = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM pg_namespace WHERE nspname = @schema)",
            connection);
        command.Parameters.AddWithValue("schema", schema);
        return (bool)(await command.ExecuteScalarAsync(token)
            ?? throw new InvalidOperationException("PostgreSQL did not return schema existence truth."));
    }

    private static async Task<bool> RoleExistsAsync(
        string connectionString,
        string role,
        CancellationToken token)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(token);
        await using var command = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = @role)",
            connection);
        command.Parameters.AddWithValue("role", role);
        return (bool)(await command.ExecuteScalarAsync(token)
            ?? throw new InvalidOperationException("PostgreSQL did not return role existence truth."));
    }

    private static async Task<string> ReadCurrentUserAsync(
        string connectionString,
        CancellationToken token)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(token);
        await using var command = new NpgsqlCommand("SELECT current_user", connection);
        return (string)(await command.ExecuteScalarAsync(token)
            ?? throw new InvalidOperationException("PostgreSQL did not return the current user."));
    }

    private static async Task<string> ReadServerVersionNumberAsync(
        string connectionString,
        CancellationToken token)
    {
        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(token);
            await using var command = new NpgsqlCommand(
                "SELECT pg_catalog.current_setting('server_version_num')",
                connection);
            return (string)(await command.ExecuteScalarAsync(token)
                ?? throw new InvalidOperationException(
                    "INFRA_ERROR: the real wrong-version PostgreSQL server did not return server_version_num; the negative is NOT_RUN."));
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch
        {
            throw new InvalidOperationException(
                "INFRA_ERROR: DPS_TEST_POSTGRES_WRONG_VERSION_URI could not be opened and attested; the real wrong-version negative is NOT_RUN.");
        }
    }

    private static async Task<string> ReadInternalTriggerNameAsync(
        string connectionString,
        string schema,
        string functionName,
        CancellationToken token)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(token);
        await using var command = new NpgsqlCommand(
            """
            SELECT trigger.tgname
            FROM pg_catalog.pg_trigger trigger
            JOIN pg_catalog.pg_class relation ON relation.oid = trigger.tgrelid
            JOIN pg_catalog.pg_namespace namespace ON namespace.oid = relation.relnamespace
            JOIN pg_catalog.pg_proc function ON function.oid = trigger.tgfoid
            WHERE namespace.nspname = @schema
              AND trigger.tgisinternal
              AND function.proname = @function_name
            """,
            connection);
        command.Parameters.AddWithValue("schema", schema);
        command.Parameters.AddWithValue("function_name", functionName);
        var name = (string)(await command.ExecuteScalarAsync(token)
            ?? throw new InvalidOperationException("PostgreSQL did not return the expected internal FK trigger."));
        if (!name.StartsWith("RI_ConstraintTrigger_", StringComparison.Ordinal) ||
            name.Any(static character => !(char.IsAsciiLetterOrDigit(character) || character == '_')))
        {
            throw new InvalidOperationException("PostgreSQL returned an unsafe internal FK trigger name.");
        }

        return name;
    }

    private static async Task<string[]> ReadTablesAsync(
        string connectionString,
        string schema,
        CancellationToken token)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(token);
        await using var command = new NpgsqlCommand(
            """
            SELECT c.relname
            FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = @schema AND c.relkind IN ('r', 'p')
            ORDER BY c.relname
            """,
            connection);
        command.Parameters.AddWithValue("schema", schema);
        await using var reader = await command.ExecuteReaderAsync(token);
        var tables = new List<string>();
        while (await reader.ReadAsync(token))
        {
            tables.Add(reader.GetString(0));
        }

        return [.. tables];
    }

    private static async Task DropSchemaAsync(string? connectionString, string schemaName)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return;
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using var command = new NpgsqlCommand($"DROP SCHEMA IF EXISTS {schemaName} CASCADE", connection);
        await command.ExecuteNonQueryAsync(CancellationToken.None);
    }

    private static async Task DropDatabaseAsync(string? connectionString, string databaseName)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return;
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(CancellationToken.None);
        await using var command = new NpgsqlCommand(
            $"DROP DATABASE IF EXISTS {databaseName} WITH (FORCE)",
            connection);
        await command.ExecuteNonQueryAsync(CancellationToken.None);
    }

    private static string NewSchema(string prefix) => prefix + Guid.NewGuid().ToString("N");

    private sealed record PostgresTestEnvironment(
        string MigrationConnectionString,
        string RuntimeConnectionString)
    {
        internal static PostgresTestEnvironment Require()
        {
            var migration = Environment.GetEnvironmentVariable("DPS_TEST_POSTGRES_ADMIN_URI");
            var runtime = Environment.GetEnvironmentVariable("DPS_TEST_POSTGRES_RUNTIME_URI");
            if (string.IsNullOrWhiteSpace(migration) || string.IsNullOrWhiteSpace(runtime))
            {
                throw new InvalidOperationException(
                    "INFRA_ERROR: DPS_TEST_POSTGRES_ADMIN_URI and DPS_TEST_POSTGRES_RUNTIME_URI are both required; missing real PostgreSQL role-separation evidence is NOT_RUN, never a mock PASS.");
            }

            return new PostgresTestEnvironment(migration, runtime);
        }

        internal static string RequireWrongVersionConnectionString()
        {
            var wrongVersion = Environment.GetEnvironmentVariable("DPS_TEST_POSTGRES_WRONG_VERSION_URI");
            if (string.IsNullOrWhiteSpace(wrongVersion))
            {
                throw new InvalidOperationException(
                    "INFRA_ERROR: DPS_TEST_POSTGRES_WRONG_VERSION_URI is required for a real non-18.4 PostgreSQL negative; missing evidence is NOT_RUN, never a mock PASS.");
            }

            return wrongVersion;
        }
    }
}
