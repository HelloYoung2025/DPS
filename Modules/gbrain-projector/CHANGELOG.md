# gbrain-projector changelog

## Unreleased

- Pinned migration preflight and every runtime connection to exact PostgreSQL 18.4 `server_version_num=180004`, failing closed before identity, DDL, or module data access. Added unit/contract coverage plus a required real non-18.4 PostgreSQL negative control; raised floors to 13 unit, 9 contract, and 16 integration tests. Real positive/negative PostgreSQL evidence remains `INFRA_ERROR/NOT_RUN` until all three external DSNs exist; release eligibility remains false.

- Tightened `gbrain.source.binding/v1` and `gbrain.projection/v2` to exact schema versions, bounded candidate computation to nonce `0..1023`, and made both the DTO and Source authority recompute the fixed `soul_id + nonce` derivation before trusting a binding.
- Removed injectable Source generation from the production authority/store path; collision tests now preoccupy real fixed-algorithm candidates without minting false bindings.
- Split PostgreSQL migration and runtime credentials. The migrator now proves one database lock namespace and rejects shared/impersonated/elevated or role-connected runtime identities, cross-database credentials, unsafe session state, replication-trigger bypass privileges, weak precreated schemas, extra relations/types/rules/publications, quote-aware exact-expression/FK/internal-trigger/index/application-trigger drift, mixed or substituted owners, and non-exact ACL before adoption; runtime initialization performs signed-owner attestation but no DDL.
- Explicitly named and attested every PostgreSQL 18 `NOT NULL` catalog constraint so PG18.4 strict adoption does not depend on automatic names or omit `contype = 'n'` objects.
- Added UPDATE/DELETE and TRUNCATE protection for all ledgers, canonical-text/JSONB equality checks, exact runtime SELECT/INSERT-only grants, same-transaction INSERT read-back, PostgreSQL timestamp precision rejection, and verification across relational columns, canonical bytes, JSONB, checksum, referenced binding, and the unambiguous current revision.
- Added adversarial unit, contract, and real-PostgreSQL tests for exact versions, nonce bounds, forged bindings, strict and near-exact adoption, quoted-literal CHECK attacks, hostile search paths, migration-owner substitution, cross-database credentials, replication-mode/parameter bypass, disabled internal RI triggers, cross-schema FK substitution, bidirectional role separation, ACL, all table/action append-only combinations, timestamp precision, ambiguous heads, and relational/canonical/JSONB splits. Real PostgreSQL evidence remains `INFRA_ERROR/NOT_RUN` until both external credentials and required test authority exist; release eligibility remains false and consumer v2 reciprocity remains red.
- Raised fail-closed suite floors to the current required set: 12 unit, 8 contract, and 15 real-PostgreSQL integration tests.

- Byte-froze `gbrain.projection/v1`, its v1 DTO, and the legacy collision corpus; moved v1 to deprecated quarantine-only with no runtime communication edge.
- Added active proposed `gbrain.source.binding/v1` and `gbrain.projection/v2`, binding full Soul hash, domain-separated SHA-256 nonce allocation, allocation time, binding revision/checksum, and projection revision/checksum.
- Added the fixed process-bound Source binding authority and non-directly-constructible renderer capability; raw caller-selected Source IDs are no longer accepted by the v2 renderer.
- Added PostgreSQL unique full-Soul Source allocation with same-Soul idempotency, bounded collision retry, nonce-exhaustion quarantine, restart verification, and append-only binding/revision tables.
- Added tests for the retained v1 collision pair, v2 distinct mapping, injected collisions, concurrent replay, restart, tamper, cross-Soul capability misuse, nonce exhaustion, revision rollback, and real PostgreSQL persistence. Real PostgreSQL and GBrain evidence remain external blockers; the module remains release-ineligible.

- Bound each proposed GBrain Source to `dps-` plus the first 112 bits of the Soul digest, published the collision/read-back corpus, and required the full Soul/revision/checksum tuple to remain authoritative when a truncated Source collides.
- Tightened the projection envelope, nested evidence hashes, and UTC timestamps to exact canonical v1 forms.
- Registered the module governance boundary. This entry does not claim runtime implementation or production verification.
- Added the F2 deterministic GBrain projection DTO renderer with Soul-owned source IDs, exact event/evidence matching, revision/checksum guards, and no network write path.
- Added unit, contract, cross-scope, duplicate/conflict, and real PostgreSQL-backed vertical-slice tests.
- Froze `gbrain.projection/v1` as the sole Source binding, projection revision, and projection checksum authority and declared `soul-memory-adapter` as an exact consumer without adding a network path.
- The module remains proposed and is not release eligible; these changes do not claim a GBrain write/read-back, Windows, device, canary, scale, or production verification.
