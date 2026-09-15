# Changelog

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
