using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dps.SoulRegistry.Contracts;
using Xunit;

namespace Dps.MemoryEventLedger.Tests;

public sealed class SoulResolvedCanonicalDifferentialTests
{
    [Theory, Trait("Category", "Contract")]
    [InlineData(0, "alias-v1")]
    [InlineData(1, "Legacy.KEY-1")]
    [InlineData(1200000, "A")]
    [InlineData(1234567, "legacy_key")]
    [InlineData(9999999, "last-key")]
    public void OwnerAuditMatchesActualUnchangedLedgerOracle(long ticks, string aliasKey)
    {
        var value = new SoulResolved("soul_" + new string('1', 64), "db_" + new string('2', 32), "pa_" + new string('3', 32), "trace_" + new string('4', 32), "idem_" + new string('5', 64), new DateTimeOffset(2026, 9, 15, 0, 0, 0, TimeSpan.Zero).AddTicks(ticks), "platform_id", new string('8', 64), aliasKey);
        // Existing friend assembly calls the actual unchanged production method, not a copied oracle.
        var legacy = SoulResolvedCanonicalizerV1.Serialize(value);
        var owner = SoulResolvedCanonicalV1.SerializeLedgerAudit(value);
        Assert.Equal(legacy, owner);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(legacy)), SoulResolvedCanonicalV1.ComputeLedgerAuditSha256(value));
        var wire = SoulResolvedCanonicalV1.SerializeWire(value);
        Assert.Equal(value, SoulResolvedCanonicalV1.FromUtf8Json(wire));
        Assert.NotEqual(Encoding.UTF8.GetString(legacy), Encoding.UTF8.GetString(wire));
        using var document = JsonDocument.Parse(wire);
        Assert.EndsWith("Z", document.RootElement.GetProperty("occurred_at").GetString());
        Assert.Contains("\\u002B00:00", Encoding.UTF8.GetString(legacy), StringComparison.Ordinal);
        if (ticks == 0)
        {
            using var stream = typeof(SoulResolvedCanonicalDifferentialTests).Assembly.GetManifestResourceStream("soul.identity.v1.corpus.json") ?? throw new InvalidOperationException("Missing owner corpus.");
            using var corpus = JsonDocument.Parse(stream);
            var model = corpus.RootElement.GetProperty("legacy_audit_model");
            Assert.Equal(Convert.FromBase64String(model.GetProperty("base64").GetString()!), legacy);
            Assert.Equal(model.GetProperty("sha256").GetString(), Convert.ToHexStringLower(SHA256.HashData(legacy)));
        }
    }

    [Theory, Trait("Category", "Contract")]
    [InlineData(1, 1, 1)]
    [InlineData(2000, 2, 29)]
    [InlineData(9999, 12, 31)]
    public void LegacyCalendarEndpointsRetainAuditAndLegalWire(int year, int month, int day)
    {
        var value = new SoulResolved("soul_" + new string('a', 64), "db_" + new string('b', 32), "pa_" + new string('c', 32), "trace_" + new string('d', 32), "idem_" + new string('e', 64), new DateTimeOffset(year, month, day, 0, 0, 0, TimeSpan.Zero), "email", new string('f', 64), "Legacy.KEY-1");
        Assert.Equal(SoulResolvedCanonicalizerV1.Serialize(value), SoulResolvedCanonicalV1.SerializeLedgerAudit(value));
        Assert.Equal(value, SoulResolvedCanonicalV1.FromUtf8Json(SoulResolvedCanonicalV1.SerializeWire(value)));
    }
}
