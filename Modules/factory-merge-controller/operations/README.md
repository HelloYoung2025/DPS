# Operations

Keep `factory_merge_controller_v1` disabled until contract and merge-head tests pass. Shadow mode compares decisions only and performs no merge or release action. The kill switch stops new decisions. A decision proves only merge-head eligibility, never production approval.
# Complete request signature

The owner contract is `contracts/provided/merge.request.v1.schema.json`.
`evaluate` accepts a decoded Mapping, freezes it once and verifies and consumes
that same frozen value. Visible changes to the original during evaluation fail.
The signed bytes are `b"dps-merge-request-attestation/v1\n"` plus canonical JSON
of the complete request excluding only its own top-level `runner_attestation`.
Embedded result attestations and array order remain signed. Canonical JSON uses
UTF-8, `sort_keys=True`, `separators=(",", ":")`, `ensure_ascii=False` and
`allow_nan=False`; this is not RFC8785/JCS. Both the payload digest and RSA-PSS
SHA-256 signature cover those same prefix-included bytes. MGF1 uses SHA-256 and
the salt is exactly 32 bytes. The old partial projection is rejected.

Every evidence result, including optional checks and branch-head evidence, must
match all four request fields `soul_id`, `device_binding_id`,
`platform_account_id` and `trace_id`. Null is an exact value. Event time and
idempotency keys are signed but need not equal those of a different event.
Result signatures, raw digests, policy, required set, suite, merge-head commit,
receipt, conflict and role checks remain independent requirements.

The shared owner corpus is `tests/fixtures/merge.request.v1.corpus.json`.
Schema validity alone grants no trust. Raw strings are unsupported by the
Mapping API; duplicate-key detection in an upstream parser is unverified.
Non-finite values are rejected at the actual Mapping boundary.

Failure handling stops new candidate evidence consumption and merge, preserving
history. It never restores the known partial signer. Local synthetic RSA and
TEST_ONLY authorization fixtures do not establish production trust or authorize
bootstrap, CLI activation, deployment or release.
