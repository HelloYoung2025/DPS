using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Dps.PolicyApproval.Contracts;

/// <summary>
/// Public, versioned projection of one authoritative Policy approval snapshot.
/// Policy Approval owns and produces it only from its own exact reader; it is
/// unsigned readback data, never execution authority by itself. It binds the
/// proposal commitment and major, and the persisted evaluation-request signature
/// (required for major 2, absent for historical major 1). <c>approval_sha256</c>
/// is the existing approval snapshot commitment
/// (<c>dps.operation-compiler.approval-snapshot-sha256/v1</c>) that consumers
/// recompute from <c>approval</c>; a historical v1 row can never be represented
/// as an ACTIVE non-shadow APPROVED readback. The top-level scope fields repeat the
/// persisted scope of the same approval row and must equal the embedded approval.
/// </summary>
public sealed record ApprovalReadbackV1(
    string SchemaVersion,
    string ContractId,
    string ProducerModule,
    string SoulId,
    string DeviceBindingId,
    string PlatformAccountId,
    string TraceId,
    string IdempotencyKey,
    DateTimeOffset OccurredAt,
    ApprovalDecisionV1 Approval,
    string ApprovalSha256,
    string ProposalSha256,
    int ProposalContractMajor,
    string? RequestSignature,
    string Status,
    long StatusRevision,
    DateTimeOffset ValidUntil,
    long RuntimeRevision,
    string RuntimeStateSha256,
    string ReleaseBomSha256,
    string PrivacyClass)
{
    public const string CurrentSchemaVersion = "1.0.0";
    public const string CurrentContractId = "approval.readback/v1";
    public const string CurrentProducerModule = "policy-approval";
    public const string Active = "ACTIVE";
    public const string Revoked = "REVOKED";

    public void Validate()
    {
        ApprovalContractGuard.RequireExact(SchemaVersion, CurrentSchemaVersion, nameof(SchemaVersion));
        ApprovalContractGuard.RequireExact(ContractId, CurrentContractId, nameof(ContractId));
        ApprovalContractGuard.RequireExact(ProducerModule, CurrentProducerModule, nameof(ProducerModule));
        ArgumentNullException.ThrowIfNull(Approval);
        Approval.Validate();
        if (!string.Equals(SoulId, Approval.SoulId, StringComparison.Ordinal)
            || !string.Equals(DeviceBindingId, Approval.DeviceBindingId, StringComparison.Ordinal)
            || !string.Equals(PlatformAccountId, Approval.PlatformAccountId, StringComparison.Ordinal)
            || !string.Equals(TraceId, Approval.TraceId, StringComparison.Ordinal)
            || !string.Equals(IdempotencyKey, Approval.IdempotencyKey, StringComparison.Ordinal)
            || OccurredAt != Approval.OccurredAt
            || OccurredAt.Offset != TimeSpan.Zero)
            throw new ArgumentException("Readback scope must equal the embedded approval scope.", nameof(SoulId));
        ApprovalContractGuard.RequireSha256(ApprovalSha256, nameof(ApprovalSha256));
        ApprovalContractGuard.RequireSha256(ProposalSha256, nameof(ProposalSha256));
        if (ProposalContractMajor is not (1 or 2))
            throw new NotSupportedException("Unknown action.proposal contract major fails closed.");
        if (Status is not (Active or Revoked))
            throw new NotSupportedException($"Unsupported {nameof(Status)} '{Status}'.");
        if (ProposalContractMajor == 2)
        {
            if (RequestSignature is null)
                throw new ArgumentException("A v2 readback must bind its persisted request signature.", nameof(RequestSignature));
            ApprovalContractGuard.RequireP256P1363Signature(RequestSignature, nameof(RequestSignature));
        }
        else
        {
            if (RequestSignature is not null)
                throw new ArgumentException("A historical v1 readback carries no request signature.", nameof(RequestSignature));
            if (Approval.Decision == ApprovalDecisionV1.Approved && !Approval.ShadowOnly && Status == Active)
                throw new NotSupportedException("An action.proposal/v1 approval is quarantine-only and never readable as executable.");
        }
        if (StatusRevision < 1) throw new ArgumentException("Status revision must be positive.", nameof(StatusRevision));
        ApprovalContractGuard.RequireUtc(ValidUntil, nameof(ValidUntil));
        if (ValidUntil <= Approval.OccurredAt)
            throw new ArgumentException("Readback validity must end after the decision occurrence time.", nameof(ValidUntil));
        if (RuntimeRevision < 1) throw new ArgumentException("Runtime revision must be positive.", nameof(RuntimeRevision));
        ApprovalContractGuard.RequireSha256(RuntimeStateSha256, nameof(RuntimeStateSha256));
        ApprovalContractGuard.RequireSha256(ReleaseBomSha256, nameof(ReleaseBomSha256));
        ApprovalContractGuard.RequireExact(PrivacyClass, "internal", nameof(PrivacyClass));
    }
}

public static class ApprovalReadbackV1Codec
{
    public const int MaximumPayloadBytes = 80 * 1024;
    private const string ContractName = "approval.readback/v1";
    private static readonly HashSet<string> ExactFields = new(StringComparer.Ordinal)
    {
        "schema_version", "contract_id", "producer_module", "soul_id", "device_binding_id",
        "platform_account_id", "trace_id", "idempotency_key", "occurred_at", "approval", "approval_sha256",
        "proposal_sha256", "proposal_contract_major", "request_signature", "status",
        "status_revision", "valid_until", "runtime_revision", "runtime_state_sha256",
        "release_bom_sha256", "privacy_class"
    };

    public static byte[] Serialize(ApprovalReadbackV1 value)
    {
        ArgumentNullException.ThrowIfNull(value);
        value.Validate();
        var approval = ApprovalDecisionV1Codec.Serialize(value.Approval);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false, SkipValidation = false }))
        {
            writer.WriteStartObject();
            writer.WriteString("schema_version", value.SchemaVersion);
            writer.WriteString("contract_id", value.ContractId);
            writer.WriteString("producer_module", value.ProducerModule);
            writer.WriteString("soul_id", value.SoulId);
            writer.WriteString("device_binding_id", value.DeviceBindingId);
            writer.WriteString("platform_account_id", value.PlatformAccountId);
            writer.WriteString("trace_id", value.TraceId);
            writer.WriteString("idempotency_key", value.IdempotencyKey);
            writer.WriteString("occurred_at", PolicyApprovalContractJson.FormatCanonicalUtc(value.OccurredAt));
            writer.WritePropertyName("approval");
            writer.WriteRawValue(approval, skipInputValidation: false);
            writer.WriteString("approval_sha256", value.ApprovalSha256);
            writer.WriteString("proposal_sha256", value.ProposalSha256);
            writer.WriteNumber("proposal_contract_major", value.ProposalContractMajor);
            if (value.RequestSignature is null) writer.WriteNull("request_signature");
            else writer.WriteString("request_signature", value.RequestSignature);
            writer.WriteString("status", value.Status);
            writer.WriteNumber("status_revision", value.StatusRevision);
            writer.WriteString("valid_until", PolicyApprovalContractJson.FormatCanonicalUtc(value.ValidUntil));
            writer.WriteNumber("runtime_revision", value.RuntimeRevision);
            writer.WriteString("runtime_state_sha256", value.RuntimeStateSha256);
            writer.WriteString("release_bom_sha256", value.ReleaseBomSha256);
            writer.WriteString("privacy_class", value.PrivacyClass);
            writer.WriteEndObject();
        }
        var payload = stream.ToArray();
        if (payload.Length > MaximumPayloadBytes)
        {
            CryptographicOperations.ZeroMemory(payload);
            throw new ArgumentException("Approval readback payload exceeds its byte budget.", nameof(value));
        }
        return payload;
    }

    public static ApprovalReadbackV1 Deserialize(ReadOnlySpan<byte> payloadUtf8)
    {
        PolicyApprovalContractJson.RequirePayload(payloadUtf8, MaximumPayloadBytes, ContractName);
        using var document = JsonDocument.Parse(
            payloadUtf8.ToArray(),
            new JsonDocumentOptions { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow, MaxDepth = 10 });
        var fields = PolicyApprovalContractJson.ReadExactFields(document.RootElement, ExactFields, ExactFields, ContractName);
        var approvalElement = fields["approval"];
        if (approvalElement.ValueKind != JsonValueKind.Object)
            throw new ArgumentException($"{ContractName} field 'approval' must be an object.");
        var major = PolicyApprovalContractJson.ReadPositiveInt64(fields, "proposal_contract_major", ContractName);
        if (major > 2) throw new NotSupportedException("Unknown action.proposal contract major fails closed.");
        var value = new ApprovalReadbackV1(
            PolicyApprovalContractJson.ReadString(fields, "schema_version", ContractName),
            PolicyApprovalContractJson.ReadString(fields, "contract_id", ContractName),
            PolicyApprovalContractJson.ReadString(fields, "producer_module", ContractName),
            PolicyApprovalContractJson.ReadString(fields, "soul_id", ContractName),
            PolicyApprovalContractJson.ReadString(fields, "device_binding_id", ContractName),
            PolicyApprovalContractJson.ReadString(fields, "platform_account_id", ContractName),
            PolicyApprovalContractJson.ReadString(fields, "trace_id", ContractName),
            PolicyApprovalContractJson.ReadString(fields, "idempotency_key", ContractName),
            PolicyApprovalContractJson.ReadCanonicalUtc(fields, "occurred_at", ContractName),
            ApprovalDecisionV1Codec.Deserialize(Encoding.UTF8.GetBytes(approvalElement.GetRawText())),
            PolicyApprovalContractJson.ReadString(fields, "approval_sha256", ContractName),
            PolicyApprovalContractJson.ReadString(fields, "proposal_sha256", ContractName),
            (int)major,
            PolicyApprovalContractJson.ReadNullableString(fields, "request_signature", ContractName),
            PolicyApprovalContractJson.ReadString(fields, "status", ContractName),
            PolicyApprovalContractJson.ReadPositiveInt64(fields, "status_revision", ContractName),
            PolicyApprovalContractJson.ReadCanonicalUtc(fields, "valid_until", ContractName),
            PolicyApprovalContractJson.ReadPositiveInt64(fields, "runtime_revision", ContractName),
            PolicyApprovalContractJson.ReadString(fields, "runtime_state_sha256", ContractName),
            PolicyApprovalContractJson.ReadString(fields, "release_bom_sha256", ContractName),
            PolicyApprovalContractJson.ReadString(fields, "privacy_class", ContractName));
        value.Validate();
        PolicyApprovalContractJson.RequireCanonicalWire(payloadUtf8, Serialize(value), ContractName);
        return value;
    }
}

public static class ApprovalReadbackV1Canonical
{
    public const string Domain = "dps.policy-approval.approval-readback/v1";

    /// <summary>Domain-separated SHA-256 of the exact canonical readback wire.</summary>
    public static string ComputeSha256(ApprovalReadbackV1 readback)
    {
        var wire = ApprovalReadbackV1Codec.Serialize(readback);
        var domain = Encoding.UTF8.GetBytes(Domain);
        var buffer = new byte[8 + domain.Length + wire.Length];
        try
        {
            BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)domain.Length);
            domain.CopyTo(buffer, 4);
            BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(4 + domain.Length), (uint)wire.Length);
            wire.CopyTo(buffer, 8 + domain.Length);
            return Convert.ToHexStringLower(SHA256.HashData(buffer));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(wire);
            CryptographicOperations.ZeroMemory(buffer);
        }
    }
}
