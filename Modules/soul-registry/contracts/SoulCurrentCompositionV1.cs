using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace Dps.SoulRegistry.Contracts;

// These are untrusted installation/transport records; parsing never installs a root or issues a lease.
public sealed record SoulCurrentBootstrapV1
{
    public const string ContractId = "soul.current.bootstrap/v1";
    private static readonly string[] Fields = ["schema_version", "contract_id", "installation_id", "enabled", "root_key_id", "root_spki_base64", "active_configuration_sha256", "active_release_bom_sha256", "active_release_generation"];
    public required Guid InstallationId { get; init; }
    public required bool Enabled { get; init; }
    public required string RootKeyId { get; init; }
    public required string RootSpkiBase64 { get; init; }
    public required string ActiveConfigurationSha256 { get; init; }
    public required string ActiveReleaseBomSha256 { get; init; }
    public required long ActiveReleaseGeneration { get; init; }
    public byte[] ToUtf8Json()
    {
        var bytes = SoulCurrentFormat.Write([
            ("schema_version", "1.0.0"), ("contract_id", ContractId),
            ("installation_id", InstallationId),
            ("enabled", Enabled),
            ("root_key_id", RootKeyId),
            ("root_spki_base64", RootSpkiBase64),
            ("active_configuration_sha256", ActiveConfigurationSha256),
            ("active_release_bom_sha256", ActiveReleaseBomSha256),
            ("active_release_generation", ActiveReleaseGeneration)
        ]);
        _ = FromUtf8Json(bytes);
        return bytes;
    }
    public static SoulCurrentBootstrapV1 FromUtf8Json(ReadOnlySpan<byte> bytes)
    {
        var root = SoulCurrentFormat.Parse(bytes, Fields, ContractId);
        return new SoulCurrentBootstrapV1
        {
            InstallationId = SoulIdentityWire.Uuid(root, "installation_id"),
            Enabled = root.GetProperty("enabled").GetBoolean(),
            RootKeyId = SoulIdentityWire.Text(root, "root_key_id"),
            RootSpkiBase64 = SoulIdentityWire.Text(root, "root_spki_base64"),
            ActiveConfigurationSha256 = SoulIdentityWire.Text(root, "active_configuration_sha256"),
            ActiveReleaseBomSha256 = SoulIdentityWire.Text(root, "active_release_bom_sha256"),
            ActiveReleaseGeneration = SoulIdentityWire.Integer(root, "active_release_generation", false),
        };
    }
}

public sealed record SoulCurrentConfigurationV1
{
    public const string ContractId = "soul.current.configuration/v1";
    private static readonly string[] Fields = ["schema_version", "contract_id", "configuration_id", "tenant_ref", "soul_id", "principal_id", "consumer_module", "permission", "host_uid", "host_gid", "host_auth_uid", "host_auth_gid", "host_auth_key_id", "host_auth_spki_base64", "current_service_uid", "current_service_gid", "current_key_id", "current_spki_base64", "status_key_id", "status_spki_base64", "database_host", "database_port", "database_name", "database_schema", "database_runtime_role", "database_secret_id", "signer_database_runtime_role", "signer_database_secret_id", "deadline_ms"];
    public required Guid ConfigurationId { get; init; }
    public required string TenantRef { get; init; }
    public required string SoulId { get; init; }
    public required string PrincipalId { get; init; }
    public required string ConsumerModule { get; init; }
    public required string Permission { get; init; }
    public required long HostUid { get; init; }
    public required long HostGid { get; init; }
    public required long HostAuthUid { get; init; }
    public required long HostAuthGid { get; init; }
    public required string HostAuthKeyId { get; init; }
    public required string HostAuthSpkiBase64 { get; init; }
    public required long CurrentServiceUid { get; init; }
    public required long CurrentServiceGid { get; init; }
    public required string CurrentKeyId { get; init; }
    public required string CurrentSpkiBase64 { get; init; }
    public required string StatusKeyId { get; init; }
    public required string StatusSpkiBase64 { get; init; }
    public required string DatabaseHost { get; init; }
    public required long DatabasePort { get; init; }
    public required string DatabaseName { get; init; }
    public required string DatabaseSchema { get; init; }
    public required string DatabaseRuntimeRole { get; init; }
    public required string DatabaseSecretId { get; init; }
    public required string SignerDatabaseRuntimeRole { get; init; }
    public required string SignerDatabaseSecretId { get; init; }
    public required long DeadlineMs { get; init; }
    public byte[] ToUtf8Json()
    {
        var bytes = SoulCurrentFormat.Write([
            ("schema_version", "1.0.0"), ("contract_id", ContractId),
            ("configuration_id", ConfigurationId),
            ("tenant_ref", TenantRef),
            ("soul_id", SoulId),
            ("principal_id", PrincipalId),
            ("consumer_module", ConsumerModule),
            ("permission", Permission),
            ("host_uid", HostUid),
            ("host_gid", HostGid),
            ("host_auth_uid", HostAuthUid),
            ("host_auth_gid", HostAuthGid),
            ("host_auth_key_id", HostAuthKeyId),
            ("host_auth_spki_base64", HostAuthSpkiBase64),
            ("current_service_uid", CurrentServiceUid),
            ("current_service_gid", CurrentServiceGid),
            ("current_key_id", CurrentKeyId),
            ("current_spki_base64", CurrentSpkiBase64),
            ("status_key_id", StatusKeyId),
            ("status_spki_base64", StatusSpkiBase64),
            ("database_host", DatabaseHost),
            ("database_port", DatabasePort),
            ("database_name", DatabaseName),
            ("database_schema", DatabaseSchema),
            ("database_runtime_role", DatabaseRuntimeRole),
            ("database_secret_id", DatabaseSecretId),
            ("signer_database_runtime_role", SignerDatabaseRuntimeRole),
            ("signer_database_secret_id", SignerDatabaseSecretId),
            ("deadline_ms", DeadlineMs)
        ]);
        _ = FromUtf8Json(bytes);
        return bytes;
    }
    public static SoulCurrentConfigurationV1 FromUtf8Json(ReadOnlySpan<byte> bytes)
    {
        var root = SoulCurrentFormat.Parse(bytes, Fields, ContractId);
        return new SoulCurrentConfigurationV1
        {
            ConfigurationId = SoulIdentityWire.Uuid(root, "configuration_id"),
            TenantRef = SoulIdentityWire.Text(root, "tenant_ref"),
            SoulId = SoulIdentityWire.Text(root, "soul_id"),
            PrincipalId = SoulIdentityWire.Text(root, "principal_id"),
            ConsumerModule = SoulIdentityWire.Text(root, "consumer_module"),
            Permission = SoulIdentityWire.Text(root, "permission"),
            HostUid = SoulIdentityWire.Integer(root, "host_uid", true),
            HostGid = SoulIdentityWire.Integer(root, "host_gid", true),
            HostAuthUid = SoulIdentityWire.Integer(root, "host_auth_uid", true),
            HostAuthGid = SoulIdentityWire.Integer(root, "host_auth_gid", true),
            HostAuthKeyId = SoulIdentityWire.Text(root, "host_auth_key_id"),
            HostAuthSpkiBase64 = SoulIdentityWire.Text(root, "host_auth_spki_base64"),
            CurrentServiceUid = SoulIdentityWire.Integer(root, "current_service_uid", true),
            CurrentServiceGid = SoulIdentityWire.Integer(root, "current_service_gid", true),
            CurrentKeyId = SoulIdentityWire.Text(root, "current_key_id"),
            CurrentSpkiBase64 = SoulIdentityWire.Text(root, "current_spki_base64"),
            StatusKeyId = SoulIdentityWire.Text(root, "status_key_id"),
            StatusSpkiBase64 = SoulIdentityWire.Text(root, "status_spki_base64"),
            DatabaseHost = SoulIdentityWire.Text(root, "database_host"),
            DatabasePort = SoulIdentityWire.Integer(root, "database_port", false),
            DatabaseName = SoulIdentityWire.Text(root, "database_name"),
            DatabaseSchema = SoulIdentityWire.Text(root, "database_schema"),
            DatabaseRuntimeRole = SoulIdentityWire.Text(root, "database_runtime_role"),
            DatabaseSecretId = SoulIdentityWire.Text(root, "database_secret_id"),
            SignerDatabaseRuntimeRole = SoulIdentityWire.Text(root, "signer_database_runtime_role"),
            SignerDatabaseSecretId = SoulIdentityWire.Text(root, "signer_database_secret_id"),
            DeadlineMs = SoulIdentityWire.Integer(root, "deadline_ms", false),
        };
    }
}

public sealed record SoulCurrentCompositionV1
{
    public const string ContractId = "soul.current.composition/v1";
    private static readonly string[] Fields = ["schema_version", "contract_id", "configuration_sha256", "host_artifact_sha256", "soul_artifact_sha256", "contracts_artifact_sha256", "release_bom_sha256", "release_generation", "issuer", "key_role", "key_id", "issued_at", "expires_at", "signature_algorithm", "signature_base64"];
    public required string ConfigurationSha256 { get; init; }
    public required string HostArtifactSha256 { get; init; }
    public required string SoulArtifactSha256 { get; init; }
    public required string ContractsArtifactSha256 { get; init; }
    public required string ReleaseBomSha256 { get; init; }
    public required long ReleaseGeneration { get; init; }
    public required string Issuer { get; init; }
    public required string KeyRole { get; init; }
    public required string KeyId { get; init; }
    public required DateTimeOffset IssuedAt { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public required string SignatureAlgorithm { get; init; }
    public required string SignatureBase64 { get; init; }
    public byte[] ToUtf8Json()
    {
        var bytes = SoulCurrentFormat.Write([
            ("schema_version", "1.0.0"), ("contract_id", ContractId),
            ("configuration_sha256", ConfigurationSha256),
            ("host_artifact_sha256", HostArtifactSha256),
            ("soul_artifact_sha256", SoulArtifactSha256),
            ("contracts_artifact_sha256", ContractsArtifactSha256),
            ("release_bom_sha256", ReleaseBomSha256),
            ("release_generation", ReleaseGeneration),
            ("issuer", Issuer),
            ("key_role", KeyRole),
            ("key_id", KeyId),
            ("issued_at", IssuedAt),
            ("expires_at", ExpiresAt),
            ("signature_algorithm", SignatureAlgorithm),
            ("signature_base64", SignatureBase64)
        ]);
        _ = FromUtf8Json(bytes);
        return bytes;
    }
    public static SoulCurrentCompositionV1 FromUtf8Json(ReadOnlySpan<byte> bytes)
    {
        var root = SoulCurrentFormat.Parse(bytes, Fields, ContractId);
        return new SoulCurrentCompositionV1
        {
            ConfigurationSha256 = SoulIdentityWire.Text(root, "configuration_sha256"),
            HostArtifactSha256 = SoulIdentityWire.Text(root, "host_artifact_sha256"),
            SoulArtifactSha256 = SoulIdentityWire.Text(root, "soul_artifact_sha256"),
            ContractsArtifactSha256 = SoulIdentityWire.Text(root, "contracts_artifact_sha256"),
            ReleaseBomSha256 = SoulIdentityWire.Text(root, "release_bom_sha256"),
            ReleaseGeneration = SoulIdentityWire.Integer(root, "release_generation", false),
            Issuer = SoulIdentityWire.Text(root, "issuer"),
            KeyRole = SoulIdentityWire.Text(root, "key_role"),
            KeyId = SoulIdentityWire.Text(root, "key_id"),
            IssuedAt = SoulIdentityWire.Time(root, "issued_at"),
            ExpiresAt = SoulIdentityWire.Time(root, "expires_at"),
            SignatureAlgorithm = SoulIdentityWire.Text(root, "signature_algorithm"),
            SignatureBase64 = SoulIdentityWire.Text(root, "signature_base64"),
        };
    }
    public byte[] CanonicalBytes() => SoulCurrentFormat.Frame(ToUtf8Json(), Fields, "dps.soul-registry.current.composition/v1");
}

public sealed record SoulCurrentAuthorityStateV1
{
    public const string ContractId = "soul.current.authority-state/v1";
    private static readonly string[] Fields = ["schema_version", "contract_id", "configuration_sha256", "tenant_ref", "soul_id", "principal_id", "permission", "status", "authorization_revision", "trust_epoch", "key_revocation_epoch", "release_bom_sha256", "release_generation", "host_auth_key_id", "current_key_id", "status_key_id"];
    public required string ConfigurationSha256 { get; init; }
    public required string TenantRef { get; init; }
    public required string SoulId { get; init; }
    public required string PrincipalId { get; init; }
    public required string Permission { get; init; }
    public required string Status { get; init; }
    public required long AuthorizationRevision { get; init; }
    public required long TrustEpoch { get; init; }
    public required long KeyRevocationEpoch { get; init; }
    public required string ReleaseBomSha256 { get; init; }
    public required long ReleaseGeneration { get; init; }
    public required string HostAuthKeyId { get; init; }
    public required string CurrentKeyId { get; init; }
    public required string StatusKeyId { get; init; }
    public byte[] ToUtf8Json()
    {
        var bytes = SoulCurrentFormat.Write([
            ("schema_version", "1.0.0"), ("contract_id", ContractId),
            ("configuration_sha256", ConfigurationSha256),
            ("tenant_ref", TenantRef),
            ("soul_id", SoulId),
            ("principal_id", PrincipalId),
            ("permission", Permission),
            ("status", Status),
            ("authorization_revision", AuthorizationRevision),
            ("trust_epoch", TrustEpoch),
            ("key_revocation_epoch", KeyRevocationEpoch),
            ("release_bom_sha256", ReleaseBomSha256),
            ("release_generation", ReleaseGeneration),
            ("host_auth_key_id", HostAuthKeyId),
            ("current_key_id", CurrentKeyId),
            ("status_key_id", StatusKeyId)
        ]);
        _ = FromUtf8Json(bytes);
        return bytes;
    }
    public static SoulCurrentAuthorityStateV1 FromUtf8Json(ReadOnlySpan<byte> bytes)
    {
        var root = SoulCurrentFormat.Parse(bytes, Fields, ContractId);
        return new SoulCurrentAuthorityStateV1
        {
            ConfigurationSha256 = SoulIdentityWire.Text(root, "configuration_sha256"),
            TenantRef = SoulIdentityWire.Text(root, "tenant_ref"),
            SoulId = SoulIdentityWire.Text(root, "soul_id"),
            PrincipalId = SoulIdentityWire.Text(root, "principal_id"),
            Permission = SoulIdentityWire.Text(root, "permission"),
            Status = SoulIdentityWire.Text(root, "status"),
            AuthorizationRevision = SoulIdentityWire.Integer(root, "authorization_revision", false),
            TrustEpoch = SoulIdentityWire.Integer(root, "trust_epoch", false),
            KeyRevocationEpoch = SoulIdentityWire.Integer(root, "key_revocation_epoch", true),
            ReleaseBomSha256 = SoulIdentityWire.Text(root, "release_bom_sha256"),
            ReleaseGeneration = SoulIdentityWire.Integer(root, "release_generation", false),
            HostAuthKeyId = SoulIdentityWire.Text(root, "host_auth_key_id"),
            CurrentKeyId = SoulIdentityWire.Text(root, "current_key_id"),
            StatusKeyId = SoulIdentityWire.Text(root, "status_key_id"),
        };
    }
}

public sealed record SoulCurrentStatusV1
{
    public const string ContractId = "soul.current.status/v1";
    private static readonly string[] Fields = ["schema_version", "contract_id", "request_id", "nonce", "query_sha256", "instance_id", "session_id", "configuration_sha256", "tenant_ref", "soul_id", "principal_id", "permission", "status", "authorization_revision", "trust_epoch", "key_revocation_epoch", "release_bom_sha256", "release_generation", "host_auth_key_id", "current_key_id", "status_key_id", "issued_at", "expires_at", "signature_algorithm", "signature_base64"];
    public required Guid RequestId { get; init; }
    public required string Nonce { get; init; }
    public required string QuerySha256 { get; init; }
    public required Guid InstanceId { get; init; }
    public required Guid SessionId { get; init; }
    public required string ConfigurationSha256 { get; init; }
    public required string TenantRef { get; init; }
    public required string SoulId { get; init; }
    public required string PrincipalId { get; init; }
    public required string Permission { get; init; }
    public required string Status { get; init; }
    public required long AuthorizationRevision { get; init; }
    public required long TrustEpoch { get; init; }
    public required long KeyRevocationEpoch { get; init; }
    public required string ReleaseBomSha256 { get; init; }
    public required long ReleaseGeneration { get; init; }
    public required string HostAuthKeyId { get; init; }
    public required string CurrentKeyId { get; init; }
    public required string StatusKeyId { get; init; }
    public required DateTimeOffset IssuedAt { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public required string SignatureAlgorithm { get; init; }
    public required string SignatureBase64 { get; init; }
    public byte[] ToUtf8Json()
    {
        var bytes = SoulCurrentFormat.Write([
            ("schema_version", "1.0.0"), ("contract_id", ContractId),
            ("request_id", RequestId),
            ("nonce", Nonce),
            ("query_sha256", QuerySha256),
            ("instance_id", InstanceId),
            ("session_id", SessionId),
            ("configuration_sha256", ConfigurationSha256),
            ("tenant_ref", TenantRef),
            ("soul_id", SoulId),
            ("principal_id", PrincipalId),
            ("permission", Permission),
            ("status", Status),
            ("authorization_revision", AuthorizationRevision),
            ("trust_epoch", TrustEpoch),
            ("key_revocation_epoch", KeyRevocationEpoch),
            ("release_bom_sha256", ReleaseBomSha256),
            ("release_generation", ReleaseGeneration),
            ("host_auth_key_id", HostAuthKeyId),
            ("current_key_id", CurrentKeyId),
            ("status_key_id", StatusKeyId),
            ("issued_at", IssuedAt),
            ("expires_at", ExpiresAt),
            ("signature_algorithm", SignatureAlgorithm),
            ("signature_base64", SignatureBase64)
        ]);
        _ = FromUtf8Json(bytes);
        return bytes;
    }
    public static SoulCurrentStatusV1 FromUtf8Json(ReadOnlySpan<byte> bytes)
    {
        var root = SoulCurrentFormat.Parse(bytes, Fields, ContractId);
        return new SoulCurrentStatusV1
        {
            RequestId = SoulIdentityWire.Uuid(root, "request_id"),
            Nonce = SoulIdentityWire.Text(root, "nonce"),
            QuerySha256 = SoulIdentityWire.Text(root, "query_sha256"),
            InstanceId = SoulIdentityWire.Uuid(root, "instance_id"),
            SessionId = SoulIdentityWire.Uuid(root, "session_id"),
            ConfigurationSha256 = SoulIdentityWire.Text(root, "configuration_sha256"),
            TenantRef = SoulIdentityWire.Text(root, "tenant_ref"),
            SoulId = SoulIdentityWire.Text(root, "soul_id"),
            PrincipalId = SoulIdentityWire.Text(root, "principal_id"),
            Permission = SoulIdentityWire.Text(root, "permission"),
            Status = SoulIdentityWire.Text(root, "status"),
            AuthorizationRevision = SoulIdentityWire.Integer(root, "authorization_revision", false),
            TrustEpoch = SoulIdentityWire.Integer(root, "trust_epoch", false),
            KeyRevocationEpoch = SoulIdentityWire.Integer(root, "key_revocation_epoch", true),
            ReleaseBomSha256 = SoulIdentityWire.Text(root, "release_bom_sha256"),
            ReleaseGeneration = SoulIdentityWire.Integer(root, "release_generation", false),
            HostAuthKeyId = SoulIdentityWire.Text(root, "host_auth_key_id"),
            CurrentKeyId = SoulIdentityWire.Text(root, "current_key_id"),
            StatusKeyId = SoulIdentityWire.Text(root, "status_key_id"),
            IssuedAt = SoulIdentityWire.Time(root, "issued_at"),
            ExpiresAt = SoulIdentityWire.Time(root, "expires_at"),
            SignatureAlgorithm = SoulIdentityWire.Text(root, "signature_algorithm"),
            SignatureBase64 = SoulIdentityWire.Text(root, "signature_base64"),
        };
    }
    public byte[] CanonicalBytes() => SoulCurrentFormat.Frame(ToUtf8Json(), Fields, "dps.soul-registry.current.status/v1");
}

internal static class SoulCurrentFormat
{
    internal static JsonElement Parse(ReadOnlySpan<byte> bytes, string[] fields, string contract)
    {
        var root = SoulIdentityWire.Parse(bytes, fields);
        SoulIdentityWire.Exact(root, "schema_version", "1.0.0");
        SoulIdentityWire.Exact(root, "contract_id", contract);
        foreach (var property in root.EnumerateObject())
        {
            var name = property.Name;
            if (name is "schema_version" or "contract_id") continue;
            if (name == "enabled")
            {
                if (property.Value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                    throw new ArgumentException("Expected a boolean.");
            }
            else if (name is "installation_id" or "configuration_id" or "request_id" or "instance_id" or "session_id")
                _ = SoulIdentityWire.Uuid(root, name);
            else if (name is "issued_at" or "expires_at") _ = SoulIdentityWire.Time(root, name);
            else if (name.EndsWith("_uid", StringComparison.Ordinal) || name.EndsWith("_gid", StringComparison.Ordinal))
            {
                if (SoulIdentityWire.Integer(root, name, true) >= uint.MaxValue) throw new ArgumentException("Invalid Unix identity.");
            }
            else if (name is "release_generation" or "active_release_generation" or "authorization_revision" or "trust_epoch" or "key_revocation_epoch" or "database_port" or "deadline_ms")
                _ = SoulIdentityWire.Integer(root, name, name == "key_revocation_epoch");
            else if (name is "status" or "signature_algorithm") _ = SoulIdentityWire.Text(root, name);
            else if (name.EndsWith("_key_id", StringComparison.Ordinal) || name == "key_id")
                SoulIdentityWire.Pattern(SoulIdentityWire.Text(root, name), "[a-z0-9][a-z0-9._-]{0,63}");
            else if (name.EndsWith("_sha256", StringComparison.Ordinal))
                SoulIdentityWire.Pattern(SoulIdentityWire.Text(root, name), "[a-f0-9]{64}");
            else if (name.EndsWith("_spki_base64", StringComparison.Ordinal))
                ValidatePublicKey(SoulIdentityWire.Text(root, name));
            else if (name == "signature_base64") _ = Base64(SoulIdentityWire.Text(root, name), 64);
            else if (name == "tenant_ref") SoulIdentityWire.Pattern(SoulIdentityWire.Text(root, name), "tenant_[a-f0-9]{64}");
            else if (name == "soul_id") SoulIdentityWire.Pattern(SoulIdentityWire.Text(root, name), "soul_[a-f0-9]{64}");
            else if (name == "nonce") SoulIdentityWire.Pattern(SoulIdentityWire.Text(root, name), "nonce_[a-f0-9]{64}");
            else SoulIdentityWire.Pattern(SoulIdentityWire.Text(root, name), "[a-z0-9][a-z0-9._/-]{0,127}");
        }
        if (root.TryGetProperty("permission", out _)) SoulIdentityWire.Exact(root, "permission", "soul-current.identity-read");
        if (root.TryGetProperty("status", out _) && SoulIdentityWire.Text(root, "status") is not ("ACTIVE" or "REVOKED"))
            throw new ArgumentException("Unknown authority status.");
        if (root.TryGetProperty("signature_algorithm", out _)) SoulIdentityWire.Exact(root, "signature_algorithm", SoulIdentityWire.Algorithm);
        if (contract == SoulCurrentConfigurationV1.ContractId)
        {
            SoulIdentityWire.Exact(root, "consumer_module", "control-plane-host");
            // Only local, explicitly port-bound PostgreSQL is supported by this closed slice.
            if (SoulIdentityWire.Text(root, "database_host") != "127.0.0.1" ||
                SoulIdentityWire.Integer(root, "database_port") is < 1024 or > 65535 or 55434 ||
                SoulIdentityWire.Integer(root, "deadline_ms") != 5000)
                throw new ArgumentException("Unsupported database target or deadline.");
            foreach (var n in new[] { "database_name", "database_schema", "database_runtime_role", "signer_database_runtime_role", "database_secret_id", "signer_database_secret_id" })
                SoulIdentityWire.Pattern(SoulIdentityWire.Text(root, n), "[a-z][a-z0-9_]{0,62}");
            if (SoulIdentityWire.Text(root, "database_runtime_role") == SoulIdentityWire.Text(root, "signer_database_runtime_role") ||
                SoulIdentityWire.Text(root, "database_secret_id") == SoulIdentityWire.Text(root, "signer_database_secret_id"))
                throw new ArgumentException("Provider and signer credentials must be distinct.");
            var keys = new[] { "host_auth_key_id", "current_key_id", "status_key_id" };
            if (keys.Select(n => SoulIdentityWire.Text(root, n)).Distinct(StringComparer.Ordinal).Count() != keys.Length ||
                new[] { "host_auth_spki_base64", "current_spki_base64", "status_spki_base64" }
                    .Select(n => SoulIdentityWire.Text(root, n)).Distinct(StringComparer.Ordinal).Count() != 3)
                throw new ArgumentException("Signing roles must have distinct keys.");
        }
        if (contract == SoulCurrentCompositionV1.ContractId)
        {
            SoulIdentityWire.Exact(root, "issuer", "dps-soul-current-installation");
            SoulIdentityWire.Exact(root, "key_role", "installation-root");
        }
        if (root.TryGetProperty("issued_at", out _))
        {
            var window = SoulIdentityWire.Time(root, "expires_at") - SoulIdentityWire.Time(root, "issued_at");
            if (window <= TimeSpan.Zero || window > (contract == SoulCurrentStatusV1.ContractId ? TimeSpan.FromSeconds(2) : TimeSpan.FromDays(1)))
                throw new ArgumentException("Invalid authority window.");
        }
        return root;
    }

    internal static byte[] Base64(string text, int? exactLength = null)
    {
        var bytes = Convert.FromBase64String(text);
        if (Convert.ToBase64String(bytes) != text || (exactLength is not null && bytes.Length != exactLength))
            throw new ArgumentException("Noncanonical Base64 or length.");
        return bytes;
    }

    private static void ValidatePublicKey(string text)
    {
        var bytes = Base64(text);
        using var key = ECDsa.Create(); key.ImportSubjectPublicKeyInfo(bytes, out var read);
        if (read != bytes.Length || key.ExportParameters(false).Curve.Oid.Value != "1.2.840.10045.3.1.7" ||
            !key.ExportSubjectPublicKeyInfo().AsSpan().SequenceEqual(bytes))
            throw new ArgumentException("Expected canonical P-256 SPKI.");
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
                    case string text: _ = SoulIdentityWire.Utf8.GetBytes(text); writer.WriteString(name, text); break;
                    case Guid id: writer.WriteString(name, id.ToString("D")); break;
                    case long number: writer.WriteNumber(name, number); break;
                    case bool flag: writer.WriteBoolean(name, flag); break;
                    case DateTimeOffset time:
                        if (time.Offset != TimeSpan.Zero) throw new ArgumentException("UTC is required.");
                        writer.WriteString(name, time.ToString(SoulIdentityWire.UtcFormat, CultureInfo.InvariantCulture)); break;
                    default: throw new ArgumentException("Unsupported closed record value.");
                }
            }
            writer.WriteEndObject();
        }
        return stream.ToArray();
    }

    internal static byte[] Frame(byte[] bytes, string[] fields, string domain)
    {
        var root = SoulIdentityWire.Parse(bytes, fields);
        using var stream = new MemoryStream();
        void Field(string value)
        {
            var encoded = SoulIdentityWire.Utf8.GetBytes(value);
            Span<byte> length = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(length, checked((uint)encoded.Length));
            stream.Write(length); stream.Write(encoded);
        }
        Field(domain);
        foreach (var name in fields)
        {
            if (name == "signature_base64") continue;
            var value = root.GetProperty(name);
            Field(name is "installation_id" or "configuration_id" or "request_id" or "instance_id" or "session_id"
                ? SoulIdentityWire.Uuid(root, name).ToString("N")
                : value.ValueKind == JsonValueKind.Number ? value.GetInt64().ToString(CultureInfo.InvariantCulture)
                : value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() ? "true" : "false"
                : SoulIdentityWire.Text(root, name));
        }
        return stream.ToArray();
    }
}
