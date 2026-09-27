# Policy Approval changelog

## 0.8.0 - Proposed

- Add the Policy-owned `approval.readback/v1` contract: a strict DTO/codec, a Draft 2020-12 schema that references `approval.decision/v1` by `$id`, a locked 28-case corpus, and the domain `dps.policy-approval.approval-readback/v1`. The exact reader projects it through `PolicyApprovalAuthoritativeSnapshot.ToReadbackV1()`. `request_signature` comes only from the persisted migration-005 column: required for major 2, null for major 1. The top-level scope fields repeat the persisted row scope and must equal the embedded decision. No migration, grant or table was added.
- Declare the outbound readback edge to `control-plane-host`. The inbound `action.proposal/v2` edge now peers `control-plane-host`, which relays with the producer preserved, under scope `module:control-plane-host`.

## 0.7.0 - Proposed

- Consume `action.proposal/v2` only, through the Planner owner codec and its `dps.planner.action-proposal-sha256/v2` commitment. One entry gate dispatches by contract major before authentication or any database access: `action.proposal/v1` returns a bounded hash-only `PolicyProposalQuarantinedException` proof and writes nothing; unknown majors fail closed.
- Breaking public API: delete the v1 typed evaluation entry together with `PolicyEvaluationEnvelope` and `PolicyAuthorizationBinding`. `PostgresPolicyApprovalService.EvaluateAsync` now takes the proposal wire, the `approval.evaluation.request/v1` wire, and the optional promotion; `DeterministicPolicyEvaluator.EvaluateAsync` takes `ActionProposalV2`, the request, and the optional promotion.
- Add policy-owned `approval.evaluation.request/v1` (wire producer `control-plane-host`, scope `policy:evaluate`) with a strict DTO/codec, Draft 2020-12 schema, locked corpus, and canonical domain `dps.policy-approval.approval-evaluation-request/v1`. It binds the proposal ID and v2 commitment, Soul/device/account/trace/idempotency, Release BOM, mode (`SHADOW`, or `EXECUTE` with the promotion hash), an `occurred_at` equal to the proposal's, and `valid_until = occurred_at + 120 s`, checked against PostgreSQL time at entry and before commit.
- Verify the request signature first and store it in its own column; the idempotency command hash is the signature-free request commitment. An identical re-signed request replays the original result, a changed business field is a hash-only conflict, and an expired new request writes nothing.
- A `SHADOW` request yields `DENIED`, `shadow_only`, `SHADOW_ONLY`. Only `EXECUTE` with a bound, independently signed `action.execution.promotion/v1` can yield a non-shadow `APPROVED`; neither path dispatches.
- `proposal_sha256` in `action.execution.promotion/v1` and `approval.submission.intent/v1` is now always the v2 commitment, and v1 is never promoted. Schema bytes are unchanged.
- Add expand migration `005_add_proposal_contract_major.sql`. Decision rows record `proposal_contract_major` (existing and pre-0.7.0 rows default to 1) and `request_signature`. A BEFORE INSERT trigger rejects submission attempts for non-v2 decisions from any caller with `42501`; its SECURITY INVOKER function has a fixed `search_path`, is revoked from `PUBLIC` and all four runtime roles, and no grant widens. Migration 004 stays unregistered.
- One shared snapshot guard keeps an ACTIVE non-shadow v1 `APPROVED` unreadable at the reader, duplicate receipt branch, fence acquire/revalidate/begin, and revocation readback; DENIED, shadow, and REVOKED v1 history stays readable. The in-process snapshot adds `ProposalSha256` and `ProposalContractMajor`.
- Move every former v1 → `APPROVED` positive test to v2 + promotion and assert v1 isolation at each exit, request binding, replay and conflict, expiry, migration 005 history and re-initialization, and the database trigger.
- Require Planner `>=0.2.0 <1.0.0`. Control Plane Host does not produce the request yet.

## 0.6.0 - Proposed

- Remove every Policy-local `native.stop.proof` and `native.stop.challenge` DTO, Schema, Corpus, NuGet Pack item, and EmbeddedResource. Executor Gateway remains the unique registered native-stop owner; Policy has no copied fallback contract source.
- Remove callback, caller-selected trust, internal trusted-composition, challenge-signer topology, and callback-timeout surfaces. Public `SubmitNativeOnceAsync` accepts only cancellation and returns explicit `WAITING_EXTERNAL` before PENDING.
- Delete the unreachable retained session-guard/callback helpers and their guard-connection read path; they cannot be mistaken for an active production fallback.
- Replace eight label-only missing-authority Integration rows with one real public failure-closed case.
- Define native result truth as `CONFIRMED_SUCCESS`, `FAILED`, or `UNKNOWN_OUTCOME`; transport ACK is not postcondition proof and never promotes itself to success.
- Retain `004_create_native_stop_challenge_ledger.sql` only as an unregistered proposed design copied into the test artifact; remove it from the Policy product assembly, product migration registry, Manifest stores, and runtime permissions. The real PostgreSQL 18.4 design case first proves product initialization leaves its tables absent, then checks all table privileges for `PUBLIC` and four runtime identities, executes five denied table operations under every login role, checks function ACLs, and proves owner mutation rejection. This evidence cannot activate the design or make the current product release-eligible. Cryptographic owner-artifact verification, a registered activation migration, the process-root guardian, reciprocal Executor Gateway/Supervisor/BOM wiring, and Windows evidence remain explicit hard blockers.

## 0.5.0 - Proposed

- Add policy-owned `approval.submission.intent/v1`, `approval.submission.acknowledgement/v1`, `approval.submission.reconciliation/v1`, `approval.submission.recovery/v1`, and `approval.submission.state/v1` contracts with strict DTO validation, Draft 2020-12 schemas, locked adversarial corpora, and domain-separated canonical commitments.
- Require explicit `BeginSubmissionAsync(intent)` immediately before native submission. It repeats authoritative fence checks and commits an exact policy-signed `SUBMISSION_PENDING` fact before returning; only a newly inserted begin result can authorize one native call.
- Make an exact duplicate begin readback-only with `ExistingUnknownSubmission` and `MaySubmit=false`; pending or unknown attempts block acquire, revalidation, pre-submit, and automatic redelivery across restart.
- Add exact durable acknowledgement, uncertainty quarantine, independent reconciliation, and separately signed human recovery transitions. Only `CONFIRMED_NOT_SUBMITTED` plus a distinct recovery authority can bind a fresh attempt, lease, Release BOM generation, authorization digest, and native-request commitment; old attempts never reopen.
- Add PostgreSQL migration `003_create_submission_lifecycle.sql` with append-only lifecycle tables, exact advisory-lock ordering, security-definer entrypoints, runtime-login verification, least-privilege ACLs, and no direct runtime mutation or helper execution.
- Serialize begin, acknowledgement, quarantine, reconciliation, and recovery under one deterministic scope/approval/command/attempt lock order plus an attempt-row lock; ACK and reconciliation are mutually exclusive, quarantine cannot follow a terminal branch, and a stale read can no longer append a fork hidden by read precedence.
- Move acquire-state checking inside that same transaction and lock set, re-read PostgreSQL time after lock waits, and bind begin to the current two-second fence expiry so pending blocks acquire/revalidate and waiting cannot extend authorization.
- Require full approval/proposal/command/Soul/device/account/trace/idempotency equality before consuming a recovery authorization; reject reusing recovery for a different command, approval, or identity scope.
- Split generic runtime, submission executor, independent reconciler, and human recovery into pairwise-distinct PostgreSQL login roles with disjoint function grants; direct DML, helper execution, role membership, object ownership, and unknown overloads fail the privilege guard.
- Split the public submission composition into executor, reconciler, and recovery options/clients so no one client exposes another role's database credential or transition methods; bind service and clients to one pairwise-distinct eight-authority fingerprint topology.
- Revalidate the full signed reconciliation/recovery chain inside the attempt 2/3 acquire transaction instead of trusting exact-looking recovery columns; strict DTOs, signatures, state digests, predecessor hashes, expiry, and fresh intent/lease/BOM bindings must all agree.
- Replace the public split begin/native/ack surface with one lease-scoped `SubmitNativeOnceAsync` callback boundary; segmented begin, acknowledgement, and quarantine remain internal only for deterministic and PostgreSQL tests.
- Append PENDING in T1, acquire runtime/approval/command/attempt session advisory locks on the same explicitly non-pooled backend before commit, run a separate database-clock revalidation transaction, cap the callback by the remaining fence window, commit ACK or confirmed UNKNOWN in a later same-backend terminal transaction, and unlock in reverse order.
- Add explicit `ACKNOWLEDGED`, `UNKNOWN_CONFIRMED_STOPPED`, `PENDING_RETAINED`, and `EXISTING_UNKNOWN_SUBMISSION` results. Existing PENDING invokes no callback; PENDING commit uncertainty invokes no callback; ACK/UNKNOWN commit uncertainty never attempts a competing terminal branch.
- Reject ordinary dispose while an unconfirmed-stop lease is retained and keep the durable PENDING fact as the no-retry boundary until process death or separately authorized reconciliation; production still requires an explicitly injected process-root guardian or host fail-fast.
- Add real-PostgreSQL cases for revoke/runtime blocking across PENDING commit and callback, exact callback count, Existing PENDING callback zero, throw/null/timeout/invalid-ACK quarantine after confirmed stop, synchronous pre-Task callback/abort blocking, retained-guard repeat suppression, and controlled process-death cleanup.
- Keep the callback stop capability proposed/test-only: its current Boolean confirmation is not a non-reusable executor-authority-signed proof bound to attempt, native binding, worker incarnation, Release BOM, and stop evidence. Production release remains forbidden until that proof and supervisor fail-fast integration exist.
- Keep production ineligible until raw policy-state private-key injection is replaced by an independently authenticated, role-validating state-receipt signer broker/HSM capability.
- Drop the obsolete five-argument begin overload and generic role helper during migration replay, and add an upgrade test seeded with the old privileged bypass.
- Enforce exact JSON field sets and null-safe headers in every security-definer lifecycle RPC, preserve the original pending-state commitment in snapshots, and require exact evidence digests for duplicate ACK/quarantine/reconciliation/recovery requests.
- Add real PostgreSQL integration cases for the three native-submission crash windows, duplicate begin, restart blocking, concurrent terminal-transition competition, terminal acknowledgement, forged/cross-scope reconciliation, recovery-authority separation, and ACL bypass attempts. Missing PostgreSQL remains a failed/infra result and never falls back to Mock.
- Keep `releaseEligible: false` until the Executor Gateway consumer is rebound, the real PostgreSQL 18.4 suite passes, and the exact policy/executor compatibility combination is recorded in a signed Release BOM.

## 0.4.0 - Proposed

- Make Planner proposals unconditionally shadow-only and require the independently signed, exact-scope `action.execution.promotion/v1` before a non-shadow approval can exist; Policy Approval uniquely owns the strict input contract while Control Plane Host remains its fixed wire producer.
- Separate evaluation, promotion, and revocation public-key inputs, reject pairwise-identical P-256 authority fingerprints, and accept only fixed 64-byte P1363 signatures with strict Base64/wire encodings.
- Bind promotions to the immutable proposal commitment, exact Soul/device/account/trace/idempotency scope, policy runtime revision, trusted database-time window, and signed Release BOM.
- Add `approval.execution.fence/v1` and a PostgreSQL execution-fence lease primitive that revalidates and serializes revocation/runtime changes; keep production ineligible until the executor two-phase SubmissionAck integration proves the critical section remains held through native submission.
- Add Draft 2020-12 shared contract corpora and terminal SHA-256 schema constraints that reject newlines, carriage returns, spaces, uppercase, short, and long digests.
- Make `approval.decision/v1` and `approval.execution.fence/v1` use bounded strict UTF-8 codecs with one canonical snake_case wire, exact and unique fields, lowercase non-zero D UUIDs, canonical Zulu timestamps, positive Int64 revisions, owner corpora, and fail-closed JSONB materialization; the fence wire now exposes the common `occurred_at` field.
- Reject terminal-newline policy IDs and year-zero timestamps, cap every detached execution-fence DTO at the producer's two-second lifetime, and lock the exact unique adversarial case IDs for all three owned contract corpora so deleting a required case fails both schema and C# gates.
- Replace permissive identifier checks with exact opaque device, platform-account, trace, and idempotency formats across DTOs, schemas, and PostgreSQL constraints.
- Snapshot caller-owned proposal collections exactly once before validation and canonicalization, closing a time-of-check/time-of-use mutation path.
- Keep production status proposed: locked NuGet restore and real PostgreSQL 18.4 Integration evidence are required before a higher verification claim.

## 0.2.0 - Proposed

- Add a PostgreSQL 18.4 vertical slice with immutable policy runtime revisions, atomic append-only rate consumptions, approval decisions, `ACTIVE`/`REVOKED` status revisions, idempotency receipts, outbox, and hash-only conflict quarantine.
- Add separate migrator/runtime roles, exact runtime-role identity/attribute checks on every connection, five-second connection and command limits, owner-level append-only triggers, and explicit refusal of the GBrain Company PostgreSQL service.
- Replace delimiter-based proposal and trust commitments with strict UTF-8 domain-separated length-prefixed canonical encoding; match the operation-compiler approval snapshot digest exactly.
- Add signed execution promotion and revocation, trusted PostgreSQL time, exact scoped authoritative reads, restart/crash recovery, concurrency, cross-scope, stale-revision, prompt-injection-shaped input, kill-switch, rate, and platform authorization coverage.
- Bind trusted runtime-state reads to the decision transaction and connection, bind outbox rows to the exact receipt and status revision, and enforce reason codes for every status revision.
- Keep production status proposed: standard locked restore is still infrastructure-blocked in the current macOS sandbox, and real PostgreSQL Integration evidence is not present in this environment.
- Record the operation-compiler v1 revocation-after-read limitation: downstream lease/dispatch revalidation remains required before production eligibility.

## 0.1.0 - Proposed

- Add deterministic `approval.decision/v1` with explicit deny reasons.
- Enforce policy allowlist, caller role, scope, platform authorization, rate budget, shadow, and kill-switch checks.
