using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dps.EdgeLocalJournal;
using Dps.WindowsEdgeWorker;
using Xunit;

namespace Dps.WindowsEdgeWorker.Tests;

public sealed class WorkerTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task Duplicate_delivery_returns_recorded_receipt_without_second_side_effect()
    {
        var token = TestContext.Current.CancellationToken;
        var transport = new FakeTransport(new NativeDispatchResult(true, NativeStatus.Success, true, "verified"));
        var processor = Processor(transport);
        var command = Command();

        var first = await processor.ProcessAsync(command, token);
        var duplicate = await processor.ProcessAsync(command, token);

        Assert.Equal("VERIFIED_SUCCESS", first.ResultStatus);
        Assert.True(duplicate.Duplicate);
        Assert.Equal(1, transport.Calls);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Newline_partition_collision_is_distinct_and_conflicting_duplicate_is_quarantined()
    {
        var token = TestContext.Current.CancellationToken;
        var transport = new FakeTransport(new NativeDispatchResult(true, NativeStatus.Success, true, "verified"));
        var processor = Processor(transport);
        var first = WithHash(Command() with
        {
            Selector = "fixture:button\nsecondary",
            Text = "tail"
        });
        var conflicting = WithHash(Command() with
        {
            Selector = "fixture:button",
            Text = "secondary\ntail"
        });

        Assert.Equal(
            string.Join("\n", first.Selector, first.Text),
            string.Join("\n", conflicting.Selector, conflicting.Text));
        Assert.NotEqual(first.RequestSha256, conflicting.RequestSha256);
        Assert.Equal(
            "f5f4d6842826b7290aa739948169e25fdee7750a0c5e4659fbc850b9175d962f",
            first.RequestSha256);

        Assert.Equal("VERIFIED_SUCCESS", (await processor.ProcessAsync(first, token)).ResultStatus);
        var duplicate = await processor.ProcessAsync(first, token);
        var conflict = await processor.ProcessAsync(conflicting, token);

        Assert.True(duplicate.Duplicate);
        Assert.Equal("QUARANTINED", conflict.ResultStatus);
        Assert.False(conflict.RetryAllowed);
        Assert.Equal(1, transport.Calls);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Request_hash_has_a_domain_separated_deterministic_golden_vector()
    {
        var command = Command();

        Assert.Equal(
            "2b712548afe5d1fd37e842ef420d277dd874ad76b4dcf11722e6165a20aacf0d",
            command.RequestSha256);
        Assert.Equal(command.RequestSha256, CommandHasher.Compute(command));
        Assert.NotEqual(
            CommandHasher.Compute(command with { Text = null }),
            CommandHasher.Compute(command with { Text = "<null>" }));
        Assert.NotEqual(
            CommandHasher.Compute(command with { Text = null }),
            CommandHasher.Compute(command with { Text = string.Empty }));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Request_hash_is_culture_independent_and_rejects_noncanonical_time_offsets()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            var command = Command();
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("tr-TR");

            Assert.Equal(command.RequestSha256, CommandHasher.Compute(command));
            Assert.Throws<InvalidDataException>(() => CommandHasher.Compute(command with
            {
                OccurredAt = command.OccurredAt!.Value.ToOffset(TimeSpan.FromHours(8))
            }));
            Assert.Throws<InvalidDataException>(() => CommandHasher.Compute(command with
            {
                LeaseExpiresAt = command.LeaseExpiresAt!.Value.ToOffset(TimeSpan.FromHours(8))
            }));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Request_hash_rejects_invalid_unicode()
    {
        Assert.Throws<EncoderFallbackException>(() =>
            CommandHasher.Compute(Command() with { Text = "\ud800" }));
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void Journal_payload_matches_owner_canonical_codec_and_golden_hash()
    {
        var write = WorkerJournalWrite.Create(
            Command(),
            "TERMINAL",
            "VERIFIED_SUCCESS",
            "verified");
        const string expected =
            "{\"detail\":\"verified\",\"entry_type\":\"VERIFIED_SUCCESS\",\"schema_version\":\"1.0\"}";

        Assert.Equal(expected, write.PayloadJson);
        Assert.Equal(expected, CanonicalJson.Canonicalize(write.PayloadJson));
        Assert.Equal(
            "527169419c376942fb39ea2c0d2dbb77ebd9e65aa8caa4850533df5748679a9c",
            write.PayloadSha256);
        Assert.Equal(WorkerTestHash.Sha256(expected), write.PayloadSha256);

        var escaped = WorkerJournalWrite.Create(
            Command(),
            "TERMINAL",
            "VERIFIED_SUCCESS",
            "line one\n\"quoted\"");
        Assert.Equal(escaped.PayloadJson, CanonicalJson.Canonicalize(escaped.PayloadJson));
        Assert.Equal(WorkerTestHash.Sha256(escaped.PayloadJson), escaped.PayloadSha256);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void Journal_request_rejects_non_token_ids_and_noncanonical_payloads()
    {
        var command = Command();
        var context = WorkerJournalContext.FromCommand(command);
        var write = WorkerJournalWrite.Create(command, "TERMINAL", "VERIFIED_SUCCESS", "verified");

        Assert.Throws<InvalidDataException>(() =>
            WorkerJournalAppendRequest.Create(context with { CommandId = "command\n1" }, write));
        Assert.Throws<InvalidDataException>(() =>
            WorkerJournalAppendRequest.Create(context with { CommandId = "command/1" }, write));
        Assert.Throws<InvalidDataException>(() => WorkerExchangeCodec.DecodeCommand(
            JsonSerializer.Serialize(WithHash(command with { CommandId = "command\n1" }))));
        Assert.Throws<InvalidDataException>(() => WorkerExchangeCodec.DecodeCommand(
            JsonSerializer.Serialize(WithHash(command with { CommandId = "command/1" }))));
        Assert.Throws<InvalidDataException>(() =>
            WorkerJournalAppendRequest.Create(
                context,
                write with { EntryId = "worker/unsafe" }));
        Assert.Throws<InvalidDataException>(() =>
            WorkerJournalAppendRequest.Create(
                context,
                write with
                {
                    PayloadJson =
                        "{\"schema_version\":\"1.0\",\"entry_type\":\"VERIFIED_SUCCESS\",\"detail\":\"verified\"}",
                    PayloadSha256 = WorkerTestHash.Sha256(
                        "{\"schema_version\":\"1.0\",\"entry_type\":\"VERIFIED_SUCCESS\",\"detail\":\"verified\"}")
                }));
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("EvidenceKind", "SIMULATION")]
    public async Task Worker_append_is_accepted_by_owner_store_and_raw_noncanonical_hash_fails()
    {
        var token = TestContext.Current.CancellationToken;
        var directory = Path.Combine(
            Path.GetTempPath(),
            "dps-worker-journal-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            await using var store = await JournalStore.OpenAsync(
                Path.Combine(directory, "journal.jsonl"),
                token);
            var command = Command();
            var request = WorkerJournalAppendRequest.Create(
                WorkerJournalContext.FromCommand(command),
                WorkerJournalWrite.Create(command, "TERMINAL", "VERIFIED_SUCCESS", "verified"));

            var receipt = await store.AppendAsync(ToOwnerRequest(request), token);

            Assert.True(receipt.Durable);
            Assert.Equal(request.EntryId, receipt.EntryId);
            Assert.Equal(request.PayloadSha256, receipt.PayloadSha256);
            await Assert.ThrowsAsync<ArgumentException>(() => store.AppendAsync(
                ToOwnerRequest(request) with
                {
                    EntryId = request.EntryId + "_BAD_HASH",
                    PayloadSha256 = new string('0', 64)
                },
                token));

            const string rawNoncanonical =
                "{\"schema_version\":\"1.0\",\"entry_type\":\"VERIFIED_SUCCESS\",\"detail\":\"verified\"}";
            await Assert.ThrowsAsync<ArgumentException>(() => store.AppendAsync(
                ToOwnerRequest(request) with
                {
                    EntryId = request.EntryId + "_RAW_HASH",
                    PayloadJson = rawNoncanonical,
                    PayloadSha256 = WorkerTestHash.Sha256(rawNoncanonical)
                },
                token));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Request_hash_binds_occurred_at_and_privacy_class()
    {
        var command = Command();

        Assert.NotEqual(
            command.RequestSha256,
            CommandHasher.Compute(command with { OccurredAt = command.OccurredAt!.Value.AddSeconds(1) }));
        Assert.NotEqual(
            command.RequestSha256,
            CommandHasher.Compute(command with { PrivacyClass = "sensitive" }));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Same_key_with_cross_soul_device_or_account_scope_is_quarantined()
    {
        var token = TestContext.Current.CancellationToken;
        var transport = new FakeTransport(new NativeDispatchResult(true, NativeStatus.Success, true, "verified"));
        var store = new InMemoryCommandStateStore();
        var processor = Processor(transport, store);
        var first = Command();
        Assert.Equal("VERIFIED_SUCCESS", (await processor.ProcessAsync(first, token)).ResultStatus);
        var changedScopes = new[]
        {
            first with { SoulId = "soul_" + new string('b', 64) },
            first with { DeviceBindingId = "db_" + new string('f', 32) },
            first with { PlatformAccountId = "pa_" + new string('1', 32) }
        };
        foreach (var changed in changedScopes)
        {
            var receipt = await processor.ProcessAsync(WithHash(changed), token);
            Assert.Equal("QUARANTINED", receipt.ResultStatus);
        }

        Assert.Equal(1, transport.Calls);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Native_success_without_postcondition_is_failure_and_shadow_never_dispatches()
    {
        var token = TestContext.Current.CancellationToken;
        var native = new FakeTransport(new NativeDispatchResult(true, NativeStatus.Success, false, "native only"));
        var nativeReceipt = await Processor(native).ProcessAsync(Command(), token);
        Assert.Equal("FAILED", nativeReceipt.ResultStatus);
        Assert.True(nativeReceipt.DispatchAcknowledged);

        var shadowTransport = new FakeTransport(new NativeDispatchResult(true, NativeStatus.Success, true, "must not run"));
        var shadow = WithHash(Command() with
        {
            IdempotencyKey = "idem_" + new string('9', 64),
            CommandId = "command-shadow",
            Shadow = true
        });
        var shadowReceipt = await Processor(shadowTransport).ProcessAsync(shadow, token);
        Assert.Equal("SHADOWED", shadowReceipt.ResultStatus);
        Assert.False(shadowReceipt.DispatchAcknowledged);
        Assert.Equal(0, shadowTransport.Calls);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void Worker_contract_is_owned_by_supervisor_and_defines_all_exchange_kinds_fail_closed()
    {
        var contractRoot = ContractRoot();
        using var schema = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(contractRoot, "edge.worker.exchange.v1.schema.json")));
        var properties = schema.RootElement.GetProperty("properties");
        Assert.False(schema.RootElement.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(
            "edge.worker.exchange/v1",
            properties.GetProperty("contract_id").GetProperty("const").GetString());
        Assert.Equal(
            new[] { "windows-edge-supervisor", "windows-edge-worker" },
            properties.GetProperty("producer_module").GetProperty("enum")
                .EnumerateArray().Select(value => value.GetString()).ToArray());

        var conditionals = schema.RootElement.GetProperty("allOf").EnumerateArray().ToArray();
        Assert.Equal(
            new[] { "COMMAND", "RECEIPT", "HEALTH" },
            conditionals.Select(conditional => conditional.GetProperty("if").GetProperty("properties")
                .GetProperty("exchange_kind").GetProperty("const").GetString()).ToArray());
        Assert.Equal(
            "windows-edge-supervisor",
            ConditionalProperties(conditionals, "COMMAND").GetProperty("producer_module").GetProperty("const").GetString());
        Assert.Equal(
            "windows-edge-worker",
            ConditionalProperties(conditionals, "RECEIPT").GetProperty("producer_module").GetProperty("const").GetString());
        Assert.Equal("null", ConditionalProperties(conditionals, "HEALTH").GetProperty("request_sha256").GetProperty("type").GetString());
        Assert.Equal("boolean", ConditionalProperties(conditionals, "RECEIPT").GetProperty("duplicate").GetProperty("type").GetString());
        Assert.Equal("boolean", ConditionalProperties(conditionals, "RECEIPT").GetProperty("retry_allowed").GetProperty("type").GetString());

        var specFile = schema.RootElement.GetProperty("x-dps-request-sha256-spec").GetString();
        Assert.Equal("edge.worker.exchange.v1.request-sha256.json", specFile);
        using var spec = JsonDocument.Parse(File.ReadAllText(Path.Combine(contractRoot, specFile!)));
        Assert.Equal("windows-edge-supervisor", spec.RootElement.GetProperty("owner_module").GetString());
        Assert.Equal("COMPUTED_BY_THIS_SPEC", spec.RootElement.GetProperty("exchange_kind_semantics")
            .GetProperty("COMMAND").GetProperty("value").GetString());
        Assert.Equal("ORIGINAL_COMMAND_REQUEST_SHA256", spec.RootElement.GetProperty("exchange_kind_semantics")
            .GetProperty("RECEIPT").GetProperty("value").GetString());
        Assert.Equal(
            new[]
            {
                "ContractId", "ProducerModule", "SoulId", "DeviceBindingId", "PlatformAccountId",
                "CommandId", "TraceId", "IdempotencyKey", "OccurredAt", "PrivacyClass",
                "LeaseId", "LeaseExpiresAt",
                "ActionKind", "StepKind", "Selector", "Text", "WaitMs", "ExpectedPostcondition", "Shadow"
            },
            spec.RootElement.GetProperty("fields").EnumerateArray()
                .Select(field => field.GetProperty("worker_property").GetString())
                .ToArray());
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void Worker_decoder_accepts_the_supervisor_production_wire_golden_vector()
    {
        var contractRoot = ContractRoot();
        var command = WorkerExchangeCodec.DecodeCommand(File.ReadAllBytes(
            Path.Combine(contractRoot, "edge.worker.exchange.v1.command.golden.json")));

        Assert.Equal("1.0", command.SchemaVersion);
        Assert.Equal("COMMAND", command.ExchangeKind);
        Assert.Equal("personal", command.PrivacyClass);
        Assert.Equal("command-vector-1", command.CommandId);
        Assert.Equal("hello\n世界", command.Text);
        Assert.True(command.Shadow);
        Assert.Equal(
            "d7a8f4901c7d56f833b2ff24ea169bff565984a6417f165a4f25a5ff233d8d1e",
            command.RequestSha256);
        Assert.Equal(command.RequestSha256, CommandHasher.Compute(command));
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void Worker_decoder_rejects_unknown_duplicate_missing_overlong_and_non_command_wire_fields()
    {
        var json = File.ReadAllText(Path.Combine(
            ContractRoot(),
            "edge.worker.exchange.v1.command.golden.json"));
        var mutations = new[]
        {
            json.Replace("\n}", ",\n  \"unknown_field\": true\n}", StringComparison.Ordinal),
            json.Replace(
                "\"schema_version\": \"1.0\",",
                "\"schema_version\": \"1.0\", \"schema_version\": \"1.0\",",
                StringComparison.Ordinal),
            json.Replace("  \"schema_version\": \"1.0\",\n", string.Empty, StringComparison.Ordinal),
            json.Replace("\"exchange_kind\": \"COMMAND\"", "\"exchange_kind\": \"RECEIPT\"", StringComparison.Ordinal),
            json.Replace("\"exchange_kind\": \"COMMAND\"", "\"exchange_kind\": \"DRAIN\"", StringComparison.Ordinal),
            json.Replace("\"trace_id\": \"trace_dddddddddddddddddddddddddddddddd\"", "\"trace_id\": \"" + new string('x', 129) + "\"", StringComparison.Ordinal),
            json.Replace("\"command_id\": \"command-vector-1\"", "\"command_id\": null", StringComparison.Ordinal),
            json.Replace("2026-07-14T00:00:00+00:00", "2026-07-14T08:00:00+08:00", StringComparison.Ordinal),
            json.Replace("2026-07-14T00:05:00+00:00", "2026-07-14T08:05:00+08:00", StringComparison.Ordinal),
            json.Replace(
                "d7a8f4901c7d56f833b2ff24ea169bff565984a6417f165a4f25a5ff233d8d1e",
                new string('0', 64),
                StringComparison.Ordinal)
        };

        foreach (var mutation in mutations)
            Assert.Throws<InvalidDataException>(() => WorkerExchangeCodec.DecodeCommand(mutation));
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void Worker_production_codecs_interoperate_for_receipt_and_health_golden_wires()
    {
        var contractRoot = ContractRoot();
        var source = WorkerExchangeCodec.DecodeCommand(File.ReadAllBytes(
            Path.Combine(contractRoot, "edge.worker.exchange.v1.command.golden.json")));
        var receipt = new CommandReceipt(
            source.CommandId,
            source.IdempotencyKey,
            "VERIFIED_SUCCESS",
            true,
            NativeStatus.Success,
            true,
            Duplicate: false,
            RetryAllowed: false,
            "fixture state changed");
        using var actualReceipt = JsonDocument.Parse(WorkerExchangeCodec.EncodeReceipt(
            source,
            receipt,
            DateTimeOffset.Parse("2026-07-14T00:00:01+00:00")));
        using var expectedReceipt = JsonDocument.Parse(File.ReadAllBytes(
            Path.Combine(contractRoot, "edge.worker.exchange.v1.receipt.golden.json")));
        Assert.True(JsonElement.DeepEquals(expectedReceipt.RootElement, actualReceipt.RootElement));

        var healthReport = new WorkerHealthReport(
            "soul_" + new string('a', 64),
            "db_" + new string('b', 32),
            "pa_" + new string('c', 32),
            "trace_" + new string('f', 32),
            "idem_" + new string('a', 64),
            DateTimeOffset.Parse("2026-07-14T00:00:02+00:00"),
            "internal",
            "worker healthy");
        using var actualHealth = JsonDocument.Parse(WorkerExchangeCodec.EncodeHealth(healthReport));
        using var expectedHealth = JsonDocument.Parse(File.ReadAllBytes(
            Path.Combine(contractRoot, "edge.worker.exchange.v1.health.golden.json")));
        Assert.True(JsonElement.DeepEquals(expectedHealth.RootElement, actualHealth.RootElement));

        Assert.Throws<InvalidDataException>(() => WorkerExchangeCodec.EncodeReceipt(
            source,
            receipt with { ResultStatus = "VERIFIED_SUCCESS", DispatchAcknowledged = false },
            DateTimeOffset.Parse("2026-07-14T00:00:01+00:00")));
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("EvidenceKind", "SIMULATION")]
    public async Task Restart_after_reservation_before_acceptance_resumes_and_dispatches_once()
    {
        var token = TestContext.Current.CancellationToken;
        var command = Command();
        var store = new InMemoryCommandStateStore();
        var interruptedEpoch = store.BeginProcessEpoch();
        var reservation = store.TryBegin(command.IdempotencyKey!, command.RequestSha256!, interruptedEpoch);
        Assert.Equal("NEW", reservation.Status);
        Assert.Equal(CommandExecutionPhase.Reserved, reservation.Phase);

        var transport = SuccessfulTransport();
        var receipt = await Processor(transport, store).ProcessAsync(command, token);

        Assert.Equal("VERIFIED_SUCCESS", receipt.ResultStatus);
        Assert.True(receipt.DispatchAcknowledged);
        Assert.Equal(1, transport.Calls);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("EvidenceKind", "SIMULATION")]
    public async Task Restart_after_acceptance_before_transport_resumes_and_dispatches_once()
    {
        var token = TestContext.Current.CancellationToken;
        var command = Command();
        var store = new InMemoryCommandStateStore();
        var interruptedEpoch = store.BeginProcessEpoch();
        Assert.Equal("NEW", store.TryBegin(command.IdempotencyKey!, command.RequestSha256!, interruptedEpoch).Status);
        store.MarkAccepted(command.IdempotencyKey!, interruptedEpoch);

        var transport = SuccessfulTransport();
        var receipt = await Processor(transport, store).ProcessAsync(command, token);

        Assert.Equal("VERIFIED_SUCCESS", receipt.ResultStatus);
        Assert.True(receipt.DispatchAcknowledged);
        Assert.Equal(1, transport.Calls);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("EvidenceKind", "SIMULATION")]
    public async Task Restart_after_transport_attempt_before_ack_is_unknown_with_ack_truth_unset()
    {
        var token = TestContext.Current.CancellationToken;
        var command = Command();
        var store = StateAt(command, CommandExecutionPhase.TransportAttempted);
        var transport = SuccessfulTransport("must not run");

        var receipt = await Processor(transport, store).ProcessAsync(command, token);

        Assert.Equal("UNKNOWN_OUTCOME", receipt.ResultStatus);
        Assert.Null(receipt.DispatchAcknowledged);
        Assert.False(receipt.RetryAllowed);
        Assert.Equal(0, transport.Calls);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("EvidenceKind", "SIMULATION")]
    public async Task Restart_after_dispatch_ack_before_completion_is_unknown_with_ack_true()
    {
        var token = TestContext.Current.CancellationToken;
        var command = Command();
        var store = StateAt(command, CommandExecutionPhase.DispatchAcknowledged);
        var transport = SuccessfulTransport("must not run");

        var receipt = await Processor(transport, store).ProcessAsync(command, token);

        Assert.Equal("UNKNOWN_OUTCOME", receipt.ResultStatus);
        Assert.True(receipt.DispatchAcknowledged);
        Assert.False(receipt.RetryAllowed);
        Assert.Equal(0, transport.Calls);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("EvidenceKind", "SIMULATION")]
    public async Task Same_epoch_duplicates_only_retry_before_transport_and_reconcile_after_attempt()
    {
        var token = TestContext.Current.CancellationToken;
        var command = Command();
        var store = new InMemoryCommandStateStore();
        var transport = SuccessfulTransport("must not run");
        var processor = Processor(transport, store);
        const long processorEpoch = 1;
        Assert.Equal("NEW", store.TryBegin(command.IdempotencyKey!, command.RequestSha256!, processorEpoch).Status);
        store.MarkAccepted(command.IdempotencyKey!, processorEpoch);

        var acceptedDuplicate = await processor.ProcessAsync(command, token);
        Assert.Equal("IN_PROGRESS", acceptedDuplicate.ResultStatus);
        Assert.False(acceptedDuplicate.DispatchAcknowledged);
        Assert.True(acceptedDuplicate.RetryAllowed);

        store.MarkTransportAttempted(command.IdempotencyKey!, processorEpoch);
        var attemptedDuplicate = await processor.ProcessAsync(command, token);
        Assert.Equal("UNKNOWN_OUTCOME", attemptedDuplicate.ResultStatus);
        Assert.Null(attemptedDuplicate.DispatchAcknowledged);
        Assert.False(attemptedDuplicate.RetryAllowed);

        store.MarkDispatchAcknowledged(command.IdempotencyKey!, processorEpoch);
        var acknowledgedDuplicate = await processor.ProcessAsync(command, token);
        Assert.Equal("UNKNOWN_OUTCOME", acknowledgedDuplicate.ResultStatus);
        Assert.True(acknowledgedDuplicate.DispatchAcknowledged);
        Assert.False(acknowledgedDuplicate.RetryAllowed);
        Assert.Equal(0, transport.Calls);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("EvidenceKind", "SIMULATION")]
    public async Task Pre_dispatch_attempt_budget_survives_restart_and_never_exceeds_two()
    {
        var token = TestContext.Current.CancellationToken;
        var command = Command();
        var store = new InMemoryCommandStateStore();
        var interruptedEpoch = store.BeginProcessEpoch();
        Assert.Equal("NEW", store.TryBegin(command.IdempotencyKey!, command.RequestSha256!, interruptedEpoch).Status);
        store.MarkAccepted(command.IdempotencyKey!, interruptedEpoch);
        Assert.Equal(1, store.MarkTransportAttempted(command.IdempotencyKey!, interruptedEpoch));
        store.MarkPreDispatchRetry(command.IdempotencyKey!, interruptedEpoch);
        var transport = new FakeTransport(
            new NativeDispatchResult(false, NativeStatus.Failed, false, "still offline"),
            new NativeDispatchResult(true, NativeStatus.Success, true, "must not run"));

        var receipt = await Processor(transport, store).ProcessAsync(command, token);

        Assert.Equal("FAILED", receipt.ResultStatus);
        Assert.False(receipt.DispatchAcknowledged);
        Assert.False(receipt.RetryAllowed);
        Assert.Equal(1, transport.Calls);
        var completed = store.TryBegin(command.IdempotencyKey!, command.RequestSha256!, store.BeginProcessEpoch());
        Assert.Equal("DUPLICATE", completed.Status);
        Assert.Equal(CommandDispatchPolicy.MaximumAttempts, completed.DispatchAttemptCount);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("EvidenceKind", "SIMULATION")]
    public async Task Unknown_outcome_is_not_retried_and_survives_processor_recovery()
    {
        var token = TestContext.Current.CancellationToken;
        var transport = new FakeTransport(
            new NativeDispatchResult(true, NativeStatus.UnknownOutcome, false, "lost after acknowledgement"));
        var store = new InMemoryCommandStateStore();
        var command = Command();
        var first = await Processor(transport, store).ProcessAsync(command, token);
        var recovered = await Processor(transport, store).ProcessAsync(command, token);

        Assert.Equal("UNKNOWN_OUTCOME", first.ResultStatus);
        Assert.True(first.DispatchAcknowledged);
        Assert.Equal("UNKNOWN_OUTCOME", recovered.ResultStatus);
        Assert.True(recovered.DispatchAcknowledged);
        Assert.False(recovered.RetryAllowed);
        Assert.Equal(1, transport.Calls);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("EvidenceKind", "SIMULATION")]
    public async Task Ambiguous_transport_exception_is_unknown_and_never_redispatched()
    {
        var token = TestContext.Current.CancellationToken;
        var transport = new FakeTransport(
            new TransportDispatchException("connection lost during write", dispatchAcknowledged: null));
        var store = new InMemoryCommandStateStore();
        var command = Command();

        var first = await Processor(transport, store).ProcessAsync(command, token);
        var duplicate = await Processor(transport, store).ProcessAsync(command, token);

        Assert.Equal("UNKNOWN_OUTCOME", first.ResultStatus);
        Assert.Null(first.DispatchAcknowledged);
        Assert.Equal("UNKNOWN_OUTCOME", duplicate.ResultStatus);
        Assert.Equal(1, transport.Calls);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("EvidenceKind", "SIMULATION")]
    public async Task Only_pre_dispatch_failure_gets_one_bounded_retry()
    {
        var token = TestContext.Current.CancellationToken;
        var transport = new FakeTransport(
            new TransportDispatchException("offline before write", dispatchAcknowledged: false),
            new NativeDispatchResult(true, NativeStatus.Success, true, "verified"));

        var receipt = await Processor(transport).ProcessAsync(Command(), token);

        Assert.Equal("VERIFIED_SUCCESS", receipt.ResultStatus);
        Assert.Equal(2, transport.Calls);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("EvidenceKind", "SIMULATION")]
    public async Task Terminal_receipt_is_not_visible_until_the_final_Journal_append_is_durable()
    {
        var token = TestContext.Current.CancellationToken;
        var command = Command();
        var transport = SuccessfulTransport();
        var store = new InMemoryCommandStateStore();
        var journal = new InMemoryJournal(failOnceEntryType: "VERIFIED_SUCCESS");
        var processor = Processor(transport, store, journal);

        await Assert.ThrowsAsync<IOException>(() => processor.ProcessAsync(command, token));

        var pending = store.GetDrainSnapshot();
        Assert.Equal(1, pending.CompletionPendingCount);
        Assert.False(pending.IsDrained);

        var duplicate = await processor.ProcessAsync(command, token);

        Assert.Equal("VERIFIED_SUCCESS", duplicate.ResultStatus);
        Assert.True(duplicate.Duplicate);
        Assert.False(duplicate.RetryAllowed);
        Assert.Equal(1, transport.Calls);
        Assert.Equal(2, journal.Writes.Count(write => write.EntryType == "VERIFIED_SUCCESS"));
        Assert.Single(journal.States, state => state == "VERIFIED_SUCCESS");
        processor.StopIntake();
        Assert.True(processor.IsDrained);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("EvidenceKind", "SIMULATION")]
    public async Task Crash_after_final_Journal_append_replays_the_same_entry_before_completion()
    {
        var token = TestContext.Current.CancellationToken;
        var command = Command();
        var transport = SuccessfulTransport();
        var innerStore = new InMemoryCommandStateStore();
        var store = new FailOnceFinalizeStore(innerStore);
        var journal = new InMemoryJournal();
        var processor = Processor(transport, store, journal);

        await Assert.ThrowsAsync<IOException>(() => processor.ProcessAsync(command, token));
        Assert.Equal(1, innerStore.GetDrainSnapshot().CompletionPendingCount);

        var recoveredTransport = SuccessfulTransport("must not run");
        var recoveredProcessor = Processor(recoveredTransport, store, journal);
        Assert.Equal(1, await recoveredProcessor.ReconcilePreparedCompletionsAsync(token));
        var duplicate = await recoveredProcessor.ProcessAsync(command, token);

        Assert.Equal("VERIFIED_SUCCESS", duplicate.ResultStatus);
        Assert.True(duplicate.Duplicate);
        Assert.Equal(1, transport.Calls);
        Assert.Equal(0, recoveredTransport.Calls);
        var terminalWrites = journal.Writes.Where(write => write.EntryType == "VERIFIED_SUCCESS").ToArray();
        Assert.Equal(2, terminalWrites.Length);
        Assert.Equal(terminalWrites[0].EntryId, terminalWrites[1].EntryId);
        Assert.Equal(terminalWrites[0].PayloadSha256, terminalWrites[1].PayloadSha256);
        Assert.Single(journal.States, state => state == "VERIFIED_SUCCESS");
        Assert.True(innerStore.GetDrainSnapshot().IsDrained);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("EvidenceKind", "SIMULATION")]
    public async Task Drain_truth_includes_persisted_unfinished_uncertain_and_completion_pending_state()
    {
        var token = TestContext.Current.CancellationToken;
        var command = Command();

        var unfinishedStore = new InMemoryCommandStateStore();
        var unfinishedEpoch = unfinishedStore.BeginProcessEpoch();
        Assert.Equal("NEW", unfinishedStore.TryBegin(
            command.IdempotencyKey!, command.RequestSha256!, unfinishedEpoch).Status);
        unfinishedStore.MarkAccepted(command.IdempotencyKey!, unfinishedEpoch);
        var unfinishedProcessor = Processor(SuccessfulTransport("must not run"), unfinishedStore);
        unfinishedProcessor.StopIntake();
        Assert.False(unfinishedProcessor.IsDrained);
        Assert.Equal(1, unfinishedProcessor.GetDrainSnapshot().UnfinishedCount);

        var uncertainStore = StateAt(command, CommandExecutionPhase.TransportAttempted);
        var uncertainProcessor = Processor(SuccessfulTransport("must not run"), uncertainStore);
        uncertainProcessor.StopIntake();
        Assert.False(uncertainProcessor.IsDrained);
        Assert.Equal(1, uncertainProcessor.GetDrainSnapshot().UncertainCount);

        var pendingStore = new InMemoryCommandStateStore();
        var pendingEpoch = pendingStore.BeginProcessEpoch();
        Assert.Equal("NEW", pendingStore.TryBegin(
            command.IdempotencyKey!, command.RequestSha256!, pendingEpoch).Status);
        pendingStore.MarkAccepted(command.IdempotencyKey!, pendingEpoch);
        var preparedReceipt = new CommandReceipt(
            command.CommandId, command.IdempotencyKey, "SHADOWED", false, null, null,
            Duplicate: false, RetryAllowed: false, "prepared terminal audit");
        pendingStore.PrepareCompletion(
            command.IdempotencyKey!,
            pendingEpoch,
            WorkerJournalContext.FromCommand(command),
            preparedReceipt,
            WorkerJournalWrite.Create(command, "TERMINAL", "SHADOWED", preparedReceipt.Detail));
        var pendingProcessor = Processor(SuccessfulTransport("must not run"), pendingStore);
        pendingProcessor.StopIntake();
        Assert.False(pendingProcessor.IsDrained);
        Assert.Equal(1, pendingProcessor.GetDrainSnapshot().CompletionPendingCount);

        var completedProcessor = Processor(SuccessfulTransport());
        Assert.Equal("VERIFIED_SUCCESS", (await completedProcessor.ProcessAsync(command, token)).ResultStatus);
        Assert.False(completedProcessor.IsDrained);
        completedProcessor.StopIntake();
        Assert.True(completedProcessor.IsDrained);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("EvidenceKind", "SIMULATION")]
    public async Task Drain_truth_stays_false_for_the_entire_reconciliation_pass()
    {
        using var store = new BlockingClaimStateStore();
        var processor = Processor(SuccessfulTransport("must not run"), store);
        processor.StopIntake();
        Assert.True(processor.IsDrained);

        var reconciliation = Task.Run(
            () => processor.ReconcilePreparedCompletionsAsync(
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);
        Assert.True(store.WaitUntilClaimed(TimeSpan.FromSeconds(5)));
        Assert.False(processor.IsDrained);

        store.ReleaseClaim();
        Assert.Equal(0, await reconciliation);
        Assert.True(processor.IsDrained);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Production_mode_rejects_simulation_only_adapters()
    {
        var transport = SuccessfulTransport("must not run");

        var exception = Assert.Throws<InvalidOperationException>(() => Processor(
            transport,
            new InMemoryCommandStateStore(),
            new InMemoryJournal(),
            WorkerRuntimeMode.Production));

        Assert.Contains("durable", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, transport.Calls);
    }

    private static CommandProcessor Processor(
        FakeTransport transport,
        ICommandStateStore? store = null,
        IWorkerJournal? journal = null,
        WorkerRuntimeMode runtimeMode = WorkerRuntimeMode.Simulation) =>
        new(
            transport,
            journal ?? new InMemoryJournal(),
            store ?? new InMemoryCommandStateStore(),
            new FixedTimeProvider(DateTimeOffset.Parse("2026-07-14T00:00:00Z")),
            runtimeMode);

    private static FakeTransport SuccessfulTransport(string detail = "verified") =>
        new(new NativeDispatchResult(true, NativeStatus.Success, true, detail));

    internal static WorkerCommand Command()
    {
        var command = new WorkerCommand(
            "1.0",
            "edge.worker.exchange/v1",
            "windows-edge-supervisor",
            "soul_" + new string('a', 64),
            "db_" + new string('b', 32),
            "pa_" + new string('c', 32),
            "trace_" + new string('d', 32),
            "idem_" + new string('e', 64),
            DateTimeOffset.Parse("2026-07-14T00:00:00Z"),
            "personal",
            "COMMAND",
            "command-1",
            "lease-1",
            DateTimeOffset.Parse("2026-07-14T00:05:00Z"),
            string.Empty,
            "TAP",
            "TAP_SELECTOR",
            "fixture:button",
            null,
            null,
            "fixture state changed",
            false,
            null,
            null,
            null,
            null,
            null,
            null,
            null);
        return WithHash(command);
    }

    internal static WorkerCommand WithHash(WorkerCommand command) =>
        command with { RequestSha256 = CommandHasher.Compute(command) };

    private static InMemoryCommandStateStore StateAt(WorkerCommand command, CommandExecutionPhase phase)
    {
        var store = new InMemoryCommandStateStore();
        var interruptedEpoch = store.BeginProcessEpoch();
        Assert.Equal("NEW", store.TryBegin(command.IdempotencyKey!, command.RequestSha256!, interruptedEpoch).Status);
        store.MarkAccepted(command.IdempotencyKey!, interruptedEpoch);
        if (phase is CommandExecutionPhase.TransportAttempted or CommandExecutionPhase.DispatchAcknowledged)
            store.MarkTransportAttempted(command.IdempotencyKey!, interruptedEpoch);
        if (phase == CommandExecutionPhase.DispatchAcknowledged)
            store.MarkDispatchAcknowledged(command.IdempotencyKey!, interruptedEpoch);
        return store;
    }

    private static JsonElement ConditionalProperties(JsonElement[] conditionals, string kind) =>
        conditionals.Single(conditional => conditional.GetProperty("if").GetProperty("properties")
                .GetProperty("exchange_kind").GetProperty("const").GetString() == kind)
            .GetProperty("then").GetProperty("properties");

    private static JournalAppendRequest ToOwnerRequest(WorkerJournalAppendRequest request) => new(
        request.SchemaVersion,
        request.ContractId,
        request.ProducerModule,
        request.CommandId,
        request.EntryId,
        request.EntryType,
        request.TraceId,
        request.IdempotencyKey,
        request.PrivacyClass,
        request.SoulId,
        request.DeviceBindingId,
        request.PlatformAccountId,
        request.PayloadJson,
        request.PayloadSha256,
        request.OccurredAt);

    private static string ContractRoot() => Path.Combine(
        RepositoryRoot(),
        "Modules/windows-edge-supervisor/contracts/provided");

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null &&
               !(File.Exists(Path.Combine(current.FullName, "AGENTS.md")) &&
                 Directory.Exists(Path.Combine(current.FullName, "governance"))))
            current = current.Parent;
        return current?.FullName ?? throw new InvalidOperationException("repository root not found");
    }
}

internal sealed class FakeTransport(params object[] outcomes) : INativeTransport
{
    private readonly Queue<object> _outcomes = new(outcomes);
    public int Calls { get; private set; }

    public Task<NativeDispatchResult> DispatchAsync(WorkerCommand command, CancellationToken cancellationToken)
    {
        Calls++;
        var outcome = _outcomes.Dequeue();
        return outcome is Exception exception
            ? Task.FromException<NativeDispatchResult>(exception)
            : Task.FromResult((NativeDispatchResult)outcome);
    }
}

internal sealed class InMemoryJournal(string? failOnceEntryType = null) : IWorkerJournal
{
    private const string GenesisChecksum =
        "0000000000000000000000000000000000000000000000000000000000000000";

    public List<string> States { get; } = [];
    public List<WorkerJournalAppendRequest> Writes { get; } = [];
    private readonly Dictionary<string, StoredJournalEntry> _entries = new(StringComparer.Ordinal);
    private bool _failed;
    private long _lastSequence;
    private string _lastChecksum = GenesisChecksum;

    public Task<WorkerJournalAppendReceipt> AppendAsync(
        WorkerJournalAppendRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var canonicalPayload = CanonicalJson.Canonicalize(request.PayloadJson);
        if (!string.Equals(canonicalPayload, request.PayloadJson, StringComparison.Ordinal))
            return Task.FromException<WorkerJournalAppendReceipt>(
                new InvalidDataException("worker Journal adapter received noncanonical payload_json"));
        var computedPayloadSha256 = WorkerTestHash.Sha256(canonicalPayload);
        if (!string.Equals(computedPayloadSha256, request.PayloadSha256, StringComparison.Ordinal))
            return Task.FromException<WorkerJournalAppendReceipt>(
                new InvalidDataException("worker Journal adapter received a mismatched payload hash"));

        Writes.Add(request);
        if (!_failed && request.EntryType == failOnceEntryType)
        {
            _failed = true;
            return Task.FromException<WorkerJournalAppendReceipt>(new IOException("injected Journal append failure"));
        }

        if (_entries.TryGetValue(request.EntryId, out var existing))
        {
            if (existing.Request != request)
                return Task.FromException<WorkerJournalAppendReceipt>(
                    new InvalidDataException("journal entry identity was reused with different scoped content"));
            return Task.FromResult(existing.Receipt with { Duplicate = true });
        }

        var sequence = checked(++_lastSequence);
        var previousChecksum = _lastChecksum;
        var entryChecksum = WorkerTestHash.Sha256(JsonSerializer.Serialize(new
        {
            domain = "dps.windows-edge-worker.test-journal-receipt/v1",
            sequence,
            previous_checksum = previousChecksum,
            request.CommandId,
            request.EntryId,
            request.EntryType,
            request.PayloadSha256
        }));
        var receipt = new WorkerJournalAppendReceipt(
            "1.0",
            "edge.journal.receipt/v1",
            "edge-local-journal",
            request.ProducerModule,
            request.CommandId,
            request.EntryId,
            request.EntryType,
            request.TraceId,
            request.IdempotencyKey,
            request.PrivacyClass,
            request.SoulId,
            request.DeviceBindingId,
            request.PlatformAccountId,
            request.OccurredAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            sequence,
            request.PayloadSha256,
            previousChecksum,
            entryChecksum,
            Durable: true,
            Duplicate: false);
        _entries.Add(request.EntryId, new StoredJournalEntry(request, receipt));
        _lastChecksum = entryChecksum;
        States.Add(request.EntryType);
        return Task.FromResult(receipt);
    }

    private sealed record StoredJournalEntry(
        WorkerJournalAppendRequest Request,
        WorkerJournalAppendReceipt Receipt);
}

internal sealed class FailOnceFinalizeStore(ICommandStateStore inner) : ICommandStateStore
{
    private bool _failed;

    public long BeginProcessEpoch() => inner.BeginProcessEpoch();
    public BeginResult? ReadExisting(string idempotencyKey, string requestSha256, long processEpoch) =>
        inner.ReadExisting(idempotencyKey, requestSha256, processEpoch);
    public BeginResult TryBegin(string idempotencyKey, string requestSha256, long processEpoch) =>
        inner.TryBegin(idempotencyKey, requestSha256, processEpoch);
    public void MarkAccepted(string idempotencyKey, long processEpoch) =>
        inner.MarkAccepted(idempotencyKey, processEpoch);
    public int MarkTransportAttempted(string idempotencyKey, long processEpoch) =>
        inner.MarkTransportAttempted(idempotencyKey, processEpoch);
    public void MarkPreDispatchRetry(string idempotencyKey, long processEpoch) =>
        inner.MarkPreDispatchRetry(idempotencyKey, processEpoch);
    public void MarkDispatchAcknowledged(string idempotencyKey, long processEpoch) =>
        inner.MarkDispatchAcknowledged(idempotencyKey, processEpoch);
    public void PrepareCompletion(
        string idempotencyKey,
        long processEpoch,
        WorkerJournalContext journalContext,
        CommandReceipt receipt,
        WorkerJournalWrite terminalWrite) =>
        inner.PrepareCompletion(idempotencyKey, processEpoch, journalContext, receipt, terminalWrite);
    public void FinalizeCompletion(
        string idempotencyKey,
        long processEpoch,
        WorkerJournalAppendReceipt journalReceipt)
    {
        if (!_failed)
        {
            _failed = true;
            throw new IOException("injected crash after durable Journal append");
        }
        inner.FinalizeCompletion(idempotencyKey, processEpoch, journalReceipt);
    }
    public IReadOnlyList<PreparedCommandCompletion> ClaimPreparedCompletions(long processEpoch) =>
        inner.ClaimPreparedCompletions(processEpoch);
    public void ReleasePreparedCompletionClaim(string key, long epoch) => inner.ReleasePreparedCompletionClaim(key, epoch);
    public CommandDrainSnapshot GetDrainSnapshot() => inner.GetDrainSnapshot();
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

internal sealed class BlockingClaimStateStore : ICommandStateStore, IDisposable
{
    private readonly InMemoryCommandStateStore inner = new();
    private readonly ManualResetEventSlim _claimEntered = new(false);
    private readonly ManualResetEventSlim _releaseClaim = new(false);

    public long BeginProcessEpoch() => inner.BeginProcessEpoch();

    public IReadOnlyList<PreparedCommandCompletion> ClaimPreparedCompletions(long processEpoch)
    {
        _claimEntered.Set();
        _releaseClaim.Wait();
        return inner.ClaimPreparedCompletions(processEpoch);
    }

    public void ReleasePreparedCompletionClaim(string key, long epoch) => inner.ReleasePreparedCompletionClaim(key, epoch);
    public CommandDrainSnapshot GetDrainSnapshot() => inner.GetDrainSnapshot();

    public bool WaitUntilClaimed(TimeSpan timeout) => _claimEntered.Wait(timeout);

    public void ReleaseClaim() => _releaseClaim.Set();

    public BeginResult? ReadExisting(string idempotencyKey, string requestSha256, long processEpoch) =>
        inner.ReadExisting(idempotencyKey, requestSha256, processEpoch);
    public BeginResult TryBegin(string idempotencyKey, string requestSha256, long processEpoch) =>
        inner.TryBegin(idempotencyKey, requestSha256, processEpoch);
    public void MarkAccepted(string idempotencyKey, long processEpoch) =>
        inner.MarkAccepted(idempotencyKey, processEpoch);
    public int MarkTransportAttempted(string idempotencyKey, long processEpoch) =>
        inner.MarkTransportAttempted(idempotencyKey, processEpoch);
    public void MarkPreDispatchRetry(string idempotencyKey, long processEpoch) =>
        inner.MarkPreDispatchRetry(idempotencyKey, processEpoch);
    public void MarkDispatchAcknowledged(string idempotencyKey, long processEpoch) =>
        inner.MarkDispatchAcknowledged(idempotencyKey, processEpoch);
    public void PrepareCompletion(
        string idempotencyKey,
        long processEpoch,
        WorkerJournalContext journalContext,
        CommandReceipt receipt,
        WorkerJournalWrite terminalWrite) =>
        inner.PrepareCompletion(idempotencyKey, processEpoch, journalContext, receipt, terminalWrite);
    public void FinalizeCompletion(
        string idempotencyKey,
        long processEpoch,
        WorkerJournalAppendReceipt journalReceipt) =>
        inner.FinalizeCompletion(idempotencyKey, processEpoch, journalReceipt);

    public void Dispose()
    {
        _releaseClaim.Set();
        _claimEntered.Dispose();
        _releaseClaim.Dispose();
    }
}

internal static class WorkerTestHash
{
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

    public static string Sha256(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(StrictUtf8.GetBytes(value)));
}

public sealed class ReviewFixRegressionTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    [Trait("Category", "Unit")]
    [Trait("R1", "true")]
    public async Task Prepared_recovery_has_one_active_owner(bool firstBatch, bool secondBatch, bool seedFailure) =>
        await AssertPreparedOwnerAsync(new InMemoryCommandStateStore(), firstBatch, secondBatch, seedFailure);

    internal static async Task AssertPreparedOwnerAsync(
        ICommandStateStore store, bool firstBatch, bool secondBatch, bool seedFailure)
    {
        var token = TestContext.Current.CancellationToken;
        var command = WorkerTests.Command();
        var owner = new InMemoryJournal();
        var native = LeaseRegressionTransport.ForOutcome("success");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var terminalCalls = 0;
        var journal = new LeaseRegressionJournal(owner, async request =>
        {
            if (request.EntryType != "VERIFIED_SUCCESS") return;
            var call = Interlocked.Increment(ref terminalCalls);
            if (seedFailure && call == 1) throw new IOException("prepared recovery fixture");
            entered.TrySetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(10), token);
        });
        var clock = new LeaseRegressionClock(command.OccurredAt!.Value);
        var processor = new CommandProcessor(native, journal, store, clock);
        if (seedFailure) await Assert.ThrowsAsync<IOException>(() => processor.ProcessAsync(command, token));
        Task first = firstBatch ? processor.ReconcilePreparedCompletionsAsync(token) : processor.ProcessAsync(command, token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), token);
        clock.Now = command.LeaseExpiresAt!.Value.AddTicks(1);
        try
        {
            if (secondBatch) Assert.Equal(0, await processor.ReconcilePreparedCompletionsAsync(token));
            else
            {
                var active = await processor.ProcessAsync(command, token);
                Assert.Equal("UNKNOWN_OUTCOME", active.ResultStatus);
                Assert.True(active.DispatchAcknowledged);
                Assert.Null(active.PostconditionVerified);
                Assert.False(active.RetryAllowed);
                _ = WorkerExchangeCodec.EncodeReceipt(command, active, clock.Now);
            }
            Assert.Equal(seedFailure ? 2 : 1, terminalCalls);
            processor.StopIntake();
            Assert.False(processor.IsDrained);
        }
        finally
        {
            release.TrySetResult();
            await first;
        }
        Assert.Equal(1, native.Calls);
        Assert.Single(owner.States, state => state == "VERIFIED_SUCCESS");
        Assert.Single(owner.Writes.Where(write => write.EntryType == "VERIFIED_SUCCESS").Distinct());
        Assert.True(processor.IsDrained);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("R1", "true")]
    public void Old_epoch_cleanup_cannot_release_new_completion_owner() =>
        AssertCompletionEpochFence(new InMemoryCommandStateStore());

    internal static void AssertCompletionEpochFence(ICommandStateStore store)
    {
        var command = WorkerTests.Command();
        var oldEpoch = store.BeginProcessEpoch();
        var key = command.IdempotencyKey!;
        var hash = command.RequestSha256!;
        store.TryBegin(key, hash, oldEpoch);
        store.MarkAccepted(key, oldEpoch);
        var receipt = new CommandReceipt(command.CommandId, key, "SHADOWED", false, null, null, false, false, "shadow");
        store.PrepareCompletion(key, oldEpoch, WorkerJournalContext.FromCommand(command), receipt,
            WorkerJournalWrite.Create(command, "TERMINAL", "SHADOWED", "shadow"));
        Assert.Empty(store.ClaimPreparedCompletions(oldEpoch));
        var nextEpoch = store.BeginProcessEpoch();
        Assert.Equal("RECONCILE_COMPLETION", store.TryBegin(key, hash, nextEpoch).Status);
        store.ReleasePreparedCompletionClaim(key, oldEpoch);
        Assert.Equal("IN_PROGRESS", store.ReadExisting(key, hash, nextEpoch)!.Status);
        Assert.Empty(store.ClaimPreparedCompletions(nextEpoch));
        store.ReleasePreparedCompletionClaim(key, nextEpoch);
        Assert.Single(store.ClaimPreparedCompletions(nextEpoch));
        store.ReleasePreparedCompletionClaim(key, nextEpoch);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Category", "Unit")]
    [Trait("R1", "true")]
    public async Task Batch_failure_or_cancellation_releases_unprocessed_claims(bool failAppend) =>
        await AssertBatchReleaseAsync(new InMemoryCommandStateStore(), failAppend);

    internal static async Task AssertBatchReleaseAsync(ICommandStateStore store, bool failAppend)
    {
        var token = TestContext.Current.CancellationToken;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        var command = WorkerTests.Command();
        var other = WorkerTests.WithHash(command with { IdempotencyKey = "idem_" + new string('f', 64) });
        var seed = true;
        var interrupt = true;
        var owner = new InMemoryJournal();
        var native = LeaseRegressionTransport.ForOutcome("success");
        var journal = new LeaseRegressionJournal(owner, request =>
        {
            if (request.EntryType != "VERIFIED_SUCCESS") return Task.CompletedTask;
            if (seed) throw new IOException("seed prepared");
            if (interrupt)
            {
                interrupt = false;
                if (failAppend) throw new IOException("batch append interrupted");
                cancellation.Cancel();
            }
            return Task.CompletedTask;
        });
        var processor = new CommandProcessor(native, journal, store, new LeaseRegressionClock(command.OccurredAt!.Value));
        await Assert.ThrowsAsync<IOException>(() => processor.ProcessAsync(command, token));
        await Assert.ThrowsAsync<IOException>(() => processor.ProcessAsync(other, token));
        seed = false;
        if (failAppend)
            await Assert.ThrowsAsync<IOException>(() => processor.ReconcilePreparedCompletionsAsync(cancellation.Token));
        else
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => processor.ReconcilePreparedCompletionsAsync(cancellation.Token));
        Assert.Equal(failAppend ? 2 : 1, await processor.ReconcilePreparedCompletionsAsync(token));
        Assert.Equal(0, await processor.ReconcilePreparedCompletionsAsync(token));
        Assert.Equal(2, native.Calls);
        Assert.Equal(2, owner.States.Count(state => state == "VERIFIED_SUCCESS"));
        Assert.Equal(2, owner.Writes.Where(write => write.EntryType == "VERIFIED_SUCCESS").Distinct().Count());
        processor.StopIntake();
        Assert.True(processor.IsDrained);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("R1", "true")]
    public async Task Failed_finalize_is_retryable_without_new_epoch() =>
        await AssertFinalizeRetryAsync(new InMemoryCommandStateStore());

    internal static async Task AssertFinalizeRetryAsync(ICommandStateStore store)
    {
        var command = WorkerTests.Command();
        var native = LeaseRegressionTransport.ForOutcome("success");
        var journal = new InMemoryJournal();
        var processor = new CommandProcessor(native, journal, new FailOnceFinalizeStore(store),
            new LeaseRegressionClock(command.OccurredAt!.Value));
        var token = TestContext.Current.CancellationToken;
        await Assert.ThrowsAsync<IOException>(() => processor.ProcessAsync(command, token));
        var completed = await processor.ProcessAsync(command, token);
        Assert.Equal("VERIFIED_SUCCESS", completed.ResultStatus);
        Assert.Equal(1, native.Calls);
        Assert.Single(journal.States, state => state == "VERIFIED_SUCCESS");
        Assert.Single(journal.Writes.Where(write => write.EntryType == "VERIFIED_SUCCESS").Distinct());
        processor.StopIntake();
        Assert.True(processor.IsDrained);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Category", "Unit")]
    [Trait("R1", "true")]
    public async Task Unknown_store_status_fails_before_execution_mutations(bool duringRead)
    {
        var command = WorkerTests.Command();
        var store = new LeaseRegressionStore(new InMemoryCommandStateStore(), beginOverride: (result, read) =>
            read == duringRead ? new BeginResult("UNRECOGNIZED", CommandExecutionPhase.Reserved, false, 0, null) : result);
        var native = LeaseRegressionTransport.ForOutcome("success");
        var journal = new InMemoryJournal();
        var processor = new CommandProcessor(native, journal, store, new LeaseRegressionClock(command.OccurredAt!.Value));
        var before = store.Mutations;
        await Assert.ThrowsAsync<InvalidDataException>(() => processor.ProcessAsync(command, TestContext.Current.CancellationToken));
        Assert.Equal(before + (duringRead ? 0 : 1), store.Mutations);
        Assert.Equal(duringRead ? 0 : 1, store.TryBegins);
        Assert.Equal(0, native.Calls);
        Assert.Empty(journal.Writes);
    }

    [Fact]
    [Trait("Category", "Contract")]
    [Trait("R1", "true")]
    public void Command_wire_enforces_byte_limit_and_strict_utf8()
    {
        var command = WorkerTests.Command();
        var bytes = JsonSerializer.SerializeToUtf8Bytes(command);
        var exact = new byte[32768];
        Array.Fill(exact, (byte)' ');
        bytes.CopyTo(exact, 0);
        Assert.Equal(command, WorkerExchangeCodec.DecodeCommand(exact));
        Assert.Throws<InvalidDataException>(() => WorkerExchangeCodec.DecodeCommand([.. exact, (byte)' ']));
        var invalid = bytes.ToArray();
        invalid[Array.IndexOf(invalid, (byte)'1')] = 0xff;
        Assert.Throws<InvalidDataException>(() => WorkerExchangeCodec.DecodeCommand(invalid));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Category", "Contract")]
    [Trait("R1", "true")]
    public async Task Journal_duplicate_flag_never_substitutes_for_identity_or_payload_proof(bool duplicate)
    {
        var command = WorkerTests.Command();
        var request = WorkerJournalAppendRequest.Create(WorkerJournalContext.FromCommand(command),
            WorkerJournalWrite.Create(command, "TERMINAL", "VERIFIED_SUCCESS", "verified"));
        var receipt = (await new InMemoryJournal().AppendAsync(request, TestContext.Current.CancellationToken)) with { Duplicate = duplicate };
        WorkerJournalReceiptValidator.Validate(request, receipt);
        Assert.Throws<InvalidDataException>(() => WorkerJournalReceiptValidator.Validate(request, receipt with { EntryId = "other" }));
        Assert.Throws<InvalidDataException>(() => WorkerJournalReceiptValidator.Validate(request, receipt with { PayloadSha256 = new string('0', 64) }));
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("null")]
    [InlineData("oversized")]
    [InlineData("invalid-unicode")]
    [InlineData("limit")]
    [InlineData("normal")]
    [Trait("Category", "Unit")]
    [Trait("R1", "true")]
    public async Task Native_detail_cannot_prevent_terminal_truth_or_receipt_encoding(string kind)
    {
        var detail = kind switch
        {
            "empty" => "", "null" => null!, "oversized" => new string('x', 4097),
            "invalid-unicode" => "\ud800", "limit" => new string('x', 4096), _ => "  verified 😀  "
        };
        var command = WorkerTests.Command();
        var native = new LeaseRegressionTransport((_, _) => Task.FromResult(
            new NativeDispatchResult(true, NativeStatus.Success, true, detail)));
        var store = new InMemoryCommandStateStore();
        var processor = new CommandProcessor(native, new InMemoryJournal(), store,
            new LeaseRegressionClock(command.OccurredAt!.Value));
        var receipt = await processor.ProcessAsync(command, TestContext.Current.CancellationToken);
        Assert.Equal("VERIFIED_SUCCESS", receipt.ResultStatus);
        Assert.True(receipt.DispatchAcknowledged);
        Assert.True(store.GetDrainSnapshot().IsDrained);
        Assert.InRange(receipt.Detail.Length, 1, 4096);
        if (kind is "normal" or "limit") Assert.Equal(detail, receipt.Detail);
        using var wire = JsonDocument.Parse(WorkerExchangeCodec.EncodeReceipt(command, receipt, command.OccurredAt.Value));
        Assert.Equal(receipt.Detail, wire.RootElement.GetProperty("detail").GetString());
        Assert.Equal(receipt with { Duplicate = true, RetryAllowed = false }, await processor.ProcessAsync(command, TestContext.Current.CancellationToken));
        Assert.Equal(1, native.Calls);
    }

    [Fact]
    [Trait("Category", "Contract")]
    [Trait("R1", "true")]
    public void Public_receipt_codec_rejects_wrong_source_hash_and_projects_legacy_empty_detail()
    {
        var source = WorkerTests.Command();
        var receipt = new CommandReceipt(source.CommandId, source.IdempotencyKey, "VERIFIED_SUCCESS",
            true, NativeStatus.Success, true, false, false, "verified");
        Assert.Throws<InvalidDataException>(() => WorkerExchangeCodec.EncodeReceipt(
            source with { RequestSha256 = new string('0', 64) }, receipt, source.OccurredAt!.Value));
        var legacy = receipt with { Detail = "" };
        using var wire = JsonDocument.Parse(WorkerExchangeCodec.EncodeReceipt(
            source, legacy, source.LeaseExpiresAt!.Value.AddDays(1)));
        Assert.False(string.IsNullOrEmpty(wire.RootElement.GetProperty("detail").GetString()));
        Assert.Equal("", legacy.Detail);
    }
}

public sealed class LeaseRegressionTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [Trait("Category", "Unit")]
    public async Task Expired_new_command_never_reserves_or_dispatches(int ticks)
    {
        var command = WorkerTests.Command();
        var clock = new LeaseRegressionClock(command.LeaseExpiresAt!.Value.AddTicks(ticks));
        var store = new LeaseRegressionStore(new InMemoryCommandStateStore());
        var journal = new InMemoryJournal();
        var native = LeaseRegressionTransport.ForOutcome("success");
        var processor = new CommandProcessor(native, journal, store, clock);
        var writes = store.Mutations;
        var result = await processor.ProcessAsync(command, TestContext.Current.CancellationToken);
        Assert.Equal("REJECTED", result.ResultStatus);
        Assert.Equal(0, native.Calls);
        Assert.Equal(writes, store.Mutations);
        Assert.Empty(journal.Writes);
        Assert.True(store.GetDrainSnapshot().IsDrained);
    }

    [Theory]
    [InlineData("ACCEPTED", 0)]
    [InlineData("ACCEPTED", 1)]
    [InlineData("MarkAccepted", 0)]
    [InlineData("MarkAccepted", 1)]
    [InlineData("MarkTransportAttempted", 0)]
    [InlineData("MarkTransportAttempted", 1)]
    [Trait("Category", "Unit")]
    public async Task Persistence_wait_crossing_expiry_blocks_first_native(string boundary, int ticks)
    {
        var command = WorkerTests.Command();
        var clock = new LeaseRegressionClock(command.OccurredAt!.Value);
        void Advance(string point)
        {
            if (point == boundary) clock.Now = command.LeaseExpiresAt!.Value.AddTicks(ticks);
        }
        var store = new LeaseRegressionStore(new InMemoryCommandStateStore(), Advance);
        var journal = new LeaseRegressionJournal(new InMemoryJournal(), request =>
        {
            Advance(request.EntryType);
            return Task.CompletedTask;
        });
        var native = LeaseRegressionTransport.ForOutcome("success");
        var result = await new CommandProcessor(native, journal, store, clock)
            .ProcessAsync(command, TestContext.Current.CancellationToken);
        Assert.Equal(0, native.Calls);
        Assert.Equal("REJECTED", result.ResultStatus);
        Assert.False(result.RetryAllowed);
        Assert.Equal(boundary == "MarkTransportAttempted" ? 1 : 0,
            store.ReadExisting(command.IdempotencyKey!, command.RequestSha256!, store.Epoch)!.DispatchAttemptCount);
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 1)]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    [Trait("Category", "Unit")]
    public async Task Clearly_not_submitted_retry_crossing_expiry_never_calls_native_again(bool exception, int ticks)
    {
        var command = WorkerTests.Command();
        var clock = new LeaseRegressionClock(command.OccurredAt!.Value);
        var native = new LeaseRegressionTransport((_, _) =>
        {
            clock.Now = command.LeaseExpiresAt!.Value.AddTicks(ticks);
            return exception
                ? Task.FromException<NativeDispatchResult>(new TransportDispatchException("not submitted", false))
                : Task.FromResult(new NativeDispatchResult(false, NativeStatus.Failed, false, "not submitted"));
        });
        var store = new LeaseRegressionStore(new InMemoryCommandStateStore());
        var result = await new CommandProcessor(native, new InMemoryJournal(), store, clock)
            .ProcessAsync(command, TestContext.Current.CancellationToken);
        Assert.Equal(1, native.Calls);
        Assert.Equal("REJECTED", result.ResultStatus);
        Assert.False(result.RetryAllowed);
        Assert.Equal(1, store.ReadExisting(command.IdempotencyKey!, command.RequestSha256!, store.Epoch)!.DispatchAttemptCount);
    }

    [Theory]
    [InlineData("non-utc")]
    [InlineData("rollback")]
    [Trait("Category", "Unit")]
    public async Task Untrusted_clock_blocks_native_and_remains_fail_closed(string fault)
    {
        var command = WorkerTests.Command();
        var clock = new LeaseRegressionClock(command.OccurredAt!.Value);
        if (fault == "non-utc") clock.Now = clock.Now.ToOffset(TimeSpan.FromHours(1));
        var journal = new LeaseRegressionJournal(new InMemoryJournal(), _ =>
        {
            if (fault == "rollback") clock.Now = command.OccurredAt!.Value.AddTicks(-1);
            return Task.CompletedTask;
        });
        var store = new LeaseRegressionStore(new InMemoryCommandStateStore());
        var native = LeaseRegressionTransport.ForOutcome("success");
        var processor = new CommandProcessor(native, journal, store, clock);
        var result = await processor.ProcessAsync(command, TestContext.Current.CancellationToken);
        Assert.Equal(0, native.Calls);
        Assert.Equal("REJECTED", result.ResultStatus);
        clock.Now = command.OccurredAt!.Value.AddSeconds(1);
        var other = WorkerTests.WithHash(command with { IdempotencyKey = "idem_" + new string('f', 64) });
        var mutations = store.Mutations;
        Assert.Equal("REJECTED", (await processor.ProcessAsync(other, TestContext.Current.CancellationToken)).ResultStatus);
        Assert.Equal(mutations, store.Mutations);
        Assert.Equal(0, native.Calls);
    }

    [Theory]
    [InlineData("success")]
    [InlineData("failed")]
    [InlineData("shadow")]
    [InlineData("unknown")]
    [InlineData("uncertain")]
    [Trait("Category", "Unit")]
    public async Task Process_created_terminal_receipt_replays_after_expiry_without_writes(string outcome)
    {
        var command = WorkerTests.WithHash(WorkerTests.Command() with { Shadow = outcome == "shadow" });
        var clock = new LeaseRegressionClock(command.OccurredAt!.Value);
        var store = new LeaseRegressionStore(new InMemoryCommandStateStore());
        var journal = new InMemoryJournal();
        var native = LeaseRegressionTransport.ForOutcome(outcome);
        var processor = new CommandProcessor(native, journal, store, clock);
        var first = await processor.ProcessAsync(command, TestContext.Current.CancellationToken);
        Assert.Equal(LeaseRegressionTransport.ExpectedStatus(outcome), first.ResultStatus);
        clock.Now = command.LeaseExpiresAt!.Value;
        var writes = store.Mutations;
        var journalWrites = journal.Writes.Count;
        var calls = native.Calls;
        var replay = await processor.ProcessAsync(command, TestContext.Current.CancellationToken);
        Assert.Equal(first with { Duplicate = true, RetryAllowed = false }, replay);
        Assert.Equal(writes, store.Mutations);
        Assert.Equal(journalWrites, journal.Writes.Count);
        Assert.Equal(calls, native.Calls);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Expired_mismatched_hash_scope_or_major_never_leaks_completed_receipt()
    {
        var command = WorkerTests.Command();
        var clock = new LeaseRegressionClock(command.OccurredAt!.Value);
        var store = new LeaseRegressionStore(new InMemoryCommandStateStore());
        var journal = new InMemoryJournal();
        var native = LeaseRegressionTransport.ForOutcome("success");
        var processor = new CommandProcessor(native, journal, store, clock);
        var first = await processor.ProcessAsync(command, TestContext.Current.CancellationToken);
        Assert.Equal("VERIFIED_SUCCESS", first.ResultStatus);
        clock.Now = command.LeaseExpiresAt!.Value.AddTicks(1);
        var writes = store.Mutations;
        var journalWrites = journal.Writes.Count;
        foreach (var (invalid, expectedStatus, expectedReads) in new[]
        {
            (command with { RequestSha256 = new string('0', 64) }, "REJECTED", 0),
            (WorkerTests.WithHash(command with { SoulId = "soul_" + new string('f', 64) }), "QUARANTINED", 1),
            (command with { SoulId = "unknown" }, "REJECTED", 0),
            (WorkerTests.WithHash(command with { SchemaVersion = "2.0" }), "REJECTED", 0)
        })
        {
            var reads = store.Reads;
            var result = await processor.ProcessAsync(invalid, TestContext.Current.CancellationToken);
            Assert.Equal(expectedStatus, result.ResultStatus);
            Assert.Equal(reads + expectedReads, store.Reads);
            Assert.False(result.Duplicate);
            Assert.False(result.DispatchAcknowledged);
            Assert.Null(result.NativeStatus);
            Assert.Null(result.PostconditionVerified);
            Assert.False(result.RetryAllowed);
            Assert.NotEqual(first.Detail, result.Detail);
        }
        Assert.Equal(writes, store.Mutations);
        Assert.Equal(journalWrites, journal.Writes.Count);
        Assert.Equal(1, native.Calls);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Concurrent_duplicate_does_not_reserve_and_accepted_wait_cannot_outlive_lease()
    {
        var command = WorkerTests.Command();
        var clock = new LeaseRegressionClock(command.OccurredAt!.Value);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var journal = new LeaseRegressionJournal(new InMemoryJournal(), async request =>
        {
            if (request.EntryType != "ACCEPTED") return;
            entered.SetResult();
            await release.Task;
        });
        var store = new LeaseRegressionStore(new InMemoryCommandStateStore());
        var native = LeaseRegressionTransport.ForOutcome("success");
        var processor = new CommandProcessor(native, journal, store, clock);
        var pending = processor.ProcessAsync(command, TestContext.Current.CancellationToken);
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        try
        {
            var writes = store.Mutations;
            var appends = journal.AppendCalls;
            var duplicate = await processor.ProcessAsync(command, TestContext.Current.CancellationToken);
            Assert.Equal("IN_PROGRESS", duplicate.ResultStatus);
            Assert.Equal(writes, store.Mutations);
            Assert.Equal(appends, journal.AppendCalls);
        }
        finally
        {
            clock.Now = command.LeaseExpiresAt!.Value;
            release.SetResult();
            await pending;
        }
        Assert.Equal("REJECTED", (await pending).ResultStatus);
        Assert.Equal(0, native.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Category", "Unit")]
    public async Task Lease_expiring_after_submission_preserves_ack_or_uncertainty(bool uncertain)
    {
        var command = WorkerTests.Command();
        var clock = new LeaseRegressionClock(command.OccurredAt!.Value);
        var native = new LeaseRegressionTransport((_, _) =>
        {
            clock.Now = command.LeaseExpiresAt!.Value.AddTicks(1);
            return uncertain
                ? Task.FromException<NativeDispatchResult>(new TransportDispatchException("ack unavailable", null))
                : Task.FromResult(new NativeDispatchResult(true, NativeStatus.Success, true, "late verified ack"));
        });
        var result = await new CommandProcessor(native, new InMemoryJournal(), new InMemoryCommandStateStore(), clock)
            .ProcessAsync(command, TestContext.Current.CancellationToken);
        Assert.Equal(1, native.Calls);
        Assert.Equal(uncertain ? "UNKNOWN_OUTCOME" : "VERIFIED_SUCCESS", result.ResultStatus);
        if (uncertain) Assert.Null(result.DispatchAcknowledged);
        else Assert.True(result.DispatchAcknowledged);
        Assert.False(result.RetryAllowed);
    }
}

internal sealed class LeaseRegressionClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now;
}

internal sealed class LeaseRegressionTransport(Func<WorkerCommand, CancellationToken, Task<NativeDispatchResult>> dispatch) : INativeTransport
{
    private int _calls;
    public int Calls => Volatile.Read(ref _calls);
    public Task<NativeDispatchResult> DispatchAsync(WorkerCommand command, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _calls);
        return dispatch(command, cancellationToken);
    }
    public static string ExpectedStatus(string outcome) => outcome switch
    {
        "shadow" => "SHADOWED", "failed" => "FAILED",
        "unknown" or "uncertain" => "UNKNOWN_OUTCOME", _ => "VERIFIED_SUCCESS"
    };
    public static LeaseRegressionTransport ForOutcome(string outcome) => new((_, _) =>
        outcome == "uncertain"
            ? Task.FromException<NativeDispatchResult>(new TransportDispatchException("unknown submission", null))
            : Task.FromResult(new NativeDispatchResult(true,
                outcome == "failed" ? NativeStatus.Failed : outcome == "unknown" ? NativeStatus.UnknownOutcome : NativeStatus.Success,
                outcome is not ("failed" or "unknown"), "process-created " + outcome)));
}

internal sealed class LeaseRegressionJournal(IWorkerJournal inner, Func<WorkerJournalAppendRequest, Task> after) : IWorkerJournal
{
    public int AppendCalls { get; private set; }
    public async Task<WorkerJournalAppendReceipt> AppendAsync(WorkerJournalAppendRequest request, CancellationToken cancellationToken)
    {
        AppendCalls++;
        var receipt = await inner.AppendAsync(request, cancellationToken);
        await after(request);
        return receipt;
    }
}

internal sealed class LeaseRegressionStore(ICommandStateStore inner, Action<string>? after = null,
    Func<BeginResult?, bool, BeginResult?>? beginOverride = null) : ICommandStateStore
{
    public int Mutations { get; private set; }
    public int Reads { get; private set; }
    public int TryBegins { get; private set; }
    public long Epoch { get; private set; }
    public long BeginProcessEpoch() { Mutations++; return Epoch = inner.BeginProcessEpoch(); }
    public BeginResult? ReadExisting(string key, string hash, long epoch)
    {
        Reads++;
        var result = inner.ReadExisting(key, hash, epoch);
        return beginOverride is null ? result : beginOverride(result, true);
    }
    public BeginResult TryBegin(string key, string hash, long epoch)
    {
        Mutations++;
        TryBegins++;
        var result = inner.TryBegin(key, hash, epoch);
        return beginOverride is null ? result : beginOverride(result, false)!;
    }
    public void MarkAccepted(string key, long epoch) { Mutations++; inner.MarkAccepted(key, epoch); after?.Invoke("MarkAccepted"); }
    public int MarkTransportAttempted(string key, long epoch)
    {
        Mutations++;
        var attempt = inner.MarkTransportAttempted(key, epoch);
        after?.Invoke("MarkTransportAttempted");
        return attempt;
    }
    public void MarkPreDispatchRetry(string key, long epoch) { Mutations++; inner.MarkPreDispatchRetry(key, epoch); }
    public void MarkDispatchAcknowledged(string key, long epoch) { Mutations++; inner.MarkDispatchAcknowledged(key, epoch); after?.Invoke("MarkDispatchAcknowledged"); }
    public void PrepareCompletion(string key, long epoch, WorkerJournalContext context, CommandReceipt receipt, WorkerJournalWrite write)
    { Mutations++; inner.PrepareCompletion(key, epoch, context, receipt, write); }
    public void FinalizeCompletion(string key, long epoch, WorkerJournalAppendReceipt receipt)
    { Mutations++; inner.FinalizeCompletion(key, epoch, receipt); }
    public IReadOnlyList<PreparedCommandCompletion> ClaimPreparedCompletions(long epoch)
    { Mutations++; return inner.ClaimPreparedCompletions(epoch); }
    public void ReleasePreparedCompletionClaim(string key, long epoch) => inner.ReleasePreparedCompletionClaim(key, epoch);
    public CommandDrainSnapshot GetDrainSnapshot() => inner.GetDrainSnapshot();
}
