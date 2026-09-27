using System.Security.Cryptography;
using System.Text;
using Dps.Planner.Contracts;
using Dps.PolicyApproval.Contracts;
using Npgsql;
using Xunit;

namespace Dps.PolicyApproval.Tests;

public sealed partial class PostgresPolicyApprovalIntegrationTests
{
    [Fact, Trait("Category", "Integration")]
    public async Task V2ExecuteWithBoundPromotionIsApprovedButBeginsNoSubmission()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var database = await PolicyApprovalTestDatabase.CreateAsync(cancellationToken);
        await database.AppendRuntimeStateAsync(State(SoulA, DeviceA, AccountA), cancellationToken);
        using var evaluationSigner = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var revocationSigner = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var service = database.CreateService(evaluationSigner, revocationSigner);
        var evaluation = SignEvaluation(
            evaluationSigner,
            Proposal(SoulA, DeviceA, AccountA, "idem-v2-positive"));

        var issued = await service.EvaluateSignedAsync(evaluation, cancellationToken);

        Assert.Equal(PolicyApprovalAppendDisposition.Inserted, issued.Disposition);
        Assert.Equal(ApprovalDecisionV1.Approved, issued.Snapshot.Approval.Decision);
        Assert.False(issued.Snapshot.Approval.ShadowOnly);
        Assert.Equal(2, issued.Snapshot.ProposalContractMajor);
        Assert.Equal(ActionProposalV2Canonical.ComputeSha256(evaluation.Proposal), issued.Snapshot.ProposalSha256);
        Assert.True(issued.Snapshot.ValidUntil <= evaluation.Request.ValidUntil);
        var stored = await database.ReadDecisionColumnsAsync(issued.Snapshot.Approval.ApprovalId, cancellationToken);
        Assert.Equal(2, stored.ProposalContractMajor);
        Assert.Equal(evaluation.Request.SignatureBase64, stored.RequestSignature);
        Assert.Equal(issued.Snapshot.ProposalSha256, stored.ProposalSha256);
        Assert.Equal(
            ApprovalEvaluationRequestV1Canonical.ComputeCommitmentSha256(evaluation.Request),
            stored.CommandSha256);
        var readback = await new PolicyApprovalAuthoritativeClient(database.Options)
            .ReadAsync(ReadRequest(issued.Snapshot), cancellationToken);
        Assert.Equal(2, readback.ProposalContractMajor);
        Assert.Equal(issued.Snapshot.CanonicalSha256, readback.CanonicalSha256);

        // An executable approval is the whole S1 positive path: nothing is fenced,
        // begun, or dispatched; the outbox row is the decision publication only.
        var rows = await database.CountOwnedRowsAsync(cancellationToken);
        Assert.Equal(1, rows["policy_rate_consumptions"]);
        Assert.Equal(1, rows["approval_outbox"]);
        foreach (var table in PolicyApprovalTestDatabase.LifecycleTables)
            Assert.Equal(0, rows[table]);
    }

    [Fact, Trait("Category", "Integration")]
    public async Task V2ShadowRequestIsDeniedShadowOnlyWithZeroSideEffectsAndCannotBeFenced()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var database = await PolicyApprovalTestDatabase.CreateAsync(cancellationToken);
        await database.AppendRuntimeStateAsync(State(SoulA, DeviceA, AccountA), cancellationToken);
        using var evaluationSigner = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var revocationSigner = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var fenceSigner = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var executorSigner = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var reconciliationSigner = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var recoverySigner = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var stateSigner = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var authorityTopology = PolicyApprovalTestAuthorities.CreateTopology(
            evaluationSigner,
            revocationSigner,
            fenceSigner,
            executorSigner,
            reconciliationSigner,
            recoverySigner,
            stateSigner);
        using var service = database.CreateService(
            evaluationSigner,
            revocationSigner,
            authorityTopology: authorityTopology);
        var proposal = Proposal(SoulA, DeviceA, AccountA, "idem-v2-shadow");

        var shadow = await service.EvaluateSignedAsync(
            SignEvaluation(evaluationSigner, proposal, execute: false),
            cancellationToken);

        Assert.Equal(PolicyApprovalAppendDisposition.Inserted, shadow.Disposition);
        Assert.Equal(ApprovalDecisionV1.Denied, shadow.Snapshot.Approval.Decision);
        Assert.True(shadow.Snapshot.Approval.ShadowOnly);
        Assert.Contains("SHADOW_ONLY", shadow.Snapshot.Approval.DenialReasons);
        Assert.Equal(2, shadow.Snapshot.ProposalContractMajor);

        // There is no fence table: "no fence" means acquisition is refused and no
        // submission attempt row can exist.
        var fenceRequest = FenceRequest(shadow.Snapshot);
        var intent = SignSubmissionIntent(
            executorSigner,
            SubmissionIntent(shadow.Snapshot, proposal, fenceRequest));
        using var fenceClient = CreateSubmissionClient(
            database,
            authorityTopology,
            fenceSigner,
            executorSigner,
            reconciliationSigner,
            recoverySigner,
            stateSigner);
        var refused = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fenceClient.AcquireAsync(
            fenceRequest,
            SignFenceAuthorization(fenceSigner, fenceRequest),
            intent,
            cancellationToken));
        Assert.StartsWith("Approval execution fence expectations", refused.Message, StringComparison.Ordinal);

        var rows = await database.CountOwnedRowsAsync(cancellationToken);
        Assert.Equal(0, rows["policy_rate_consumptions"]);
        foreach (var table in PolicyApprovalTestDatabase.LifecycleTables)
            Assert.Equal(0, rows[table]);
        Assert.Equal(1, rows["approval_decisions"]);
        Assert.Equal(1, rows["approval_status_revisions"]);
        Assert.Equal(1, rows["approval_idempotency_receipts"]);
        Assert.Equal(1, rows["approval_outbox"]);
        Assert.Equal(0, rows["approval_idempotency_quarantine"]);
    }

    [Fact, Trait("Category", "Integration")]
    public async Task V1AndUnknownMajorProposalWireFailBeforeAuthenticationWithZeroRows()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var database = await PolicyApprovalTestDatabase.CreateAsync(cancellationToken);
        await database.AppendRuntimeStateAsync(State(SoulA, DeviceA, AccountA), cancellationToken);
        using var evaluationSigner = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var revocationSigner = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var service = database.CreateService(evaluationSigner, revocationSigner);
        var evaluation = SignEvaluation(evaluationSigner, Proposal(SoulA, DeviceA, AccountA, "idem-v1-gate"));

        // Even an authentic EXECUTE request and promotion bound to the v1 commitment
        // cannot reach authentication: the v1 wire is quarantined at the entry gate.
        var legacy = LegacyProposal(SoulA, DeviceA, AccountA, "idem-v1-gate");
        var legacyWire = ActionProposalV1Json.Serialize(legacy);
        var legacySha256 = ActionProposalCanonical.ComputeSha256(legacy);
        var legacyPromotion = SignPromotion(
            evaluationSigner,
            evaluation.Promotion! with { ProposalSha256 = legacySha256 });
        var legacyRequest = SignRequest(evaluationSigner, evaluation.Request with
        {
            ProposalSha256 = legacySha256,
            ExecutionPromotionSha256 = ApprovalEvaluationRequestV1Canonical.ComputeExecutionPromotionSha256(legacyPromotion),
            OccurredAt = legacy.OccurredAt,
            ValidUntil = legacy.OccurredAt + ApprovalEvaluationRequestV1.Lifetime
        });
        var quarantined = await Assert.ThrowsAsync<PolicyProposalQuarantinedException>(() => service.EvaluateAsync(
            legacyWire,
            ApprovalEvaluationRequestV1Codec.Serialize(legacyRequest),
            legacyPromotion,
            cancellationToken));
        Assert.Equal(ActionProposalV1.CurrentContractId, quarantined.Proof.ContractId);
        Assert.Equal(1, quarantined.Proof.Major);
        Assert.Equal(PolicyProposalQuarantineProof.QuarantineDisposition, quarantined.Proof.Disposition);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(legacyWire)), quarantined.Proof.WireSha256);
        Assert.Equal(legacyWire.Length, quarantined.Proof.WireBytes);

        var unknownMajor = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(evaluation.ProposalWire).Replace(
            "\"contract_id\":\"action.proposal/v2\"",
            "\"contract_id\":\"action.proposal/v3\"",
            StringComparison.Ordinal));
        await Assert.ThrowsAsync<NotSupportedException>(() => service.EvaluateSignedAsync(
            evaluation with { ProposalWire = unknownMajor },
            cancellationToken));
        Assert.All(await database.CountOwnedRowsAsync(cancellationToken), pair => Assert.Equal(0, pair.Value));

        var issued = await service.EvaluateSignedAsync(evaluation, cancellationToken);
        Assert.Equal(ApprovalDecisionV1.Approved, issued.Snapshot.Approval.Decision);
    }

    [Fact, Trait("Category", "Integration")]
    public async Task EvaluationRequestMustBindExactProposalModeAndAuthorityBeforeAnyWrite()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var database = await PolicyApprovalTestDatabase.CreateAsync(cancellationToken);
        await database.AppendRuntimeStateAsync(State(SoulA, DeviceA, AccountA), cancellationToken);
        using var evaluationSigner = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var revocationSigner = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var foreignSigner = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var service = database.CreateService(evaluationSigner, revocationSigner);
        var evaluation = SignEvaluation(evaluationSigner, Proposal(SoulA, DeviceA, AccountA, "idem-binding"));
        var other = SignEvaluation(evaluationSigner, Proposal(SoulA, DeviceA, AccountA, "idem-binding-other"));
        var shadow = SignEvaluation(evaluationSigner, evaluation.Proposal, execute: false);
        var forged = SignEvaluation(foreignSigner, evaluation.Proposal);

        foreach (var attempt in new[]
                 {
                     evaluation with { ProposalWire = other.ProposalWire },
                     evaluation with { Promotion = null },
                     shadow with { Promotion = evaluation.Promotion },
                     evaluation with { Promotion = other.Promotion },
                     evaluation with { Promotion = forged.Promotion },
                     evaluation with { RequestWire = forged.RequestWire },
                     forged
                 })
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => service.EvaluateSignedAsync(attempt, cancellationToken));
        }
        await Assert.ThrowsAsync<ArgumentException>(() => service.EvaluateSignedAsync(
            evaluation with { RequestWire = "{}"u8.ToArray() },
            cancellationToken));
        Assert.All(await database.CountOwnedRowsAsync(cancellationToken), pair => Assert.Equal(0, pair.Value));
    }

    [Fact, Trait("Category", "Integration")]
    public async Task SignatureFreeCommitmentMakesResignedReplaysDuplicatesAndBusinessChangesConflicts()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var database = await PolicyApprovalTestDatabase.CreateAsync(cancellationToken);
        await database.AppendRuntimeStateAsync(State(SoulA, DeviceA, AccountA), cancellationToken);
        using var evaluationSigner = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var revocationSigner = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var foreignSigner = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var service = database.CreateService(evaluationSigner, revocationSigner);
        var evaluation = SignEvaluation(evaluationSigner, Proposal(SoulA, DeviceA, AccountA, "idem-replay"));
        var issued = await service.EvaluateSignedAsync(evaluation, cancellationToken);
        Assert.Equal(PolicyApprovalAppendDisposition.Inserted, issued.Disposition);

        var resignedRequest = SignRequest(evaluationSigner, evaluation.Request);
        var resignedPromotion = SignPromotion(evaluationSigner, evaluation.Promotion!);
        Assert.NotEqual(evaluation.Request.SignatureBase64, resignedRequest.SignatureBase64);
        Assert.NotEqual(evaluation.Promotion!.SignatureBase64, resignedPromotion.SignatureBase64);
        foreach (var replay in new[]
                 {
                     evaluation,
                     evaluation with
                     {
                         Request = resignedRequest,
                         RequestWire = ApprovalEvaluationRequestV1Codec.Serialize(resignedRequest)
                     },
                     evaluation with { Promotion = resignedPromotion }
                 })
        {
            var duplicate = await service.EvaluateSignedAsync(replay, cancellationToken);
            Assert.Equal(PolicyApprovalAppendDisposition.DuplicateNoOp, duplicate.Disposition);
            Assert.Equal(issued.Snapshot.CanonicalSha256, duplicate.Snapshot.CanonicalSha256);
        }
        Assert.Equal(
            evaluation.Request.SignatureBase64,
            (await database.ReadDecisionColumnsAsync(issued.Snapshot.Approval.ApprovalId, cancellationToken))
                .RequestSignature);

        // Only authentic requests reach the receipt comparison; a forged one leaves no trace.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.EvaluateSignedAsync(
            evaluation with
            {
                RequestWire = ApprovalEvaluationRequestV1Codec.Serialize(
                    SignRequest(foreignSigner, evaluation.Request))
            },
            cancellationToken));
        Assert.Equal(0, await service.CountAsync("quarantine", cancellationToken));

        // An authentic request whose business content differs is a hash-only conflict.
        await Assert.ThrowsAsync<PolicyApprovalIdempotencyConflictException>(() => service.EvaluateSignedAsync(
            SignEvaluation(evaluationSigner, evaluation.Proposal, execute: false),
            cancellationToken));
        await Assert.ThrowsAsync<PolicyApprovalIdempotencyConflictException>(() => service.EvaluateSignedAsync(
            SignEvaluation(evaluationSigner, evaluation.Proposal),
            cancellationToken));

        var rows = await database.CountOwnedRowsAsync(cancellationToken);
        Assert.Equal(1, rows["approval_decisions"]);
        Assert.Equal(1, rows["approval_idempotency_receipts"]);
        Assert.Equal(1, rows["approval_outbox"]);
        Assert.Equal(2, rows["approval_idempotency_quarantine"]);
        await database.AssertQuarantineContainsHashesOnlyAsync(cancellationToken);
    }

    [Fact, Trait("Category", "Integration")]
    public async Task ExpiredNewRequestWritesNothingWhileExactReplayNeedsNoFreshWindow()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var database = await PolicyApprovalTestDatabase.CreateAsync(cancellationToken);
        await database.AppendRuntimeStateAsync(State(SoulA, DeviceA, AccountA), cancellationToken);
        using var evaluationSigner = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var revocationSigner = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var service = database.CreateService(evaluationSigner, revocationSigner);

        var expired = SignEvaluation(evaluationSigner, Proposal(
            SoulA,
            DeviceA,
            AccountA,
            "idem-expired-at-entry",
            occurredAt: DateTimeOffset.UtcNow - ApprovalEvaluationRequestV1.Lifetime - TimeSpan.FromSeconds(1)));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.EvaluateSignedAsync(expired, cancellationToken));
        Assert.All(await database.CountOwnedRowsAsync(cancellationToken), pair => Assert.Equal(0, pair.Value));

        var shortLived = SignEvaluation(evaluationSigner, Proposal(
            SoulA,
            DeviceA,
            AccountA,
            "idem-short-lived",
            occurredAt: DateTimeOffset.UtcNow.AddSeconds(3) - ApprovalEvaluationRequestV1.Lifetime));
        var issued = await service.EvaluateSignedAsync(shortLived, cancellationToken);
        Assert.Equal(PolicyApprovalAppendDisposition.Inserted, issued.Disposition);
        var remaining = shortLived.Request.ValidUntil - DateTimeOffset.UtcNow;
        if (remaining > TimeSpan.Zero)
            await Task.Delay(remaining + TimeSpan.FromMilliseconds(250), cancellationToken);

        var replay = await service.EvaluateSignedAsync(shortLived, cancellationToken);
        Assert.Equal(PolicyApprovalAppendDisposition.DuplicateNoOp, replay.Disposition);
        Assert.Equal(issued.Snapshot.CanonicalSha256, replay.Snapshot.CanonicalSha256);
        Assert.Equal(1, await service.CountAsync("decisions", cancellationToken));
    }

    [Fact, Trait("Category", "Integration")]
    public async Task Migration005KeepsHistoricalV1RowsQuarantineOnlyAndReinitializesIdempotently()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var database = await PolicyApprovalTestDatabase.CreateAsync(
            cancellationToken,
            applyProductMigrations: false);
        var state = State(SoulA, DeviceA, AccountA);
        await database.AppendRuntimeStateAsync(state, cancellationToken);
        var approvedLegacy = LegacyDecision(SoulA, DeviceA, AccountA, "idem-historical-approved", approved: true);
        var deniedLegacy = LegacyDecision(SoulA, DeviceA, AccountA, "idem-historical-denied", approved: false);
        var approvedSha256 = await database.InsertLegacyDecisionAsync(approvedLegacy, state, cancellationToken);
        var deniedSha256 = await database.InsertLegacyDecisionAsync(deniedLegacy, state, cancellationToken);

        await database.MigrateAsync(cancellationToken);

        foreach (var legacy in new[] { approvedLegacy, deniedLegacy })
        {
            var columns = await database.ReadDecisionColumnsAsync(legacy.ApprovalId, cancellationToken);
            Assert.Equal(1, columns.ProposalContractMajor);
            Assert.Null(columns.RequestSignature);
        }
        var reader = new PolicyApprovalAuthoritativeClient(database.Options);
        await AssertV1QuarantinedAsync(
            () => reader.ReadAsync(LegacyReadRequest(approvedLegacy, approvedSha256), cancellationToken));
        var denied = await reader.ReadAsync(LegacyReadRequest(deniedLegacy, deniedSha256), cancellationToken);
        Assert.Equal(1, denied.ProposalContractMajor);
        Assert.Equal(ApprovalDecisionV1.Denied, denied.Approval.Decision);

        using var evaluationSigner = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var revocationSigner = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        PolicyApprovalAppendResult issued;
        using (var service = database.CreateService(evaluationSigner, revocationSigner))
        {
            issued = await service.EvaluateSignedAsync(
                SignEvaluation(evaluationSigner, Proposal(SoulA, DeviceA, AccountA, "idem-after-005")),
                cancellationToken);
            Assert.Equal(2, issued.Snapshot.ProposalContractMajor);

            // Revocation still reaches the historical approval, and REVOKED v1 is readable.
            var revoke = new PolicyApprovalRevocationRequest(
                approvedLegacy.ApprovalId,
                approvedLegacy.ProposalId,
                SoulA,
                DeviceA,
                AccountA,
                TraceId("revoke-historical"),
                IdempotencyKey("revoke-historical"),
                approvedSha256,
                1);
            var revoked = await service.RevokeAsync(revoke, SignRevocation(revocationSigner, revoke), cancellationToken);
            Assert.Equal(PolicyApprovalAuthoritativeSnapshot.Revoked, revoked.Snapshot.Status);
            Assert.Equal(1, revoked.Snapshot.ProposalContractMajor);
        }

        await database.MigrateAsync(cancellationToken);

        Assert.Equal(1, (await database.ReadDecisionColumnsAsync(approvedLegacy.ApprovalId, cancellationToken)).ProposalContractMajor);
        Assert.Equal(2, (await database.ReadDecisionColumnsAsync(issued.Snapshot.Approval.ApprovalId, cancellationToken)).ProposalContractMajor);
        Assert.Equal(
            issued.Snapshot.CanonicalSha256,
            (await reader.ReadAsync(ReadRequest(issued.Snapshot), cancellationToken)).CanonicalSha256);
        Assert.Equal(
            PolicyApprovalAuthoritativeSnapshot.Revoked,
            (await reader.ReadAsync(LegacyReadRequest(approvedLegacy, approvedSha256), cancellationToken)).Status);
        using var restarted = database.CreateService(evaluationSigner, revocationSigner);
        Assert.Equal(3, await restarted.CountAsync("decisions", cancellationToken));
    }

    [Fact, Trait("Category", "Integration")]
    public async Task DatabaseRejectsSubmissionAttemptsForNonV2DecisionsFromAnyCaller()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var database = await PolicyApprovalTestDatabase.CreateAsync(cancellationToken);
        var state = State(SoulA, DeviceA, AccountA);
        await database.AppendRuntimeStateAsync(state, cancellationToken);
        using var evaluationSigner = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var revocationSigner = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var service = database.CreateService(evaluationSigner, revocationSigner);
        var issued = await service.EvaluateSignedAsync(
            SignEvaluation(evaluationSigner, Proposal(SoulA, DeviceA, AccountA, "idem-trigger-v2")),
            cancellationToken);
        // A pre-0.7.0 binary that keeps writing after migration 005 produces v1 rows.
        var legacy = LegacyDecision(SoulA, DeviceA, AccountA, "idem-trigger-v1", approved: true);
        await database.InsertLegacyDecisionAsync(legacy, state, cancellationToken);
        Assert.Equal(1, (await database.ReadDecisionColumnsAsync(legacy.ApprovalId, cancellationToken)).ProposalContractMajor);

        await using var owner = new NpgsqlConnection(database.AdminConnectionString);
        await owner.OpenAsync(cancellationToken);
        await PolicyApprovalTestDatabase.AssertSqlStateAsync(
            owner,
            $"INSERT INTO {database.SchemaName}.approval_submission_attempts (approval_id) VALUES ('{legacy.ApprovalId}')",
            PostgresErrorCodes.InsufficientPrivilege,
            cancellationToken);
        await using (var v2Attempt = new NpgsqlCommand(
            $"INSERT INTO {database.SchemaName}.approval_submission_attempts (approval_id) VALUES ('{issued.Snapshot.Approval.ApprovalId}')",
            owner) { CommandTimeout = 5 })
        {
            var rowConstraint = await Assert.ThrowsAsync<PostgresException>(
                () => v2Attempt.ExecuteNonQueryAsync(cancellationToken));
            Assert.NotEqual(PostgresErrorCodes.InsufficientPrivilege, rowConstraint.SqlState);
        }

        await using var catalog = new NpgsqlCommand(
            $"""
            SELECT function.prosecdef,
                   array_to_string(function.proconfig, ';'),
                   function.proacl IS NOT NULL
                   AND NOT EXISTS (
                       SELECT 1
                         FROM aclexplode(function.proacl) AS acl
                        WHERE acl.grantee <> function.proowner),
                   has_function_privilege(@runtime_role, function.oid, 'EXECUTE'),
                   has_function_privilege(@executor_role, function.oid, 'EXECUTE'),
                   has_function_privilege(@reconciliation_role, function.oid, 'EXECUTE'),
                   has_function_privilege(@recovery_role, function.oid, 'EXECUTE'),
                   (SELECT count(*)
                      FROM pg_trigger AS trigger
                     WHERE trigger.tgfoid = function.oid
                       AND trigger.tgname = 'approval_submission_attempts_require_v2'
                       AND trigger.tgenabled = 'O'
                       AND trigger.tgrelid = '{database.SchemaName}.approval_submission_attempts'::regclass)
              FROM pg_proc AS function
             WHERE function.oid = '{database.SchemaName}.reject_non_v2_submission_attempt()'::regprocedure
            """,
            owner) { CommandTimeout = 5 };
        catalog.Parameters.AddWithValue("runtime_role", database.RuntimeRoleName);
        catalog.Parameters.AddWithValue("executor_role", database.SubmissionExecutorRoleName);
        catalog.Parameters.AddWithValue("reconciliation_role", database.ReconciliationRoleName);
        catalog.Parameters.AddWithValue("recovery_role", database.RecoveryRoleName);
        await using var reader = await catalog.ExecuteReaderAsync(cancellationToken);
        Assert.True(await reader.ReadAsync(cancellationToken));
        Assert.False(reader.GetBoolean(0));
        Assert.Equal($"search_path=pg_catalog, {database.SchemaName}", reader.GetString(1));
        Assert.True(reader.GetBoolean(2));
        for (var ordinal = 3; ordinal <= 6; ordinal++)
            Assert.False(reader.GetBoolean(ordinal));
        Assert.Equal(1L, reader.GetInt64(7));
        await reader.DisposeAsync();

        Assert.Equal(0, (await database.CountOwnedRowsAsync(cancellationToken))["approval_submission_attempts"]);
    }

    [Fact, Trait("Category", "Integration")]
    public async Task V1ExecutableRowIsIsolatedAtDuplicateBranchReaderAndFence()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var database = await PolicyApprovalTestDatabase.CreateAsync(cancellationToken);
        var state = State(SoulA, DeviceA, AccountA);
        await database.AppendRuntimeStateAsync(state, cancellationToken);
        using var evaluationSigner = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var revocationSigner = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var fenceSigner = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var executorSigner = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var reconciliationSigner = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var recoverySigner = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var stateSigner = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var authorityTopology = PolicyApprovalTestAuthorities.CreateTopology(
            evaluationSigner,
            revocationSigner,
            fenceSigner,
            executorSigner,
            reconciliationSigner,
            recoverySigner,
            stateSigner);
        using var service = database.CreateService(
            evaluationSigner,
            revocationSigner,
            authorityTopology: authorityTopology);

        // A pre-0.7.0 binary left an executable v1 approval and its receipt for exactly
        // the scoped idempotency key that an authentic v2 request now replays.
        var evaluation = SignEvaluation(evaluationSigner, Proposal(SoulA, DeviceA, AccountA, "idem-legacy-receipt"));
        var legacy = LegacyDecision(SoulA, DeviceA, AccountA, "idem-legacy-receipt", approved: true);
        var legacySha256 = await database.InsertLegacyDecisionAsync(
            legacy,
            state,
            cancellationToken,
            ApprovalEvaluationRequestV1Canonical.ComputeCommitmentSha256(evaluation.Request));
        var before = await database.CountOwnedRowsAsync(cancellationToken);

        await AssertV1QuarantinedAsync(() => service.EvaluateSignedAsync(evaluation, cancellationToken));
        await AssertV1QuarantinedAsync(() => new PolicyApprovalAuthoritativeClient(database.Options)
            .ReadAsync(LegacyReadRequest(legacy, legacySha256), cancellationToken));

        var fenceRequest = new ApprovalExecutionFenceRequestV1(
            ApprovalExecutionFenceRequestV1.CurrentSchemaVersion,
            ApprovalExecutionFenceRequestV1.CurrentContractId,
            ApprovalExecutionFenceRequestV1.CurrentConsumerModule,
            legacy.ApprovalId,
            legacy.ProposalId,
            legacy.SoulId,
            legacy.DeviceBindingId,
            legacy.PlatformAccountId,
            legacy.TraceId,
            legacy.IdempotencyKey,
            legacySha256,
            1,
            state.Revision,
            PolicyRuntimeStateCommitment.ComputeSha256(state),
            state.ReleaseBomSha256);
        using var fenceClient = CreateSubmissionClient(
            database,
            authorityTopology,
            fenceSigner,
            executorSigner,
            reconciliationSigner,
            recoverySigner,
            stateSigner);
        await AssertV1QuarantinedAsync(() => fenceClient.AcquireAsync(
            fenceRequest,
            SignFenceAuthorization(fenceSigner, fenceRequest),
            SignSubmissionIntent(executorSigner, LegacySubmissionIntent(fenceRequest)),
            cancellationToken));

        Assert.Equal(before, await database.CountOwnedRowsAsync(cancellationToken));
    }

    // Proves the refusal came from the one shared snapshot guard, not an earlier check.
    private static async Task AssertV1QuarantinedAsync(Func<Task> action)
    {
        var refused = await Assert.ThrowsAsync<UnauthorizedAccessException>(action);
        Assert.StartsWith("An action.proposal/v1 approval is quarantine-only", refused.Message, StringComparison.Ordinal);
    }

    private static ApprovalDecisionV1 LegacyDecision(
        string soulId,
        string deviceBindingId,
        string platformAccountId,
        string idempotencyKey,
        bool approved)
    {
        var canonicalIdempotencyKey = IdempotencyKey(idempotencyKey);
        return new ApprovalDecisionV1(
            ApprovalDecisionV1.CurrentSchemaVersion,
            ApprovalDecisionV1.CurrentContractId,
            ApprovalDecisionV1.CurrentProducerModule,
            Guid.NewGuid(),
            ActionProposalIdentity.Create(
                soulId,
                deviceBindingId,
                platformAccountId,
                canonicalIdempotencyKey),
            soulId,
            deviceBindingId,
            platformAccountId,
            TraceId(idempotencyKey),
            canonicalIdempotencyKey,
            DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
            "internal",
            "observe",
            false,
            !approved,
            new Dictionary<string, string>(),
            approved ? ApprovalDecisionV1.Approved : ApprovalDecisionV1.Denied,
            ApprovalDecisionV1.DeterministicAuthority,
            "1.0.0",
            DeterministicPolicyEvaluator.KnownPolicies.Order(StringComparer.Ordinal).ToArray(),
            PlatformAuthorization,
            approved ? [] : ["SHADOW_ONLY"]);
    }

    private static PolicyApprovalReadRequest LegacyReadRequest(ApprovalDecisionV1 decision, string decisionSha256)
        => new(
            decision.ApprovalId,
            decision.ProposalId,
            decision.SoulId,
            decision.DeviceBindingId,
            decision.PlatformAccountId,
            decision.TraceId,
            decision.IdempotencyKey,
            decisionSha256);

    private static ApprovalSubmissionIntentV1 LegacySubmissionIntent(ApprovalExecutionFenceRequestV1 fenceRequest)
        => new(
            ApprovalSubmissionIntentV1.CurrentSchemaVersion,
            ApprovalSubmissionIntentV1.CurrentContractId,
            ApprovalSubmissionIntentV1.CurrentProducerModule,
            ApprovalSubmissionIntentV1.CurrentAuthScope,
            Guid.NewGuid(),
            PolicyApprovalExecutionFenceBinding.ComputeRequestSha256(fenceRequest),
            fenceRequest.ApprovalId,
            fenceRequest.ProposalId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            fenceRequest.SoulId,
            fenceRequest.DeviceBindingId,
            fenceRequest.PlatformAccountId,
            fenceRequest.TraceId,
            fenceRequest.IdempotencyKey,
            fenceRequest.ApprovalSha256,
            new string('1', 64),
            fenceRequest.ExpectedStatusRevision,
            fenceRequest.ExpectedRuntimeRevision,
            fenceRequest.ExpectedRuntimeStateSha256,
            fenceRequest.ExpectedReleaseBomSha256,
            1,
            new string('e', 64),
            Sha256Hex("native:" + fenceRequest.ProposalId + ":1"),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMinutes(1),
            "internal",
            Convert.ToBase64String(new byte[64]));
}
