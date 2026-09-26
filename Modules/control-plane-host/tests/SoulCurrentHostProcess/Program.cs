using System.Runtime.Loader;
using Dps.ControlPlaneHost;
using Dps.SoulRegistry.Contracts;

if (!OperatingSystem.IsMacOS()) return 7;
if (args.Length != 3) return 2;
var mode = args[0]; var boundary = Path.GetFullPath(args[1]);
var location = new ControlPlaneSoulInstallationLocation(Path.Combine(boundary, "installation"), Path.Combine(boundary, "run"),
    ControlPlaneSoulCurrentNative.geteuid(), boundary);
try
{
    if (mode is "host-read" or "host-other-alc" or "host-wait" or "host-lease" or "host-public")
    {
        // Referencing the actual façade forces the expected Host assembly to load from this prepared generation.
        _ = typeof(ControlPlaneSoulCurrentComposition).Assembly;
        if (mode == "host-other-alc")
            _ = new AssemblyLoadContext("synthetic-negative", true).LoadFromAssemblyPath(typeof(ControlPlaneSoulCurrentComposition).Assembly.Location);
        if (mode == "host-wait") { Console.WriteLine("WAITING"); if (await Console.In.ReadLineAsync() != "CONTINUE") return 5; }
        var line = await Console.In.ReadLineAsync() ?? throw new ArgumentException("Missing query.");
        var query = SoulCurrentQueryV1.FromUtf8Json(Convert.FromBase64String(line));
        if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "Dps.SoulRegistry")) throw new InvalidOperationException("Host loaded Soul runtime.");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        if (mode == "host-lease")
        {
            using var deadline = new ControlPlaneSoulCurrentDeadline(timeout.Token);
            var installation = ControlPlaneSoulCurrentInstallation.Load(location);
            installation.VerifyLoadedArtifacts();
            var client = new ControlPlaneSoulCurrentIpcClient(installation, deadline);
            await using var lease = await client.AcquireCurrentAsync(query, timeout.Token);
            Console.WriteLine("LEASED");
            var command = await Console.In.ReadLineAsync();
            if (command == "EXPIRE") await Task.Delay(TimeSpan.FromSeconds(5.1));
            if (command == "CANCEL") timeout.Cancel();
            if (command == "CONCURRENT") await Task.WhenAll(lease.RevalidateAsync(timeout.Token), lease.DisposeAsync().AsTask());
            if (command is "REVALIDATE" or "EXPIRE" or "CANCEL")
                await lease.RevalidateAsync(timeout.Token);
            await lease.DisposeAsync(); await lease.DisposeAsync();
            if (command == "AFTER_DISPOSE") await lease.RevalidateAsync(timeout.Token);
            Console.WriteLine("DISPOSED"); return 0;
        }
        var composition = mode == "host-public" ? ControlPlaneSoulCurrentComposition.Open() : ControlPlaneSoulCurrentComposition.ForSyntheticProcess(location);
        var result = await composition.ReadSoulActiveAsOfAsync(query, timeout.Token);
        if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "Dps.SoulRegistry")) throw new InvalidOperationException("Host loaded Soul runtime.");
        Console.WriteLine("SOUL_ACTIVE_AS_OF:" + Convert.ToBase64String(SoulResolvedCanonicalV1.SerializeWire(result)));
        return 0;
    }
    return 6;
}
catch (Exception error) when (error is not OutOfMemoryException)
{
    Console.WriteLine("REJECTED:" + error.GetType().Name); return 1;
}
