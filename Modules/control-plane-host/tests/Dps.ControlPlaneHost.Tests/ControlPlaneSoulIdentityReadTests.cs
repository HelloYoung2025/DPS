using Dps.SoulRegistry.Contracts;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace Dps.ControlPlaneHost.Tests;

public sealed class ControlPlaneSoulIdentityReadTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void ClosedHostEntryCannotAcquireAuthorityWithoutProtectedInstallation()
    {
        Assert.False(File.Exists("/Library/Application Support/DPS/soul-current/bootstrap.v1.json"));
        Assert.Throws<UnauthorizedAccessException>(() => ControlPlaneSoulCurrentComposition.Open());
    }
    [Fact]
    [Trait("Category", "Unit")]
    public void HostAssemblyReferencesContractsWithoutSoulRuntime()
    {
        var references = typeof(ControlPlaneSoulCurrentComposition).Assembly.GetReferencedAssemblies().Select(x => x.Name).ToArray();
        Assert.Contains("Dps.SoulRegistry.Contracts", references); Assert.DoesNotContain("Dps.SoulRegistry", references);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task MockDisposalCancellationIsObservedBeforeBusinessSuccess()
    {
        // Deterministic unit mock: real PG/IPC evidence is in the integration suite.
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "global.json"))) directory = directory.Parent;
        var root = directory?.FullName ?? throw new InvalidOperationException("Repository root missing.");
        using var corpus = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "Modules/soul-registry/contracts/provided/soul.identity.v1.corpus.json")));
        var cases = corpus.RootElement.GetProperty("cases").EnumerateArray().ToArray();
        byte[] Payload(string id) => System.Text.Encoding.UTF8.GetBytes(cases.Single(x => x.GetProperty("id").GetString() == id).GetProperty("payload").GetRawText());
        var query = SoulCurrentQueryV1.FromUtf8Json(Payload("soul.current.query.valid"));
        var resolution = SoulCurrentResolutionV1.FromUtf8Json(Payload("soul.current.resolution.valid"));
        using var configCorpus = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "Modules/soul-registry/contracts/corpus/soul.current.configuration.v2.corpus.json")));
        var configuration = SoulCurrentConfigurationV2.FromUtf8Json(System.Text.Encoding.UTF8.GetBytes(configCorpus.RootElement.GetProperty("cases")[0].GetProperty("payload").GetRawText()));
        using var oldCorpus = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "Modules/soul-registry/contracts/corpus/soul.current.composition.v1.corpus.json")));
        var composition = SoulCurrentCompositionV1.FromUtf8Json(System.Text.Encoding.UTF8.GetBytes(oldCorpus.RootElement.GetProperty("cases").EnumerateArray().Single(x => x.GetProperty("id").GetString() == "soul.current.composition.valid").GetProperty("payload").GetRawText()));
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        configuration = configuration with { SoulId = query.SoulId, TenantRef = query.TenantRef, CurrentKeyId = resolution.KeyId, CurrentSpkiBase64 = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()) };
        composition = composition with { ReleaseBomSha256 = resolution.ReleaseBomSha256, ReleaseGeneration = resolution.ReleaseGeneration };
        resolution = resolution with { IssuedAt = DateTimeOffset.UtcNow.AddMilliseconds(-1), ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(5) };
        resolution = resolution with { SignatureBase64 = Convert.ToBase64String(key.SignData(resolution.CanonicalBytes(), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation)) };
        using var cancel = new CancellationTokenSource();
        var mock = new DisposalCancellationMock(resolution, cancel);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ControlPlaneSoulIdentityRead(mock, configuration, composition).ReadAsync(query, cancel.Token));
        Assert.True(mock.Revalidated); Assert.True(mock.Disposed);
    }
    private sealed class DisposalCancellationMock(SoulCurrentResolutionV1 snapshot, CancellationTokenSource cancel) : ISoulCurrentClientV1, ISoulCurrentLeaseV1
    {
        internal bool Revalidated, Disposed;
        public SoulCurrentResolutionV1 Snapshot => snapshot;
        public Task<ISoulCurrentLeaseV1> AcquireCurrentAsync(SoulCurrentQueryV1 query, CancellationToken cancellationToken) => Task.FromResult<ISoulCurrentLeaseV1>(this);
        public Task<SoulCurrentResolutionV1> ResolveCurrentAsync(SoulCurrentQueryV1 query, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task RevalidateAsync(CancellationToken cancellationToken) { Revalidated = true; return Task.CompletedTask; }
        public ValueTask DisposeAsync() { Disposed = true; cancel.Cancel(); return ValueTask.CompletedTask; }
    }

}
