-- Expand: every decision row records the action.proposal contract major it was
-- evaluated from. Existing rows and rows written by pre-0.7.0 binaries default to 1.
-- A v2 row also keeps the Control Plane request signature in its own column; the
-- idempotency commitment (command_sha256) never includes that signature.
ALTER TABLE __SCHEMA__.approval_decisions
    ADD COLUMN IF NOT EXISTS proposal_contract_major smallint NOT NULL DEFAULT 1;
ALTER TABLE __SCHEMA__.approval_decisions
    ADD COLUMN IF NOT EXISTS request_signature text;

DO $constraints$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
         WHERE conrelid = '__SCHEMA__.approval_decisions'::regclass
           AND conname = 'approval_decisions_proposal_contract_major_check')
    THEN
        ALTER TABLE __SCHEMA__.approval_decisions
            ADD CONSTRAINT approval_decisions_proposal_contract_major_check
            CHECK (proposal_contract_major IN (1, 2));
    END IF;
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
         WHERE conrelid = '__SCHEMA__.approval_decisions'::regclass
           AND conname = 'approval_decisions_request_signature_check')
    THEN
        ALTER TABLE __SCHEMA__.approval_decisions
            ADD CONSTRAINT approval_decisions_request_signature_check
            CHECK (
                (proposal_contract_major = 1 AND request_signature IS NULL)
                OR (proposal_contract_major = 2
                    AND request_signature ~ '^[A-Za-z0-9+/]{85}[AQgw]==$'));
    END IF;
END;
$constraints$;

-- DB-layer fence: no submission attempt, from any caller or binary version, may
-- begin against a decision that was not evaluated from action.proposal/v2.
CREATE OR REPLACE FUNCTION __SCHEMA__.reject_non_v2_submission_attempt()
RETURNS trigger
LANGUAGE plpgsql
SET search_path = pg_catalog, __SCHEMA__
AS $function$
BEGIN
    IF NOT EXISTS (
        SELECT 1
          FROM __SCHEMA__.approval_decisions AS decision
         WHERE decision.approval_id = NEW.approval_id
           AND decision.proposal_contract_major = 2)
    THEN
        RAISE EXCEPTION USING ERRCODE = '42501', MESSAGE = 'submission attempts require an action.proposal/v2 decision; v1 is quarantine-only';
    END IF;
    RETURN NEW;
END;
$function$;

DROP TRIGGER IF EXISTS approval_submission_attempts_require_v2 ON __SCHEMA__.approval_submission_attempts;
CREATE TRIGGER approval_submission_attempts_require_v2
    BEFORE INSERT ON __SCHEMA__.approval_submission_attempts
    FOR EACH ROW EXECUTE FUNCTION __SCHEMA__.reject_non_v2_submission_attempt();

REVOKE ALL ON FUNCTION __SCHEMA__.reject_non_v2_submission_attempt() FROM PUBLIC, __RUNTIME_ROLE__, __SUBMISSION_EXECUTOR_ROLE__, __SUBMISSION_RECONCILIATION_ROLE__, __SUBMISSION_RECOVERY_ROLE__;
