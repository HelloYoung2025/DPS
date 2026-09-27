-- Fixed cold bootstrap only: run immediately after immutable 001 in its transaction.
-- Restrict this migrator's future functions in this dedicated database, across schemas.
ALTER DEFAULT PRIVILEGES REVOKE EXECUTE ON FUNCTIONS FROM PUBLIC;

ALTER TABLE __SCHEMA__.migration_ledger
    DROP CONSTRAINT migration_schema_version_exact,
    ADD CONSTRAINT migration_schema_version_exact CHECK (schema_version IN ('1', '2'));
ALTER TABLE __SCHEMA__.catalog_attestations
    DROP CONSTRAINT catalog_schema_version_exact,
    ADD CONSTRAINT catalog_schema_version_exact CHECK (schema_version = '2');

INSERT INTO __SCHEMA__.migration_ledger(
    schema_version, migration_sha256, runtime_capability_sha256, migrator_role,
    server_version_num)
VALUES (
    '2', '__MIGRATION_SHA256__', '__RUNTIME_CAPABILITY_SHA256__', current_user,
    current_setting('server_version_num')::integer);

CREATE OR REPLACE FUNCTION __SCHEMA__.assert_runtime_capability(p_runtime_capability bytea)
RETURNS void
LANGUAGE plpgsql
STABLE
SECURITY DEFINER
SET search_path = pg_catalog
SET row_security = off
AS $function$
BEGIN
    IF p_runtime_capability IS NULL
       OR pg_catalog.octet_length(p_runtime_capability) <> 32 THEN
        RAISE EXCEPTION 'runtime database capability is missing or invalid'
            USING ERRCODE = '42501';
    END IF;
    IF NOT EXISTS (
        SELECT 1
        FROM __SCHEMA__.migration_ledger AS ledger
        WHERE ledger.schema_version = '2'
          AND ledger.runtime_capability_sha256 =
              pg_catalog.encode(pg_catalog.sha256(p_runtime_capability), 'hex')) THEN
        RAISE EXCEPTION 'runtime database capability is missing or invalid'
            USING ERRCODE = '42501';
    END IF;
END
$function$;

CREATE OR REPLACE FUNCTION __SCHEMA__.api_runtime_attestation(p_runtime_capability bytea)
RETURNS TABLE(
    schema_version text, migration_sha256 text, catalog_sha256 text,
    server_version_num integer, migrator_role text, runtime_role text)
LANGUAGE plpgsql
STABLE
SECURITY DEFINER
SET search_path = pg_catalog
SET row_security = off
AS $function$
BEGIN
    PERFORM __SCHEMA__.assert_runtime_capability(p_runtime_capability);
    RETURN QUERY
    SELECT ledger.schema_version, ledger.migration_sha256, attestation.catalog_sha256,
           current_setting('server_version_num')::integer, ledger.migrator_role, session_user::text
    FROM __SCHEMA__.migration_ledger AS ledger
    JOIN LATERAL (
        SELECT value.catalog_sha256
        FROM __SCHEMA__.catalog_attestations AS value
        WHERE value.migration_sha256 = ledger.migration_sha256
          AND value.schema_version = ledger.schema_version
        ORDER BY value.recorded_at DESC
        LIMIT 1
    ) AS attestation ON true
    WHERE ledger.schema_version = '2';
END
$function$;
