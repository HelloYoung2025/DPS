using System.Security.Cryptography;
using Dps.Planner.Contracts;
using Dps.PolicyApproval.Contracts;

namespace Dps.ControlPlaneHost;

/// <summary>
/// Configuration input for the shadow chain. The chain is closed unless the
/// caller explicitly clears <c>kill_control_plane_ingest</c>.
/// </summary>
public sealed record ControlPlaneShadowChainOptions(string ReleaseBomSha256)
{
    public bool KillControlPlaneIngest { get; init; } = true;
}

public sealed class ControlPlaneShadowChainKilledException()
    : InvalidOperationException("kill_control_plane_ingest is active; the Policy shadow chain is closed.");

/// <summary>
/// Host side of the non-production Planner → signed <c>approval.evaluation.request/v1</c>
/// → Policy Approval → exact <c>approval.readback/v1</c> shadow chain, built on contract
/// packs only. The Host relays the exact Planner <c>action.proposal/v2</c> bytes (producer
/// preserved), binds their v2 commitment into a SHADOW request it signs, and accepts only a
/// readback that binds that exact request. The proposal's Soul, Binding and account ids are
/// a SHADOW echo only, never an authorized identity. It never produces EXECUTE, a
/// promotion, or a v1 request. <see cref="HostStartup"/> never constructs this type; only
/// the test-only entry process does, injecting the Policy evaluator and the request signer.
/// </summary>
public sealed class ControlPlanePolicyShadowChain
{
    // Format-valid placeholder so the owner canonicalizer can validate the unsigned request.
    private static readonly string UnsignedPlaceholder = Convert.ToBase64String(new byte[64]);
    private readonly Func<ReadOnlyMemory<byte>, byte[], CancellationToken, Task<byte[]>> _evaluateAndReadExact;
    private readonly ECDsa _requestSigner;
    private readonly ControlPlaneShadowChainOptions _options;

    /// <param name="evaluateAndReadExact">
    /// Policy Approval's evaluation of (proposal wire, request wire), returning the exact
    /// reader's <c>approval.readback/v1</c> wire for the resulting approval.
    /// </param>
    public ControlPlanePolicyShadowChain(
        Func<ReadOnlyMemory<byte>, byte[], CancellationToken, Task<byte[]>> evaluateAndReadExact,
        ECDsa requestSigner,
        ControlPlaneShadowChainOptions options)
    {
        ArgumentNullException.ThrowIfNull(evaluateAndReadExact);
        ArgumentNullException.ThrowIfNull(requestSigner);
        ArgumentNullException.ThrowIfNull(options);
        ApprovalContractGuard.RequireSha256(options.ReleaseBomSha256, nameof(options.ReleaseBomSha256));
        _evaluateAndReadExact = evaluateAndReadExact;
        _requestSigner = requestSigner;
        _options = options;
    }

    /// <summary>
    /// Relays one Planner <c>action.proposal/v2</c> wire unchanged. Anything that is
    /// not a strict v2 proposal (v1, unknown major, malformed) fails closed here,
    /// before a request is signed or Policy is reached.
    /// </summary>
    public async Task<byte[]> RelayShadowAsync(ReadOnlyMemory<byte> proposalWire, CancellationToken cancellationToken)
    {
        if (_options.KillControlPlaneIngest) throw new ControlPlaneShadowChainKilledException();
        var proposal = ActionProposalV2Json.Deserialize(proposalWire.Span);
        var proposalSha256 = ActionProposalV2Canonical.ComputeSha256(proposal);
        var unsigned = new ApprovalEvaluationRequestV1(
            ApprovalEvaluationRequestV1.CurrentSchemaVersion,
            ApprovalEvaluationRequestV1.CurrentContractId,
            ApprovalEvaluationRequestV1.CurrentProducerModule,
            ApprovalEvaluationRequestV1.CurrentAuthScope,
            proposal.ProposalId,
            proposalSha256,
            proposal.SoulId,
            proposal.DeviceBindingId,
            proposal.PlatformAccountId,
            proposal.TraceId,
            proposal.IdempotencyKey,
            _options.ReleaseBomSha256,
            ApprovalEvaluationRequestV1.Shadow,
            null,
            proposal.OccurredAt,
            proposal.OccurredAt + ApprovalEvaluationRequestV1.Lifetime,
            "internal",
            UnsignedPlaceholder);
        var canonical = ApprovalEvaluationRequestV1Canonical.CanonicalBytes(unsigned);
        try
        {
            var signature = _requestSigner.SignData(
                canonical, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            var request = unsigned with { SignatureBase64 = Convert.ToBase64String(signature) };
            var wire = await _evaluateAndReadExact(
                proposalWire, ApprovalEvaluationRequestV1Codec.Serialize(request), cancellationToken);
            var readback = ApprovalReadbackV1Codec.Deserialize(wire);
            // An exact replay returns the originally persisted signature, so bind the
            // readback to this request's content rather than to this signature's bytes.
            if (readback.ProposalContractMajor != 2
                || !string.Equals(readback.ProposalSha256, proposalSha256, StringComparison.Ordinal)
                || readback.Approval.ProposalId != proposal.ProposalId
                || !string.Equals(readback.SoulId, proposal.SoulId, StringComparison.Ordinal)
                || !string.Equals(readback.DeviceBindingId, proposal.DeviceBindingId, StringComparison.Ordinal)
                || !string.Equals(readback.PlatformAccountId, proposal.PlatformAccountId, StringComparison.Ordinal)
                || !string.Equals(readback.TraceId, proposal.TraceId, StringComparison.Ordinal)
                || !string.Equals(readback.IdempotencyKey, proposal.IdempotencyKey, StringComparison.Ordinal)
                || !string.Equals(readback.ReleaseBomSha256, _options.ReleaseBomSha256, StringComparison.Ordinal)
                || !readback.Approval.ShadowOnly
                || readback.Approval.Decision != ApprovalDecisionV1.Denied
                || !_requestSigner.VerifyData(
                    canonical,
                    Convert.FromBase64String(readback.RequestSignature!),
                    HashAlgorithmName.SHA256,
                    DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
                throw new UnauthorizedAccessException("Policy readback does not bind the relayed SHADOW request.");
            return wire;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(canonical);
        }
    }
}
