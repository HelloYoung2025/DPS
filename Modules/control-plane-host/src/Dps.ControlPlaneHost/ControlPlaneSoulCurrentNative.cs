using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Dps.ControlPlaneHost;

// Narrow Darwin interop: files are checked/read on held descriptors; socket attrs describe a vnode,
// not the connected socket. Protected ancestors + peer identity + typed signatures close that boundary.
internal static class ControlPlaneSoulCurrentNative
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
            throw ControlPlaneSoulCurrentInstallation.Unavailable();
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
        if (fd < 0) throw ControlPlaneSoulCurrentInstallation.Unavailable();
        return new SafeFileHandle((IntPtr)fd, true);
    }
    private static int Fd(SafeFileHandle handle) => checked((int)handle.DangerousGetHandle());
    private static Node Stat(SafeFileHandle handle)
    {
        var data = new byte[144];
        if (fstat(Fd(handle), data) != 0) throw ControlPlaneSoulCurrentInstallation.Unavailable();
        return new Node(U32(data, 0), U64(data, 8), U32(data, 16), U32(data, 20),
            BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(4)), I64(data, 48), I64(data, 56), I64(data, 64), I64(data, 72), I64(data, 96));
    }
    internal static void NoAcl(SafeFileHandle handle)
    {
        var acl = acl_get_fd_np(Fd(handle), 0x100);
        var error = Marshal.GetLastPInvokeError();
        if (acl == IntPtr.Zero)
        {
            if (error != 2) throw ControlPlaneSoulCurrentInstallation.Unavailable();
            RequireEmptyAclOnSameDescriptor(handle);
            return;
        }
        try
        {
            if (acl_valid(acl) != 0) throw ControlPlaneSoulCurrentInstallation.Unavailable();
            // Darwin returns 0 for an entry, -1/EINVAL after a valid empty ACL (not POSIX 1/0).
            if (acl_get_entry(acl, 0, out _) != -1 || Marshal.GetLastPInvokeError() != 22)
                throw ControlPlaneSoulCurrentInstallation.Unavailable();
        }
        finally { _ = acl_free(acl); }
    }
    private static void RequireEmptyAclOnSameDescriptor(SafeFileHandle handle)
    {
        var before = Stat(handle);
        var type = (before.Mode & 0xf000) switch { 0x8000 => 1u, 0x4000 => 2u, _ => throw ControlPlaneSoulCurrentInstallation.Unavailable() };
        var volume = new byte[108]; var va = new AttrList { Count = 5, Common = 0x80000006, Volume = 0xc0020000 };
        if (fgetattrlist(Fd(handle), ref va, volume, (nuint)volume.Length, 4) != 0)
            throw ControlPlaneSoulCurrentInstallation.Unavailable();
        var bytes = new byte[8192]; var attrs = new AttrList { Count = 5, Common = 0x82438c0e };
        if (fgetattrlist(Fd(handle), ref attrs, bytes, (nuint)bytes.Length, 4) != 0)
            throw ControlPlaneSoulCurrentInstallation.Unavailable();
        var length = U32(bytes, 0);
        if (length > bytes.Length) throw ControlPlaneSoulCurrentInstallation.Unavailable();
        var vnode = ParseNodeAttributes(volume, bytes.AsSpan(0, checked((int)length)), before.Uid, before.Gid, type, before.Mode & 0xf000);
        if (vnode.Device != before.Device || vnode.Inode != before.Inode || vnode.Mode != before.Mode ||
            vnode.ModifiedSeconds != before.ModifiedSeconds || vnode.ModifiedNanos != before.ModifiedNanos ||
            vnode.ChangedSeconds != before.ChangedSeconds || vnode.ChangedNanos != before.ChangedNanos || Stat(handle) != before)
            throw ControlPlaneSoulCurrentInstallation.Unavailable();
    }

    private static void Require(Node node, uint owner, uint type, bool secret)
    {
        if ((node.Mode & 0xf000) != type || node.Uid != owner || (node.Mode & 0x12) != 0 ||
            (secret && (node.Mode & 0x3f) != 0)) throw ControlPlaneSoulCurrentInstallation.Unavailable();
    }
    private static void Component(string name)
    {
        if (name.Length == 0 || name is "." or ".." || name.Contains('/') || name.Contains('\0'))
            throw ControlPlaneSoulCurrentInstallation.Unavailable();
    }

    internal static DirectoryChain OpenDirectory(string path, ControlPlaneSoulInstallationLocation location) => new(path, location);
    internal sealed class DirectoryChain : IDisposable
    {
        private readonly List<(SafeFileHandle Handle, string Name, Node Node)> _chain = [];
        private readonly uint _owner;
        internal int Descriptor => Fd(_chain[^1].Handle);
        internal DirectoryChain(string path, ControlPlaneSoulInstallationLocation location)
        {
            _owner = location.OwnerUid;
            var start = location.SyntheticBoundary ?? "/";
            if (!Path.IsPathFullyQualified(path) || Path.GetFullPath(path) != path ||
                (start != "/" && path != start && !path.StartsWith(start + "/", StringComparison.Ordinal)))
                throw ControlPlaneSoulCurrentInstallation.Unavailable();
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
                    throw ControlPlaneSoulCurrentInstallation.Unavailable();
                Require(now, _owner, 0x4000, false); NoAcl(item.Handle);
                if (index == 0) continue;
                using var byName = Handle(openat(Fd(_chain[index - 1].Handle), item.Name, 0x100100));
                var named = Stat(byName);
                if (named.Device != now.Device || named.Inode != now.Inode) throw ControlPlaneSoulCurrentInstallation.Unavailable();
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
                throw ControlPlaneSoulCurrentInstallation.Unavailable();
        }
        internal byte[] ReadFile(string name, uint owner, bool secret, int maximum, DateTime? predates = null)
        {
            Component(name); Revalidate();
            using var handle = Handle(openat(Descriptor, name, 0x100));
            var before = Stat(handle); Require(before, owner, 0x8000, secret); NoAcl(handle);
            if (predates is not null) Predates(before, predates.Value);
            if (before.Size < 1 || before.Size > maximum) throw ControlPlaneSoulCurrentInstallation.Unavailable();
            var bytes = new byte[checked((int)before.Size)]; var offset = 0;
            while (offset < bytes.Length)
            {
                var read = RandomAccess.Read(handle, bytes.AsSpan(offset), offset);
                if (read == 0) throw ControlPlaneSoulCurrentInstallation.Unavailable();
                offset += read;
            }
            if (Stat(handle) != before) throw ControlPlaneSoulCurrentInstallation.Unavailable();
            NoAcl(handle); Revalidate(); return bytes;
        }
        internal SocketNode Socket(string name, uint uid, uint gid)
        {
            Component(name); Revalidate();
            var volume = new byte[108]; var va = new AttrList { Count = 5, Common = 0x80000006, Volume = 0xc0020000 };
            if (fgetattrlist(Descriptor, ref va, volume, (nuint)volume.Length, 4) != 0)
                throw ControlPlaneSoulCurrentInstallation.Unavailable();
            var data = new byte[8192]; var sa = new AttrList { Count = 5, Common = 0x82438c0e };
            if (getattrlistat(Descriptor, name, ref sa, data, (nuint)data.Length, 5) != 0)
                throw ControlPlaneSoulCurrentInstallation.Unavailable();
            var length = U32(data, 0);
            if (length > data.Length) throw ControlPlaneSoulCurrentInstallation.Unavailable();
            var result = ParseSocket(volume, data.AsSpan(0, checked((int)length)), uid, gid);
            if (result.Device != Stat(_chain[^1].Handle).Device) throw ControlPlaneSoulCurrentInstallation.Unavailable();
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
            throw ControlPlaneSoulCurrentInstallation.Unavailable();
        var mask = U32(data, 4); var fileIdOffset = 84;
        if (mask == 0x82038c0e)
        {
            if (data.Length != 100 || U64(data, 92) != 0) throw ControlPlaneSoulCurrentInstallation.Unavailable();
        }
        else if (mask == 0x82438c0e)
        {
            fileIdOffset = 92;
            var offset = checked(84L + BinaryPrimitives.ReadInt32LittleEndian(data.Slice(84, 4)));
            var length = U32(data, 88);
            if (offset != 100 || length < 44 || length > 44 + 169 * 24 || offset + length != data.Length ||
                U32(data, checked((int)offset)) != 0x012cc16d) throw ControlPlaneSoulCurrentInstallation.Unavailable();
            var count = U32(data, checked((int)offset + 36));
            if (count is not (0 or uint.MaxValue) || length != 44) throw ControlPlaneSoulCurrentInstallation.Unavailable();
        }
        else throw ControlPlaneSoulCurrentInstallation.Unavailable();
        if (I64(data, 48) is < 0 or >= 1_000_000_000 || I64(data, 64) is < 0 or >= 1_000_000_000)
            throw ControlPlaneSoulCurrentInstallation.Unavailable();
        return new SocketNode(U32(data, 24), U64(data, 28), U64(data, fileIdOffset), uid, gid, U32(data, 80),
            I64(data, 40), I64(data, 48), I64(data, 56), I64(data, 64));
    }
    private static uint U32(ReadOnlySpan<byte> b, int n) => BinaryPrimitives.ReadUInt32LittleEndian(b.Slice(n, 4));
    private static ulong U64(ReadOnlySpan<byte> b, int n) => BinaryPrimitives.ReadUInt64LittleEndian(b.Slice(n, 8));
    private static long I64(ReadOnlySpan<byte> b, int n) => BinaryPrimitives.ReadInt64LittleEndian(b.Slice(n, 8));
}
