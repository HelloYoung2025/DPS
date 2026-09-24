using System.Reflection;
using Dps.SoulRegistry.Contracts;

namespace Dps.SoulRegistry;

/// <summary>Closed installation-bound composition; it does not register a production route.</summary>
public sealed class SoulCurrentComposition
{
    private static readonly AsyncLocal<SoulInstallationLocation?> Synthetic = new();

    public ISoulCurrentClientV1 Client { get; }
    public SoulCurrentConfigurationV1 Configuration { get; }
    public SoulCurrentCompositionV1 InstallationProof { get; }
    private SoulCurrentComposition(SoulCurrentInstallation installation)
    {
        Client = new SoulCurrentAuthority(installation);
        Configuration = installation.Configuration;
        InstallationProof = installation.Composition;
    }

    public static SoulCurrentComposition CreateForHost()
    {
        var location = Synthetic.Value ?? SoulInstallationLocation.Production;
        var installation = SoulCurrentInstallation.Load(location);
        if (SoulCurrentNative.geteuid() != installation.Configuration.HostUid ||
            SoulCurrentNative.getegid() != installation.Configuration.HostGid)
            throw SoulCurrentInstallation.Unavailable();
        installation.VerifyLoadedArtifacts();
        return new SoulCurrentComposition(installation);
    }

    // Accessible only to friend test assemblies; the public entry has no root/path/client/key parameter.
    internal static IDisposable UseSyntheticInstallation(SoulInstallationLocation location)
    {
        if (Assembly.GetEntryAssembly()?.GetName().Name != "Dps.SoulCurrent.SignerFixture" ||
            location.SyntheticBoundary is null || location.OwnerUid != SoulCurrentNative.geteuid() || Synthetic.Value is not null)
            throw SoulCurrentInstallation.Unavailable();
        Synthetic.Value = location;
        return new SyntheticScope();
    }
    private sealed class SyntheticScope : IDisposable
    {
        public void Dispose() => Synthetic.Value = null;
    }
}
