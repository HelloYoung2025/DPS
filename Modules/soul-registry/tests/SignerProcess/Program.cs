using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using Dps.ControlPlaneHost;
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
    if (mode is "host-auth" or "current")
    {
        using var primary = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var status = mode == "current" ? ECDsa.Create(ECCurve.NamedCurves.nistP256) : null;
        Console.WriteLine(JsonSerializer.Serialize(new { pid = Environment.ProcessId,
            primary = Convert.ToBase64String(primary.ExportSubjectPublicKeyInfo()),
            status = status is null ? "" : Convert.ToBase64String(status.ExportSubjectPublicKeyInfo()) }));
        if (await Console.In.ReadLineAsync() != "START") return 3;
        using var service = SoulCurrentSigningService.ForSyntheticProcess(location, mode == "host-auth", primary, status);
        var path = Path.Combine(location.SocketRoot, mode == "host-auth" ? "host-auth" : "current", "service.sock");
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
                try { await service.HandleConnectionAsync(socket, stop.Token); }
                catch (Exception error) when (error is not OutOfMemoryException) { }
        }
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
    if (mode is "host-read" or "host-other-alc" or "host-wait" or "host-lease")
    {
        // Referencing the actual façade forces the expected Host assembly to load from this prepared generation.
        _ = typeof(ControlPlaneSoulCurrentComposition).Assembly;
        if (mode == "host-other-alc")
            _ = new AssemblyLoadContext("synthetic-negative", true).LoadFromAssemblyPath(typeof(SoulCurrentComposition).Assembly.Location);
        if (mode == "host-wait") { Console.WriteLine("WAITING"); if (await Console.In.ReadLineAsync() != "CONTINUE") return 5; }
        var line = await Console.In.ReadLineAsync() ?? throw new ArgumentException("Missing query.");
        var query = SoulCurrentQueryV1.FromUtf8Json(Convert.FromBase64String(line));
        using var scope = SoulCurrentComposition.UseSyntheticInstallation(location);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        if (mode == "host-lease")
        {
            var client = SoulCurrentComposition.CreateForHost().Client;
            await using var lease = await client.AcquireCurrentAsync(query, timeout.Token);
            Console.WriteLine("LEASED");
            var command = await Console.In.ReadLineAsync();
            if (command == "EXPIRE") await Task.Delay(TimeSpan.FromSeconds(5.1));
            if (command == "CANCEL") timeout.Cancel();
            if (command is "REVALIDATE" or "EXPIRE" or "CANCEL")
                await lease.RevalidateAsync(timeout.Token);
            await lease.DisposeAsync(); await lease.DisposeAsync();
            Console.WriteLine("DISPOSED"); return 0;
        }
        var composition = ControlPlaneSoulCurrentComposition.Open();
        var result = await composition.ReadSoulActiveAsOfAsync(query, timeout.Token);
        Console.WriteLine("SOUL_ACTIVE_AS_OF:" + Convert.ToBase64String(SoulResolvedCanonicalV1.SerializeWire(result)));
        return 0;
    }
    return 6;
}
catch (Exception error) when (error is not OutOfMemoryException)
{
    // No stack/exception messages: Npgsql failure details can contain credential-bearing context.
    Console.WriteLine("REJECTED:" + error.GetType().Name); return 1;
}

internal static class Native
{
    [DllImport("/usr/lib/libSystem.B.dylib", SetLastError = true)] internal static extern int chown(string path, uint uid, uint gid);
}
