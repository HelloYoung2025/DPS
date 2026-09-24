using Dps.SoulRegistry;
using Xunit;

namespace Dps.ControlPlaneHost.Tests;

public sealed class ControlPlaneSoulIdentityReadTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void ClosedHostEntryCannotAcquireAuthorityWithoutProtectedInstallation()
    {
        Assert.False(File.Exists("/Library/Application Support/DPS/soul-current/bootstrap.v1.json"));
        Assert.Throws<SoulRegistryException>(() => ControlPlaneSoulCurrentComposition.Open());
    }
}
