# soul-registry changelog

## Unreleased

- Add an internal, test-composed Soul-only Verify/Apply core, additive 003 authority-state migration, signed retained-key references, durable nonce/idempotency receipts and owner-current revision/lease. Real PostgreSQL tests include backend termination and a dropped COMMIT acknowledgement. Production issuer, Binding composition, public current route and P06/P11 handoff remain unavailable; no formal level is claimed.

- Add proposed owner identity DTO/codecs, four closed schemas and shared synthetic corpus, plus current/alias port declarations and split legal wire versus legacy audit encoding. Add full corpus/commitment/schema tests; no production provider, key root, lease implementation or new consumer edge. Stage I implementation only; runtime tests and independent QA remain pending.

- Tightened the full identity envelope to exact opaque `soul_`/`db_`/`pa_`/`trace_`/`idem_` forms at Schema, DTO, PostgreSQL, and adversarial-test boundaries; timestamps and digests now reject non-canonical suffixes and case.
- Registered the module governance boundary. This entry does not claim runtime implementation or production verification.
- Added the F2 no-side-effect implementation for verified-alias resolution, stable random Soul identifiers, key rotation, revocation, idempotency, and PostgreSQL persistence.
- Added database-enforced tenant/Soul composite foreign keys, append-only receipts, raw-alias privacy checks, crash recovery, and real PostgreSQL 18.4 integration tests.
- Moved the sole test-only friend assembly declaration from production C# source into the SDK project file, scoped exactly to `Dps.SoulRegistry.Tests`.
- The module remains proposed and is not release eligible; these changes do not claim Windows, device, canary, scale, or production verification.
