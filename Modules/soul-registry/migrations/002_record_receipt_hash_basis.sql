-- Add metadata without changing any existing request hash or result.
ALTER TABLE __SCHEMA__.resolution_receipts
    ADD COLUMN IF NOT EXISTS request_hash_version integer NOT NULL DEFAULT 1,
    ADD COLUMN IF NOT EXISTS request_hash_key_ids text[] NULL;

ALTER TABLE __SCHEMA__.mutation_receipts
    ADD COLUMN IF NOT EXISTS request_hash_version integer NOT NULL DEFAULT 1,
    ADD COLUMN IF NOT EXISTS request_hash_key_ids text[] NULL;

DO $$
DECLARE
    receipt_table text;
BEGIN
    FOREACH receipt_table IN ARRAY ARRAY['resolution_receipts', 'mutation_receipts'] LOOP
        IF NOT EXISTS (
            SELECT 1 FROM pg_constraint
            WHERE conrelid = format('%I.%I', '__SCHEMA__', receipt_table)::regclass
              AND conname = 'receipt_hash_basis_shape'
        ) THEN
            EXECUTE format(
                'ALTER TABLE %I.%I ADD CONSTRAINT receipt_hash_basis_shape CHECK (
                    request_hash_version IN (1, 2)
                    AND (request_hash_version = 1 OR cardinality(request_hash_key_ids) = 1)
                    AND (request_hash_key_ids IS NOT NULL OR request_hash_version = 1)
                    AND (request_hash_key_ids IS NULL OR (
                        array_ndims(request_hash_key_ids) = 1
                        AND array_lower(request_hash_key_ids, 1) = 1
                        AND cardinality(request_hash_key_ids) > 0
                        AND array_position(request_hash_key_ids, NULL) IS NULL
                    ))
                )', '__SCHEMA__', receipt_table);
        END IF;
    END LOOP;
END;
$$;
