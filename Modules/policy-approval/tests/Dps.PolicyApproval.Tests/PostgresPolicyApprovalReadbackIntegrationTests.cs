using System.Security.Cryptography;
using Dps.Planner.Contracts;
using Dps.PolicyApproval.Contracts;
using Xunit;

namespace Dps.PolicyApproval.Tests;

public sealed partial class PostgresPolicyApprovalIntegrationTests
{
    [Fact, Trait("Category", "Integration")]
    public async Task ExactReaderProjectsApprovalReadbackV1WithPersistedSignatureCommitmentAndMajor()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var database = await PolicyApprovalTestDatabase.CreateAsync(
            cancellationToken,
            applyProductMigrations: false);
        var state = State(SoulA, DeviceA, AccountA);
        await database.AppendRuntimeStateAsync(state, cancellationToken);
        var approvedLegacy = LegacyDecision(SoulA, DeviceA, AccountA, "idem-readback-v1-approved", approved: true);
        var deniedLegacy = LegacyDecision(SoulA, DeviceA, AccountA, "idem-readback-v1-denied", approved: false);
        var approvedLegacySha256 = await database.InsertLegacyDecisionAsync(approvedLegacy, state, cancellationToken);
        var deniedLegacySha256 = await database.InsertLegacyDecisionAsync(deniedLegacy, state, cancellationToken);
        await database.MigrateAsync(cancellationToken);
        using var evaluationSigner = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var revocationSigner = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var service = database.CreateService(evaluationSigner, revocationSigner);
        var reader = new PolicyApprovalAuthoritativeClient(database.Options);

        foreach (var execute in new[] { false, true })
        {
            var evaluation = SignEvaluation(
                evaluationSigner,
                Proposal(SoulA, DeviceA, AccountA, execute ? "idem-readback-v2-execute" : "idem-readback-v2-shadow"),
                execute: execute);
            var issued = await service.EvaluateSignedAsync(evaluation, cancellationToken);
            var stored = await database.ReadDecisionColumnsAsync(issued.Snapshot.Approval.ApprovalId, cancellationToken);
            var readback = (await reader.ReadAsync(ReadRequest(issued.Snapshot), cancellationToken)).ToReadbackV1();

            // The exact reader, not the caller, supplies every bound value.
            Assert.Equal(ApprovalReadbackV1Codec.Serialize(issued.Snapshot.ToReadbackV1()), ApprovalReadbackV1Codec.Serialize(readback));
            Assert.Equal(2, readback.ProposalContractMajor);
            Assert.Equal(ActionProposalV2Canonical.ComputeSha256(evaluation.Proposal), readback.ProposalSha256);
            Assert.Equal(stored.RequestSignature, readback.RequestSignature);
            Assert.Equal(evaluation.Request.SignatureBase64, readback.RequestSignature);
            Assert.Equal(PolicyApprovalDecisionCanonical.ComputeSha256(readback.Approval), readback.ApprovalSha256);
            Assert.Equal(execute, readback.Approval.Decision == ApprovalDecisionV1.Approved && !readback.Approval.ShadowOnly);
            var wire = ApprovalReadbackV1Codec.Serialize(readback);
            Assert.Equal(wire, ApprovalReadbackV1Codec.Serialize(ApprovalReadbackV1Codec.Deserialize(wire)));
            Assert.Equal(
                ApprovalReadbackV1Canonical.ComputeSha256(readback),
                ApprovalReadbackV1Canonical.ComputeSha256(ApprovalReadbackV1Codec.Deserialize(wire)));
        }

        // A historical DENIED v1 row stays readable, without a signature; an ACTIVE
        // executable v1 row is still refused by the shared snapshot guard before
        // any readback can be projected, and becomes readable only once REVOKED.
        var denied = (await reader.ReadAsync(LegacyReadRequest(deniedLegacy, deniedLegacySha256), cancellationToken)).ToReadbackV1();
        Assert.Equal(1, denied.ProposalContractMajor);
        Assert.Null(denied.RequestSignature);
        var deniedWire = ApprovalReadbackV1Codec.Serialize(denied);
        Assert.Equal(deniedWire, ApprovalReadbackV1Codec.Serialize(ApprovalReadbackV1Codec.Deserialize(deniedWire)));
        await AssertV1QuarantinedAsync(
            () => reader.ReadAsync(LegacyReadRequest(approvedLegacy, approvedLegacySha256), cancellationToken));
        var revoke = new PolicyApprovalRevocationRequest(
            approvedLegacy.ApprovalId,
            approvedLegacy.ProposalId,
            SoulA,
            DeviceA,
            AccountA,
            TraceId("revoke-readback"),
            IdempotencyKey("revoke-readback"),
            approvedLegacySha256,
            1);
        await service.RevokeAsync(revoke, SignRevocation(revocationSigner, revoke), cancellationToken);
        var revoked = (await reader.ReadAsync(LegacyReadRequest(approvedLegacy, approvedLegacySha256), cancellationToken)).ToReadbackV1();
        Assert.Equal(ApprovalReadbackV1.Revoked, revoked.Status);
        Assert.Null(revoked.RequestSignature);
        Assert.Throws<NotSupportedException>(() => (revoked with { Status = ApprovalReadbackV1.Active }).Validate());
    }
}
