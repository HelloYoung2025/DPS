using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dps.PolicyApproval.Contracts;

/// <summary>
/// Signed Control Plane request for one Policy Approval evaluation of one exact
/// <c>action.proposal/v2</c> commitment. The signature authenticates the request
/// but is not part of the idempotency commitment: a fresh valid signature over
/// identical content is a replay, never a conflict. Control Plane Host is the
/// only allowed wire producer; Policy Approval owns the contract.
/// </summary>
public sealed record ApprovalEvaluationRequestV1(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,
    [property: JsonPropertyName("contract_id")] string ContractId,
    [property: JsonPropertyName("producer_module")] string ProducerModule,
    [property: JsonPropertyName("auth_scope")] string AuthScope,
    [property: JsonPropertyName("proposal_id")] Guid ProposalId,
    [property: JsonPropertyName("proposal_sha256")] string ProposalSha256,
    [property: JsonPropertyName("soul_id")] string SoulId,
    [property: JsonPropertyName("device_binding_id")] string DeviceBindingId,
    [property: JsonPropertyName("platform_account_id")] string PlatformAccountId,
    [property: JsonPropertyName("trace_id")] string TraceId,
    [property: JsonPropertyName("idempotency_key")] string IdempotencyKey,
    [property: JsonPropertyName("release_bom_sha256")] string ReleaseBomSha256,
    [property: JsonPropertyName("mode")] string Mode,
    [property: JsonPropertyName("execution_promotion_sha256")] string? ExecutionPromotionSha256,
    [property: JsonPropertyName("occurred_at")] DateTimeOffset OccurredAt,
    [property: JsonPropertyName("valid_until")] DateTimeOffset ValidUntil,
    [property: JsonPropertyName("privacy_class")] string PrivacyClass,
    [property: JsonPropertyName("signature_base64")] string SignatureBase64)
{
    public const string CurrentSchemaVersion = "1.0.0";
    public const string CurrentContractId = "approval.evaluation.request/v1";
    public const string CurrentProducerModule = "control-plane-host";
    public const string CurrentAuthScope = "policy:evaluate";
    public const string Shadow = "SHADOW";
    public const string Execute = "EXECUTE";

    /// <summary>
    /// <c>valid_until</c> is derived from the request, never chosen by the caller:
    /// it is exactly <c>occurred_at</c> plus this lifetime, and <c>occurred_at</c>
    /// must equal the bound proposal's <c>occurred_at</c>.
    /// </summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(120);

    public void Validate()
    {
        ApprovalContractGuard.RequireExact(
            SchemaVersion,
            CurrentSchemaVersion,
            nameof(SchemaVersion));
        ApprovalContractGuard.RequireExact(ContractId, CurrentContractId, nameof(ContractId));
        ApprovalContractGuard.RequireExact(
            ProducerModule,
            CurrentProducerModule,
            nameof(ProducerModule));
        ApprovalContractGuard.RequireExact(AuthScope, CurrentAuthScope, nameof(AuthScope));
        ApprovalContractGuard.RequireGuid(ProposalId, nameof(ProposalId));
        ApprovalContractGuard.RequireSha256(ProposalSha256, nameof(ProposalSha256));
        ApprovalContractGuard.RequireScope(SoulId, DeviceBindingId, PlatformAccountId);
        ApprovalContractGuard.RequireTraceId(TraceId);
        ApprovalContractGuard.RequireIdempotencyKey(IdempotencyKey);
        ApprovalContractGuard.RequireSha256(ReleaseBomSha256, nameof(ReleaseBomSha256));
        if (Mode is not (Shadow or Execute))
            throw new NotSupportedException($"Unsupported {nameof(Mode)} '{Mode}'.");
        if ((Mode == Shadow) != (ExecutionPromotionSha256 is null))
            throw new ArgumentException(
                "SHADOW requests bind no execution promotion; EXECUTE requests bind exactly one.",
                nameof(ExecutionPromotionSha256));
        if (ExecutionPromotionSha256 is not null)
            ApprovalContractGuard.RequireSha256(
                ExecutionPromotionSha256,
                nameof(ExecutionPromotionSha256));
        ApprovalContractGuard.RequireUtc(OccurredAt, nameof(OccurredAt));
        ApprovalContractGuard.RequireUtc(ValidUntil, nameof(ValidUntil));
        if (ValidUntil != OccurredAt + Lifetime)
            throw new ArgumentException(
                "Evaluation request validity must be exactly 120 seconds after occurred_at.",
                nameof(ValidUntil));
        ApprovalContractGuard.RequireExact(PrivacyClass, "internal", nameof(PrivacyClass));
        ApprovalContractGuard.RequireP256P1363Signature(SignatureBase64, nameof(SignatureBase64));
    }
}

public static class ApprovalEvaluationRequestV1Codec
{
    public const int MaximumPayloadBytes = 16 * 1024;
    private const string ContractName = "approval.evaluation.request/v1";
    private static readonly HashSet<string> ExactFields = new(StringComparer.Ordinal)
    {
        "schema_version", "contract_id", "producer_module", "auth_scope",
        "proposal_id", "proposal_sha256", "soul_id", "device_binding_id",
        "platform_account_id", "trace_id", "idempotency_key", "release_bom_sha256",
        "mode", "execution_promotion_sha256", "occurred_at", "valid_until",
        "privacy_class", "signature_base64"
    };

    public static byte[] Serialize(ApprovalEvaluationRequestV1 value)
    {
        ArgumentNullException.ThrowIfNull(value);
        value.Validate();
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(
                   stream,
                   new JsonWriterOptions { Indented = false, SkipValidation = false }))
        {
            writer.WriteStartObject();
            writer.WriteString("schema_version", value.SchemaVersion);
            writer.WriteString("contract_id", value.ContractId);
            writer.WriteString("producer_module", value.ProducerModule);
            writer.WriteString("auth_scope", value.AuthScope);
            writer.WriteString("proposal_id", value.ProposalId.ToString("D"));
            writer.WriteString("proposal_sha256", value.ProposalSha256);
            writer.WriteString("soul_id", value.SoulId);
            writer.WriteString("device_binding_id", value.DeviceBindingId);
            writer.WriteString("platform_account_id", value.PlatformAccountId);
            writer.WriteString("trace_id", value.TraceId);
            writer.WriteString("idempotency_key", value.IdempotencyKey);
            writer.WriteString("release_bom_sha256", value.ReleaseBomSha256);
            writer.WriteString("mode", value.Mode);
            if (value.ExecutionPromotionSha256 is null)
                writer.WriteNull("execution_promotion_sha256");
            else
                writer.WriteString("execution_promotion_sha256", value.ExecutionPromotionSha256);
            writer.WriteString(
                "occurred_at",
                PolicyApprovalContractJson.FormatCanonicalUtc(value.OccurredAt));
            writer.WriteString(
                "valid_until",
                PolicyApprovalContractJson.FormatCanonicalUtc(value.ValidUntil));
            writer.WriteString("privacy_class", value.PrivacyClass);
            writer.WriteString("signature_base64", value.SignatureBase64);
            writer.WriteEndObject();
        }
        var payload = stream.ToArray();
        if (payload.Length > MaximumPayloadBytes)
        {
            CryptographicOperations.ZeroMemory(payload);
            throw new ArgumentException(
                "Approval evaluation request payload exceeds its byte budget.",
                nameof(value));
        }
        return payload;
    }

    public static ApprovalEvaluationRequestV1 Deserialize(ReadOnlySpan<byte> payloadUtf8)
    {
        PolicyApprovalContractJson.RequirePayload(
            payloadUtf8,
            MaximumPayloadBytes,
            ContractName);
        using var document = JsonDocument.Parse(
            payloadUtf8.ToArray(),
            new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 4
            });
        var fields = PolicyApprovalContractJson.ReadExactFields(
            document.RootElement,
            ExactFields,
            ExactFields,
            ContractName);
        var value = new ApprovalEvaluationRequestV1(
            PolicyApprovalContractJson.ReadString(fields, "schema_version", ContractName),
            PolicyApprovalContractJson.ReadString(fields, "contract_id", ContractName),
            PolicyApprovalContractJson.ReadString(fields, "producer_module", ContractName),
            PolicyApprovalContractJson.ReadString(fields, "auth_scope", ContractName),
            PolicyApprovalContractJson.ReadAbsoluteGuid(fields, "proposal_id", ContractName),
            PolicyApprovalContractJson.ReadString(fields, "proposal_sha256", ContractName),
            PolicyApprovalContractJson.ReadString(fields, "soul_id", ContractName),
            PolicyApprovalContractJson.ReadString(fields, "device_binding_id", ContractName),
            PolicyApprovalContractJson.ReadString(fields, "platform_account_id", ContractName),
            PolicyApprovalContractJson.ReadString(fields, "trace_id", ContractName),
            PolicyApprovalContractJson.ReadString(fields, "idempotency_key", ContractName),
            PolicyApprovalContractJson.ReadString(fields, "release_bom_sha256", ContractName),
            PolicyApprovalContractJson.ReadString(fields, "mode", ContractName),
            PolicyApprovalContractJson.ReadNullableString(
                fields,
                "execution_promotion_sha256",
                ContractName),
            PolicyApprovalContractJson.ReadCanonicalUtc(fields, "occurred_at", ContractName),
            PolicyApprovalContractJson.ReadCanonicalUtc(fields, "valid_until", ContractName),
            PolicyApprovalContractJson.ReadString(fields, "privacy_class", ContractName),
            PolicyApprovalContractJson.ReadString(fields, "signature_base64", ContractName));
        value.Validate();
        PolicyApprovalContractJson.RequireCanonicalWire(
            payloadUtf8,
            Serialize(value),
            ContractName);
        return value;
    }
}

public static class ApprovalEvaluationRequestV1Canonical
{
    public const string Domain = "dps.policy-approval.approval-evaluation-request/v1";

    /// <summary>
    /// Unsigned canonical bytes. These exact bytes are what the Control Plane
    /// signs, and their SHA-256 is the idempotency commitment. The signature is
    /// deliberately excluded.
    /// </summary>
    public static byte[] CanonicalBytes(ApprovalEvaluationRequestV1 request)
    {
        ArgumentNullException.ThrowIfNull(request);
        request.Validate();
        using var writer = new CanonicalWriter();
        writer.Field(Domain);
        writer.Field(request.SchemaVersion);
        writer.Field(request.ContractId);
        writer.Field(request.ProducerModule);
        writer.Field(request.AuthScope);
        writer.Field(request.ProposalId.ToString("N"));
        writer.Field(request.ProposalSha256);
        writer.Field(request.SoulId);
        writer.Field(request.DeviceBindingId);
        writer.Field(request.PlatformAccountId);
        writer.Field(request.TraceId);
        writer.Field(request.IdempotencyKey);
        writer.Field(request.ReleaseBomSha256);
        writer.Field(request.Mode);
        writer.NullableField(request.ExecutionPromotionSha256);
        writer.Field(request.OccurredAt.ToString("O", CultureInfo.InvariantCulture));
        writer.Field(request.ValidUntil.ToString("O", CultureInfo.InvariantCulture));
        writer.Field(request.PrivacyClass);
        return writer.ToArray();
    }

    /// <summary>Signature-free idempotency commitment of one request.</summary>
    public static string ComputeCommitmentSha256(ApprovalEvaluationRequestV1 request)
        => HashAndClear(CanonicalBytes(request));

    /// <summary>
    /// Binds an EXECUTE request to the unsigned canonical payload of one
    /// <c>action.execution.promotion/v1</c>; re-signing the same promotion keeps
    /// the request commitment unchanged.
    /// </summary>
    public static string ComputeExecutionPromotionSha256(ActionExecutionPromotionV1 promotion)
        => HashAndClear(ActionExecutionPromotionV1Canonical.CanonicalBytes(promotion));

    private static string HashAndClear(byte[] canonical)
    {
        try
        {
            return Convert.ToHexStringLower(SHA256.HashData(canonical));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(canonical);
        }
    }

    private sealed class CanonicalWriter : IDisposable
    {
        private static readonly UTF8Encoding StrictUtf8 = new(false, true);
        private readonly MemoryStream _stream = new();

        internal void Field(string value)
        {
            ArgumentNullException.ThrowIfNull(value);
            var bytes = StrictUtf8.GetBytes(value);
            try
            {
                Span<byte> length = stackalloc byte[sizeof(uint)];
                BinaryPrimitives.WriteUInt32BigEndian(length, checked((uint)bytes.Length));
                _stream.Write(length);
                _stream.Write(bytes);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(bytes);
            }
        }

        internal void NullableField(string? value)
        {
            Field(value is null ? "false" : "true");
            if (value is not null) Field(value);
        }

        internal byte[] ToArray() => _stream.ToArray();

        public void Dispose() => _stream.Dispose();
    }
}
