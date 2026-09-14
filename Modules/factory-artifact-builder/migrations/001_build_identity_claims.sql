BEGIN;

CREATE SCHEMA factory_artifact;
REVOKE ALL ON SCHEMA factory_artifact FROM PUBLIC;

CREATE OR REPLACE FUNCTION factory_artifact.reject_build_identity_mutation()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
  RAISE EXCEPTION 'append-only artifact build identity cannot be mutated';
END
$$;

CREATE TABLE factory_artifact.build_identity_claim (
  build_id text
    CONSTRAINT build_identity_claim_pkey PRIMARY KEY
    CONSTRAINT build_identity_claim_build_id_check
      CHECK (build_id ~ '^[a-z0-9][a-z0-9._:-]{7,127}$'),
  claim_sha256 char(64) NOT NULL
    CONSTRAINT build_identity_claim_claim_sha256_check
      CHECK (claim_sha256 ~ '^[a-f0-9]{64}$'),
  request_sha256 char(64) NOT NULL
    CONSTRAINT build_identity_claim_request_sha256_check
      CHECK (request_sha256 ~ '^[a-f0-9]{64}$'),
  decision_sha256 char(64) NOT NULL
    CONSTRAINT build_identity_claim_decision_sha256_check
      CHECK (decision_sha256 ~ '^[a-f0-9]{64}$'),
  artifact_sha256 char(64) NOT NULL
    CONSTRAINT build_identity_claim_artifact_sha256_check
      CHECK (artifact_sha256 ~ '^[a-f0-9]{64}$'),
  source_tree_sha256 char(64) NOT NULL
    CONSTRAINT build_identity_claim_source_tree_sha256_check
      CHECK (source_tree_sha256 ~ '^[a-f0-9]{64}$'),
  module_id text NOT NULL
    CONSTRAINT build_identity_claim_module_id_check
      CHECK (module_id ~ '^[a-z0-9]+(?:-[a-z0-9]+)*$'),
  module_version text NOT NULL
    CONSTRAINT build_identity_claim_module_version_check
      CHECK (module_version ~ '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$'),
  integration_commit char(40) NOT NULL
    CONSTRAINT build_identity_claim_integration_commit_check
      CHECK (integration_commit ~ '^[a-f0-9]{40}$'),
  claimed_at timestamptz NOT NULL DEFAULT clock_timestamp()
);

CREATE TRIGGER reject_build_identity_update_delete
  BEFORE UPDATE OR DELETE
  ON factory_artifact.build_identity_claim
  FOR EACH ROW
  EXECUTE FUNCTION factory_artifact.reject_build_identity_mutation();

CREATE TRIGGER reject_build_identity_truncate
  BEFORE TRUNCATE
  ON factory_artifact.build_identity_claim
  FOR EACH STATEMENT
  EXECUTE FUNCTION factory_artifact.reject_build_identity_mutation();

CREATE OR REPLACE FUNCTION factory_artifact.claim_build_identity(
  requested_build_id text,
  requested_claim_sha256 text,
  requested_request_sha256 text,
  requested_decision_sha256 text,
  requested_artifact_sha256 text,
  requested_source_tree_sha256 text,
  requested_module_id text,
  requested_module_version text,
  requested_integration_commit text
)
RETURNS boolean
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = pg_catalog
AS $$
DECLARE
  existing factory_artifact.build_identity_claim%ROWTYPE;
BEGIN
  IF requested_build_id IS NULL
     OR requested_build_id !~ '^[a-z0-9][a-z0-9._:-]{7,127}$'
     OR requested_claim_sha256 IS NULL
     OR requested_claim_sha256 !~ '^[a-f0-9]{64}$'
     OR requested_request_sha256 IS NULL
     OR requested_request_sha256 !~ '^[a-f0-9]{64}$'
     OR requested_decision_sha256 IS NULL
     OR requested_decision_sha256 !~ '^[a-f0-9]{64}$'
     OR requested_artifact_sha256 IS NULL
     OR requested_artifact_sha256 !~ '^[a-f0-9]{64}$'
     OR requested_source_tree_sha256 IS NULL
     OR requested_source_tree_sha256 !~ '^[a-f0-9]{64}$'
     OR requested_module_id IS NULL
     OR requested_module_id !~ '^[a-z0-9]+(?:-[a-z0-9]+)*$'
     OR requested_module_version IS NULL
     OR requested_module_version !~ '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$'
     OR requested_integration_commit IS NULL
     OR requested_integration_commit !~ '^[a-f0-9]{40}$'
  THEN
    RAISE EXCEPTION 'invalid artifact build identity claim'
      USING ERRCODE = '22023';
  END IF;

  INSERT INTO factory_artifact.build_identity_claim (
    build_id, claim_sha256, request_sha256, decision_sha256,
    artifact_sha256, source_tree_sha256, module_id, module_version,
    integration_commit
  ) VALUES (
    requested_build_id, requested_claim_sha256, requested_request_sha256,
    requested_decision_sha256, requested_artifact_sha256,
    requested_source_tree_sha256, requested_module_id,
    requested_module_version, requested_integration_commit
  )
  ON CONFLICT (build_id) DO NOTHING;

  SELECT *
    INTO STRICT existing
    FROM factory_artifact.build_identity_claim
   WHERE build_id = requested_build_id;

  RETURN existing.claim_sha256 = requested_claim_sha256
     AND existing.request_sha256 = requested_request_sha256
     AND existing.decision_sha256 = requested_decision_sha256
     AND existing.artifact_sha256 = requested_artifact_sha256
     AND existing.source_tree_sha256 = requested_source_tree_sha256
     AND existing.module_id = requested_module_id
     AND existing.module_version = requested_module_version
     AND existing.integration_commit = requested_integration_commit;
END
$$;

REVOKE ALL ON FUNCTION factory_artifact.reject_build_identity_mutation() FROM PUBLIC;
REVOKE ALL ON FUNCTION factory_artifact.claim_build_identity(text,text,text,text,text,text,text,text,text) FROM PUBLIC;
REVOKE ALL ON TABLE factory_artifact.build_identity_claim FROM PUBLIC;

COMMENT ON SCHEMA factory_artifact IS
  'dps.factory-artifact-schema/v2;sha256=d0dc7779691dcf7f4be5b6f2aa4b646529584119b487557191a7abed251f887c';
COMMENT ON TABLE factory_artifact.build_identity_claim IS
  'dps.factory-artifact-schema/v2;sha256=d0dc7779691dcf7f4be5b6f2aa4b646529584119b487557191a7abed251f887c';
COMMENT ON FUNCTION factory_artifact.claim_build_identity(text,text,text,text,text,text,text,text,text) IS
  'dps.factory-artifact-schema/v2;sha256=d0dc7779691dcf7f4be5b6f2aa4b646529584119b487557191a7abed251f887c';
COMMENT ON FUNCTION factory_artifact.reject_build_identity_mutation() IS
  'dps.factory-artifact-schema/v2;sha256=d0dc7779691dcf7f4be5b6f2aa4b646529584119b487557191a7abed251f887c';

COMMIT;
