using System.Security.Cryptography;
using Dps.Planner.Contracts;
using Dps.PolicyApproval.Contracts;

namespace Dps.PolicyApproval;

public sealed record PolicyRuntimeState(
    string SoulId,
    string DeviceBindingId,
    string PlatformAccountId,
    string PolicyVersion,
    IReadOnlySet<string> EnabledPolicyIds,
    bool KillSwitchEnabled,
    int RemainingRateBudget,
    bool PlatformAuthorized,
    string? PlatformAuthorizationId,
    DateTimeOffset EvaluatedAt,
    bool ExecutionEnabled = false,
    string ReleaseBomSha256 = "",
    long RuntimeRevision = 0,
    string RuntimeStateSha256 = "",
    DateTimeOffset RuntimeValidUntil = default);

public sealed record VerifiedPolicyEvaluationContext(
    string SoulId,
    string DeviceBindingId,
    string PlatformAccountId,
    string PolicyVersion,
    IReadOnlySet<string> EnabledPolicyIds,
    bool KillSwitchEnabled,
    int RemainingRateBudget,
    bool PlatformAuthorized,
    string? PlatformAuthorizationId,
    DateTimeOffset EvaluatedAt,
    string TrustEvidenceSha256,
    bool ExecutionAuthorized = false,
    string? ExecutionPromotionSha256 = null,
    string ReleaseBomSha256 = "",
    long RuntimeRevision = 0,
    string RuntimeStateSha256 = "",
    DateTimeOffset AuthorizationValidUntil = default,
    DateTimeOffset RuntimeValidUntil = default);

public interface IPolicyTrustProvider
{
    ValueTask<VerifiedPolicyEvaluationContext> ResolveVerifiedContextAsync(
        ActionProposalV2 proposal,
        ApprovalEvaluationRequestV1 request,
        ActionExecutionPromotionV1? promotion,
        CancellationToken cancellationToken);
}

public interface IPolicyRuntimeStateSource
{
    ValueTask<PolicyRuntimeState> ReadVerifiedStateAsync(ActionProposalV2 proposal, CancellationToken cancellationToken);
}

internal sealed record VerifiedPolicyEvaluationResult(
    ApprovalDecisionV1 Decision,
    VerifiedPolicyEvaluationContext Context);

internal static class PolicyEcdsaGuard
{
    internal const int P256SignatureByteLength = 64;
    private const int P256SignatureBase64Length = 88;
    private const int MaximumSubjectPublicKeyInfoLength = 512;

    internal static void ImportP256SubjectPublicKeyInfo(
        ECDsa key,
        ReadOnlySpan<byte> subjectPublicKeyInfo,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (subjectPublicKeyInfo.Length is < 1 or > MaximumSubjectPublicKeyInfoLength)
            throw new ArgumentException("ECDSA SubjectPublicKeyInfo must be a bounded P-256 key.", parameterName);
        key.ImportSubjectPublicKeyInfo(subjectPublicKeyInfo, out var bytesRead);
        if (bytesRead != subjectPublicKeyInfo.Length)
            throw new ArgumentException("ECDSA SubjectPublicKeyInfo contains trailing bytes.", parameterName);
        var parameters = key.ExportParameters(includePrivateParameters: false);
        if (key.KeySize != 256
            || !string.Equals(
                parameters.Curve.Oid.Value,
                ECCurve.NamedCurves.nistP256.Oid.Value,
                StringComparison.Ordinal))
            throw new ArgumentException("Only the NIST P-256 ECDSA curve is accepted.", parameterName);
    }

    internal static byte[] DecodeCanonicalP1363Signature(string? signatureBase64)
    {
        if (signatureBase64 is null || signatureBase64.Length != P256SignatureBase64Length)
            throw new UnauthorizedAccessException("ECDSA signature must be canonical Base64 for a 64-byte P-256 P1363 signature.");
        byte[] signature;
        try { signature = Convert.FromBase64String(signatureBase64); }
        catch (FormatException exception)
        {
            throw new UnauthorizedAccessException("ECDSA signature is not valid canonical Base64.", exception);
        }
        if (signature.Length != P256SignatureByteLength
            || !string.Equals(Convert.ToBase64String(signature), signatureBase64, StringComparison.Ordinal))
        {
            CryptographicOperations.ZeroMemory(signature);
            throw new UnauthorizedAccessException("ECDSA signature is not canonical P-256 P1363.");
        }
        return signature;
    }

    internal static void RequireDistinctPublicKeys(
        ECDsa first,
        string firstName,
        ECDsa second,
        string secondName)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        byte[]? firstEncoding = null;
        byte[]? secondEncoding = null;
        byte[]? firstFingerprint = null;
        byte[]? secondFingerprint = null;
        try
        {
            firstEncoding = first.ExportSubjectPublicKeyInfo();
            secondEncoding = second.ExportSubjectPublicKeyInfo();
            firstFingerprint = SHA256.HashData(firstEncoding);
            secondFingerprint = SHA256.HashData(secondEncoding);
            if (CryptographicOperations.FixedTimeEquals(
                    firstFingerprint,
                    secondFingerprint))
            {
                throw new ArgumentException(
                    $"{firstName} and {secondName} must use distinct P-256 authorities.",
                    secondName);
            }
        }
        finally
        {
            if (firstEncoding is not null)
                CryptographicOperations.ZeroMemory(firstEncoding);
            if (secondEncoding is not null)
                CryptographicOperations.ZeroMemory(secondEncoding);
            if (firstFingerprint is not null)
                CryptographicOperations.ZeroMemory(firstFingerprint);
            if (secondFingerprint is not null)
                CryptographicOperations.ZeroMemory(secondFingerprint);
        }
    }
}

/// <summary>
/// Authenticates one evaluation request before any database access: exact binding
/// to the v2 proposal commitment, the Control Plane request signature, and for
/// EXECUTE the independently signed promotion bound to that same commitment.
/// </summary>
internal sealed class PolicyEvaluationRequestVerifier : IDisposable
{
    private readonly object _sync = new();
    private readonly ECDsa _publicKey;
    private readonly ECDsa _promotionPublicKey;

    internal PolicyEvaluationRequestVerifier(
        ReadOnlySpan<byte> subjectPublicKeyInfo,
        ReadOnlySpan<byte> promotionSubjectPublicKeyInfo)
    {
        _publicKey = ECDsa.Create();
        _promotionPublicKey = ECDsa.Create();
        try
        {
            PolicyEcdsaGuard.ImportP256SubjectPublicKeyInfo(
                _publicKey, subjectPublicKeyInfo, nameof(subjectPublicKeyInfo));
            PolicyEcdsaGuard.ImportP256SubjectPublicKeyInfo(
                _promotionPublicKey,
                promotionSubjectPublicKeyInfo,
                nameof(promotionSubjectPublicKeyInfo));
            PolicyEcdsaGuard.RequireDistinctPublicKeys(
                _publicKey,
                nameof(subjectPublicKeyInfo),
                _promotionPublicKey,
                nameof(promotionSubjectPublicKeyInfo));
        }
        catch
        {
            _publicKey.Dispose();
            _promotionPublicKey.Dispose();
            throw;
        }
    }

    /// <summary>Returns the signature-free idempotency commitment of an authentic request.</summary>
    internal string Verify(
        ActionProposalV2 proposal,
        ApprovalEvaluationRequestV1 request,
        ActionExecutionPromotionV1? promotion)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        ArgumentNullException.ThrowIfNull(request);
        try { request.Validate(); }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            throw new UnauthorizedAccessException("Policy evaluation request contract is invalid.", exception);
        }
        var proposalSha256 = ActionProposalV2Canonical.ComputeSha256(proposal);
        if (request.ProposalId != proposal.ProposalId
            || !FixedSha256Equals(request.ProposalSha256, proposalSha256)
            || !string.Equals(request.SoulId, proposal.SoulId, StringComparison.Ordinal)
            || !string.Equals(request.DeviceBindingId, proposal.DeviceBindingId, StringComparison.Ordinal)
            || !string.Equals(request.PlatformAccountId, proposal.PlatformAccountId, StringComparison.Ordinal)
            || !string.Equals(request.TraceId, proposal.TraceId, StringComparison.Ordinal)
            || !string.Equals(request.IdempotencyKey, proposal.IdempotencyKey, StringComparison.Ordinal)
            || request.OccurredAt != proposal.OccurredAt)
            throw new UnauthorizedAccessException("Policy evaluation request is not bound to the exact v2 proposal commitment, scope, and occurrence time.");
        VerifySignature(
            _publicKey,
            ApprovalEvaluationRequestV1Canonical.CanonicalBytes(request),
            request.SignatureBase64,
            "Policy authorization signature verification failed.");
        if (request.Mode == ApprovalEvaluationRequestV1.Shadow)
        {
            if (promotion is not null)
                throw new UnauthorizedAccessException("A SHADOW evaluation cannot carry an execution promotion.");
        }
        else
        {
            if (promotion is null)
                throw new UnauthorizedAccessException("An EXECUTE evaluation requires its bound execution promotion.");
            try { promotion.Validate(); }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or InvalidOperationException)
            {
                throw new UnauthorizedAccessException("Execution promotion contract is invalid.", exception);
            }
            if (!FixedSha256Equals(
                    request.ExecutionPromotionSha256!,
                    ApprovalEvaluationRequestV1Canonical.ComputeExecutionPromotionSha256(promotion))
                || promotion.ProposalId != proposal.ProposalId
                || !string.Equals(promotion.SoulId, proposal.SoulId, StringComparison.Ordinal)
                || !string.Equals(promotion.DeviceBindingId, proposal.DeviceBindingId, StringComparison.Ordinal)
                || !string.Equals(promotion.PlatformAccountId, proposal.PlatformAccountId, StringComparison.Ordinal)
                || !string.Equals(promotion.TraceId, proposal.TraceId, StringComparison.Ordinal)
                || !string.Equals(promotion.IdempotencyKey, proposal.IdempotencyKey, StringComparison.Ordinal)
                || !FixedSha256Equals(promotion.ProposalSha256, proposalSha256)
                || !FixedSha256Equals(promotion.ReleaseBomSha256, request.ReleaseBomSha256)
                || promotion.OccurredAt < proposal.OccurredAt)
                throw new UnauthorizedAccessException("Execution promotion is not bound to the exact v2 proposal commitment, request, and Release BOM.");
            VerifySignature(
                _promotionPublicKey,
                ActionExecutionPromotionV1Canonical.CanonicalBytes(promotion),
                promotion.SignatureBase64,
                "Independent execution-promotion signature verification failed.");
        }
        return ApprovalEvaluationRequestV1Canonical.ComputeCommitmentSha256(request);
    }

    private void VerifySignature(ECDsa key, byte[] canonical, string signatureBase64, string failure)
    {
        byte[]? signature = null;
        try
        {
            signature = PolicyEcdsaGuard.DecodeCanonicalP1363Signature(signatureBase64);
            bool verified;
            lock (_sync)
                verified = key.VerifyData(
                    canonical,
                    signature,
                    HashAlgorithmName.SHA256,
                    DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            if (!verified) throw new UnauthorizedAccessException(failure);
        }
        finally
        {
            if (signature is not null) CryptographicOperations.ZeroMemory(signature);
            CryptographicOperations.ZeroMemory(canonical);
        }
    }

    internal static bool FixedSha256Equals(string left, string right)
    {
        byte[]? leftBytes = null;
        byte[]? rightBytes = null;
        try
        {
            leftBytes = Convert.FromHexString(left);
            rightBytes = Convert.FromHexString(right);
            return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
        }
        catch (FormatException) { return false; }
        finally
        {
            if (leftBytes is not null) CryptographicOperations.ZeroMemory(leftBytes);
            if (rightBytes is not null) CryptographicOperations.ZeroMemory(rightBytes);
        }
    }

    public void Dispose()
    {
        _publicKey.Dispose();
        _promotionPublicKey.Dispose();
    }
}

public sealed class DeterministicPolicyEvaluator
{
    private readonly IPolicyTrustProvider _trustProvider;

    public DeterministicPolicyEvaluator(IPolicyTrustProvider trustProvider)
    {
        ArgumentNullException.ThrowIfNull(trustProvider);
        _trustProvider = trustProvider;
    }

    public static readonly IReadOnlySet<string> KnownPolicies = new HashSet<string>(
        ["SOUL-ISO-001", "CMD-IDEMP-001", "RESULT-VERIFY-001", "GBRAIN-READBACK-001", "EDGE-NORESTART-001"],
        StringComparer.Ordinal);

    public async ValueTask<ApprovalDecisionV1> EvaluateAsync(
        ActionProposalV2 proposal,
        ApprovalEvaluationRequestV1 request,
        ActionExecutionPromotionV1? promotion = null,
        CancellationToken cancellationToken = default)
        => (await EvaluateVerifiedAsync(proposal, request, promotion, cancellationToken)).Decision;

    internal async ValueTask<VerifiedPolicyEvaluationResult> EvaluateVerifiedAsync(
        ActionProposalV2 proposal,
        ApprovalEvaluationRequestV1 request,
        ActionExecutionPromotionV1? promotion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        ArgumentNullException.ThrowIfNull(request);
        proposal = PolicyCanonicalization.SnapshotProposal(proposal);
        if (string.IsNullOrWhiteSpace(request.SignatureBase64)) throw new UnauthorizedAccessException("Authenticated Control Plane authorization is required.");
        var context = await _trustProvider.ResolveVerifiedContextAsync(proposal, request, promotion, cancellationToken);
        ArgumentNullException.ThrowIfNull(context);
        try { PolicyCanonicalization.RequireSha256(context.TrustEvidenceSha256, nameof(context.TrustEvidenceSha256)); }
        catch (ArgumentException exception) { throw new UnauthorizedAccessException("Trusted policy context evidence is invalid.", exception); }
        if ((promotion is null) != (context.ExecutionPromotionSha256 is null))
            throw new UnauthorizedAccessException("Trusted policy context promotion evidence does not match the signed request.");
        if (context.ExecutionPromotionSha256 is not null)
        {
            try { PolicyCanonicalization.RequireSha256(context.ExecutionPromotionSha256, nameof(context.ExecutionPromotionSha256)); }
            catch (ArgumentException exception) { throw new UnauthorizedAccessException("Trusted execution-promotion evidence is invalid.", exception); }
        }
        if (context.ExecutionAuthorized && promotion is null)
            throw new UnauthorizedAccessException("Execution cannot be authorized without an independent promotion contract.");
        ApprovalContractGuard.RequireUtc(context.EvaluatedAt, nameof(context.EvaluatedAt));
        if (!string.Equals(proposal.SoulId, context.SoulId, StringComparison.Ordinal) || !string.Equals(proposal.DeviceBindingId, context.DeviceBindingId, StringComparison.Ordinal) || !string.Equals(proposal.PlatformAccountId, context.PlatformAccountId, StringComparison.Ordinal)) throw new UnauthorizedAccessException("SOUL-ISO-001: proposal and policy scope differ.");
        var enabledPolicies = new HashSet<string>(context.EnabledPolicyIds, StringComparer.Ordinal);
        if (enabledPolicies.Count != context.EnabledPolicyIds.Count) throw new UnauthorizedAccessException("Trusted policy context exposes inconsistent policy cardinality.");
        if (enabledPolicies.Any(policy => !KnownPolicies.Contains(policy))) throw new NotSupportedException("Unknown policy ID fails closed.");

        var reasons = new List<string>();
        if (!KnownPolicies.IsSubsetOf(enabledPolicies)) reasons.Add("REQUIRED_POLICY_DISABLED");
        var promoted = context.ExecutionAuthorized && promotion is not null;
        if (proposal.ShadowOnly && !promoted) reasons.Add("SHADOW_ONLY");
        if (context.KillSwitchEnabled) reasons.Add("KILL_SWITCH_ACTIVE");
        if (context.RemainingRateBudget <= 0) reasons.Add("RATE_BUDGET_EXHAUSTED");
        if (proposal.IsSideEffect && (!context.PlatformAuthorized || string.IsNullOrWhiteSpace(context.PlatformAuthorizationId))) reasons.Add("PLATFORM_AUTHORIZATION_REQUIRED");
        var decision = reasons.Count == 0 ? ApprovalDecisionV1.Approved : ApprovalDecisionV1.Denied;
        var approval = new ApprovalDecisionV1(
            ApprovalDecisionV1.CurrentSchemaVersion,
            ApprovalDecisionV1.CurrentContractId,
            ApprovalDecisionV1.CurrentProducerModule,
            DeterministicApprovalId(proposal, context, decision),
            proposal.ProposalId,
            proposal.SoulId,
            proposal.DeviceBindingId,
            proposal.PlatformAccountId,
            proposal.TraceId,
            proposal.IdempotencyKey,
            context.EvaluatedAt,
            "internal",
            proposal.ActionKind,
            proposal.IsSideEffect,
            proposal.ShadowOnly && !promoted,
            new Dictionary<string, string>(proposal.Parameters, StringComparer.Ordinal),
            decision,
            ApprovalDecisionV1.DeterministicAuthority,
            context.PolicyVersion,
            enabledPolicies.Order(StringComparer.Ordinal).ToArray(),
            context.PlatformAuthorized ? context.PlatformAuthorizationId : null,
            reasons.Order(StringComparer.Ordinal).ToArray());
        approval.Validate();
        return new VerifiedPolicyEvaluationResult(
            PolicyCanonicalization.SnapshotDecision(approval),
            context);
    }

    private static Guid DeterministicApprovalId(
        ActionProposalV2 proposal,
        VerifiedPolicyEvaluationContext context,
        string decision)
    {
        var digestHex = PolicyCanonicalHash.Compute(writer =>
        {
            writer.Field("dps.policy-approval.approval-id/v1");
            writer.Field(proposal.ProposalId);
            writer.Field(proposal.SoulId);
            writer.Field(proposal.DeviceBindingId);
            writer.Field(proposal.PlatformAccountId);
            writer.Field(proposal.TraceId);
            writer.Field(proposal.IdempotencyKey);
            writer.Field(context.PolicyVersion);
            writer.Field(context.TrustEvidenceSha256);
            writer.Field(context.ExecutionAuthorized);
            writer.NullableField(context.ExecutionPromotionSha256);
            writer.NullableField(context.PlatformAuthorizationId);
            writer.Field(decision);
        });
        var digest = Convert.FromHexString(digestHex);
        try { return new Guid(digest.AsSpan(0, 16)); }
        finally { CryptographicOperations.ZeroMemory(digest); }
    }
}

public sealed class EcdsaPolicyTrustProvider : IPolicyTrustProvider, IDisposable
{
    private readonly PolicyEvaluationRequestVerifier _verifier;
    private readonly IPolicyRuntimeStateSource _runtimeStateSource;

    public EcdsaPolicyTrustProvider(
        ReadOnlySpan<byte> subjectPublicKeyInfo,
        ReadOnlySpan<byte> promotionSubjectPublicKeyInfo,
        IPolicyRuntimeStateSource runtimeStateSource)
    {
        ArgumentNullException.ThrowIfNull(runtimeStateSource);
        _runtimeStateSource = runtimeStateSource;
        _verifier = new PolicyEvaluationRequestVerifier(
            subjectPublicKeyInfo,
            promotionSubjectPublicKeyInfo);
    }

    public async ValueTask<VerifiedPolicyEvaluationContext> ResolveVerifiedContextAsync(
        ActionProposalV2 proposal,
        ApprovalEvaluationRequestV1 request,
        ActionExecutionPromotionV1? promotion,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        proposal = PolicyCanonicalization.SnapshotProposal(proposal);
        _ = _verifier.Verify(proposal, request, promotion);
        byte[]? canonical = null;
        try
        {
            canonical = ApprovalEvaluationRequestV1Canonical.CanonicalBytes(request);
            var state = await _runtimeStateSource.ReadVerifiedStateAsync(proposal, cancellationToken);
            ArgumentNullException.ThrowIfNull(state);
            ApprovalContractGuard.RequireUtc(state.EvaluatedAt, nameof(state.EvaluatedAt));
            ApprovalContractGuard.RequireUtc(state.RuntimeValidUntil, nameof(state.RuntimeValidUntil));
            PolicyCanonicalization.RequireSha256(state.ReleaseBomSha256, nameof(state.ReleaseBomSha256));
            PolicyCanonicalization.RequireSha256(state.RuntimeStateSha256, nameof(state.RuntimeStateSha256));
            if (state.RuntimeRevision <= 0) throw new UnauthorizedAccessException("Trusted policy runtime revision is invalid.");
            if (state.EvaluatedAt >= request.ValidUntil)
                throw new UnauthorizedAccessException("Policy authorization expired before trusted database time.");
            if (state.EvaluatedAt >= state.RuntimeValidUntil)
                throw new UnauthorizedAccessException("Policy runtime state expired before trusted database time.");
            if (!PolicyEvaluationRequestVerifier.FixedSha256Equals(request.ReleaseBomSha256, state.ReleaseBomSha256)) throw new UnauthorizedAccessException("The signed release BOM is not the active policy runtime BOM.");
            var policies = new HashSet<string>(state.EnabledPolicyIds, StringComparer.Ordinal);
            if (policies.Count != state.EnabledPolicyIds.Count) throw new UnauthorizedAccessException("Trusted policy state exposes inconsistent policy cardinality.");
            string? executionPromotionSha256 = null;
            var authorizationValidUntil = request.ValidUntil;
            if (promotion is not null)
            {
                if (promotion.ExpectedRuntimeRevision != state.RuntimeRevision
                    || promotion.OccurredAt > state.EvaluatedAt
                    || state.EvaluatedAt >= promotion.ValidUntil)
                    throw new UnauthorizedAccessException("Execution promotion is not bound to the exact runtime generation and database time.");
                executionPromotionSha256 = ActionExecutionPromotionV1Canonical.ComputeSignedSha256(promotion);
                if (promotion.ValidUntil < authorizationValidUntil)
                    authorizationValidUntil = promotion.ValidUntil;
            }
            var executionAuthorized = executionPromotionSha256 is not null && state.ExecutionEnabled;
            var evidence = PolicyCanonicalHash.Compute(writer =>
            {
                writer.Field("dps.policy-approval.trust-evidence-sha256/v1");
                writer.Field(canonical);
                writer.Field(state.SoulId);
                writer.Field(state.DeviceBindingId);
                writer.Field(state.PlatformAccountId);
                writer.Field(state.PolicyVersion);
                writer.Field(policies.Count);
                foreach (var policy in policies.Order(StringComparer.Ordinal)) writer.Field(policy);
                writer.Field(state.KillSwitchEnabled);
                writer.Field(state.RemainingRateBudget);
                writer.Field(state.PlatformAuthorized);
                writer.NullableField(state.PlatformAuthorizationId);
                writer.Field(state.EvaluatedAt);
                writer.Field(state.ExecutionEnabled);
                writer.Field(state.ReleaseBomSha256);
                writer.Field(state.RuntimeRevision);
                writer.Field(state.RuntimeStateSha256);
                writer.Field(state.RuntimeValidUntil);
                writer.Field(executionAuthorized);
                writer.NullableField(executionPromotionSha256);
            });
            return new VerifiedPolicyEvaluationContext(
                state.SoulId,
                state.DeviceBindingId,
                state.PlatformAccountId,
                state.PolicyVersion,
                policies,
                state.KillSwitchEnabled,
                state.RemainingRateBudget,
                state.PlatformAuthorized,
                state.PlatformAuthorizationId,
                state.EvaluatedAt,
                evidence,
                executionAuthorized,
                executionPromotionSha256,
                state.ReleaseBomSha256,
                state.RuntimeRevision,
                state.RuntimeStateSha256,
                authorizationValidUntil,
                state.RuntimeValidUntil);
        }
        finally
        {
            if (canonical is not null) CryptographicOperations.ZeroMemory(canonical);
        }
    }

    public void Dispose() => _verifier.Dispose();
}
