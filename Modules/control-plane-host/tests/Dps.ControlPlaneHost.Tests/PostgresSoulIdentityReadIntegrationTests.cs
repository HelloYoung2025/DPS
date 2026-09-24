using Dps.SoulRegistry.Tests;
using Xunit;

namespace Dps.ControlPlaneHost.Tests;

public sealed class PostgresSoulIdentityReadIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ActualHostFacadeUsesIndependentSignersAndPreservesBusinessRows()
    {
        if (!OperatingSystem.IsMacOS()) throw new PlatformNotSupportedException();
        var token = TestContext.Current.CancellationToken;
        await using var fixture = await SoulCurrentProcessFixture.CreateAsync(token);
        var before = await fixture.BusinessSnapshotAsync(token);
        var result = await fixture.ReadHostAsync(null, token);
        Assert.True(result.Exit == 0, result.Output);
        Assert.StartsWith("SOUL_ACTIVE_AS_OF:", result.Output, StringComparison.Ordinal);
        Assert.Equal(2, fixture.SignerProcessIds.Distinct().Count());
        Assert.Equal(before, await fixture.BusinessSnapshotAsync(token));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task HealthyHostThenRevokedAuthorityNeverReturnsAnIdentity()
    {
        if (!OperatingSystem.IsMacOS()) throw new PlatformNotSupportedException();
        var token = TestContext.Current.CancellationToken;
        await using var fixture = await SoulCurrentProcessFixture.CreateAsync(token);
        Assert.Equal(0, (await fixture.ReadHostAsync(null, token)).Exit);
        var before = await fixture.BusinessSnapshotAsync(token);
        fixture.SetAuthorityState(fixture.State with { Status = "REVOKED", AuthorizationRevision = 2 });
        var rejected = await fixture.ReadHostAsync(null, token);
        Assert.Equal(1, rejected.Exit); Assert.StartsWith("REJECTED:", rejected.Output, StringComparison.Ordinal);
        Assert.Equal(before, await fixture.BusinessSnapshotAsync(token));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task HealthyHostThenUnboundArtifactIsDeniedWithoutWrites()
    {
        if (!OperatingSystem.IsMacOS()) throw new PlatformNotSupportedException();
        var token = TestContext.Current.CancellationToken;
        await using var fixture = await SoulCurrentProcessFixture.CreateAsync(token);
        Assert.Equal(0, (await fixture.ReadHostAsync(null, token)).Exit);
        var before = await fixture.BusinessSnapshotAsync(token);
        using var host = fixture.Start("host-wait");
        Assert.Equal("WAITING", await host.StandardOutput.ReadLineAsync(token));
        var artifact = Path.Combine(fixture.Root, "installation/releases/1/Dps.SoulRegistry.dll");
        File.SetLastWriteTimeUtc(artifact, DateTime.UtcNow);
        await host.StandardInput.WriteLineAsync("CONTINUE");
        await host.StandardInput.WriteLineAsync(Convert.ToBase64String(fixture.Query.ToUtf8Json()));
        await host.StandardInput.FlushAsync(token);
        var output = await host.StandardOutput.ReadToEndAsync(token); await host.WaitForExitAsync(token);
        Assert.Equal(1, host.ExitCode); Assert.StartsWith("REJECTED:", output, StringComparison.Ordinal);
        Assert.Equal(before, await fixture.BusinessSnapshotAsync(token));
    }
}
