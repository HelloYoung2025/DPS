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
        var proof = await PossessionAsync(installation, fixture.Query, instance, session, deadline);
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
        var secondProof = await PossessionAsync(installation, fixture.Query, secondInstance, secondSession, deadline);
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
        var proof = await PossessionAsync(installation, fixture.Query, Guid.NewGuid(), Guid.NewGuid(), deadline);
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
        var relative = fault is "disabled" or "root-key" ? "bootstrap.v1.json" : fault == "configuration" ? "configuration.v2.json" : "composition.v1.json";
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

    [Theory]
    [Trait("Category", "Integration")]
    [InlineData("unlock-false")]
    [InlineData("still-held")]
    public async Task RealOwnerConnectionMustConfirmExactLockReleaseBeforeNormalAcknowledgement(string fault)
    {
        if (!OperatingSystem.IsMacOS()) throw new PlatformNotSupportedException();
        var token = TestContext.Current.CancellationToken;
        await using var fixture = await SoulCurrentProcessFixture.CreateAsync(token);
        var before = await fixture.BusinessSnapshotAsync(token);
        var installation = SoulCurrentInstallation.Load(new SoulInstallationLocation(Path.Combine(fixture.Root, "installation"), Path.Combine(fixture.Root, "run"), SoulCurrentNative.geteuid(), fixture.Root));
        await using var lease = await SoulCurrentOwnerLease.AcquireAsync(installation.ReadDatabaseConnection(false), fixture.Schema, fixture.Query.TenantRef, fixture.Query.SoulId, token);
        // Test-only fault injection into the already acquired real backend, never a substitute lease/decision path.
        var connection = (Npgsql.NpgsqlConnection)typeof(SoulCurrentOwnerLease).GetField("_connection", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(lease)!;
        var sql = fault == "unlock-false" ? "SELECT pg_advisory_unlock(hashtextextended(@key, 0))" : "SELECT pg_advisory_lock(hashtextextended(@key, 0))";
        await using (var command = new Npgsql.NpgsqlCommand(sql, connection))
        { command.Parameters.AddWithValue("key", $"soul-current:{fixture.Query.TenantRef}:{fixture.Query.SoulId}"); await command.ExecuteNonQueryAsync(token); }
        await Assert.ThrowsAsync<SoulRegistryException>(() => lease.ReleaseConfirmedAsync(token));
        await lease.DisposeAsync();
        Assert.Equal(0, await fixture.AdvisoryLockCountAsync(true, token));
        Assert.Equal(before, await fixture.BusinessSnapshotAsync(token));
    }

    [Theory]
    [Trait("Category", "Integration")]
    [InlineData("unlock")]
    [InlineData("readback")]
    public async Task RealOwnerConnectionLostReleaseResultCannotConfirmRelease(string lost)
    {
        if (!OperatingSystem.IsMacOS()) throw new PlatformNotSupportedException();
        var token = TestContext.Current.CancellationToken;
        await using var fixture = await SoulCurrentProcessFixture.CreateAsync(token);
        var before = await fixture.BusinessSnapshotAsync(token);
        var installation = SoulCurrentInstallation.Load(new SoulInstallationLocation(Path.Combine(fixture.Root, "installation"), Path.Combine(fixture.Root, "run"), SoulCurrentNative.geteuid(), fixture.Root));
        await using var bridge = new ReleaseResultDroppingBridge(checked((int)fixture.Configuration.DatabasePort), lost);
        // Only the endpoint changes (plaintext, so the bridge sees statement boundaries): the real provider login and real backend execute the release statements.
        var connection = new Npgsql.NpgsqlConnectionStringBuilder(installation.ReadDatabaseConnection(false)) { Port = bridge.Port, SslMode = Npgsql.SslMode.Disable }.ConnectionString;
        await using var lease = await SoulCurrentOwnerLease.AcquireAsync(connection, fixture.Schema, fixture.Query.TenantRef, fixture.Query.SoulId, token);
        await Assert.ThrowsAnyAsync<Npgsql.NpgsqlException>(() => lease.ReleaseConfirmedAsync(token));
        Assert.True(bridge.DroppedCompletedResult);
        await lease.DisposeAsync();
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(token); bounded.CancelAfter(5000);
        while (await fixture.AdvisoryLockCountAsync(true, bounded.Token) != 0) await Task.Delay(10, bounded.Token);
        Assert.Equal(before, await fixture.BusinessSnapshotAsync(token));
    }

    [Theory]
    [Trait("Category", "Integration")]
    [InlineData("EOF")]
    [InlineData("CONCURRENT")]
    [InlineData("AFTER_DISPOSE")]
    public async Task SessionTerminalStateAndDisconnectCannotResumeOrLeakARealLease(string fault)
    {
        if (!OperatingSystem.IsMacOS()) throw new PlatformNotSupportedException();
        var token = TestContext.Current.CancellationToken;
        await using var fixture = await SoulCurrentProcessFixture.CreateAsync(token);
        using var host = await fixture.AcquireLeaseProcessAsync(token);
        var before = await fixture.BusinessSnapshotAsync(token);
        if (fault == "EOF") host.Kill();
        else { await host.StandardInput.WriteLineAsync(fault); await host.StandardInput.FlushAsync(token); }
        await host.WaitForExitAsync(token); Assert.NotEqual(0, host.ExitCode);
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(token); bounded.CancelAfter(6000);
        while (await fixture.AdvisoryLockCountAsync(true, bounded.Token) != 0) await Task.Delay(10, bounded.Token);
        Assert.Equal(before, await fixture.BusinessSnapshotAsync(token));
    }

    [Theory]
    [Trait("Category", "Integration")]
    [InlineData("lock", "EOF")]
    [InlineData("lock", "PIPELINE")]
    [InlineData("sign", "EOF")]
    [InlineData("sign", "PIPELINE")]
    [InlineData("release", "EOF")]
    [InlineData("release", "PIPELINE")]
    public async Task RealProviderRejectsHostEofOrPipelinedBytesWhileLockSignOrReleaseIsInFlight(string window, string stimulus)
    {
        if (!OperatingSystem.IsMacOS()) throw new PlatformNotSupportedException();
        var token = TestContext.Current.CancellationToken;
        await using var fixture = await SoulCurrentProcessFixture.CreateAsync(token);
        var before = await fixture.BusinessSnapshotAsync(token);
        var installation = SoulCurrentInstallation.Load(new SoulInstallationLocation(Path.Combine(fixture.Root, "installation"), Path.Combine(fixture.Root, "run"), SoulCurrentNative.geteuid(), fixture.Root));
        var admin = new Npgsql.NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("DPS_TEST_POSTGRES") ?? throw new InvalidOperationException("DPS_TEST_POSTGRES required."))
        { Database = fixture.Configuration.DatabaseName, Pooling = false }.ConnectionString;
        await using var holder = new Npgsql.NpgsqlConnection(admin);
        var stopped = 0;
        try
        {
            using var deadline = new SoulCurrentDeadline(token);
            using var socket = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.Unix, System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Unspecified);
            await socket.ConnectAsync(new System.Net.Sockets.UnixDomainSocketEndPoint(Path.Combine(fixture.Root, "run/provider/service.sock")), deadline.Token);
            SoulCurrentNative.RequirePeer((int)socket.Handle, (uint)fixture.Configuration.ProviderUid, (uint)fixture.Configuration.ProviderGid);
            using var stream = new System.Net.Sockets.NetworkStream(socket, false);
            var begin = new SoulCurrentLeaseSessionV1
            {
                ProducerModule = "control-plane-host", Phase = "acquire/begin", Query = fixture.Query, RequestId = Guid.NewGuid(), Nonce = SoulCurrentSigningClient.Nonce(),
                InstanceId = Guid.NewGuid(), SessionId = "", Sequence = 1, ConfigurationSha256 = installation.ConfigurationSha256,
                ReleaseBomSha256 = installation.Composition.ReleaseBomSha256, ReleaseGeneration = installation.Composition.ReleaseGeneration, ExpiresAt = deadline.ExpiresAt
            };
            await SoulCurrentLeaseSessionV1.WriteAsync(stream, begin, deadline.Token);
            var challenge = await SoulCurrentLeaseSessionV1.ReadAsync(stream, deadline.Token); begin.RequireResponse(challenge, "acquire/challenge", true);
            var proof = await PossessionAsync(installation, fixture.Query, challenge.InstanceId, Guid.Parse(challenge.SessionId), deadline);
            var complete = challenge with { Phase = "acquire/complete", ProducerModule = "control-plane-host", Sequence = 3, SigningBase64 = Convert.ToBase64String(proof.ToUtf8Json()) };
            // Pipelined bytes are a well-formed next request written without waiting for the response.
            var next = complete with { Phase = "revalidate/request", Sequence = 5, RequestId = Guid.NewGuid(), Nonce = SoulCurrentSigningClient.Nonce(), SigningBase64 = "" };
            if (window == "lock")
            {
                await holder.OpenAsync(token);
                await using var take = new Npgsql.NpgsqlCommand("SELECT pg_advisory_lock(hashtextextended(@key, 0))", holder);
                take.Parameters.AddWithValue("key", $"soul-current:{fixture.Query.TenantRef}:{fixture.Query.SoulId}"); await take.ExecuteNonQueryAsync(token);
            }
            // Test-only stall of this unit's own current-signer child or PG backend; always resumed.
            if (window == "sign") { stopped = fixture.SignerProcessIds[1]; Signal(stopped, SigStop); }
            await SoulCurrentLeaseSessionV1.WriteAsync(stream, complete, deadline.Token);
            if (window == "release")
            {
                var acquired = await SoulCurrentLeaseSessionV1.ReadAsync(stream, deadline.Token); complete.RequireResponse(acquired, "acquire/acquired");
                stopped = await ProviderLeaseBackendAsync(admin, fixture.Configuration.DatabaseRuntimeRole, false, token);
                Assert.NotEqual(0, stopped); Signal(stopped, SigStop);
                var dispose = acquired with { Phase = "dispose/request", ProducerModule = "control-plane-host", Sequence = 5, RequestId = Guid.NewGuid(), Nonce = SoulCurrentSigningClient.Nonce(), StatusBase64 = "", ResolutionBase64 = "" };
                await SoulCurrentLeaseSessionV1.WriteAsync(stream, dispose, deadline.Token);
                next = dispose with { Phase = "revalidate/request", Sequence = 7, RequestId = Guid.NewGuid(), Nonce = SoulCurrentSigningClient.Nonce() };
                // In flight by construction only: release waits on the stopped backend; nothing observable exists without a fixture change.
                await Task.Delay(300, token);
            }
            else
            {
                // Observed in flight: provider waits for the held lock, or its lease backend is idle after the alias read (next step: signer RPC).
                using var observed = CancellationTokenSource.CreateLinkedTokenSource(token); observed.CancelAfter(3000);
                while (window == "lock" ? await fixture.AdvisoryLockCountAsync(false, observed.Token) == 0
                    : await ProviderLeaseBackendAsync(admin, fixture.Configuration.DatabaseRuntimeRole, true, observed.Token) == 0) await Task.Delay(10, observed.Token);
            }
            if (stimulus == "EOF") socket.Shutdown(System.Net.Sockets.SocketShutdown.Send);
            else await SoulCurrentLeaseSessionV1.WriteAsync(stream, next, deadline.Token);
            using var read = CancellationTokenSource.CreateLinkedTokenSource(token); read.CancelAfter(8000);
            var outcome = SoulCurrentLeaseSessionV1.ReadAsync(stream, read.Token);
            await Task.WhenAny(outcome, Task.Delay(window == "release" ? 700 : 1000, token));
            // Let the stalled operation complete; only an already aborted session keeps it from becoming a result.
            if (window == "lock") await holder.CloseAsync();
            else { Signal(stopped, SigContinue); stopped = 0; }
            try { Assert.Fail("Provider returned " + (await outcome).Phase + " after " + stimulus + " during in-flight " + window + "."); }
            catch (EndOfStreamException) { }
        }
        finally { if (stopped != 0) _ = kill(stopped, SigContinue); }
        using var cleanup = CancellationTokenSource.CreateLinkedTokenSource(token); cleanup.CancelAfter(6000);
        while (await fixture.AdvisoryLockCountAsync(true, cleanup.Token) != 0) await Task.Delay(10, cleanup.Token);
        Assert.Equal(before, await fixture.BusinessSnapshotAsync(token));
    }

    [Theory]
    [Trait("Category", "Integration")]
    [InlineData("sequence")]
    [InlineData("duplicate")]
    [InlineData("session")]
    [InlineData("instance")]
    [InlineData("query")]
    [InlineData("configuration")]
    [InlineData("bom")]
    [InlineData("generation")]
    [InlineData("deadline")]
    [InlineData("connection")]
    public async Task RealProviderRejectsSessionReplayTransferAndBindingChanges(string fault)
    {
        if (!OperatingSystem.IsMacOS()) throw new PlatformNotSupportedException();
        var token = TestContext.Current.CancellationToken;
        await using var fixture = await SoulCurrentProcessFixture.CreateAsync(token);
        var before = await fixture.BusinessSnapshotAsync(token);
        var installation = SoulCurrentInstallation.Load(new SoulInstallationLocation(Path.Combine(fixture.Root, "installation"), Path.Combine(fixture.Root, "run"), SoulCurrentNative.geteuid(), fixture.Root));
        using var deadline = new SoulCurrentDeadline(token);
        using var socket = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.Unix, System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Unspecified);
        await socket.ConnectAsync(new System.Net.Sockets.UnixDomainSocketEndPoint(Path.Combine(fixture.Root, "run/provider/service.sock")), deadline.Token);
        SoulCurrentNative.RequirePeer((int)socket.Handle, (uint)fixture.Configuration.ProviderUid, (uint)fixture.Configuration.ProviderGid);
        using var stream = new System.Net.Sockets.NetworkStream(socket, false);
        var begin = new SoulCurrentLeaseSessionV1
        {
            ProducerModule = "control-plane-host", Phase = "acquire/begin", Query = fixture.Query, RequestId = Guid.NewGuid(), Nonce = SoulCurrentSigningClient.Nonce(),
            InstanceId = Guid.NewGuid(), SessionId = "", Sequence = 1, ConfigurationSha256 = installation.ConfigurationSha256,
            ReleaseBomSha256 = installation.Composition.ReleaseBomSha256, ReleaseGeneration = installation.Composition.ReleaseGeneration, ExpiresAt = deadline.ExpiresAt
        };
        await SoulCurrentLeaseSessionV1.WriteAsync(stream, begin, deadline.Token);
        var challenge = await SoulCurrentLeaseSessionV1.ReadAsync(stream, deadline.Token); begin.RequireResponse(challenge, "acquire/challenge", true);
        var proof = await PossessionAsync(installation, fixture.Query, challenge.InstanceId, Guid.Parse(challenge.SessionId), deadline);
        var complete = challenge with { Phase = "acquire/complete", ProducerModule = "control-plane-host", Sequence = 3, SigningBase64 = Convert.ToBase64String(proof.ToUtf8Json()) };
        await SoulCurrentLeaseSessionV1.WriteAsync(stream, complete, deadline.Token);
        var acquired = await SoulCurrentLeaseSessionV1.ReadAsync(stream, deadline.Token); complete.RequireResponse(acquired, "acquire/acquired");
        var request = acquired with { Phase = "revalidate/request", ProducerModule = "control-plane-host", Sequence = 5, RequestId = Guid.NewGuid(), Nonce = SoulCurrentSigningClient.Nonce(), StatusBase64 = "", ResolutionBase64 = "" };
        request = fault switch
        {
            "sequence" => request with { Sequence = 7 },
            "duplicate" => request with { RequestId = begin.RequestId, Nonce = begin.Nonce },
            "session" => request with { SessionId = Guid.NewGuid().ToString("D") },
            "instance" => request with { InstanceId = Guid.NewGuid() },
            "query" => request with { Query = fixture.Query with { TraceId = "trace_" + new string('e', 32) } },
            "configuration" => request with { ConfigurationSha256 = new string('e', 64) },
            "bom" => request with { ReleaseBomSha256 = new string('e', 64) },
            "generation" => request with { ReleaseGeneration = 2 },
            "deadline" => request with { ExpiresAt = request.ExpiresAt.AddMilliseconds(1) },
            _ => request
        };
        using var other = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.Unix, System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Unspecified);
        if (fault == "connection") await other.ConnectAsync(new System.Net.Sockets.UnixDomainSocketEndPoint(Path.Combine(fixture.Root, "run/provider/service.sock")), deadline.Token);
        using var destination = fault == "connection" ? new System.Net.Sockets.NetworkStream(other, false) : null;
        var target = destination ?? stream;
        await SoulCurrentLeaseSessionV1.WriteAsync(target, request, deadline.Token);
        await Assert.ThrowsAsync<EndOfStreamException>(() => SoulCurrentLeaseSessionV1.ReadAsync(target, deadline.Token));
        socket.Dispose();
        using var cleanup = CancellationTokenSource.CreateLinkedTokenSource(token); cleanup.CancelAfter(5000);
        while (await fixture.AdvisoryLockCountAsync(true, cleanup.Token) != 0) await Task.Delay(10, cleanup.Token);
        Assert.Equal(before, await fixture.BusinessSnapshotAsync(token));
    }

    [Theory]
    [Trait("Category", "Integration")]
    [InlineData("provider")]
    [InlineData("host-auth")]
    [InlineData("current")]
    public async Task EachSoulRoleRejectsItsActuallyLoadedArtifactChangedAfterStartup(string role)
    {
        if (!OperatingSystem.IsMacOS()) throw new PlatformNotSupportedException();
        var token = TestContext.Current.CancellationToken;
        await using var fixture = await SoulCurrentProcessFixture.CreateAsync(token);
        Assert.Equal(0, (await fixture.ReadHostAsync(null, token)).Exit);
        var before = await fixture.BusinessSnapshotAsync(token);
        var installation = SoulCurrentInstallation.Load(new SoulInstallationLocation(Path.Combine(fixture.Root, "installation"), Path.Combine(fixture.Root, "run"), SoulCurrentNative.geteuid(), fixture.Root));
        using var deadline = new SoulCurrentDeadline(token);
        var query = fixture.Query;
        var instance = Guid.NewGuid(); var session = Guid.NewGuid();
        var proof = role == "current" ? await PossessionAsync(installation, query, instance, session, deadline) : null;
        File.SetLastWriteTimeUtc(Path.Combine(fixture.Root, "installation/releases/1/Dps.SoulRegistry.dll"), DateTime.UtcNow);
        if (role == "provider") Assert.Equal(1, (await fixture.ReadHostAsync(null, token)).Exit);
        else if (role == "host-auth")
            await Assert.ThrowsAsync<ArgumentException>(() => PossessionAsync(installation, query, instance, session, deadline));
        else await Assert.ThrowsAsync<SoulRegistryException>(() => new SoulCurrentSigningClient(installation).StatusAsync(query, instance, session, proof!, deadline));
        Assert.Equal(before, await fixture.BusinessSnapshotAsync(token));
    }

    // Same-UID synthetic adversary: obtains a real Host proof over the public envelope, then attacks the internal signer.
    private static async Task<SoulCurrentSigningProtocolV1> PossessionAsync(SoulCurrentInstallation installation,
        SoulCurrentQueryV1 query, Guid instance, Guid session, SoulCurrentDeadline deadline)
    {
        var request = new SoulCurrentSigningProtocolV1
        {
            Operation = "host-possession", MessageType = "challenge-request", RequestId = Guid.NewGuid(), InstanceId = instance, SessionId = session,
            QueryBase64 = Convert.ToBase64String(query.ToUtf8Json()), QuerySha256 = query.ComputeSha256(), ConfigurationSha256 = installation.ConfigurationSha256,
            ReleaseBomSha256 = installation.Composition.ReleaseBomSha256, ReleaseGeneration = installation.Composition.ReleaseGeneration, ExpiresAt = deadline.ExpiresAt,
            ChallengeNonce = "", PossessionBase64 = "", StatusBase64 = "", ResolutionBase64 = "", ErrorCode = "", SignatureBase64 = ""
        };
        var envelope = new SoulCurrentLeaseSessionV1
        {
            ProducerModule = "control-plane-host", Phase = "acquire/possession-request", Query = query, RequestId = request.RequestId,
            Nonce = SoulCurrentSigningClient.Nonce(), InstanceId = instance, SessionId = session.ToString("D"), Sequence = 1,
            ConfigurationSha256 = request.ConfigurationSha256, ReleaseBomSha256 = request.ReleaseBomSha256, ReleaseGeneration = request.ReleaseGeneration,
            ExpiresAt = request.ExpiresAt, SigningBase64 = Convert.ToBase64String(request.ToUtf8Json())
        };
        using var socket = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.Unix, System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Unspecified);
        await socket.ConnectAsync(new System.Net.Sockets.UnixDomainSocketEndPoint(Path.Combine(installation.Location.SocketRoot, "host-auth/service.sock")), deadline.Token);
        SoulCurrentNative.RequirePeer((int)socket.Handle, (uint)installation.Configuration.HostAuthUid, (uint)installation.Configuration.HostAuthGid);
        using var stream = new System.Net.Sockets.NetworkStream(socket, false);
        await SoulCurrentLeaseSessionV1.WriteAsync(stream, envelope, deadline.Token);
        var challenge = await SoulCurrentLeaseSessionV1.ReadAsync(stream, deadline.Token); envelope.RequireResponse(challenge, "acquire/possession-challenge");
        var inner = SoulCurrentSigningProtocolV1.FromUtf8Json(Convert.FromBase64String(challenge.SigningBase64));
        var answer = challenge with { Phase = "acquire/possession-answer", ProducerModule = "control-plane-host", Sequence = 3,
            SigningBase64 = Convert.ToBase64String((inner with { MessageType = "answer" }).ToUtf8Json()) };
        await SoulCurrentLeaseSessionV1.WriteAsync(stream, answer, deadline.Token);
        var response = await SoulCurrentLeaseSessionV1.ReadAsync(stream, deadline.Token); answer.RequireResponse(response, "acquire/possession-proof");
        var proof = SoulCurrentSigningProtocolV1.FromUtf8Json(Convert.FromBase64String(response.SigningBase64));
        SoulCurrentInstallation.Verify(installation.Configuration.HostAuthSpkiBase64, proof.CanonicalBytes(), proof.SignatureBase64); return proof;
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

    // Darwin signal numbers. Only this fixture's own current-signer child or its disposable-PG provider backend is signalled.
    private const int SigStop = 17, SigContinue = 19;
    private static void Signal(int pid, int signal) => Assert.Equal(0, kill(pid, signal));
    [System.Runtime.InteropServices.DllImport("/usr/lib/libSystem.B.dylib", SetLastError = true)] private static extern int kill(int pid, int sig);

    // The provider login's backend holding the Soul advisory lock; afterAlias also requires it idle after the alias read.
    private static async Task<int> ProviderLeaseBackendAsync(string admin, string role, bool afterAlias, CancellationToken token)
    {
        await using var connection = new Npgsql.NpgsqlConnection(admin); await connection.OpenAsync(token);
        await using var command = new Npgsql.NpgsqlCommand("""
            SELECT a.pid FROM pg_catalog.pg_stat_activity a JOIN pg_catalog.pg_locks l ON l.pid = a.pid
            WHERE a.datname = current_database() AND a.usename = @role AND l.locktype = 'advisory' AND l.granted
              AND (NOT @after OR (a.state = 'idle' AND a.query LIKE '%identity_authority_aliases%'))
            """, connection);
        command.Parameters.AddWithValue("role", role); command.Parameters.AddWithValue("after", afterAlias);
        return await command.ExecuteScalarAsync(token) is int pid ? pid : 0;
    }

    // Test-owned loopback bridge to this unit's PostgreSQL only: forwards the real release statement to the server,
    // then withholds its completed result from Npgsql and closes the client side, so the result is lost on the wire.
    private sealed class ReleaseResultDroppingBridge : IAsyncDisposable
    {
        private readonly int _postgresPort;
        private readonly string _lost;
        private readonly System.Net.Sockets.TcpListener _listener = new(System.Net.IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stop = new();
        private readonly System.Collections.Concurrent.ConcurrentBag<Task> _bridges = [];
        private readonly Task _accept;
        private int _dropped;
        internal int Port => ((System.Net.IPEndPoint)_listener.LocalEndpoint).Port;
        internal bool DroppedCompletedResult => Volatile.Read(ref _dropped) == 1;

        internal ReleaseResultDroppingBridge(int postgresPort, string lost)
        {
            _postgresPort = postgresPort; _lost = lost;
            _listener.Start();
            _accept = AcceptAsync();
        }

        private async Task AcceptAsync()
        {
            try
            {
                while (!_stop.IsCancellationRequested)
                    _bridges.Add(BridgeAsync(await _listener.AcceptTcpClientAsync(_stop.Token)));
            }
            catch (OperationCanceledException) { }
            catch (System.Net.Sockets.SocketException) when (_stop.IsCancellationRequested) { }
        }

        private async Task BridgeAsync(System.Net.Sockets.TcpClient client)
        {
            using (client)
            using (var backend = new System.Net.Sockets.TcpClient())
            {
                try
                {
                    await backend.ConnectAsync(System.Net.IPAddress.Loopback, _postgresPort, _stop.Token);
                    var clientStream = client.GetStream(); var serverStream = backend.GetStream();
                    var withhold = 0;
                    async Task Outbound()
                    {
                        var buffer = new byte[8192]; var tail = ""; var sawUnlock = false;
                        while (true)
                        {
                            var count = await clientStream.ReadAsync(buffer, _stop.Token);
                            if (count == 0) return;
                            var text = tail + System.Text.Encoding.ASCII.GetString(buffer, 0, count);
                            // Armed before the statement reaches the server, so no byte of its result can pass.
                            if (_lost == "unlock" ? text.Contains("pg_advisory_unlock(", StringComparison.Ordinal)
                                : sawUnlock && text.Contains("pg_locks", StringComparison.Ordinal)) Volatile.Write(ref withhold, 1);
                            sawUnlock |= text.Contains("pg_advisory_unlock(", StringComparison.Ordinal);
                            tail = text.Length > 32 ? text[^32..] : text;
                            await serverStream.WriteAsync(buffer.AsMemory(0, count), _stop.Token);
                        }
                    }
                    async Task Inbound()
                    {
                        var buffer = new byte[8192]; var tail = "";
                        while (true)
                        {
                            var count = await serverStream.ReadAsync(buffer, _stop.Token);
                            if (count == 0) return;
                            if (Volatile.Read(ref withhold) == 0) { await clientStream.WriteAsync(buffer.AsMemory(0, count), _stop.Token); continue; }
                            var text = tail + System.Text.Encoding.ASCII.GetString(buffer, 0, count);
                            // The server completed the statement (CommandComplete "SELECT 1"); Npgsql never receives it.
                            if (text.Contains("SELECT 1\0", StringComparison.Ordinal)) { Volatile.Write(ref _dropped, 1); client.Close(); return; }
                            tail = text.Length > 32 ? text[^32..] : text;
                        }
                    }
                    var outbound = Outbound(); var inbound = Inbound();
                    await Task.WhenAny(outbound, inbound);
                    client.Close(); backend.Close();
                    try { await Task.WhenAll(outbound, inbound); }
                    catch (Exception) when (!_stop.IsCancellationRequested) { }
                }
                catch (Exception) when (!_stop.IsCancellationRequested) { }
            }
        }

        public async ValueTask DisposeAsync()
        {
            _stop.Cancel(); _listener.Stop();
            await _accept;
            try { await Task.WhenAll(_bridges); } catch (Exception) { }
            _stop.Dispose();
        }
    }
}
