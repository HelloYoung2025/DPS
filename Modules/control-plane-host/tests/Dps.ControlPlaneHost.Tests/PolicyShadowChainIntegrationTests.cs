using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Dps.Planner.Contracts;
using Dps.PolicyApproval.Contracts;
using Dps.SoulRegistry.Contracts;
using Dps.SoulRegistry.Tests;
using Npgsql;
using Xunit;

namespace Dps.ControlPlaneHost.Tests;

/// <summary>
/// REAL_POSTGRESQL shadow chain: fixture-gated soul.current → test-only entry process
/// (real Planner → contract-only Host chain) → signed approval.evaluation.request/v1 →
/// Policy → exact approval.readback/v1. This project references contract packs only; every
/// Planner/Policy implementation call runs in that entry process. Every test owns a
/// disposable database, so a whole-database row snapshot proves the side-effect budget.
/// Synthetic keys live only in memory and reach the child only through stdin.
/// </summary>
[SupportedOSPlatform("macos")]
public sealed class PolicyShadowChainIntegrationTests
{
    private static readonly string ReleaseBom = new('b', 64);
    private static readonly string[] DecisionTables =
        ["approval_decisions", "approval_status_revisions", "approval_idempotency_receipts", "approval_outbox"];

    [Fact, Trait("Category", "Integration")]
    public async Task RealPlannerOutputFlowsThroughSignedShadowRequestIntoExactReadbackAndReplaysExactly()
    {
        if (!OperatingSystem.IsMacOS()) throw new PlatformNotSupportedException();
        var token = TestContext.Current.CancellationToken;
        await using var soul = await SoulCurrentProcessFixture.CreateAsync(token);
        var identity = await ReadActiveSoulAsync(soul, token);
        var resolved = SoulResolvedCanonicalV1.FromUtf8Json(Convert.FromBase64String(identity));
        await using var chain = await ShadowChainDatabase.CreateAsync(token);
        await chain.AppendRuntimeStateAsync(resolved.SoulId, resolved.DeviceBindingId, resolved.PlatformAccountId, token);
        var input = Input(DateTimeOffset.UtcNow.AddSeconds(-1));
        var before = await chain.SnapshotAsync(token);

        var first = await chain.RunAsync(Plan(identity, input), token);
        Assert.True(first.Exit == 0, first.Output);
        var wire = Evaluated(first.Output);
        var readback = ApprovalReadbackV1Codec.Deserialize(wire);

        // Independently recompute, from the Planner contract, what the real Planner in the
        // entry process must have produced and the Host must have bound.
        var proposal = ControlPlaneTruthStoreTests.ExpectedProposal(
            resolved.SoulId, resolved.DeviceBindingId, resolved.PlatformAccountId,
            (string)input["trace_id"]!, (string)input["idempotency_key"]!,
            DateTimeOffset.Parse((string)input["occurred_at"]!).ToUniversalTime());
        var proposalSha256 = ActionProposalV2Canonical.ComputeSha256(proposal);
        Assert.Equal(ApprovalDecisionV1.Denied, readback.Approval.Decision);
        Assert.True(readback.Approval.ShadowOnly);
        Assert.Contains("SHADOW_ONLY", readback.Approval.DenialReasons);
        Assert.Equal(2, readback.ProposalContractMajor);
        Assert.Equal(proposalSha256, readback.ProposalSha256);
        Assert.Equal(proposal.ProposalId, readback.Approval.ProposalId);
        Assert.Equal(ReleaseBom, readback.ReleaseBomSha256);
        var expectedRequest = new ApprovalEvaluationRequestV1(
            ApprovalEvaluationRequestV1.CurrentSchemaVersion, ApprovalEvaluationRequestV1.CurrentContractId,
            ApprovalEvaluationRequestV1.CurrentProducerModule, ApprovalEvaluationRequestV1.CurrentAuthScope,
            proposal.ProposalId, proposalSha256, proposal.SoulId, proposal.DeviceBindingId, proposal.PlatformAccountId,
            proposal.TraceId, proposal.IdempotencyKey, ReleaseBom, ApprovalEvaluationRequestV1.Shadow, null,
            proposal.OccurredAt, proposal.OccurredAt + ApprovalEvaluationRequestV1.Lifetime, "internal",
            readback.RequestSignature!);
        Assert.True(chain.Evaluation.VerifyData(
            ApprovalEvaluationRequestV1Canonical.CanonicalBytes(expectedRequest),
            Convert.FromBase64String(readback.RequestSignature!),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
        // The exact reader keys on approval_sha256 and re-verifies it against the payload.
        Assert.Equal(wire, await chain.ReadAsync(readback, token));

        var afterFirst = await chain.SnapshotAsync(token);
        AssertOnlyDecisionRowsAdded(chain, before, afterFirst, 1);

        // Same content, fresh ECDSA signature (new process): a replay, not a conflict.
        var replay = await chain.RunAsync(Plan(identity, input), token);
        Assert.True(replay.Exit == 0, replay.Output);
        Assert.Equal(wire, Evaluated(replay.Output));
        Assert.Equal(afterFirst, await chain.SnapshotAsync(token));
    }

    [Fact, Trait("Category", "Integration")]
    public async Task RelaySeventhFractionalDigitIsEvaluatedOnFirstCallAndReplaysExactly()
    {
        var token = TestContext.Current.CancellationToken;
        await using var chain = await ShadowChainDatabase.CreateAsync(token);
        var (soulId, device, account) = Scope("digit7-relay");
        await chain.AppendRuntimeStateAsync(soulId, device, account, token);
        var occurredAt = SeventhDigit(1234567);
        await AssertSeventhDigitEvaluatedOnFirstCallAsync(
            chain, Relay(PlannerWire(soulId, device, account, "digit7-relay", occurredAt)), occurredAt, token);
    }

    [Fact, Trait("Category", "Integration")]
    public async Task PlanSeventhFractionalDigitIsEvaluatedOnFirstCallAndReplaysExactly()
    {
        if (!OperatingSystem.IsMacOS()) throw new PlatformNotSupportedException();
        var token = TestContext.Current.CancellationToken;
        await using var soul = await SoulCurrentProcessFixture.CreateAsync(token);
        var identity = await ReadActiveSoulAsync(soul, token);
        var resolved = SoulResolvedCanonicalV1.FromUtf8Json(Convert.FromBase64String(identity));
        await using var chain = await ShadowChainDatabase.CreateAsync(token);
        await chain.AppendRuntimeStateAsync(resolved.SoulId, resolved.DeviceBindingId, resolved.PlatformAccountId, token);
        var occurredAt = SeventhDigit(3456787);
        await AssertSeventhDigitEvaluatedOnFirstCallAsync(chain, Plan(identity, Input(occurredAt)), occurredAt, token);
    }

    [Fact, Trait("Category", "Integration")]
    public async Task RejectedOrForgedIdentityNeverReachesPlannerOrPolicy()
    {
        if (!OperatingSystem.IsMacOS()) throw new PlatformNotSupportedException();
        var token = TestContext.Current.CancellationToken;
        await using var soul = await SoulCurrentProcessFixture.CreateAsync(token);
        var otherSoul = await soul.ReadHostAsync(
            soul.Query with { SoulId = "soul_" + new string('9', 64) }, token);
        Assert.NotEqual(0, otherSoul.Exit);
        Assert.StartsWith("REJECTED:", otherSoul.Output, StringComparison.Ordinal);

        await using var chain = await ShadowChainDatabase.CreateAsync(token);
        var before = await chain.SnapshotAsync(token);
        // A body-constructed identity is not a fixture-gated SOUL_ACTIVE_AS_OF wire.
        var forged = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"soul_id\":\"soul_" + new string('1', 64) + "\"}"));
        var result = await chain.RunAsync(Plan(forged, Input(DateTimeOffset.UtcNow.AddSeconds(-1))), token);
        Assert.Equal(1, result.Exit);
        Assert.StartsWith("REJECTED:", result.Output, StringComparison.Ordinal);
        Assert.Equal(before, await chain.SnapshotAsync(token));
    }

    [Fact, Trait("Category", "Integration")]
    public async Task CrossScopeRelayAndReadbackFailClosedWithZeroWrites()
    {
        var token = TestContext.Current.CancellationToken;
        await using var chain = await ShadowChainDatabase.CreateAsync(token);
        var (soulId, deviceA, accountA) = Scope("a");
        var (_, deviceB, accountB) = Scope("b");
        await chain.AppendRuntimeStateAsync(soulId, deviceA, accountA, token);
        var issued = await chain.RunAsync(Relay(PlannerWire(soulId, deviceA, accountA, "cross-a")), token);
        Assert.True(issued.Exit == 0, issued.Output);
        var readback = ApprovalReadbackV1Codec.Deserialize(Evaluated(issued.Output));
        var before = await chain.SnapshotAsync(token);

        // Same Soul, other device/account: no runtime truth for that exact scope.
        var crossed = await chain.RunAsync(Relay(PlannerWire(soulId, deviceB, accountB, "cross-b")), token);
        Assert.Equal(1, crossed.Exit);
        Assert.StartsWith("REJECTED:", crossed.Output, StringComparison.Ordinal);
        var crossRead = await chain.RunAsync(ReadRequest(
            readback with { Approval = readback.Approval with { DeviceBindingId = deviceB, PlatformAccountId = accountB } }), token);
        Assert.Equal((1, "REJECTED:UnauthorizedAccessException"), crossRead);
        Assert.Equal(before, await chain.SnapshotAsync(token));
    }

    [Fact, Trait("Category", "Integration")]
    public async Task ExpiredPlannerOutputWritesNothing()
    {
        var token = TestContext.Current.CancellationToken;
        await using var chain = await ShadowChainDatabase.CreateAsync(token);
        var (soulId, device, account) = Scope("expired");
        await chain.AppendRuntimeStateAsync(soulId, device, account, token);
        var before = await chain.SnapshotAsync(token);
        var expired = await chain.RunAsync(
            Relay(PlannerWire(soulId, device, account, "expired", DateTimeOffset.UtcNow.AddMinutes(-10))),
            token);
        Assert.Equal(1, expired.Exit);
        Assert.StartsWith("REJECTED:", expired.Output, StringComparison.Ordinal);
        Assert.Equal(before, await chain.SnapshotAsync(token));
    }

    [Fact, Trait("Category", "Integration")]
    public async Task UnknownMajorAndV1WiresFailClosedBeforeSigningWithZeroWrites()
    {
        var token = TestContext.Current.CancellationToken;
        await using var chain = await ShadowChainDatabase.CreateAsync(token);
        var (soulId, device, account) = Scope("major");
        await chain.AppendRuntimeStateAsync(soulId, device, account, token);
        var before = await chain.SnapshotAsync(token);
        var v2 = Encoding.UTF8.GetString(PlannerWire(soulId, device, account, "major"));
        var unknown = Encoding.UTF8.GetBytes(v2.Replace(
            "\"contract_id\":\"action.proposal/v2\"", "\"contract_id\":\"action.proposal/v3\"", StringComparison.Ordinal));
        var v1 = ActionProposalV1Json.Serialize(LegacyProposal(soulId, device, account, "major"));
        foreach (var wire in new[] { unknown, v1 })
        {
            var result = await chain.RunAsync(Relay(wire), token);
            Assert.Equal(1, result.Exit);
            Assert.StartsWith("REJECTED:", result.Output, StringComparison.Ordinal);
        }
        Assert.Equal(before, await chain.SnapshotAsync(token));
    }

    [Fact, Trait("Category", "Integration")]
    public async Task LegallySignedV1AndV1BoundPromotionStayQuarantinedOnTheChainDatabase()
    {
        var token = TestContext.Current.CancellationToken;
        await using var chain = await ShadowChainDatabase.CreateAsync(token);
        var (soulId, device, account) = Scope("v1");
        await chain.AppendRuntimeStateAsync(soulId, device, account, token);
        var before = await chain.SnapshotAsync(token);

        // An authentic EXECUTE request and promotion bound to the v1 commitment.
        var legacy = LegacyProposal(soulId, device, account, "v1");
        var legacySha256 = ActionProposalCanonical.ComputeSha256(legacy);
        var legacyPromotion = chain.SignPromotion(Promotion(legacy.ProposalId, soulId, device, account,
            legacy.TraceId, legacy.IdempotencyKey, legacySha256));
        var legacyRequest = chain.SignRequest(ExecuteRequest(
            legacy.ProposalId, legacySha256, soulId, device, account, legacy.TraceId, legacy.IdempotencyKey,
            legacy.OccurredAt, legacyPromotion));
        Assert.Equal((5, "QUARANTINED:1:QUARANTINE"), await chain.RunAsync(
            PolicyEvaluate(ActionProposalV1Json.Serialize(legacy), legacyRequest, legacyPromotion), token));

        // A v2 proposal whose promotion binds the v1 hash is not authority either.
        var proposal = ActionProposalV2Json.Deserialize(PlannerWire(soulId, device, account, "v1-bound"));
        var v1BoundPromotion = chain.SignPromotion(Promotion(proposal.ProposalId, soulId, device, account,
            proposal.TraceId, proposal.IdempotencyKey, legacySha256));
        var v1BoundRequest = chain.SignRequest(ExecuteRequest(
            proposal.ProposalId, ActionProposalV2Canonical.ComputeSha256(proposal), soulId, device, account,
            proposal.TraceId, proposal.IdempotencyKey, proposal.OccurredAt, v1BoundPromotion));
        var v1Bound = await chain.RunAsync(
            PolicyEvaluate(ActionProposalV2Json.Serialize(proposal), v1BoundRequest, v1BoundPromotion), token);
        Assert.NotEqual(0, v1Bound.Exit);
        Assert.DoesNotContain("EVALUATED", v1Bound.Output, StringComparison.Ordinal);
        Assert.Equal(before, await chain.SnapshotAsync(token));
    }

    [Fact, Trait("Category", "Integration")]
    public async Task KillControlPlaneIngestStopsTheChainWithoutReleasingV1OrDeletingHistory()
    {
        var token = TestContext.Current.CancellationToken;
        await using var chain = await ShadowChainDatabase.CreateAsync(token);
        var (soulId, device, account) = Scope("kill");
        await chain.AppendRuntimeStateAsync(soulId, device, account, token);
        var issued = await chain.RunAsync(Relay(PlannerWire(soulId, device, account, "kill-before")), token);
        Assert.True(issued.Exit == 0, issued.Output);
        var readback = ApprovalReadbackV1Codec.Deserialize(Evaluated(issued.Output));
        var before = await chain.SnapshotAsync(token);

        var v1 = ActionProposalV1Json.Serialize(LegacyProposal(soulId, device, account, "kill-v1"));
        foreach (var wire in new[] { PlannerWire(soulId, device, account, "kill-after"), v1 })
        {
            var killed = await chain.RunAsync(Relay(wire, kill: true), token);
            Assert.Equal(4, killed.Exit);
            Assert.Equal("KILLED", killed.Output);
        }
        // Omitting the switch is also closed: the composition defaults to killed.
        var defaulted = await chain.RunAsync(Relay(PlannerWire(soulId, device, account, "kill-default"), kill: null), token);
        Assert.Equal(4, defaulted.Exit);
        Assert.Equal(before, await chain.SnapshotAsync(token));
        // History is retained and still exactly readable; v1 stays quarantined at Policy.
        Assert.Equal(ApprovalReadbackV1Codec.Serialize(readback), await chain.ReadAsync(readback, token));
        Assert.Equal((5, "QUARANTINED:1:QUARANTINE"), await chain.RunAsync(PolicyEvaluate(
            v1, chain.SignRequest(ExecuteRequest(
                Guid.NewGuid(), new string('c', 64), soulId, device, account, "trace_" + new string('3', 32),
                "idem_" + new string('4', 64), DateTimeOffset.UtcNow, null)), null), token));
        Assert.Equal(before, await chain.SnapshotAsync(token));
    }

    /// <summary>Whole second − 3 s plus a sub-second tick count whose 100 ns digit is non-zero; independent of clock precision.</summary>
    private static DateTimeOffset SeventhDigit(long ticks)
    {
        var now = DateTimeOffset.UtcNow;
        var occurredAt = new DateTimeOffset(now.Ticks - now.Ticks % TimeSpan.TicksPerSecond, TimeSpan.Zero).AddSeconds(-3).AddTicks(ticks);
        Assert.NotEqual(0, occurredAt.Ticks % 10);
        return occurredAt;
    }

    /// <summary>
    /// The first call is EVALUATED with exactly one row in each decision table, equals the exact
    /// reader, carries the stored (µs-truncated) valid_until, and an exact replay adds nothing.
    /// </summary>
    private static async Task AssertSeventhDigitEvaluatedOnFirstCallAsync(
        ShadowChainDatabase chain, JsonObject request, DateTimeOffset occurredAt, CancellationToken token)
    {
        var before = await chain.SnapshotAsync(token);
        var first = await chain.RunAsync(request, token);
        Assert.True(first.Exit == 0, first.Output);
        var wire = Evaluated(first.Output);
        var afterFirst = await chain.SnapshotAsync(token);
        AssertOnlyDecisionRowsAdded(chain, before, afterFirst, 1);
        var readback = ApprovalReadbackV1Codec.Deserialize(wire);
        Assert.Equal(wire, await chain.ReadAsync(readback, token));
        var bound = occurredAt + ApprovalEvaluationRequestV1.Lifetime;
        Assert.Equal(new DateTimeOffset(bound.Ticks - bound.Ticks % 10, TimeSpan.Zero), readback.ValidUntil);

        var replay = await chain.RunAsync(request, token);
        Assert.True(replay.Exit == 0, replay.Output);
        Assert.Equal(wire, Evaluated(replay.Output));
        Assert.Equal(afterFirst, await chain.SnapshotAsync(token));
        Assert.Equal(wire, await chain.ReadAsync(readback, token));
    }

    private static async Task<string> ReadActiveSoulAsync(SoulCurrentProcessFixture soul, CancellationToken token)
    {
        var result = await soul.ReadHostAsync(soul.Query, token);
        Assert.True(result.Exit == 0, result.Output);
        Assert.StartsWith("SOUL_ACTIVE_AS_OF:", result.Output, StringComparison.Ordinal);
        return result.Output["SOUL_ACTIVE_AS_OF:".Length..];
    }

    private static void AssertOnlyDecisionRowsAdded(
        ShadowChainDatabase chain,
        IReadOnlyDictionary<string, long> before,
        IReadOnlyDictionary<string, long> after,
        long added)
    {
        // Structural: the chain database has only Policy tables, so there is no
        // command/lease/attempt table outside Policy that could hold a row.
        Assert.All(after.Keys, key => Assert.StartsWith(chain.SchemaName + ".", key, StringComparison.Ordinal));
        Assert.Equal(before.Keys.Order(StringComparer.Ordinal), after.Keys.Order(StringComparer.Ordinal));
        foreach (var (table, count) in after)
        {
            var expected = before[table] + (DecisionTables.Any(name => table == chain.SchemaName + "." + name) ? added : 0);
            Assert.True(expected == count, $"{table}: expected {expected}, observed {count}");
        }
        foreach (var zero in new[]
                 {
                     "policy_rate_consumptions", "approval_idempotency_quarantine", "approval_submission_attempts",
                     "approval_submission_acknowledgements", "approval_submission_quarantines",
                     "approval_submission_reconciliations", "approval_submission_recoveries"
                 })
            Assert.Equal(0, after[chain.SchemaName + "." + zero]);
    }

    private static JsonObject Input(DateTimeOffset occurredAt)
    {
        var suffix = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        return new JsonObject
        {
            ["trace_id"] = "trace_" + suffix[..32],
            ["idempotency_key"] = "idem_" + suffix,
            ["occurred_at"] = occurredAt.UtcDateTime.ToString("O"),
            ["proposer_kind"] = "deterministic-planner",
            ["action_kind"] = "observe",
            ["parameters"] = new JsonObject(),
            ["evidence_refs"] = new JsonArray("evidence_" + new string('e', 64))
        };
    }

    private static JsonObject Plan(string soulResolvedWire, JsonObject input)
        => new() { ["mode"] = "plan", ["soul_resolved_wire"] = soulResolvedWire, ["input"] = input.DeepClone(), ["kill_control_plane_ingest"] = false };

    private static JsonObject Relay(byte[] proposalWire, bool? kill = false)
    {
        var request = new JsonObject { ["mode"] = "relay", ["proposal_wire"] = Convert.ToBase64String(proposalWire) };
        if (kill is not null) request["kill_control_plane_ingest"] = kill.Value;
        return request;
    }

    private static JsonObject PolicyEvaluate(byte[] proposalWire, ApprovalEvaluationRequestV1 request, ActionExecutionPromotionV1? promotion)
    {
        var body = new JsonObject
        {
            ["mode"] = "policy_evaluate",
            ["proposal_wire"] = Convert.ToBase64String(proposalWire),
            ["request_wire"] = Convert.ToBase64String(ApprovalEvaluationRequestV1Codec.Serialize(request))
        };
        if (promotion is not null) body["promotion_wire"] = Convert.ToBase64String(ActionExecutionPromotionV1Codec.Serialize(promotion));
        return body;
    }

    private static JsonObject ReadRequest(ApprovalReadbackV1 readback)
        => new()
        {
            ["mode"] = "read",
            ["read_request"] = new JsonObject
            {
                ["approval_id"] = readback.Approval.ApprovalId.ToString(),
                ["proposal_id"] = readback.Approval.ProposalId.ToString(),
                ["soul_id"] = readback.Approval.SoulId,
                ["device_binding_id"] = readback.Approval.DeviceBindingId,
                ["platform_account_id"] = readback.Approval.PlatformAccountId,
                ["trace_id"] = readback.Approval.TraceId,
                ["idempotency_key"] = readback.Approval.IdempotencyKey,
                ["approval_sha256"] = readback.ApprovalSha256
            }
        };

    private static byte[] Evaluated(string output) => Prefixed("EVALUATED:", output);

    private static byte[] Prefixed(string prefix, string output)
    {
        Assert.StartsWith(prefix, output, StringComparison.Ordinal);
        return Convert.FromBase64String(output[prefix.Length..]);
    }


    private static (string Soul, string Device, string Account) Scope(string label)
    {
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("p06-s2-" + label)));
        return ("soul_" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("p06-s2-soul"))),
            "db_" + hash[..32], "pa_" + hash[32..]);
    }

    private static byte[] PlannerWire(string soulId, string device, string account, string label, DateTimeOffset? occurredAt = null)
    {
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("p06-s2-idem-" + label)));
        return ActionProposalV2Json.Serialize(ControlPlaneTruthStoreTests.ExpectedProposal(
            soulId, device, account, "trace_" + hash[..32], "idem_" + hash, occurredAt ?? DateTimeOffset.UtcNow.AddSeconds(-1)));
    }

    private static ActionProposalV1 LegacyProposal(string soulId, string device, string account, string label)
    {
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("p06-s2-legacy-" + label)));
        var idempotencyKey = "idem_" + hash;
        return new ActionProposalV1(
            ActionProposalV1.CurrentSchemaVersion, ActionProposalV1.CurrentContractId, ActionProposalV1.CurrentProducerModule,
            ActionProposalIdentity.Create(soulId, device, account, idempotencyKey),
            soulId, device, account, "trace_" + hash[..32], idempotencyKey, DateTimeOffset.UtcNow.AddSeconds(-1),
            "internal", "observe", false, true, new Dictionary<string, string>(), ["evidence:authorized-fixture"]);
    }

    private static ActionExecutionPromotionV1 Promotion(
        Guid proposalId, string soulId, string device, string account, string traceId, string idempotencyKey, string proposalSha256)
    {
        var promotedAt = DateTimeOffset.UtcNow;
        return new ActionExecutionPromotionV1(
            ActionExecutionPromotionV1.CurrentSchemaVersion, ActionExecutionPromotionV1.CurrentContractId,
            ActionExecutionPromotionV1.CurrentProducerModule, ActionExecutionPromotionV1.CurrentAuthScope,
            Guid.NewGuid(), proposalId, Guid.NewGuid(), soulId, device, account, traceId, idempotencyKey,
            proposalSha256, ReleaseBom, 1, promotedAt, promotedAt.AddMinutes(5), "internal",
            Convert.ToBase64String(new byte[64]));
    }

    private static ApprovalEvaluationRequestV1 ExecuteRequest(
        Guid proposalId, string proposalSha256, string soulId, string device, string account,
        string traceId, string idempotencyKey, DateTimeOffset occurredAt, ActionExecutionPromotionV1? promotion)
        => new(
            ApprovalEvaluationRequestV1.CurrentSchemaVersion, ApprovalEvaluationRequestV1.CurrentContractId,
            ApprovalEvaluationRequestV1.CurrentProducerModule, ApprovalEvaluationRequestV1.CurrentAuthScope,
            proposalId, proposalSha256, soulId, device, account, traceId, idempotencyKey, ReleaseBom,
            promotion is null ? ApprovalEvaluationRequestV1.Shadow : ApprovalEvaluationRequestV1.Execute,
            promotion is null ? null : ApprovalEvaluationRequestV1Canonical.ComputeExecutionPromotionSha256(promotion),
            occurredAt, occurredAt + ApprovalEvaluationRequestV1.Lifetime, "internal", Convert.ToBase64String(new byte[64]));

    /// <summary>One disposable database with the product Policy migration and four login roles.</summary>
    private sealed class ShadowChainDatabase : IAsyncDisposable
    {
        private readonly string _admin;
        private readonly string _database;
        private readonly string[] _roles;
        private readonly string _runtimeConnection;
        private readonly ECDsa[] _authorities;

        private ShadowChainDatabase(string admin, string database, string schema, string[] roles, string runtimeConnection)
        {
            _admin = admin;
            _database = database;
            SchemaName = schema;
            _roles = roles;
            _runtimeConnection = runtimeConnection;
            _authorities = Enumerable.Range(0, 8).Select(_ => ECDsa.Create(ECCurve.NamedCurves.nistP256)).ToArray();
        }

        public string SchemaName { get; }
        public ECDsa Evaluation => _authorities[0];
        private ECDsa PromotionSigner => _authorities[1];
        private string DatabaseAdmin => new NpgsqlConnectionStringBuilder(_admin) { Database = _database, Pooling = false }.ConnectionString;

        public static async Task<ShadowChainDatabase> CreateAsync(CancellationToken token)
        {
            var admin = Environment.GetEnvironmentVariable("DPS_TEST_POSTGRES");
            if (string.IsNullOrWhiteSpace(admin))
                throw new InvalidOperationException("INFRA_ERROR: DPS_TEST_POSTGRES is required; the shadow chain never skips or mocks PostgreSQL.");
            var settings = new NpgsqlConnectionStringBuilder(admin);
            if (settings.Port == 55434 || settings.Database == "dps_gbrain_company")
                throw new InvalidOperationException("The shadow chain refuses PostgreSQL 55434 and the GBrain Company database.");
            var suffix = Guid.NewGuid().ToString("N");
            var database = "p06_chain_" + suffix;
            string[] roles = ["p06_runtime_" + suffix, "p06_submit_" + suffix, "p06_reconcile_" + suffix, "p06_recover_" + suffix];
            var runtimePassword = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
            await using (var connection = new NpgsqlConnection(admin))
            {
                await connection.OpenAsync(token);
                await using var version = new NpgsqlCommand("SHOW server_version_num", connection) { CommandTimeout = 5 };
                if (await version.ExecuteScalarAsync(token) is not "180004")
                    throw new InvalidOperationException("INFRA_ERROR: PostgreSQL 18.4 is required.");
                await using var create = new NpgsqlCommand($"CREATE DATABASE {database}", connection) { CommandTimeout = 10 };
                await create.ExecuteNonQueryAsync(token);
                foreach (var role in roles)
                {
                    // Names and passwords are locally generated hex, never caller values.
                    var password = role == roles[0] ? runtimePassword : Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
                    await using var createRole = new NpgsqlCommand(
                        $"CREATE ROLE {role} LOGIN PASSWORD '{password}' NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS",
                        connection) { CommandTimeout = 5 };
                    await createRole.ExecuteNonQueryAsync(token);
                }
            }
            var runtime = new NpgsqlConnectionStringBuilder(admin)
            {
                Database = database, Username = roles[0], Password = runtimePassword, Passfile = null,
                Pooling = false, Timeout = 5, CommandTimeout = 5
            }.ConnectionString;
            var chain = new ShadowChainDatabase(admin, database, "dps_policy_approval_" + suffix, roles, runtime);
            try
            {
                var setup = await chain.RunAsync(
                    new JsonObject { ["mode"] = "setup", ["roles"] = new JsonArray(roles.Select(role => (JsonNode)role).ToArray()) },
                    token,
                    owner: true);
                Assert.True(setup == (0, "MIGRATED"), setup.Output);
                return chain;
            }
            catch
            {
                await chain.DisposeAsync();
                throw;
            }
        }

        /// <summary>Declared configuration input (D6): a synthetic runtime revision written with the owner connection.</summary>
        public async Task AppendRuntimeStateAsync(string soulId, string device, string account, CancellationToken token)
        {
            var appended = await RunAsync(
                new JsonObject
                {
                    ["mode"] = "append_state", ["soul_id"] = soulId, ["device_binding_id"] = device,
                    ["platform_account_id"] = account, ["release_bom_sha256"] = ReleaseBom
                },
                token,
                owner: true);
            Assert.True(appended == (0, "APPENDED"), appended.Output);
        }

        /// <summary>Row count of every base table in every non-system schema of this database.</summary>
        public async Task<IReadOnlyDictionary<string, long>> SnapshotAsync(CancellationToken token)
        {
            await using var connection = new NpgsqlConnection(DatabaseAdmin);
            await connection.OpenAsync(token);
            var tables = new List<string>();
            await using (var list = new NpgsqlCommand(
                """
                SELECT quote_ident(table_schema) || '.' || quote_ident(table_name)
                FROM information_schema.tables
                WHERE table_type = 'BASE TABLE' AND table_schema NOT IN ('pg_catalog', 'information_schema')
                ORDER BY 1
                """, connection) { CommandTimeout = 5 })
            await using (var reader = await list.ExecuteReaderAsync(token))
                while (await reader.ReadAsync(token)) tables.Add(reader.GetString(0));
            var counts = new SortedDictionary<string, long>(StringComparer.Ordinal);
            foreach (var table in tables)
            {
                await using var count = new NpgsqlCommand($"SELECT count(*) FROM {table}", connection) { CommandTimeout = 5 };
                counts.Add(table, (long)(await count.ExecuteScalarAsync(token))!);
            }
            Assert.NotEmpty(counts);
            return counts;
        }

        /// <summary>Policy's exact reader, run in the entry process; returns the readback wire.</summary>
        public async Task<byte[]> ReadAsync(ApprovalReadbackV1 readback, CancellationToken token)
        {
            var read = await RunAsync(ReadRequest(readback), token);
            Assert.True(read.Exit == 0, read.Output);
            return Prefixed("READ:", read.Output);
        }

        public ApprovalEvaluationRequestV1 SignRequest(ApprovalEvaluationRequestV1 unsigned)
            => unsigned with { SignatureBase64 = Sign(Evaluation, ApprovalEvaluationRequestV1Canonical.CanonicalBytes(unsigned)) };

        public ActionExecutionPromotionV1 SignPromotion(ActionExecutionPromotionV1 unsigned)
            => unsigned with { SignatureBase64 = Sign(PromotionSigner, ActionExecutionPromotionV1Canonical.CanonicalBytes(unsigned)) };

        private static string Sign(ECDsa signer, byte[] canonical)
            => Convert.ToBase64String(signer.SignData(canonical, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));

        /// <summary>
        /// Runs the entry process. Chain modes get only the runtime login and never the
        /// bootstrap credential; <paramref name="owner"/> fixture modes (migration, D6 runtime
        /// state) get the test-database owner connection instead.
        /// </summary>
        public async Task<(int Exit, string Output)> RunAsync(JsonObject request, CancellationToken token, bool owner = false)
        {
            request["schema"] = SchemaName;
            if (owner)
            {
                request["admin_connection"] = DatabaseAdmin;
            }
            else
            {
                var keys = new[] { "evaluation", "promotion", "revocation", "fence", "executor", "reconciliation", "recovery", "state" };
                var spki = new JsonObject();
                for (var index = 0; index < keys.Length; index++)
                    spki[keys[index]] = Convert.ToBase64String(_authorities[index].ExportSubjectPublicKeyInfo());
                request["runtime_connection"] = _runtimeConnection;
                request["runtime_role"] = _roles[0];
                request["authority_spki"] = spki;
                request["evaluation_private_pkcs8"] = Convert.ToBase64String(Evaluation.ExportPkcs8PrivateKey());
                request["release_bom_sha256"] = ReleaseBom;
            }
            var entry = Path.Combine(SoulCurrentProcessFixture.RepositoryRoot(),
                "Modules/control-plane-host/tests/PolicyShadowChainProcess/bin/Release/net10.0/Dps.ControlPlaneHost.PolicyShadowChainFixture.dll");
            if (!File.Exists(entry)) throw new InvalidOperationException("Build the shadow chain entry process first.");
            var start = new ProcessStartInfo(Path.Combine(
                Environment.GetEnvironmentVariable("DOTNET_ROOT") ?? throw new InvalidOperationException("Pinned DOTNET_ROOT required."), "dotnet"))
            {
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false
            };
            start.ArgumentList.Add(entry);
            start.Environment.Remove("DPS_TEST_POSTGRES");
            if (!owner) start.Environment.Remove("PGPASSFILE");
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Entry process failed to start.");
            await process.StandardInput.WriteLineAsync(Convert.ToBase64String(Encoding.UTF8.GetBytes(request.ToJsonString())));
            process.StandardInput.Close();
            var output = await process.StandardOutput.ReadToEndAsync(token);
            var error = await process.StandardError.ReadToEndAsync(token);
            await process.WaitForExitAsync(token);
            if (error.Length != 0) throw new InvalidOperationException("Entry process emitted stderr: " + error);
            Assert.NotEqual(3, process.ExitCode);
            return (process.ExitCode, output.Trim());
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var key in _authorities) key.Dispose();
            await using var connection = new NpgsqlConnection(_admin);
            await connection.OpenAsync(CancellationToken.None);
            await using (var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {_database} WITH (FORCE)", connection) { CommandTimeout = 10 })
                await drop.ExecuteNonQueryAsync(CancellationToken.None);
            foreach (var role in _roles)
            {
                await using var dropRole = new NpgsqlCommand($"DROP ROLE IF EXISTS {role}", connection) { CommandTimeout = 5 };
                await dropRole.ExecuteNonQueryAsync(CancellationToken.None);
            }
        }
    }
}
