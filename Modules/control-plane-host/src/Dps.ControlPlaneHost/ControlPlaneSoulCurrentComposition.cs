using Dps.SoulRegistry;
using Dps.SoulRegistry.Contracts;

namespace Dps.ControlPlaneHost;

/// <summary>
/// Callable, installation-bound local composition. No listener/startup route is registered.
/// This façade is the sole Proposed Host composition-only dependency on Soul runtime.
/// </summary>
public sealed class ControlPlaneSoulCurrentComposition
{
    private readonly ControlPlaneSoulIdentityRead _reader;
    private ControlPlaneSoulCurrentComposition(SoulCurrentComposition composition) =>
        _reader = new(composition.Client, composition.Configuration, composition.InstallationProof);

    public static ControlPlaneSoulCurrentComposition Open() => new(SoulCurrentComposition.CreateForHost());

    /// <summary>SOUL_ACTIVE_AS_OF only; echoed device/account values do not prove active bindings.</summary>
    public Task<SoulResolved> ReadSoulActiveAsOfAsync(SoulCurrentQueryV1 query, CancellationToken cancellationToken) =>
        _reader.ReadAsync(query, cancellationToken);
}
