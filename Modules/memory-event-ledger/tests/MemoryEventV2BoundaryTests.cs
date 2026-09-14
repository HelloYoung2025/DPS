using System.Text;
using System.Text.Json;
using Dps.MemoryEventLedger.Contracts;
using Xunit;

namespace Dps.MemoryEventLedger.Tests;

public sealed class MemoryEventV2BoundaryTests
{
    [Theory]
    [InlineData("2")]
    [InlineData("2.0")]
    [InlineData("2.1")]
    [InlineData("2.0.1")]
    [InlineData("2.00.0")]
    [Trait("Category", "Contract")]
    public void AppendRequestRejectsVersionsOtherThanExactSchemaConstant(string version)
    {
        var request = AppendRequest() with { SchemaVersion = version };
        Assert.Throws<NotSupportedException>(request.Validate);
    }

    [Theory]
    [InlineData("2")]
    [InlineData("2.0")]
    [InlineData("2.1")]
    [InlineData("2.0.1")]
    [InlineData("2.00.0")]
    [Trait("Category", "Contract")]
    public void EventRejectsVersionsOtherThanExactSchemaConstant(string version)
    {
        var memoryEvent = Event(0) with { SchemaVersion = version };
        Assert.Throws<NotSupportedException>(memoryEvent.Validate);
    }

    [Theory]
    [InlineData("2")]
    [InlineData("2.0")]
    [InlineData("2.1")]
    [InlineData("2.0.1")]
    [InlineData("2.00.0")]
    [Trait("Category", "Contract")]
    public void OutboxRejectsVersionsOtherThanExactSchemaConstant(string version)
    {
        var outbox = Outbox() with { SchemaVersion = version };
        Assert.Throws<NotSupportedException>(outbox.Validate);
    }

    [Theory]
    [InlineData("2")]
    [InlineData("2.0")]
    [InlineData("2.1")]
    [InlineData("2.0.1")]
    [InlineData("2.00.0")]
    [Trait("Category", "Contract")]
    public void ExactDeserializerRejectsNonSchemaVersions(string version)
    {
        var canonical = MemoryEventCanonicalizerV2.Serialize(Event(0));
        var raw = Encoding.UTF8.GetBytes(canonical.Replace(
            "\"schema_version\":\"2.0.0\"", $"\"schema_version\":\"{version}\"", StringComparison.Ordinal));
        Assert.Throws<NotSupportedException>(() => MemoryEventCanonicalizerV2.DeserializeExact(raw));
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void ExactSchemaVersionIsAcceptedByEveryV2Dto()
    {
        (AppendRequest() with { SchemaVersion = "2.0.0" }).Validate();
        (Event(0) with { SchemaVersion = "2.0.0" }).Validate();
        (Outbox() with { SchemaVersion = "2.0.0" }).Validate();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(29)]
    [InlineData(30)]
    [InlineData(31)]
    [InlineData(32)]
    [Trait("Category", "Contract")]
    public void CanonicalEventRoundTripsThroughTheSchemaSignalLimit(int signalCount)
    {
        var canonical = MemoryEventCanonicalizerV2.Serialize(Event(signalCount));
        var parsed = MemoryEventCanonicalizerV2.DeserializeExact(Encoding.UTF8.GetBytes(canonical));
        Assert.Equal(signalCount, parsed.Observation.InterestSignals.Count);
        Assert.Equal(canonical, MemoryEventCanonicalizerV2.Serialize(parsed));
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void ThirtyThreeSignalsAreRejectedByDtosAndExactDeserializer()
    {
        var signals = Signals(33);
        var memoryEvent = Event(32);
        var oversized = memoryEvent with { Observation = memoryEvent.Observation with { InterestSignals = signals } };
        Assert.Throws<ArgumentOutOfRangeException>(oversized.Validate);
        Assert.Throws<ArgumentOutOfRangeException>((AppendRequest() with { InterestSignals = signals }).Validate);
        Assert.Throws<JsonException>(() => MemoryEventCanonicalizerV2.DeserializeExact(JsonSerializer.SerializeToUtf8Bytes(oversized)));
    }

    [Fact]
    [Trait("Category", "Contract")]
    public void ExtraOrDuplicatePropertiesRemainRejectedAtTheSignalLimit()
    {
        var canonical = MemoryEventCanonicalizerV2.Serialize(Event(32));
        foreach (var extraProperty in new[] { "\"extra\":0,", "\"schema_version\":\"2.0.0\"," })
        {
            var raw = Encoding.UTF8.GetBytes("{" + extraProperty + canonical[1..]);
            Assert.Throws<JsonException>(() => MemoryEventCanonicalizerV2.DeserializeExact(raw));
        }
    }

    // These public DTO fixtures exercise shape validation only; they never authorize or append an event.
    private static MemoryEventV2 Event(int signalCount)
    {
        var request = V2TestData.Request();
        var signals = Signals(signalCount);
        var digest = new string('f', 64);
        return new MemoryEventV2("2.0.0", "memory.event/v2", "memory-event-ledger", request.EventId,
            request.SoulId, request.DeviceBindingId, request.PlatformAccountId, request.TraceId,
            request.IdempotencyKey, request.OccurredAt, "personal", "content.observed",
            new MemoryObservationV2(digest, MemorySignalCanonicalizerV2.ComputeSha256(signals), digest,
                request.EventId, request.EventId, signals),
            new IdentityAuthorityAuditV2(digest, 1, "soul-registry", "memory-event-ledger", "soul-resolution-current",
                "test-identity", 1, 0, V2TestData.Now, V2TestData.Now.AddMinutes(1)),
            new ResultAuthorityAuditV2("executor-gateway", "memory-event-ledger", "verified-observation-receipt",
                "test-result", 1, 0, V2TestData.Now, V2TestData.Now.AddMinutes(1)));
    }

    private static InterestSignalV2[] Signals(int count) => Enumerable.Range(0, count)
        .Select(static index => new InterestSignalV2($"topic-{index:D2}", 0.5m, new string('f', 64))).ToArray();

    private static MemoryAppendRequestV2 AppendRequest()
    {
        var request = V2TestData.Request();
        return new MemoryAppendRequestV2("2.0.0", "memory.append.request/v2", "control-plane-host", request.EventId,
            request.SoulId, request.DeviceBindingId, request.PlatformAccountId, request.TraceId,
            request.IdempotencyKey, request.OccurredAt, "personal", "AQ==", []);
    }

    private static MemoryOutboxV2 Outbox()
    {
        var request = V2TestData.Request();
        var digest = new string('f', 64);
        return new MemoryOutboxV2("2.0.0", "memory.outbox/v2", "memory-event-ledger", request.EventId, request.EventId,
            request.SoulId, request.DeviceBindingId, request.PlatformAccountId, request.TraceId,
            request.IdempotencyKey, request.OccurredAt, "personal", "memory.event/v2", digest, 1, digest, digest);
    }
}
