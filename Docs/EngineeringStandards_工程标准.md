# DPS Engineering Standards 工程标准

状态: Current policy, implementation in progress<br>
日期: 2026-07-14

当前正式签发证据级别是 `NONE`; `REPOSITORY_STATIC_VERIFIED` 只能在唯一整仓 Phase 0 门禁重新通过并生成有效证据后签发. 本文中尚未有对应源码, 自动测试和原始环境证据的条目都是 required target, 不是已完成功能.

## 什么是现代软件工程标准

现代软件工程没有一张万能证书. 对 DPS 来说, 标准是一个可验证的 Definition of Done:

> 任何人在一台干净机器上都能构建, 测试, 部署, 观察, 回滚和解释系统; 系统失败时不会串设备, 丢记忆, 重复副作用或伪造成功.

是否使用最新 AI 模型不是主要标准. 更重要的是边界清楚, contract 可执行, 测试能失败, 数据可追溯, 发布可回滚.

## Must: 下一次正式发布前必须具备

### 1. 可复现构建

- 为可独立编译的 C# 代码建立 solution and project files.
- 固定 SDK, compiler, package, Python, Node, PowerShell, ADB, ZennoDroid, Android 和测试 APP 版本.
- 依赖使用 lock file.
- clean checkout 不依赖某台电脑的绝对路径.
- 构建产物不能依赖手工复制但未校验的源码.

Legacy ZennoDroid bridge 和 modern service 分开构建:

- legacy bridge 只使用目标 ZennoDroid 已验证支持的 Framework and language level.
- modern service 的实际 SDK、runtime 和 target 以 `global.json`、`toolchain.lock.json` 及模块 Manifest 为准；不得把“当前受支持的 LTS”解释为自动升级授权。2026-07-13 的推荐基线是 .NET 10 LTS，后续工具链升级作为独立变更审查.
- 现有 `Core/`, loose `Modules/*.cs`, `Modules/Core/`, `ZDProjects/` 和 `Extensions/` C# 文件保持原始 bytes, BOM 和 line endings. 迁移时不得通过全局 formatter 或 Git EOL 规则重写遗留文件.
- 新注册模块的 `Modules/<module-id>/src/` and `tests/`, 以及 `apps/`, `tests/` 和 `zenno/` modern C# 使用 UTF-8 and LF.

### 2. Versioned contract

所有跨边界数据必须有 schema and version:

- Soul and device binding.
- Persona and interests.
- Memory event.
- Command and step.
- Native result and business receipt.
- Approval and action lease.
- GBrain projection page.
- Test evidence.

规则:

- 使用 JSON Schema and OpenAPI.
- Product/runtime DTOs use a SemVer `schema_version` such as `1.0.0` plus a major-bound `contract_id` such as `memory.event/v1`. Governance and Factory evidence envelopes may use a self-identifying constant such as `dps.release-bom/v1`. A single contract must choose exactly one convention, constrain it in Schema, and preserve it in canonical hashing; consumers must not infer compatibility from a loose string.
- unknown version fail closed.
- v1 内只做 additive change.
- breaking change 使用新 major version.
- schema migration 可 dry-run, 幂等, 可审计, 可回滚.
- 安全标识符、摘要、签名和版本字符串同时声明精确 `minLength`/`maxLength` 与 ECMAScript-safe absolute-end pattern；普通 `$` 不能单独作为末尾边界，因为 Draft 2020-12 validator 可能接受尾随换行.
- 所有 owner Schema 必须有 provider-owned valid/invalid corpus；消费者复用同一 corpus 做 parser/Schema differential，至少覆盖 LF、CR、空格、额外字符、大小写、短值、长值、未知 major、重复 key 和未知字段.
- JSON integer 若映射到 .NET `long`/PostgreSQL `bigint`，Schema 必须固定 `-9223372036854775808` 到 `9223372036854775807`，corpus 还要包含相邻溢出值。禁止用 JavaScript `Number` 或其他 IEEE-754 binary64 路径读取后重写这类 golden corpus；否则 64 位整数可能在无报错时被改写.
- 跨模块时间只接受明确的 zero-offset UTC wire value，并在 Schema、DTO、canonical bytes 和数据库约束间做 parity test；本地时间或未知 offset 失败关闭.

### 2.1 Module governance and independent upgrade

每个注册模块的逻辑 home 是 `modules/<module-id>/`. 当前保守迁移的物理 home 是 `Modules/<module-id>/`, 因为现有 legacy 目录与 lowercase 名称在 macOS/Windows 大小写不敏感文件系统上是同一路径:

```text
Modules/<module-id>/
├── AGENTS.md
├── module.yaml
├── src/
├── contracts/provided/
├── contracts/consumed/
├── tests/
├── migrations/
├── operations/
└── CHANGELOG.md
```

硬规则:

- 每个模块恰好一个 root `AGENTS.md` and one `module.yaml`; nested AGENTS is forbidden.
- 不得并存 `Modules/` 和 `modules/`; legacy 退休后的 case-only normalization 必须单独审查且不混入行为变化.
- 每个路径和 public contract 恰好一个 owner.
- Manifest 声明 ownership, artifacts, runtime, contracts, communication, dependencies, permissions, data, tests, compatibility, rollout and device gates.
- 模块 AGENTS 必须要求升级 AI 读取 Manifest, provided and consumed contracts, dependency graph, compatibility matrix, tests, canary, rollback and communication interfaces.
- AI instruction receipt 记录 root and module AGENTS hashes, Manifest hash, base commit and ordered scope. Diff 扩大或 instruction 改变使 receipt stale.
- Receipt 证明读取, 不证明 correctness. 实现者, evidence issuer and release approver remain separate.
- 单模块可独立 build, test, version and package. 有关联模块由 DAG, compatibility matrix and Release BOM 排序, 不假装完全独立.
- 首期禁止任意第三方代码插件和 unsigned in-process hot replacement.

### 2.2 指令适用主体与维护

模块文档中的 Factory runtime invariants 描述必须实现和保持的产品行为。例如 `factory-control-plane-host` 中九个角色相互独立、implementer 不能兼任测试或审批角色的规则，约束该 Factory workflow；阅读或修改模块文件本身不会给当前开发会话分配其中任何角色。

当前会话的写入范围和角色来自有效用户授权、工单及适用治理规则。若会话已被分配到正式 Factory、独立 QA 或冻结工单，继续执行该角色的完整限制；不得因“这是本地开发”而自行豁免。普通开发中的补充测试要求也不授予独立证据签发、治理自审批或发布权限。角色绑定或授权确有冲突时，只暂停依赖该冲突的动作，报告具体文件、条款和缺失事实，继续已授权且不依赖它的工作。

指令按以下职责维护：

- 根 `AGENTS.md` 保存仓库通用边界、模块读取顺序、验证入口和完成条件。
- 模块 `AGENTS.md` 保存模块职责、必须保持的不变量、必读引用及特有停止条件；保留每模块一份文件的规则。
- 字段集合、限额、具体协议和兼容矩阵由模块合同、Manifest、测试及运维文档承载。移动细节时保留原有约束和可定位引用，不能仅因已有测试就删除安全不变量。
- 不复制个人全局偏好或整篇模型提示指南；新增规则应解决已观察到的问题。指令整理仍需重新绑定，不得弱化冻结清单、角色隔离或审批节点。

### 3. 自动化测试分层

| 层级 | 目的 | 例子 |
|---|---|---|
| Unit | 验证纯业务规则 | interest reducer, policy, idempotency, parser |
| Contract | 验证模块和数据闭包 | operation to step, schema, variable writer and reader |
| Integration | 验证真实组件协作 | Postgres, outbox, GBrain adapter, local spool |
| Device E2E | 验证真实手机行为 | Locate, Tap, Verify, crash recovery |
| Soak and chaos | 验证长期和故障行为 | 断网, timeout, restart, duplicate delivery |

硬规则:

- required test 的 FAIL, SKIP, PARTIAL, missing evidence 都返回非零退出码.
- mock test 清楚标记, 不能冒充 device E2E.
- 测试不得预置一条绕过生产决策链的 golden path.
- native execution and business postcondition 分开验证.
- Mutation 必须测试 duplicate, crash window, timeout and recovery.

### 4. CI and device gates

Hosted CI 至少运行:

- secret scan.
- JSON and schema validation.
- encoding and path checks.
- C# build.
- Unit, Contract and Integration tests.
- documentation link checks.
- dependency vulnerability checks.

受信门禁执行规则:

- 可执行工具只能从版本锁和固定候选路径解析，不能信任 ambient `PATH`, `HOME` 或调用者提供的工具变量.
- 每次门禁使用独立、owner-only 的临时 HOME、CLI cache、scratch 和 temp；测试不得继承用户 NuGet 配置或共享可变 CLI 状态.
- .NET restore 必须同时使用锁文件、仓库 `NuGet.Config`、static graph、NuGetAudit `true/all` 和不可被项目覆写的命令行属性；漏洞源或平台安全服务不可用时结果为 `INFRA_ERROR`，不得关闭审计重跑.
- 超时必须终止整个子进程组，不能只停止顶层进程后留下 MSBuild、测试或设备子进程.
- Evidence 与 receipt 只能写入规定的 Git-ignored evidence root，不能覆盖 tracked 文件或共享相同路径；文件名、伴随证据命名空间、符号链接与并发写入均失败关闭.
- Phase 0 与 Candidate 未显式指定路径时，必须使用由受信 Python runner 生成的唯一 run-id 目录；已 COMMITTED 的逻辑路径不得覆盖.
- CI 必须以完整证据目录为 artifact 单元，同时传输 payload、publication marker 和可能存在的 quarantine claim；下载后任何 claim 仍使 reader 失败关闭.
- claim 删除后必须 fsync 父目录；持久化失败时必须恢复 quarantine claim 并禁止将可见的 COMMITTED marker 当作可信证据.
- JSON evidence 必须用单一安全文件描述符读取、计算摘要并解析；正式签发前后都要复核 HEAD、workspace digest 和 clean 状态.
- 本地同一 OS 用户下运行测试和签发者仍不构成强隔离。正式 Factory 必须把 untrusted test runner、evidence issuer 和 release approver 放在独立身份及权限边界中.

Windows self-hosted gate 运行:

- ZennoDroid template and bridge load.
- ADB and authorized fixture device.
- native positive and negative actions.
- postcondition verification.
- cross-device isolation.
- recovery and duplicate-delivery tests.

证据状态只使用 `PASS`, `FAIL`, `SKIP`, `PARTIAL`, `NOT_RUN`, `INFRA_ERROR`, `NOT_APPLICABLE`. Required evidence 只有 `PASS` 可以放行.

累计验证等级固定为:

1. `REPOSITORY_STATIC_VERIFIED`: repository structure and static gates passed.
2. `CONTRACT_VERIFIED`: module, schema, ownership, dependency and compatibility contract gates passed.
3. `INTEGRATION_VERIFIED`: real declared service dependencies and recovery paths passed.
4. `WINDOWS_VERIFIED`: target Windows and ZennoDroid load, Edge A/B and no-restart gates passed.
5. `DEVICE_VERIFIED`: authorized fixture devices and GBrain read-back gates passed.
6. `CANARY_VERIFIED`: declared 30-device production canary and rollback evidence passed.
7. `SCALE_VERIFIED`: 200 managed devices, load, recovery and 72-hour soak passed.

Hosted CI, mock, simulator, Windows and device evidence must be labelled separately. Hosted CI alone can never issue `WINDOWS_VERIFIED` or a higher level. 单项检查结果、门禁支持的等级上限和已正式签发的等级分别报告；Hosted CI 的绿色状态不能证明未执行的合同或集成套件通过。唯一 Phase 0 的等级上限是 `REPOSITORY_STATIC_VERIFIED`，上限本身不是已取得的等级。

当前状态入口是 [README](../README.md)，签发条件见 [RepositoryProtection_仓库保护.md](Operations/RepositoryProtection_仓库保护.md)。本地 Phase 0 workspace diagnostic 不签发正式等级；Contract/Integration Candidate 无论工作区是否干净，都保持 unsigned、`verification_level=null`、`candidate_verification_level=null`、`formal_evidence_eligible=false`。后续等级必须由对应可执行门禁、精确候选的原始证据和独立签发条件共同支持。

### 4.1 验证入口与适用条件

下列入口都已存在于仓库；本表只提供导航，不新增门禁。先读取受影响模块 `module.yaml` 的 `tests.suites`、合同和兼容关系，确定全部适用检查，不以一次定向检查代替必需门禁。

| 目的 | 入口 | 使用条件与结果边界 |
|---|---|---|
| 文档链接、JSON、Python 语法和差异空白检查 | [validate_repo.py](../Tools/ci/validate_repo.py) | 快速仓库诊断；不是完整 Phase 0，也不签发等级 |
| 唯一整仓 Phase 0 | [run_phase0_gate.py](../Tools/ci/run_phase0_gate.py) | 绑定基线、指令和完整必需清单；正式运行要求 clean checkout |
| Contract/Integration Candidate | [run_candidate_gate.py](../Tools/ci/run_candidate_gate.py) | `--level contract` 或 `--level integration`；integration 累计执行两层；只输出 unsigned candidate |
| 模块构建与测试 | 目标 `module.yaml` 的 `artifacts[].build` 和 `tests.suites` | 遵循锁定工具链及环境要求；模拟、真实数据库和设备证据分开 |
| Windows、ZennoDroid、GBrain 与设备外部验证 | [ExternalVerification_F6-F9_外部验收.md](Platforms/ExternalVerification_F6-F9_外部验收.md) | 只在所需授权、受信环境和前置证据齐备时运行；本地诊断不能替代 |

执行前核对 `.python-version`、`global.json`、`toolchain.lock.json` 和依赖锁。已有 `.venv` 也须核对实际 Python 与依赖版本；[bootstrap-ci-python.sh](../scripts/bootstrap-ci-python.sh) 会拒绝错误的 Python 版本，并按 `requirements-ci.txt` 的哈希安装依赖。以下是 POSIX 命令模板，须在仓库根目录、锁定环境就绪后执行。`DPS_BASELINE_COMMIT` 必须先设置为本次评审所绑定的完整基线 SHA；不得用当前 HEAD 自动替代正式前代基线。

```sh
# 快速检查
./.venv/bin/python Tools/ci/validate_repo.py

# 整仓 Phase 0 的显式工作区诊断
./.venv/bin/python Tools/ci/run_phase0_gate.py --base "${DPS_BASELINE_COMMIT:?set the bound baseline SHA}" --diagnostic-workspace

# 仅在适用时选择所需 Candidate 层级；integration 累计包含 contract
./.venv/bin/python Tools/ci/run_candidate_gate.py --level contract --base "${DPS_BASELINE_COMMIT:?set the bound baseline SHA}" --diagnostic-workspace
./.venv/bin/python Tools/ci/run_candidate_gate.py --level integration --base "${DPS_BASELINE_COMMIT:?set the bound baseline SHA}" --diagnostic-workspace
```

正式运行必须另行满足 clean checkout、前代信任根、独立身份和签发条件，不能只删除 `--diagnostic-workspace` 就视为已满足。依赖、受信 PostgreSQL 或外部环境缺失时，保留实际 `INFRA_ERROR`/`NOT_RUN` 及非零退出码，不降低版本、关闭审计或删减必需套件。

临时任务计划留在任务或 PR 中；禁止新增代理私有状态目录或替代项目门禁。受治理工具生成的 receipt、原始证据、publication marker 和 quarantine claim 按本节既有规则保存在规定的 Git-ignored evidence root。它们不属于被禁止的私有任务状态，也不能据此新增一套审批机制。

#### Ledger PostgreSQL environment boundary

The central Ledger suite forwards only its dedicated
`DPS_FACTORY_EVIDENCE_POSTGRES_MIGRATION_URI`,
`DPS_FACTORY_EVIDENCE_POSTGRES_ADMIN_URI`,
`DPS_FACTORY_EVIDENCE_POSTGRES_RUNTIME_URI`, append HMAC key, revocation epoch
and `DPS_PSQL`. Host retains its generic family; Artifact retains its database
URLs. Any present dedicated connection key, including an empty string, prevents
standalone fallback to the complete legacy family. Both schema key inventories
and the PostgreSQL connection condition must agree with the execution allowlists.

Use the Ledger [operations guide](../Modules/factory-evidence-ledger/operations/README.md)
for input validation, all-three `180004`/identity readback and authorized SQL
side effects. A real wrong-version control must differ from `180004`.
Separate native exit, actual runner status and diagnostic infrastructure reason.
Preserve ordinary exit-1 `FAIL` even when stderr contains `INFRA_ERROR`.
No-PG tests of forwarding/preflight do not award a Candidate level or replace
required real PG, cumulative gates, independent QA or release approval.

### 5. Observability

每次 command 必须能关联以下链路:

```text
trace_id
-> soul_id
-> device_binding_id
-> platform_account_id
-> session_id
-> command_id
-> step_id
-> native result
-> business result
-> memory event
-> GBrain projection
```

日志使用 structured JSON, 不把正文, token, screenshot, device serial 或完整账号写入普通日志.

最低指标:

- active devices and leases.
- command latency and error rate.
- verified action success.
- duplicate and unknown outcomes.
- cross-Soul access denials.
- event outbox lag.
- GBrain projection lag and failures.
- approval pending and expired.
- kill switch state.

### 6. Security and privacy by design

- Soul ID 是业务 ID, 不是 credential.
- 每个 service 使用独立 OAuth client and minimum scope.
- 手机和 ZennoDroid 不保存 GBrain secret.
- Secret 进入 secret manager, 不进入 Git, JSON config, prompt or log.
- screen, OCR, UI XML and platform content are untrusted input.
- model output only creates a proposal, not direct authority.
- sensitive actions require policy, approval and short action lease.
- screenshots default ephemeral and encrypted.
- every personal-data field declares purpose, consent and retention.
- deletion covers primary data, cache, GBrain page, chunks, embeddings and backups.

### 7. Reliable data and idempotency

- Runtime events use append-only ledger.
- Outbox and business change commit in one database transaction.
- Each event and command has idempotency key.
- same ID and same hash is no-op.
- same ID and different hash is conflict and quarantine.
- clocks do not decide ordering; use sequence and writer epoch.
- GBrain health or queued response is not write success. Use exact read-back.

### 8. Safe release

- Use semantic versioning.
- Release only from a clean, reviewed commit and protected tag.
- Do not use broad `git add .` in release automation.
- Package includes checksums, contract versions, migration versions, toolchain lock and test evidence.
- Rollout order is sandbox, one-device canary, small pilot, cohort, fleet.
- Each rollout has a feature flag, owner, expiry, rollback and kill switch.
- One cross-Soul leak, approval bypass or false success stops rollout immediately.

## Should: 十台设备试点前完成

- Explicit `SessionContext` or one isolated worker per device.
- One active lease per device and platform account.
- Local encrypted SQLite spool with WAL.
- Postgres backup and restore drill with measured RPO and RTO.
- GBrain source isolation fuzz tests.
- Persona proposal and approval UI.
- Interest evidence and decay inspection UI.
- Fleet dashboard with pause, resume, quarantine and replay-safe recovery.
- Fourteen-day pilot observation before enabling wider mutations.
- N/N, N/N-1, N-1/N and N-1/N-1 contract compatibility during rolling upgrade; unknown N+1 fails closed.

Suggested pilot SLO, only after evidence exists:

- cross-Soul memory contamination: 0.
- duplicate mutation caused by retry: 0.
- approval bypass: 0.
- verified memory event durability: at least 99.9 percent.
- verified read-only command success: at least 99 percent.
- projection backlog recovery: within the declared RTO.

## Later: 规模化阶段

- Device farm covering target models and Android versions.
- Seventy-two-hour final soak and two-times-target simulated load test.
- Contract and state-machine fuzz testing.
- Selector drift replay library.
- OpenTelemetry collector and centralized dashboards.
- SBOM, artifact signing and build provenance.
- Automated canary rollback based on error budget.
- Capacity model for GBrain sources, OAuth clients, token minting, Postgres connections, embeddings and search latency.

## Repository target layout

所有独立升级单元使用一个 module root. `apps/` 或 `zenno/` 可以作为制品输出或 legacy input, 但不能成为平行的模块治理根:

```text
Modules/                 # transitional physical root; logical name is modules
  <module-id>/
    AGENTS.md
    module.yaml
    src/
    contracts/
      provided/
      consumed/
    tests/
    migrations/
    operations/
    CHANGELOG.md
Docs/
  Architecture/
  Platforms/
Tools/
  ci/
.github/workflows/
```

Do not move existing runtime files into this layout in one large rewrite. `legacy-runtime-adapter` temporarily owns the loose legacy runtime paths through its Manifest, while registered module directories live beside them under `Modules/<module-id>/`. Build a new vertical slice beside the legacy path, run shadow and canary, then reduce legacy ownership one capability at a time after parity evidence.

## Pull Request checklist

A normal change should answer:

- What current behavior is proven?
- Which root and module instructions were bound, and are their hashes current?
- Which Manifest-owned paths, contracts, dependencies and consumers are affected?
- What contract changes?
- What fails before this change?
- What tests now pass?
- What environments were not tested?
- Does this touch personal data or a side effect?
- Is migration backward compatible?
- What metric proves rollout safety?
- How is it rolled back?
- Which verification level is supported by raw evidence, and which levels remain unverified?

## Current modernization gates

Before GBrain memory is production-ready:

- stable Soul binding exists.
- one Soul cannot read another source.
- event journal and outbox work offline.
- projection uses exact read-back.
- current Persona uses deterministic lookup.
- search result source and freshness are validated.
- delete removes page, chunks and embeddings.

Before ZennoDroid mutation is production-ready:

- command and receipt contracts are versioned.
- unknown and expired command fails before action.
- result is never marked success before postcondition verification.
- duplicate delivery creates no duplicate mutation.
- local and global kill switches work.
- target platform authorization is documented.
