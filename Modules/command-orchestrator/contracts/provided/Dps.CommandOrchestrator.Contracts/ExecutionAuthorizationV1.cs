using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Dps.CommandOrchestrator.Contracts;

public static class ExecutionAuthorizationProtocolV1
{
    public const int P1363SignatureSizeBytes = Dps.PolicyApproval.Contracts.ExecutionAuthorizationCanonicalV1.P1363SignatureSizeBytes;
    private const string CommandDomain = "dps.command-orchestrator.command-dispatch/v1";
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static string ComputeCommandSha256(CommandDispatchV1 command)
    {
        var canonical = CanonicalCommandBytes(command);
        try { return Convert.ToHexStringLower(SHA256.HashData(canonical)); }
        finally { CryptographicOperations.ZeroMemory(canonical); }
    }

    public static string ComputeAuthorizationSha256(ExecutionAuthorizationV1 authorization) =>
        Dps.PolicyApproval.Contracts.ExecutionAuthorizationCanonicalV1.ComputeAuthorizationSha256(authorization);

    public static byte[] CanonicalCommandBytes(CommandDispatchV1 command)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Validate();
        return Encode(writer =>
        {
            writer.Token(CommandDomain);
            writer.Field("schema_version", command.SchemaVersion);
            writer.Field("contract_id", command.ContractId);
            writer.Field("producer_module", command.ProducerModule);
            writer.Field("command_id", command.CommandId);
            writer.Field("operation_id", command.OperationId);
            writer.Field("approval_id", command.ApprovalId);
            writer.Field("approval_sha256", command.ApprovalSha256);
            writer.Field("soul_id", command.SoulId);
            writer.Field("device_binding_id", command.DeviceBindingId);
            writer.Field("platform_account_id", command.PlatformAccountId);
            writer.Field("trace_id", command.TraceId);
            writer.Field("idempotency_key", command.IdempotencyKey);
            writer.Field("occurred_at", command.OccurredAt);
            writer.Field("privacy_class", command.PrivacyClass);
            writer.Field("action_kind", command.ActionKind);
            writer.Field("is_side_effect", command.IsSideEffect);
            writer.NullableField("platform_authorization_id", command.PlatformAuthorizationId);
            writer.Field("lease_id", command.LeaseId);
            writer.Field("lease_owner", command.LeaseOwner);
            writer.Field("lease_expires_at", command.LeaseExpiresAt);
            writer.Field("attempt", command.Attempt);
            writer.Field("steps.count", command.Steps.Count);
            for (var stepOrdinal = 0; stepOrdinal < command.Steps.Count; stepOrdinal++)
            {
                var step = command.Steps[stepOrdinal];
                writer.Field("steps.ordinal", stepOrdinal);
                writer.Field("steps.step_id", step.StepId);
                writer.Field("steps.step_kind", step.StepKind);
                writer.Field("steps.retry_safe", step.RetrySafe);
                writer.Field("steps.postcondition_kind", step.PostconditionKind);
                writer.Field("steps.arguments.count", step.Arguments.Count);
                var argumentOrdinal = 0;
                foreach (var pair in step.Arguments.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    writer.Field("steps.arguments.ordinal", argumentOrdinal++);
                    writer.Field("steps.arguments.key", pair.Key);
                    writer.Field("steps.arguments.value", pair.Value);
                }
            }
        });
    }

    public static byte[] CanonicalAuthorizationBytes(ExecutionAuthorizationV1 authorization) =>
        Dps.PolicyApproval.Contracts.ExecutionAuthorizationCanonicalV1.CanonicalAuthorizationBytes(authorization);

    private static byte[] Encode(Action<CanonicalFieldWriter> write)
    {
        using var writer = new CanonicalFieldWriter();
        write(writer);
        return writer.ToArray();
    }

    private sealed class CanonicalFieldWriter : IDisposable
    {
        private readonly MemoryStream _stream = new();

        public void Field(string name, string value) { Token(name); Token(value); }
        public void Field(string name, Guid value) => Field(name, value.ToString("N"));
        public void Field(string name, int value) => Field(name, value.ToString(CultureInfo.InvariantCulture));
        public void Field(string name, long value) => Field(name, value.ToString(CultureInfo.InvariantCulture));
        public void Field(string name, bool value) => Field(name, value ? "true" : "false");
        public void Field(string name, DateTimeOffset value) => Field(name, value.ToString("O", CultureInfo.InvariantCulture));
        public void NullableField(string name, string? value)
        {
            Field($"{name}.present", value is not null);
            if (value is not null) Field($"{name}.value", value);
        }

        public void Token(string value)
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
            finally { CryptographicOperations.ZeroMemory(bytes); }
        }

        public byte[] ToArray() => _stream.ToArray();
        public void Dispose() => _stream.Dispose();
    }
}
