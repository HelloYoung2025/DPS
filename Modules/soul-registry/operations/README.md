# soul-registry operations

The module is proposed and not production release eligible. Runtime routing may change only through an exact signed Release BOM.

## Receipt hashing across alias-key rotation

The P05 implementation slice separates alias lookup from idempotency verification. Alias lookup uses the active lookup keyring. New resolution and mutation receipts record hash version 2 and exactly one hash key ID, with the operation and complete request commitment under separate versioned hash domains. Replay uses that recorded key, including after a newer lookup key becomes current. Retain the recorded verification key for the required replay period; reusing a key ID with different key bytes must fail verification.

Legacy version 1 receipts keep their original hashes and result bytes. When their hash key basis is absent, the service may verify the exact current key set or an explicitly configured historical key set in `LegacyReceiptHashKeySets`. Each historical set must name the complete known original key IDs, with the corresponding key material retained. Only an exact match to the complete stored legacy request hash permits replay. The result alias key alone is not the request hash basis; subset guessing and rewriting an old receipt are forbidden. Missing, ambiguous or unverifiable history fails closed.

Apply the additive receipt-metadata migration before enabling the new binary. The original receipt fields and append-only protections remain. Older writers still create version 1 receipts with no basis metadata. However, the old binary cannot replay a new version 2 receipt, even with the same single key. Stop old writers before enabling version 2 writes. Once version 2 receipts exist, rollback requires a binary that understands them or a forward fix; do not drop the metadata or rewrite stored hashes and results. Retaining the old binary as a rollback target requires a separately validated compatibility stage, which this slice does not implement.

Runtime identity verification, Soul current-resolution authority, Binding composition and the positive Ledger v2 integration remain separate P05/P11 work; this hash repair does not make caller-supplied verification flags authoritative.

The 2026-09-14 local builder run passed locked restore/build, 14 unit cases and 7 contract cases. The complete real PostgreSQL 18.4 integration suite ran 21 cases: 20 passed, with only the retained P11 Soul-to-Ledger v1 positive path failing. This includes native old single-key and two-key receipt migration, exact historical-key replay and rejection of an incomplete history containing only the result alias key. These runs used the compiled MTP DLL. The original Manifest Unit frontend timed out after 15 seconds and remains INFRA_ERROR; the original frontend suites are not claimed as passed. Independent review of the final candidate is pending. This slice is neither a P05 package PASS nor deployment or evidence issuance authorization.

Rollout order is synthetic test, shadow, bounded canary, rolling waves, and soak. Shadow is forbidden from producing real-world side effects. The feature flag, kill switch, canary rule, rollback target, minimum evidence, and human approval boundary are machine truth in ../module.yaml.

Rollback restores routing and the previous compatible signed BOM. It must not pretend that external side effects or destructive data changes were reversed; those require stop, reconcile, compensate, or restore procedures.

## Stage I identity contract assets were inactive

P05 adds owner DTO/codec/canonical/schema assets and public port declarations only. The four new manifest entries remain proposed/quarantine-only, without a new consumer edge or runtime handler. No alias verifier, signer, current factory or lease provider is activated; no trust root, nonce store or database migration is introduced. Decoder success, a signed-looking DTO or the old receipt replay path cannot authorize a current mutation.

Freeze and independently verify the actual candidate before any downstream asset handoff. Full current capability and lifecycle/integration work remains outstanding, and this asset slice does not satisfy P05's real-capability dependency exit to P06/P11. Stage I is IMPLEMENTED_UNVERIFIED; Stage II environment and project tests require their own bound authorization.

## P05-R1 Soul-only local decision core

An internal test-composed core now verifies an evidence provider's exact request commitment, signs and validates the owner alias proof, records nonce and complete retained-key references, and atomically applies create/attach/resolve/revoke to separate authenticated alias rows. It checks old alias rows for conflicts without promoting them. Soul current revision and revocation epoch are owner-local; a nonpooled PostgreSQL session holds the same advisory key that owner mutations take in their transactions. COMMIT response loss is reconciled against the exact idempotency receipt after the original connection is closed. Missing or mismatched readback remains `COMMIT_UNKNOWN`.

No protected production evidence resolver, signing key, fixed release BOM or authenticated caller route is installed. The class has only an internal constructor and the four public identity contracts remain `quarantine-only`; public current-resolution signing and Binding composition are absent. A local Soul lease cannot promise that a separate Ledger transaction has committed, especially if its acknowledgement is unknown. Rollout, canary and release remain disabled; the safe stop is to leave routing closed. Do not downgrade 003 or restart an old writer after authority rows exist without a separately proved compatibility path.
