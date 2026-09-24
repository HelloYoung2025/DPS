using System.Security.Cryptography;
using Dps.SoulRegistry.Contracts;

namespace Dps.ControlPlaneHost;

// Business reader depends only on contracts; only the fixed composition façade constructs it.
internal sealed class ControlPlaneSoulIdentityRead(ISoulCurrentClientV1 client,
    SoulCurrentConfigurationV1 configuration, SoulCurrentCompositionV1 installation)
{
    internal async Task<SoulResolved> ReadAsync(SoulCurrentQueryV1 query, CancellationToken cancellationToken)
    {
        _ = query.ToUtf8Json();
        if (query.ProducerModule != "control-plane-host" || query.Purpose != "identity-read" ||
            query.TenantRef != configuration.TenantRef || query.SoulId != configuration.SoulId)
            throw new UnauthorizedAccessException("Soul read scope mismatch.");
        await using var lease = await client.AcquireCurrentAsync(query, cancellationToken);
        var snapshot = lease.Snapshot;
        Verify(snapshot, query);
        await lease.RevalidateAsync(cancellationToken);
        Verify(snapshot, query);
        cancellationToken.ThrowIfCancellationRequested();
        return snapshot.Resolution;
    }

    private void Verify(SoulCurrentResolutionV1 snapshot, SoulCurrentQueryV1 query)
    {
        snapshot.RequireMatches(query); snapshot.RequireValidWindow(DateTimeOffset.UtcNow);
        if (snapshot.Status != "ACTIVE" || snapshot.KeyId != configuration.CurrentKeyId ||
            snapshot.ReleaseBomSha256 != installation.ReleaseBomSha256 || snapshot.ReleaseGeneration != installation.ReleaseGeneration)
            throw new UnauthorizedAccessException("Soul read authority mismatch.");
        using var key = ECDsa.Create(); var spki = Convert.FromBase64String(configuration.CurrentSpkiBase64);
        key.ImportSubjectPublicKeyInfo(spki, out var read);
        if (read != spki.Length || !key.VerifyData(snapshot.CanonicalBytes(), Convert.FromBase64String(snapshot.SignatureBase64),
            HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
            throw new UnauthorizedAccessException("Soul read signature mismatch.");
    }
}
