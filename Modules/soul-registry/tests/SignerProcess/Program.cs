using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using Dps.SoulRegistry;
using Dps.SoulRegistry.Contracts;

// Owned synthetic processes only. Private keys are generated here and never exported to the test parent/Host.
if (!OperatingSystem.IsMacOS()) return 7;
if (args.Length != 3) return 2;
var mode = args[0]; var boundary = Path.GetFullPath(args[1]);
var location = new SoulInstallationLocation(Path.Combine(boundary, "installation"), Path.Combine(boundary, "run"),
    SoulCurrentNative.geteuid(), boundary);
try
{
    if (mode is "host-auth" or "current" or "provider")
    {
        using var primary = mode == "provider" ? null : ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var status = mode == "current" ? ECDsa.Create(ECCurve.NamedCurves.nistP256) : null;
        Console.WriteLine(JsonSerializer.Serialize(new { pid = Environment.ProcessId,
            primary = primary is null ? "" : Convert.ToBase64String(primary.ExportSubjectPublicKeyInfo()),
            status = status is null ? "" : Convert.ToBase64String(status.ExportSubjectPublicKeyInfo()) }));
        if (await Console.In.ReadLineAsync() != "START") return 3;
        using var service = mode == "provider" ? null : SoulCurrentSigningService.ForSyntheticProcess(location, mode == "host-auth", primary!, status);
        var provider = mode == "provider" ? SoulCurrentProviderService.ForSyntheticProcess(location) : null;
        var path = Path.Combine(location.SocketRoot, mode, "service.sock");
        using var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        listener.Bind(new UnixDomainSocketEndPoint(path));
        if (Native.chown(path, SoulCurrentNative.geteuid(), SoulCurrentNative.getegid()) != 0) return 4;
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        listener.Listen(16);
        Console.WriteLine("READY");
        using var stop = new CancellationTokenSource();
        var shutdown = Task.Run(async () => { _ = await Console.In.ReadLineAsync(); stop.Cancel(); });
        var active = new List<Task>();
        try
        {
            while (!stop.IsCancellationRequested)
            {
                var socket = await listener.AcceptAsync(stop.Token);
                active.RemoveAll(task => task.IsCompleted);
                if (active.Count >= 16) { socket.Dispose(); continue; }
                active.Add(HandleAsync(socket));
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        await Task.WhenAll(active); await shutdown;
        File.Delete(path); return 0;
        async Task HandleAsync(Socket socket)
        {
            using (socket)
                try { if (provider is not null) await provider.HandleConnectionAsync(socket, stop.Token);
                    else await service!.HandleConnectionAsync(socket, stop.Token); }
                catch (Exception error) when (error is not OutOfMemoryException) { }
        }
    }
    if (mode == "provider-proxy")
    {
        var directory = Path.Combine(location.SocketRoot, "provider"); var path = Path.Combine(directory, "service.sock");
        using var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        listener.Bind(new UnixDomainSocketEndPoint(path));
        if (Native.chown(path, SoulCurrentNative.geteuid(), SoulCurrentNative.getegid()) != 0) return 4;
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite); listener.Listen(1);
        using var stop = new CancellationTokenSource();
        var shutdown = Task.Run(async () => { _ = await Console.In.ReadLineAsync(); stop.Cancel(); });
        Console.WriteLine("READY");
        try
        {
            using var caller = await listener.AcceptAsync(stop.Token);
            using var target = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            await target.ConnectAsync(new UnixDomainSocketEndPoint(Path.Combine(directory, "backend.sock")), stop.Token);
            using var input = new NetworkStream(caller, false); using var backend = new NetworkStream(target, false);
            SoulCurrentLeaseSessionV1? acquired = null;
            while (!stop.IsCancellationRequested)
            {
                var request = await SoulCurrentLeaseSessionV1.ReadAsync(input, stop.Token);
                await SoulCurrentLeaseSessionV1.WriteAsync(backend, request, stop.Token);
                var response = await SoulCurrentLeaseSessionV1.ReadAsync(backend, stop.Token);
                if (response.Phase == "acquire/acquired") acquired = response;
                if (response.Phase == "dispose/ack")
                {
                    Console.WriteLine("REAL_RELEASE_ACK");
                    if (args[2] == "drop-ack") break;
                    if (args[2] == "delay-ack") await Task.Delay(5100, stop.Token);
                    if (args[2] == "stale-ack") await Task.Delay(2200, stop.Token);
                }
                if (response.Phase == "revalidate/response" && args[2] == "old-status") response = acquired!;
                if (response.Phase == "revalidate/response" && args[2] == "wrong-final-nonce")
                {
                    var node = System.Text.Json.Nodes.JsonNode.Parse(response.ToUtf8Json())!.AsObject();
                    node["nonce"] = SoulCurrentSigningClient.Nonce();
                    var body = System.Text.Encoding.UTF8.GetBytes(node.ToJsonString()); var header = new byte[4];
                    System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(header, (uint)body.Length);
                    await input.WriteAsync(header, stop.Token); await input.WriteAsync(body, stop.Token); break;
                }
                await SoulCurrentLeaseSessionV1.WriteAsync(input, response, stop.Token);
                if (response.Phase == "dispose/ack") break;
            }
        }
        catch (Exception error) when (error is not OutOfMemoryException) { }
        await shutdown; File.Delete(path); return 0;
    }
    if (mode == "late-current")
    {
        var directory = Path.Combine(location.SocketRoot, "current");
        var path = Path.Combine(directory, "service.sock");
        using var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        listener.Bind(new UnixDomainSocketEndPoint(path));
        if (Native.chown(path, SoulCurrentNative.geteuid(), SoulCurrentNative.getegid()) != 0) return 4;
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite); listener.Listen(1);
        using var stop = new CancellationTokenSource();
        var shutdown = Task.Run(async () => { _ = await Console.In.ReadLineAsync(); stop.Cancel(); });
        Console.WriteLine("READY");
        try
        {
            using var caller = await listener.AcceptAsync(stop.Token);
            using var target = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            await target.ConnectAsync(new UnixDomainSocketEndPoint(Path.Combine(directory, "backend.sock")), stop.Token);
            using var input = new NetworkStream(caller, false); using var backend = new NetworkStream(target, false);
            var request = await SoulCurrentSigningClient.ReadAsync(input, stop.Token);
            await SoulCurrentSigningClient.WriteAsync(backend, request, stop.Token);
            var response = await SoulCurrentSigningClient.ReadAsync(backend, stop.Token);
            if (response.Operation != "read-current-status" || response.MessageType != "response") return 8;
            var status = SoulCurrentStatusV1.FromUtf8Json(Convert.FromBase64String(response.StatusBase64));
            var installation = SoulCurrentInstallation.Load(location);
            SoulCurrentInstallation.Verify(installation.Configuration.StatusSpkiBase64, status.CanonicalBytes(), status.SignatureBase64);
            SoulCurrentInstallation.RequireWindow(status.IssuedAt, status.ExpiresAt);
            Console.WriteLine("SIGNED_RESPONSE_CAPTURED");
            await Task.Delay(TimeSpan.FromSeconds(6), stop.Token);
            await SoulCurrentSigningClient.WriteAsync(input, response, stop.Token);
        }
        catch (Exception error) when (error is not OutOfMemoryException) { }
        await shutdown; File.Delete(path); return 0;
    }
    return 6;
}
catch (Exception error) when (error is not OutOfMemoryException)
{
    // No stack/exception messages: Npgsql failure details can contain credential-bearing context.
    Console.WriteLine("REJECTED:" + error.GetType().Name + ":" + string.Join(";", new System.Diagnostics.StackTrace(error, true).GetFrames().Select(f => f.GetMethod()?.DeclaringType?.Name + "." + f.GetMethod()?.Name + ":" + f.GetFileLineNumber()))); return 1;
}

internal static class Native
{
    [DllImport("/usr/lib/libSystem.B.dylib", SetLastError = true)] internal static extern int chown(string path, uint uid, uint gid);
}
