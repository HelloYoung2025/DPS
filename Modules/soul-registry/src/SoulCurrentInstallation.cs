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
using Npgsql;

namespace Dps.SoulRegistry;

internal sealed record SoulInstallationLocation(string Root, string SocketRoot, uint OwnerUid,
    string? SyntheticBoundary = null)
{
    internal static readonly SoulInstallationLocation Production = new(
        "/Library/Application Support/DPS/soul-current", "/var/run/dps-soul-current", 0);
}

internal sealed class SoulCurrentInstallation
{
    internal SoulInstallationLocation Location { get; }
    internal SoulCurrentBootstrapV1 Bootstrap { get; }
    internal SoulCurrentConfigurationV1 Configuration { get; }
    internal SoulCurrentCompositionV1 Composition { get; }
    internal string ConfigurationSha256 { get; }
    private readonly byte[] _bootstrapBytes;
    private readonly byte[] _compositionBytes;

    private SoulCurrentInstallation(SoulInstallationLocation location, byte[] bootstrap,
        byte[] configuration, byte[] composition)
    {
        Location = location; _bootstrapBytes = bootstrap; _compositionBytes = composition;
        Bootstrap = SoulCurrentBootstrapV1.FromUtf8Json(bootstrap);
        Configuration = SoulCurrentConfigurationV1.FromUtf8Json(configuration);
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
            new[] { Configuration.HostUid, Configuration.HostAuthUid, Configuration.CurrentServiceUid }.Distinct().Count() != 3)
            throw Unavailable();
    }

    internal static SoulCurrentInstallation Load(SoulInstallationLocation location)
    {
        RequirePlatform();
        return new SoulCurrentInstallation(location, Read(location, "bootstrap.v1.json"),
            Read(location, "configuration.v1.json"), Read(location, "composition.v1.json"));
    }

    internal void Revalidate()
    {
        var current = Load(Location);
        if (!_bootstrapBytes.AsSpan().SequenceEqual(current._bootstrapBytes) ||
            !_compositionBytes.AsSpan().SequenceEqual(current._compositionBytes) ||
            ConfigurationSha256 != current.ConfigurationSha256) throw Unavailable();
    }

    internal SoulCurrentAuthorityStateV1 ReadAuthorityState()
    {
        Revalidate();
        var state = SoulCurrentAuthorityStateV1.FromUtf8Json(Read(Location, "authority-state.v1.json"));
        var c = Configuration;
        if (state.ConfigurationSha256 != ConfigurationSha256 || state.TenantRef != c.TenantRef ||
            state.SoulId != c.SoulId || state.PrincipalId != c.PrincipalId || state.Permission != c.Permission ||
            state.ReleaseBomSha256 != Composition.ReleaseBomSha256 || state.ReleaseGeneration != Composition.ReleaseGeneration ||
            state.HostAuthKeyId != c.HostAuthKeyId || state.CurrentKeyId != c.CurrentKeyId || state.StatusKeyId != c.StatusKeyId)
            throw Unavailable();
        return state;
    }

    internal void RequireQuery(SoulCurrentQueryV1 query)
    {
        _ = query.ToUtf8Json();
        if (query.TenantRef != Configuration.TenantRef || query.SoulId != Configuration.SoulId ||
            query.ProducerModule != "control-plane-host" || query.Purpose != "identity-read") throw Unavailable();
    }

    internal string ReadDatabaseConnection(bool signer)
    {
        var c = Configuration;
        var uid = signer ? c.CurrentServiceUid : c.HostUid;
        if (SoulCurrentNative.geteuid() != uid) throw Unavailable();
        var secret = Read(Location, "secrets/" + (signer ? c.SignerDatabaseSecretId : c.DatabaseSecretId),
            checked((uint)uid), true);
        var password = new UTF8Encoding(false, true).GetString(secret);
        if (password.Length is < 24 or > 256 || password.Any(char.IsControl)) throw Unavailable();
        return new NpgsqlConnectionStringBuilder
        {
            Host = c.DatabaseHost, Port = checked((int)c.DatabasePort), Database = c.DatabaseName,
            Username = signer ? c.SignerDatabaseRuntimeRole : c.DatabaseRuntimeRole,
            Password = password, Pooling = false, Timeout = 5, CommandTimeout = 5,
            IncludeErrorDetail = false, ApplicationName = signer ? "dps-soul-current-signer" : "dps-soul-current-provider"
        }.ConnectionString;
    }

    internal ECDsa ReadServiceKey(string role)
    {
        var c = Configuration;
        var (uid, keyId, spki) = role switch
        {
            "host-possession" => (c.HostAuthUid, c.HostAuthKeyId, c.HostAuthSpkiBase64),
            "soul-current" => (c.CurrentServiceUid, c.CurrentKeyId, c.CurrentSpkiBase64),
            "current-status" => (c.CurrentServiceUid, c.StatusKeyId, c.StatusSpkiBase64),
            _ => throw Unavailable()
        };
        if (SoulCurrentNative.geteuid() != uid) throw Unavailable();
        var bytes = Read(Location, "keys/" + keyId + ".pkcs8", checked((uint)uid), true);
        var key = ECDsa.Create();
        try
        {
            key.ImportPkcs8PrivateKey(bytes, out var consumed);
            if (consumed != bytes.Length || Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()) != spki)
                throw Unavailable();
            return key;
        }
        catch { key.Dispose(); throw; }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    internal void VerifyLoadedArtifacts()
    {
        var host = AssemblyLoadContext.Default.Assemblies.SingleOrDefault(a => a.GetName().Name == "Dps.ControlPlaneHost")
            ?? throw Unavailable();
        if (host.GetType("Dps.ControlPlaneHost.ControlPlaneSoulCurrentComposition", false) is null) throw Unavailable();
        VerifyAssembly(host, "Dps.ControlPlaneHost", Composition.HostArtifactSha256);
        VerifyAssembly(typeof(SoulCurrentComposition).Assembly, "Dps.SoulRegistry", Composition.SoulArtifactSha256);
        VerifyAssembly(typeof(SoulCurrentQueryV1).Assembly, "Dps.SoulRegistry.Contracts", Composition.ContractsArtifactSha256);
    }

    private void VerifyAssembly(Assembly assembly, string name, string expectedHash)
    {
        var directory = Path.Combine(Location.Root, "releases", Composition.ReleaseGeneration.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var path = Path.Combine(directory, name + ".dll");
        if (assembly.IsDynamic || AssemblyLoadContext.GetLoadContext(assembly) != AssemblyLoadContext.Default ||
            assembly.Location != path || assembly.GetName().Name != name ||
            AppDomain.CurrentDomain.GetAssemblies().Count(a => a.GetName().Name == name) != 1) throw Unavailable();
        using var parent = SoulCurrentNative.OpenDirectory(directory, Location);
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

    private static byte[] Read(SoulInstallationLocation location, string relative, uint? owner = null, bool secret = false)
    {
        var parts = relative.Split('/');
        using var directory = SoulCurrentNative.OpenDirectory(Path.Combine(location.Root,
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
    internal static SoulRegistryException Unavailable() => new("AUTHORITY_UNAVAILABLE");
    private static void RequirePlatform()
    {
        if (!OperatingSystem.IsMacOS() || !Environment.Is64BitProcess || !BitConverter.IsLittleEndian)
            throw Unavailable();
    }
}

// Narrow Darwin interop: files are checked/read on held descriptors; socket attrs describe a vnode,
// not the connected socket. Protected ancestors + peer identity + typed signatures close that boundary.
internal static class SoulCurrentNative
{
    private const string Lib = "/usr/lib/libSystem.B.dylib";
    [DllImport(Lib, SetLastError = true)] private static extern int open(string path, int flags);
    [DllImport(Lib, SetLastError = true)] private static extern int openat(int fd, string path, int flags);
    [DllImport(Lib, SetLastError = true)] private static extern int fstat(int fd, byte[] stat);
    [DllImport(Lib, SetLastError = true)] private static extern IntPtr acl_get_fd_np(int fd, int type);
    [DllImport(Lib, SetLastError = true)] private static extern int acl_valid(IntPtr acl);
    [DllImport(Lib, SetLastError = true)] private static extern int acl_get_entry(IntPtr acl, int entryId, out IntPtr entry);
    [DllImport(Lib)] private static extern int acl_free(IntPtr acl);
    [DllImport(Lib)] internal static extern uint geteuid();
    [DllImport(Lib)] internal static extern uint getegid();
    [DllImport(Lib, SetLastError = true)] internal static extern int getpeereid(int socket, out uint uid, out uint gid);
    internal static void RequirePeer(int descriptor, uint uid, uint gid)
    {
        if (getpeereid(descriptor, out var actualUid, out var actualGid) != 0 || actualUid != uid || actualGid != gid)
            throw SoulCurrentInstallation.Unavailable();
    }
    [StructLayout(LayoutKind.Sequential)] private struct AttrList
    {
        internal ushort Count, Reserved;
        internal uint Common, Volume, Directory, File, Fork;
    }
    [DllImport(Lib, SetLastError = true)] private static extern int fgetattrlist(int fd, ref AttrList attrs, byte[] buffer, nuint size, uint options);
    [DllImport(Lib, SetLastError = true)] private static extern int getattrlistat(int fd, string name, ref AttrList attrs, byte[] buffer, nuint size, uint options);

    internal sealed record Node(uint Device, ulong Inode, uint Uid, uint Gid, uint Mode,
        long ModifiedSeconds, long ModifiedNanos, long ChangedSeconds, long ChangedNanos, long Size);
    internal sealed record SocketNode(uint Device, ulong FsId, ulong Inode, uint Uid, uint Gid, uint Mode,
        long ModifiedSeconds, long ModifiedNanos, long ChangedSeconds, long ChangedNanos);

    private static SafeFileHandle Handle(int fd)
    {
        if (fd < 0) throw SoulCurrentInstallation.Unavailable();
        return new SafeFileHandle((IntPtr)fd, true);
    }
    private static int Fd(SafeFileHandle handle) => checked((int)handle.DangerousGetHandle());
    private static Node Stat(SafeFileHandle handle)
    {
        var data = new byte[144];
        if (fstat(Fd(handle), data) != 0) throw SoulCurrentInstallation.Unavailable();
        return new Node(U32(data, 0), U64(data, 8), U32(data, 16), U32(data, 20),
            BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(4)), I64(data, 48), I64(data, 56), I64(data, 64), I64(data, 72), I64(data, 96));
    }
    internal static void NoAcl(SafeFileHandle handle)
    {
        var acl = acl_get_fd_np(Fd(handle), 0x100);
        var error = Marshal.GetLastPInvokeError();
        if (acl == IntPtr.Zero)
        {
            if (error != 2) throw SoulCurrentInstallation.Unavailable();
            RequireEmptyAclOnSameDescriptor(handle);
            return;
        }
        try
        {
            if (acl_valid(acl) != 0) throw SoulCurrentInstallation.Unavailable();
            // Darwin returns 0 for an entry, -1/EINVAL after a valid empty ACL (not POSIX 1/0).
            if (acl_get_entry(acl, 0, out _) != -1 || Marshal.GetLastPInvokeError() != 22)
                throw SoulCurrentInstallation.Unavailable();
        }
        finally { _ = acl_free(acl); }
    }
    private static void RequireEmptyAclOnSameDescriptor(SafeFileHandle handle)
    {
        var before = Stat(handle);
        var type = (before.Mode & 0xf000) switch { 0x8000 => 1u, 0x4000 => 2u, _ => throw SoulCurrentInstallation.Unavailable() };
        var volume = new byte[108]; var va = new AttrList { Count = 5, Common = 0x80000006, Volume = 0xc0020000 };
        if (fgetattrlist(Fd(handle), ref va, volume, (nuint)volume.Length, 4) != 0)
            throw SoulCurrentInstallation.Unavailable();
        var bytes = new byte[8192]; var attrs = new AttrList { Count = 5, Common = 0x82438c0e };
        if (fgetattrlist(Fd(handle), ref attrs, bytes, (nuint)bytes.Length, 4) != 0)
            throw SoulCurrentInstallation.Unavailable();
        var length = U32(bytes, 0);
        if (length > bytes.Length) throw SoulCurrentInstallation.Unavailable();
        var vnode = ParseNodeAttributes(volume, bytes.AsSpan(0, checked((int)length)), before.Uid, before.Gid, type, before.Mode & 0xf000);
        if (vnode.Device != before.Device || vnode.Inode != before.Inode || vnode.Mode != before.Mode ||
            vnode.ModifiedSeconds != before.ModifiedSeconds || vnode.ModifiedNanos != before.ModifiedNanos ||
            vnode.ChangedSeconds != before.ChangedSeconds || vnode.ChangedNanos != before.ChangedNanos || Stat(handle) != before)
            throw SoulCurrentInstallation.Unavailable();
    }

    private static void Require(Node node, uint owner, uint type, bool secret)
    {
        if ((node.Mode & 0xf000) != type || node.Uid != owner || (node.Mode & 0x12) != 0 ||
            (secret && (node.Mode & 0x3f) != 0)) throw SoulCurrentInstallation.Unavailable();
    }
    private static void Component(string name)
    {
        if (name.Length == 0 || name is "." or ".." || name.Contains('/') || name.Contains('\0'))
            throw SoulCurrentInstallation.Unavailable();
    }

    internal static DirectoryChain OpenDirectory(string path, SoulInstallationLocation location) => new(path, location);
    internal sealed class DirectoryChain : IDisposable
    {
        private readonly List<(SafeFileHandle Handle, string Name, Node Node)> _chain = [];
        private readonly uint _owner;
        internal int Descriptor => Fd(_chain[^1].Handle);
        internal DirectoryChain(string path, SoulInstallationLocation location)
        {
            _owner = location.OwnerUid;
            var start = location.SyntheticBoundary ?? "/";
            if (!Path.IsPathFullyQualified(path) || Path.GetFullPath(path) != path ||
                (start != "/" && path != start && !path.StartsWith(start + "/", StringComparison.Ordinal)))
                throw SoulCurrentInstallation.Unavailable();
            try
            {
                var first = Handle(open(start, 0x100100));
                _chain.Add((first, "", Stat(first)));
                Require(_chain[0].Node, _owner, 0x4000, false); NoAcl(first);
                foreach (var part in Path.GetRelativePath(start, path).Split('/'))
                {
                    if (part == ".") continue;
                    Component(part);
                    var next = Handle(openat(Descriptor, part, 0x100100));
                    var node = Stat(next); _chain.Add((next, part, node));
                    Require(node, _owner, 0x4000, false); NoAcl(next);
                }
            }
            catch { Dispose(); throw; }
        }
        internal void Revalidate()
        {
            for (var index = 0; index < _chain.Count; index++)
            {
                var item = _chain[index]; var now = Stat(item.Handle);
                // Directory mtime changes when an owned service creates its socket; identity/protection cannot change.
                if (now.Device != item.Node.Device || now.Inode != item.Node.Inode ||
                    now.Uid != item.Node.Uid || now.Gid != item.Node.Gid || now.Mode != item.Node.Mode)
                    throw SoulCurrentInstallation.Unavailable();
                Require(now, _owner, 0x4000, false); NoAcl(item.Handle);
                if (index == 0) continue;
                using var byName = Handle(openat(Fd(_chain[index - 1].Handle), item.Name, 0x100100));
                var named = Stat(byName);
                if (named.Device != now.Device || named.Inode != now.Inode) throw SoulCurrentInstallation.Unavailable();
            }
        }
        internal void RequirePredates(DateTime utc)
        {
            var node = Stat(_chain[^1].Handle);
            Predates(node, utc);
        }
        private static void Predates(Node node, DateTime utc)
        {
            var limit = new DateTimeOffset(utc).ToUnixTimeMilliseconds();
            if (checked(node.ModifiedSeconds * 1000 + node.ModifiedNanos / 1_000_000) >= limit ||
                checked(node.ChangedSeconds * 1000 + node.ChangedNanos / 1_000_000) >= limit)
                throw SoulCurrentInstallation.Unavailable();
        }
        internal byte[] ReadFile(string name, uint owner, bool secret, int maximum, DateTime? predates = null)
        {
            Component(name); Revalidate();
            using var handle = Handle(openat(Descriptor, name, 0x100));
            var before = Stat(handle); Require(before, owner, 0x8000, secret); NoAcl(handle);
            if (predates is not null) Predates(before, predates.Value);
            if (before.Size < 1 || before.Size > maximum) throw SoulCurrentInstallation.Unavailable();
            var bytes = new byte[checked((int)before.Size)]; var offset = 0;
            while (offset < bytes.Length)
            {
                var read = RandomAccess.Read(handle, bytes.AsSpan(offset), offset);
                if (read == 0) throw SoulCurrentInstallation.Unavailable();
                offset += read;
            }
            if (Stat(handle) != before) throw SoulCurrentInstallation.Unavailable();
            NoAcl(handle); Revalidate(); return bytes;
        }
        internal SocketNode Socket(string name, uint uid, uint gid)
        {
            Component(name); Revalidate();
            var volume = new byte[108]; var va = new AttrList { Count = 5, Common = 0x80000006, Volume = 0xc0020000 };
            if (fgetattrlist(Descriptor, ref va, volume, (nuint)volume.Length, 4) != 0)
                throw SoulCurrentInstallation.Unavailable();
            var data = new byte[8192]; var sa = new AttrList { Count = 5, Common = 0x82438c0e };
            if (getattrlistat(Descriptor, name, ref sa, data, (nuint)data.Length, 5) != 0)
                throw SoulCurrentInstallation.Unavailable();
            var length = U32(data, 0);
            if (length > data.Length) throw SoulCurrentInstallation.Unavailable();
            var result = ParseSocket(volume, data.AsSpan(0, checked((int)length)), uid, gid);
            if (result.Device != Stat(_chain[^1].Handle).Device) throw SoulCurrentInstallation.Unavailable();
            Revalidate(); return result;
        }
        public void Dispose()
        {
            for (var i = _chain.Count - 1; i >= 0; i--) _chain[i].Handle.Dispose();
            _chain.Clear();
        }
    }

    internal static SocketNode ParseSocket(ReadOnlySpan<byte> volume, ReadOnlySpan<byte> data, uint uid, uint gid) =>
        ParseNodeAttributes(volume, data, uid, gid, 6, 0xc000);

    internal static SocketNode ParseNodeAttributes(ReadOnlySpan<byte> volume, ReadOnlySpan<byte> data,
        uint uid, uint gid, uint type, uint mode)
    {
        if (volume.Length != 108 || U32(volume, 0) != 108 || U32(volume, 4) != 0x80000006 ||
            U32(volume, 8) != 0x40020000 || U32(volume, 12) != 0 || U32(volume, 16) != 0 || U32(volume, 20) != 0 ||
            (U32(volume, 40) & 0x400) == 0 || (U32(volume, 56) & 0x400) == 0 ||
            (U32(volume, 68) & 0x400000) == 0 || (U32(volume, 88) & 0x400000) == 0 ||
            data.Length < 100 || U32(data, 0) != data.Length || U32(data, 8) != 0 || U32(data, 12) != 0 ||
            U32(data, 16) != 0 || U32(data, 20) != 0 || U32(data, 24) != U32(volume, 24) ||
            U64(data, 28) != U64(volume, 28) || U32(data, 36) != type ||
            U32(data, 72) != uid || U32(data, 76) != gid || (U32(data, 80) & 0xf000) != mode ||
            (U32(data, 80) & 0x12) != 0)
            throw SoulCurrentInstallation.Unavailable();
        var mask = U32(data, 4); var fileIdOffset = 84;
        if (mask == 0x82038c0e)
        {
            if (data.Length != 100 || U64(data, 92) != 0) throw SoulCurrentInstallation.Unavailable();
        }
        else if (mask == 0x82438c0e)
        {
            fileIdOffset = 92;
            var offset = checked(84L + BinaryPrimitives.ReadInt32LittleEndian(data.Slice(84, 4)));
            var length = U32(data, 88);
            if (offset != 100 || length < 44 || length > 44 + 169 * 24 || offset + length != data.Length ||
                U32(data, checked((int)offset)) != 0x012cc16d) throw SoulCurrentInstallation.Unavailable();
            var count = U32(data, checked((int)offset + 36));
            if (count is not (0 or uint.MaxValue) || length != 44) throw SoulCurrentInstallation.Unavailable();
        }
        else throw SoulCurrentInstallation.Unavailable();
        if (I64(data, 48) is < 0 or >= 1_000_000_000 || I64(data, 64) is < 0 or >= 1_000_000_000)
            throw SoulCurrentInstallation.Unavailable();
        return new SocketNode(U32(data, 24), U64(data, 28), U64(data, fileIdOffset), uid, gid, U32(data, 80),
            I64(data, 40), I64(data, 48), I64(data, 56), I64(data, 64));
    }
    private static uint U32(ReadOnlySpan<byte> b, int n) => BinaryPrimitives.ReadUInt32LittleEndian(b.Slice(n, 4));
    private static ulong U64(ReadOnlySpan<byte> b, int n) => BinaryPrimitives.ReadUInt64LittleEndian(b.Slice(n, 8));
    private static long I64(ReadOnlySpan<byte> b, int n) => BinaryPrimitives.ReadInt64LittleEndian(b.Slice(n, 8));
}
