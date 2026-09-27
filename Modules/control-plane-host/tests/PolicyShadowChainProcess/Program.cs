using System.Security.Cryptography;
using System.Text.Json;
using Dps.ControlPlaneHost;
using Dps.Planner;
using Dps.Planner.Contracts;
using Dps.PolicyApproval;
using Dps.PolicyApproval.Contracts;
using Dps.SoulRegistry.Contracts;
using Npgsql;
using NpgsqlTypes;

// Test-only entry process (non-production); the only project that wires the Planner and
// Policy implementations to the contract-only Host chain. HostStartup still accepts only
// --self-check. One request per process: a base64 JSON line on stdin; the evaluation test
// key arrives only through that pipe and is never written to disk. Only the fixture modes
// "setup" and "append_state" receive the test-database owner connection; the chain modes
// get the runtime login only.
string[] forbidden = ["Dps.SoulRegistry", "Dps.CommandOrchestrator", "Dps.ExecutorGateway"];
bool LoadedForbidden() => AppDomain.CurrentDomain.GetAssemblies().Any(a => forbidden.Contains(a.GetName().Name));
if (args.Length != 0) return 2;
byte[]? privateKey = null;
try
{
    var line = await Console.In.ReadLineAsync() ?? throw new ArgumentException("Missing request.");
    using var document = JsonDocument.Parse(Convert.FromBase64String(line));
    var root = document.RootElement;
    string Text(string name) => root.GetProperty(name).GetString() ?? throw new ArgumentException(name);
    byte[] Wire(string name) => Convert.FromBase64String(Text(name));
    byte[] Key(string name) => Convert.FromBase64String(root.GetProperty("authority_spki").GetProperty(name).GetString()!);
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    var mode = Text("mode");

    if (mode == "setup")
    {
        var roles = root.GetProperty("roles").EnumerateArray().Select(role => role.GetString()!).ToArray();
        await new PostgresPolicyApprovalMigrator(new PolicyApprovalMigrationOptions(
            Text("admin_connection"), Text("schema"), roles[0], roles[1], roles[2], roles[3])).InitializeAsync(timeout.Token);
        Console.WriteLine("MIGRATED");
        return 0;
    }
    if (mode == "append_state")
    {
        await AppendRuntimeStateAsync(
            Text("admin_connection"), Text("schema"), Text("soul_id"), Text("device_binding_id"),
            Text("platform_account_id"), Text("release_bom_sha256"), timeout.Token);
        Console.WriteLine("APPENDED");
        return 0;
    }

    var options = new PostgresPolicyApprovalOptions(Text("runtime_connection"), Text("schema"), Text("runtime_role"));
    var reader = new PolicyApprovalAuthoritativeClient(options);
    if (mode == "read")
    {
        var request = root.GetProperty("read_request");
        string Field(string name) => request.GetProperty(name).GetString()!;
        var snapshot = await reader.ReadAsync(new PolicyApprovalReadRequest(
            Guid.Parse(Field("approval_id")), Guid.Parse(Field("proposal_id")), Field("soul_id"), Field("device_binding_id"),
            Field("platform_account_id"), Field("trace_id"), Field("idempotency_key"), Field("approval_sha256")), timeout.Token);
        if (LoadedForbidden()) return 3;
        Console.WriteLine("READ:" + Convert.ToBase64String(ApprovalReadbackV1Codec.Serialize(snapshot.ToReadbackV1())));
        return 0;
    }

    var evaluation = Key("evaluation");
    var promotion = Key("promotion");
    var revocation = Key("revocation");
    var topology = PolicyApprovalSubmissionAuthorityTopology.Create(
        evaluation, promotion, revocation, Key("fence"), Key("executor"), Key("reconciliation"), Key("recovery"), Key("state"));
    using var policy = PostgresPolicyApprovalService.CreateProduction(options, topology, evaluation, promotion, revocation);

    // Policy evaluation plus the exact reader: the read readback must equal the appended one.
    async Task<byte[]> EvaluateAndReadExactAsync(ReadOnlyMemory<byte> proposalWire, byte[] requestWire, CancellationToken token)
    {
        var appended = await policy.EvaluateAsync(proposalWire, requestWire, null, token);
        var approval = appended.Snapshot.Approval;
        var read = await reader.ReadAsync(new PolicyApprovalReadRequest(
            approval.ApprovalId, approval.ProposalId, approval.SoulId, approval.DeviceBindingId,
            approval.PlatformAccountId, approval.TraceId, approval.IdempotencyKey, appended.Snapshot.CanonicalSha256), token);
        var wire = ApprovalReadbackV1Codec.Serialize(read.ToReadbackV1());
        if (!wire.AsSpan().SequenceEqual(ApprovalReadbackV1Codec.Serialize(appended.Snapshot.ToReadbackV1())))
            throw new UnauthorizedAccessException("Exact readback differs from the appended Policy snapshot.");
        return wire;
    }

    if (mode == "policy_evaluate")
    {
        // Direct Policy evaluation on the chain database (v1 quarantine evidence); no Host.
        try
        {
            var promotionWire = root.TryGetProperty("promotion_wire", out var value) ? value.GetString() : null;
            var appended = await policy.EvaluateAsync(
                Wire("proposal_wire"),
                Wire("request_wire"),
                promotionWire is null ? null : ActionExecutionPromotionV1Codec.Deserialize(Convert.FromBase64String(promotionWire)),
                timeout.Token);
            if (LoadedForbidden()) return 3;
            Console.WriteLine("EVALUATED:" + Convert.ToBase64String(ApprovalReadbackV1Codec.Serialize(appended.Snapshot.ToReadbackV1())));
            return 0;
        }
        catch (PolicyProposalQuarantinedException quarantined)
        {
            Console.WriteLine($"QUARANTINED:{quarantined.Proof.Major}:{quarantined.Proof.Disposition}");
            return 5;
        }
    }

    using var signer = ECDsa.Create();
    privateKey = Wire("evaluation_private_pkcs8");
    signer.ImportPkcs8PrivateKey(privateKey, out _);
    var chain = new ControlPlanePolicyShadowChain(
        EvaluateAndReadExactAsync,
        signer,
        // An omitted switch keeps the composition's closed default.
        root.TryGetProperty("kill_control_plane_ingest", out var kill)
            ? new ControlPlaneShadowChainOptions(Text("release_bom_sha256")) { KillControlPlaneIngest = kill.GetBoolean() }
            : new ControlPlaneShadowChainOptions(Text("release_bom_sha256")));
    byte[] proposalWire;
    if (mode == "relay")
    {
        proposalWire = Wire("proposal_wire");
    }
    else if (mode == "plan")
    {
        // Identity context arrives only as the fixture-gated soul.current output
        // (SOUL_ACTIVE_AS_OF wire). Soul, device and account are a SHADOW echo only,
        // never an authorized identity.
        var soul = SoulResolvedCanonicalV1.FromUtf8Json(Wire("soul_resolved_wire"));
        var input = root.GetProperty("input");
        proposalWire = ActionProposalV2Json.Serialize(new ShadowActionPlanner().Propose(new PlanningRequest(
            soul.SoulId,
            soul.DeviceBindingId,
            soul.PlatformAccountId,
            input.GetProperty("trace_id").GetString()!,
            input.GetProperty("idempotency_key").GetString()!,
            input.GetProperty("occurred_at").GetDateTimeOffset().ToUniversalTime(),
            input.GetProperty("proposer_kind").GetString()!,
            input.GetProperty("action_kind").GetString()!,
            input.GetProperty("parameters").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!, StringComparer.Ordinal),
            input.GetProperty("evidence_refs").EnumerateArray().Select(e => e.GetString()!).ToArray())));
    }
    else return 6;
    var evaluated = await chain.RelayShadowAsync(proposalWire, timeout.Token);
    if (LoadedForbidden()) return 3;
    Console.WriteLine("EVALUATED:" + Convert.ToBase64String(evaluated));
    return 0;
}
catch (ControlPlaneShadowChainKilledException)
{
    if (LoadedForbidden()) return 3;
    Console.WriteLine("KILLED");
    return 4;
}
catch (Exception error) when (error is not OutOfMemoryException)
{
    Console.WriteLine("REJECTED:" + error.GetType().Name);
    return 1;
}
finally
{
    if (privateKey is not null) CryptographicOperations.ZeroMemory(privateKey);
}

// Declared configuration input (D6): a synthetic runtime revision written with the owner connection.
static async Task AppendRuntimeStateAsync(
    string adminConnection, string schema, string soulId, string device, string account, string releaseBom, CancellationToken token)
{
    var state = new PolicyRuntimeStateRevisionV1(
        soulId, device, account, 1, PolicyRuntimeStateRevisionV1.Active, "1.0.0",
        DeterministicPolicyEvaluator.KnownPolicies.Order(StringComparer.Ordinal).ToArray(),
        false, 100, true, "platform-authorization-fixture", true, releaseBom, DateTimeOffset.UtcNow.AddHours(1));
    state.Validate();
    await using var connection = new NpgsqlConnection(adminConnection);
    await connection.OpenAsync(token);
    await using var command = new NpgsqlCommand(
        $"""
        INSERT INTO {schema}.policy_runtime_revisions
        (soul_id, device_binding_id, platform_account_id, revision, state_status,
         policy_version, enabled_policy_ids, kill_switch_enabled, remaining_rate_budget,
         platform_authorized, platform_authorization_id, execution_enabled,
         release_bom_sha256, valid_until, state_sha256)
        VALUES
        (@soul_id, @device_binding_id, @platform_account_id, @revision, @state_status,
         @policy_version, @enabled_policy_ids, @kill_switch_enabled, @remaining_rate_budget,
         @platform_authorized, @platform_authorization_id, @execution_enabled,
         @release_bom_sha256, @valid_until, @state_sha256)
        """, connection) { CommandTimeout = 5 };
    command.Parameters.AddWithValue("soul_id", state.SoulId);
    command.Parameters.AddWithValue("device_binding_id", state.DeviceBindingId);
    command.Parameters.AddWithValue("platform_account_id", state.PlatformAccountId);
    command.Parameters.AddWithValue("revision", state.Revision);
    command.Parameters.AddWithValue("state_status", state.StateStatus);
    command.Parameters.AddWithValue("policy_version", state.PolicyVersion);
    command.Parameters.AddWithValue("enabled_policy_ids", NpgsqlDbType.Array | NpgsqlDbType.Text, state.EnabledPolicyIds.ToArray());
    command.Parameters.AddWithValue("kill_switch_enabled", state.KillSwitchEnabled);
    command.Parameters.AddWithValue("remaining_rate_budget", state.RemainingRateBudget);
    command.Parameters.AddWithValue("platform_authorized", state.PlatformAuthorized);
    command.Parameters.AddWithValue("platform_authorization_id", NpgsqlDbType.Text, (object?)state.PlatformAuthorizationId ?? DBNull.Value);
    command.Parameters.AddWithValue("execution_enabled", state.ExecutionEnabled);
    command.Parameters.AddWithValue("release_bom_sha256", state.ReleaseBomSha256);
    command.Parameters.AddWithValue("valid_until", state.ValidUntil);
    command.Parameters.AddWithValue("state_sha256", PolicyRuntimeStateCommitment.ComputeSha256(state));
    await command.ExecuteNonQueryAsync(token);
}
