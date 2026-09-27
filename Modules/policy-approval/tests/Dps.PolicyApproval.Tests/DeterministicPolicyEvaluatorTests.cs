using Dps.Planner.Contracts;
using Dps.PolicyApproval.Contracts;
using System.Security.Cryptography;
using Xunit;

namespace Dps.PolicyApproval.Tests;

public sealed class DeterministicPolicyEvaluatorTests
{
    private const string Soul = "soul_cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
    private const string Device = "db_cccccccccccccccccccccccccccccccc";
    private const string OtherDevice = "db_dddddddddddddddddddddddddddddddd";
    private const string Account = "pa_cccccccccccccccccccccccccccccccc";
    private const string OtherAccount = "pa_dddddddddddddddddddddddddddddddd";
    private const string Trace = "trace_cccccccccccccccccccccccccccccccc";
    private const string Idempotency = "idem_cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
    private static readonly string Selector = "selector_" + new string('a', 64);
    private static readonly string Evidence = "evidence_" + new string('e', 64);
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ShadowProposalIsDeniedEvenWhenTrustedChecksPass()
    {
        var result = await Evaluate(Evaluator(Context()), Proposal(), Request());
        Assert.Equal(ApprovalDecisionV1.Denied, result.Decision);
        Assert.Contains("SHADOW_ONLY", result.DenialReasons);
    }

    [Theory]
    [InlineData(true, 1, "KILL_SWITCH_ACTIVE")]
    [InlineData(false, 0, "RATE_BUDGET_EXHAUSTED")]
    [Trait("Category", "Unit")]
    public async Task KillSwitchAndRateBudgetFailClosed(bool killSwitch, int budget, string reason)
    {
        var result = await Evaluate(Evaluator(Context() with { KillSwitchEnabled = killSwitch, RemainingRateBudget = budget }), Proposal(), Request());
        Assert.Contains(reason, result.DenialReasons);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task SideEffectWithoutTrustedPlatformAuthorizationIsDenied()
    {
        var proposal = Proposal("fixture.tap", true);
        var result = await Evaluate(Evaluator(Context() with { PlatformAuthorized = false, PlatformAuthorizationId = null }), proposal, Request(proposal));
        Assert.Contains("PLATFORM_AUTHORIZATION_REQUIRED", result.DenialReasons);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task OnlyTrustedExecutionPromotionCanProduceNonShadowApproval()
    {
        var proposal = Proposal();
        var promotion = Promotion(proposal);
        var promotionEvidence = ActionExecutionPromotionV1Canonical.ComputeSignedSha256(promotion);
        var promoted = await Evaluate(
            Evaluator(Context() with
            {
                ExecutionAuthorized = true,
                ExecutionPromotionSha256 = promotionEvidence
            }),
            proposal,
            Request(proposal, promotion),
            promotion);
        Assert.Equal(ApprovalDecisionV1.Approved, promoted.Decision);
        Assert.False(promoted.ShadowOnly);

        var denied = await Evaluate(
            Evaluator(Context() with { ExecutionPromotionSha256 = promotionEvidence }),
            proposal,
            Request(proposal, promotion),
            promotion);
        Assert.Equal(ApprovalDecisionV1.Denied, denied.Decision);
        Assert.True(denied.ShadowOnly);
        Assert.Contains("SHADOW_ONLY", denied.DenialReasons);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
        {
            await Evaluate(
                Evaluator(Context() with
                {
                    ExecutionAuthorized = true,
                    ExecutionPromotionSha256 = promotionEvidence
                }),
                proposal,
                Request(proposal));
        });
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ModelOrUnauthenticatedCallerCannotSelfReportApprovalContext()
    {
        var provider = new FakeTrustProvider(Context(), authorized: false);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => { await Evaluate(new DeterministicPolicyEvaluator(provider), Proposal(), Request() with { ProducerModule = "model" }); });
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => { await Evaluate(Evaluator(Context()), Proposal(), Request() with { SignatureBase64 = "" }); });
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task UnknownPolicyFailsClosed()
    {
        var policies = new HashSet<string>(DeterministicPolicyEvaluator.KnownPolicies, StringComparer.Ordinal) { "UNKNOWN-POLICY-999" };
        await Assert.ThrowsAsync<NotSupportedException>(async () => { await Evaluate(Evaluator(Context() with { EnabledPolicyIds = policies }), Proposal(), Request()); });
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CrossSoulDeviceAndAccountTrustedContextIsRejected()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => { await Evaluate(Evaluator(Context() with { SoulId = "soul_dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd" }), Proposal(), Request()); });
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => { await Evaluate(Evaluator(Context() with { DeviceBindingId = OtherDevice }), Proposal(), Request()); });
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => { await Evaluate(Evaluator(Context() with { PlatformAccountId = OtherAccount }), Proposal(), Request()); });
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task InvalidIdentityOrTrustEvidenceCannotCrossPolicyBoundary()
    {
        await Assert.ThrowsAsync<ArgumentException>(async () => { await Evaluate(Evaluator(Context()), Proposal() with { SoulId = Guid.NewGuid().ToString() }, Request()); });
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => { await Evaluate(Evaluator(Context() with { TrustEvidenceSha256 = "forged" }), Proposal(), Request()); });
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task ForgedModelAuthorityAndUnknownMajorAreRejected()
    {
        var denied = await Evaluate(Evaluator(Context()), Proposal(), Request());
        Assert.Throws<NotSupportedException>(() => (denied with { Authority = "model" }).Validate());
        Assert.Throws<NotSupportedException>(() => (denied with { SchemaVersion = "2.0.0" }).Validate());
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task RealEcdsaTrustProviderBindsCallerProposalAndSignedBom()
    {
        var proposal = Proposal(); using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256); using var promoter = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var signed = SignRequest(signer, Request(proposal));
        using var provider = new EcdsaPolicyTrustProvider(signer.ExportSubjectPublicKeyInfo(), promoter.ExportSubjectPublicKeyInfo(), new FakeRuntimeStateSource(RuntimeState()));
        var result = await Evaluate(new DeterministicPolicyEvaluator(provider), proposal, signed); Assert.Equal(ApprovalDecisionV1.Denied, result.Decision); Assert.Contains("SHADOW_ONLY", result.DenialReasons);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => { await Evaluate(new DeterministicPolicyEvaluator(provider), proposal, signed with { ReleaseBomSha256 = new string('b', 64) }); });
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => { await Evaluate(new DeterministicPolicyEvaluator(provider), proposal, SignRequest(signer, Request(proposal) with { ReleaseBomSha256 = new string('b', 64) })); });
        await Assert.ThrowsAsync<ArgumentException>(async () => { await Evaluate(new DeterministicPolicyEvaluator(provider), proposal with { SoulId = "soul_ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff" }, signed); });
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task SignedRequestBindsExactV2CommitmentScopeOccurrenceAndLifetime()
    {
        var proposal = Proposal();
        using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var promoter = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var provider = new EcdsaPolicyTrustProvider(signer.ExportSubjectPublicKeyInfo(), promoter.ExportSubjectPublicKeyInfo(), new FakeRuntimeStateSource(RuntimeState()));
        var evaluator = new DeterministicPolicyEvaluator(provider);
        var mismatches = new[]
        {
            Request(proposal) with { ProposalSha256 = new string('c', 64) },
            Request(proposal) with { TraceId = "trace_dddddddddddddddddddddddddddddddd" },
            Request(proposal) with { IdempotencyKey = "idem_dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd" },
            Request(proposal) with { DeviceBindingId = OtherDevice },
            Request(proposal) with { OccurredAt = Now.AddSeconds(1), ValidUntil = Now.AddSeconds(1) + ApprovalEvaluationRequestV1.Lifetime }
        };
        foreach (var mismatch in mismatches)
            await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => { await Evaluate(evaluator, proposal, SignRequest(signer, mismatch)); });

        var outOfLifetime = Request(proposal) with { ValidUntil = Now.AddMinutes(5) };
        Assert.Throws<ArgumentException>(() => outOfLifetime.Validate());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => { await Evaluate(evaluator, proposal, outOfLifetime); });

        using var expired = new EcdsaPolicyTrustProvider(signer.ExportSubjectPublicKeyInfo(), promoter.ExportSubjectPublicKeyInfo(), new FakeRuntimeStateSource(RuntimeState() with { EvaluatedAt = Now + ApprovalEvaluationRequestV1.Lifetime, RuntimeValidUntil = Now.AddMinutes(10) }));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => { await Evaluate(new DeterministicPolicyEvaluator(expired), proposal, SignRequest(signer, Request(proposal))); });
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task IndependentPromotionBindsExactProposalRuntimeBomAndSignature()
    {
        var proposal = Proposal();
        using var evaluationSigner = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var promotionSigner = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var provider = new EcdsaPolicyTrustProvider(
            evaluationSigner.ExportSubjectPublicKeyInfo(),
            promotionSigner.ExportSubjectPublicKeyInfo(),
            new FakeRuntimeStateSource(RuntimeState() with { ExecutionEnabled = true }));
        var evaluator = new DeterministicPolicyEvaluator(provider);
        var promotion = SignPromotion(promotionSigner, Promotion(proposal));
        var executeRequest = SignRequest(evaluationSigner, Request(proposal, promotion));
        var approved = await Evaluate(evaluator, proposal, executeRequest, promotion);
        Assert.Equal(ApprovalDecisionV1.Approved, approved.Decision);
        Assert.False(approved.ShadowOnly);

        // The request binds the unsigned promotion payload: a fresh signature over
        // the same promotion is the same authority, never a different request.
        var resigned = SignPromotion(promotionSigner, promotion);
        Assert.NotEqual(promotion.SignatureBase64, resigned.SignatureBase64);
        Assert.Equal(ApprovalDecisionV1.Approved, (await Evaluate(evaluator, proposal, executeRequest, resigned)).Decision);

        using var disabledProvider = new EcdsaPolicyTrustProvider(
            evaluationSigner.ExportSubjectPublicKeyInfo(),
            promotionSigner.ExportSubjectPublicKeyInfo(),
            new FakeRuntimeStateSource(RuntimeState()));
        var disabled = await Evaluate(new DeterministicPolicyEvaluator(disabledProvider), proposal, executeRequest, promotion);
        Assert.Equal(ApprovalDecisionV1.Denied, disabled.Decision);
        Assert.True(disabled.ShadowOnly);
        Assert.Contains("SHADOW_ONLY", disabled.DenialReasons);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => { await Evaluate(evaluator, proposal, executeRequest); });
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => { await Evaluate(evaluator, proposal, SignRequest(evaluationSigner, Request(proposal)), promotion); });
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => { await Evaluate(evaluator, proposal, executeRequest, SignPromotion(evaluationSigner, Promotion(proposal))); });

        var tamperedPromotion = promotion with { ReleaseApprovalId = Guid.NewGuid() };
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => { await Evaluate(evaluator, proposal, executeRequest, tamperedPromotion); });
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
        {
            await Evaluate(evaluator, proposal, SignRequest(evaluationSigner, Request(proposal, tamperedPromotion)), tamperedPromotion);
        });
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task EcdsaBoundaryRejectsP384DerAndOversizedSignatures()
    {
        using var p384 = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        Assert.Throws<ArgumentException>(() =>
            new EcdsaPolicyTrustProvider(
                p384.ExportSubjectPublicKeyInfo(),
                p384.ExportSubjectPublicKeyInfo(),
                new FakeRuntimeStateSource(RuntimeState())));
        Assert.Throws<ArgumentException>(() =>
            new EcdsaPolicyRevocationAuthorizer(p384.ExportSubjectPublicKeyInfo()));

        using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        Assert.Throws<ArgumentException>(() =>
            new EcdsaPolicyTrustProvider(
                signer.ExportSubjectPublicKeyInfo(),
                signer.ExportSubjectPublicKeyInfo(),
                new FakeRuntimeStateSource(RuntimeState())));
        using var promotionSigner = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var serviceRevocationSigner = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var fenceSigner = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var executorSigner = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var reconciliationSigner = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var recoverySigner = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var stateSigner = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var authorityTopology = PolicyApprovalSubmissionAuthorityTopology.Create(
            signer.ExportSubjectPublicKeyInfo(),
            promotionSigner.ExportSubjectPublicKeyInfo(),
            serviceRevocationSigner.ExportSubjectPublicKeyInfo(),
            fenceSigner.ExportSubjectPublicKeyInfo(),
            executorSigner.ExportSubjectPublicKeyInfo(),
            reconciliationSigner.ExportSubjectPublicKeyInfo(),
            recoverySigner.ExportSubjectPublicKeyInfo(),
            stateSigner.ExportSubjectPublicKeyInfo());
        using var provider = new EcdsaPolicyTrustProvider(
            signer.ExportSubjectPublicKeyInfo(),
            promotionSigner.ExportSubjectPublicKeyInfo(),
            new FakeRuntimeStateSource(RuntimeState()));
        Assert.Throws<UnauthorizedAccessException>(() =>
            PostgresPolicyApprovalService.CreateProduction(
                new PostgresPolicyApprovalOptions(
                    "Host=localhost;Database=dps_test;Username=dps_policy_runtime;Pooling=false",
                    "dps_policy_test",
                    "dps_policy_runtime"),
                authorityTopology,
                signer.ExportSubjectPublicKeyInfo(),
                promotionSigner.ExportSubjectPublicKeyInfo(),
                signer.ExportSubjectPublicKeyInfo()));
        Assert.Throws<ArgumentException>(() => PolicyApprovalSubmissionAuthorityTopology.Create(
            signer.ExportSubjectPublicKeyInfo(),
            promotionSigner.ExportSubjectPublicKeyInfo(),
            serviceRevocationSigner.ExportSubjectPublicKeyInfo(),
            signer.ExportSubjectPublicKeyInfo(),
            executorSigner.ExportSubjectPublicKeyInfo(),
            reconciliationSigner.ExportSubjectPublicKeyInfo(),
            recoverySigner.ExportSubjectPublicKeyInfo(),
            stateSigner.ExportSubjectPublicKeyInfo()));
        var evaluator = new DeterministicPolicyEvaluator(provider);
        var unsigned = Request();
        var canonical = ApprovalEvaluationRequestV1Canonical.CanonicalBytes(unsigned);
        byte[]? der = null;
        try
        {
            der = signer.SignData(
                canonical,
                HashAlgorithmName.SHA256,
                DSASignatureFormat.Rfc3279DerSequence);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            {
                await Evaluate(
                    evaluator,
                    Proposal(),
                    unsigned with { SignatureBase64 = Convert.ToBase64String(der) });
            });
            await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            {
                await Evaluate(
                    evaluator,
                    Proposal(),
                    unsigned with { SignatureBase64 = new string('A', 4096) });
            });
        }
        finally
        {
            CryptographicOperations.ZeroMemory(canonical);
            if (der is not null) CryptographicOperations.ZeroMemory(der);
        }
    }

    private static DeterministicPolicyEvaluator Evaluator(VerifiedPolicyEvaluationContext context) => new(new FakeTrustProvider(context, authorized: true));
    private static ValueTask<ApprovalDecisionV1> Evaluate(DeterministicPolicyEvaluator evaluator, ActionProposalV2 proposal, ApprovalEvaluationRequestV1 request, ActionExecutionPromotionV1? promotion = null)
        => evaluator.EvaluateAsync(proposal, request, promotion, TestContext.Current.CancellationToken);
    private static ApprovalEvaluationRequestV1 Request(ActionProposalV2? proposal = null, ActionExecutionPromotionV1? promotion = null)
    {
        proposal ??= Proposal();
        return new ApprovalEvaluationRequestV1(
            ApprovalEvaluationRequestV1.CurrentSchemaVersion,
            ApprovalEvaluationRequestV1.CurrentContractId,
            ApprovalEvaluationRequestV1.CurrentProducerModule,
            ApprovalEvaluationRequestV1.CurrentAuthScope,
            proposal.ProposalId,
            ActionProposalV2Canonical.ComputeSha256(proposal),
            proposal.SoulId,
            proposal.DeviceBindingId,
            proposal.PlatformAccountId,
            proposal.TraceId,
            proposal.IdempotencyKey,
            new string('d', 64),
            promotion is null ? ApprovalEvaluationRequestV1.Shadow : ApprovalEvaluationRequestV1.Execute,
            promotion is null ? null : ApprovalEvaluationRequestV1Canonical.ComputeExecutionPromotionSha256(promotion),
            proposal.OccurredAt,
            proposal.OccurredAt + ApprovalEvaluationRequestV1.Lifetime,
            "internal",
            Convert.ToBase64String(new byte[64]));
    }
    private static ActionProposalV2 Proposal(string action = "observe", bool sideEffect = false) => new(
        ActionProposalV2.CurrentSchemaVersion, ActionProposalV2.CurrentContractId, ActionProposalV2.CurrentProducerModule,
        ActionProposalIdentity.Create(Soul, Device, Account, Idempotency), Soul, Device, Account, Trace, Idempotency, Now,
        "internal", action, sideEffect, true,
        action == "fixture.tap" ? new Dictionary<string, string> { ["selector_ref"] = Selector } : new Dictionary<string, string>(),
        [Evidence]);
    private static VerifiedPolicyEvaluationContext Context() => new(
        Soul, Device, Account, "1.0.0", new HashSet<string>(DeterministicPolicyEvaluator.KnownPolicies, StringComparer.Ordinal),
        false, 10, true, "approval_platform_test", Now, new string('a', 64));
    private static PolicyRuntimeState RuntimeState() => new(Soul, Device, Account, "1.0.0", new HashSet<string>(DeterministicPolicyEvaluator.KnownPolicies, StringComparer.Ordinal), false, 10, true, "approval_platform_test", Now, false, new string('d', 64), 1, new string('e', 64), Now.AddMinutes(2));

    private static ActionExecutionPromotionV1 Promotion(ActionProposalV2 proposal) => new(
        ActionExecutionPromotionV1.CurrentSchemaVersion,
        ActionExecutionPromotionV1.CurrentContractId,
        ActionExecutionPromotionV1.CurrentProducerModule,
        ActionExecutionPromotionV1.CurrentAuthScope,
        Guid.Parse("55000000-0000-0000-0000-000000000005"),
        proposal.ProposalId,
        Guid.Parse("66000000-0000-0000-0000-000000000006"),
        proposal.SoulId,
        proposal.DeviceBindingId,
        proposal.PlatformAccountId,
        proposal.TraceId,
        proposal.IdempotencyKey,
        ActionProposalV2Canonical.ComputeSha256(proposal),
        new string('d', 64),
        1,
        Now,
        Now.AddMinutes(1),
        "internal",
        Convert.ToBase64String(new byte[64]));

    private static ActionExecutionPromotionV1 SignPromotion(
        ECDsa signer,
        ActionExecutionPromotionV1 promotion)
    {
        var canonical = ActionExecutionPromotionV1Canonical.CanonicalBytes(promotion);
        byte[]? signature = null;
        try
        {
            signature = signer.SignData(
                canonical,
                HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            return promotion with { SignatureBase64 = Convert.ToBase64String(signature) };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(canonical);
            if (signature is not null) CryptographicOperations.ZeroMemory(signature);
        }
    }

    private static ApprovalEvaluationRequestV1 SignRequest(
        ECDsa signer,
        ApprovalEvaluationRequestV1 request)
    {
        var canonical = ApprovalEvaluationRequestV1Canonical.CanonicalBytes(request);
        byte[]? signature = null;
        try
        {
            signature = signer.SignData(
                canonical,
                HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            return request with { SignatureBase64 = Convert.ToBase64String(signature) };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(canonical);
            if (signature is not null) CryptographicOperations.ZeroMemory(signature);
        }
    }

    private sealed class FakeTrustProvider(VerifiedPolicyEvaluationContext context, bool authorized) : IPolicyTrustProvider
    {
        public ValueTask<VerifiedPolicyEvaluationContext> ResolveVerifiedContextAsync(ActionProposalV2 proposal, ApprovalEvaluationRequestV1 request, ActionExecutionPromotionV1? promotion, CancellationToken cancellationToken)
        {
            if (!authorized) throw new UnauthorizedAccessException("FAKE: request signature rejected.");
            return ValueTask.FromResult(context);
        }
    }

    private sealed class FakeRuntimeStateSource(PolicyRuntimeState state) : IPolicyRuntimeStateSource
    {
        public ValueTask<PolicyRuntimeState> ReadVerifiedStateAsync(ActionProposalV2 proposal, CancellationToken cancellationToken) => ValueTask.FromResult(state);
    }
}
