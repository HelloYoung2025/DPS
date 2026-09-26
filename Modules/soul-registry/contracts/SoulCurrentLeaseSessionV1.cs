using System.Buffers.Binary;

namespace Dps.SoulRegistry.Contracts;

// Quarantined wire codec; parsing grants no lease or runtime authority.
public sealed record SoulCurrentLeaseSessionV1
{
    public const string ContractId = "soul.current.lease.session/v1";
    private static readonly string[] Fields = ["schema_version", "contract_id", "producer_module", "soul_id", "device_binding_id", "platform_account_id", "trace_id", "idempotency_key", "occurred_at", "privacy_class", "phase", "request_id", "nonce", "instance_id", "session_id", "sequence", "tenant_ref", "query_base64", "query_sha256", "configuration_sha256", "release_bom_sha256", "release_generation", "expires_at", "signing_base64", "resolution_base64", "status_base64", "error_code"];
    public required string ProducerModule { get; init; }
    public required string Phase { get; init; }
    public required SoulCurrentQueryV1 Query { get; init; }
    public required Guid RequestId { get; init; }
    public required string Nonce { get; init; }
    public required Guid InstanceId { get; init; }
    // Empty only for begin. A Guid.Empty surrogate is forbidden.
    public required string SessionId { get; init; }
    public required long Sequence { get; init; }
    public required string ConfigurationSha256 { get; init; }
    public required string ReleaseBomSha256 { get; init; }
    public required long ReleaseGeneration { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public string SigningBase64 { get; init; } = "";
    public string ResolutionBase64 { get; init; } = "";
    public string StatusBase64 { get; init; } = "";
    public string ErrorCode { get; init; } = "";

    public byte[] ToUtf8Json()
    {
        var bytes = Encode(); _ = FromUtf8Json(bytes); return bytes;
    }
    private byte[] Encode() => SoulCurrentFormat.Write([
        ("schema_version", "1.0.0"), ("contract_id", ContractId), ("producer_module", ProducerModule),
        ("soul_id", Query.SoulId), ("device_binding_id", Query.DeviceBindingId), ("platform_account_id", Query.PlatformAccountId),
        ("trace_id", Query.TraceId), ("idempotency_key", Query.IdempotencyKey), ("occurred_at", Query.OccurredAt), ("privacy_class", "sensitive"),
        ("phase", Phase), ("request_id", RequestId), ("nonce", Nonce), ("instance_id", InstanceId), ("session_id", SessionId), ("sequence", Sequence),
        ("tenant_ref", Query.TenantRef), ("query_base64", Convert.ToBase64String(Query.ToUtf8Json())), ("query_sha256", Query.ComputeSha256()),
        ("configuration_sha256", ConfigurationSha256), ("release_bom_sha256", ReleaseBomSha256), ("release_generation", ReleaseGeneration),
        ("expires_at", ExpiresAt), ("signing_base64", SigningBase64), ("resolution_base64", ResolutionBase64), ("status_base64", StatusBase64), ("error_code", ErrorCode)]);

    public static SoulCurrentLeaseSessionV1 FromUtf8Json(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length is < 1 or > 16384) throw new ArgumentException("Invalid lease frame size.");
        var r = SoulIdentityWire.Parse(bytes, Fields);
        SoulIdentityWire.Exact(r, "schema_version", "1.0.0"); SoulIdentityWire.Exact(r, "contract_id", ContractId);
        var q = SoulCurrentQueryV1.FromUtf8Json(SoulCurrentFormat.Base64(SoulIdentityWire.Text(r, "query_base64")));
        var v = new SoulCurrentLeaseSessionV1
        {
            ProducerModule = SoulIdentityWire.Text(r, "producer_module"), Phase = SoulIdentityWire.Text(r, "phase"), Query = q,
            RequestId = SoulIdentityWire.Uuid(r, "request_id"), Nonce = SoulIdentityWire.Text(r, "nonce"), InstanceId = SoulIdentityWire.Uuid(r, "instance_id"),
            SessionId = SoulIdentityWire.Text(r, "session_id"), Sequence = SoulIdentityWire.Integer(r, "sequence"),
            ConfigurationSha256 = SoulIdentityWire.Text(r, "configuration_sha256"), ReleaseBomSha256 = SoulIdentityWire.Text(r, "release_bom_sha256"),
            ReleaseGeneration = SoulIdentityWire.Integer(r, "release_generation"), ExpiresAt = SoulIdentityWire.Time(r, "expires_at"),
            SigningBase64 = SoulIdentityWire.Text(r, "signing_base64"), ResolutionBase64 = SoulIdentityWire.Text(r, "resolution_base64"),
            StatusBase64 = SoulIdentityWire.Text(r, "status_base64"), ErrorCode = SoulIdentityWire.Text(r, "error_code")
        };
        SoulIdentityWire.Pattern(v.Nonce, "nonce_[a-f0-9]{64}");
        SoulIdentityWire.Pattern(v.ConfigurationSha256, "[a-f0-9]{64}"); SoulIdentityWire.Pattern(v.ReleaseBomSha256, "[a-f0-9]{64}");
        if (v.Phase == "acquire/begin") { if (v.SessionId != "" || v.Sequence != 1) throw new ArgumentException("Invalid begin."); }
        else _ = SoulIdentityWire.Uuid(r, "session_id");
        var host = v.Phase is "acquire/begin" or "acquire/possession-request" or "acquire/possession-answer" or "acquire/complete" or "revalidate/request" or "dispose/request";
        if (v.ProducerModule != (host ? "control-plane-host" : "soul-registry")) throw new ArgumentException("Wrong producer.");
        var signing = v.Phase switch
        {
            "acquire/possession-request" => "challenge-request", "acquire/possession-challenge" => "challenge",
            "acquire/possession-answer" => "answer", "acquire/possession-proof" or "acquire/complete" => "proof", _ => ""
        };
        var resolution = v.Phase == "acquire/acquired";
        var status = v.Phase is "acquire/acquired" or "revalidate/response";
        if (v.Phase is not ("acquire/begin" or "acquire/challenge" or "acquire/possession-request" or "acquire/possession-challenge" or
            "acquire/possession-answer" or "acquire/possession-proof" or "acquire/complete" or "acquire/acquired" or "revalidate/request" or "revalidate/response" or "dispose/request" or "dispose/ack" or "error/response"))
            throw new ArgumentException("Unknown lease phase.");
        if ((signing.Length != 0) != (v.SigningBase64.Length != 0) || resolution != (v.ResolutionBase64.Length != 0) || status != (v.StatusBase64.Length != 0) ||
            (v.Phase == "error/response" ? v.ErrorCode is not ("AUTHORITY_UNAVAILABLE" or "NOT_FOUND_OR_DENIED" or "DEADLINE_EXCEEDED") : v.ErrorCode != ""))
            throw new ArgumentException("Invalid phase payload.");
        if (signing.Length != 0)
        {
            var p = SoulCurrentSigningProtocolV1.FromUtf8Json(SoulCurrentFormat.Base64(v.SigningBase64));
            if (p.Operation != "host-possession" || p.MessageType != signing || p.InstanceId != v.InstanceId || p.SessionId.ToString("D") != v.SessionId ||
                p.QueryBase64 != Convert.ToBase64String(q.ToUtf8Json()) || p.ConfigurationSha256 != v.ConfigurationSha256 ||
                p.ReleaseBomSha256 != v.ReleaseBomSha256 || p.ReleaseGeneration != v.ReleaseGeneration || p.ExpiresAt != v.ExpiresAt ||
                (v.Phase.StartsWith("acquire/possession-", StringComparison.Ordinal) && p.RequestId != v.RequestId))
                throw new ArgumentException("Unbound possession.");
        }
        if (resolution)
        {
            var p = SoulCurrentResolutionV1.FromUtf8Json(SoulCurrentFormat.Base64(v.ResolutionBase64)); p.RequireMatches(q);
            if (p.ReleaseBomSha256 != v.ReleaseBomSha256 || p.ReleaseGeneration != v.ReleaseGeneration || p.ExpiresAt > v.ExpiresAt) throw new ArgumentException("Unbound resolution.");
        }
        if (status)
        {
            var p = SoulCurrentStatusV1.FromUtf8Json(SoulCurrentFormat.Base64(v.StatusBase64));
            if (p.RequestId != v.RequestId || p.Nonce != v.Nonce || p.InstanceId != v.InstanceId || p.SessionId.ToString("D") != v.SessionId ||
                p.QuerySha256 != q.ComputeSha256() || p.TenantRef != q.TenantRef || p.SoulId != q.SoulId || p.ConfigurationSha256 != v.ConfigurationSha256 ||
                p.ReleaseBomSha256 != v.ReleaseBomSha256 || p.ReleaseGeneration != v.ReleaseGeneration || p.ExpiresAt > v.ExpiresAt) throw new ArgumentException("Unbound status.");
        }
        // Also proves every public scope field equals the actual query; no opaque substitute or alternate encoding.
        if (!bytes.SequenceEqual(v.Encode())) throw new ArgumentException("Noncanonical or mismatched lease frame.");
        return v;
    }

    public void RequireBinding(SoulCurrentLeaseSessionV1 other, bool assignSession = false)
    {
        if (InstanceId != other.InstanceId || (!assignSession && SessionId != other.SessionId) ||
            !Query.ToUtf8Json().AsSpan().SequenceEqual(other.Query.ToUtf8Json()) || ConfigurationSha256 != other.ConfigurationSha256 ||
            ReleaseBomSha256 != other.ReleaseBomSha256 || ReleaseGeneration != other.ReleaseGeneration || ExpiresAt != other.ExpiresAt)
            throw new ArgumentException("Lease binding mismatch.");
    }
    public void RequireResponse(SoulCurrentLeaseSessionV1 response, string phase, bool assignSession = false)
    {
        RequireBinding(response, assignSession);
        if (response.Phase != phase || response.Sequence != Sequence + 1 || response.RequestId != RequestId || response.Nonce != Nonce)
            throw new ArgumentException("Lease response mismatch.");
    }
    public SoulCurrentLeaseSessionV1 Reply(string phase) => this with { Phase = phase, ProducerModule = "soul-registry", Sequence = checked(Sequence + 1), SigningBase64 = "", ResolutionBase64 = "", StatusBase64 = "", ErrorCode = "" };

    public static async Task WriteAsync(Stream stream, SoulCurrentLeaseSessionV1 value, CancellationToken token)
    {
        var bytes = value.ToUtf8Json(); var header = new byte[4]; BinaryPrimitives.WriteUInt32BigEndian(header, (uint)bytes.Length);
        await stream.WriteAsync(header, token); await stream.WriteAsync(bytes, token); await stream.FlushAsync(token);
    }
    public static async Task<SoulCurrentLeaseSessionV1> ReadAsync(Stream stream, CancellationToken token)
    {
        var header = new byte[4]; await stream.ReadExactlyAsync(header, token); var size = BinaryPrimitives.ReadUInt32BigEndian(header);
        if (size is < 1 or > 16384) throw new ArgumentException("Invalid lease frame size.");
        var bytes = new byte[(int)size]; await stream.ReadExactlyAsync(bytes, token); return FromUtf8Json(bytes);
    }
}
