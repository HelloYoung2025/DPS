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
        var artifact = Path.Combine(fixture.Root, "installation/releases/1/Dps.ControlPlaneHost.dll");
        File.SetLastWriteTimeUtc(artifact, DateTime.UtcNow);
        await host.StandardInput.WriteLineAsync("CONTINUE");
        await host.StandardInput.WriteLineAsync(Convert.ToBase64String(fixture.Query.ToUtf8Json()));
        await host.StandardInput.FlushAsync(token);
        var output = await host.StandardOutput.ReadToEndAsync(token); await host.WaitForExitAsync(token);
        Assert.Equal(1, host.ExitCode); Assert.StartsWith("REJECTED:", output, StringComparison.Ordinal);
        Assert.Equal(before, await fixture.BusinessSnapshotAsync(token));
    }
    [Theory]
    [Trait("Category", "Integration")]
    [InlineData("drop-ack")]
    [InlineData("delay-ack")]
    [InlineData("stale-ack")]
    [InlineData("old-status")]
    [InlineData("wrong-final-nonce")]
    public async Task RealProviderProxyCannotTurnLostLateOrUncorrelatedEvidenceIntoSuccess(string fault)
    {
        if (!OperatingSystem.IsMacOS()) throw new PlatformNotSupportedException();
        var token = TestContext.Current.CancellationToken;
        await using var fixture = await SoulCurrentProcessFixture.CreateAsync(token);
        Assert.Equal(0, (await fixture.ReadHostAsync(null, token)).Exit);
        var before = await fixture.BusinessSnapshotAsync(token);
        var proxy = await fixture.ProxyProviderAsync(fault, token);
        var start = System.Diagnostics.Stopwatch.StartNew();
        var result = await fixture.ReadHostAsync(null, token);
        Assert.Equal(1, result.Exit); Assert.DoesNotContain("SOUL_ACTIVE_AS_OF:", result.Output, StringComparison.Ordinal);
        Assert.True(start.Elapsed < TimeSpan.FromSeconds(11));
        if (fault.EndsWith("ack", StringComparison.Ordinal))
        {
            using var ackRead = CancellationTokenSource.CreateLinkedTokenSource(token); ackRead.CancelAfter(2000);
            Assert.Equal("REAL_RELEASE_ACK", await proxy.StandardOutput.ReadLineAsync(ackRead.Token));
        }
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(token); bounded.CancelAfter(5000);
        while (await fixture.AdvisoryLockCountAsync(true, bounded.Token) != 0) await Task.Delay(10, bounded.Token);
        Assert.Equal(before, await fixture.BusinessSnapshotAsync(token));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task EnabledSyntheticInstallationDoesNotActivatePublicEntryAndHostNeverLoadsSoulRuntime()
    {
        if (!OperatingSystem.IsMacOS()) throw new PlatformNotSupportedException();
        var token = TestContext.Current.CancellationToken;
        await using var fixture = await SoulCurrentProcessFixture.CreateAsync(token);
        Assert.Equal(3, fixture.ServiceProcessIds.Distinct().Count());
        Assert.Equal(0, (await fixture.ReadHostAsync(null, token)).Exit); // Child checks loaded assemblies before and after.
        Assert.Equal(1, (await fixture.ReadHostAsync(null, token, "host-public")).Exit);
    }

}
