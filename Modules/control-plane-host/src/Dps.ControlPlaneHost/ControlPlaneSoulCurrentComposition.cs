using Dps.SoulRegistry.Contracts;

namespace Dps.ControlPlaneHost;

/// <summary>Quarantined composition. Production activation requires a separate reviewed release.</summary>
public sealed class ControlPlaneSoulCurrentComposition
{
    private readonly ControlPlaneSoulInstallationLocation _location;
    private ControlPlaneSoulCurrentComposition(ControlPlaneSoulInstallationLocation location) => _location = location;
    public static ControlPlaneSoulCurrentComposition Open() => throw ControlPlaneSoulCurrentInstallation.Unavailable();
    internal static ControlPlaneSoulCurrentComposition ForSyntheticProcess(ControlPlaneSoulInstallationLocation location)
    {
        if (System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name != "Dps.SoulCurrent.HostFixture" ||
            location.SyntheticBoundary is null || location.OwnerUid != ControlPlaneSoulCurrentNative.geteuid())
            throw ControlPlaneSoulCurrentInstallation.Unavailable();
        return new(location);
    }
    /// <summary>SOUL_ACTIVE_AS_OF only; echoed device/account values do not prove active bindings.</summary>
    public async Task<SoulResolved> ReadSoulActiveAsOfAsync(SoulCurrentQueryV1 query, CancellationToken cancellationToken)
    {
        using var deadline = new ControlPlaneSoulCurrentDeadline(cancellationToken);
        var installation = ControlPlaneSoulCurrentInstallation.Load(_location);
        installation.VerifyLoadedArtifacts(); deadline.RequireLive();
        var client = new ControlPlaneSoulCurrentIpcClient(installation, deadline);
        return await new ControlPlaneSoulIdentityRead(client, installation.Configuration, installation.Composition).ReadAsync(query, deadline.Token);
    }
}
