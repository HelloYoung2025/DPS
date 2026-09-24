using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using System.Text.RegularExpressions;

// The historical CLR namespace preserves source usage; Policy.Contracts owns this sole definition.
namespace Dps.CommandOrchestrator.Contracts
{
    public sealed record ExecutionAuthorizationV1(
        string SchemaVersion, string ContractId, string ProducerModule,
        string SignatureDomain, string CanonicalEncoding, string CommandDigestAlgorithm,
        string SignatureAlgorithm, string SignatureFormat, string SignatureEncoding,
        string CallerModule, string AuthScope, Guid CommandId, Guid LeaseId, int Attempt,
        string SoulId, string DeviceBindingId, string PlatformAccountId, string TraceId, string IdempotencyKey,
        DateTimeOffset OccurredAt, string PrivacyClass, string CommandSha256, string ReleaseBomSha256,
        long ActiveReleaseBomGeneration, string ActiveReleaseBomTokenSha256,
        DateTimeOffset ValidUntil, bool ShadowMode, string SignatureBase64)
    {
        public const string CurrentSchemaVersion = "1.0.0";
        public const string CurrentContractId = "execution.authorization/v1";
        public const string CurrentProducerModule = "command-orchestrator";
        public const string CurrentSignatureDomain = "dps.command-orchestrator.execution-authorization/v1";
        public const string CurrentCanonicalEncoding = "dps.canonical.uint32be-length-prefixed-utf8/v1";
        public const string CurrentCommandDigestAlgorithm = "sha256:dps.command-orchestrator.command-dispatch/v1";
        public const string CurrentSignatureAlgorithm = "ecdsa-p256-sha256";
        public const string CurrentSignatureFormat = "ieee-p1363-fixed-field-concatenation";
        public const string CurrentSignatureEncoding = "base64";
        public const string CurrentCallerModule = "command-orchestrator";
        public const string CurrentAuthScope = "executor:dispatch";

        public void ValidatePayload()
        {
            ExecutionAuthorizationGuardV1.RequireMajor(SchemaVersion, 1);
            ExecutionAuthorizationGuardV1.RequireExact(ContractId, CurrentContractId, nameof(ContractId));
            ExecutionAuthorizationGuardV1.RequireExact(ProducerModule, CurrentProducerModule, nameof(ProducerModule));
            ExecutionAuthorizationGuardV1.RequireExact(SignatureDomain, CurrentSignatureDomain, nameof(SignatureDomain));
            ExecutionAuthorizationGuardV1.RequireExact(CanonicalEncoding, CurrentCanonicalEncoding, nameof(CanonicalEncoding));
            ExecutionAuthorizationGuardV1.RequireExact(CommandDigestAlgorithm, CurrentCommandDigestAlgorithm, nameof(CommandDigestAlgorithm));
            ExecutionAuthorizationGuardV1.RequireExact(SignatureAlgorithm, CurrentSignatureAlgorithm, nameof(SignatureAlgorithm));
            ExecutionAuthorizationGuardV1.RequireExact(SignatureFormat, CurrentSignatureFormat, nameof(SignatureFormat));
            ExecutionAuthorizationGuardV1.RequireExact(SignatureEncoding, CurrentSignatureEncoding, nameof(SignatureEncoding));
            ExecutionAuthorizationGuardV1.RequireExact(CallerModule, CurrentCallerModule, nameof(CallerModule));
            ExecutionAuthorizationGuardV1.RequireExact(AuthScope, CurrentAuthScope, nameof(AuthScope));
            ExecutionAuthorizationGuardV1.RequireGuid(CommandId, nameof(CommandId));
            ExecutionAuthorizationGuardV1.RequireGuid(LeaseId, nameof(LeaseId));
            if (Attempt is < 1 or > 3) throw new InvalidOperationException("Authorization attempt must be between one and three.");
            ExecutionAuthorizationGuardV1.RequireScope(SoulId, DeviceBindingId, PlatformAccountId);
            ExecutionAuthorizationGuardV1.RequireTraceId(TraceId);
            ExecutionAuthorizationGuardV1.RequireIdempotencyKey(IdempotencyKey);
            ExecutionAuthorizationGuardV1.RequireUtc(OccurredAt, nameof(OccurredAt));
            ExecutionAuthorizationGuardV1.RequireExact(PrivacyClass, "internal", nameof(PrivacyClass));
            ExecutionAuthorizationGuardV1.RequireSha256(CommandSha256, nameof(CommandSha256));
            ExecutionAuthorizationGuardV1.RequireSha256(ReleaseBomSha256, nameof(ReleaseBomSha256));
            if (ActiveReleaseBomGeneration < 1) throw new ArgumentOutOfRangeException(nameof(ActiveReleaseBomGeneration));
            ExecutionAuthorizationGuardV1.RequireSha256(ActiveReleaseBomTokenSha256, nameof(ActiveReleaseBomTokenSha256));
            ExecutionAuthorizationGuardV1.RequireUtc(ValidUntil, nameof(ValidUntil));
            if (ValidUntil <= OccurredAt) throw new InvalidOperationException("Authorization must expire after it is issued.");
        }

        public void Validate()
        {
            ValidatePayload();
            byte[] signature;
            try { signature = Convert.FromBase64String(SignatureBase64); }
            catch (FormatException exception) { throw new ArgumentException("Signature must use Base64 encoding.", nameof(SignatureBase64), exception); }
            try
            {
                if (signature.Length != Dps.PolicyApproval.Contracts.ExecutionAuthorizationCanonicalV1.P1363SignatureSizeBytes)
                    throw new ArgumentException("Signature must be a 64-byte P-256 IEEE P1363 value.", nameof(SignatureBase64));
                if (!string.Equals(Convert.ToBase64String(signature), SignatureBase64, StringComparison.Ordinal))
                    throw new ArgumentException("Signature must use canonical Base64 without ignored whitespace or pad bits.", nameof(SignatureBase64));
            }
            finally { CryptographicOperations.ZeroMemory(signature); }
        }
    }

    internal static class ExecutionAuthorizationGuardV1
    {
        private static readonly Regex Soul = new("^soul_[a-f0-9]{64}\\z", RegexOptions.CultureInvariant); private static readonly Regex Device = new("^db_[a-f0-9]{32}\\z", RegexOptions.CultureInvariant); private static readonly Regex Account = new("^pa_[a-f0-9]{32}\\z", RegexOptions.CultureInvariant); private static readonly Regex Trace = new("^trace_[a-f0-9]{32}\\z", RegexOptions.CultureInvariant); private static readonly Regex Idempotency = new("^idem_[a-f0-9]{64}\\z", RegexOptions.CultureInvariant);
        public static void RequireMajor(string value, int expected) { RequireText(value, 32, nameof(value)); if (!Regex.IsMatch(value, $"\\A{expected}(?:\\.[0-9]+){{0,2}}\\z", RegexOptions.CultureInvariant)) throw new NotSupportedException($"Unsupported contract major '{value}'."); }
        public static void RequireExact(string actual, string expected, string name) { if (!string.Equals(actual, expected, StringComparison.Ordinal)) throw new NotSupportedException($"Unsupported {name} '{actual}'."); }
        public static void RequireGuid(Guid value, string name) { if (value == Guid.Empty) throw new ArgumentException($"{name} cannot be empty.", name); }
        public static void RequireScope(string soul, string device, string account) { if (!Soul.IsMatch(soul) || !Device.IsMatch(device) || !Account.IsMatch(account)) throw new ArgumentException("Invalid opaque identity scope."); }
        public static void RequireTraceId(string value) { if (!Trace.IsMatch(value)) throw new ArgumentException("Invalid opaque trace_id.", nameof(value)); }
        public static void RequireIdempotencyKey(string value) { if (!Idempotency.IsMatch(value)) throw new ArgumentException("Invalid opaque idempotency_key.", nameof(value)); }
        public static void RequireText(string value, int max, string name) { if (string.IsNullOrWhiteSpace(value) || value.Length > max) throw new ArgumentException($"Invalid {name}.", name); }
        public static void RequireUtc(DateTimeOffset value, string name) { if (value.Offset != TimeSpan.Zero) throw new ArgumentException($"{name} must be UTC.", name); }
        public static void RequireSha256(string value, string name) { if (value.Length != 64 || value.Any(character => !Uri.IsHexDigit(character)) || value.Any(char.IsUpper)) throw new ArgumentException($"{name} must be lowercase SHA-256.", name); }
    }
}

namespace Dps.PolicyApproval.Contracts
{
    using Dps.CommandOrchestrator.Contracts;

    public static class ExecutionAuthorizationCanonicalV1
    {
        public const int P1363SignatureSizeBytes = 64;
        private static readonly UTF8Encoding StrictUtf8 = new(false, true);

        public static string ComputeAuthorizationSha256(ExecutionAuthorizationV1 authorization)
        {
            var canonical = CanonicalAuthorizationBytes(authorization);
            try { return Convert.ToHexStringLower(SHA256.HashData(canonical)); }
            finally { CryptographicOperations.ZeroMemory(canonical); }
        }

        public static byte[] CanonicalAuthorizationBytes(ExecutionAuthorizationV1 authorization)
        {
            ArgumentNullException.ThrowIfNull(authorization);
            authorization.ValidatePayload();
            return Encode(writer =>
            {
                writer.Token(authorization.SignatureDomain);
                writer.Field("schema_version", authorization.SchemaVersion);
                writer.Field("contract_id", authorization.ContractId);
                writer.Field("producer_module", authorization.ProducerModule);
                writer.Field("signature_domain", authorization.SignatureDomain);
                writer.Field("canonical_encoding", authorization.CanonicalEncoding);
                writer.Field("command_digest_algorithm", authorization.CommandDigestAlgorithm);
                writer.Field("signature_algorithm", authorization.SignatureAlgorithm);
                writer.Field("signature_format", authorization.SignatureFormat);
                writer.Field("signature_encoding", authorization.SignatureEncoding);
                writer.Field("caller_module", authorization.CallerModule);
                writer.Field("auth_scope", authorization.AuthScope);
                writer.Field("command_id", authorization.CommandId);
                writer.Field("lease_id", authorization.LeaseId);
                writer.Field("attempt", authorization.Attempt);
                writer.Field("soul_id", authorization.SoulId);
                writer.Field("device_binding_id", authorization.DeviceBindingId);
                writer.Field("platform_account_id", authorization.PlatformAccountId);
                writer.Field("trace_id", authorization.TraceId);
                writer.Field("idempotency_key", authorization.IdempotencyKey);
                writer.Field("occurred_at", authorization.OccurredAt);
                writer.Field("privacy_class", authorization.PrivacyClass);
                writer.Field("command_sha256", authorization.CommandSha256);
                writer.Field("release_bom_sha256", authorization.ReleaseBomSha256);
                writer.Field("active_release_bom_generation", authorization.ActiveReleaseBomGeneration);
                writer.Field("active_release_bom_token_sha256", authorization.ActiveReleaseBomTokenSha256);
                writer.Field("valid_until", authorization.ValidUntil);
                writer.Field("shadow_mode", authorization.ShadowMode);
            });
        }

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
}
