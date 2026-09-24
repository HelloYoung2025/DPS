using System.Buffers.Binary;
using System.Diagnostics;
using Xunit;

namespace Dps.SoulRegistry.Tests;

public sealed class SoulCurrentCompositionTests
{
    // Captured Darwin ABI bytes: parser unit fixtures, not claims of current native/production protection.
    private static readonly byte[] Volume = Convert.FromHexString("6c000000060000800000024000000000000000000000000012000001120000011a000000df6e9b01c76faf010000000000000000ffffff07ffffff010000000000000000ffffffffffff9f70070000002f360000fc1f0000fffeff3efdff8670070000002f060000fc1f0000");
    private static readonly byte[] Clean = Convert.FromHexString("640000000e8c03820000000000000000000000000000000012000001120000011a000000060000006aaab56a000000008fa1c50d000000006aaab56a00000000dffcc50d00000000f50100000000000080c100001adb3705000000000000000000000000");
    private static readonly byte[] WithAcl = Convert.FromHexString("a80000000e8c43820000000000000000000000000000000012000001120000011a000000060000006aaab56a000000008fa1c50d000000006aaab56a00000000110b230e00000000f50100000000000080c1000010000000440000001adb3705000000006dc12c0100000000000000000000000000000000000000000000000000000000000000000100000000000000abcdefabcdefabcdefabcdef0000000c0100000004000000");

    [Fact]
    [Trait("Category", "Unit")]
    public void NativeSocketParserAcceptsKnownEmptyAndRejectsCapturedRealAcl()
    {
        var node = SoulCurrentNative.ParseSocket(Volume, Clean, 501, 0);
        Assert.Equal(87546650ul, node.Inode);
        Assert.Throws<SoulRegistryException>(() => SoulCurrentNative.ParseSocket(Volume, WithAcl, 501, 0));
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("capability")]
    [InlineData("valid-capability")]
    [InlineData("valid-security")]
    [InlineData("native-security")]
    [InlineData("volume-length")]
    [InlineData("missing-mask")]
    [InlineData("extra-mask")]
    [InlineData("other-mask")]
    [InlineData("cross-volume")]
    [InlineData("wrong-type")]
    [InlineData("wrong-mode")]
    [InlineData("wrong-owner")]
    [InlineData("padding")]
    [InlineData("truncate")]
    [InlineData("offset-overflow")]
    [InlineData("acl-count")]
    public void NativeAttributeParserFailsClosedForMalformedOrUnprovedState(string fault)
    {
        var volume = Volume.ToArray(); var node = Clean.ToArray();
        void Put(byte[] b, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(offset), value);
        switch (fault)
        {
            case "capability": Put(volume, 40, 0); break;
            case "valid-capability": Put(volume, 56, 0); break;
            case "valid-security": Put(volume, 68, 0); break;
            case "native-security": Put(volume, 88, 0); break;
            case "volume-length": Put(volume, 0, 107); break;
            case "missing-mask": Put(node, 4, 0x80000000); break;
            case "extra-mask": Put(node, 4, 0x82038c0f); break;
            case "other-mask": Put(node, 8, 1); break;
            case "cross-volume": Put(node, 28, 0); break;
            case "wrong-type": Put(node, 36, 5); break;
            case "wrong-mode": Put(node, 80, 0x81a4); break;
            case "wrong-owner": Put(node, 72, 0); break;
            case "padding": Put(node, 92, 1); break;
            case "truncate": node = node[..^1]; break;
            case "offset-overflow": node = WithAcl.ToArray(); Put(node, 84, int.MaxValue); break;
            case "acl-count": node = WithAcl.ToArray(); Put(node, 136, uint.MaxValue - 1); break;
        }
        Assert.Throws<SoulRegistryException>(() => SoulCurrentNative.ParseSocket(volume, node, 501, 0));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void PublicFactoryHasNoCallerRootAndMissingProductionInstallationCloses()
    {
        Assert.Empty(typeof(SoulCurrentComposition).GetConstructors());
        Assert.Empty(typeof(SoulCurrentComposition).GetMethod(nameof(SoulCurrentComposition.CreateForHost))!.GetParameters());
        // This test requires the explicitly unprovisioned local test environment.
        Assert.False(File.Exists("/Library/Application Support/DPS/soul-current/bootstrap.v1.json"));
        Assert.Throws<SoulRegistryException>(() => SoulCurrentComposition.CreateForHost());
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void HeldFileAndDirectoryDescriptorsDetectRealAclAndRestoreWithoutModeFallback()
    {
        if (!OperatingSystem.IsMacOS()) throw new PlatformNotSupportedException();
        var root = "/private/tmp/dps-fd-" + Guid.NewGuid().ToString("N")[..12];
        Directory.CreateDirectory(root); File.SetUnixFileMode(root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var file = Path.Combine(root, "data"); File.WriteAllText(file, "owned");
        File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        try
        {
            var location = new SoulInstallationLocation(root, root, SoulCurrentNative.geteuid(), root);
            using var directory = SoulCurrentNative.OpenDirectory(root, location);
            using var handle = File.OpenHandle(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            SoulCurrentNative.NoAcl(handle); directory.Revalidate();
            Chmod("+a", "everyone allow write", file);
            Assert.Throws<SoulRegistryException>(() => SoulCurrentNative.NoAcl(handle));
            Chmod("-N", file); SoulCurrentNative.NoAcl(handle);
            Chmod("+a", "everyone allow write", root);
            Assert.Throws<SoulRegistryException>(() => directory.Revalidate());
            Chmod("-N", root); directory.Revalidate();
            Assert.Equal("owned", System.Text.Encoding.UTF8.GetString(directory.ReadFile("data", SoulCurrentNative.geteuid(), true, 16384)));
            File.CreateSymbolicLink(Path.Combine(root, "link"), "data");
            Assert.Throws<SoulRegistryException>(() => directory.ReadFile("link", SoulCurrentNative.geteuid(), false, 16384));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void EveryInstallationCorpusCaseUsesStrictCodecAndGoldenFrames()
    {
        using var stream = typeof(SoulCurrentCompositionTests).Assembly.GetManifestResourceStream("soul.current.composition.v1.corpus.json")!;
        using var document = System.Text.Json.JsonDocument.Parse(stream);
        var cases = document.RootElement.GetProperty("cases").EnumerateArray().ToArray();
        Assert.Equal(131, cases.Length);
        Assert.Equal(cases.Select(c => c.GetProperty("id").GetString()), document.RootElement.GetProperty("inventory").EnumerateArray().Select(c => c.GetString()));
        foreach (var test in cases)
        {
            var family = test.GetProperty("contract_id").GetString();
            var bytes = test.TryGetProperty("raw_base64", out var encoded) ? Convert.FromBase64String(encoded.GetString()!)
                : System.Text.Encoding.UTF8.GetBytes(test.GetProperty("payload").GetRawText());
            byte[]? frame = null;
            void Decode()
            {
                switch (family)
                {
                    case "soul.current.bootstrap/v1": _ = Dps.SoulRegistry.Contracts.SoulCurrentBootstrapV1.FromUtf8Json(bytes); break;
                    case "soul.current.configuration/v1": _ = Dps.SoulRegistry.Contracts.SoulCurrentConfigurationV1.FromUtf8Json(bytes); break;
                    case "soul.current.composition/v1": frame = Dps.SoulRegistry.Contracts.SoulCurrentCompositionV1.FromUtf8Json(bytes).CanonicalBytes(); break;
                    case "soul.current.authority-state/v1": _ = Dps.SoulRegistry.Contracts.SoulCurrentAuthorityStateV1.FromUtf8Json(bytes); break;
                    case "soul.current.status/v1": frame = Dps.SoulRegistry.Contracts.SoulCurrentStatusV1.FromUtf8Json(bytes).CanonicalBytes(); break;
                    case "soul.current.signing-protocol/v1": _ = Dps.SoulRegistry.Contracts.SoulCurrentSigningProtocolV1.FromUtf8Json(bytes); break;
                    default: Assert.Fail("Unknown current corpus family."); break;
                }
            }
            if (test.GetProperty("codec_expected").GetBoolean())
            {
                var error = Record.Exception(Decode);
                Assert.True(error is null, test.GetProperty("id").GetString() + ": " + error?.Message);
                if (test.TryGetProperty("canonical_sha256", out var hash))
                    Assert.Equal(hash.GetString(), Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(frame!)));
            }
            else Assert.Throws<ArgumentException>(Decode);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task ActualBidirectionalPeerCredentialsRejectWrongExpectedUidAndClosedSocket()
    {
        if (!OperatingSystem.IsMacOS()) throw new PlatformNotSupportedException();
        var path = "/private/tmp/dps-peer-" + Guid.NewGuid().ToString("N")[..12];
        using var listener = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.Unix, System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Unspecified);
        listener.Bind(new System.Net.Sockets.UnixDomainSocketEndPoint(path)); listener.Listen(1);
        try
        {
            using var client = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.Unix, System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Unspecified);
            await client.ConnectAsync(new System.Net.Sockets.UnixDomainSocketEndPoint(path), TestContext.Current.CancellationToken);
            using var server = await listener.AcceptAsync(TestContext.Current.CancellationToken);
            foreach (var socket in new[] { client, server })
            {
                SoulCurrentNative.RequirePeer(checked((int)socket.Handle), SoulCurrentNative.geteuid(), SoulCurrentNative.getegid());
                Assert.Throws<SoulRegistryException>(() => SoulCurrentNative.RequirePeer(checked((int)socket.Handle), SoulCurrentNative.geteuid() + 1, SoulCurrentNative.getegid()));
            }
            Assert.Throws<SoulRegistryException>(() => SoulCurrentNative.RequirePeer(-1, SoulCurrentNative.geteuid(), SoulCurrentNative.getegid()));
        }
        finally { File.Delete(path); }
    }

    private static void Chmod(params string[] args)
    {
        var start = new ProcessStartInfo("/bin/chmod") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!; process.WaitForExit(); Assert.Equal(0, process.ExitCode);
    }
}
