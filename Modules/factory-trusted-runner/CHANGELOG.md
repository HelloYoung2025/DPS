# Changelog

## Unreleased - P17B-R1 - 2026-09-15

- Add process-bound `issue_merge_request` with one optional currentness verifier, a nonblocking instance lock and a complete sequential required-check batch. Freeze all inputs and results, compare full receipt summaries, and recheck every admitted auth, plan, Manifest, HEAD, workspace identity, lease, fence, policy and time before and after signing. PRE and POST bind identical complete unsigned bytes; a failed final check returns no request.
- Sign the owner profile's domain-prefixed complete request, including embedded result attestations, with explicit OpenSSL SHA-256, MGF1-SHA-256 and 32-byte RSA-PSS salt. Reject non-finite JSON and caller evidence, role, required-set or attestation overrides. Preserve all 34 P17A unit methods and 5 contract methods and the disabled CLI.
- Add real two-check synthetic OpenSSL-to-Merge interoperability, batch drift and provider failure tests, and direct use of the 82-case shared owner corpus in both original suites. Authorization fixtures remain TEST_ONLY; raw wire requests are rejected by the proposal API, and an upstream duplicate-key parser is not claimed. Explicit date-time checking avoids depending on optional jsonschema format extras.
- Stop execution, candidate evidence consumption and merge on failure; preserve history and never restore the partial request signer. Local development results do not grant formal verification. Independent QA, three PostgreSQL suites, central gates, deployed trust, bootstrap, CLI activation and production remain separate and incomplete.

## Unreleased - P17A-R3 - 2026-09-15

- Fix P17A-QA-F01: replace error-suppressing workspace globbing with explicit directory-descriptor enumeration, no-follow opens and stable identity checks. Reject every enumeration or classification failure instead of returning a partial workspace digest. Preserve global path ordering and the top-level-only Git exclusion.
- Add four regression methods covering real searchable/unlistable directories before and after execution, six pre/post enumeration-error branches, and independently specified digest ordering with nested Git names. The same real pre-execution case exposes an issuer call and persistent mutation on the old implementation, then rejects before execution after the fix. Restore fixture permissions and retain process cleanup evidence.
- Preserve all prior currentness, CLI, crypto, failure-diagnostic and lifecycle checks. Keep the old candidate and BLOCKED QA evidence; the correction still needs fresh independent QA and grants no formal gate, integration or production authority.

## Unreleased - 2026-09-15

- Freeze run inputs and recheck exact workspace, HEAD, Manifest, auth/receipt, plan and lease/fencing after process cleanup and output freeze. Reject changed identities, unsafe reads, expired leases and clock rollback before issuance. Parse and hash each Manifest from the same protected bytes.
- Bound POSIX leader/group cleanup, retain native exit and timeout facts, and stop signals after ESRCH. Preserve nonzero-signal errors as terminal; signal 0 EPERM stays UNKNOWN until an actual in-budget ESRCH or the original deadline. Record synthetic process identities and all observations; unresolved fixture resources stop further test execution. Descendant time is included in the timeout.
- Refuse the static HMAC CLI with exit 2 before input/key reads or execution. Restoring it requires separately bound independent currentness support. Move its real RSA-PSS/Schema coverage to dynamic synthetic fixtures and add actual signature/payload tamper rejection without deploying a trusted key.
- Preserve ordinary signed non-releasing failure diagnostics after successful integrity checks. Keep v1 contracts, policy capabilities and consumers unchanged. P17B, independent QA, full Phase0/Candidate gates, real issuer, Windows and device verification remain separate; local tests confer no formal level or production containment claim.

## 0.1.0 - 2026-07-14

- Tightened nullable identity envelopes plus trace/idempotency to exact canonical opaque forms in runtime validation, Schema, and adversarial fixtures.
- Registered the versioned `trusted.test.result/v1` receipt path to `factory-control-plane-host`.
- Added exact policy-owned argv execution with no shell, active fencing, role separation, workspace/commit binding, hashed fail-closed evidence, and externally issued RSA-PSS attestations.
- Added a required Draft 2020-12 `trusted.test.result/v1` contract suite that validates real `TrustedRunner` output and adversarial instances; the Manifest candidate minimum is now `CONTRACT_VERIFIED`, without claiming formal verification.
