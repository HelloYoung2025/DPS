-- New authority state is deliberately empty after upgrading 001/002. Legacy
-- caller-supplied verification rows never become trusted current identity.
CREATE TABLE IF NOT EXISTS __SCHEMA__.identity_verifications (
    tenant_ref text NOT NULL,
    nonce text NOT NULL,
    idempotency_key text NOT NULL,
    request_sha256 text NOT NULL,
    proof_id uuid NOT NULL,
    proof_wire bytea NOT NULL,
    alias_references jsonb NOT NULL,
    references_signature bytea NOT NULL,
    operation text NOT NULL,
    target_soul_id text NULL,
    expires_at timestamptz NOT NULL,
    consumed_at timestamptz NULL,
    PRIMARY KEY (tenant_ref, nonce),
    CONSTRAINT uq_identity_verification_idempotency UNIQUE (tenant_ref, idempotency_key),
    CONSTRAINT uq_identity_verification_proof UNIQUE (proof_id),
    CONSTRAINT ck_identity_verification_digest CHECK (request_sha256 ~ '^[a-f0-9]{64}$'),
    CONSTRAINT ck_identity_verification_refs CHECK (jsonb_typeof(alias_references) = 'array'),
    CONSTRAINT ck_identity_verification_operation CHECK (operation IN ('create', 'attach', 'resolve', 'revoke'))
);

CREATE TABLE IF NOT EXISTS __SCHEMA__.identity_authority_aliases (
    tenant_ref text NOT NULL,
    alias_kind text NOT NULL,
    alias_key_id text NOT NULL,
    alias_digest text NOT NULL,
    soul_id text NOT NULL,
    proof_id uuid NOT NULL REFERENCES __SCHEMA__.identity_verifications(proof_id),
    verified_at timestamptz NOT NULL,
    revoked_at timestamptz NULL,
    PRIMARY KEY (tenant_ref, alias_kind, alias_key_id, alias_digest),
    CONSTRAINT fk_authority_alias_soul FOREIGN KEY (tenant_ref, soul_id)
        REFERENCES __SCHEMA__.souls (tenant_id, soul_id),
    CONSTRAINT ck_authority_alias_kind CHECK (alias_kind IN ('email', 'phone', 'platform_id')),
    CONSTRAINT ck_authority_alias_digest CHECK (alias_digest ~ '^[a-f0-9]{64}$')
);
CREATE INDEX IF NOT EXISTS ix_authority_alias_soul
    ON __SCHEMA__.identity_authority_aliases (tenant_ref, soul_id, revoked_at);

CREATE TABLE IF NOT EXISTS __SCHEMA__.soul_current_state (
    tenant_ref text NOT NULL,
    soul_id text NOT NULL,
    resolution_revision bigint NOT NULL CHECK (resolution_revision > 0),
    revocation_epoch bigint NOT NULL CHECK (revocation_epoch >= 0),
    status text NOT NULL CHECK (status IN ('ACTIVE', 'REVOKED')),
    updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_ref, soul_id),
    CONSTRAINT fk_current_soul FOREIGN KEY (tenant_ref, soul_id)
        REFERENCES __SCHEMA__.souls (tenant_id, soul_id)
);

CREATE TABLE IF NOT EXISTS __SCHEMA__.identity_authority_receipts (
    tenant_ref text NOT NULL,
    idempotency_key text NOT NULL,
    proof_id uuid NOT NULL REFERENCES __SCHEMA__.identity_verifications(proof_id),
    proof_sha256 text NOT NULL CHECK (proof_sha256 ~ '^[a-f0-9]{64}$'),
    soul_id text NOT NULL,
    result_wire bytea NOT NULL,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_ref, idempotency_key),
    CONSTRAINT uq_authority_receipt_proof UNIQUE (proof_id),
    CONSTRAINT fk_authority_receipt_soul FOREIGN KEY (tenant_ref, soul_id)
        REFERENCES __SCHEMA__.souls (tenant_id, soul_id)
);

CREATE TABLE IF NOT EXISTS __SCHEMA__.identity_authority_events (
    event_id uuid PRIMARY KEY,
    tenant_ref text NOT NULL,
    soul_id text NOT NULL,
    proof_id uuid NOT NULL,
    event_kind text NOT NULL CHECK (event_kind IN ('create', 'attach', 'resolve', 'revoke')),
    proof_sha256 text NOT NULL CHECK (proof_sha256 ~ '^[a-f0-9]{64}$'),
    resolution_revision bigint NOT NULL CHECK (resolution_revision > 0),
    revocation_epoch bigint NOT NULL CHECK (revocation_epoch >= 0),
    occurred_at timestamptz NOT NULL,
    CONSTRAINT fk_authority_event_soul FOREIGN KEY (tenant_ref, soul_id)
        REFERENCES __SCHEMA__.souls (tenant_id, soul_id)
);

DROP TRIGGER IF EXISTS trg_authority_receipt_append_only ON __SCHEMA__.identity_authority_receipts;
CREATE TRIGGER trg_authority_receipt_append_only
BEFORE UPDATE OR DELETE ON __SCHEMA__.identity_authority_receipts
FOR EACH ROW EXECUTE FUNCTION __SCHEMA__.reject_receipt_mutation();
DROP TRIGGER IF EXISTS trg_authority_event_append_only ON __SCHEMA__.identity_authority_events;
CREATE TRIGGER trg_authority_event_append_only
BEFORE UPDATE OR DELETE ON __SCHEMA__.identity_authority_events
FOR EACH ROW EXECUTE FUNCTION __SCHEMA__.reject_receipt_mutation();
REVOKE UPDATE, DELETE ON __SCHEMA__.identity_authority_receipts FROM PUBLIC;
REVOKE UPDATE, DELETE ON __SCHEMA__.identity_authority_events FROM PUBLIC;
