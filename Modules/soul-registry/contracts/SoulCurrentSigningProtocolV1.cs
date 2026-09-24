using System.Text.Json;

namespace Dps.SoulRegistry.Contracts;

// Closed messages for three process roles, not a byte-signing interface.
public sealed record SoulCurrentSigningProtocolV1
{
    public const string ContractId = "soul.current.signing-protocol/v1";
    private static readonly string[] Fields = ["schema_version", "contract_id", "operation", "message_type", "request_id", "instance_id", "session_id", "query_base64", "query_sha256", "configuration_sha256", "release_bom_sha256", "release_generation", "expires_at", "challenge_nonce", "possession_base64", "status_base64", "resolution_base64", "error_code", "signature_base64"];
    public required string Operation { get; init; }
    public required string MessageType { get; init; }
    public required Guid RequestId { get; init; }
    public required Guid InstanceId { get; init; }
    public required Guid SessionId { get; init; }
    public required string QueryBase64 { get; init; }
    public required string QuerySha256 { get; init; }
    public required string ConfigurationSha256 { get; init; }
    public required string ReleaseBomSha256 { get; init; }
    public required long ReleaseGeneration { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public required string ChallengeNonce { get; init; }
    public required string PossessionBase64 { get; init; }
    public required string StatusBase64 { get; init; }
    public required string ResolutionBase64 { get; init; }
    public required string ErrorCode { get; init; }
    public required string SignatureBase64 { get; init; }
    public byte[] ToUtf8Json()
    {
        var bytes = SoulCurrentFormat.Write([
            ("schema_version", "1.0.0"), ("contract_id", ContractId),
            ("operation", Operation),
            ("message_type", MessageType),
            ("request_id", RequestId),
            ("instance_id", InstanceId),
            ("session_id", SessionId),
            ("query_base64", QueryBase64),
            ("query_sha256", QuerySha256),
            ("configuration_sha256", ConfigurationSha256),
            ("release_bom_sha256", ReleaseBomSha256),
            ("release_generation", ReleaseGeneration),
            ("expires_at", ExpiresAt),
            ("challenge_nonce", ChallengeNonce),
            ("possession_base64", PossessionBase64),
            ("status_base64", StatusBase64),
            ("resolution_base64", ResolutionBase64),
            ("error_code", ErrorCode),
            ("signature_base64", SignatureBase64)
        ]);
        _ = FromUtf8Json(bytes);
        return bytes;
    }

    public static SoulCurrentSigningProtocolV1 FromUtf8Json(ReadOnlySpan<byte> bytes)
    {
        var root = SoulIdentityWire.Parse(bytes, Fields);
        SoulIdentityWire.Exact(root, "schema_version", "1.0.0");
        SoulIdentityWire.Exact(root, "contract_id", ContractId);
        var value = new SoulCurrentSigningProtocolV1
        {
            Operation = SoulIdentityWire.Text(root, "operation"),
            MessageType = SoulIdentityWire.Text(root, "message_type"),
            RequestId = SoulIdentityWire.Uuid(root, "request_id"),
            InstanceId = SoulIdentityWire.Uuid(root, "instance_id"),
            SessionId = SoulIdentityWire.Uuid(root, "session_id"),
            QueryBase64 = SoulIdentityWire.Text(root, "query_base64"),
            QuerySha256 = SoulIdentityWire.Text(root, "query_sha256"),
            ConfigurationSha256 = SoulIdentityWire.Text(root, "configuration_sha256"),
            ReleaseBomSha256 = SoulIdentityWire.Text(root, "release_bom_sha256"),
            ReleaseGeneration = SoulIdentityWire.Integer(root, "release_generation"),
            ExpiresAt = SoulIdentityWire.Time(root, "expires_at"),
            ChallengeNonce = SoulIdentityWire.Text(root, "challenge_nonce"),
            PossessionBase64 = SoulIdentityWire.Text(root, "possession_base64"),
            StatusBase64 = SoulIdentityWire.Text(root, "status_base64"),
            ResolutionBase64 = SoulIdentityWire.Text(root, "resolution_base64"),
            ErrorCode = SoulIdentityWire.Text(root, "error_code"),
            SignatureBase64 = SoulIdentityWire.Text(root, "signature_base64"),
        };
        value.Validate();
        return value;
    }

    public SoulCurrentQueryV1 ReadQuery() => SoulCurrentQueryV1.FromUtf8Json(SoulCurrentFormat.Base64(QueryBase64));
    public byte[] CanonicalBytes() => SoulCurrentFormat.Frame(ToUtf8Json(), Fields,
        "dps.soul-registry.host-possession/v1");

    private void Validate()
    {
        if (ReadQuery().ComputeSha256() != QuerySha256) throw new ArgumentException("Query canonical commitment mismatch.");
        SoulIdentityWire.Pattern(ConfigurationSha256, "[a-f0-9]{64}");
        SoulIdentityWire.Pattern(ReleaseBomSha256, "[a-f0-9]{64}");
        var valid = (Operation, MessageType) switch
        {
            ("host-possession", "challenge-request" or "challenge" or "answer" or "proof") => true,
            ("read-current-status" or "sign-current", "request" or "response") => true,
            ("error", "response") => true,
            _ => false
        };
        if (!valid) throw new ArgumentException("Unknown typed signing message.");
        if (Operation == "host-possession")
        {
            Empty(PossessionBase64, StatusBase64, ResolutionBase64, ErrorCode);
            if (MessageType == "challenge-request") Empty(ChallengeNonce);
            else SoulIdentityWire.Pattern(ChallengeNonce, "nonce_[a-f0-9]{64}");
            if (MessageType == "proof") _ = SoulCurrentFormat.Base64(SignatureBase64, 64);
            else Empty(SignatureBase64);
        }
        else if (Operation == "error")
        {
            Empty(PossessionBase64, StatusBase64, ResolutionBase64, SignatureBase64);
            if (ErrorCode is not ("AUTHORITY_UNAVAILABLE" or "NOT_FOUND_OR_DENIED" or "DEADLINE_EXCEEDED"))
                throw new ArgumentException("Unknown correlated error.");
            if (ChallengeNonce.Length != 0) SoulIdentityWire.Pattern(ChallengeNonce, "nonce_[a-f0-9]{64}");
        }
        else
        {
            Empty(ErrorCode, SignatureBase64);
            SoulIdentityWire.Pattern(ChallengeNonce, "nonce_[a-f0-9]{64}");
            if (MessageType == "request")
            {
                // Inspect the shape before recursively decoding: a proof cannot carry another proof.
                var proofBytes = SoulCurrentFormat.Base64(PossessionBase64);
                var proofRoot = SoulIdentityWire.Parse(proofBytes, Fields);
                SoulIdentityWire.Exact(proofRoot, "operation", "host-possession");
                SoulIdentityWire.Exact(proofRoot, "message_type", "proof");
                SoulIdentityWire.Exact(proofRoot, "possession_base64", "");
                _ = FromUtf8Json(proofBytes);
                Empty(ResolutionBase64);
                if (Operation == "sign-current") _ = SoulCurrentStatusV1.FromUtf8Json(SoulCurrentFormat.Base64(StatusBase64));
                else Empty(StatusBase64);
            }
            else
            {
                Empty(PossessionBase64);
                if (Operation == "read-current-status")
                {
                    Empty(ResolutionBase64);
                    _ = SoulCurrentStatusV1.FromUtf8Json(SoulCurrentFormat.Base64(StatusBase64));
                }
                else
                {
                    Empty(StatusBase64);
                    _ = SoulCurrentResolutionV1.FromUtf8Json(SoulCurrentFormat.Base64(ResolutionBase64));
                }
            }
        }
    }

    private static void Empty(params string[] values)
    {
        if (values.Any(value => value.Length != 0)) throw new ArgumentException("Unexpected typed signing payload.");
    }
}
