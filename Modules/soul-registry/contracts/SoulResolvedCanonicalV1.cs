using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Dps.SoulRegistry.Contracts;

public static class SoulResolvedCanonicalV1
{
    public const string LedgerAuditId = "soul.resolved.ledger-audit/v1";
    private static readonly string[] Fields = ["schema_version", "contract_id", "producer_module", "soul_id", "device_binding_id", "platform_account_id", "trace_id", "idempotency_key", "occurred_at", "privacy_class", "alias_kind", "alias_digest", "alias_key_id"];

    public static byte[] SerializeLedgerAudit(SoulResolved value) => Serialize(value, false);
    public static string ComputeLedgerAuditSha256(SoulResolved value) => SoulIdentityWire.Hash(SerializeLedgerAudit(value));
    public static byte[] SerializeWire(SoulResolved value) => Serialize(value, true);

    private static byte[] Serialize(SoulResolved value, bool wire)
    {
        ArgumentNullException.ThrowIfNull(value);
        value.Validate();
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("schema_version", value.SchemaVersion); writer.WriteString("contract_id", value.ContractId);
            writer.WriteString("producer_module", value.ProducerModule); writer.WriteString("soul_id", value.SoulId);
            writer.WriteString("device_binding_id", value.DeviceBindingId); writer.WriteString("platform_account_id", value.PlatformAccountId);
            writer.WriteString("trace_id", value.TraceId); writer.WriteString("idempotency_key", value.IdempotencyKey);
            writer.WriteString("occurred_at", wire ? WireTime(value.OccurredAt) : value.OccurredAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            writer.WriteString("privacy_class", value.PrivacyClass); writer.WriteString("alias_kind", value.AliasKind);
            writer.WriteString("alias_digest", value.AliasDigest); writer.WriteString("alias_key_id", value.AliasKeyId); writer.WriteEndObject();
        }
        return stream.ToArray();
    }

    private static string WireTime(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        var fraction = utc.ToString("fffffff", CultureInfo.InvariantCulture).TrimEnd('0');
        return utc.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture) + (fraction.Length == 0 ? "" : "." + fraction) + "Z";
    }

    public static SoulResolved FromUtf8Json(ReadOnlySpan<byte> bytes) => FromWireElement(SoulIdentityWire.Parse(bytes, Fields));

    internal static SoulResolved FromWireElement(JsonElement root)
    {
        SoulIdentityWire.CheckObject(root, Fields);
        SoulIdentityWire.Exact(root, "schema_version", "1.0.0");
        SoulIdentityWire.Exact(root, "contract_id", "soul.resolved/v1");
        SoulIdentityWire.Exact(root, "producer_module", "soul-registry");
        SoulIdentityWire.Exact(root, "privacy_class", "pseudonymous");
        var rawTime = SoulIdentityWire.Text(root, "occurred_at");
        SoulIdentityWire.Pattern(rawTime, @"[0-9]{4}-(?:0[1-9]|1[0-2])-(?:0[1-9]|[12][0-9]|3[01])T(?:[01][0-9]|2[0-3]):[0-5][0-9]:[0-5][0-9](?:\.[0-9]{0,6}[1-9])?Z");
        if (!DateTimeOffset.TryParse(rawTime, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var time) || WireTime(time) != rawTime)
            throw new ArgumentException("Invalid legacy wire timestamp.");
        var result = new SoulResolved(SoulIdentityWire.Text(root, "soul_id"), SoulIdentityWire.Text(root, "device_binding_id"), SoulIdentityWire.Text(root, "platform_account_id"), SoulIdentityWire.Text(root, "trace_id"), SoulIdentityWire.Text(root, "idempotency_key"), time, SoulIdentityWire.Text(root, "alias_kind"), SoulIdentityWire.Text(root, "alias_digest"), SoulIdentityWire.Text(root, "alias_key_id"));
        result.Validate();
        return result;
    }
}
