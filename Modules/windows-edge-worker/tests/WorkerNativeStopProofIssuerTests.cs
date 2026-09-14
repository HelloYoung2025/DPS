using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text.Json;
using Dps.ExecutorGateway.Contracts;
using Xunit;

namespace Dps.WindowsEdgeWorker.Tests;

public sealed class WorkerNativeStopProofIssuerTests
{
    private static readonly DateTimeOffset OccurredAt =
        new(2026, 7, 15, 10, 0, 2, TimeSpan.Zero);

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Legacy_v1_issuer_is_a_zero_authority_quarantine_tombstone()
    {
        using var runtime = new DrainReceiptTemporaryRuntimeDirectory();
        await using var store = DurableNativeStopProofStore.Open(runtime.Path);
        var issuer = new WorkerNativeStopProofIssuer();

        var error = await Assert.ThrowsAsync<NotSupportedException>(() =>
            issuer.IssueAsync(TestContext.Current.CancellationToken));

        Assert.Contains("quarantine-only", error.Message, StringComparison.Ordinal);
        Assert.Null(store.InspectExisting(Request().SubmissionAttemptId));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Quarantine_store_exposes_metadata_only_and_has_no_issuance_API()
    {
        Assert.False(typeof(WorkerNativeStopProofIssuer).IsPublic);
        Assert.False(typeof(DurableNativeStopProofStore).IsPublic);
        var assembly = typeof(WorkerNativeStopProofIssuer).Assembly;
        foreach (var forbiddenType in new[]
        {
            "WorkerNativeStopRequest",
            "WorkerRuntimeIdentitySnapshot",
            "IWorkerRuntimeIdentityProvider",
            "IWorkerNativeNoLaterWriteController",
            "IWorkerNativeStopProofSigningAuthority",
            "WorkerNativeStopProofIssuanceResult"
        })
        {
            Assert.DoesNotContain(
                assembly.GetTypes(),
                type => type.Name == forbiddenType);
        }
        var constructor = Assert.Single(typeof(WorkerNativeStopProofIssuer).GetConstructors(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
        Assert.Empty(constructor.GetParameters());
        var issue = typeof(WorkerNativeStopProofIssuer).GetMethod(
            "IssueAsync",
            BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(issue);
        Assert.Equal(typeof(Task), issue.ReturnType);
        var issueParameter = Assert.Single(issue.GetParameters());
        Assert.Equal(typeof(CancellationToken), issueParameter.ParameterType);
        Assert.DoesNotContain(
            typeof(LegacyNativeStopProofV1Observation).GetProperties(),
            property => property.PropertyType == typeof(byte[]) ||
                property.Name.Contains("WireUtf8", StringComparison.Ordinal));
        Assert.DoesNotContain(
            typeof(DurableNativeStopProofStore).GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
            method => method.Name is "Prepare" or "AcquireIssuanceLeaseAsync");
        var internalReader = typeof(DurableNativeStopProofStore).GetMethod(
            "ReadInternal",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(internalReader);
        Assert.DoesNotContain(
            internalReader.ReturnType.GetProperties(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
            property => property.PropertyType == typeof(byte[]) ||
                property.Name.Contains("WireUtf8", StringComparison.Ordinal));

        using var runtime = new DrainReceiptTemporaryRuntimeDirectory();
        using var store = DurableNativeStopProofStore.Open(runtime.Path);
        Assert.Throws<ArgumentException>(() => store.InspectExisting(Guid.Empty));
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void Legacy_v1_wire_is_owner_decoded_but_only_quarantine_metadata_is_returned()
    {
        using var runtime = new DrainReceiptTemporaryRuntimeDirectory();
        var request = Request();
        var expected = WriteLegacyRecord(runtime.Path, request);

        using var store = DurableNativeStopProofStore.Open(runtime.Path);
        var observed = store.InspectExisting(request.SubmissionAttemptId);

        Assert.NotNull(observed);
        Assert.Equal(request.SubmissionAttemptId, observed.SubmissionAttemptId);
        Assert.Equal(expected.InputFingerprintSha256, observed.InputFingerprintSha256);
        Assert.Equal(expected.WireSha256, observed.WireSha256);
        Assert.Equal(expected.WireBytes, observed.ExactWireBytes);
        Assert.Equal("QUARANTINE_ONLY", observed.Disposition);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void Manifest_declares_legacy_v1_quarantine_only_with_no_runtime_edge()
    {
        var root = FindRepositoryRoot();
        using var document = JsonDocument.Parse(File.ReadAllBytes(
            Path.Combine(root, "Modules", "windows-edge-worker", "module.yaml")));
        var manifest = document.RootElement;
        var declarations = manifest.GetProperty("contracts").GetProperty("consumed")
            .EnumerateArray()
            .Where(item =>
                item.GetProperty("contractId").GetString() == "native.stop.proof" &&
                item.GetProperty("major").GetInt32() == 1)
            .ToArray();

        var declaration = Assert.Single(declarations);
        Assert.Equal("quarantine-only", declaration.GetProperty("mode").GetString());
        Assert.Equal("deprecated", declaration.GetProperty("status").GetString());
        foreach (var direction in new[] { "inbound", "outbound" })
        {
            Assert.DoesNotContain(
                manifest.GetProperty("communication").GetProperty(direction).EnumerateArray(),
                edge => edge.GetProperty("contractId").GetString() == "native.stop.proof");
        }
        Assert.DoesNotContain(
            manifest.GetProperty("permissions").GetProperty("allowed").EnumerateArray(),
            permission => permission.GetString() is
                "worker:native-stop-proof-sign" or "native:no-later-write-stop");
        Assert.Contains(
            manifest.GetProperty("security").GetProperty("failClosedOn").EnumerateArray(),
            item => item.GetString()?.Contains("Windows identity ACL", StringComparison.Ordinal) == true);
        Assert.True(
            manifest.GetProperty("deviceGates").GetProperty("requiresWindows").GetBoolean());
        Assert.False(manifest.GetProperty("module").GetProperty("releaseEligible").GetBoolean());
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void Unix_diagnostics_are_explicitly_non_counting_and_excluded_from_required_suites()
    {
        var diagnostics = typeof(WorkerNativeStopProofIssuerTests).Assembly.GetTypes()
            .SelectMany(type => type.GetMethods(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            .Where(method => MethodTraits(method).Contains(
                ("Category", "UnixDiagnostic")))
            .ToArray();

        Assert.Equal(6, diagnostics.Length);
        foreach (var method in diagnostics)
        {
            var traits = MethodTraits(method);
            Assert.Contains(("EvidenceKind", "UNIX_ONLY_DIAGNOSTIC"), traits);
            Assert.Contains(("CountsToward", "NONE"), traits);
            Assert.Contains(
                method.GetCustomAttributesData(),
                attribute =>
                    attribute.AttributeType == typeof(UnsupportedOSPlatformAttribute) &&
                    attribute.ConstructorArguments.Count == 1 &&
                    attribute.ConstructorArguments[0].Value as string == "windows");
            Assert.DoesNotContain(traits, trait =>
                trait.Item1 == "Category" &&
                trait.Item2 is "Unit" or "Contract" or "Integration");
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("EvidenceKind", "SIMULATION")]
    public async Task Repeated_legacy_requests_create_no_stop_call_signature_or_artifact()
    {
        using var runtime = new DrainReceiptTemporaryRuntimeDirectory();
        await using var store = DurableNativeStopProofStore.Open(runtime.Path);
        var issuer = new WorkerNativeStopProofIssuer();

        for (var index = 0; index < 20; index++)
        {
            await Assert.ThrowsAsync<NotSupportedException>(() =>
                issuer.IssueAsync(TestContext.Current.CancellationToken));
        }

        AssertZeroAuthority(runtime.Path);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("EvidenceKind", "SIMULATION")]
    public async Task Concurrent_legacy_requests_create_no_stop_call_signature_or_artifact()
    {
        using var runtime = new DrainReceiptTemporaryRuntimeDirectory();
        await using var store = DurableNativeStopProofStore.Open(runtime.Path);
        var issuer = new WorkerNativeStopProofIssuer();

        await Task.WhenAll(Enumerable.Range(0, 32).Select(async _ =>
        {
            await Assert.ThrowsAsync<NotSupportedException>(() =>
                issuer.IssueAsync(TestContext.Current.CancellationToken));
        }));

        AssertZeroAuthority(runtime.Path);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("EvidenceKind", "SIMULATION")]
    public async Task Cancellation_cannot_convert_quarantine_only_into_emission()
    {
        using var runtime = new DrainReceiptTemporaryRuntimeDirectory();
        await using var store = DurableNativeStopProofStore.Open(runtime.Path);
        var issuer = new WorkerNativeStopProofIssuer();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var error = await Assert.ThrowsAsync<NotSupportedException>(() =>
            issuer.IssueAsync(cancellation.Token));

        Assert.Contains("Policy-owned v2", error.Message, StringComparison.Ordinal);
        AssertZeroAuthority(runtime.Path);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("EvidenceKind", "SIMULATION")]
    public void Restart_repeats_the_same_bounded_quarantine_observation_without_wire_release()
    {
        using var runtime = new DrainReceiptTemporaryRuntimeDirectory();
        var request = Request();
        _ = WriteLegacyRecord(runtime.Path, request);
        LegacyNativeStopProofV1Observation first;
        using (var store = DurableNativeStopProofStore.Open(runtime.Path))
            first = store.InspectExisting(request.SubmissionAttemptId)!;
        using var recovered = DurableNativeStopProofStore.Open(runtime.Path);
        var second = recovered.InspectExisting(request.SubmissionAttemptId);

        Assert.Equal(first, second);
        Assert.Equal("QUARANTINE_ONLY", second!.Disposition);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("EvidenceKind", "SIMULATION")]
    public async Task Conflicting_legacy_inputs_cannot_create_or_mutate_quarantine_state()
    {
        using var runtime = new DrainReceiptTemporaryRuntimeDirectory();
        await using var store = DurableNativeStopProofStore.Open(runtime.Path);
        var request = Request();
        var issuer = new WorkerNativeStopProofIssuer();

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            issuer.IssueAsync(TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            issuer.IssueAsync(TestContext.Current.CancellationToken));

        AssertZeroAuthority(runtime.Path);
        Assert.Null(store.InspectExisting(request.SubmissionAttemptId));
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("EvidenceKind", "SIMULATION")]
    public void Quarantine_store_retains_cross_platform_writer_fence()
    {
        using var runtime = new DrainReceiptTemporaryRuntimeDirectory();
        using (var first = DurableNativeStopProofStore.Open(runtime.Path))
            Assert.Throws<IOException>(() => DurableNativeStopProofStore.Open(runtime.Path));
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("EvidenceKind", "SIMULATION")]
    public void Quarantine_store_rejects_or_blocks_live_writer_path_replacement()
    {
        using var runtime = new DrainReceiptTemporaryRuntimeDirectory();
        using var store = DurableNativeStopProofStore.Open(runtime.Path);
        var writerPath = Path.Combine(runtime.Path, "native-stop-proofs.writer.lock");
        var archivedPath = writerPath + ".replaced";
        var originalLength = checked((int)new FileInfo(writerPath).Length);

        var replacementError = Record.Exception(() =>
        {
            File.Move(writerPath, archivedPath);
            WritePrivate(writerPath, new byte[originalLength]);
        });

        if (replacementError is null)
        {
            Assert.Throws<IOException>(() =>
                store.InspectExisting(Request().SubmissionAttemptId));
        }
        else
        {
            Assert.True(
                replacementError is IOException or UnauthorizedAccessException,
                "expected the host to block replacement or the store to reject it, got " +
                replacementError.GetType().FullName);
            Assert.True(File.Exists(writerPath));
            Assert.Null(store.InspectExisting(Request().SubmissionAttemptId));
        }
    }

    [Fact]
    [Trait("Category", "UnixDiagnostic")]
    [Trait("EvidenceKind", "UNIX_ONLY_DIAGNOSTIC")]
    [Trait("CountsToward", "NONE")]
    [UnsupportedOSPlatform("windows")]
    public void Quarantine_store_enforces_unix_mode_and_effective_owner_without_claiming_Windows_ACL()
    {
        RequireUnixDiagnosticHost();
        using var runtime = new DrainReceiptTemporaryRuntimeDirectory();
        var request = Request();
        _ = WriteLegacyRecord(runtime.Path, request);
        var path = LegacyProofPath(runtime.Path, request.SubmissionAttemptId);

        using (var store = DurableNativeStopProofStore.Open(runtime.Path))
            Assert.NotNull(store.InspectExisting(request.SubmissionAttemptId));

        Assert.Equal(
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
            File.GetUnixFileMode(runtime.Path));
        Assert.Equal(
            UnixFileMode.UserRead | UnixFileMode.UserWrite,
            File.GetUnixFileMode(path));
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var identity = SecureRuntimeFileSystem.CaptureOpenFileIdentity(stream, path);
            Assert.NotNull(identity);
            Assert.Equal(NativeGetEffectiveUserId(), identity.Value.OwnerUid);

            var verifyOwner = typeof(SecureRuntimeFileSystem).GetMethod(
                "VerifyEffectiveUserOwns",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(verifyOwner);
            var wrongOwner = identity.Value with
            {
                OwnerUid = identity.Value.OwnerUid == uint.MaxValue
                    ? identity.Value.OwnerUid - 1
                    : identity.Value.OwnerUid + 1
            };
            var invocation = Assert.Throws<TargetInvocationException>(() =>
                verifyOwner.Invoke(null, new object[] { wrongOwner, "test quarantine file" }));
            Assert.IsType<UnauthorizedAccessException>(invocation.InnerException);
        }

        File.SetUnixFileMode(
            path,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead);
        Assert.Throws<UnauthorizedAccessException>(() =>
            DurableNativeStopProofStore.Open(runtime.Path));
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("EvidenceKind", "SIMULATION")]
    public void Malformed_legacy_wire_and_existing_conflict_marker_fail_closed_after_restart()
    {
        var request = Request();
        using (var malformedRuntime = new DrainReceiptTemporaryRuntimeDirectory())
        {
            var malformedPath = LegacyProofPath(malformedRuntime.Path, request.SubmissionAttemptId);
            WritePrivate(malformedPath, "{}"u8.ToArray());
            using var store = DurableNativeStopProofStore.Open(malformedRuntime.Path);
            Assert.ThrowsAny<Exception>(() => store.InspectExisting(request.SubmissionAttemptId));
        }

        using var quarantinedRuntime = new DrainReceiptTemporaryRuntimeDirectory();
        var markerPath = LegacyProofPath(quarantinedRuntime.Path, request.SubmissionAttemptId) +
            ".quarantine";
        WritePrivate(markerPath, "{}"u8.ToArray());
        using var quarantined = DurableNativeStopProofStore.Open(quarantinedRuntime.Path);
        Assert.Throws<NativeStopProofConflictException>(() =>
            quarantined.InspectExisting(request.SubmissionAttemptId));
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("EvidenceKind", "SIMULATION")]
    public void Oversized_record_wire_and_non_utf8_record_fail_before_metadata_release()
    {
        var request = Request();
        using (var oversizedRecordRuntime = new DrainReceiptTemporaryRuntimeDirectory())
        {
            WritePrivate(
                LegacyProofPath(oversizedRecordRuntime.Path, request.SubmissionAttemptId),
                new byte[128 * 1024 + 1]);
            using var store = DurableNativeStopProofStore.Open(oversizedRecordRuntime.Path);
            Assert.Throws<InvalidDataException>(() =>
                store.InspectExisting(request.SubmissionAttemptId));
        }

        using (var oversizedWireRuntime = new DrainReceiptTemporaryRuntimeDirectory())
        {
            var oversizedWire = new byte[64 * 1024 + 1];
            var record = JsonSerializer.SerializeToUtf8Bytes(new
            {
                schema_version = "1.0",
                submission_attempt_id = request.SubmissionAttemptId.ToString("D"),
                input_fingerprint_sha256 = new string('f', 64),
                wire_base64 = Convert.ToBase64String(oversizedWire),
                wire_sha256 = Sha256(oversizedWire)
            });
            WritePrivate(
                LegacyProofPath(oversizedWireRuntime.Path, request.SubmissionAttemptId),
                record);
            using var store = DurableNativeStopProofStore.Open(oversizedWireRuntime.Path);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                store.InspectExisting(request.SubmissionAttemptId));
        }

        using var binaryRuntime = new DrainReceiptTemporaryRuntimeDirectory();
        WritePrivate(
            LegacyProofPath(binaryRuntime.Path, request.SubmissionAttemptId),
            new byte[] { 0xff, 0xfe, 0xfd });
        using var binaryStore = DurableNativeStopProofStore.Open(binaryRuntime.Path);
        Assert.Throws<InvalidDataException>(() =>
            binaryStore.InspectExisting(request.SubmissionAttemptId));

        using (var decodedBinaryRuntime = new DrainReceiptTemporaryRuntimeDirectory())
        {
            var decodedBinary = new byte[] { 0xff, 0xfe, 0xfd };
            WriteLegacyEnvelope(
                decodedBinaryRuntime.Path,
                request,
                Convert.ToBase64String(decodedBinary),
                Sha256(decodedBinary));
            using var store = DurableNativeStopProofStore.Open(decodedBinaryRuntime.Path);
            Assert.Throws<InvalidDataException>(() =>
                store.InspectExisting(request.SubmissionAttemptId));
        }

        using var noncanonicalRuntime = new DrainReceiptTemporaryRuntimeDirectory();
        var canonicalWire = CreateLegacyOwnerWire(request);
        try
        {
            WriteLegacyEnvelope(
                noncanonicalRuntime.Path,
                request,
                Convert.ToBase64String(canonicalWire) + "\n",
                Sha256(canonicalWire));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(canonicalWire);
        }
        using var noncanonicalStore = DurableNativeStopProofStore.Open(noncanonicalRuntime.Path);
        Assert.Throws<InvalidDataException>(() =>
            noncanonicalStore.InspectExisting(request.SubmissionAttemptId));
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("EvidenceKind", "SIMULATION")]
    public void Quarantine_store_rechecks_artifact_budget_after_open_on_every_platform()
    {
        var request = Request();

        using var budgetRuntime = new DrainReceiptTemporaryRuntimeDirectory();
        using var budgetStore = DurableNativeStopProofStore.Open(budgetRuntime.Path);
        for (var index = 0; index <= 8192; index++)
        {
            WritePrivate(
                Path.Combine(budgetRuntime.Path, $"native-stop-proof-budget-{index:D5}"),
                "x"u8.ToArray());
        }
        Assert.Throws<IOException>(() =>
            budgetStore.InspectExisting(request.SubmissionAttemptId));
    }

    [Fact]
    [Trait("Category", "UnixDiagnostic")]
    [Trait("EvidenceKind", "UNIX_ONLY_DIAGNOSTIC")]
    [Trait("CountsToward", "NONE")]
    [UnsupportedOSPlatform("windows")]
    public void Quarantine_store_rechecks_unix_symlink_hardlink_and_fifo_after_open()
    {
        RequireUnixDiagnosticHost();
        var request = Request();

        using (var symbolicRuntime = new DrainReceiptTemporaryRuntimeDirectory())
        using (var store = DurableNativeStopProofStore.Open(symbolicRuntime.Path))
        {
            var target = Path.Combine(symbolicRuntime.Path, "outside-record.json");
            WritePrivate(target, "{}"u8.ToArray());
            File.CreateSymbolicLink(
                LegacyProofPath(symbolicRuntime.Path, request.SubmissionAttemptId),
                target);
            AssertQuarantinePathRejected(() =>
                store.InspectExisting(request.SubmissionAttemptId));
        }

        using (var hardLinkRuntime = new DrainReceiptTemporaryRuntimeDirectory())
        using (var store = DurableNativeStopProofStore.Open(hardLinkRuntime.Path))
        {
            var target = Path.Combine(hardLinkRuntime.Path, "outside-record.json");
            WritePrivate(target, "{}"u8.ToArray());
            Assert.Equal(
                0,
                CreateHardLink(
                    target,
                    LegacyProofPath(hardLinkRuntime.Path, request.SubmissionAttemptId)));
            AssertQuarantinePathRejected(() =>
                store.InspectExisting(request.SubmissionAttemptId));
        }

        using (var fifoRuntime = new DrainReceiptTemporaryRuntimeDirectory())
        using (var store = DurableNativeStopProofStore.Open(fifoRuntime.Path))
        {
            Assert.Equal(
                0,
                CreateFifo(
                    LegacyProofPath(fifoRuntime.Path, request.SubmissionAttemptId),
                    Convert.ToUInt32("600", 8)));
            AssertQuarantinePathRejected(() =>
                store.InspectExisting(request.SubmissionAttemptId));
        }
    }

    private static void AssertZeroAuthority(string runtimeDirectory)
    {
        Assert.Empty(Directory.EnumerateFiles(
            runtimeDirectory,
            "native-stop-proof-*.json",
            SearchOption.TopDirectoryOnly));
    }

    private static LegacyFixtureRequest Request() => new(
        Guid.Parse("7b000000-0000-0000-0000-00000000000b"),
        Guid.Parse("71000000-0000-0000-0000-000000000001"),
        Guid.Parse("72000000-0000-0000-0000-000000000002"),
        1,
        new string('1', 64),
        new string('2', 64),
        "soul_" + new string('3', 64),
        "db_" + new string('4', 32),
        "pa_" + new string('5', 32),
        "trace_" + new string('6', 32),
        "idem_" + new string('7', 64),
        new string('8', 64),
        17,
        new string('9', 64),
        "wi_" + new string('a', 32),
        23);

    private static (string InputFingerprintSha256, string WireSha256, int WireBytes)
        WriteLegacyRecord(string runtimeDirectory, LegacyFixtureRequest request)
    {
        var wire = CreateLegacyOwnerWire(request);
        var wireSha256 = Sha256(wire);
        var fingerprint = new string('f', 64);
        var record = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema_version = "1.0",
            submission_attempt_id = request.SubmissionAttemptId.ToString("D"),
            input_fingerprint_sha256 = fingerprint,
            wire_base64 = Convert.ToBase64String(wire),
            wire_sha256 = wireSha256
        });
        WritePrivate(LegacyProofPath(runtimeDirectory, request.SubmissionAttemptId), record);
        return (fingerprint, wireSha256, wire.Length);
    }

    private static void WriteLegacyEnvelope(
        string runtimeDirectory,
        LegacyFixtureRequest request,
        string wireBase64,
        string wireSha256)
    {
        var record = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema_version = "1.0",
            submission_attempt_id = request.SubmissionAttemptId.ToString("D"),
            input_fingerprint_sha256 = new string('f', 64),
            wire_base64 = wireBase64,
            wire_sha256 = wireSha256
        });
        WritePrivate(LegacyProofPath(runtimeDirectory, request.SubmissionAttemptId), record);
    }

    private static byte[] CreateLegacyOwnerWire(LegacyFixtureRequest request)
    {
        var placeholder = Convert.ToBase64String(
            new byte[NativeAbortConfirmation.P1363SignatureSizeBytes]);
        var proof = new NativeAbortConfirmation(
            NativeAbortConfirmation.CurrentSchemaVersion,
            NativeAbortConfirmation.CurrentContractId,
            NativeAbortConfirmation.CurrentProducerModule,
            true,
            request.SubmissionAttemptId,
            request.CommandId,
            request.LeaseId,
            request.Attempt,
            request.NativeRequestBindingSha256,
            request.SubmittedRequestSha256,
            request.SoulId,
            request.DeviceBindingId,
            request.PlatformAccountId,
            request.TraceId,
            request.IdempotencyKey,
            request.ActiveReleaseBomSha256,
            request.ActiveReleaseBomGeneration,
            request.ActiveReleaseBomTokenSha256,
            request.WorkerInstanceId,
            request.WorkerGeneration,
            NativeAbortConfirmation.TransportAborted,
            new string('0', 64),
            OccurredAt,
            NativeAbortConfirmation.CurrentPrivacyClass,
            NativeAbortConfirmation.CurrentAuthScope,
            "legacy-v1-quarantine-fixture",
            placeholder);
        proof = proof with
        {
            EvidenceSha256 = NativeStopProofProtocolV1.ComputeEvidenceSha256(proof)
        };
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var signingBytes = NativeStopProofProtocolV1.CanonicalSigningBytes(proof);
        try
        {
            var signature = key.SignData(
                signingBytes,
                HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            try
            {
                proof = proof with { SignatureBase64 = Convert.ToBase64String(signature) };
            }
            finally
            {
                CryptographicOperations.ZeroMemory(signature);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(signingBytes);
        }
        return System.Text.Encoding.UTF8.GetBytes(
            ExecutorGatewayContractJson.SerializeNativeStopProof(proof));
    }

    private static string LegacyProofPath(string runtimeDirectory, Guid submissionAttemptId) =>
        Path.Combine(
            runtimeDirectory,
            "native-stop-proof-" + submissionAttemptId.ToString("N") + ".json");

    private static void WritePrivate(string path, byte[] bytes)
    {
        var directory = Path.GetDirectoryName(path) ??
            throw new InvalidOperationException("legacy quarantine fixture path has no directory");
        if (!Directory.Exists(directory))
        {
            if (OperatingSystem.IsWindows())
                Directory.CreateDirectory(directory);
            else
                Directory.CreateDirectory(
                    directory,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        File.WriteAllBytes(path, bytes);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    private static string Sha256(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static void AssertQuarantinePathRejected(Action action)
    {
        var error = Record.Exception(action);
        Assert.NotNull(error);
        Assert.True(
            error is IOException or UnauthorizedAccessException,
            "expected a filesystem-bound quarantine rejection, got " + error.GetType().FullName);
    }

    private static void RequireUnixDiagnosticHost()
    {
        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux())
            throw new PlatformNotSupportedException(
                "UnixDiagnostic is non-counting Unix evidence; Windows ACL/file-ID evidence remains WAITING_EXTERNAL");
    }

    private static IReadOnlyList<(string, string)> MethodTraits(MethodInfo method) =>
        method.GetCustomAttributesData()
            .Where(attribute => attribute.AttributeType.Name == "TraitAttribute" &&
                attribute.ConstructorArguments.Count == 2)
            .Select(attribute => (
                attribute.ConstructorArguments[0].Value as string ?? string.Empty,
                attribute.ConstructorArguments[1].Value as string ?? string.Empty))
            .ToArray();

    private static string FindRepositoryRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory);
             current is not null;
             current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "AGENTS.md")) &&
                Directory.Exists(Path.Combine(current.FullName, "Modules")))
                return current.FullName;
        }
        throw new DirectoryNotFoundException("repository root was not found");
    }

    private sealed record LegacyFixtureRequest(
        Guid SubmissionAttemptId,
        Guid CommandId,
        Guid LeaseId,
        int Attempt,
        string NativeRequestBindingSha256,
        string SubmittedRequestSha256,
        string SoulId,
        string DeviceBindingId,
        string PlatformAccountId,
        string TraceId,
        string IdempotencyKey,
        string ActiveReleaseBomSha256,
        long ActiveReleaseBomGeneration,
        string ActiveReleaseBomTokenSha256,
        string WorkerInstanceId,
        long WorkerGeneration);

    [DllImport("libc", EntryPoint = "link", SetLastError = true)]
    private static extern int CreateHardLink(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string existingPath,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string newPath);

    [DllImport("libc", EntryPoint = "mkfifo", SetLastError = true)]
    private static extern int CreateFifo(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        uint mode);

    [DllImport("libc", EntryPoint = "geteuid")]
    private static extern uint NativeGetEffectiveUserId();
}
