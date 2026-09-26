using System.Buffers.Binary;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using Dps.SoulRegistry.Contracts;
using Microsoft.Win32.SafeHandles;

namespace Dps.ControlPlaneHost;

internal sealed record ControlPlaneSoulInstallationLocation(string Root, string SocketRoot, uint OwnerUid,
    string? SyntheticBoundary = null)
{
    internal static readonly ControlPlaneSoulInstallationLocation Production = new(
        "/Library/Application Support/DPS/soul-current", "/var/run/dps-soul-current", 0);
}

internal sealed class ControlPlaneSoulCurrentInstallation
{
    internal ControlPlaneSoulInstallationLocation Location { get; }
    internal SoulCurrentBootstrapV1 Bootstrap { get; }
    internal SoulCurrentConfigurationV2 Configuration { get; }
    internal SoulCurrentCompositionV1 Composition { get; }
    internal string ConfigurationSha256 { get; }
    private readonly byte[] _bootstrapBytes;
    private readonly byte[] _compositionBytes;

    private ControlPlaneSoulCurrentInstallation(ControlPlaneSoulInstallationLocation location, byte[] bootstrap,
        byte[] configuration, byte[] composition)
    {
        Location = location; _bootstrapBytes = bootstrap; _compositionBytes = composition;
        Bootstrap = SoulCurrentBootstrapV1.FromUtf8Json(bootstrap);
        Configuration = SoulCurrentConfigurationV2.FromUtf8Json(configuration);
        Composition = SoulCurrentCompositionV1.FromUtf8Json(composition);
        ConfigurationSha256 = Hash(configuration);
        if (!Bootstrap.Enabled || Bootstrap.ActiveConfigurationSha256 != ConfigurationSha256 ||
            Composition.ConfigurationSha256 != ConfigurationSha256 ||
            Composition.ReleaseBomSha256 != Bootstrap.ActiveReleaseBomSha256 ||
            Composition.ReleaseGeneration != Bootstrap.ActiveReleaseGeneration ||
            Composition.KeyId != Bootstrap.RootKeyId ||
            Bootstrap.RootKeyId == Configuration.HostAuthKeyId || Bootstrap.RootKeyId == Configuration.CurrentKeyId ||
            Bootstrap.RootKeyId == Configuration.StatusKeyId ||
            new[] { Configuration.HostAuthSpkiBase64, Configuration.CurrentSpkiBase64, Configuration.StatusSpkiBase64 }
                .Contains(Bootstrap.RootSpkiBase64, StringComparer.Ordinal))
            throw Unavailable();
        Verify(Bootstrap.RootSpkiBase64, Composition.CanonicalBytes(), Composition.SignatureBase64);
        RequireWindow(Composition.IssuedAt, Composition.ExpiresAt);
        if (location.SyntheticBoundary is null &&
            new[] { Configuration.HostUid, Configuration.ProviderUid, Configuration.HostAuthUid, Configuration.CurrentServiceUid }.Distinct().Count() != 4)
            throw Unavailable();
    }

    internal static ControlPlaneSoulCurrentInstallation Load(ControlPlaneSoulInstallationLocation location)
    {
        RequirePlatform();
        return new ControlPlaneSoulCurrentInstallation(location, Read(location, "bootstrap.v1.json"),
            Read(location, "configuration.v2.json"), Read(location, "composition.v1.json"));
    }

    internal void Revalidate()
    {
        var current = Load(Location);
        if (!_bootstrapBytes.AsSpan().SequenceEqual(current._bootstrapBytes) ||
            !_compositionBytes.AsSpan().SequenceEqual(current._compositionBytes) ||
            ConfigurationSha256 != current.ConfigurationSha256) throw Unavailable();
    }

    internal void RequireQuery(SoulCurrentQueryV1 query)
    {
        _ = query.ToUtf8Json();
        if (query.TenantRef != Configuration.TenantRef || query.SoulId != Configuration.SoulId ||
            query.ProducerModule != "control-plane-host" || query.Purpose != "identity-read") throw Unavailable();
    }

    internal void VerifyLoadedArtifacts()
    {
        if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "Dps.SoulRegistry")) throw Unavailable();
        VerifyAssembly(typeof(ControlPlaneSoulCurrentComposition).Assembly, "Dps.ControlPlaneHost", Composition.HostArtifactSha256);
        VerifyAssembly(typeof(SoulCurrentQueryV1).Assembly, "Dps.SoulRegistry.Contracts", Composition.ContractsArtifactSha256);
    }

    private void VerifyAssembly(Assembly assembly, string name, string expectedHash)
    {
        var directory = Path.Combine(Location.Root, "releases", Composition.ReleaseGeneration.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var path = Path.Combine(directory, name + ".dll");
        if (assembly.IsDynamic || AssemblyLoadContext.GetLoadContext(assembly) != AssemblyLoadContext.Default ||
            assembly.Location != path || assembly.GetName().Name != name ||
            AppDomain.CurrentDomain.GetAssemblies().Count(a => a.GetName().Name == name) != 1) throw Unavailable();
        using var parent = ControlPlaneSoulCurrentNative.OpenDirectory(directory, Location);
        var start = Process.GetCurrentProcess().StartTime.ToUniversalTime();
        parent.RequirePredates(start);
        var bytes = parent.ReadFile(name + ".dll", Location.OwnerUid, false, 16 * 1024 * 1024, start);
        if (Hash(bytes) != expectedHash) throw Unavailable();
        using var pe = new PEReader(new MemoryStream(bytes, false));
        var metadata = pe.GetMetadataReader();
        var definition = metadata.GetAssemblyDefinition();
        var identity = assembly.GetName();
        if (metadata.GetGuid(metadata.GetModuleDefinition().Mvid) != assembly.ManifestModule.ModuleVersionId ||
            metadata.GetString(definition.Name) != identity.Name || definition.Version != identity.Version ||
            metadata.GetString(definition.Culture) != (identity.CultureName ?? "") ||
            !metadata.GetBlobBytes(definition.PublicKey).AsSpan().SequenceEqual(identity.GetPublicKey() ?? [])) throw Unavailable();
    }

    private static byte[] Read(ControlPlaneSoulInstallationLocation location, string relative, uint? owner = null, bool secret = false)
    {
        var parts = relative.Split('/');
        using var directory = ControlPlaneSoulCurrentNative.OpenDirectory(Path.Combine(location.Root,
            Path.Combine(parts[..^1])), location);
        return directory.ReadFile(parts[^1], owner ?? location.OwnerUid, secret, 16384);
    }

    internal static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    internal static void Verify(string spki, byte[] frame, string signature)
    {
        using var key = ECDsa.Create(); var bytes = Convert.FromBase64String(spki);
        key.ImportSubjectPublicKeyInfo(bytes, out var read);
        if (read != bytes.Length || !key.VerifyData(frame, Convert.FromBase64String(signature),
            HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation)) throw Unavailable();
    }
    internal static void RequireWindow(DateTimeOffset issued, DateTimeOffset expires)
    {
        var now = DateTimeOffset.UtcNow;
        if (now < issued || now >= expires) throw Unavailable();
    }
    internal static UnauthorizedAccessException Unavailable() => new("AUTHORITY_UNAVAILABLE");
    private static void RequirePlatform()
    {
        if (!OperatingSystem.IsMacOS() || !Environment.Is64BitProcess || !BitConverter.IsLittleEndian)
            throw Unavailable();
    }
}
