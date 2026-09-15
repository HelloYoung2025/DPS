using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Dps.SoulRegistry.Contracts;

// Shape and commitment checks never confer identity authority or a current lease.
internal static class SoulIdentityWire
{
    internal static readonly UTF8Encoding Utf8 = new(false, true);
    internal const string UtcFormat = "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'";
    internal const string Algorithm = "ES256-P1363-SHA256";

    internal static JsonElement Parse(ReadOnlySpan<byte> bytes, string[] fields)
    {
        if (bytes.Length is < 1 or > 16384 || bytes.StartsWith(new byte[] { 239, 187, 191 }))
            throw new ArgumentException("Invalid identity wire size or BOM.");
        _ = Utf8.GetString(bytes);
        using var document = JsonDocument.Parse(bytes.ToArray(), new JsonDocumentOptions { MaxDepth = 4 });
        var root = document.RootElement;
        CheckObject(root, fields);
        return root.Clone();
    }

    internal static void CheckObject(JsonElement value, string[] fields)
    {
        if (value.ValueKind != JsonValueKind.Object) throw new ArgumentException("Expected an object.");
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in value.EnumerateObject())
            if (!names.Add(field.Name) || !fields.Contains(field.Name, StringComparer.Ordinal))
                throw new ArgumentException("Duplicate or unknown identity field.");
        if (names.Count != fields.Length) throw new ArgumentException("Missing identity field.");
    }

    internal static string Text(JsonElement root, string field)
    {
        var value = root.GetProperty(field);
        if (value.ValueKind != JsonValueKind.String) throw new ArgumentException("Expected text: " + field);
        var text = value.GetString()!;
        _ = Utf8.GetBytes(text);
        return text;
    }

    internal static void Pattern(string value, string pattern)
    {
        if (!Regex.IsMatch(value, "\\A(?:" + pattern + ")\\z", RegexOptions.CultureInvariant))
            throw new ArgumentException("Invalid identity field format.");
    }

    internal static Guid Uuid(JsonElement root, string field)
    {
        var text = Text(root, field);
        if (!Guid.TryParseExact(text, "D", out var id) || id == Guid.Empty || id.ToString("D") != text)
            throw new ArgumentException("Expected a nonzero lowercase UUID.");
        return id;
    }

    internal static long Integer(JsonElement root, string field, bool zero = false)
    {
        var value = root.GetProperty(field);
        // Reject exponent and decimal spellings even when mathematically integral.
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out var result)
            || result < (zero ? 0 : 1) || value.GetRawText() != result.ToString(CultureInfo.InvariantCulture))
            throw new ArgumentException("Expected canonical Int64 integer: " + field);
        return result;
    }

    internal static DateTimeOffset Time(JsonElement root, string field)
    {
        var value = Text(root, field);
        if (value.Length != 28 || !DateTimeOffset.TryParseExact(value, UtcFormat, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var result)
            || result.ToString(UtcFormat, CultureInfo.InvariantCulture) != value)
            throw new ArgumentException("Expected a real UTC7 timestamp.");
        return result;
    }

    internal static void Exact(JsonElement root, string field, string expected)
    {
        if (Text(root, field) != expected) throw new ArgumentException("Invalid identity metadata: " + field);
    }

    internal static void Validate(JsonElement root, string contract)
    {
        Exact(root, "schema_version", "1.0.0"); Exact(root, "contract_id", contract);
        Exact(root, "privacy_class", "sensitive");
        foreach (var item in root.EnumerateObject())
        {
            var n = item.Name;
            if (n == "expected_resolution_revision" && item.Value.ValueKind == JsonValueKind.Null) continue;
            if (n is "request_id" or "proof_id") _ = Uuid(root, n);
            else if (n is "occurred_at" or "issued_at" or "expires_at") _ = Time(root, n);
            else if (n is "alias_key_epoch" or "resolution_revision" or "trust_epoch" or "release_generation" or "expected_resolution_revision") _ = Integer(root, n);
            else if (n == "revocation_epoch") _ = Integer(root, n, true);
            else if (n == "target_soul_id")
            {
                if (item.Value.ValueKind != JsonValueKind.Null) Pattern(Text(root, n), "soul_[a-f0-9]{64}");
            }
            else if (n == "soul_id") Pattern(Text(root, n), "soul_[a-f0-9]{64}");
            else if (n == "device_binding_id") Pattern(Text(root, n), "db_[a-f0-9]{32}");
            else if (n == "platform_account_id") Pattern(Text(root, n), "pa_[a-f0-9]{32}");
            else if (n == "trace_id") Pattern(Text(root, n), "trace_[a-f0-9]{32}");
            else if (n == "idempotency_key") Pattern(Text(root, n), "idem_[a-f0-9]{64}");
            else if (n == "tenant_ref") Pattern(Text(root, n), "tenant_[a-f0-9]{64}");
            else if (n == "nonce") Pattern(Text(root, n), "nonce_[a-f0-9]{64}");
            else if (n == "proof_ref") Pattern(Text(root, n), "proof_[a-f0-9]{64}");
            else if (n.EndsWith("_sha256", StringComparison.Ordinal) || n == "alias_digest") Pattern(Text(root, n), "[a-f0-9]{64}");
            else if (n is "key_id" or "alias_key_id") Pattern(Text(root, n), "[a-z0-9][a-z0-9._-]{0,63}");
            else if (n == "alias_kind" && Text(root, n) is not ("email" or "phone" or "platform_id")) throw new ArgumentException("Invalid alias kind.");
        }
        if (contract is "soul.alias.verify.request/v1" or "soul.alias.verification/v1")
        {
            var op = Text(root, "operation");
            if (op is not ("create" or "attach" or "resolve" or "revoke")) throw new ArgumentException("Invalid alias operation.");
            if ((op == "create") != (root.GetProperty("target_soul_id").ValueKind == JsonValueKind.Null))
                throw new ArgumentException("Invalid alias target relationship.");
        }
        if (contract == "soul.alias.verify.request/v1") Exact(root, "producer_module", "control-plane-host");
        else if (contract == "soul.current.query/v1")
        {
            var purpose = Text(root, "producer_module") switch
            {
                "binding" => "binding-activate", "control-plane-host" => "identity-read",
                "policy-approval" => "policy-shadow-evaluate", "memory-event-ledger" => "ledger-append",
                _ => throw new ArgumentException("Unknown current caller.")
            };
            Exact(root, "purpose", purpose);
        }
        else
        {
            Exact(root, "producer_module", "soul-registry");
            Exact(root, "signature_algorithm", Algorithm);
            var signature = Text(root, "signature_base64");
            if (signature.Length != 88) throw new ArgumentException("Invalid signature length.");
            var decoded = Convert.FromBase64String(signature);
            if (decoded.Length != 64 || Convert.ToBase64String(decoded) != signature) throw new ArgumentException("Invalid P1363 encoding.");
            var lifetime = Time(root, "expires_at") - Time(root, "issued_at");
            if (lifetime <= TimeSpan.Zero || lifetime > TimeSpan.FromSeconds(300)) throw new ArgumentException("Invalid signed window.");
            if (contract == "soul.alias.verification/v1")
            {
                Exact(root, "issuer", "dps-soul-alias-authority"); Exact(root, "audience", "soul-registry"); Exact(root, "key_role", "alias-verification");
            }
            else
            {
                Exact(root, "issuer", "dps-soul-current-authority"); Exact(root, "key_role", "soul-current");
                if (Text(root, "audience") is not ("binding" or "control-plane-host" or "policy-approval" or "memory-event-ledger")) throw new ArgumentException("Invalid current audience.");
                if (Text(root, "status") is not ("ACTIVE" or "REVOKED")) throw new ArgumentException("Invalid current status.");
                Exact(root, "resolution_canonicalizer_id", SoulResolvedCanonicalV1.LedgerAuditId);
                var resolution = SoulResolvedCanonicalV1.FromWireElement(root.GetProperty("resolution"));
                if (Text(root, "resolution_sha256") != SoulResolvedCanonicalV1.ComputeLedgerAuditSha256(resolution)) throw new ArgumentException("Resolution commitment mismatch.");
                foreach (var field in new[] { "soul_id", "device_binding_id", "platform_account_id", "trace_id", "idempotency_key" })
                    if (Text(root, field) != Text(root.GetProperty("resolution"), field)) throw new ArgumentException("Resolution scope mismatch.");
            }
        }
    }

    internal static byte[] Write((string Name, object? Value)[] values)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var (name, value) in values)
            {
                switch (value)
                {
                    case null: writer.WriteNull(name); break;
                    case string text: _ = Utf8.GetBytes(text); writer.WriteString(name, text); break;
                    case Guid id: writer.WriteString(name, id.ToString("D")); break;
                    case long number: writer.WriteNumber(name, number); break;
                    case DateTimeOffset time:
                        if (time.Offset != TimeSpan.Zero) throw new ArgumentException("UTC is required.");
                        writer.WriteString(name, time.ToString(UtcFormat, CultureInfo.InvariantCulture)); break;
                    case SoulResolved soul: writer.WritePropertyName(name); writer.WriteRawValue(SoulResolvedCanonicalV1.SerializeWire(soul)); break;
                    default: throw new ArgumentException("Unsupported identity value.");
                }
            }
            writer.WriteEndObject();
        }
        return stream.ToArray();
    }

    internal static byte[] Frame(JsonElement root, string domain, string[] fields)
    {
        using var stream = new MemoryStream();
        void Field(string value)
        {
            var bytes = Utf8.GetBytes(value); Span<byte> size = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(size, checked((uint)bytes.Length)); stream.Write(size); stream.Write(bytes);
        }
        Field(domain);
        foreach (var name in fields)
        {
            if (name is "signature_base64" or "resolution") continue;
            var item = root.GetProperty(name);
            if (name is "target_soul_id" or "expected_resolution_revision")
            {
                Field(item.ValueKind == JsonValueKind.Null ? "false" : "true");
                if (item.ValueKind == JsonValueKind.Null) continue;
            }
            Field(name is "request_id" or "proof_id" ? Uuid(root, name).ToString("N") :
                item.ValueKind == JsonValueKind.Number ? item.GetInt64().ToString(CultureInfo.InvariantCulture) : Text(root, name));
        }
        return stream.ToArray();
    }

    internal static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}

public enum SoulIdentityErrorV1
{
    INVALID_WIRE, UNSUPPORTED_CONTRACT, AUTHORITY_UNAVAILABLE, INVALID_SIGNATURE,
    SCOPE_MISMATCH, STALE_IDENTITY, EXPIRED, REVOKED, IDEMPOTENCY_CONFLICT,
    DEADLINE_EXCEEDED, COMMIT_UNKNOWN, NOT_FOUND_OR_DENIED
}

/// <summary>Declarations only. Implementations must authenticate callers and use owner transactions.</summary>
public interface ISoulAliasClientV1
{
    Task<SoulAliasVerificationV1> VerifyAliasAsync(SoulAliasVerificationRequestV1 request, CancellationToken cancellationToken);
    Task<SoulResolved> ApplyVerifiedAliasAsync(SoulAliasVerificationV1 proof, CancellationToken cancellationToken);
}

/// <summary>Immutable untrusted contract data. Codec validation does not issue authority.</summary>
public sealed record SoulAliasVerificationRequestV1
{
    public const string ContractId = "soul.alias.verify.request/v1";
    private static readonly string[] Fields = ["schema_version", "contract_id", "producer_module", "request_id", "tenant_ref", "operation", "alias_kind", "proof_ref", "target_soul_id", "device_binding_id", "platform_account_id", "trace_id", "idempotency_key", "occurred_at", "privacy_class", "nonce"];
    public required Guid RequestId { get; init; }
    public required string TenantRef { get; init; }
    public required string Operation { get; init; }
    public required string AliasKind { get; init; }
    public required string ProofRef { get; init; }
    public required string? TargetSoulId { get; init; }
    public required string DeviceBindingId { get; init; }
    public required string PlatformAccountId { get; init; }
    public required string TraceId { get; init; }
    public required string IdempotencyKey { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }
    public required string Nonce { get; init; }

    public byte[] ToUtf8Json()
    {
        var bytes = SoulIdentityWire.Write([
            ("schema_version", "1.0.0"),
            ("contract_id", "soul.alias.verify.request/v1"),
            ("producer_module", "control-plane-host"),
            ("request_id", RequestId),
            ("tenant_ref", TenantRef),
            ("operation", Operation),
            ("alias_kind", AliasKind),
            ("proof_ref", ProofRef),
            ("target_soul_id", TargetSoulId),
            ("device_binding_id", DeviceBindingId),
            ("platform_account_id", PlatformAccountId),
            ("trace_id", TraceId),
            ("idempotency_key", IdempotencyKey),
            ("occurred_at", OccurredAt),
            ("privacy_class", "sensitive"),
            ("nonce", Nonce)
        ]);
        var root = SoulIdentityWire.Parse(bytes, Fields);
        SoulIdentityWire.Validate(root, ContractId);
        return bytes;
    }

    public static SoulAliasVerificationRequestV1 FromUtf8Json(ReadOnlySpan<byte> bytes)
    {
        var root = SoulIdentityWire.Parse(bytes, Fields);
        SoulIdentityWire.Validate(root, ContractId);
        return new SoulAliasVerificationRequestV1
        {
            RequestId = SoulIdentityWire.Uuid(root, "request_id"),
            TenantRef = SoulIdentityWire.Text(root, "tenant_ref"),
            Operation = SoulIdentityWire.Text(root, "operation"),
            AliasKind = SoulIdentityWire.Text(root, "alias_kind"),
            ProofRef = SoulIdentityWire.Text(root, "proof_ref"),
            TargetSoulId = root.GetProperty("target_soul_id").ValueKind == JsonValueKind.Null ? null : SoulIdentityWire.Text(root, "target_soul_id"),
            DeviceBindingId = SoulIdentityWire.Text(root, "device_binding_id"),
            PlatformAccountId = SoulIdentityWire.Text(root, "platform_account_id"),
            TraceId = SoulIdentityWire.Text(root, "trace_id"),
            IdempotencyKey = SoulIdentityWire.Text(root, "idempotency_key"),
            OccurredAt = SoulIdentityWire.Time(root, "occurred_at"),
            Nonce = SoulIdentityWire.Text(root, "nonce")
        };
    }

    public byte[] CanonicalBytes() => SoulIdentityWire.Frame(SoulIdentityWire.Parse(ToUtf8Json(), Fields), "dps.soul-registry.alias-verify-request/v1", Fields);
    public string ComputeSha256() => SoulIdentityWire.Hash(CanonicalBytes());
}

/// <summary>Immutable untrusted contract data. Codec validation does not issue authority.</summary>
public sealed record SoulAliasVerificationV1
{
    public const string ContractId = "soul.alias.verification/v1";
    private static readonly string[] Fields = ["schema_version", "contract_id", "producer_module", "proof_id", "tenant_ref", "operation", "alias_kind", "alias_digest", "alias_key_id", "alias_key_epoch", "target_soul_id", "device_binding_id", "platform_account_id", "trace_id", "idempotency_key", "occurred_at", "privacy_class", "request_sha256", "issuer", "audience", "key_role", "key_id", "trust_epoch", "revocation_epoch", "release_bom_sha256", "release_generation", "issued_at", "expires_at", "nonce", "signature_algorithm", "signature_base64"];
    public required Guid ProofId { get; init; }
    public required string TenantRef { get; init; }
    public required string Operation { get; init; }
    public required string AliasKind { get; init; }
    public required string AliasDigest { get; init; }
    public required string AliasKeyId { get; init; }
    public required long AliasKeyEpoch { get; init; }
    public required string? TargetSoulId { get; init; }
    public required string DeviceBindingId { get; init; }
    public required string PlatformAccountId { get; init; }
    public required string TraceId { get; init; }
    public required string IdempotencyKey { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }
    public required string RequestSha256 { get; init; }
    public required string KeyId { get; init; }
    public required long TrustEpoch { get; init; }
    public required long RevocationEpoch { get; init; }
    public required string ReleaseBomSha256 { get; init; }
    public required long ReleaseGeneration { get; init; }
    public required DateTimeOffset IssuedAt { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public required string Nonce { get; init; }
    public required string SignatureBase64 { get; init; }

    public byte[] ToUtf8Json()
    {
        var bytes = SoulIdentityWire.Write([
            ("schema_version", "1.0.0"),
            ("contract_id", "soul.alias.verification/v1"),
            ("producer_module", "soul-registry"),
            ("proof_id", ProofId),
            ("tenant_ref", TenantRef),
            ("operation", Operation),
            ("alias_kind", AliasKind),
            ("alias_digest", AliasDigest),
            ("alias_key_id", AliasKeyId),
            ("alias_key_epoch", AliasKeyEpoch),
            ("target_soul_id", TargetSoulId),
            ("device_binding_id", DeviceBindingId),
            ("platform_account_id", PlatformAccountId),
            ("trace_id", TraceId),
            ("idempotency_key", IdempotencyKey),
            ("occurred_at", OccurredAt),
            ("privacy_class", "sensitive"),
            ("request_sha256", RequestSha256),
            ("issuer", "dps-soul-alias-authority"),
            ("audience", "soul-registry"),
            ("key_role", "alias-verification"),
            ("key_id", KeyId),
            ("trust_epoch", TrustEpoch),
            ("revocation_epoch", RevocationEpoch),
            ("release_bom_sha256", ReleaseBomSha256),
            ("release_generation", ReleaseGeneration),
            ("issued_at", IssuedAt),
            ("expires_at", ExpiresAt),
            ("nonce", Nonce),
            ("signature_algorithm", "ES256-P1363-SHA256"),
            ("signature_base64", SignatureBase64)
        ]);
        var root = SoulIdentityWire.Parse(bytes, Fields);
        SoulIdentityWire.Validate(root, ContractId);
        return bytes;
    }

    public static SoulAliasVerificationV1 FromUtf8Json(ReadOnlySpan<byte> bytes)
    {
        var root = SoulIdentityWire.Parse(bytes, Fields);
        SoulIdentityWire.Validate(root, ContractId);
        return new SoulAliasVerificationV1
        {
            ProofId = SoulIdentityWire.Uuid(root, "proof_id"),
            TenantRef = SoulIdentityWire.Text(root, "tenant_ref"),
            Operation = SoulIdentityWire.Text(root, "operation"),
            AliasKind = SoulIdentityWire.Text(root, "alias_kind"),
            AliasDigest = SoulIdentityWire.Text(root, "alias_digest"),
            AliasKeyId = SoulIdentityWire.Text(root, "alias_key_id"),
            AliasKeyEpoch = SoulIdentityWire.Integer(root, "alias_key_epoch"),
            TargetSoulId = root.GetProperty("target_soul_id").ValueKind == JsonValueKind.Null ? null : SoulIdentityWire.Text(root, "target_soul_id"),
            DeviceBindingId = SoulIdentityWire.Text(root, "device_binding_id"),
            PlatformAccountId = SoulIdentityWire.Text(root, "platform_account_id"),
            TraceId = SoulIdentityWire.Text(root, "trace_id"),
            IdempotencyKey = SoulIdentityWire.Text(root, "idempotency_key"),
            OccurredAt = SoulIdentityWire.Time(root, "occurred_at"),
            RequestSha256 = SoulIdentityWire.Text(root, "request_sha256"),
            KeyId = SoulIdentityWire.Text(root, "key_id"),
            TrustEpoch = SoulIdentityWire.Integer(root, "trust_epoch"),
            RevocationEpoch = SoulIdentityWire.Integer(root, "revocation_epoch", true),
            ReleaseBomSha256 = SoulIdentityWire.Text(root, "release_bom_sha256"),
            ReleaseGeneration = SoulIdentityWire.Integer(root, "release_generation"),
            IssuedAt = SoulIdentityWire.Time(root, "issued_at"),
            ExpiresAt = SoulIdentityWire.Time(root, "expires_at"),
            Nonce = SoulIdentityWire.Text(root, "nonce"),
            SignatureBase64 = SoulIdentityWire.Text(root, "signature_base64")
        };
    }

    public byte[] CanonicalBytes() => SoulIdentityWire.Frame(SoulIdentityWire.Parse(ToUtf8Json(), Fields), "dps.soul-registry.alias-verification/v1", Fields);
    public string ComputeSha256() => SoulIdentityWire.Hash(CanonicalBytes());

    /// <summary>Time eligibility only; caller authentication, trust, signature and owner state are still required.</summary>
    public void RequireValidWindow(DateTimeOffset trustedNow)
    {
        _ = ToUtf8Json();
        if (trustedNow < IssuedAt || trustedNow >= ExpiresAt) throw new ArgumentException("Proof is outside its signed window.");
    }

    public void RequireMatches(SoulAliasVerificationRequestV1 request)
    {
        ArgumentNullException.ThrowIfNull(request);
        _ = ToUtf8Json();
        if (RequestSha256 != request.ComputeSha256() || TenantRef != request.TenantRef || Operation != request.Operation || AliasKind != request.AliasKind || TargetSoulId != request.TargetSoulId || DeviceBindingId != request.DeviceBindingId || PlatformAccountId != request.PlatformAccountId || TraceId != request.TraceId || IdempotencyKey != request.IdempotencyKey || OccurredAt != request.OccurredAt || Nonce != request.Nonce)
            throw new ArgumentException("Alias proof does not bind this request.");
    }
}
