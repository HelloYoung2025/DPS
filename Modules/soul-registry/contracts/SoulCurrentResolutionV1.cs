using System.Text.Json;

namespace Dps.SoulRegistry.Contracts;

/// <summary>Data snapshots are not leases. A fixed owner provider must issue and hold the lease.</summary>
public interface ISoulCurrentClientV1
{
    Task<SoulCurrentResolutionV1> ResolveCurrentAsync(SoulCurrentQueryV1 query, CancellationToken cancellationToken);
    Task<ISoulCurrentLeaseV1> AcquireCurrentAsync(SoulCurrentQueryV1 query, CancellationToken cancellationToken);
}

public interface ISoulCurrentLeaseV1 : IAsyncDisposable
{
    SoulCurrentResolutionV1 Snapshot { get; }
    Task RevalidateAsync(CancellationToken cancellationToken);
}

/// <summary>Immutable untrusted contract data. Codec validation does not issue authority.</summary>
public sealed record SoulCurrentQueryV1
{
    public const string ContractId = "soul.current.query/v1";
    private static readonly string[] Fields = ["schema_version", "contract_id", "producer_module", "request_id", "tenant_ref", "soul_id", "device_binding_id", "platform_account_id", "trace_id", "idempotency_key", "occurred_at", "privacy_class", "purpose", "expected_resolution_revision", "nonce"];
    public required string ProducerModule { get; init; }
    public required Guid RequestId { get; init; }
    public required string TenantRef { get; init; }
    public required string SoulId { get; init; }
    public required string DeviceBindingId { get; init; }
    public required string PlatformAccountId { get; init; }
    public required string TraceId { get; init; }
    public required string IdempotencyKey { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }
    public required string Purpose { get; init; }
    public required long? ExpectedResolutionRevision { get; init; }
    public required string Nonce { get; init; }

    public byte[] ToUtf8Json()
    {
        var bytes = SoulIdentityWire.Write([
            ("schema_version", "1.0.0"),
            ("contract_id", "soul.current.query/v1"),
            ("producer_module", ProducerModule),
            ("request_id", RequestId),
            ("tenant_ref", TenantRef),
            ("soul_id", SoulId),
            ("device_binding_id", DeviceBindingId),
            ("platform_account_id", PlatformAccountId),
            ("trace_id", TraceId),
            ("idempotency_key", IdempotencyKey),
            ("occurred_at", OccurredAt),
            ("privacy_class", "sensitive"),
            ("purpose", Purpose),
            ("expected_resolution_revision", ExpectedResolutionRevision),
            ("nonce", Nonce)
        ]);
        var root = SoulIdentityWire.Parse(bytes, Fields);
        SoulIdentityWire.Validate(root, ContractId);
        return bytes;
    }

    public static SoulCurrentQueryV1 FromUtf8Json(ReadOnlySpan<byte> bytes)
    {
        var root = SoulIdentityWire.Parse(bytes, Fields);
        SoulIdentityWire.Validate(root, ContractId);
        return new SoulCurrentQueryV1
        {
            ProducerModule = SoulIdentityWire.Text(root, "producer_module"),
            RequestId = SoulIdentityWire.Uuid(root, "request_id"),
            TenantRef = SoulIdentityWire.Text(root, "tenant_ref"),
            SoulId = SoulIdentityWire.Text(root, "soul_id"),
            DeviceBindingId = SoulIdentityWire.Text(root, "device_binding_id"),
            PlatformAccountId = SoulIdentityWire.Text(root, "platform_account_id"),
            TraceId = SoulIdentityWire.Text(root, "trace_id"),
            IdempotencyKey = SoulIdentityWire.Text(root, "idempotency_key"),
            OccurredAt = SoulIdentityWire.Time(root, "occurred_at"),
            Purpose = SoulIdentityWire.Text(root, "purpose"),
            ExpectedResolutionRevision = root.GetProperty("expected_resolution_revision").ValueKind == JsonValueKind.Null ? null : SoulIdentityWire.Integer(root, "expected_resolution_revision"),
            Nonce = SoulIdentityWire.Text(root, "nonce")
        };
    }

    public byte[] CanonicalBytes() => SoulIdentityWire.Frame(SoulIdentityWire.Parse(ToUtf8Json(), Fields), "dps.soul-registry.current-query/v1", Fields);
    public string ComputeSha256() => SoulIdentityWire.Hash(CanonicalBytes());
}

/// <summary>Immutable untrusted contract data. Codec validation does not issue authority.</summary>
public sealed record SoulCurrentResolutionV1
{
    public const string ContractId = "soul.current.resolution/v1";
    private static readonly string[] Fields = ["schema_version", "contract_id", "producer_module", "proof_id", "tenant_ref", "soul_id", "device_binding_id", "platform_account_id", "trace_id", "idempotency_key", "occurred_at", "privacy_class", "query_sha256", "status", "resolution_revision", "resolution_canonicalizer_id", "resolution_sha256", "resolution", "issuer", "audience", "key_role", "key_id", "trust_epoch", "revocation_epoch", "release_bom_sha256", "release_generation", "issued_at", "expires_at", "nonce", "signature_algorithm", "signature_base64"];
    public required Guid ProofId { get; init; }
    public required string TenantRef { get; init; }
    public required string SoulId { get; init; }
    public required string DeviceBindingId { get; init; }
    public required string PlatformAccountId { get; init; }
    public required string TraceId { get; init; }
    public required string IdempotencyKey { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }
    public required string QuerySha256 { get; init; }
    public required string Status { get; init; }
    public required long ResolutionRevision { get; init; }
    public required string ResolutionSha256 { get; init; }
    public required SoulResolved Resolution { get; init; }
    public required string Audience { get; init; }
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
            ("contract_id", "soul.current.resolution/v1"),
            ("producer_module", "soul-registry"),
            ("proof_id", ProofId),
            ("tenant_ref", TenantRef),
            ("soul_id", SoulId),
            ("device_binding_id", DeviceBindingId),
            ("platform_account_id", PlatformAccountId),
            ("trace_id", TraceId),
            ("idempotency_key", IdempotencyKey),
            ("occurred_at", OccurredAt),
            ("privacy_class", "sensitive"),
            ("query_sha256", QuerySha256),
            ("status", Status),
            ("resolution_revision", ResolutionRevision),
            ("resolution_canonicalizer_id", "soul.resolved.ledger-audit/v1"),
            ("resolution_sha256", ResolutionSha256),
            ("resolution", Resolution),
            ("issuer", "dps-soul-current-authority"),
            ("audience", Audience),
            ("key_role", "soul-current"),
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

    public static SoulCurrentResolutionV1 FromUtf8Json(ReadOnlySpan<byte> bytes)
    {
        var root = SoulIdentityWire.Parse(bytes, Fields);
        SoulIdentityWire.Validate(root, ContractId);
        return new SoulCurrentResolutionV1
        {
            ProofId = SoulIdentityWire.Uuid(root, "proof_id"),
            TenantRef = SoulIdentityWire.Text(root, "tenant_ref"),
            SoulId = SoulIdentityWire.Text(root, "soul_id"),
            DeviceBindingId = SoulIdentityWire.Text(root, "device_binding_id"),
            PlatformAccountId = SoulIdentityWire.Text(root, "platform_account_id"),
            TraceId = SoulIdentityWire.Text(root, "trace_id"),
            IdempotencyKey = SoulIdentityWire.Text(root, "idempotency_key"),
            OccurredAt = SoulIdentityWire.Time(root, "occurred_at"),
            QuerySha256 = SoulIdentityWire.Text(root, "query_sha256"),
            Status = SoulIdentityWire.Text(root, "status"),
            ResolutionRevision = SoulIdentityWire.Integer(root, "resolution_revision"),
            ResolutionSha256 = SoulIdentityWire.Text(root, "resolution_sha256"),
            Resolution = SoulResolvedCanonicalV1.FromWireElement(root.GetProperty("resolution")),
            Audience = SoulIdentityWire.Text(root, "audience"),
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

    public byte[] CanonicalBytes() => SoulIdentityWire.Frame(SoulIdentityWire.Parse(ToUtf8Json(), Fields), "dps.soul-registry.current-resolution/v1", Fields);
    public string ComputeSha256() => SoulIdentityWire.Hash(CanonicalBytes());

    /// <summary>Time eligibility only; caller authentication, trust, signature and owner state are still required.</summary>
    public void RequireValidWindow(DateTimeOffset trustedNow)
    {
        _ = ToUtf8Json();
        if (trustedNow < IssuedAt || trustedNow >= ExpiresAt) throw new ArgumentException("Proof is outside its signed window.");
    }

    public void RequireMatches(SoulCurrentQueryV1 query)
    {
        ArgumentNullException.ThrowIfNull(query);
        _ = ToUtf8Json();
        if (QuerySha256 != query.ComputeSha256() || Audience != query.ProducerModule || TenantRef != query.TenantRef || SoulId != query.SoulId || DeviceBindingId != query.DeviceBindingId || PlatformAccountId != query.PlatformAccountId || TraceId != query.TraceId || IdempotencyKey != query.IdempotencyKey || Nonce != query.Nonce || (query.ExpectedResolutionRevision is long revision && revision != ResolutionRevision))
            throw new ArgumentException("Current snapshot does not bind this query.");
    }
}
