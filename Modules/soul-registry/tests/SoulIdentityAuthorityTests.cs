using System.Security.Cryptography;
using Dps.SoulRegistry.Contracts;
using Xunit;

namespace Dps.SoulRegistry.Tests;

public sealed class SoulIdentityAuthorityTests
{
    [Fact, Trait("Category", "Unit")]
    public void LocalDecisionCoreHasNoPublicProductionConstructor()
    {
        Assert.False(typeof(SoulIdentityAuthority).IsPublic);
        Assert.Empty(typeof(SoulIdentityAuthority).GetConstructors());
        Assert.True(typeof(ISoulAliasClientV1).IsAssignableFrom(typeof(SoulIdentityAuthority)));
    }

    [Fact, Trait("Category", "Unit")]
    public void TrustBindingRequiresExactPinnedReleaseAndEpochs()
    {
        using var aliasKey = new AliasHmacKey("key-v1", Enumerable.Repeat((byte)0x11, 32).ToArray());
        using var registry = new PostgresSoulRegistry(new SoulRegistryOptions(
            "Host=unused", "soul_test", "key-v1", [aliasKey]));
        using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var verifier = ECDsa.Create(signer.ExportParameters(false));
        var bindings = new SoulAuthorityBindings(new DenyAllEvidence(), verifier,
            bytes => signer.SignData(bytes, HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation),
            "test-key", 1, 1, new string('a', 64), 1, TimeProvider.System);
        _ = new SoulIdentityAuthority(registry, bindings);
        Assert.Throws<ArgumentException>(() => new SoulIdentityAuthority(registry,
            bindings with { ReleaseBomSha256 = "not-a-digest" }));
        Assert.Throws<ArgumentException>(() => new SoulIdentityAuthority(registry,
            bindings with { TrustEpoch = 0 }));
    }

    private sealed class DenyAllEvidence : ISoulAliasEvidenceAuthority
    {
        public Task<VerifiedAliasEvidence> AuthenticateAndVerifyAsync(
            SoulAliasVerificationRequestV1 request, CancellationToken cancellationToken)
            => throw new SoulRegistryException("No trusted evidence was configured.");
    }
}
