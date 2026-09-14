CREATE SCHEMA __SCHEMA__;

CREATE TABLE __SCHEMA__.source_bindings (
    soul_id text CONSTRAINT source_bindings_soul_id_not_null NOT NULL,
    source_id text CONSTRAINT source_bindings_source_id_not_null NOT NULL,
    algorithm text CONSTRAINT source_bindings_algorithm_not_null NOT NULL,
    nonce bigint CONSTRAINT source_bindings_nonce_not_null NOT NULL,
    soul_hash char(64) CONSTRAINT source_bindings_soul_hash_not_null NOT NULL,
    allocated_at timestamptz CONSTRAINT source_bindings_allocated_at_not_null NOT NULL,
    binding_revision char(64) CONSTRAINT source_bindings_binding_revision_not_null NOT NULL,
    binding_checksum char(64) CONSTRAINT source_bindings_binding_checksum_not_null NOT NULL,
    canonical_json text CONSTRAINT source_bindings_canonical_json_not_null NOT NULL,
    binding_json jsonb CONSTRAINT source_bindings_binding_json_not_null NOT NULL,
    created_at timestamptz CONSTRAINT source_bindings_created_at_not_null NOT NULL DEFAULT pg_catalog.clock_timestamp(),
    CONSTRAINT source_bindings_pkey PRIMARY KEY (soul_id),
    CONSTRAINT source_bindings_source_id_key UNIQUE (source_id),
    CONSTRAINT source_bindings_binding_revision_key UNIQUE (binding_revision),
    CONSTRAINT source_bindings_binding_checksum_key UNIQUE (binding_checksum),
    CONSTRAINT source_bindings_identity_proof_key
        UNIQUE (soul_id, source_id, binding_revision, binding_checksum),
    CONSTRAINT source_bindings_soul_id_format
        CHECK (soul_id ~ '^soul_[a-f0-9]{64}$'),
    CONSTRAINT source_bindings_source_id_format
        CHECK (source_id ~ '^dps-[a-f0-9]{28}$'),
    CONSTRAINT source_bindings_algorithm_fixed
        CHECK (algorithm = 'dps.gbrain-source-binding.sha256-nonce/v1'),
    CONSTRAINT source_bindings_nonce_bounded
        CHECK (nonce BETWEEN 0 AND 1023),
    CONSTRAINT source_bindings_soul_hash_format
        CHECK (soul_hash ~ '^[a-f0-9]{64}$'),
    CONSTRAINT source_bindings_soul_hash_matches
        CHECK (soul_hash = pg_catalog.substr(soul_id, 6, 64)),
    CONSTRAINT source_bindings_binding_revision_format
        CHECK (binding_revision ~ '^[a-f0-9]{64}$'),
    CONSTRAINT source_bindings_binding_checksum_format
        CHECK (binding_checksum ~ '^[a-f0-9]{64}$'),
    CONSTRAINT source_bindings_canonical_present
        CHECK (pg_catalog.length(canonical_json) > 0),
    CONSTRAINT source_bindings_json_object
        CHECK (pg_catalog.jsonb_typeof(binding_json) = 'object'),
    CONSTRAINT source_bindings_canonical_json_matches
        CHECK (binding_json = canonical_json::jsonb)
);

CREATE TABLE __SCHEMA__.source_binding_quarantine (
    quarantine_id uuid CONSTRAINT source_binding_quarantine_quarantine_id_not_null NOT NULL,
    soul_id text CONSTRAINT source_binding_quarantine_soul_id_not_null NOT NULL,
    soul_hash char(64) CONSTRAINT source_binding_quarantine_soul_hash_not_null NOT NULL,
    maximum_nonce bigint CONSTRAINT source_binding_quarantine_maximum_nonce_not_null NOT NULL,
    reason text CONSTRAINT source_binding_quarantine_reason_not_null NOT NULL,
    created_at timestamptz CONSTRAINT source_binding_quarantine_created_at_not_null NOT NULL DEFAULT pg_catalog.clock_timestamp(),
    CONSTRAINT source_binding_quarantine_pkey PRIMARY KEY (quarantine_id),
    CONSTRAINT source_binding_quarantine_soul_id_format
        CHECK (soul_id ~ '^soul_[a-f0-9]{64}$'),
    CONSTRAINT source_binding_quarantine_soul_hash_format
        CHECK (soul_hash ~ '^[a-f0-9]{64}$'),
    CONSTRAINT source_binding_quarantine_nonce_bounded
        CHECK (maximum_nonce BETWEEN 0 AND 1023),
    CONSTRAINT source_binding_quarantine_reason_bounded
        CHECK (pg_catalog.length(reason) BETWEEN 1 AND 256),
    CONSTRAINT source_binding_quarantine_soul_hash_matches
        CHECK (soul_hash = pg_catalog.substr(soul_id, 6, 64))
);

CREATE INDEX source_binding_quarantine_soul_idx
    ON __SCHEMA__.source_binding_quarantine (soul_id, created_at);

CREATE TABLE __SCHEMA__.rendered_revisions (
    soul_id text CONSTRAINT rendered_revisions_soul_id_not_null NOT NULL,
    source_id text CONSTRAINT rendered_revisions_source_id_not_null NOT NULL,
    source_binding_revision char(64) CONSTRAINT rendered_revisions_source_binding_revision_not_null NOT NULL,
    source_binding_checksum char(64) CONSTRAINT rendered_revisions_source_binding_checksum_not_null NOT NULL,
    projection_revision char(64) CONSTRAINT rendered_revisions_projection_revision_not_null NOT NULL,
    projection_checksum char(64) CONSTRAINT rendered_revisions_projection_checksum_not_null NOT NULL,
    source_event_count integer CONSTRAINT rendered_revisions_source_event_count_not_null NOT NULL,
    occurred_at timestamptz CONSTRAINT rendered_revisions_occurred_at_not_null NOT NULL,
    canonical_json text CONSTRAINT rendered_revisions_canonical_json_not_null NOT NULL,
    projection_json jsonb CONSTRAINT rendered_revisions_projection_json_not_null NOT NULL,
    created_at timestamptz CONSTRAINT rendered_revisions_created_at_not_null NOT NULL DEFAULT pg_catalog.clock_timestamp(),
    CONSTRAINT rendered_revisions_pkey PRIMARY KEY (soul_id, projection_revision),
    CONSTRAINT rendered_revisions_source_revision_key UNIQUE (source_id, projection_revision),
    CONSTRAINT rendered_revisions_soul_checksum_key UNIQUE (soul_id, projection_checksum),
    CONSTRAINT rendered_revisions_soul_event_count_key UNIQUE (soul_id, source_event_count),
    CONSTRAINT rendered_revisions_projection_revision_format
        CHECK (projection_revision ~ '^[a-f0-9]{64}$'),
    CONSTRAINT rendered_revisions_projection_checksum_format
        CHECK (projection_checksum ~ '^[a-f0-9]{64}$'),
    CONSTRAINT rendered_revisions_source_event_count_nonnegative
        CHECK (source_event_count >= 0),
    CONSTRAINT rendered_revisions_canonical_present
        CHECK (pg_catalog.length(canonical_json) > 0),
    CONSTRAINT rendered_revisions_json_object
        CHECK (pg_catalog.jsonb_typeof(projection_json) = 'object'),
    CONSTRAINT rendered_revisions_canonical_json_matches
        CHECK (projection_json = canonical_json::jsonb),
    CONSTRAINT rendered_revisions_source_binding_fkey
        FOREIGN KEY (soul_id, source_id, source_binding_revision, source_binding_checksum)
        REFERENCES __SCHEMA__.source_bindings(soul_id, source_id, binding_revision, binding_checksum)
);

CREATE INDEX rendered_revisions_latest_idx
    ON __SCHEMA__.rendered_revisions (soul_id, source_event_count DESC, created_at DESC);

CREATE FUNCTION __SCHEMA__.reject_gbrain_projector_mutation()
RETURNS trigger
LANGUAGE plpgsql
SECURITY INVOKER
AS $$
BEGIN
    RAISE EXCEPTION 'gbrain-projector identity and revision ledgers are append-only';
END;
$$;

REVOKE ALL ON FUNCTION __SCHEMA__.reject_gbrain_projector_mutation()
FROM PUBLIC, __RUNTIME_ROLE__;

CREATE TRIGGER source_bindings_append_only_rows
BEFORE UPDATE OR DELETE ON __SCHEMA__.source_bindings
FOR EACH ROW EXECUTE FUNCTION __SCHEMA__.reject_gbrain_projector_mutation();

CREATE TRIGGER source_bindings_append_only_truncate
BEFORE TRUNCATE ON __SCHEMA__.source_bindings
FOR EACH STATEMENT EXECUTE FUNCTION __SCHEMA__.reject_gbrain_projector_mutation();

CREATE TRIGGER source_binding_quarantine_append_only_rows
BEFORE UPDATE OR DELETE ON __SCHEMA__.source_binding_quarantine
FOR EACH ROW EXECUTE FUNCTION __SCHEMA__.reject_gbrain_projector_mutation();

CREATE TRIGGER source_binding_quarantine_append_only_truncate
BEFORE TRUNCATE ON __SCHEMA__.source_binding_quarantine
FOR EACH STATEMENT EXECUTE FUNCTION __SCHEMA__.reject_gbrain_projector_mutation();

CREATE TRIGGER rendered_revisions_append_only_rows
BEFORE UPDATE OR DELETE ON __SCHEMA__.rendered_revisions
FOR EACH ROW EXECUTE FUNCTION __SCHEMA__.reject_gbrain_projector_mutation();

CREATE TRIGGER rendered_revisions_append_only_truncate
BEFORE TRUNCATE ON __SCHEMA__.rendered_revisions
FOR EACH STATEMENT EXECUTE FUNCTION __SCHEMA__.reject_gbrain_projector_mutation();

REVOKE ALL ON SCHEMA __SCHEMA__ FROM PUBLIC;
REVOKE ALL ON TABLE
    __SCHEMA__.source_bindings,
    __SCHEMA__.source_binding_quarantine,
    __SCHEMA__.rendered_revisions
FROM PUBLIC, __RUNTIME_ROLE__;

GRANT USAGE ON SCHEMA __SCHEMA__ TO __RUNTIME_ROLE__;
GRANT SELECT, INSERT ON TABLE
    __SCHEMA__.source_bindings,
    __SCHEMA__.source_binding_quarantine,
    __SCHEMA__.rendered_revisions
TO __RUNTIME_ROLE__;
