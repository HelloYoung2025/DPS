# Changelog

## 0.2.1 - Proposed

- Aligned the provider and Release consumer on canonical UTC `Z` timestamps, lowercase build IDs, SemVer build metadata, build-ID SPDX namespaces, and the exact `0.2.1` builder identity.
- Added SPDX 2.3 analyzed-file essentials: SHA1 plus SHA256 file checksums, Package Verification Code, `documentDescribes`, and exact package `CONTAINS` relationships. SHA1 is compatibility-only and never a DPS trust digest.
- Replaced the inaccurate SLSA predicate claim with the explicit DPS custom provenance predicate; the current dependency/run metadata is not accepted as SLSA provenance evidence.
- Derived descriptor receipt idempotency from the durable `build_id` rather than the artifact digest, so separate legal builds of identical payload bytes cannot collide downstream.
- Added a v1 Schema annotation for the receipt-idempotency derivation and synchronized the reciprocal Release Controller communication edge.
- Re-attested the PostgreSQL catalog with explicit constraint names, exact definitions, a complete one-index allowlist, and zero column ACLs; weakened checks, extra indexes, or column grants now fail closed.
- Added unit and real-PostgreSQL adversarial cases for column ACL drift, weakened CHECK constraints, and extra indexes. The weakened-CHECK test restores the exact constraint in `finally`; real PostgreSQL 18.4 remains a required external gate.

## 0.2.0 - Proposed

- Made production composition reject every registry except the exact PostgreSQL implementation; the in-memory registry remains test-only.
- Added clean transaction ownership, synchronous-commit enforcement, `fsync`/full-page-write checks, direct login binding, and rejection of database ownership, database CREATE/TEMP, or any schema CREATE privilege.
- Added per-claim catalog attestation for the common migration owner, migration comments, function bodies and properties, exact ACL split, columns, constraints, primary index, and append-only triggers.
- Wrapped migration 001 in one explicit transaction and added static and adversarial drift tests.
- Made SBOM, provenance, and descriptor filenames self-addressed by their own canonical content digests so identical payloads from distinct valid builds coexist safely.
- Made every SPDX document namespace use the durably unique build ID and synchronized SBOM/provenance builder identity with version 0.2.0.

## 0.1.0 - Proposed

- Registered the versioned `artifact.descriptor/v1` receipt path to `factory-control-plane-host`.
- Added immutable SHA-256 artifact descriptors.
- Added SPDX 2.3 JSON SBOM and SLSA-compatible provenance generation.
- Enforced external-only signing and exact merge-decision linkage.
- Hardened the required unit suite with fail-closed, module-owned exact-file loading under isolated Python.
- Tightened Soul, device, account, trace, and idempotency identifiers to the repository-wide opaque formats with absolute-end matching and added trailing-newline rejection tests at both runtime and schema boundaries.
- Replaced path-based input/output checks with pinned descriptor-relative no-follow reads and writes, including namespace-drift detection for input parents and output directories.
- Added bounded Git pipe reads, duplicate-path rejection, and hard manifest, artifact, file-count, per-file, listing, metadata, and total-tree byte limits.
- Added locked staged publication with file/directory sync, no-replace linking, non-writable single-link validation, deterministic crash recovery, and full bundle read-back before completion.
- Added adversarial tests for input/output swaps, oversized inputs, duplicate and oversized Git inventories, hardlinked/writable outputs, idempotent duplicate publication, and process death on both sides of the atomic link.
- Made the signed merge decision pass the complete `merge.decision/v1` runtime shape before it can authorize a build; a valid signature no longer makes unknown fields, malformed IDs, impossible calendar timestamps, empty approval evidence, or contradictory approval reasons acceptable.
- Bound Soul, device, account, trace, and temporal workflow scope between the signed merge decision and the build request, and reject requests that precede their authorizing decision.
- Bound `module_version` to the exact version in the commit-matched module Manifest and added adversarial tests for cross-scope decisions, malformed request metadata, and version substitution.
- Added a required linearizable build-identity registry: each `build_id` is committed against canonical request/decision digests, artifact/source digests, module version, and integration commit before publication; exact retries succeed and divergent reuse fails closed.
- Added the PostgreSQL 18.4 append-only claim migration, least-privilege SECURITY DEFINER API, UPDATE/DELETE/TRUNCATE guards, unit concurrency/conflict tests, and a required real-PostgreSQL restart/ACL/concurrency suite that reports missing infrastructure as `INFRA_ERROR`.
- Added per-connection production attestation that rejects admin, owner, privileged, inheriting, member, wrong-version, direct-table, or schema-create database identities before a build ID can be claimed.
