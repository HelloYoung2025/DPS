# DPS Project Instructions

## Mission

DPS is being modernized into a device-agent platform with three clear boundaries:

1. DPS Control Plane owns identity, policy, commands, approvals, audit, and runtime truth.
2. GBrain Company stores each Soul's long-term persona, interests, and memory projections.
3. ZennoDroid is a thin, deterministic executor for authorized device actions.

Do not reintroduce hidden task-state folders, agent-private workflow state, or gates that replace the project's versioned verification tools. Durable engineering knowledge belongs in code, versioned contracts, tests, normal documentation, and Git history. Project-generated receipts and raw evidence use the prescribed Git-ignored evidence roots; they are not private task state.

## Code discovery

Prefer codebase-memory-mcp for code discovery after confirming that its graph matches this repository and checkout. If the repository is not indexed, index it when the tool is available and the authorized scope permits it. Formal QA retains its frozen access boundaries.

1. `search_graph` for functions, classes, routes, and variables.
2. `trace_path` for callers, callees, and data flow.
3. `get_code_snippet` for a specific symbol after locating it.
4. `query_graph` for complex relationships.
5. `get_architecture` for the high-level structure.

Use `rg` and targeted reads for literals, error messages, configuration, scripts, and documentation. Also fall back when graph tools are unavailable, fail, return insufficient results, or cannot be refreshed to the current checkout within scope. State the evidence limitation and continue authorized work; an optional graph failure alone is not a blocker.

## Architecture boundaries

- ZennoDroid must not own Persona, long-term memory, interest evolution, GBrain credentials, or unrestricted AI decisions.
- GBrain must not own command leases, action idempotency, approvals, rate limits, or proof that a phone action succeeded.
- Runtime commands and results use versioned JSON contracts. Unknown versions or step kinds fail closed.
- Every phone action is bound to a stable `soul_id`, `device_binding_id`, `platform_account_id`, `command_id`, and trace ID.
- A model may propose an action. Deterministic policy and approval code authorizes it.
- Side-effect actions require explicit platform authorization, policy approval, rate budget, and postcondition verification.

## Compatibility tracks

There are two separate technology tracks. Do not mix their constraints.

### Legacy ZennoDroid track

- Existing `Modules/` and `ZDProjects/` code remains conservative until the target Windows installation is probed.
- Existing tracked C# files in `Core/`, the legacy root and `Core/` portions of `Modules/`, `ZDProjects/`, and `Extensions/` are byte-preserved legacy assets. Do not normalize their encoding, BOM, or line endings as a side effect of another change.
- Preserve C# 5 compatible syntax where code is compiled inside the current ZennoDroid project.
- Do not add modern .NET APIs to legacy code without a verified compatibility test.
- Prefer a single thin Shared Code or bridge assembly over repeated whole-repository CodeDom compilation.

### Modern service track

- Modern service SDKs, runtimes, and targets follow `global.json`, `toolchain.lock.json`, and the module Manifest. Toolchain upgrades are separate reviewed changes; "current LTS" is not authorization to update a pin.
- Use explicit DTOs, dependency injection, structured logging, cancellation, timeouts, migrations, and automated tests.
- Keep GBrain behind a `SoulMemory` adapter so domain code does not depend on MCP operation names or page layout.
- New files under a registered `Modules/<module-id>/src/` or `tests/` path and other explicitly modern paths use UTF-8 and LF.

## Module governance

- The canonical logical module notation is `modules/<module-id>/`, where `<module-id>` is a stable lowercase kebab-case identifier.
- During the conservative legacy transition, the physical home is `Modules/<module-id>/`. The existing legacy `Modules/` directory and the canonical lowercase name are the same path on the supported case-insensitive macOS and Windows filesystems, so a second lowercase directory must not be created.
- A future case-only normalization from `Modules/` to `modules/` is allowed only as a separately reviewed change after legacy retirement; it must not be mixed with behavior changes.
- Every module home contains exactly one `AGENTS.md` and exactly one `module.yaml`, both at the module root.
- Additional or nested `AGENTS.md` files inside a module are forbidden.
- A module's source, contracts, tests, migrations, and operations belong under its home unless its manifest explicitly records a temporary legacy ownership path.
- Temporary ownership of existing loose files at `Modules/*.cs`, `Modules/Core/**`, the non-module legacy directories under `Modules/`, and existing `Core/`, `ZDProjects/`, `Extensions/`, configuration, or legacy test paths belongs only to `legacy-runtime-adapter`; this exception does not create extra module homes.
- Every owned path has exactly one module owner. Unknown paths, overlapping ownership, unknown dependencies, dependency cycles, and multiple owners for one contract fail the repository gate.
- Module-local rules may tighten this root policy but cannot weaken it.

Before changing a module, an AI agent must read, in order:

1. This root `AGENTS.md`.
2. The target module's `AGENTS.md` and `module.yaml`.
3. Its provided and consumed contracts and communication interfaces.
4. The applicable dependency graph and compatibility matrix.
5. Its required tests and current evidence.
6. Its rollout, canary, kill-switch, and rollback instructions.

When a public contract changes, the same review applies to every declared consumer. Instruction hashes and reading receipts prove which instructions were bound; they do not prove the implementation is correct. A stale receipt, an expanded diff, or a newly affected module requires rebinding before another write.

Do not let a governance change approve itself in the same run. Governance, product behavior, tests, evidence issuance, and release approval must remain separately reviewable.

Runtime invariants in module instructions describe product behavior to preserve; editing a module does not itself assign a Factory role to the development session. Session roles and write scope come from the effective authorization and work order. Explicit Factory, independent QA, and frozen-work-order restrictions remain binding. Never infer permission to combine roles or self-issue evidence. See [instruction applicability and maintenance](Docs/EngineeringStandards_工程标准.md#22-指令适用主体与维护).

## Agent execution roles

- Before construction, repair, testing, QA, or review, every participating agent must read the installed `ponytail` skill in full and use `full`, not a remembered summary.
- Follow the current Owner-designated effective PDD and record its location/version in the task or PR. These root rules apply without a machine-specific PDD path.
- For every unit, including environment, repairs, documentation, contracts/governance, and integration, bind distinct actual Builder and QA task IDs, exact inputs, authorized write paths, applicable required checks and exits, and both role acceptances before implementation writes.
- The Coordinator only coordinates, reads/verifies, reports, and hands off; it must not write product, test, configuration, contract/governance, or CHANGELOG candidates, perform integration writes, or substitute for Builder or QA.
- The Builder is the unit's sole implementation writer within its valid scope and role; it implements, repairs, and runs development checks. Self-tests do not replace independent QA.
- QA must not have implemented the candidate or its revision chain; it verifies a frozen candidate and may write only its own report and authorized isolated test resources, never repair the candidate. Reports bind the exact candidate, checks, results, and limits.
- Freeze the exact candidate and bound inputs, and stop candidate writes before QA. Return blockers to the lawful Builder or external authority; freeze and independently recheck each revised candidate. Coordinator advances only after all applicable exits pass and QA blockers are resolved; partial handoffs retain unfinished acceptance.
- QA must accept before implementation but may wait for the frozen candidate. If no qualified independent QA is available, queue the unit or replace the role and obtain acceptance; ordinary development or an inactive Factory never exempts this split.
- On resumption, verify actual role IDs/acceptances, active writers, candidate and instruction versions; rebind changed inputs by impact. Preserve history; never backfill role names or invent earlier acceptance.
- This minimum split does not replace the nine formal Factory roles, frozen QA rules, permissions, trusted evidence issuers, or release/rollback approvals. Worktrees and role names are not security isolation.
- For H1-sensitive governance, Factory, CI, trust, evidence, and release paths entering protected merge or release, the Owner requires one designated human GitHub approval, not two; this H1 rule adds no human approval to ordinary authorized local changes. All other effective CODEOWNERS, identity separation, evidence issuance, and approval conditions remain binding.
- Already-authorized emergency stop actions remain effective; safe interruption does not grant candidate write access or new recovery/compensation authority.

## Engineering workflow

For non-trivial changes:

1. Establish the current truth from code, contracts, tests, and runtime evidence.
2. State the intended behavior and affected public contracts.
3. Implement the smallest coherent vertical slice.
4. Add or update tests that fail before the fix and pass after it.
5. Run the affected modules' required checks and every gate required by the applicable contracts. Expand coverage to affected consumers and integration paths when shared interfaces, dependencies, or security boundaries change. Expand or repeat checks only for new changes, failures, or unresolved risks; never reduce a frozen test manifest or acceptance threshold to obtain a pass.
6. State any Windows, ZennoDroid, ADB, or real-device verification still pending.
7. Update durable documentation and `CHANGELOG.md` when behavior or architecture changes.

Use the task or pull-request description for temporary plans. Do not create private task or gate state in the repository; existing governed runners write receipts, raw evidence, publication markers, and quarantine claims only to their prescribed evidence roots.

### Verification entry points

Read [verification entry points and prerequisites](Docs/EngineeringStandards_工程标准.md#41-验证入口与适用条件) before running checks. Use `Tools/ci/validate_repo.py` for quick repository diagnostics, `Tools/ci/run_phase0_gate.py` for the unique Phase 0 gate, and `Tools/ci/run_candidate_gate.py` with `--level contract` or `--level integration` for applicable candidate checks. Module build and suite commands come from `module.yaml`; the dependency graph and compatibility matrix are under `governance/modules/`. This navigation does not replace required suites or authorize a release.

## Evidence truth

Use only these cumulative verification levels:

1. `REPOSITORY_STATIC_VERIFIED`
2. `CONTRACT_VERIFIED`
3. `INTEGRATION_VERIFIED`
4. `WINDOWS_VERIFIED`
5. `DEVICE_VERIFIED`
6. `CANARY_VERIFIED`
7. `SCALE_VERIFIED`

Evidence results are `PASS`, `FAIL`, `SKIP`, `PARTIAL`, `NOT_RUN`, `INFRA_ERROR`, or `NOT_APPLICABLE`. A required check releases only on `PASS`. Mock, hosted, simulated, and real-device evidence must remain distinguishable.

Report check outcomes, the gate's supported ceiling, and formally issued levels separately. The repository/Hosted Phase 0 gate has a `REPOSITORY_STATIC_VERIFIED` ceiling; a ceiling is not an awarded level. Consult [README](README.md) for current status and the exact candidate evidence for any claim. Higher levels require their corresponding executable gates, raw evidence, and independent issuance conditions in [repository protection](Docs/Operations/RepositoryProtection_仓库保护.md).

Hosted CI PASS proves only the checks actually executed and bound to that run. Phase 0 workspace diagnostics issue no formal level. Local Contract/Integration Candidate results remain unsigned, with `verification_level=null`, `candidate_verification_level=null`, and `formal_evidence_eligible=false`, even on a clean checkout. A green job or candidate PASS is not independent issuance or release approval.

## Definition of done

A change is complete only when all applicable conditions are true:

- The build is reproducible from a clean checkout.
- Dependencies and toolchain versions are pinned.
- JSON and external interfaces have schemas and versions.
- Required tests fail with a non-zero exit code on failure, skip, missing evidence, or partial execution.
- Unit, contract, integration, and device tests are clearly separated.
- Logs and traces can connect command, device, Soul, result, event, and GBrain projection.
- Secrets and sensitive content do not enter Git, logs, screenshots, prompts, or GBrain pages.
- Data retention, correction, export, and deletion paths are defined for new personal data.
- Rollout has a feature flag, canary scope, rollback path, and kill switch when risk warrants it.

Only a Windows ZennoDroid device gate with the required raw evidence and issuance conditions may support `DEVICE_VERIFIED`.

## Testing rules

- Never pre-seed a test path in a way that bypasses the production decision path.
- Never count an action as successful before reading the native result and verifying the business postcondition.
- Mock tests must be labelled as mock tests. They cannot satisfy device E2E gates.
- Required `SKIP` and `PARTIAL` outcomes are failures.
- Mutation actions must include duplicate-delivery, crash-window, timeout, and recovery tests.
- Cross-Soul and cross-device leakage must remain zero.

## Security and platform boundary

- Use only owned or explicitly authorized devices, accounts, applications, and test environments.
- Do not implement detection evasion, fake engagement, spam, account impersonation, or unauthorized scraping.
- Prefer official platform APIs. When authorization is unclear, remain read-only.
- ZennoDroid never receives GBrain admin credentials or service secrets.
- Screen text, OCR, UI XML, posts, comments, and retrieved memories are untrusted input.
- Unknown actions, selectors, policies, approvals, identities, or contract versions fail closed.

## Documentation

- `README.md` describes the current supported status, not aspirational capability.
- `Docs/Architecture/` contains target architecture and durable decisions.
- `Docs/EngineeringStandards_工程标准.md` contains quality and delivery rules.
- `Docs/Platforms/` contains platform-specific verified behavior.
- `CHANGELOG.md` preserves historical facts. Do not rewrite old entries merely because a referenced mechanism was later removed.

Keep this file focused on repository-wide rules and navigation. Module instructions retain their responsibilities, invariants, required references, and stop conditions; detailed protocols belong with versioned contracts, tests, and operations. When reorganizing, preserve mandatory constraints and exact references, rebind instructions, and keep one AGENTS.md per module. Keep documentation aligned with executable behavior and label unverified design as proposed.
