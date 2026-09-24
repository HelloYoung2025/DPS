using System.Buffers.Binary;
using System.Net.Sockets;
using System.Security.Cryptography;
using Dps.SoulRegistry.Contracts;

namespace Dps.SoulRegistry;

internal sealed class SoulCurrentSigningClient(SoulCurrentInstallation installation)
{
    internal async Task<SoulCurrentSigningProtocolV1> PossessionAsync(SoulCurrentQueryV1 query,
        Guid instance, Guid session, SoulCurrentDeadline deadline)
    {
        var request = Request("host-possession", query, instance, session, deadline) with
        { MessageType = "challenge-request", ChallengeNonce = "" };
        using var socket = await ConnectAsync(true, deadline.Token);
        using var stream = new NetworkStream(socket, false);
        await WriteAsync(stream, request, deadline.Token);
        var challenge = await ReadAsync(stream, deadline.Token);
        RequireAssociation(request, challenge);
        if (challenge.Operation != "host-possession" || challenge.MessageType != "challenge")
            throw SoulCurrentInstallation.Unavailable();
        var answer = challenge with { MessageType = "answer" };
        await WriteAsync(stream, answer, deadline.Token);
        var proof = await ReadAsync(stream, deadline.Token);
        RequireAssociation(request, proof);
        if (proof.Operation != "host-possession" || proof.MessageType != "proof" ||
            proof.ChallengeNonce != challenge.ChallengeNonce) throw SoulCurrentInstallation.Unavailable();
        SoulCurrentInstallation.Verify(installation.Configuration.HostAuthSpkiBase64, proof.CanonicalBytes(), proof.SignatureBase64);
        deadline.RequireLive();
        return proof;
    }

    internal async Task<SoulCurrentStatusV1> StatusAsync(SoulCurrentQueryV1 query,
        Guid instance, Guid session, SoulCurrentSigningProtocolV1 proof, SoulCurrentDeadline deadline)
    {
        var request = Request("read-current-status", query, instance, session, deadline) with
        { PossessionBase64 = Convert.ToBase64String(proof.ToUtf8Json()) };
        var response = await ExchangeAsync(request, deadline);
        var state = SoulCurrentStatusV1.FromUtf8Json(Convert.FromBase64String(response.StatusBase64));
        RequireStatus(state, query, instance, session, request.RequestId, request.ChallengeNonce);
        deadline.RequireLive();
        return state;
    }

    internal async Task<SoulCurrentResolutionV1> SignCurrentAsync(SoulCurrentQueryV1 query,
        Guid instance, Guid session, SoulCurrentSigningProtocolV1 proof, SoulCurrentStatusV1 status,
        SoulCurrentDeadline deadline)
    {
        var request = Request("sign-current", query, instance, session, deadline) with
        {
            PossessionBase64 = Convert.ToBase64String(proof.ToUtf8Json()),
            StatusBase64 = Convert.ToBase64String(status.ToUtf8Json())
        };
        var response = await ExchangeAsync(request, deadline);
        var resolution = SoulCurrentResolutionV1.FromUtf8Json(Convert.FromBase64String(response.ResolutionBase64));
        resolution.RequireMatches(query); resolution.RequireValidWindow(DateTimeOffset.UtcNow);
        if (resolution.KeyId != installation.Configuration.CurrentKeyId || resolution.Status != "ACTIVE" ||
            resolution.TrustEpoch != status.TrustEpoch || resolution.ReleaseBomSha256 != status.ReleaseBomSha256 ||
            resolution.ReleaseGeneration != status.ReleaseGeneration || resolution.ExpiresAt > deadline.ExpiresAt)
            throw SoulCurrentInstallation.Unavailable();
        SoulCurrentInstallation.Verify(installation.Configuration.CurrentSpkiBase64, resolution.CanonicalBytes(), resolution.SignatureBase64);
        deadline.RequireLive(); return resolution;
    }

    internal void RequireStatus(SoulCurrentStatusV1 state, SoulCurrentQueryV1 query, Guid instance,
        Guid session, Guid request, string nonce)
    {
        var c = installation.Configuration;
        SoulCurrentInstallation.Verify(c.StatusSpkiBase64, state.CanonicalBytes(), state.SignatureBase64);
        SoulCurrentInstallation.RequireWindow(state.IssuedAt, state.ExpiresAt);
        if (state.RequestId != request || state.Nonce != nonce || state.QuerySha256 != query.ComputeSha256() ||
            state.InstanceId != instance || state.SessionId != session || state.ConfigurationSha256 != installation.ConfigurationSha256 ||
            state.TenantRef != c.TenantRef || state.SoulId != c.SoulId || state.PrincipalId != c.PrincipalId ||
            state.Permission != c.Permission || state.Status != "ACTIVE" ||
            state.ReleaseBomSha256 != installation.Composition.ReleaseBomSha256 ||
            state.ReleaseGeneration != installation.Composition.ReleaseGeneration || state.HostAuthKeyId != c.HostAuthKeyId ||
            state.CurrentKeyId != c.CurrentKeyId || state.StatusKeyId != c.StatusKeyId)
            throw SoulCurrentInstallation.Unavailable();
    }

    private SoulCurrentSigningProtocolV1 Request(string operation, SoulCurrentQueryV1 query,
        Guid instance, Guid session, SoulCurrentDeadline deadline) => new()
    {
        Operation = operation, MessageType = "request", RequestId = Guid.NewGuid(), InstanceId = instance, SessionId = session,
        QueryBase64 = Convert.ToBase64String(query.ToUtf8Json()), QuerySha256 = query.ComputeSha256(), ConfigurationSha256 = installation.ConfigurationSha256,
        ReleaseBomSha256 = installation.Composition.ReleaseBomSha256, ReleaseGeneration = installation.Composition.ReleaseGeneration,
        ExpiresAt = deadline.ExpiresAt, ChallengeNonce = Nonce(), PossessionBase64 = "", StatusBase64 = "",
        ResolutionBase64 = "", ErrorCode = "", SignatureBase64 = ""
    };

    private async Task<SoulCurrentSigningProtocolV1> ExchangeAsync(SoulCurrentSigningProtocolV1 request,
        SoulCurrentDeadline deadline)
    {
        using var socket = await ConnectAsync(false, deadline.Token);
        using var stream = new NetworkStream(socket, false);
        await WriteAsync(stream, request, deadline.Token);
        var response = await ReadAsync(stream, deadline.Token);
        RequireAssociation(request, response);
        if (response.Operation != request.Operation || response.MessageType != "response" ||
            response.ChallengeNonce != request.ChallengeNonce) throw SoulCurrentInstallation.Unavailable();
        deadline.RequireLive(); return response;
    }

    private async Task<Socket> ConnectAsync(bool hostAuth, CancellationToken token)
    {
        var directory = Path.Combine(installation.Location.SocketRoot, hostAuth ? "host-auth" : "current");
        var c = installation.Configuration;
        var uid = checked((uint)(hostAuth ? c.HostAuthUid : c.CurrentServiceUid));
        var gid = checked((uint)(hostAuth ? c.HostAuthGid : c.CurrentServiceGid));
        using var parent = SoulCurrentNative.OpenDirectory(directory, installation.Location);
        var before = parent.Socket("service.sock", uid, gid);
        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            await socket.ConnectAsync(new UnixDomainSocketEndPoint(Path.Combine(directory, "service.sock")), token);
            SoulCurrentNative.RequirePeer(checked((int)socket.Handle), uid, gid);
            if (parent.Socket("service.sock", uid, gid) != before) throw SoulCurrentInstallation.Unavailable();
            return socket;
        }
        catch { socket.Dispose(); throw; }
    }

    internal static string Nonce() => "nonce_" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
    internal static void RequireAssociation(SoulCurrentSigningProtocolV1 request, SoulCurrentSigningProtocolV1 response)
    {
        if (response.RequestId != request.RequestId || response.InstanceId != request.InstanceId ||
            response.SessionId != request.SessionId || response.QueryBase64 != request.QueryBase64 || response.QuerySha256 != request.QuerySha256 ||
            response.ConfigurationSha256 != request.ConfigurationSha256 || response.ReleaseBomSha256 != request.ReleaseBomSha256 ||
            response.ReleaseGeneration != request.ReleaseGeneration || response.ExpiresAt != request.ExpiresAt)
            throw SoulCurrentInstallation.Unavailable();
    }

    internal static async Task WriteAsync(Stream stream, SoulCurrentSigningProtocolV1 value, CancellationToken token)
    {
        var bytes = value.ToUtf8Json();
        if (bytes.Length is < 1 or > 16384) throw SoulCurrentInstallation.Unavailable();
        var header = new byte[4]; BinaryPrimitives.WriteUInt32BigEndian(header, checked((uint)bytes.Length));
        await stream.WriteAsync(header, token); await stream.WriteAsync(bytes, token); await stream.FlushAsync(token);
    }
    internal static async Task<SoulCurrentSigningProtocolV1> ReadAsync(Stream stream, CancellationToken token)
    {
        var header = new byte[4]; await stream.ReadExactlyAsync(header, token);
        var size = BinaryPrimitives.ReadUInt32BigEndian(header);
        if (size is < 1 or > 16384) throw SoulCurrentInstallation.Unavailable();
        var bytes = new byte[checked((int)size)]; await stream.ReadExactlyAsync(bytes, token);
        return SoulCurrentSigningProtocolV1.FromUtf8Json(bytes);
    }
}
