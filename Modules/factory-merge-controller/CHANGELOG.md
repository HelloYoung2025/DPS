# Changelog

## Unreleased - P17B-R1 - 2026-09-15

- Verify the complete domain-prefixed request excluding only its own top-level attestation. Freeze once for verification and consumption, reject visible original drift and non-finite JSON, and reject the old partial projection. Compare every result's four identity scope fields exactly, including optional checks, branch-head evidence and null values.
- Document the signature profile using Schema annotations only, preserving all original wire fields and constraints. Enforce existing request grammar at runtime and retain independent inner signatures, raw digests, policy, required, suite, commit, receipt, conflict and role checks.
- Preserve the 7 original unit and 4 original contract methods; add the real parent-red context regression, complete nested/array mutation coverage, correctly re-signed cross-scope evidence, signature parameter failures and an 82-case shared owner corpus read by all four Runner/Merge test files. Corpus authentication is synthetic TEST_ONLY. Mapping APIs cannot recover discarded raw duplicate keys.
- On failure stop candidate evidence consumption and merge and preserve history; never restore the partial signer. Local results remain diagnostic and require independent QA; PostgreSQL suites, central gates and production trust are incomplete.

## 0.1.0 - Proposed

- Tightened nullable identity envelopes plus trace/idempotency to exact canonical opaque forms in runtime validation, Schema, and adversarial fixtures.
- Registered the versioned `merge.decision/v1` receipt path to `factory-control-plane-host`.
- Added merge-head-only evidence evaluation.
- Added role separation, stale-instruction, conflict, and required-PASS gates.
- Added versioned merge request and decision contracts plus unit tests.
- Hardened the Contract suite to load the exact module-owned production source under isolated Python without relying on `PYTHONPATH` or unit-test fixtures.
- Hardened the required Unit suite with the same exact-path isolated loader instead of an ambient `PYTHONPATH` import.
