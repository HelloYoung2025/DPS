namespace Dps.SoulRegistry.Contracts;

// Four-role installation; v1 remains a frozen historical codec.
public sealed record SoulCurrentConfigurationV2
{
    public const string ContractId = "soul.current.configuration/v2";
    private static readonly string[] Fields = ["schema_version", "contract_id", "configuration_id", "tenant_ref", "soul_id", "principal_id", "consumer_module", "permission", "host_uid", "host_gid", "provider_uid", "provider_gid", "host_auth_uid", "host_auth_gid", "host_auth_key_id", "host_auth_spki_base64", "current_service_uid", "current_service_gid", "current_key_id", "current_spki_base64", "status_key_id", "status_spki_base64", "database_host", "database_port", "database_name", "database_schema", "database_runtime_role", "database_secret_id", "signer_database_runtime_role", "signer_database_secret_id", "deadline_ms"];
    public required Guid ConfigurationId { get; init; }
    public required string TenantRef { get; init; }
    public required string SoulId { get; init; }
    public required string PrincipalId { get; init; }
    public required string ConsumerModule { get; init; }
    public required string Permission { get; init; }
    public required long HostUid { get; init; }
    public required long HostGid { get; init; }
    public required long ProviderUid { get; init; }
    public required long ProviderGid { get; init; }
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
            ("schema_version", "2.0.0"), ("contract_id", ContractId),
            ("configuration_id", ConfigurationId),
            ("tenant_ref", TenantRef),
            ("soul_id", SoulId),
            ("principal_id", PrincipalId),
            ("consumer_module", ConsumerModule),
            ("permission", Permission),
            ("host_uid", HostUid),
            ("host_gid", HostGid),
            ("provider_uid", ProviderUid),
            ("provider_gid", ProviderGid),
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
    public static SoulCurrentConfigurationV2 FromUtf8Json(ReadOnlySpan<byte> bytes)
    {
        var root = SoulCurrentFormat.Parse(bytes, Fields, ContractId);
        return new SoulCurrentConfigurationV2
        {
            ConfigurationId = SoulIdentityWire.Uuid(root, "configuration_id"),
            TenantRef = SoulIdentityWire.Text(root, "tenant_ref"),
            SoulId = SoulIdentityWire.Text(root, "soul_id"),
            PrincipalId = SoulIdentityWire.Text(root, "principal_id"),
            ConsumerModule = SoulIdentityWire.Text(root, "consumer_module"),
            Permission = SoulIdentityWire.Text(root, "permission"),
            HostUid = SoulIdentityWire.Integer(root, "host_uid", true),
            HostGid = SoulIdentityWire.Integer(root, "host_gid", true),
            ProviderUid = SoulIdentityWire.Integer(root, "provider_uid", true),
            ProviderGid = SoulIdentityWire.Integer(root, "provider_gid", true),
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
