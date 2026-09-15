# memory-event-ledger tests

This directory separates unit, contract, integration, resilience, Windows, and device evidence. The exact required suites and commands are declared in ../module.yaml.

A required check releases only on PASS. FAIL, SKIP, PARTIAL, NOT_RUN, INFRA_ERROR, NOT_APPLICABLE, an empty test set, or missing raw evidence is a failure. Mock evidence must be labelled and cannot satisfy integration, Windows, ZennoDroid, ADB, device, canary, or scale gates.

The first implementation slice is side-effect free. PostgreSQL integration must use a real PostgreSQL 18.4 instance; an in-memory replacement cannot satisfy the required integration suite.

v2 unit/contract coverage includes forged v1 booleans and Soul DTOs, wrong/cross scope, raw byte swaps, wrong keys, stale/equal-expiry/revoked authorities, signed-receipt replay under another event ID, unknown/acted outcomes, capability reconstruction/JSON round-trip, canonical JSON limits, and signal bounds. Integration requires distinct bootstrap/admin/runtime roles and covers atomic event/outbox crash windows, duplicate no-op, same-id conflict quarantine, concurrency, chain replay, runtime role bypass, direct DML, UPDATE/DELETE/TRUNCATE, cross-Soul privacy references, and privacy-table grants. Missing `DPS_TEST_POSTGRES` is `INFRA_ERROR/NOT_RUN`, never a skip or pass.

`MemoryEventV2BoundaryTests` checks the exact `2.0.0` schema version for append, event, and outbox DTOs and exact event deserialization. It covers canonical round-trips at 0, 1, 29, 30, 31, and 32 signals, rejection above 32, and rejection of extra or duplicate properties at the limit. These public DTO fixtures test shape validation only; they do not issue capabilities or authorize an append. The v1 quarantine contracts remain frozen.

## Actual legacy Soul audit differential

`SoulResolvedCanonicalDifferentialTests` adds eight Contract cases: five fractional-tick/key spelling cases and three calendar endpoints. Each compares owner bytes with the unchanged production `SoulResolvedCanonicalizerV1.Serialize` available through the existing friend assembly; no copied oracle, private export or production-code edit is used. The zero-fraction case additionally checks the shared owner design vector, whose model status remains explicit until fixed .NET execution. Legal Zulu wire and escaped O-format audit must remain distinct. The test assembly embeds the same Soul-owned corpus through its existing dependency closure.

These tests have not run in Stage I. Original suites/cases, ten Soul legacy fixture bytes and required PG gates remain unchanged. New tests and the full existing Contract category must run under the separately frozen Stage II environment and independent QA.
