# Contract major modes 合同主版本模式

状态: Central compatibility policy adopted by registered module Manifests, the Phase 0 generator, and the executable F9 signed-Manifest validator. Product communication reciprocity and candidate execution evidence remain separately reviewable.

## Purpose

Every item in `contracts.provided[]` and `contracts.consumed[]` identifies one exact `(contractId, major)` and must declare one machine-readable `mode`:

- `active`
- `compat-read`
- `quarantine-only`
- `retired`

`compat-read` is valid only in `contracts.consumed[]`. A provided declaration cannot encode or publish a read-compatibility major, so Schema validation rejects `contracts.provided[].mode: compat-read`.

There is no default. A missing or unknown contract ID, major, or mode fails closed. A later major is never inferred to be compatible from SemVer, file names, DTO shape, successful deserialization, or the presence of an older major.

`status` and `mode` answer different questions:

- `status` is the lifecycle of the contract asset: proposed, active, deprecated, or retired.
- `mode` is the runtime treatment of that exact major in this exact module version.

A retired status and retired mode must agree. Other lifecycle states do not silently determine a runtime mode.

## Mode semantics

| Mode | Runtime behavior | Encoding | Eligibility contribution |
|---|---|---|---|
| `active` | Decode, validate, and use the exact major on the declared runtime path | Allowed only when the reciprocal communication direction and constrained producer permit it | May contribute to runnable, deployability, an active producer-consumer pair, and candidate green after every other gate passes |
| `compat-read` | Decode and validate the previous major for read-only compatibility evidence; do not execute its runtime domain path | Forbidden | Must not contribute to runnable, deployability, active producer-consumer inventory, or candidate green |
| `quarantine-only` | Inspect only bounded routing metadata needed to quarantine and audit | Forbidden | Must not contribute to runnable, deployability, active producer-consumer inventory, or candidate green |
| `retired` | Reject at runtime | Forbidden | Must not contribute to any runtime or release result |

A `true` eligibility value in the policy means only “may contribute”. It is never sufficient evidence by itself. Tests, reciprocal communication, exact artifacts, compatibility combinations, signed Release BOM selection, rollout controls, and all higher-risk approvals still apply.

Passing a negative quarantine test proves that fail-closed handling works. It does not turn the quarantined major into a runnable path or positive candidate evidence, and it must not increase any green count.

## Compatibility matrix v2

`governance/verification/f9-compatibility-matrix.v2.schema.json` replaces the ambiguous v1 snapshot shape for new evidence while retaining the v1 Schema for historical verification. V2 requires:

- `majorDeclarations`: the exact module, declaration kind, contract ID, major, source, lifecycle status, mode, owner, and static eligibility for every declaration;
- `policySha256`: the SHA-256 of the exact validated compatibility policy used to generate the snapshot;
- `majorDeclarations.schemaProducers`: the exact `producer_module` const/enum read from the contract Schema rather than inferred from contract ownership;
- `declarationMatrix`: the contract owner, Schema producer, transport sender, transport receiver/runtime consumer, reciprocal resolution, canonical communication-pair digest, directional class, mode class, `readCompatible`, independent/group requirements, and separate runtime/release accounting booleans;
- `axisMeaning`: N and N-1 explicitly mean candidate and previous-stable module versions selected by signed Release BOMs, not contract majors;
- `executionCombinations`: the fixed N/N, N/N-1, N-1/N, and N-1/N-1 rows; and
- top-level `candidateGreenEligible`: only the static prerequisite that all required declaration rows are eligible.

Every execution-combination row is fixed to `evidenceStatus: NOT_RUN`, `evidenceClass: candidate-artifact-required`, and `candidateGreenEligible: false`. The static compatibility snapshot therefore cannot self-issue test evidence. A separate candidate artifact must prove each required execution combination.

For `active -> compat-read`, `readCompatible` is true while `runnable`, `deployable`, `activeProducerConsumer`, and `candidateGreenEligible` are all false. Any quarantine-only or retired row also fixes all four accounting booleans to false.

An empty declaration matrix is never green. `all([])` is not an acceptable
proof: v2 requires at least one exact runtime row, and the Phase 0 aggregate
uses an explicit non-empty check before evaluating `all(...)`.

V2 also separates ordinary and grouped deployment:

- `independentDeployable` is true only when the current active pair and every required previous-major pair are runnable without coordinated release.
- `compatibilityGroupRequired` is true when the current active pair is runnable but a required previous major is intentionally unavailable because it is missing, `compat-read`, `quarantine-only`, or `retired`.

A compatibility group prevents a security-breaking upgrade from being permanently rejected merely because the old major must no longer execute. It is not an exception to evidence. Static Phase 0 output can only identify the required group; only an exact signed Release BOM plus complete candidate execution evidence for the group may authorize rollout. While that evidence is absent, `candidateGreenEligible` remains false and all execution combinations remain `NOT_RUN`.

The executable F9 validator independently rebuilds the same v2 bytes from the
raw Manifests bound by the signed BOM. It rejects v1 artifacts, missing or
unknown modes, non-fail-closed Manifest behavior, and any v2 matrix that differs
from those signed facts. Until a separately versioned full-group execution
artifact is present and bound to the exact BOM, a required compatibility group
fails with `compatibility_group_evidence_missing`; the static v2 matrix cannot
authorize it.

## Ownership is not producer identity

`contracts.provided[]` records the module that owns the public contract definition. It does not prove that the owner emits every message. A module may own an input contract whose authenticated peer is the wire producer.

The producer and consumer roles for a deployment are resolved only from:

1. the exact signed module manifests selected by the Release BOM;
2. reciprocal `communication.inbound[]` and `communication.outbound[]` entries for the same contract ID and major;
3. the contract Schema's constrained `producer_module`; and
4. the exact mode declared by each affected module for that major.

The resulting fields deliberately keep four identities separate:

- `ownerModule` owns and versions the contract Schema;
- `producerModule` is constrained by the exact Schema's `producer_module`;
- `transportSenderModule` owns the outbound communication edge; and
- `transportReceiverModule`/`consumerModule` owns the reciprocal inbound edge.

Ordinarily the Schema producer is the transport sender. A relay may differ only
when its exact outbound edge declares `preserveProducer: true`, the relay has an
exact consumed declaration for that contract major, the Schema resolves one
unambiguous original producer, and the receiver declares the reciprocal inbound
edge. The snapshot then records
`producerResolution: schema-producer-preserved-by-relay`. It never rewrites the
producer to the relay module.

`communicationPairSha256` binds a stable canonical representation of both
communication edges, including sender, receiver, contract major, transport,
timeouts, retry/idempotency/auth/failure semantics, and `preserveProducer`.
Missing, duplicate, conflicting, or one-sided edges set
`producerResolution: unresolved`, leave the digest null, and cannot be
runnable, deployable, active, independently deployable, or candidate green.

An active runtime pair is `active -> active` only after the owner, Schema producer, any relay sender, and runtime consumer all resolve to `active` and the reciprocal pair is bound. An `active -> compat-read` pair proves only bounded read compatibility; it is not a runnable or deployable execution path and supplies no candidate-green credit. Any required pair containing `quarantine-only` is unavailable for runtime and deployment accounting. Any pair containing `retired` rejects the message.

## Fail-closed resolution

For every inbound or outbound contract message, gate, matrix generator, and release evaluator:

1. Parse a bounded contract identity and explicit positive major.
2. Resolve an exact `(contractId, major)` declaration; do not fall forward or backward.
3. Reject a missing or unknown contract or major.
4. Read the explicit mode; reject a missing or unknown mode.
5. Resolve the reciprocal peer and communication direction.
6. Apply only an allowed exact mode pair.
7. Require the exact module versions, contract sources, matrix, and artifacts selected by the signed Release BOM.

`supportedContractMajors` must cover the same declared majors, but it does not supply a default mode. The per-major contract item remains the source of mode truth.

## Upgrade sequence

For a V1 to V2 breaking change:

1. Add V2 without changing the meaning of V1.
2. While V1 traffic can still execute, ship consumers that declare both V1 and V2 `active` and pass all four N/N-1 execution combinations.
3. Apply additive storage expansion and keep writes on the selected current major.
4. Enable the V2 producer only through an exact signed BOM and disabled-by-default feature flag.
5. Run shadow, canary, and the N/N-1 observation window.
6. Stop V1 production in a separate release.
7. After no V1 execution path remains, change the consumed V1 declaration to `compat-read` only for bounded historical/readback compatibility checks; this contributes no runtime or candidate-green credit.
8. Change V1 to `quarantine-only` when delayed V1 traffic must be isolated and audited; this is not positive compatibility evidence.
9. Change V1 to `retired` and remove its runtime decoder only in a later release after the retention and rollback window closes.

Any gate or matrix implementation that ignores `mode`, supplies a default for an absent mode, treats `quarantine-only` as supported runtime traffic, or counts a quarantine result as candidate green is incompatible with this policy and must fail closed.


## Proposed: execution.authorization ownership migration (P04)

**Proposed only; not effective.** This design is the bounded P04 ownership slice under DPS PDD v2.1 (§3, P04/P05/P08), based on `6f008026391f6428328f7c77f3acc3a6fcf7e1ac` (tree `80927494365920c22e9221efe6fb70b9227213d4`). It does not amend any effective module instruction, Manifest, schema, source, required suite, trust configuration or release. Command Orchestrator remains the current unique owner. In particular, its effective [module instructions](../../Modules/command-orchestrator/AGENTS.md) have not been superseded by this section. The existing compatibility matrix still records the Command-to-Policy authorization path as missing the Policy declaration; there is no production Policy authorization provider established by this design.

### Decision and dependency direction

Move the unique definition owner of `execution.authorization/v1` to the existing `policy-approval` module in a separately reviewed migration. Policy owns the authorization DTO, strict schema/codec, corpus, authorization canonicalizer and typed signing input port. Command remains the schema/wire producer, owns `command.dispatch/v1`, computes its exact digest and dispatches the signed result to Gateway. Policy is the independent signer and preserves the producer on return. No third shared module is introduced.

The declared dependency edges relevant here currently include `Command -> Policy`, `Command -> Compiler -> Policy`, `Policy -> Planner`, and `Gateway -> Command, Policy`. F9 requires a consumed contract's external owner in explicit `dependencies`; `contract-only`, `quarantine-only` and `required: false` do not waive this rule. Retaining Command ownership while adding Policy consumption creates both `Command -> Policy -> Command` and `Command -> Compiler -> Policy -> Command`. Moving only an interface into Command.Contracts, or making Policy.Contracts reference Command.Contracts, does not break those cycles.

After the proposed migration, those existing dependency directions remain; Command consumes the Policy-owned authorization and signing request, Gateway consumes the Policy-owned authorization, and Policy provides the schema and consumes the same major of each of its own authorization and signing-request contracts, with matching provided/consumed modes. There is **no Policy -> Command or Policy -> Compiler dependency**, no Policy read of their tables, and no self-dependency for Policy's self-owned consumed declaration. Communication back to Command is a relay edge, not a reverse ownership dependency. Full-repository DAG validation remains required: this local direction argument does not certify unrelated existing edges.

| Payload / direction | Unique owner after migration | Schema producer | Transport sender -> receiver | Declaration and producer handling |
|---|---|---|---|---|
| unsigned authorization, Command -> Policy | Policy | Command | Command -> Policy | Command consumed; Policy provided **and consumed**, same major and mode; direct producer edge, no `preserveProducer` |
| signed authorization, Policy -> Command | Policy | Command | Policy -> Command | Policy consumed enables relay; Command consumed; Policy outbound `preserveProducer: true` and reciprocal Command inbound |
| signed authorization, Command -> Gateway | Policy | Command | Command -> Gateway | Command/Gateway consumed; direct producer edge, no `preserveProducer` |
| typed signing request, Command -> Policy | Policy | Command | Command -> Policy | Policy schema owner provided **and receiver consumed**, same major and mode; Command schema/wire producer and consumed; reciprocal authenticated request edge; direct producer, no `preserveProducer` |
| command dispatch, Command -> Gateway | Command | Command | Command -> Gateway | Existing owner and producer unchanged |

The signing request is a new, separately versioned proposed input (`execution.authorization.signing.request/v1`), not extra fields in authorization v1. Its unsigned authorization member and signed response retain the authorization declarations above; an outer request does not hide the inner consumed contract. For each of `execution.authorization/v1` and `execution.authorization.signing.request/v1`, Policy must declare both schema-owner provided and receiver consumed at the same major and mode; those provided/consumed modes must agree because F9 resolves consumed declarations first. Neither self-owned consumed declaration adds a Policy self-dependency or a Policy -> Command dependency. Command must not set `preserveProducer` on either producer edge: a schema producer cannot claim to be its own preserving relay. Every pair must bind transport, authentication, retry, timeout, failure and idempotency semantics in the generated communication digest. This table does not activate an absent edge or change an existing active/quarantine mode.

### Byte-preserving public asset split

The current [authorization source](../../Modules/command-orchestrator/contracts/provided/Dps.CommandOrchestrator.Contracts/ExecutionAuthorizationV1.cs) mixes two responsibilities. Extract only `ExecutionAuthorizationV1`, its payload/signature validation, `CanonicalAuthorizationBytes` and `ComputeAuthorizationSha256` into Policy.Contracts. Replace its `CommandContractGuard` dependency with Policy-owned authorization-local validation of the same constraints. The pure authorization closure must compile without any Command or Compiler reference, reflection, callback into their internals, or transitive import. Reusing existing Policy validation primitives is allowed only where semantics match exactly; do not weaken UTC, GUID, scope, UTF-8, digest, range or Base64 rejection rules.

`CommandDispatchV1`, `CanonicalCommandBytes` and `ComputeCommandSha256` remain Command-owned and accept Command types there. Gateway retains the Command contract package for recomputation. Separate the private encoding helpers with these responsibilities; a small local helper is preferable to a new shared module or a public arbitrary-byte signing service. The public signer accepts the typed request below, never `byte[]`, a caller-selected algorithm/domain, or a callback producing arbitrary bytes.

Preserve schema version `1.0.0`, contract ID `execution.authorization/v1`, producer/caller `command-orchestrator`, scope `executor:dispatch`, signature domain `dps.command-orchestrator.execution-authorization/v1`, command digest domain `dps.command-orchestrator.command-dispatch/v1`, and all existing wire fields. Preserve the full field order in the existing machine specification, strict UTF-8 with uint32 big-endian lengths, GUID `N`, invariant UTC `O`, invariant decimal integers and lowercase booleans. Preserve command step ordinal, ordinal sorting of argument keys only, optional-field presence encoding and every command field including `approval_sha256`. The signature remains ECDSA P-256/SHA-256, fixed 64-byte IEEE P1363 `r || s`, canonical Base64; only `signature_base64` is excluded from authorization signing. Moving a file or namespace does not authorize any byte-semantic change.

The existing assets are `Modules/command-orchestrator/contracts/provided/execution.authorization.v1.schema.json`, `execution.authorization.v1.canonical.json` and `execution.authorization.v1.canonical.md`. The JSON canonical specification contains both command and authorization vectors; preserve its bytes and schema-bound digest when relocating the authoritative copy to `Modules/policy-approval/contracts/provided/`. Command retains normative ownership of the command digest algorithm; Policy's copy documents the commitment it receives, not a Policy implementation of CommandDispatch. Update all references atomically; do not leave two independent authoritative schemas/specifications. A changed descriptive Markdown owner statement must identify the effective migration, not rewrite historical evidence.

There is currently no separate `execution.authorization.v1.corpus.json`. The migration must preserve every existing canonical vector and `Modules/command-orchestrator/tests/Dps.CommandOrchestrator.Tests/ExecutionAuthorizationContractTests.cs` case, and add the Policy-owned shared strict corpus at `Modules/policy-approval/contracts/provided/execution.authorization.v1.corpus.json` with stable case IDs. Run the owner implementation and every migrated authorization consumer against it, including malformed UTF-8, duplicate/unknown/missing properties, wrong constants, numeric bounds, noncanonical signatures and byte differential cases. Strict bounded JSON decoding is an implementation deliverable, not a capability established by the current record DTO's `Validate` method. Any old package forwarding shim must remain one-way to Policy.Contracts, introduce no duplicate owner/codec, and be covered by the same vectors; do not promise binary compatibility from namespace migration alone.

### Typed signing request and authority boundary

The existing signer takes only an unsigned authorization. That payload has neither `approval_id` nor `approval_sha256`; `command_sha256` cannot reveal them. Treating this digest, a DTO caller name, or an asserted approval ID as approval would create a signing oracle. The proposed port therefore accepts a strict immutable Policy-owned `ExecutionAuthorizationSigningRequestV1` with these logical members (concrete serialization/schema and limits must be frozen in the later asset migration):

| Member | Required content and source |
|---|---|
| unsigned authorization | Exact v1 payload, with no caller-supplied valid signature; the port constructs the signed result only after all checks |
| approval query | `ApprovalId`, **`ProposalId`**, `SoulId`, `DeviceBindingId`, `PlatformAccountId`, `TraceId`, `IdempotencyKey`, `ApprovalSha256`, matching the existing `PolicyApprovalReadRequest` query |
| expected current authority | Approval status revision, runtime revision/state digest and release BOM binding obtained from the authoritative approval read; expectations are compared with a fresh Policy read and never confer authority themselves |
| operation binding | Operation ID, approved action kind, typed/bounded action parameters, side-effect/shadow flags and platform authorization ID, sourced from the immutable approved compilation input and checked against its compiled result |
| command commitment | Exact `CommandSha256`, command/lease IDs, attempt, lease owner/expiry and all scope/trace/idempotency/occurrence bindings; trusted Command adapter asserts the exact durable dispatch and compilation binding |
| request binding | Version, request ID, bounded issuance/expiry and retry identity; authenticated caller context binds the entire immutable request, intended Policy endpoint and selected producer artifact, not merely the hash or approval ID |

The caller principal is **not** a writable DTO field. A trusted composition root admits only the Command adapter bound to the selected artifact and least-privilege signing operation. For a remote boundary, authenticated transport must verify that principal and integrity of the entire request with replay protection; copying a `producer_module` string does not authenticate it. For an in-process boundary, composition must provide a non-publicly constructible trusted caller context and no untrusted path to the provider. The request schema and its claimed revisions are data, never substitute credentials. If the production identity/composition boundary is absent or cannot prove the binding, the signing path stays closed.

The Command adapter must obtain the durable leased dispatch and its immutable compiled operation, retain `ProposalId` and approved parameters from the authoritative compilation input (do not invent them from the dispatch hash), verify approval ID/digest, operation ID, scope, action/flags/platform authorization and compiled steps against those stored artifacts, compute the unchanged Command digest, and bind this exact request to that dispatch. Persisting a supplied `CompiledOperationV1` or matching its self-reported producer/digest is not compiler provenance. The production admission path must bind the admitted Compiler artifact and its authenticated authoritative approval read to this exact compiled result and immutable compilation input; untrusted enqueue callers cannot substitute either. This may use the existing trusted in-process composition rather than introduce another signing service, but the boundary must be demonstrated before enablement. It may not expose a public method that certifies arbitrary caller-supplied digests or arbitrary commands. If durable provenance cannot supply the query and operation binding, fail closed; future P08 must implement the necessary owner-local persistence/adapter plumbing before enabling the port. No Policy query of Command tables is permitted.

Policy uses its own `PolicyApprovalAuthoritativeClient`/owner authority path in `Modules/policy-approval/src/Dps.PolicyApproval/PostgresPolicyApprovalService.cs`; it must not trust a request-supplied snapshot or use Compiler's reader interface as an authority provider. Independently read the exact query, recompute the approval's canonical digest, require deterministic `APPROVED`, current `ACTIVE`, unrevoked/unexpired state and matching current revisions, then compare all query scope keys, action kind, bounded parameters, side-effect/shadow flags and platform authorization binding with the authoritative decision. Match the request commitment fields to the unsigned authorization and require its expiry within approval and lease validity. Independently validate the current BOM/generation/token binding and permitted signing principal through the established trusted runtime/activation authority; request assertions cannot bootstrap that authority. Rate budget, kill switch and execution fence remain Policy responsibilities. Serialize signing with applicable approval revocation/current-state controls or reject a changed revision; a successful stale read alone is not sufficient.

Policy signs only its reconstructed canonical authorization payload and returns that same payload with the signature filled. Command compares canonical payload bytes before/after signing, verifies the signature against the admitted key, then rechecks the same lease/attempt and exact durable command before dispatch. Gateway independently recomputes the Command digest, verifies authorization and current activation, and applies its current approval execution fence immediately before native submission. A signature is neither a lease nor native success.

This is an explicit trust split: Policy independently authorizes the **approval semantics and authenticated Command commitment**; the trusted Command adapter establishes that the exact dispatch/steps implement that approved compilation, and Gateway establishes that the received bytes match the signed commitment. Policy cannot reconstruct or independently prove arbitrary steps from an opaque digest. A compromised admitted Command adapter remains outside that guarantee; eliminating that trust would require another reviewed protocol/ownership change, not a hidden Command parser or reverse dependency in Policy. P08 must demonstrate that ordinary callers cannot obtain that adapter's trusted context or cause it to attest unbound input.

Required negative acceptance includes wrong principal/scope/proposal, mismatched approval or command digest, stale/revoked/expired approval or runtime revision, changed action/parameters/platform authority, substituted operation/steps, arbitrary digest, changed lease/attempt, stale BOM/token, shadow-to-active escalation, and replay under another request or scope. Exact retries may return only the same bound result while current authority remains valid; changed content under the same retry identity conflicts, and stale authority rejects even a previously successful retry. Duplicate delivery, timeout, cancellation, crash/uncertain commit and revocation races must preserve the existing failure/recovery semantics; no new command or signing permission follows from a retry.

### Complete migration surface and consumers

Paths below are future migration/review inputs, **not changes made by this document**. Re-read every affected module's `AGENTS.md`, `module.yaml`, `contracts/`, `tests/README.md` and `operations/README.md`; revise effective ownership instructions only in that future governed unit. For these six modules the exact root prefix is `Modules/<module-id>/`, with IDs `command-orchestrator`, `policy-approval`, `executor-gateway`, `audit-metrics`, `operation-compiler`, `control-plane-host`.

| Surface | Exact current path or bounded path set | Required treatment |
|---|---|---|
| authorization owner assets | The schema/canonical assets identified above; C# is `Modules/command-orchestrator/contracts/provided/Dps.CommandOrchestrator.Contracts/ExecutionAuthorizationV1.cs` | Split pure authorization from Command digest; move authority/schema/spec; add strict shared corpus and typed request assets under Policy `contracts/provided/` |
| signer and dispatch adapter | `Modules/command-orchestrator/src/Dps.CommandOrchestrator/PolicyExecutionAuthorizationSignerV1.cs`, `PostgresCommandOrchestrator.cs`, `AuthoritativeExecutionAuthorizationVerifier.cs` in the same directory | Replace local public signer type with Policy-owned typed port; preserve Command dispatch/hash/lease invariants |
| owner contract projects | `Modules/command-orchestrator/contracts/provided/Dps.CommandOrchestrator.Contracts/Dps.CommandOrchestrator.Contracts.csproj`; `Modules/policy-approval/contracts/provided/Dps.PolicyApproval.Contracts/Dps.PolicyApproval.Contracts.csproj` | Policy must have no Command reference; any Command-to-Policy reference/forwarder is one-way and explicitly reviewed |
| direct wire users | Command, Policy (new signer ingress/relay), Gateway | Rebind exact provided/consumed source, owner, major, same modes and reciprocal communication; Gateway stays a consumer, not signer |
| package regression users | Audit consumes Command.Contracts for receipts; Compiler and Host consume Policy.Contracts | Compile and run affected required suites; do not invent authorization wire-consumer edges for these package users |
| source/test projects | `Modules/<id>/src/Dps.<Name>/Dps.<Name>.csproj` and `Modules/<id>/tests/Dps.<Name>.Tests/Dps.<Name>.Tests.csproj`, for the six rows mapped below | Follow all direct/transitive ProjectReferences and corresponding `packages.lock.json`; no stale duplicate authorization type or unreviewed transitive cycle |
| generated governance | `governance/modules/module-catalog.yaml`, `governance/modules/dependency-graph.yaml`, `governance/modules/compatibility.yaml` | Regenerate with the existing generator after Manifest edits; verify unique owner and whole DAG, all exact majors/modes and execution combinations |
| validators and delivery | `Tools/ci/run_phase0_gate.py`, `Tools/verification/external_gate.py`, `Tools/ci/run_candidate_gate.py`; affected module tests/operations and existing release rules | Consumers of changed declarations, not permission to weaken F9, required flags/floors, trust or release gates |

The `<id>` to `<Name>` mapping is `command-orchestrator`/`CommandOrchestrator`, `policy-approval`/`PolicyApproval`, `executor-gateway`/`ExecutorGateway`, `audit-metrics`/`AuditMetrics`, `operation-compiler`/`OperationCompiler`, `control-plane-host`/`ControlPlaneHost`. Host tests also directly reference Policy.Contracts; the other listed tests inherit their source project's references. Gateway source currently references both Command.Contracts and Policy.Contracts; Command source references Command.Contracts and Compiler.Contracts; Compiler source references Policy.Contracts; Audit source references Command.Contracts. Review `Modules/executor-gateway/tests/Dps.ExecutorGateway.NativeFixture/Dps.ExecutorGateway.NativeFixture.csproj` as the test dependency without labelling it a wire consumer. Both owner contract projects currently have no ProjectReference; do not accidentally reverse that direction. These package relationships require regression coverage even where the authorization wire does not flow.

### Ordered rollout, rollback and unfinished work

1. Freeze this Proposed design and independently review its ownership, authority and consumer inventory. This is a P04 partial handoff, not the whole P04 completion or a runtime gate.
2. In a separate asset/governance migration unit, bind all affected instructions and consumers, split the pure authorization closure, define strict request/schema/corpus and reject paths, update the sole owner and reciprocal declarations atomically, migrate packages/references/locks, and regenerate snapshots. Preserve existing v1 bytes with before/after differential vectors. Never temporarily publish two owners or a Policy -> Command dependency. Keep existing active/quarantine treatment and required suites/floors; declarations do not establish a provider.
3. Verify the relevant required builds/suites, all direct and transitive package consumers, strict schema/canonical corpus, whole DAG/F9 owner and reciprocal-edge checks, and signed-BOM N/N-1 compatibility requirements. Namespace/package moves require coordinated candidate selection or proven compatibility; unchanged wire bytes alone do not prove old binaries load. Missing combinations remain missing, never presumed green.
4. P08 separately implements Policy provider, Command durable typed adapter and authenticated Host composition only after P07 and the applicable P04 handoff. P07 still depends on P06/P05 real authority capability. This design does not accelerate those prerequisites or convert existing synthetic/local authority into production trust. Bind signing keys and current activation through the existing authority/release process, with signing disabled until every required dependency is ready.
5. Runtime activation, canary, kill switch and release/rollback follow existing module operations, signed BOM and protected release rules. Do not add a new ordinary Owner-choice approval. Current activation/fence checks remain per native call, and real-device evidence remains separate.

Before activation, rollback means discard/revert the unactivated owner/package migration as a coherent unit to its exact base; retain historical evidence. After any future activation, first stop signing/dispatch through the existing kill switch, drain or reject outstanding leases/requests and invalidate stale authority via existing revocation/activation controls. Select a previously verified compatible BOM and whole package group; never roll back only the owner Manifest or restore an old signer that bypasses new authority/replay guards. Retain old verification material for historical validation, not permission to execute delayed traffic. If old binaries cannot safely read the new owner-local persistence or request records, keep writers stopped and use a compatible rollback build or forward fix. No runtime rollback capability is implemented here.

This two-document slice leaves actual instruction/Manifest/schema/corpus/package migration, strict codecs, provider, authenticated composition, persistence, adversarial runtime tests and deployment unfinished. The baseline's reported full Phase 0 diagnostic result (48/53, FAIL) and null formal level are not changed by this design. A quick documentation diagnostic, independent review or a future unsigned local candidate PASS does not issue a verification level, activate a signer, or complete P04/P08. Closure of the wider P04 requires its consumer migrations and PDD acceptance (including the applicable P23 verification), not merely acceptance of this proposal.
