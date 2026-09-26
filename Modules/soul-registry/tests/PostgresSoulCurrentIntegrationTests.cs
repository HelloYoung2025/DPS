using Dps.SoulRegistry.Contracts;
using Xunit;

namespace Dps.SoulRegistry.Tests;

public sealed class PostgresSoulCurrentIntegrationTests
{
    [Theory]
    [Trait("Category", "Integration")]
    [InlineData("provider", "INSERT")]
    [InlineData("provider", "UPDATE")]
    [InlineData("provider", "REFERENCES")]
    [InlineData("provider", "MAINTAIN")]
    [InlineData("signer", "INSERT")]
    [InlineData("signer", "UPDATE")]
    [InlineData("signer", "REFERENCES")]
    [InlineData("signer", "MAINTAIN")]
    [InlineData("PUBLIC", "INSERT")]
    [InlineData("PUBLIC", "UPDATE")]
    [InlineData("PUBLIC", "REFERENCES")]
    [InlineData("PUBLIC", "MAINTAIN")]
    public async Task ActualHostRejectsEffectiveNonSelectPrivilegeAndRecoversAfterRevoke(string grantee, string privilege)
    {
        if (!OperatingSystem.IsMacOS()) throw new PlatformNotSupportedException();
        var token = TestContext.Current.CancellationToken;
        await using var fixture = await SoulCurrentProcessFixture.CreateAsync(token);
        Assert.Equal(0, (await fixture.ReadHostAsync(null, token)).Exit);
        var before = await fixture.BusinessSnapshotAsync(token);
        var roles = grantee == "PUBLIC" ? new[] { false, true } : new[] { grantee == "signer" };
        foreach (var signer in roles) Assert.False(await fixture.HasRuntimePrivilegeAsync(signer, privilege, token));
        await fixture.SetRuntimePrivilegeAsync(grantee, privilege, true, token);
        try
        {
            foreach (var signer in roles) Assert.True(await fixture.HasRuntimePrivilegeAsync(signer, privilege, token));
            // PUBLIC is effective for both logins; provider-first refusal is not signer execution evidence.
            var result = await fixture.ReadHostAsync(null, token);
            Assert.Equal(1, result.Exit);
            Assert.StartsWith("REJECTED:", result.Output, StringComparison.Ordinal);
            Assert.Equal(before, await fixture.BusinessSnapshotAsync(token));
        }
        finally { await fixture.SetRuntimePrivilegeAsync(grantee, privilege, false, CancellationToken.None); }
        foreach (var signer in roles) Assert.False(await fixture.HasRuntimePrivilegeAsync(signer, privilege, token));
        Assert.Equal(0, (await fixture.ReadHostAsync(null, token)).Exit);
        Assert.Equal(before, await fixture.BusinessSnapshotAsync(token));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task IndependentSignersAndActualHostReadReturnScopedAsOfWithoutWrites()
    {
        if (!OperatingSystem.IsMacOS()) throw new PlatformNotSupportedException("Real macOS IPC required.");
        var token = TestContext.Current.CancellationToken;
        await using var fixture = await SoulCurrentProcessFixture.CreateAsync(token);
        Assert.Equal(2, fixture.SignerProcessIds.Distinct().Count());
        var before = await fixture.BusinessSnapshotAsync(token);
        var result = await fixture.ReadHostAsync(null, token);
        Assert.True(result.Exit == 0, result.Output);
        Assert.StartsWith("SOUL_ACTIVE_AS_OF:", result.Output, StringComparison.Ordinal);
        var soul = SoulResolvedCanonicalV1.FromUtf8Json(Convert.FromBase64String(result.Output[18..]));
        Assert.Equal(fixture.Query.SoulId, soul.SoulId);
        Assert.Equal(fixture.Query.DeviceBindingId, soul.DeviceBindingId);
        Assert.Equal(before, await fixture.BusinessSnapshotAsync(token));
    }

    [Theory]
    [Trait("Category", "Integration")]
    [InlineData("tenant")]
    [InlineData("soul")]
    [InlineData("purpose")]
    [InlineData("revision")]
    public async Task ActualHostRejectsCrossScopeOrWrongRevisionAfterHealthyBaseline(string mutation)
    {
        if (!OperatingSystem.IsMacOS()) throw new PlatformNotSupportedException();
        var token = TestContext.Current.CancellationToken;
        await using var fixture = await SoulCurrentProcessFixture.CreateAsync(token);
        Assert.Equal(0, (await fixture.ReadHostAsync(null, token)).Exit);
        var before = await fixture.BusinessSnapshotAsync(token);
        var query = mutation switch
        {
            "tenant" => fixture.Query with { TenantRef = "tenant_" + new string('e', 64) },
            "soul" => fixture.Query with { SoulId = "soul_" + new string('e', 64) },
            "purpose" => fixture.Query with { ProducerModule = "binding", Purpose = "binding-activate" },
            _ => fixture.Query with { ExpectedResolutionRevision = 2 }
        };
        Assert.Equal(1, (await fixture.ReadHostAsync(query, token)).Exit);
        Assert.Equal(before, await fixture.BusinessSnapshotAsync(token));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task FinalRealStatusRpcRejectsRevocationWhileActualLeaseRemainsHeld()
    {
        if (!OperatingSystem.IsMacOS()) throw new PlatformNotSupportedException();
        var token = TestContext.Current.CancellationToken;
        await using var fixture = await SoulCurrentProcessFixture.CreateAsync(token);
        using var host = await fixture.AcquireLeaseProcessAsync(token);
        Assert.True(await fixture.AdvisoryLockCountAsync(true, token) > 0);
        var before = await fixture.BusinessSnapshotAsync(token);
        fixture.SetAuthorityState(fixture.State with { Status = "REVOKED", AuthorizationRevision = 2, KeyRevocationEpoch = 1 });
        await host.StandardInput.WriteLineAsync("REVALIDATE"); await host.StandardInput.FlushAsync(token);
        var output = await host.StandardOutput.ReadToEndAsync(token); await host.WaitForExitAsync(token);
        Assert.Equal(1, host.ExitCode); Assert.StartsWith("REJECTED:", output, StringComparison.Ordinal);
        Assert.Equal(0, await fixture.AdvisoryLockCountAsync(true, token));
        Assert.Equal(before, await fixture.BusinessSnapshotAsync(token));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AliasRevocationWaitsForRealLeaseThenOriginalOwnerStateRejectsNewRead()
    {
        if (!OperatingSystem.IsMacOS()) throw new PlatformNotSupportedException();
        var token = TestContext.Current.CancellationToken;
        await using var fixture = await SoulCurrentProcessFixture.CreateAsync(token);
        using var host = await fixture.AcquireLeaseProcessAsync(token);
        var revoke = fixture.RevokeAliasAsync(token);
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(token); bounded.CancelAfter(TimeSpan.FromSeconds(2));
        while (await fixture.AdvisoryLockCountAsync(false, bounded.Token) == 0) await Task.Delay(10, bounded.Token);
        Assert.False(revoke.IsCompleted);
        await host.StandardInput.WriteLineAsync("DISPOSE"); await host.StandardInput.FlushAsync(token);
        Assert.Equal("DISPOSED", (await host.StandardOutput.ReadToEndAsync(token)).Trim());
        await host.WaitForExitAsync(token); Assert.Equal(0, host.ExitCode);
        await revoke;
        Assert.Equal(1, (await fixture.ReadHostAsync(null, token)).Exit);
    }

    [Theory]
    [Trait("Category", "Integration")]
    [InlineData("EXPIRE")]
    [InlineData("CANCEL")]
    [InlineData("BACKEND")]
    public async Task LostCancelledOrExpiredLeaseCannotReturnSuccessAndReleasesSession(string failure)
    {
        if (!OperatingSystem.IsMacOS()) throw new PlatformNotSupportedException();
        var token = TestContext.Current.CancellationToken;
        await using var fixture = await SoulCurrentProcessFixture.CreateAsync(token);
        using var host = await fixture.AcquireLeaseProcessAsync(token);
        var before = await fixture.BusinessSnapshotAsync(token);
        if (failure == "BACKEND") await fixture.TerminateProviderAsync(token);
        await host.StandardInput.WriteLineAsync(failure == "BACKEND" ? "REVALIDATE" : failure);
        await host.StandardInput.FlushAsync(token);
        var output = await host.StandardOutput.ReadToEndAsync(token); await host.WaitForExitAsync(token);
        Assert.Equal(1, host.ExitCode); Assert.StartsWith("REJECTED:", output, StringComparison.Ordinal);
        Assert.Equal(0, await fixture.AdvisoryLockCountAsync(true, token));
        Assert.Equal(before, await fixture.BusinessSnapshotAsync(token));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task DistinctRuntimeLoginsCannotWriteAndOtherLoadedAssemblyIsRejected()
    {
        if (!OperatingSystem.IsMacOS()) throw new PlatformNotSupportedException();
        var token = TestContext.Current.CancellationToken;
        await using var fixture = await SoulCurrentProcessFixture.CreateAsync(token);
        Assert.Equal(0, (await fixture.ReadHostAsync(null, token)).Exit);
        Assert.NotEqual(fixture.Configuration.DatabaseRuntimeRole, fixture.Configuration.SignerDatabaseRuntimeRole);
        var before = await fixture.BusinessSnapshotAsync(token);
        Assert.Equal("42501", await fixture.DeniedRuntimeWriteAsync(false, token));
        Assert.Equal("42501", await fixture.DeniedRuntimeWriteAsync(true, token));
        Assert.Equal(1, (await fixture.ReadHostAsync(null, token, "host-other-alc")).Exit);
        Assert.Equal(before, await fixture.BusinessSnapshotAsync(token));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AuthorizedHostCannotHaveCallerSuppliedOwnerFactsSignedAndReplayIsRejected()
    {
        if (!OperatingSystem.IsMacOS()) throw new PlatformNotSupportedException();
        var token = TestContext.Current.CancellationToken;
        await using var fixture = await SoulCurrentProcessFixture.CreateAsync(token);
        Assert.Equal(0, (await fixture.ReadHostAsync(null, token)).Exit);
        var before = await fixture.BusinessSnapshotAsync(token);
        var location = new SoulInstallationLocation(Path.Combine(fixture.Root, "installation"), Path.Combine(fixture.Root, "run"), SoulCurrentNative.geteuid(), fixture.Root);
        var installation = SoulCurrentInstallation.Load(location);
        var signing = new SoulCurrentSigningClient(installation);
        using var deadline = new SoulCurrentDeadline(token);
        var instance = Guid.NewGuid(); var session = Guid.NewGuid();
        var proof = await signing.PossessionAsync(fixture.Query, instance, session, deadline);
        var request = proof with
        {
            Operation = "read-current-status", MessageType = "request", RequestId = Guid.NewGuid(),
            ChallengeNonce = SoulCurrentSigningClient.Nonce(), PossessionBase64 = Convert.ToBase64String(proof.ToUtf8Json()), SignatureBase64 = ""
        };
        var statusResponse = SoulCurrentSigningProtocolV1.FromUtf8Json(await RawExchangeAsync(fixture.Root, request.ToUtf8Json(), token));
        Assert.Equal("read-current-status", statusResponse.Operation);
        var replay = SoulCurrentSigningProtocolV1.FromUtf8Json(await RawExchangeAsync(fixture.Root, request.ToUtf8Json(), token));
        Assert.Equal("error", replay.Operation); Assert.Equal("AUTHORITY_UNAVAILABLE", replay.ErrorCode);
        var status = SoulCurrentStatusV1.FromUtf8Json(Convert.FromBase64String(statusResponse.StatusBase64));
        var sign = request with { Operation = "sign-current", RequestId = Guid.NewGuid(), ChallengeNonce = SoulCurrentSigningClient.Nonce(), StatusBase64 = statusResponse.StatusBase64 };
        var forged = System.Text.Json.Nodes.JsonNode.Parse(sign.ToUtf8Json())!.AsObject();
        forged["owner_revision"] = 999;
        Assert.Empty(await RawExchangeAsync(fixture.Root, System.Text.Encoding.UTF8.GetBytes(forged.ToJsonString()), token));
        // The same real authorization still signs only a new independently read owner snapshot.
        var genuine = await signing.SignCurrentAsync(fixture.Query, instance, session, proof, status, deadline);
        Assert.Equal(1, genuine.ResolutionRevision); Assert.Equal(fixture.Query.SoulId, genuine.SoulId);
        var secondInstance = Guid.NewGuid(); var secondSession = Guid.NewGuid();
        var secondProof = await signing.PossessionAsync(fixture.Query, secondInstance, secondSession, deadline);
        var secondStatus = await signing.StatusAsync(fixture.Query, secondInstance, secondSession, secondProof, deadline);
        var secondRequest = secondProof with { Operation = "sign-current", MessageType = "request", RequestId = Guid.NewGuid(),
            ChallengeNonce = SoulCurrentSigningClient.Nonce(), PossessionBase64 = Convert.ToBase64String(secondProof.ToUtf8Json()),
            StatusBase64 = Convert.ToBase64String(secondStatus.ToUtf8Json()), SignatureBase64 = "" };
        var supplied = System.Text.Json.Nodes.JsonNode.Parse(secondRequest.ToUtf8Json())!.AsObject();
        supplied["resolution_base64"] = Convert.ToBase64String((genuine with { ResolutionRevision = 999 }).ToUtf8Json());
        Assert.Empty(await RawExchangeAsync(fixture.Root, System.Text.Encoding.UTF8.GetBytes(supplied.ToJsonString()), token));
        Assert.Equal(1, (await signing.SignCurrentAsync(fixture.Query, secondInstance, secondSession, secondProof, secondStatus, deadline)).ResolutionRevision);
        Assert.Equal(before, await fixture.BusinessSnapshotAsync(token));
    }

    [Theory]
    [Trait("Category", "Integration")]
    [InlineData("signature")]
    [InlineData("query")]
    [InlineData("session")]
    [InlineData("bom")]
    [InlineData("role")]
    public async Task TypedServiceRejectsChangedProofAssociationsAfterRealHealthyRead(string mutation)
    {
        if (!OperatingSystem.IsMacOS()) throw new PlatformNotSupportedException();
        var token = TestContext.Current.CancellationToken;
        await using var fixture = await SoulCurrentProcessFixture.CreateAsync(token);
        Assert.Equal(0, (await fixture.ReadHostAsync(null, token)).Exit);
        var before = await fixture.BusinessSnapshotAsync(token);
        var installation = SoulCurrentInstallation.Load(new SoulInstallationLocation(Path.Combine(fixture.Root, "installation"), Path.Combine(fixture.Root, "run"), SoulCurrentNative.geteuid(), fixture.Root));
        using var deadline = new SoulCurrentDeadline(token);
        var signing = new SoulCurrentSigningClient(installation);
        var proof = await signing.PossessionAsync(fixture.Query, Guid.NewGuid(), Guid.NewGuid(), deadline);
        var changed = mutation == "signature" ? proof with { SignatureBase64 = Convert.ToBase64String(new byte[64]) } : proof;
        var request = proof with { Operation = "read-current-status", MessageType = "request", RequestId = Guid.NewGuid(),
            ChallengeNonce = SoulCurrentSigningClient.Nonce(), SignatureBase64 = "", PossessionBase64 = Convert.ToBase64String(changed.ToUtf8Json()) };
        request = mutation switch
        {
            "query" => ChangeQueryCommitment(request, fixture.Query with { Nonce = SoulCurrentSigningClient.Nonce() }),
            "session" => request with { SessionId = Guid.NewGuid() },
            "bom" => request with { ReleaseBomSha256 = new string('e', 64) },
            _ => request
        };
        var bytes = request.ToUtf8Json();
        if (mutation == "role")
        {
            var bad = System.Text.Json.Nodes.JsonNode.Parse(bytes)!.AsObject(); bad["key_role"] = "installation-root";
            bytes = System.Text.Encoding.UTF8.GetBytes(bad.ToJsonString());
        }
        var response = await RawExchangeAsync(fixture.Root, bytes, token);
        if (mutation == "role") Assert.Empty(response);
        else Assert.Equal("error", SoulCurrentSigningProtocolV1.FromUtf8Json(response).Operation);
        Assert.Equal(before, await fixture.BusinessSnapshotAsync(token));
    }

    [Theory]
    [Trait("Category", "Integration")]
    [InlineData("oversize")]
    [InlineData("truncated")]
    [InlineData("utf8")]
    public async Task ActualStreamRejectsMalformedFramesWithoutBusinessWrites(string failure)
    {
        if (!OperatingSystem.IsMacOS()) throw new PlatformNotSupportedException();
        var token = TestContext.Current.CancellationToken;
        await using var fixture = await SoulCurrentProcessFixture.CreateAsync(token);
        Assert.Equal(0, (await fixture.ReadHostAsync(null, token)).Exit);
        var before = await fixture.BusinessSnapshotAsync(token);
        var response = await RawExchangeAsync(fixture.Root, new byte[] { 255 }, token, failure == "oversize" ? 16385u : failure == "truncated" ? 100u : 1u);
        Assert.Empty(response); Assert.Equal(before, await fixture.BusinessSnapshotAsync(token));
    }

    [Theory]
    [Trait("Category", "Integration")]
    [InlineData("disabled")]
    [InlineData("root-key")]
    [InlineData("root-signature")]
    [InlineData("configuration")]
    [InlineData("bom")]
    [InlineData("key-role")]
    [InlineData("group-writable")]
    [InlineData("symlink")]
    public async Task HealthyClosedCompositionRejectsChangedInstallationMaterial(string fault)
    {
        if (!OperatingSystem.IsMacOS()) throw new PlatformNotSupportedException();
        var token = TestContext.Current.CancellationToken;
        await using var fixture = await SoulCurrentProcessFixture.CreateAsync(token);
        Assert.Equal(0, (await fixture.ReadHostAsync(null, token)).Exit);
        var before = await fixture.BusinessSnapshotAsync(token);
        var relative = fault is "disabled" or "root-key" ? "bootstrap.v1.json" : fault == "configuration" ? "configuration.v1.json" : "composition.v1.json";
        var path = Path.Combine(fixture.Root, "installation", relative);
        var node = System.Text.Json.Nodes.JsonNode.Parse(await File.ReadAllBytesAsync(path, token))!.AsObject();
        switch (fault)
        {
            case "disabled": node["enabled"] = false; break;
            case "root-key": node["root_spki_base64"] = fixture.Configuration.HostAuthSpkiBase64; break;
            case "root-signature": node["signature_base64"] = Convert.ToBase64String(new byte[64]); break;
            case "configuration": node["tenant_ref"] = "tenant_" + new string('e', 64); break;
            case "bom": node["release_bom_sha256"] = new string('e', 64); break;
            case "key-role": node["key_role"] = "host-possession"; break;
        }
        if (fault == "group-writable") File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupWrite);
        else if (fault == "symlink") { File.Move(path, path + ".original"); File.CreateSymbolicLink(path, Path.GetFileName(path) + ".original"); }
        else fixture.Write(relative, System.Text.Encoding.UTF8.GetBytes(node.ToJsonString()));
        Assert.Equal(1, (await fixture.ReadHostAsync(null, token)).Exit);
        Assert.Equal(before, await fixture.BusinessSnapshotAsync(token));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task RealSignedButLateRpcResponseCannotRenewDeadlineOrKeepOwnerLock()
    {
        if (!OperatingSystem.IsMacOS()) throw new PlatformNotSupportedException();
        var token = TestContext.Current.CancellationToken;
        await using var fixture = await SoulCurrentProcessFixture.CreateAsync(token);
        Assert.Equal(0, (await fixture.ReadHostAsync(null, token)).Exit);
        var before = await fixture.BusinessSnapshotAsync(token);
        var proxy = await fixture.DelayCurrentResponseAsync(token);
        var read = fixture.ReadHostAsync(null, token);
        Assert.Equal("SIGNED_RESPONSE_CAPTURED", await proxy.StandardOutput.ReadLineAsync(token));
        var rejected = await read;
        Assert.Equal(1, rejected.Exit); Assert.StartsWith("REJECTED:", rejected.Output, StringComparison.Ordinal);
        Assert.Equal(0, await fixture.AdvisoryLockCountAsync(true, token));
        Assert.Equal(before, await fixture.BusinessSnapshotAsync(token));
    }

    private static SoulCurrentSigningProtocolV1 ChangeQueryCommitment(SoulCurrentSigningProtocolV1 request, SoulCurrentQueryV1 query) =>
        request with { QueryBase64 = Convert.ToBase64String(query.ToUtf8Json()), QuerySha256 = query.ComputeSha256() };

    private static async Task<byte[]> RawExchangeAsync(string root, byte[] body, CancellationToken token, uint? announcedLength = null)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(3));
        using var socket = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.Unix, System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Unspecified);
        await socket.ConnectAsync(new System.Net.Sockets.UnixDomainSocketEndPoint(Path.Combine(root, "run/current/service.sock")), timeout.Token);
        using var stream = new System.Net.Sockets.NetworkStream(socket, false);
        var header = new byte[4]; System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(header, announcedLength ?? checked((uint)body.Length));
        await stream.WriteAsync(header, timeout.Token); await stream.WriteAsync(body, timeout.Token);
        socket.Shutdown(System.Net.Sockets.SocketShutdown.Send);
        var output = new MemoryStream(); await stream.CopyToAsync(output, timeout.Token);
        var bytes = output.ToArray();
        if (bytes.Length == 0) return [];
        Assert.True(bytes.Length >= 4);
        Assert.Equal(bytes.Length - 4, checked((int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes)));
        return bytes[4..];
    }
}
