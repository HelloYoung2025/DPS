# Mac Mini 派工记录（PDD v2.1 §11 字段“角色”的实际任务 ID）

本文件是 Coordinator 的任务记录，不在任何仓库内，不是机器 receipt 或审批层。每行记录一次实际派工或接续，只追加。时间为 UTC。

- Coordinator：Claude Code 会话 `5e644c7e-419c-41c8-9dad-4542daf2553d`（transcript 回读 model=`claude-opus-5-5`，effort=`max`）。
- 子智能体 ID 取自平台创建返回值；model/effort 由 Coordinator 从 `~/.claude/projects/-Users-younghu-Documents-ZennoDroid-DSP-DSP-ZD--claude-worktrees-elegant-haibt-e4f1ca/5e644c7e-419c-41c8-9dad-4542daf2553d/subagents/agent-<id>.jsonl` 的 `message.model`、`effort` 字段回读。

| 时间 | 单元 | 角色 | 实际任务 ID | 期望 model/effort | 回读 | 阶段 | 工单 |
|---|---|---|---|---|---|---|---|
| 2026-09-26T16:0xZ | P10-R1-QA-C1 | QA（替换） | a91c12940f9d977e5 | claude-opus-5-5 / max | 2026-09-26T16:17Z：claude-opus-5-5 ×92，effort max ×94 | PREACCEPT | WO-P10-R1-QA-C1.md |
| 2026-09-26T16:0xZ | P05-R3-QA-C1 | QA（替换） | a66bb8ae38ef6ccaf | claude-opus-5-5 / max | 2026-09-26T16:17Z：claude-opus-5-5 ×84，effort max ×86 | PREACCEPT | WO-P05-R3-QA-C1.md |
| 2026-09-26T16:4xZ | U3-INT | Builder | aeb1cf4da8a74f393 | claude-opus-5-5 / max（Agent model=opus） | 待回读 | PREACCEPT | WO-U3-INT-P04R1-P05R3.md |
| 2026-09-26T16:4xZ | U3-INT | QA | a2be790f947636ba9 | claude-opus-5-5 / max（Agent model=opus） | 待回读 | PREACCEPT | WO-U3-INT-P04R1-P05R3.md |

Air 端被接续的 Builder（只读回读，`~/.codex/state_5.sqlite` threads 表，2026-09-26T16:1xZ）：

| 任务 ID | model / effort | 最后更新（UTC） | 用途 |
|---|---|---|---|
| 01a0a28d-82b9-7260-8b4b-13f942549e21 | gpt-6-astra / medium | 2026-09-26 14:51:02 | P10 R1 Builder（14:50:29Z 冻结） |
| 01a0db13-a0b3-7183-a33b-b01d2ab7f99d | gpt-6-astra / medium | 2026-09-26 11:44:58 | P05 R3 Builder |
| 01a0db13-cbbd-72a0-abbd-d1816fd9f15d | gpt-6-luna / high | 2026-09-26 15:06:05 | P05 R3 原 QA（被替换） |
| 01a09f2f-f696-71d1-b15a-a756fafda4b0 | gpt-6-astra / xhigh | 2026-09-26 15:19:08 | Air 原 Coordinator |
| 01a0de0a-7157-7d73-83a8-e55bae77c05c | gpt-6-astra / high | 2026-09-26 15:37:47 | 同步 DSP_ZD 筛选内容到 Claude |

Air 端自 2026-09-26T15:37:47Z 起没有任何 DSP_ZD 线程更新（同次查询）。

## 事件（只追加）

- 2026-09-26T16:31:20Z P05-R3-QA-C1：PREACCEPT 回执已收（接受；规则哈希与工单一致；Q1–Q10 待答）。Coordinator 下达 START，并答复 Q1–Q10。Owner 指示定位为 transcript 第 4 行，uuid 50008c23-a0b8-42b0-90d2-974c88be5320，时间 2026-09-26T15:44:00.586Z；工单中“2026-09-27”系本地日期误差。执行顺序：A–D → F → G → 回报 READY-FOR-EXTERNAL，等放行 → E → H。
- 2026-09-26T16:31:41Z 更正（实测，来源为 Coordinator transcript 中 Agent 调用的时间戳）：
  - 上方派工表中的“16:0xZ”“16:4xZ”为估写，实际派工时间如下：
    - P10-R1-QA-C1：2026-09-26T16:09:04Z
    - P05-R3-QA-C1：16:09:59Z
    - U3 Builder：16:23:43Z
    - U3 QA：16:24:05Z
    - P06 范围调研（只读，非角色单元）：16:25:30Z
  - 外审替代的 Owner 确认请求提交于 2026-09-26T16:26:01Z（uuid b5347c35-c067-4b09-a0fa-a2ba65f48ff5）。
  - 发给 P05 QA 的 START 消息中“16:5xZ”为误写，已另发更正消息。
- 2026-09-26T16:35:17Z P10-R1-QA-C1：已收 PREACCEPT 回执（接受；身份 11 项 PASS；diff 为 af5e4282…/88986 字节，口径已钉死）。Coordinator 下达 START，决定如下：
  - (a) 门禁加 --diagnostic-workspace，只运行一次。
  - (b) 授权门禁与 companion 的固有写入：~/.nuget/packages、/private/tmp/dps-phase0-executor-*、~/.claude/plugins/data/codex-inline、~/.codex/。
  - (c) 不启动 PostgreSQL，不设 DPS_* PG 变量；非 18.4 负控服务器与 PKCS8 测试签名者记为外部缺项。
  - (d) S8 前回报 READY-FOR-GATE，S9 前回报 READY-FOR-EXTERNAL，均需 Coordinator 放行。
  - 本机同一时间只允许一次累计门禁。
  - START 消息中写的“16:4xZ”为估写，以本行时间为准。
- 2026-09-26T16:36:46Z 辅助调研（只读，非角色单元）：P17/P18 真实进度重建。任务 ID a2bd93269756b2012，产出 scratchpad/p17-p18-status.md。依据：P17A 516c724、P17B fede545、P18A 2aaaaa8 三个分支均从 3e47ac7 分出，都不在主线 afb4f9b 中（Coordinator 只读实测）。
- 2026-09-26T16:46:37Z P10-R1-QA-C1：回报 READY-FOR-GATE。S0–S7 全部 PASS，required 共 218 项，与 Builder 相同；R1 补充运行 49/49；validate_repo PASS。Coordinator 确认本机没有其他门禁进程后，将门禁位交给 P10 QA（S8，--diagnostic-workspace）。门禁位当前占用者：P10-R1-QA-C1。
- 2026-09-26T16:53:11Z U3-INT：收到 Builder 的 PREACCEPT 回执（接受；规则哈希一致；自行回读 claude-opus-5-5×159、max×159）和 QA 的回执（接受；回读 Builder×154、QA×181 均为 opus/max）。双方接收齐备。
  - Coordinator 下达 Builder START，并答复 Q1–Q12。
  - 工单 §7 调整：Builder 的 START 不再等 P05 QA 处置；若 5ab9aab 被退回，本候选作废并重新绑定。U3 QA 的 START 与 P06 取用集成头，仍以 P05 QA 处置为前提。
  - 提交身份：-c user.name=YOUNG -c user.email=young@hofish.com，外加 Co-Authored-By 行（本机无全局 git 身份，实测）。
  - policy-approval.integration 实测 32 项，candidate-test-policy 的 floor 为 40（第 549 行），base/P1/P2 三处同样存在；不作停止条件，转交 P06 核实。
  - companion 实际数据目录为 CLAUDE_PLUGIN_DATA=~/.claude/plugins/data/codex-openai-codex，已向三个 QA 更正授权路径。
  - U3 Builder 在进入 (D) PG/IPC 段之前须回报 READY-FOR-PG-WINDOW。
- 2026-09-26T16:58:30Z U3-INT：Builder 的 START 消息已投递，transcript 在 16:58:10Z 有更新，属实测。
  - U3 QA 更正了自己回执中一条未经证实的依据（“无口令认证断言”）。Coordinator 据此撤回 trust 裁决，改为全部连接使用 scram-sha-256。
  - bootstrap 口令由 QA 本机随机生成，写入 0600 文件，不回显、不进证据。
  - 起 PG 前，QA 须检索认证相关断言及 fixture 的口令设置方式，并把结果写入报告。
- 2026-09-26T17:01:10Z P10-R1-QA-C1：回报 READY-FOR-EXTERNAL。
  - S8 门禁（16:47:18Z–16:51:49Z，--diagnostic-workspace）：exit 1，外层 ABORTED；Phase0 48/53，失败 ID 与 Air 基线一致；module-governance 候选独有 0 行。
  - 门禁位已释放：16:53Z 与 17:00Z 两次复核，均无门禁进程。
  - 代码审查：无 P0/P1 已证实缺陷。LOW 发现 3 项（CR-1 README 与并发重复状态不一致；CR-2 替换文案断言弱；CR-3 红灯证据只覆盖 10 项）。DTO 风险按非阻断残余风险接受，附前置条件。
- 2026-09-26T17:01:10Z P05-R3-QA-C1：回报 READY-FOR-EXTERNAL。
  - F 无需退回的已证实缺陷。覆盖缺口 4 项（F01–F04），未证实风险 2 项（F06、F07），按设计局限 1 项（F05）。
  - G 补跑 7 个 suite，真实退出码全为 0。
  - Air 端早先的两处静态缺口在本候选中已关闭。
- 2026-09-26T17:01Z 外审位交给 P05 QA（位于关键路径）；P10 QA 排队等待。companion 作业按单位串行。
- 2026-09-26T17:02:49Z P05-R3-QA-C1 外审 E：companion 调用一次，exit 1，job review-muin15id-wyg4lt，Codex 会话 01a0deab-3c36-7ce0-9172-e037a801930b。
  - 错误：400 invalid_request_error，“The 'gpt-6-astra' model requires a newer version of Codex. Please upgrade to the latest app or CLI and try again.”
  - 本机环境（Coordinator 实测）：codex-cli 0.146.0（~/.local/bin/codex → ~/.codex/packages/standalone/current/bin/codex）；另有 npm 全局 @openai/codex@0.144.5；config 为 model=gpt-6-astra、effort=medium。
  - 处理：记 UNAVAILABLE，外审位随即释放。
- 2026-09-26T17:05Z Coordinator 决定：
  - 在 Owner 授权升级 Codex CLI 前，各 QA 不再调用 companion，因为同一环境必然失败，且 PDD §3.4 禁止空转。
  - P10 QA 的 S9 记 BLOCKED（环境），先写报告；P05 QA 继续 H。
  - 外审阶段对 P05、P10、U3 均挂起，升级后逐一重新放行。
- 2026-09-26T17:07:29Z P06（M1）：签发工单 WO-P06-M1-SHADOW.md（sha256 8e8bf0b48feed6ad…）。
  - 派出 Builder a215eb04aa3c9f1b0 与 QA a8525e143d13b4754，两者均为 Agent model=opus，进入 PREACCEPT。
  - Coordinator 按 §3.1 最小闭包裁定：D1=A；D2=直连，由 QA 核验是否构成隐藏路由；D4=migration 005（expand）。
  - S2（Host）暂不授权，待 B1 身份来源裁定。
  - 依据：调研 P06-SCOPING-20260926.md（ad594d4f…）。
- 2026-09-26T17:05:14Z、17:06:26Z U3-INT Builder：两次创建单元根 `units/u3-int/builder`（含独立 HOME/XDG/NuGet/pip 缓存与 secrets 目录；第一次另含 cenv 包装脚本）均被 Claude Code 自动模式分类器拒绝，理由 [Unauthorized Persistence]。17:07:49Z Builder 实测 units/u3-int 不存在。
  - 对照（Coordinator 只读实测）：P10 QA 使用同类 env -i 隔离环境（test-home、nuget-*、pip-cache、tmp），创建时未被拒。
  - 17:12:06Z Coordinator 通知 Builder 停止：不得绕过，不得改由其他代理执行；可以做完只读 dry run，交回后待命。
  - 决定权交 Owner。U3 以及以其冻结头为基线的 P06 S1 暂停。
- 2026-09-26T17:12Z P10-R1-QA-C1：交回 S10 最终报告，结论 PARTIAL。
  - 本地必需检查 PASS（218 项）。累计门禁 FAIL：Phase0 48/5，均为既有项。外审 S9 BLOCKED（Codex CLI 版本）。无已证实 P0/P1。LOW 发现 CR-1..3 本轮不退回。
  - 17:12:45Z Coordinator 实测：QA-REPORT.md sha256 7af69ebf6bd9a8d0…，EVIDENCE_INDEX.json sha256 76413e364659acda…，与回报一致；clone HEAD 11d43cf、tree 870bc80c，工作树干净，报告路径被 .gitignore 忽略；门禁、companion、VBCSCompiler、postgres 进程均为 0。
  - 门禁位空闲。P10 在 Owner 授权升级 Codex CLI 后重新放行 S9，此前保持 PARTIAL。
- 2026-09-26T17:17:17Z P17-INT：工单草案 WO-P17-INT.md 已写（sha256 7c5f08ab6ec9905b…），未派发。
  - 基线 afb4f9b，合入 fede545（等于 P17A+P17B），只允许 CHANGELOG 冲突解决。
  - 检查范围：6 个模块共 19 个 required suites，其中 PG 套件 3 个（M12/M16/M26），使用单元自有的一次性 PG 实例。
  - Coordinator 按 §3.1 最小闭包裁定：C02/C03 记为 NOT_RUN，依赖 P18 步骤 1 与步骤 3；P17 整包保持 PARTIAL，只作为 P18 的输入。
  - merge.request 在 v1 内收紧签名，与 compatibility-policy 的 breaking→new-major 冲突，待 Owner 裁定。
  - 派发前提：Owner 决定单元工作区的创建方式。
  - 调研报告已存档：research/P17-P18-STATUS-20260926.md（sha256 a34c43712d862bae…）。
- 2026-09-26T17:22Z U3-INT Builder 交回停止报告（阶段 2 零写入）。
  - 分类器共拒绝 3 次：17:05:14Z、17:06:26Z 为 [Unauthorized Persistence]（创建工作区）；17:20:57Z 为 [Modify Shared Resources]（只读 dry run，读取了 Coordinator scratch 仓库）。另有 3 次无结论超时（17:10:18Z、17:12:38Z、17:18:30Z）。Builder 均未绕过。
  - 17:22:18Z Builder 实测：units/u3-int 不存在；history.git 与交接包目录无新条目。
  - Builder 提出最小替代方案：只建 clone、environment/home、environment/tmp，每条命令内联 env -i；(D) 阶段放行后再建 pg 目录和一个 0600 口令文件。还列出了后续可能再被拦的动作：clone、pip、dotnet restore、测试 /private/tmp 固有写入、pg_ctl、清理残留、在 clone 内提交。
  - dry run 与探针树 b164d1f2 的比对未完成（UNAVAILABLE）。
  - 2026-09-26T17:24:39Z Coordinator 复核：所有子智能体中只有 U3 Builder 遇到分类器拒绝。决定权在 Owner，已提请一次性授权。
- 2026-09-26T17:31Z P05-R3-QA-C1 交回 H 阶段最终处置。
  - 实现缺陷：0。
  - F01–F03 由“覆盖缺口”更正为“交付证据缺项”，依据 WO-R3-v2 第 103、105、109、119 行。
  - 外审 E：UNAVAILABLE。
  - §3.1 局部交接：有条件可以。
  - P05 整包未完，阻断项已归属到责任方。
  - QA-REPORT.md sha256 2ea8d4986c104fe6…，EVIDENCE_INDEX.json sha256 84984a27cb45c1f9…。Coordinator 于 2026-09-26T17:36:02Z 复算一致；HEAD 5ab9aab；工作树干净；报告路径被忽略。
- 2026-09-26T17:36:02Z Coordinator 裁定（P05）：
  1. F01–F03 退回补证，依据 PDD §4.3 第 1 行。Coordinator 只读核对了 WO-R3-v2 第 103、105、109、119 行（sha256 9be7c1bd…），确实要求这三项矩阵覆盖。
     - 补证由新 Claude Builder 完成，复验沿用替换 QA。工单草案 WO-P05-R4-EVIDENCE.md（sha256 f31a2e9d5e4d533c…）尚未派发，派发前提是 Owner 决定单元工作区的创建方式。
     - F04 不纳入。
  2. 外审绑定补证后的新 head：base 922db5b，执行一次。5ab9aab 的 E 记为 UNAVAILABLE，由新 head 的外审取代。
  3. §3.1 获准使用（暂定）：5ab9aab 的合同、codec，以及受 fixture 门控、已核验的同 UID 合成行为，作为 P05 局部成果供 P06 建设使用。
     - 挂起项：外审；F01–F03；Owner 对 Q1/Q3 的确认。
     - 补证只涉及测试，不改交接面。据此把“退回补证”解释为不构成 5ab9aab 作废：U3 不重绑，R4 以后随 P10 R1 在下一个集成单元并入。若 R4 或外审改动了交接面，下游按 §4.3 重绑。
  4. B1（P06 S2 的身份来源）裁定：采用 P05 QA 报告 §9.4 的合法路径，即在测试专用入口进程中，经 fixture 门控的 synthetic seam 读取 soul.current，再用 SoulResolvedCanonicalV1 解码。不采用 Host 现有 binding/device/account 运行时真相的方案（那将是有记录的偏离）。
     - device/account 只是回显，不作为 Binding 证明，只能用于 shadow；任何非 shadow 路径都 fail closed。
     - 遵守 §9.4 所列禁止项：不新增公开 Open；不放宽门控；不新增 InternalsVisibleTo；生产代码不 ProjectReference Soul runtime；不从请求体构造 SoulResolved；不用自报的 AliasVerification(Verified=true)（QA-H01）。
     - soul.* 的消费声明和通信边由 P06 自行登记。
     - S2 仍须等 S1 冻结后另行授权。
- 2026-09-26T17:47:42Z P06-S1 QA：PREACCEPT 回执已收（2026-09-26T17:4xZ 交回）。
  - 接受；规则哈希与期望一致；自身回读 claude-opus-5-5 ×157、max ×157；全程只读，未建任何目录。
  - 提出 START 前需裁定的疑点 11 项：模型档位、外审挂起时的结论口径、D2 在 S2 可能构成中继、诊断基线、v1 隔离后的审计落点、DB 层 fence 函数、promotion 的 proposal_sha256 语义、valid_until 公式、reader 合同、命令行数、回退开关。另有 START 时核实项 6 项。
  - Coordinator 只读实测：run_phase0_gate.py:2592–2596 以 validate_governance(ROOT, require_schema=True) 生成 module-governance，只依赖树内容、不依赖 --base，可用同树的 U3 QA Phase0 证据作基线。
  - 裁定待 P06 Builder 回执到齐后合并签发。
- 2026-09-26T17:48:25Z Coordinator 裁定（P10）：LOW 发现 CR-1..3 不单独退回。
  - 待 Owner 授权升级 Codex CLI、11d43cf 完成外审后，外审中已证实的发现连同 CR-1..3，合并为一轮 R2 退回 Builder。
  - 若外审无发现，则 CR-1..3 单独作为小型 R2 退回。
  - 无论哪种情况，都在 P10 整包收口前关闭，不作为遗留义务。
  - 理由：现在退回会产生新候选，使待执行的外审失去绑定对象，而 CR-1 属于文档与行为不一致，不能永久接受。
- 2026-09-26T18:00Z P06-S1 Builder：PREACCEPT 回执已收。
  - 接受；全程只读，未建任何目录；自身回读 claude-opus-5-5 ×346、max ×346。
  - START 前需满足两项：A1，effort 期望值未写明，与根规则第 99 行的 medium 不一致；A2，U3 冻结头和 Builder clone 尚不存在。
  - 另有默认做法 B1–B17，请 Coordinator 确认或否决。
  - 新发现：第五条路径 begin_approval_submission 不校验 major；正例依赖点远多于 7 处；新合同必须从 major 1 起步。
- 2026-09-26T18:21:13Z Coordinator 签发 P06 S1 开工前裁定第 1 号：WO-P06-M1-SHADOW-RULINGS-1.md（sha256 38537e033716ce36…），合并答复 Builder 与 QA 两份回执。要点如下：
  1. S1 不改 Host module.yaml，声明只写现状，覆盖工单 S1 第 7 项末句。请求合同因 Host 侧缺声明产生的诊断，逐条归属 S2。
  2. 第五条路径采用方案 C：在 approval_submission_attempts 上加触发器，拒绝 major≠2。005 的性质改为“expand 加 DB 层 fence 收紧”。
  3. v1 请求只返回 hash-only 证明，不落库。
  4. valid_until = proposal.occurred_at + 120 s，以 DB 时钟判定。
  5. 诊断基线采用 U3 QA 在同一树上产出的 Phase0 证据。
  6. 结论上限为 PARTIAL。
  7. model/effort 期望值记为 claude-opus-5-5 / max，并入 Owner 确认事项。
  - 此后 5 个子智能体全部停止，等待 Owner 答复：U3 Builder/QA、P05 QA、P10 QA、P06 Builder/QA。
- 2026-09-27T00:05:22.756Z Owner 答复（Coordinator transcript 第 1872 行，uuid d56003b9-c09f-4333-8180-34004306be70）。原文如下：
  > 1，允许 / 2，允许升级 / 3，builder和QA必须由Session来执行，而且必须像codex一样有一个只负责编排工作不执行具体工作的orchestrator 总管项目的Session。/ 4，这个你启用多智能体多角度分析告诉我前因后果后我才能作出判断，目前我无法判断。
  - Coordinator 执行口径：
    1. 单元工作区授权清单（5 项）已获 Owner 允许。
    2. 允许执行 `codex update`。
    3. 此后 Builder 与 QA 一律由独立的 Claude Code session 担任，不再使用本会话的子智能体。本会话（“DPS项目多智能体协同”，local_c93841cc-5ce4-44a2-b809-42c12a1be072）只做编排，不执行具体工作。
    4. merge.request 在 v1 内收紧一事，先做多智能体、多角度分析，再交 Owner 裁定。
- 2026-09-27T00:08Z Codex CLI 升级（Owner 已允许）：`codex update` exit 0，版本 0.146.0 → 0.157.1；~/.local/bin/codex 指向 ~/.codex/packages/standalone/current（即 0.157.1）。无残留的 codex app-server 或 broker 进程（Coordinator 实测）。
  - companion 的 `review` 入口不接受 --effort/--model 参数，只读取 ~/.codex/config.toml（gpt-6-astra/medium）。根规则要求外审 xhigh，差距待 Owner 裁定。
- 2026-09-27T00:09Z merge.request 多角度分析已派出 5 个只读分析员（本会话子智能体，非 Builder/QA 角色）：历史、安全、治理、下游，另有 1 个 Codex 异构视角（task 入口，xhigh，只读）。产出目录为 research/merge-request-v1/。
- 2026-09-27T00:14:34Z 编排方式切换为 session 制：
  - 签发 ORCHESTRATION-SESSIONS.md（sha256 2a25a4a545292ac0…）。WO-P05-R4-EVIDENCE.md 已改为 session 制（sha256 87173e7e01f3cb9a…）。
  - 此前子智能体的回执已存档到 receipts/，共 6 份。
  - 已发出 4 张 session 任务卡，等 Owner 点击启动：task_97fef137 U3 Builder、task_1b10b2e0 U3 QA、task_f025ba16 P05 R4 Builder、task_3c705ea1 P05 QA。
  - P06 S1 的 Builder/QA 卡，等 U3 接近冻结时再发；P10 QA 卡，等外审 effort 裁定后再发；P17-INT 卡，等 merge.request 裁定后再发。
- 2026-09-27T00:29:07Z merge.request 分析第 5 角度（Codex 异构）交回。
  - 实测：companion task 入口在 CLI 0.157.1 上以 gpt-6-astra、--effort xhigh 成功运行（00:22:28Z–00:27:19Z，exit 0）。这证明升级已解除此前的 400 版本错误。
  - 结论倾向 B：开 v2；过渡期内 v1 设为 quarantine-only，之后退役。
  - 产出 angle-5-codex.md，sha256 37dc33dc6e2605d7…，已复算一致。
  - 其余 4 个角度仍在运行。
- 2026-09-27T00:37:30Z merge.request 第 2 角度（安全）交回：angle-2-security.md sha256 d44aa3bfeee0726d…（已复算一致）。结论有条件倾向 A。更正了一项前提：域前缀早已存在，P17B 改的是签名 payload 的覆盖范围。
- 2026-09-27T00:40:48Z merge.request 第 4 角度（下游）交回：angle-4-downstream.md sha256 0ce9312e3320d35d…（已复算一致）。结论倾向 A。依据：A 相对 P17-INT 草案的增量为零；B 需要多 1 个单元、2–3 轮 Builder+QA，并会改写 P17-INT、牵动 P18-1 与 P19。
- 2026-09-27T00:50:51Z merge.request 第 1 角度（历史）交回：angle-1-history.md sha256 892ad9e7970043ac…（已复算一致）。结论弱倾向 A，前提是 Owner 明示落档。
- 2026-09-27T00:51:21Z merge.request 第 3 角度（治理）交回：angle-3-governance.md sha256 e67b7b4fccdb8888…（已复算一致）。结论弱倾向 B。5 个角度已全部交回。
- 2026-09-27T00:58:22Z merge.request 五路分析综合完成：research/merge-request-v1/SYNTHESIS.md（sha256 8c49d1576edc6946…）。
  - 核对结论：可篡改而不破坏旧签名的字段为 8 个（soul_id、device_binding_id、platform_account_id、trace_id、idempotency_key、occurred_at、privacy_class、branch_heads）；另有 3 个未签字段有常量检查。域前缀早已存在，不是 P17B 新增。
  - 倾向：历史弱 A、安全有条件 A、下游 A、治理弱 B1、Codex B。Orchestrator 建议 A（干净的 A，5 个条件），已提请 Owner 裁决。
  - 两个方案都要处理：MC operations/README.md L3 的回退分句与 L29–30 矛盾，拟在集成单元中删除该分句。
  - 4 张 session 任务卡仍未点击（list_sessions linked 为空）。
- 2026-09-27T01:06:09Z 4 张 session 任务卡已由 Owner 启动（Coordinator transcript 5e644c7e 第 2268 行的系统提示，2026-09-27T01:00:13.817Z）。均为本会话的子 session（parentSessionId = local_c93841cc-…），创建时为 claude-opus-5-5 / max：
  - U3 Builder：local_17b1a16b-926c-4909-a441-07dbbdff75db，创建于 00:57:43.969Z。
  - U3 QA：local_ea019004-d5ab-4ade-a84f-3651ae9b6eed，创建于 00:57:49.731Z。
  - P05 R4 Builder：local_a5acbfc0-bdcb-4f0f-8f00-f4c5bd5655f7，创建于 00:57:51.717Z。
  - P05 QA：local_774502b0-574d-4cfd-b4fc-b968ddd7093c，创建于 00:57:53.235Z。
- Owner 裁决原文（transcript b096d66e 第 102 行，2026-09-27T01:02:44.624Z，uuid ff7ab141-3968-4362-8891-21429d1fd96c；同一内容的前半段先见于 5e644c7e 第 2268 行，uuid 709cd3a4-adcd-4343-9324-d546ace6c5d8，随后被中断）：
  "还需要裁决的：1，Builder的effort为medim, QA 为xhigh, 都使用opus 5.5 2,暂时取消外审 3，暂时只用本机的opus5.5 QA , 4，可以  我的建议：A，批准"
  - 对应关系（按 Coordinator 上一条回复的编号）：第 1 项 = session 档位；第 2 项 = 外审强度；第 3 项 = 外审渠道；第 4 项 = 此前子智能体 QA 只作参考、由新 QA session 自行复核；"A，批准" = merge.request 第 4 项选 A。
  - 执行：
    - 2026-09-27T01:06:09Z 前已对 4 个 session 调用 set_session_effort：两个 Builder 设为 medium，两个 QA 设为 xhigh，自下一轮起生效。模型均为 claude-opus-5-5，无需更改。
    - 外审（ChatGPT 与 Codex companion）暂停，各工单中的外审步骤记为 NOT_RUN（Owner 暂停外审），不记 BLOCKED 或 UNAVAILABLE。QA 只用本机的 Claude Opus 5.5 session。
    - 暂停期间不做任何受保护合并。Owner 全局规则要求合入前做异构复审，本项目当前本来就不做受保护合并，因此二者不冲突。恢复外审之前，不得声称任何候选"合入就绪"。
- merge.request 裁决（Owner 选 A）：
  - 内容：P17B 对 merge.request/v1 的签名收紧（完整请求签名，加上 evidence 的 4 个 scope 字段相等检查），按 Owner 知情偏离接受，留在 v1。
  - 绑定：fede54547a343de52be104da6da49aea1b6a22e9（tree 86b830d6d053d31c8f2a3afe837cf69c9f0c14cf），以及承载它的 P17-INT 集成提交。集成提交固定后，由 Coordinator 把 SHA 补记在本账本。
  - 依据：Owner 的决定。不是 PDD 豁免（PDD L25），也不是 compatibility-policy 或工程标准的例外。规则文本一字不改。
  - 不附带"以后迁移到 v2"的义务。
  - 兼容矩阵中 merge.request 的 independentDeployable/candidateGreenEligible 声明，没有反映这次收紧。P17B 之前的 Merge Controller 和 Trusted Runner 构建，永不作为部署或回退目标。
  - 不构成先例：已发布或已有生产者的合同，破坏性变更仍开新 major。
  - 分析依据：research/merge-request-v1/SYNTHESIS.md（sha256 8c49d1576edc6946…）。
- 2026-09-27T01:11:45Z 规则与工单按 Owner 裁决更新：
  - ORCHESTRATION-SESSIONS.md 改为 sha256 fef79352a8d18685…。内容：档位改为 Builder medium、QA xhigh；新增第 3 节“外审暂停”；第 1 节记入 Owner 对第 4 项的确认。
  - WO-P17-INT.md 改为 sha256 19351a48e9d43147…，并已派发。改动如下：
    - 第 1.2 节写入 Owner 的 A 裁决。
    - 角色改为 session 制。
    - 候选改为合并提交 M 加文档提交 D。D 只把 MC operations/README.md 第 3 行的回退分句删去。
    - Trusted Runner 的两处回退文本，待 Owner 确认裁决第 4 条是否也适用于 Runner，由 START 通知处理方式。
    - 外审记为 NOT_RUN。
  - 已发出 2 张任务卡，等 Owner 点击：task_062b9a8c（P17-INT Builder）、task_55065dfa（P17-INT QA）。
- 裁决第 4 条中 Trusted Runner 部分的问题（Coordinator 起草有误，已提请 Owner 确认）：
  - P17B 之前的 Runner 源码里没有任何 merge.request 签发代码，回退到它只会让 merge 停止，不会重新接受部分签名。所以第 4 条写进 Runner 属于写宽了。
  - Coordinator 建议把第 4 条收窄为只约束 Merge Controller。
  - 若 Owner 不同意收窄，D 还须删改两处：Runner 的 operations/README.md 第 17 行，以及 Runner 的 AGENTS.md 第 35 行。后者属于规则文本。
- 2026-09-27T01:14:46Z P17-INT 两张任务卡已由 Owner 启动：Builder 为 local_89746da8-33fa-4fc9-a113-40b78f94dcb7，QA 为 local_19d3e934-13bf-413c-91d8-c9229186fcec。已调用 set_session_effort：Builder 设为 medium，QA 设为 xhigh，自下一轮起生效。
- 2026-09-27T01:14:46Z WO-P10-R2.md 已写（sha256 bce319ec5f0ca092…），暂不派发。原因：Owner 刚提示“要跟 codex 一样有一个编排者角色的总管 Session”，总管 session 的设置方式先与 Owner 确认，再发新卡。
- Owner 同一消息原文：“另外我之前要求跟codex一样，有一个编排者角色的总管Session。”
- 2026-09-27T01:18:53Z Owner 通过 AskUserQuestion 选择“另开一个总管 session”，没有选“本会话当总管”。据此：
  - 已发出总管任务卡 task_c41f4af5（“接任 DPS 总管（Orchestrator）”），等 Owner 点击。
  - 交接步骤：新总管启动后，由原编排会话 local_c93841cc-… 执行以下操作，完成后原会话退出编排：
    1. 用 set_session_effort 把新总管设为 xhigh。
    2. 把 ORCHESTRATION-SESSIONS.md 中的 Orchestrator ID 改为新总管的 session id。
    3. 逐一通知 6 个 session 改向新总管汇报。
    4. 把交接前收到的回执原文存入 receipts/。
    5. 给新总管发交接完成消息。
  - 交接完成前，原编排会话仍是 Orchestrator，但不再下达新的 START。
- 2026-09-27T01:23:53Z 交接前收到 2 份 PREACCEPT 回执，原文存档，均未下达 START：
  - receipts/U3-QA-session-PREACCEPT.md（sha256 5086f65b97ac4e7c…），来自 local_ea019004-…，提出 10 项待裁定。
  - receipts/U3-BUILDER-session-PREACCEPT.md（sha256 8a7baa2e12d3c188…），来自 local_17b1a16b-…，提出 Q1–Q9 待裁定。
- 2026-09-27T01:23:53Z 总管交接：
  - 新总管 session 为 local_cb8c68d0-c514-4803-9e7c-a51cdbb7f2be（“接任 DPS 总管（Orchestrator）”，创建于 01:20:26.066Z）。已调用 set_session_effort 设为 xhigh，get_session 回读为 claude-opus-5-5 / xhigh。
  - ORCHESTRATION-SESSIONS.md 第 1、4 节已改为新总管 ID（sha256 c87a0481c5ecfbf1…）。
  - 接下来通知 6 个 session 和新总管，发送结果记在下一条。
- Owner 原文（本会话，约 01:21Z，回应第 4 条收窄的问题）：“关于你这个没有了解前后因果关系，你启用多智能体对抗讨论并且告诉我最新的信息后，我在回复是收窄与否。”
  - 此项由原编排会话执行（Owner 在本会话直接提出）：组织多智能体对抗讨论，结果写入 research/merge-request-v1/condition-4/，并直接报告 Owner。
  - 在 Owner 答复之前，P17-INT 的文档提交 D 仍按工单第 3.1 节第 3 项执行：不改 Runner 的任何文件。
- 2026-09-27T01:25:44Z 交接通知已发出，SendMessage 结果如下：
  - 已送达并开始处理：U3 Builder、U3 QA、P05 QA。
  - 排队中（对方正在进行中的一轮结束后处理）：P05 R4 Builder、P17-INT Builder、P17-INT QA，以及新总管 local_cb8c68d0-…。
  - 通知内容：新总管 ID、ORCHESTRATION-SESSIONS.md 的新 sha256、回执改发新总管、START 前保持只读。发给新总管的另附交接完成说明。
  - 至此，原编排会话 local_c93841cc-… 不再向 Builder 或 QA 发协调指令。
  - 若仍有 session 把回执发到原会话，原会话只做原文存档，并转交新总管。
- 2026-09-27T01:25:44Z 按 Owner 要求，启动“第 4 条是否收窄”的多智能体对抗讨论：
  - 第 1 轮 3 名分析员：正方（主张收窄）、反方（主张不收窄）、中立事实核查。
  - 第 2 轮交叉反驳。
  - 产出目录：research/merge-request-v1/condition-4/。
  - 分析员均为本会话的 Claude 子智能体，只读。Owner 已暂停外审，本次不使用 Codex。
- 2026-09-27T01:29:32Z P05 R4 QA（local_774502b0-…）的 PREACCEPT 回执发到了原会话。它写回执时，尚未处理交接通知。
  - 原文已存档：receipts/P05-R4-QA-session-PREACCEPT.md，sha256 25248fda1f678c5b…。已转交新总管 local_cb8c68d0-…。原会话不答复。
  - 回执中的观察已由原会话只读核实：pid 34884（app-server-broker）与 34887（codex app-server）启动于 00:22:29Z，正是原会话 angle-5 Codex task 的启动时刻。二者属于 companion 常驻 broker，目前空闲。决定不处理。
- 2026-09-27T01:36:29Z P17-INT QA（local_19d3e934-…）的 PREACCEPT 回执发到了原会话。它写回执时，尚未处理交接通知（其 ReadNotifications 为空；通知在它进行中的一轮后排队）。
  - 原文已存档：receipts/P17-INT-QA-session-PREACCEPT.md，sha256 e18ca3ef7583890848509c53d19447f5f606f49f0b8f651d026f68f49fc95e40。原会话不答复，转交新总管 local_cb8c68d0-…，结果记在下一条。
  - 回执要点（均为该 QA 的陈述，原会话未复核）：
    - E-1：M12 test_postgres_integration.py:171–176 要求 server_version 严格等于 "18.4"，本机唯一的 18.4 构建 Postgres.app 带 extra-version " (Postgres.app)"。这是静态推断，未实测。它提出二选一：(a) 获取官方 18.4 构建，需 Owner 新增下载授权；(b) 记 INFRA_ERROR 并裁定归属和结论口径。
    - 需裁定 D1–D12（回执路由、E-1、Phase0 基准、diff --check 口径、FREEZE diff 选项、CHANGELOG 顺序、MC CHANGELOG 行、TR 文本不一致、~/.nuget/packages 写入授权、门禁时 PG 状态、PDD 798 反例范围与 TEST_ONLY 探针、套件执行形式）。
- 2026-09-27T01:38Z P17-INT QA 回执已用 SendMessage 转交新总管 local_cb8c68d0-…，结果为 queued（对方一轮进行中），message_id c09244ba-4a54-4637-934e-5f20e2fbbebc。转交时特别说明：E-1 (a) 需要 Owner 新增下载授权；D8 要等 Owner 对第 4 条的答复。
- 2026-09-27T01:38:28Z 新总管 local_cb8c68d0-… 收到原编排会话的交接完成消息（约 01:31Z 送达），开始履职。接手核对如下：
  - 只读复算：ORCHESTRATION-SESSIONS.md 为 c87a0481c5ecfbf1…；receipts/ 两份 U3 session 回执与 01:23:53Z 条一致；有效规则 AGENTS 945fd895…、PDD b720fe42…、ponytail 1316a2f3… 一致。
  - 根规则第 83 行要求总管全文读 codex-with-chatgpt-review。本机未安装该 skill，已全文读交接包副本（sha256 143b3da0949620b6…）。其默认 QA 配置已由 Owner 01:02:44Z 的明示例外取代，见 ORCHESTRATION-SESSIONS.md 第 2、3 节。
  - get_session 回读（01:29Z，平台保存值）：6 个 session 均为 claude-opus-5-5。U3 Builder medium、U3 QA xhigh、P05 R4 Builder medium、P05 QA xhigh、P17-INT Builder medium、P17-INT QA xhigh；本会话 xhigh。均与 Owner 裁定一致。
  - 与保存值不符的实测（各 transcript 的 perTurnEffort 字段，只读）：
    - U3 Builder ×180、U3 QA ×173、P05 R4 Builder ×126、P05 QA ×145、P17-INT Builder ×134、P17-INT QA ×151，全部为 max。
    - 下列轮次都开始于 set_session_effort 之后、由跨 session 消息触发，仍为 max：U3 Builder 01:24Z 起的一轮、P05 QA 交接后的一轮、本会话 01:31Z 起的一轮（本会话 01:23:53Z 已被设为 xhigh）。
    - 本机全部 transcript 中 perTurnEffort 共 24802 条，全部为 max。
    - 只记事实：保存值正确，但消息触发轮次的实际档位没有体现保存值。U3 Builder 据根规则第 101 行保持只读；P05 QA 就此提出 A9。
  - 01:34:42Z–01:34:44Z 在三个 session 空闲时重新调用 set_session_effort：U3 Builder medium、U3 QA xhigh、P05 QA xhigh。工具回执均为“from its next turn on”。U3 Builder 的 START 轮将验证是否生效：START 要求它先读本轮 perTurnEffort，不是 medium 就只回报、不写入。
  - P05 QA 的 PREACCEPT 回执：初版由原会话存档为 receipts/P05-R4-QA-session-PREACCEPT.md（sha256 25248fda1f678c5b…）；v2 在交接后直接发给新总管，取代初版，A1–A9 待裁定。P05 R4 Builder、P17-INT Builder、P17-INT QA 的回执尚未收到。
  - 原会话已只读核实 pid 34884（app-server-broker）与 34887（codex app-server）由其 00:22:29Z 的 angle-5 任务启动。任何 session 不处理这两个进程。
- 2026-09-27T01:40:43Z U3 Builder START 已发出：SendMessage 结果 delivered，message_id 4a47e125-b50e-4f3d-88d1-a3d011647ae0；对方新一轮从 01:39:21Z 开始。
  - 裁定要点：
    - Q1：CHANGELOG 中 CPH attestation 一节只保留一份，两父全文须为结果的子序列，集成条目采用 Builder 提出的英文原文。
    - Q2：(a) 如实测确有，dotnet 工具链在 /private/tmp 建命名管道 socket 属授权第 3 项的固有结果；(b) 不设 UseSharedCompilation=false，不执行 build-server shutdown，不结束共享进程，只报告。此条适用于所有单元。
    - Q3：残留清理用三条件规则。
    - Q4：允许用 initdb 引导超级用户给夹具建库建角色。依据 PDD L174、L288，不属于“借超级用户代替”：最小权限角色仍由夹具创建、由产品代码断言。报告须写明哪些套件用了超级用户 DSN 及原因。
    - Q5：consumer 只做构建。Q6、Q7 采用默认。
    - Q8：不预先授权重跑，由总管逐项决定。
    - FREEZE.json 须写明两父 diff 字节 SHA-256 的完整命令。
  - 开工条件：先核对本轮 perTurnEffort，不是 medium 就只读并回报。
  - 实测（只读 transcript）：该轮 01:39:24Z–01:39:57Z 的 9 条记录，perTurnEffort 全部为 max。01:34:42Z 在其空闲时重设 medium，也没有生效。
  - 由此，所有单元的 START 都受阻于同一事实：保存的 medium/xhigh 对消息触发的轮次不生效。已提请 Owner 裁定。
- 2026-09-27T01:42Z Owner 原文（原会话，约 01:39Z，本轮中途发来）：“之后所有的项目进行的编排职责移交给总管。并告知其他session。”
  - 原会话收到的两份材料已原文存档，并转交总管：
    - receipts/P17-INT-BUILDER-session-PREACCEPT.md，sha256 b0b3c63a7bd762f2db6e127cae6b30b1768f07c897c94e724be9ccccf280189a。来自 local_89746da8-…。它读的是交接前的规则 fef79352…，所以发到了原会话。其中 Q1 即 E-1，Builder 建议 (a) 从 postgresql.org 下载官方 18.4 源码自行编译，这需要 Owner 新增授权。
    - receipts/P17-INT-QA-session-PREACCEPT-correction-1.md，sha256 16a2d6a36d6890b920cab6f9b94b4f1d0fcb5490387b7d3c54c65022523dc539。来自 local_19d3e934-…，更正预登记 blob 列表中的一处笔误。
  - 通知发送结果（SendMessage）：
    - 总管 local_cb8c68d0-…：queued，message_id 462a0a3c-0a40-4d79-9fca-92d47d5b24f6。内容为 Owner 原文、职责全部移交、上述两份材料，以及“原会话只剩第 4 条对抗分析”。
    - 6 个 Builder/QA session（U3 Builder、U3 QA、P05 R4 Builder、P05 QA、P17-INT Builder、P17-INT QA）均未送达。app 返回原文：“Not delivered: this session has already messaged Claude Desktop sessions 10 times since your user last typed here, which looks like sessions messaging each other automatically. Paused until your user's next message in this session.”（后 5 条的计数依次为 11–15。）
    - 原会话不绕过这一暂停，也不请其他 session 代发。
  - 更正：发给总管的消息写了“同样的通知已发给 6 个 Builder/QA session”，与事实不符。暂停解除后，原会话先向总管发更正，再补发这 6 条通知。
- 2026-09-27T01:51:39Z Owner 裁定（总管 transcript，AskUserQuestion 答复于 2026-09-27T01:50:56.150Z，uuid 95fc7d22-9d53-4872-9cc3-ab7bb581524a）。
  - 问题：保存的 medium/xhigh 对消息触发的轮次不生效，实测每一轮都按 max 运行；根规则不允许 Builder 用 medium 以外的档位，因此各单元 START 受阻。怎么处理？
  - Owner 选择：“接受按 max 运行（推荐）”。选项说明原文：“保存值仍设为 medium/xhigh，实际按 max 运行。这作为你知情同意的偏离，写入编排规则第 2 节。各报告如实写实际档位，不声称按 medium/xhigh 运行。质量不会下降；代价是 Builder 的算力和耗时高于 medium。”
  - 执行：ORCHESTRATION-SESSIONS.md 第 2 节末尾新增一条“实际档位”，其余各节不变，新 sha256 为 82fe2779d22c6aae…。
  - 这是 Owner 知情偏离，不是 PDD 或根规则的例外，根规则文本不变。各 session 下一次收到总管消息时，按新 sha 重绑。
- 2026-09-27T01:51:39Z P10 R2 任务卡已发出，等 Owner 点击：task_974a3866（Builder）、task_2164d1f9（QA）。回执地址为新总管。
- 2026-09-27T01:59:02Z 总管处理记录（01:51Z 之后）：
  - 01:52Z 前后向 U3 Builder 发出开工确认（message_id e6f2973d-24f1-40ed-9437-b01c87e2a454，delivered）。内容：Owner 已接受按 max 运行；编排文件新 sha 82fe2779…；撤销“仍为 max 就只读”一条，改为每轮如实记录实际档位。
  - Owner 原文（总管会话，本轮中途发来，2026-09-27T01:53:01.872Z，attachment uuid d3fc49e2-7860-4382-a977-9a0f1dfb9fd3）：“你是这个项目的总管，负责编排、指挥和协调所有赛行的工作。按照计划任务和 PDD 的方案推进项目，只履行总管职责，不负责写代码。你需要把控整个项目的进度和整体推进方向，确保项目不跑偏。同时安排各个任务的进度，例如 Builder 和 QA 何时开始、做什么内容，并在汇集他们的返回信息后进行编排。以及更多编排者应该做的事情”
  - 总管只读核实 E-1：
    - Postgres.app 的 postgres --version 输出为“postgres (PostgreSQL) 18.4 (Postgres.app)”，configure 含 --with-extra-version= (Postgres.app)。
    - fede545 上 factory-control-plane-host/tests/test_postgres_integration.py 第 171–176 行要求 SHOW server_version 严格等于 "18.4"，否则抛 INFRA_ERROR。
    - 官方源码 https://ftp.postgresql.org/pub/source/v18.4/postgresql-18.4.tar.bz2 经 HEAD 请求实测为 22,567,173 字节，同目录另有 .sha256 文件。
  - Owner 裁定（AskUserQuestion 答复于 2026-09-27T01:55:42.847Z，uuid e813ca77-af4f-459a-a6f2-9183fb33fcfe）：选“授权下载官方源码（推荐）”。选项原文：“只下载 postgresql-18.4.tar.bz2（22,567,173 字节，来源 https://ftp.postgresql.org/pub/source/v18.4/）及同目录的 .sha256 校验文件。Builder 和 QA 各自在自己的单元目录内编译（含 pgcrypto），不装到系统，不改测试。编译只用本机已有的工具，如需再下载别的东西就停下来回报。以后要跑 M12 的单元（如 P18A-INT）也适用。”
  - 总管只读核实：governance/modules/module-catalog.yaml 只登记各模块 AGENTS.md 的路径，generate_governance.py 不计算内容哈希。因此如果第 4 条不收窄、需要改 TR AGENTS.md 第 35 行，不会连带改动 governance 快照。
  - P05 R4 Builder START 已发出（message_id 9c7e23f2-7bfc-4a79-8d87-00282a932e7b，delivered）。裁定要点：
    - F02 放在 owner-lease 层，使用真实 PG，让结果在线路上丢失。
    - 接受用 SIGSTOP/SIGCONT 制造在途状态，只作用于本单元对象，并在 finally 中恢复；release 窗口的在途须在报告中写明是靠构造建立的。
    - PG 同时开 127.0.0.1 和单元内私有 socket。
    - 允许新增测试自有的 loopback 桥。
    - 既有的 P11 用例 FAIL 如实记录，不加过滤、不降 floor。
    - fixture seam 边界采用 P05 QA A1 的读法，Builder 不改三个夹具文件。
    - 共享编译服务不关，残留清理三条件，不预授重跑。
  - P17-INT Builder START 已发出（message_id d703d190-7b8b-4266-918d-b6dbe7260b69，delivered）。裁定要点：
    - Q1：按 Owner 授权下载并编译 PG 18.4，M16/M26 也改用该实例。
    - Q2–Q5、Q7、Q8 采用默认。
    - Q6：M26 迁移身份可以是 TEST_ONLY 超级用户，因为 002 的 ALTER ROLE 本身需要超级用户权限；admin/runtime 仍为非特权角色。
    - Q9：Runner 文件在收到通知前不改；PG 段完成后仍无通知就回报等待，不 FREEZE。
  - U3 QA、P05 QA、P17-INT QA 按各自工单等待候选固定，START 与各项裁定在对应 FREEZE 后下达。P05 QA 的 A1 已作为 seam 边界写进 P05 R4 Builder 的 START。
  - 平台限制（原会话实测，见 01:42Z 条）：Owner 没有在某个 session 中输入时，该 session 最多只能向其他 session 发 10 条消息。已写进各 START，要求只在停点回报并合并内容。
- 2026-09-27T02:00:35Z P10 R2 两张任务卡已由 Owner 启动（list_sessions 实测，均在进行 PREACCEPT）：
  - Builder：local_63a8c471-1b36-48fb-85e0-7524e1d1e649
  - QA：local_80f25d82-2c91-442a-b4e0-04a244ee316b
- 2026-09-27T02:00:35Z P05 R4 Builder 于 01:51:32Z 把 PREACCEPT 回执改发给新总管。这发生在总管 01:58:19Z 的 START 送达之前，内容与原会话存档的版本相同，只少了收件方一项。START 已覆盖其全部 8 项，不再单独答复。transcript 显示它 02:00Z 起已在执行 START。
- 2026-09-27T02:26:02Z Owner 在总管会话输入“你继续安排编排工作”（约 02:2xZ）。总管据此做全局核查并安排：
  - 只读实测（02:24:13Z）：
    - U3 Builder clone 已有合并提交 f91d1dc（分支 codex/int-p04r1-p05r3）。
    - P05 R4 Builder clone 已有工作提交 1f991fa（分支 codex/p05-r4-evidence）。
    - units/p17-int 尚不存在。
    - 563xx 端口无监听，没有 postgres 或门禁进程。
  - P17-INT Builder 自 02:02:46Z 起停在 AskUserQuestion：按其 session 的安全规则，下载文件须由 Owner 在该 session 内直接确认，总管转达的授权不能代替。问题内容与 Owner 01:55:42Z 的授权范围一致。已提请 Owner 到该 session 答复。P17-INT QA 开工时预计也需要 Owner 同样确认一次。
  - P06 S1 任务卡已提前发出，等 Owner 点击：task_1a5ae4c7（Builder）、task_7868a8f1（QA）。卡中写明以下条款被新规则取代：工单第 4 节、裁定第 0 节第 1 项、第 0 节第 3 项、第 2 节第 2 项。
  - 排期裁定（工单冲突，选一）：
    - WO-P06 第 9 节规定，U3 Builder 冻结后即下达 P06 Builder START；WO-U3 第 7 节规定，U3 出口满足后再派 P06。
    - 选前者：P06 S1 只改 policy-approval，U3 是纯合并，等 U3 QA 完成会让 P06 这一对空等 1–2 小时。
    - 代价与约束：若 U3 QA 结论为 NOT PASS，P06 S1 按 PDD §4.3 重绑到修复后的集成头；P06 QA 的 START 仍在 S1 冻结之后。
  - 暂不写 P18A-INT 与“P05 R4 + P10 R2 集成”两张工单：它们的起点（P17-INT 冻结头，以及 R4、R2 的冻结头）尚不存在。在对应单元接近冻结时再写。
- 2026-09-27T02:37:04Z PG 窗口排期、P10 R2 开工（总管）：
  - U3 Builder 的 READY-FOR-PG-WINDOW（02:19:40Z）没有送达总管（message_id f9b92ed5-3d72-4f09-8fcc-53bf761e4cb5）。总管按其证据目录中的 READY-FOR-PG-WINDOW.md 原文处理（sha256 f4f7d96033bd7e11…）。
  - 02:31:47Z 放行 U3 的 PG 窗口，端口 56320–56329（message_id 43a3b5bf-f5cc-4d15-b408-75c91b1ebabc，delivered）。放行前实测 56300–56399 无监听；02:37:04Z 实测 postgres 已监听 127.0.0.1:56320。
    - E 段按其第 6 节执行：5 个套件都用超级用户引导 DSN，报告须写明原因。
    - 预期 229 个测试，其中 Soul 为 98 PASS + 1 FAIL（P11 用例）。
    - 其余偏离即停，不重跑。完成后直接 FREEZE，回报时写明 PG 实际停止时刻。
  - P05 R4 Builder 同样请求 PG 窗口（约 02:26Z，端口 56380）。总管让它排在 U3 之后（message_id fa8d47de-ea13-413b-9654-8d513e4d601f，delivered）。
    - 理由：U3 请求在先，且在关键路径上（P06 以 U3 的冻结头为基线）；两边都是时序敏感的 Soul/Host 真实 PG 测试，不并行。
    - 等待期间只允许 validate_repo.py、generate_governance.py（校验模式）以及 scratch 的 git 和文本准备；不做 dotnet、initdb 或启动 PG。
    - 裁定一：sign 窗口可以卡在 provider 与当前签名服务的第一次交换，即读取带签名的 status。
      - 依据是总管只读核对 1f991fa：SoulCurrentAuthority.cs 中 L46 owner lease、L50 StatusAsync、L53 SignCurrentAsync、L58 RevalidateAsync，都位于 SoulCurrentProviderService.cs L57 的同一个 ConnectedAsync 之内。
      - REPORT.md 须写明卡点位置和上述依据，并写明 lock 与 sign 两个窗口检验的是同一个调用。
    - 裁定二：同意用 Npgsql Passfile 指向 0600 口令文件；收尾时口令 grep 计数须为 0。
  - P10 R2 两份 PREACCEPT 回执都没有送达总管：Builder 为 f700c838-c158-4276-aaa8-99629ff95c3c（02:21:07Z），QA 为 00e71147-6e60-4308-91bd-9347ba1f88fd（02:05:41Z）。总管从各自 transcript 的 SendMessage 原文只读取得并存档：
    - receipts/P10-R2-BUILDER-session-PREACCEPT.md（sha256 0d27a87643b684276f872502f64ba6f2d702d52b342c29211994f02c8946d1af）
    - receipts/P10-R2-QA-session-PREACCEPT.md（sha256 bb869fc1ae2609f33e7aee0fc92db2505005450f6a3d34bac8ead1c8afe7b3a8）
  - P10 R2 Builder：已调用 set_session_effort 设为 medium，get_session 回读为 claude-opus-5-5 / medium。START 已发出（message_id b5508740-0f4d-43ee-8cb3-8a556de2cb8a，delivered）。裁定要点：
    - CR-1 取值按回执第三节确认：被持有的 CompletionPrepared（ack=false）收到同 epoch 重复请求时，四种组合一律返回 IN_PROGRESS（duplicate=true，retry_allowed=true）。
    - 代码只改 CommandProcessor.cs 第 750–751 行。这条路径不再检查时钟，接受这一连带变化，条件是保留时钟回拨 2 例，并在 REPORT.md 中主动披露。
    - Q1：CR-3 的编译垫片采用 Builder 的做法：只在 scratch 的测试项目和 process-harness 项目中各加一个空实现扩展方法；src 与 922 逐字节一致。
      - QA 回执 F1 提议在 src 接口和两个 store 中加空实现，这会改动 src，与工单 3.2-5 的“src 恢复为 922 内容”冲突，不采用。
      - 总管只读核对了镜像：测试侧 4 处包装都是隐式 public 方法，922 全仓对该成员的引用为 0 处，扩展方法方案可以编译。
    - Q2：CR-1 只覆盖问题栏的四种组合。Builder 另发现的 Reserved/Accepted 窄竞态（922 起既有，未实测）列为已知限制，本单元不修。
    - Q3：提交身份沿用 YOUNG 加 Co-Authored-By。
    - 可删减项全部保留。3.2-3 逐字执行 test.sh，QA 用同一口径。FREEZE.json 须写明 diff 的完整 argv、配置隔离方式，以及复算 922..11d43cf 得到 af5e4282… 的自检结果。
    - P10 R2 QA 的其余待裁项（F2–F7）在 Builder FREEZE 后随 QA START 下达。
  - 其他只读观测：
    - P17-INT Builder 已在单元根内编译安装 PG 18.4，含 pgcrypto，尚未启动服务器。它在 02:02:46Z 起提出的下载确认已有答复，units/p17-int/builder 创建于约 02:26Z。
    - 原编排会话 local_c93841cc 正按 Owner 要求，就裁决第 4 条是否收窄组织正反方讨论（research/merge-request-v1/condition-4/），没有向各单元发协调指令。
  - 总管自 Owner 最近一次在本会话输入以来，已发 3 条跨会话消息。
- 2026-09-27T02:46:40Z U3 Builder 冻结；U3 QA START；P05 R4 PG 窗口放行（总管）：
  - U3 的 E 段：02:36:41Z 启动 PG，02:40:34Z 停止（`pg_ctl -m fast stop` exit 0）。总管的监视在 02:40:47Z 观察到 56320 不再监听。
    - 5 个套件都与预期一致：AM 8/8、CO 24/24、PA 32/32、CPH 66/66、Soul 98/99。唯一 FAIL 为保留的 P11 用例，exit 2，与 P05 相同。
    - 没有残留角色、库或 schema；口令扫描 104 个文件，命中 0 次。
  - 02:41Z 放行 P05 R4 的 PG 窗口，端口 56380–56389（message_id 1d62f2e7-8bec-47a0-aed5-4f3abca7f2e2，delivered）。02:43:03Z 观察到 127.0.0.1:56380 开始监听。
  - U3 Builder FREEZE 回报发于 02:44:30Z，状态为 queued，交付给总管。冻结物位于 units/u3-int/builder/DSP_ZD/Reports/ci/u3-int-builder/：
    - FREEZE.json：sha256 219847a8045a3780f49b9185c85418dc26cbcb95522a5b2de1ee73b0afba9212（91378 B，02:41:28Z）。
    - REPORT.md：sha256 b3cda828837087a4bda5eb84e8d81aff1636d21281d743f38e474936e8e61ca7（19721 B）。
      - 02:43:33Z 首次写成时为 81070ec6…，02:43:55Z 修改一次：第 7 节中一组 socket 的数量由 7 改为 6，使 3+6+16+5=30，与 FREEZE.json 一致。
    - 候选：commit f91d1dc5eb9b22fa121c4b1481cd7127a724f9a4，tree 0aeb563169dada91b11b0f94e3fa3baf71f27b57，父依次为 5ab9aab…、4d19d3f…，分支 codex/int-p04r1-p05r3，工作树干净。
    - 自测：25 个套件共 936 个测试，935 PASS，1 FAIL（P11），0 偏离；构建 22 项全部符合预期。/private/tmp 新增 30 个 socket，均未删除；收尾时没有残留进程。
  - 总管只读核验：用 FREEZE 所记命令复算对两父的 diff 字节。P1 为 265312 B / d4dbf75b…，P2 为 1667843 B / 56e48dbf…，与 FREEZE 一致；tree 与两父顺序正确；clone 的 HEAD 为 f91d1dc，porcelain 为空。
  - U3 QA：get_session 回读为 claude-opus-5-5 / xhigh。START 已发出（message_id a8b6c9bf-8b74-4257-99e2-4aa89df4cb37，delivered），裁定其第九节的 1–10 项：
    1. 执行 S5。
    2. 门禁带 --diagnostic-workspace，只跑一次。
    3. 门禁不设 DPS_* PG 变量。
    4. 单元根外的固有写入按 P10 R1 (b) 先例准许，门禁前后各快照一次。
    5. 前任的裁决不延续。
    6. 父线基线用去敏副本，并注明限制。
    7. 外审单列为“Owner 暂停项”，结论最高 PARTIAL，有新失败则 NOT PASS；阻断只来自父线既有未完项和 Owner 暂停项时，视为满足 U3 出口。
    8. diff --check 分别对 P1..C 和 P2..C 执行。
    9. 先从镜像 clone，再从 Builder clone 只读 fetch。
    10. 按 FREEZE 所记的 diff 口径复算。
    - PG 窗口排在 P05 R4 之后，门禁须先回报 READY-FOR-GATE。门禁 run 目录须完整保留，供 P06 作诊断基线。
  - 更正：START 中写的 REPORT.md 哈希（81070ec6…）已过时，02:45Z 另发更正，改为 b3cda828…（message_id fc35c3a2-1554-401e-bc4c-2f2df19a0a0b，queued）。
  - P06 S1 Builder 的 START 按 WO-P06 第 9 节在 U3 冻结后下达。它的 PREACCEPT 回执尚未收到（两个 P06 session 仍在进行 PREACCEPT），收到后下达。
  - 总管自 Owner 最近一次在本会话输入以来，已发 6 条跨会话消息。
- 2026-09-27T02:45:56Z 更正（总管）：上一条的标题时间“2026-09-27T02:46:40Z”写错了。该条的实际写入时刻为 2026-09-27T02:45:35Z（写入时 `date -u` 实测）。原条目不改。
- 2026-09-27T02:54:39Z 原编排会话 local_c93841cc-… 的最后一条记录。此后账本只由总管写。
  - Owner 原文（原会话，约 02:35Z）：“我发现很多session还是在继续给你发送信息，应该发给编排者，你的交接工作没有做好，你启用多智能体来处理好。”
  - 02:37:54Z–02:38:15Z 向 6 个 Builder/QA session 补发“最后通知”（今后只发总管；任务卡中 to=c93841cc 的一句作废；原会话不再转交）。SendMessage 结果：U3 QA、P05 R4 Builder、P05 QA、P17-INT QA 为 delivered；U3 Builder（message_id 922407e5…）、P17-INT Builder（ddfb7b18…）为 queued。
  - 更正 L282：01:40Z 发给总管的“同样的通知已发给 6 个 Builder/QA session”在当时不成立，至 02:38Z 才成立。
  - 撤回 L235：原会话不再存档、转交任何回执。误发到原会话的消息，原会话只回“请改发总管”。
  - 只读排查（原会话子智能体，约 02:47Z）结论：
    - 误发根因：原会话签发的 6 张任务卡都写了 `to: local_c93841cc`；session 按各自上下文中最近一条明确指令发送，处理到改向通知前就照任务卡发。交接（01:23:53Z）之后发往原会话的只有 4 条：P17-INT QA 01:33:13Z、01:33:22Z，P17-INT Builder 01:33:40Z，P05 R4 Builder 01:49:35Z。01:49:35Z 之后没有。
    - 协调文件里仍把原会话写成协调者的只剩 WO-P17-INT.md 第 52 行（其余 Coordinator 字样不带 id）。该文件仍是 19351a48… 版。建议总管改该行并附新 sha 通知 P17-INT 双方。
    - 原会话发过的 7 张任务卡均已启动，无待撤回的卡；总管发的 4 张卡收件人均为总管。
  - 第 4 条对抗讨论：第 1 轮与第 2 轮共 5 份报告及综合结论在 research/merge-request-v1/condition-4/（SYNTHESIS-condition-4.md sha256 cdb0e47166407083…，建议乙）。Owner 随后要求以 Fable 5.1 从系统工程、软件工程、编排者三个视角再做一轮对抗讨论，产出在 condition-4/debate-2/，结果由原会话直接报 Owner，Owner 答复原文由原会话转给总管一次。
- 2026-09-27T02:57:18Z Owner 指令后的复核与派发（总管）：
  - Owner 于约 02:51Z 在总管会话中输入，原文：“继续编排。你上一轮被中断，后台监视也停了。先 date -u，重新核对各单元的实际状态（U3 的 FREEZE.json 和 REPORT.md、P05 R4 的 PG 段、P17-INT、P10 R2、P06 S1），重新开监视，然后按计划下达 U3 QA START 和 P06 S1 Builder START。第 4 条（Runner 是否收窄）我正在另一个 Fable 5.1 会话里讨论，裁决稍后在本会话给你。那边发来的内容只作参考，以我在本会话的输入为准。在此之前，P17-INT 按 Q9 执行：不改 Runner 文件；PG 段完成后回报并等待，不 FREEZE。”
  - 02:52:05Z 起实测复核：
    - U3 冻结物仍为 FREEZE.json 219847a8… 与 REPORT.md b3cda828…；Builder clone 的 HEAD 为 f91d1dc，porcelain 为空。
    - 原监视任务 blwict065 当时仍在运行（02:52:05Z 仍有输出）。按 Owner 要求停止后重开为 bmv78oq6e。新脚本启动时会补报 WATCH_SINCE 之后所有发给总管的 SendMessage。
  - U3 QA START 没有重发：它已于 02:44:26Z 送达（message_id a8b6c9bf…），QA 02:49Z 起执行，transcript 中也有 02:45Z 的 REPORT 哈希更正（fc35c3a2…）。
  - 漏收一条：P17-INT Builder 的 READY-FOR-PG-WINDOW 发于 02:39:17Z，状态 queued，总管一直没有收到。旧监视 02:39:32Z 才开始扫描，也没有捕获。
    - 总管 02:53Z 从其 transcript 读到后放行：端口 56360–56369（message_id 52e27789-1c2a-4394-bf78-94a8794a64ed，delivered），并按 Owner 指令重申 Q9。
    - 实测其 PG 从 02:54:40Z 监听到 02:56:25Z。
    - 其候选：M=0718a6fd665aba8e5708ac4cd9d8f84347dbc846（父为 afb4f9b、fede545），D=9c1755d983928fcd02a2121a35b0711ac33c55ff，分支 codex/p17-int-afb4f9b。16 个非 PG 套件共 276 个测试全部通过。PG 18.4 由官方源码编译，源码包 SHA-256 为 81a81ec6…，与官方一致。
  - P05 R4 于 02:52:49Z 回报偏离（queued；由 transcript 取得）：soul-registry.integration 除 P11 外另有 3 例失败，均为 R1 commit-ACK 丢失代理用例，报错“No password has been provided”。
    - 总管只读核验：CommitAckDroppingProxy.ConnectionString（PostgresSoulIdentityAuthorityIntegrationTests.cs:599–606）保留 Passfile，只把 Host/Port 改为 127.0.0.1 和随机端口；而 pgpass 条目写死了 56380。根因成立，属环境配置错误。
    - 裁定采用方案 A（message_id f8f7bd77-da8c-4376-9655-46f4d21a8100，delivered）：pgpass 端口改为 `*`，同一命令重跑一次；仍有 P11 以外的失败即停；REPORT 单列这项偏离，失败那次的原始日志保留。
    - 实测其 PG 于 02:54:55Z 在 56380 上重开。
  - P06 S1：
    - QA 回执发于 02:54:33Z，undelivered，存档为 receipts/P06-S1-QA-session-PREACCEPT.md（sha256 a57ba0cc725e3f9c39a6d9fa875bb2a98e484ebc8bb4b5cc668abad264c931fa）。其 4 项待 S1 冻结后随 QA START 裁定。
    - Builder 回执发于 02:55:05Z，undelivered，存档为 receipts/P06-S1-BUILDER-session-PREACCEPT.md（sha256 bfe42d4abdde77f5b75a51bef591deaca456efb3eaa87b876305255a8d9131d4）。
    - 已调用 set_session_effort：Builder 设为 medium，QA 设为 xhigh。02:56Z 回读 Builder 为 claude-opus-5-5 / medium。
    - Builder START 已发出（message_id 1c7ffd05-67e6-4418-8df4-5cda9a744537，delivered），基线 f91d1dc / 0aeb5631。裁定要点：
      - Q1：先从镜像 clone，再从 U3 Builder clone 只读 fetch。
      - Q2：PyPI 下载由 Owner 在该会话中直接确认，总管已提请 Owner。
      - Q3：policy、CO、audit-metrics 的 PG 套件不属于 Soul/Host 负载，可在自有 PG 上随时跑；Host.integration 须先回报 READY-FOR-PG-WINDOW；不采用“其他单元开窗期间停做 dotnet”。
      - 补充约束（来自 QA 回执中的事实）：不新增 csproj；clone 内不存放 AGENTS.md 或 module.yaml 的副本；diff 口径与 U3 相同，显式加 --no-renames。
  - 本次 Owner 输入以来，总管已发 3 条跨会话消息。
- 2026-09-27T02:57:47Z P06 S1 QA 的 PREACCEPT 回执（02:54:33Z 发出，当时状态为 undelivered）于约 2026-09-27T02:57:47Z 以 cross-session message 形式送达总管会话。内容与总管 02:55Z 从其 transcript 取得、存档的版本一致（receipts/P06-S1-QA-session-PREACCEPT.md）。实测说明：标为 undelivered 的消息事后仍可能送达。总管不回复，QA START 在 S1 冻结后下达。
- 2026-09-27T02:57:59Z P06 S1 Builder 的 PREACCEPT 回执（02:55:05Z 发出，当时 undelivered）也于约 2026-09-27T02:57:59Z 迟到送达，内容与存档版本一致。其 Q1–Q4 已在 02:56Z 的 START 中裁定，总管不另回复。
- 2026-09-27T03:03:35Z P17-INT Builder PG 段结果与裁决（总管）：
  - 回报发于 02:59:38Z，undelivered，由 transcript 取得，存档为 receipts/P17-INT-BUILDER-session-PG-SEGMENT.md（sha256 87d6e93d94bbe63cdf5c85c4485e09be0c50892c4db7045b16b451055fce4e01）。它引用的 Reports/ci/p17-int-builder/PG-SEGMENT-DONE.md，总管实测 sha256 为 f612fb6c8eed42442afb20f9078bc0977b40b559ebdb8d7684d379b5f1a9446b，与回报一致。
  - 回报内容：M12 rc 0，Ran 12；M16 rc 1，Ran 8，failures=3、errors=5；M26 按偏离即停未运行。PG 02:56:24Z 停止。
  - 总管只读复核 M16：
    - 用 Builder clone 的 git 对象做 diff-tree（GIT_OPTIONAL_LOCKS=0），Modules/factory-artifact-builder 子树在 3e47ac7、afb4f9b、fede545、D(9c1755d) 上都是 e30adefa4a38dd0f2aabb73d6e05b8935da2fd07。
    - s11-m16 日志 sha256 e651c838…：5 个 error 都是 artifact_builder.py:501 “build identity registry constraints do not exactly match migration v1”。
    - 交接包 evidence/3df1/Reports/ci/p02-artifact-builder/dps-p02-artifact-8x6mvkqg/README.md（sha256 3601dfa8…）记录同一套件为 FAIL，8 个测试、3 failures + 5 errors，诊断相同：PG 18.4 多出 10 行 NOT NULL 约束，CHECK 的渲染也不同。
  - 裁决（message_id 572b2f36-8a74-4adf-8b8c-5315cfd55292，delivered）：
    - M16：记为既有缺陷 FAIL，不是本单元引入的，也不是新增失败；不重跑，不在 P17-INT 修。
    - M16 归属：按 PDD 模块表，factory-artifact-builder 属于 P19（PDD-DIGEST L871、L480）。是否提前另开修复单元，报 Owner 决定。
    - M26：批准首跑一次，只跑 M26。在同一实例上重启并回读，命令为 manifest 原文，env -i 加该套件自己的 DSN；允许改 <E>/tmp 下的 pg_suites.py，须记录 diff 和 sha；偏离即停，跑完后停库并扫描泄漏。
    - M26 属 factory 套件，不占 Soul/Host 窗口（ORCH §7）。
    - Runner：维持 Owner 指令，不改 Runner 文件，不 FREEZE。
  - 本次 Owner 输入以来，总管已发 4 条跨会话消息。
- 2026-09-27T03:08:50Z P17-INT M26 结果、P05 R4 冻结核验、U3 QA 放行（总管）：
  - P17-INT M26：
    - 回报发于 03:06:26Z，undelivered，由 transcript 取得，存档为 receipts/P17-INT-BUILDER-session-M26.md（sha256 f616d86e0052900de60d9cac2bc1c6151fb06cfefae6ae04ca16a422aa62ff44）。M26-DONE.md 实测 sha256 3d147709bd7eaa9f3d10b372c71115b5404c4d1fc7da79d3221d098aa636993f，与回报一致。
    - 结果：M26 首跑一次，rc 1，Ran 5，failures=1、errors=3。日志 s11-m26 的 sha256 为 89ae3d01…。PG 于 03:04:10Z 停止。
    - 总管只读复核：
      - Modules/factory-evidence-ledger 子树在 3e47ac7、afb4f9b、fede545、D 上都是 0fd9870c6996b49af23fb95a1ca90d5a0b25a181。
      - 候选内 migrations/002_authenticated_append_acl.sql L486 要求 p_event_json 恰好 21 个键；同一条件 L487 的 ?& 数组只列 20 个键名；evidence_ledger.py:74 的 _EVENT_FIELDS 也是 20 个，L342 要求事件键集恰好等于它。结论：任何合法事件都会被拒绝，3 个 error 同源。
      - 1 个 failure 是测试期望写成 t/f，而 boolean 拼接成 text 后实际是 true/false。
      - 镜像内 002 只有 5b7d16c 一次提交，P18A 2aaaaa8 对该模块只改 module.yaml、operations/README.md、tests/test_postgres_integration.py，没有改动 002。
    - 定性：既有缺陷，不是本单元引入的。按 PDD 模块表（PDD-DIGEST L873、L438），factory-evidence-ledger 属于 P03/P17/P18/P19，本身就在 P17 的模块范围内，但不在 P17-INT 写域内。
    - M26 的定性裁决暂不单发。它不影响 P17-INT 当前的等待状态，与 Owner 第 4 条裁决后的 Runner 通知合并下达，以节省消息。
  - P05 R4 冻结只读核验：
    - FREEZE.json sha256 07a71217340bcc24d6490ffd8d1ee24e98ffeef4314ce6cf3e4e71ac08f9d0c5。commit 1f991fa465912fe661676d1832e75272d1683f7f，tree 519cf1af3d8e87ae450e8433b5e4e39ab02ad99a，唯一父 5ab9aab（tree 2bf31538…）。
    - 按统一 argv 复算 diff：25399 字节，sha256 d4af32b2cd66922e5fb0c2267c192fec04fbb242de6c64341700f6024fc71edb，与 FREEZE 一致。
    - 共 5 个路径，都在 §4.1 范围内；两个模块的 src 相对 5ab9aab 无变化。Builder clone 的 HEAD 在该提交上，tracked 工作树干净。
    - REPORT.md 于 03:05:57Z 出现，当时 sha256 为 b0189a09…。等 Builder 的定稿通知到后复算，再下达 QA START。
    - P05 QA（local_774502b0-…）的 get_session 回读为 claude-opus-5-5 / xhigh。
  - U3 QA：
    - 03:07:46Z 回报 READY-FOR-PG-WINDOW 和 READY-FOR-GATE（undelivered；原件为其 Reports/ci/u3-int-qa/READY-FOR-PG-WINDOW-AND-GATE.md，sha256 83c2403b57b3a08e353618a82056cdc7cbed78101344b0e6153bec85c6994a98）。
    - 总管已放行（message_id a9ac5f81-3a7c-4bef-927a-439a6c75beb5，queued）：先开 PG 窗口，端口 56330–56339；停机后直接给门禁位，两项串行。门禁前须核对 clone 内没有未跟踪的 AGENTS.md 或 module.yaml 副本。门禁固有写入按 P10 R1 (b) 先例允许，并做前后快照；run 目录保留，供 P06 作基线。
    - 在它完成之前，本机不再放行第二个 Soul/Host PG 窗口，也不再放行第二个累计门禁。
  - 本次 Owner 输入以来，总管已发 5 条跨会话消息。
- 2026-09-27T03:24:00Z P05 R4 冻结定稿与 QA START、P10 R2 冻结与 README 裁定（总管）：
  - P05 R4 Builder 于 03:21:41Z 发出定稿通知，状态 undelivered，由 transcript 取得。存档为 receipts/P05-R4-BUILDER-session-FREEZE.md，sha256 3969ac1316ace48ea6fb28659322afba2d746d080f9663ec738c412c3f4de8de。
    - 该存档首次写入时，会话 ID 误写为 local_1c0d8d4a，已当场改为账本 L178 所载的 local_a5acbfc0-bdcb-4f0f-8f00-f4c5bd5655f7。上面的 sha256 是改正后的值。
  - 定稿物复算：
    - FREEZE.json sha256 07a71217340bcc24d6490ffd8d1ee24e98ffeef4314ce6cf3e4e71ac08f9d0c5，03:03 起未变。
    - REPORT.md sha256 7533a524a6b17127181da02750dbf40618b58106e73ea6023c9b0836751f5c4f，27449 字节；03:05–03:20 间多次修改后定稿。
    - 候选身份与 diff 的核验见上一条。
  - P05 QA START 已发（message_id ef48dcec-6e11-4462-87d3-958a8f58f362，delivered），正文 sha256 63b9ec1ca0fc36a750d2a501edc418f20111250ed46f4fe4e301da0d82681918。裁定：
    - A1：seam 读法照准。
    - A2：F01 按 WO 原文判关闭，范围为锁、签名、释放三个在途点。L81 RevalidateAsync 在途无反例，照实记为 finding，对照 R3 L103，责任方为 P05 后续轮。release 行“by construction only”是否可接受，由 QA 判定并写明理由。
    - A3：接受 owner lease 层加真实 PG 的注入，不接受 mock。
    - A4：接受单元级断言。
    - A5：两种变异都跑。
    - A6：外审 NOT_RUN。
    - A7：门禁固有写入按 P10 R1 (b) 先例处理，前后快照；clone 内不得有 AGENTS.md 或 module.yaml 副本；base 为 4156d33。
    - A8：先从镜像 clone，再从 Builder clone fetch。
    - A9：实际档位如实记录。
    - 另附 pgpass 端口须写 `*` 的已知陷阱。
  - P10 R2 Builder 于 03:22:12Z 发出冻结通知，状态 undelivered，由 transcript 取得。存档为 receipts/P10-R2-BUILDER-session-FREEZE.md，sha256 4a028dd1ca3887f1b0d692e6b6f2c50a7914bec85d80aae6dd091f26a3b9b894。
    - 另：03:21:25Z 监视器报过一次 UNDELIVERED，是误报。那是该 Builder 用 grep 读自己 transcript 时输出的旧投递失败文字，不是新消息。
  - P10 R2 冻结物只读复算：
    - FREEZE.json sha256 2a585f989498d79969119ce5f0e139ee41dc9f276534ee47198a6cb5fce246d6；REPORT.md sha256 32da4cb0762c096a4e0b9b7e9b992d2b4d4a953f25985a25869c0fcfa390c7b5。
    - commit ef9819f961142f14c33d761656b3a3eceaf85e5b，tree 062dee47fed61294189bb3ca46ea345c7030d08a，唯一父 11d43cf（tree 870bc80c…）。
    - 按 R1 口径（git diff --no-ext-diff --no-textconv，不带 --binary）复算：922db5b..H 为 105177 字节，d1a52ee7…；11d43cf..H 为 26424 字节，75ff54cc…。两者都与 FREEZE 一致。
    - 按统一口径（--binary --full-index --no-renames --no-color）另算，供参考：922db5b..H 为 105837 字节，403d21c2…；11d43cf..H 为 26820 字节，b6b59426…。
    - 10 个路径与 allowlist 相同，全部为 M。HEAD 在分支上，tracked 工作树干净。
  - P10 R2 README 裁定（message_id 862ce7a4-1b03-436d-8176-df23d11ddca0，delivered）：
    - 事实：operations/README.md 新句 “exact redelivery returns the durable terminal receipt once its Journal append is committed” 与 CR-1 用例在暂停点的实测不符。暂停点处 Journal 已追加、尚未 FinalizeCompletion，实测返回 IN_PROGRESS。因此不满足 WO §2 CR-1 “README 与实现一致”。
    - 成因：总管 START §1 要求逐字采用回执文本，责任在总管。
    - 处置：在 ef9819f 之上追加一个只改这句的文档提交，写明两个条件；然后重新冻结。
      - 重做：diff --check、allowlist 核对、validate_repo、两份 diff SHA。
      - 沿用：构建、12 个 suites、红灯证据，前提是给出 ef9819f..新 HEAD 只动 README 的证明，以及 src、tests 两棵树的 tree 相同。
      - 如果沿用不成立，就停下回报。
    - 总管已只读确认：候选内测试和 Tools/ci 都不引用该 README 路径。
    - 第 2–4 项：时钟观测点变化交 QA 审查；与回执的出入照准；Q2 窄竞态不修，只记录。
    - P10 R2 QA START 等新冻结后再下达。
  - 本次 Owner 输入以来，总管已发 7 条跨会话消息。
- 2026-09-27T03:32:54Z Owner 第 4 条裁决与 P17-INT 通知（总管）：
  - Owner 在总管会话中先后两次输入：
    - 2026-09-27T03:27:05.472Z（uuid 5deb8cbf-fc8c-46d4-bf91-52c7e83ab100），原文“你为什么停下来？”；
    - 2026-09-27T03:30:04.705Z 送达的轮中消息（attachment uuid f4dfe52f-a6e0-44a5-ba11-dc5be4738c0e），原文：“1. 第 4 条不收窄，账本 L192 原文有效。2. P17-INT 的 D 另删 Trusted Runner operations/README.md 第 17 行的 "Rollback routes to the previous signed runner and preserves emitted hashes." 和 AGENTS.md 第 35 行的 "rollback routes to the prior signed runner and "，依据是该模块 manifest 的 rollback 字段。3. D 改写后的 Builder 自测重跑范围由总管按 PDD L119 裁定并记账，QA 在冻结头上照常全跑。依据文书：coordination/research/merge-request-v1/condition-4/debate-2/judge.md（sha256 a797e99e…）。”
  - 总管实测 judge.md 的 sha256 为 a797e99eb369fc1dbd8c4501e68f635f2d128b4262c8d5d9a9634bc0fb974be5，20467 字节，与 Owner 给出的前缀一致。它只作依据；指令以 Owner 在本会话的输入为准。
  - 第 4 条据此定稿。本条目上方 merge.request 裁决条目中的原文“P17B 之前的 Merge Controller 和 Trusted Runner 构建，永不作为部署或回退目标”有效。WO-P17-INT §1.2 第 4 项中“Trusted Runner 是否同样适用，待 Owner 确认”至此已确认，确认记在账本；WO 文本不改（§1.2 约束：裁决只记在账本里）。
  - 总管只读核验（候选 D=9c1755d）：
    - Runner operations/README.md L17 全文为 “`factory_disable_trusted_execution` stops new processes. Rollback routes to the previous signed runner and preserves emitted hashes.”。
    - Runner AGENTS.md L35 以 “…Shadow runs synthetic fixtures only; rollback routes to the prior signed runner and `factory_disable_trusted_execution` stops new processes.” 结尾。
    - Runner module.yaml L39 rollout.rollback 为 “stop new execution, candidate evidence consumption and merge; preserve history and evidence hashes; never restore the known partial request signer”。
    - 两个文件的内容 sha256（AGENTS 422dc28f…，README 98223194…）和 blob id 在候选内都没有被引用。governance/modules/module-catalog.yaml:172 只引用 AGENTS 的路径。
  - 总管按 PDD L119 裁定重跑范围。L119 原文：“压缩本身不使证据失效；基线/范围/信任/有效期变化才按影响重绑，不重跑已足够且仍有效的检查。”
    - 在 D′ 上重做：身份核对；diff --check（afb4f9b、fede545、M 各对 D′，另加 M..D′）；generate_governance 校验；validate_repo；16 个非 PG 套件全部重跑。理由：release-controller、artifact-builder、control-plane-host 的 src 中有按仓库树或提交读取 AGENTS.md 的代码路径，“不受影响”无法低成本排除；这批套件上次合计约 2 分钟。
    - 沿用 D 的结果、不重跑：M12 PASS、M16 FAIL、M26 FAIL。前提：FREEZE 附 9c1755d..D′ 的 name-status；三个 PG 模块的子树 tree 相同；并用 grep 证明三个 PG 测试不读取 Runner 的这两个文件。
    - 重跑结果与 D 不同时，停下回报。QA 在冻结头上照常全跑。
  - D′ 的规格：父为 M 0718a6f，候选仍为两个提交，只含三处行内删除。
    - MC README L3，与 D 相同。
    - Runner README L17：删去该句并连同其前的空格，行尾不得留空格。
    - Runner AGENTS L35：删去 Owner 所给的片段。
    - 动 AGENTS.md 前，先重读 Runner 的 AGENTS 和 manifest 并重绑。
    - 模块 CHANGELOG 按 WO §3.1 第 2 项的同一规则处理。
    - 不改 governance/** 和合同；generate_governance 校验若失败，停下回报。
    - 旧 D 9c1755d 在 REPORT 中记为“已被取代”。
  - 同一条通知一并下达 M26 定性：既有缺陷 FAIL，不是本单元引入、不是新增失败，不重跑、不修。归属 P03/P17/P18/P19（factory-evidence-ledger）。是否开修复单元待 Owner 决定，不影响冻结。
  - 通知 message_id 0e880b05-41ad-4d3e-86c6-e0101f2a8bd2，delivered。
  - 本次 Owner 输入（03:30:04Z）以来，总管已发 1 条跨会话消息。
- 2026-09-27T03:35:52Z U4-INT 工单签发（总管）：
  - 起因：账本 2026-09-26T17:47:42Z 条目写明“P05 R4 + P10 R2 集成”工单等两者接近冻结时再写。现在 P05 R4 已冻结（1f991fa），P10 R2 正在补一个 README 文档提交后重新冻结。
  - 工单：coordination/WO-U4-INT-P05R4-P10R2.md，sha256 0ff679a4b0a31ec3649cda324cc9d366d7faa70281c166276ef9e028bdf63ccc，9558 字节。
    - 基线 L：U3 集成头 f91d1dc。
    - 候选：两个合并提交。M1 的父为 L 和 R4 1f991fa；M2 的父为 M1 和 R2 最终头。R2 最终头在 Builder START 中给出。
    - 门禁 base 为 f91d1dc。端口复用 U3 的两段：Builder 56320–56329，QA 56330–56339。
  - 总管只读实测，作为工单依据：
    - 祖先关系：afb4f9b 是 922db5b 的祖先；922db5b 是 5ab9aab 和 11d43cf 的祖先；merge-base(5ab9aab, 11d43cf) = 922db5b。
    - 在 L 一侧，R4 的 5 个路径自 5ab9aab 起只有 CHANGELOG.md 改过；R2 的 windows-edge-worker 路径自 922db5b 起没有被改过，同期改过的只有 CHANGELOG.md。
    - edge-local-journal、windows-edge-supervisor 自 922db5b 起在 L 上没有改动。
    - 56320–56339 实测无监听。
  - 任务卡：第一版 task_c4b8372c、task_146b859b 已撤回。原因是卡内“不建目录”与“回执写入 units/u4-int/”互相矛盾。
    - 改正后重发为 task_8ca4cfdd（Builder）和 task_5dad20f2（QA），等 Owner 点击。
    - 本轮 PREACCEPT 唯一允许的写入：建 units/u4-int/ 目录，并写各自的回执文件。
  - 未纳入：P06 S1 在建，是另一条线。它与 U4 冻结头如何合流，待 S1 冻结后另行裁定。
- 2026-09-27T03:37:50Z U3 QA 结论与出口（总管）：
  - U3 QA 最终报告：03:36:07Z 发出，undelivered，由 transcript 取得。存档为 receipts/U3-QA-session-FINAL.md，sha256 9fc70b7e9f927e1e084d9da7e057cc5f5e5bf76e28c705c4a797542e6e7f066a。
  - 总管复算：QA-REPORT.md sha256 8c2b637b8264e0d994d71b564514ffb535a5435ae2b9c684c948fefeb72655e1（27340 字节）；EVIDENCE_INDEX.json sha256 0c8c2485f6c1cb9e5f4f3435f5da9101172a7ecbfd955b7445a6226239809b28（37324 字节）。两者与报告一致。
  - 结论为 PARTIAL：集成正确，没有新增失败。
    - PG 段：03:14:36Z–03:18:46Z，229 例，其中 Soul 98+1（P11）。
    - 门禁：03:20:37Z–03:26:10Z 只跑一次。exit 1，ABORTED；Phase0 为 48/53，失败项与两父逐项相同；module-governance 诊断中候选独有行为 0；未跟踪的 AGENTS/module.yaml 副本为 0。
    - 门禁 run 目录保留在 units/u3-int/qa/DSP_ZD/Reports/ci/candidate-runs/integration/0f30d554fd0fcfd9d798dbacd67732c8/，供 P06 和 U4 作对照。
  - 按 WO-U3 §7，U3 出口已满足：f91d1dc 确认为 P06 线基线，也是 U4 的基线 L。PDD §4.3 的重绑条件不触发。
  - 门禁位与 Soul/Host PG 窗口均已释放。
  - QA 新指出一项既有缺口：policy-approval.integration 实际执行 32 例，低于 candidate-test-policy 的 minimumExecutedTests 40。
    - 该模块在 C 与 P2 上逐字节相同，策略文件在 B、P1、P2、C 上为同一 blob。
    - 总管裁定归属 P06：PDD 模块表（PDD-DIGEST L891）中 policy-approval 的首个包是 P06；P06 S1 正在改该模块；P06 S1 QA 的 PREACCEPT 已计划按 40 核对。
    - 下一条发给 P06 S1 Builder 的消息中一并告知：以 40 为准；不得为凑数新增 S1 范围外的测试；冻结时仍低于 40 的，照实报告。
  - 同时补存档 P17-INT QA 发给新总管的 PREACCEPT 回执（01:35:16Z，自述以本份为准）：receipts/P17-INT-QA-session-PREACCEPT-to-new-orchestrator.md，sha256 dd03a4929d8c7383a90f2d67ef257c51536df63fe979dc76b53bbffa93db6eba。
  - 监视脚本修正：只把 SendMessage 自身 tool_result 中的 queued/undelivered 计为 UNDELIVERED。原因是 03:21:25Z、03:36:11Z 两次 P10 R2 的 UNDELIVERED 都是误报，来自该 Builder 用 grep 读取自己的 transcript。新监视 b6q6s86c9，WATCH_SINCE 为 03:36:30Z。
- 2026-09-27T03:39:32Z P05 R4 QA 放行（总管）：
  - P05 QA 于 03:37:55Z 回报 READY-FOR-PG-WINDOW 和 READY-FOR-GATE，状态 undelivered。存档为 receipts/P05-R4-QA-session-READY.md，sha256 5ced57b448e581d4d82483a63d0db83b6bbc7422c150d05327a1117c050effcb。
  - 回报内容：不需要 PG 的检查已全部完成，无偏离。
    - 身份和 diff 均复算一致；FREEZE 索引的 117 个文件全部相符。
    - F03 已关闭；另有 QA 自己的变异 M3。
    - clone 内没有未跟踪的 AGENTS 或 module.yaml 副本。
  - 放行前实测：563xx 无监听，无门禁进程。
  - 放行（message_id e546edce-4c54-4400-bac1-4db0b938483f，delivered）：先开 PG 窗口，端口 56390–56399；停机后直接给门禁位，base 4156d33，两项串行。门禁固有写入按先例处理并做快照。
  - 在它完成之前，不再放行第二个 Soul/Host 窗口或累计门禁。P06 S1 的 Host.integration 如果提出申请，排在它之后。
  - 本次 Owner 输入（03:30:04Z）以来，总管已发 2 条跨会话消息。
- 2026-09-27T03:43:42Z Owner 授权（总管记）：
  - Owner 在本会话约 03:4xZ 的原话：“之后新开工作、新开 Session 之类的，我都授权你，你不用再问我让我去点，你自己决定就行。”
  - 同时 Owner 已点开 U4 两张任务卡：task_8ca4cfdd（Builder）和 task_5dad20f2（QA），两个 session 开始做 PREACCEPT。
  - 总管实测本会话的工具：没有 start_session 或 hand_off_to_session（ToolSearch 返回 “No matching deferred tools found”）。能开出独立 session 的只有 spawn_task 任务卡，而任务卡仍要点一下才能启动。
  - 执行口径：
    - 开不开新工作、新单元、新 session，由总管自行决定并记账，不再请示 Owner。
    - 发卡后在回复里写明“待点卡”，这一步是平台机制，总管无法代替。
    - 不用本会话的 subagent 充当 Builder 或 QA。理由：PDD 要求每个单元的 Builder 和 QA 各为独立 session；总管也不写候选。
- 2026-09-27T03:50:54Z Owner 档位强约束与执行（总管）：
  - Owner 原文（本会话 2026-09-27T03:46:24Z，uuid 6e6c6aee-ef4e-4175-8a7a-0c91ecea4ea6）：“我发现了 Builder 的 session effort 用得太高了。所以每次开新 session 之前，必须检查 effort 是否正确，以及使用的模型是否正确。1. Builder：使用 ops 5.5 medium 2. QA：使用 ops 5.5 的 max。这个必须作为长期记忆记下来。”同一条消息说明，此前的停止是误操作，编排继续。
  - 已存为长期记忆：memory/dps-session-model-effort.md，并加入 MEMORY.md 索引。
  - ORCHESTRATION-SESSIONS.md 第 2 节按新规则改写：QA 改为 max；删去对 Builder 按 max 运行的接受；写入开工前核查的做法和实测根因。第 3 节的依据改为直接引用 01:02:44Z 裁定。文件 sha256 由 82fe2779… 变为 63260fe82596aef92323fbfe6ce356cb87f4597341bfb0a690f48f43cc61773e（5741 字节）。
  - get_session 实测保存值：
    - U4 Builder 为 max，不合规；U4 QA 为 max。两者都是 03:42 由任务卡开出，继承了总管当时的 max。
    - P10 R2、P17-INT、P06 S1 三个 Builder 都是 medium。
    - P05、P17-INT、P10 R2、P06 S1 四个 QA 都是 xhigh。
    - 模型全部为 claude-opus-5-5。
  - 已改正：U4 Builder 改为 medium；上述四个 QA 改为 max。工具回执都是“from its next turn on”。
  - 根因实测（只读）：
    - 各单元 transcript 的 perTurnEffort 全部为 max：P10R2-B 602 条、P17INT-B 515 条、P06S1-B 371 条、U4-B 68 条。
    - ps 显示各 session 进程带启动参数 --effort：P10 R2 两个进程为 xhigh，其余为 max。
    - ~/.claude/settings.json 第 5 行的 env 含 "CLAUDE_CODE_EFFORT_LEVEL": "max"，进程环境里也有 CLAUDE_CODE_EFFORT_LEVEL=max。因此所有 session 每轮都按 max 运行，与保存值和 --effort 无关。
    - 只读取了与 effort、model 相关的行，没有读取该文件的其余内容。
  - 结论：在这一项被移除、相关进程重新启动之前，Builder 的实际档位仍然是 max，不合规。改这个文件属于全局持久配置，本机所有项目都会受影响，须 Owner 明示许可，已提请 Owner 决定。
- 2026-09-27T03:52:16Z 全局 effort 覆盖项已删除（总管，Owner 许可）：
  - Owner 在 AskUserQuestion 中选择“总管删这一项（推荐）”。
  - 改动于 03:52:03Z 完成：从 ~/.claude/settings.json 的 env 中删去 "CLAUDE_CODE_EFFORT_LEVEL": "max" 一行，并去掉上一行 BASH_MAX_TIMEOUT_MS 末尾的逗号。
  - 改动前后解析 JSON 比对：除去这一个键外完全相同，文件权限仍为 0600，大小 2288 → 2249 字节。
  - 回滚办法：把这一行加回 env。
  - 生效范围：已在运行的进程保留启动时的环境。之后各 session 的实际档位是否变化，以下一轮 transcript 的 perTurnEffort 为准，届时回读并记账。
- 2026-09-27T03:54:14Z P10 R2 重冻结核验与 QA START（总管）：
  - Builder 回报（03:50:53Z，undelivered）存档为 receipts/P10-R2-BUILDER-session-REFREEZE.md，sha256 180db0fc43adbf5775f91a6e516ccce060c5174b398e2f6b6c17c8d3c4ff2f96。
  - 总管只读复算：
    - 最终头 1536e3844af6978f8a25ba61d15c1565e69fad19，tree 591fd11bad1cac5e403b59d0e4374a5b5afce006，唯一父为 ef9819f。Builder clone 的 HEAD 在该提交上，分支为 codex/p10-review-fixes-r2，tracked 工作树干净。
    - FREEZE.json sha256 为 1d871e0e…（348242 B），REPORT.md 为 10396dae…（70764 B），垫片 patch 为 102014d5…，都与回报一致。
    - ef9819f..1536e384 只改了 Modules/windows-edge-worker/operations/README.md，改动为 1/1 行。922db5b..1536e384 的 diff --check 结果 rc=0。
    - R1 口径的三份 diff 与 FREEZE 一致：e0384c40…（105426 B）、d04fe016…（26673 B）、5a468a57…（5242 B）。
    - 统一口径为 62eb767b…（106086 B）、834233ed…（27069 B）、69bb70c1…（5308 B）。
  - 裁定：README 改写符合 03:23:05Z 裁定的意图。Builder 没有逐字写“在此之前”，其理由（claim 释放前状态已为 Completed）交 QA 从源码核实。沿用 ef9819f 的构建和 suites 结果，依据是 src 与 tests 的子树不变，并有 5 条补充只读检查。QA 在最终头上全部重跑。
  - Builder 自报 perTurnEffort 624 条全部为 max（01:51:11Z–03:49:22Z），与总管实测一致。
  - QA START 已发 local_80f25d82：先用 get_session 回读为 claude-opus-5-5 / max，再发出。message_id 63ad0740-bb85-43d3-9d80-3b778277f0d2，delivered。F1–F7、N1 按草案裁定，另加 README 核对和档位两节。
  - 下一步：U4 Builder 的 START 以 R2 = 1536e384 为输入，等 U4 Builder 的 PREACCEPT 回执到达后下达。
  - 本次 Owner 输入（03:46:24Z）以来，总管已发 1 条跨会话消息。
- 2026-09-27T04:02:05Z P17-INT D′ 冻结核验与 QA START（总管）：
  - Builder 回报（03:57:15Z，undelivered）存档为 receipts/P17-INT-BUILDER-session-FREEZE-Dprime.md，sha256 296b973848401fb7079736cafffb54b54460cf3b6187253bba7d40a256096e6b。
  - 总管只读复算：
    - D′ 为 320ce77836edfcc13d2136ddb8cc76d4b0b0a566，tree 28d925799afe4cb8c025cbcc8a0693a0fd1117f9，父为 M 0718a6fd665aba8e5708ac4cd9d8f84347dbc846。M 的 tree 为 f5eac17e…，两个父依次为 afb4f9b、fede545。
    - Builder clone 的 HEAD 与分支头都是 D′，tracked 工作树干净。
    - FREEZE.json 为 4244029d…（50714 B），REPORT.md 为 6bcc8b7c…（17915 B），与回报一致。
    - M..D′ 共 3 个路径，每个 1/1 行，与 Owner 03:30:04Z 所给的删除片段逐字相同，行尾没有空格。diff --check 结果 rc=0。
    - 按 FREEZE 记录的 argv（git -c core.quotepath=false diff --no-color --no-ext-diff --no-renames --binary --full-index）复算：A→M 为 3c90ddbc…（670992 B），F→M 为 da67ebff…（1337086 B），M→D′ 为 45a49231…（3682 B），全部一致。
    - 统一口径下 F→M 为 bc178314…（1337346 B）。只加 quotepath=false 后，结果就等于 da67ebff…，所以差异来自一个非 ASCII 路径的转义，不是缺陷。
  - merge.request 裁决条目补记：Owner 裁决所绑定的集成候选为 M 0718a6f 与 D′ 320ce77；旧 D 9c1755d 已被取代。
  - 沿用 D 上的 M12/M16/M26 结果，这符合总管 03:32:54Z 按 L119 所作裁定的前提：Host、Artifact、Ledger 三个模块的 tree 相同，governance、Tools、Tests 相同，三个测试文件 grep 结果 rc=1。16 个非 PG 套件在 D′ 上重跑，共 276 个测试，结果与旧 D 相同。
  - Builder 自报实际档位全部为 max（582 条，其中 03:46:24Z 之后 73 条），进程 env 中 CLAUDE_CODE_EFFORT_LEVEL=max。记为编排层偏离，不算候选缺陷。
  - 保留物：environment/secrets（TEST_ONLY，0600）和已停止的 environment/pg/data。等 P17-INT QA 结束后统一收口。
  - QA START 已发 local_19d3e934：先用 get_session 回读为 claude-opus-5-5 / max 再发。message_id 0e645556-3958-4950-8d5b-ea6af475741d，delivered。
    - 内容包括 D1–D11 裁定、Runner 通知和档位一节。
    - 另请 QA 判断 TR AGENTS L22 “Candidate code is executed by the previous stable signed Runner artifact.” 是否与第 4 条冲突。
    - M16、M26 修复单元由总管安排在 P18A-INT 冻结之后（依据 Owner 03:43Z 授权）。
- 2026-09-27T04:02:05Z U4 Builder 档位不合规，换 session（总管）：
  - 第一个 U4 Builder（local_c309a4c4，pid 13482，03:42:02Z 启动）的 PREACCEPT 回执：units/u4-int/PREACCEPT-BUILDER.md，sha256 76fc800ace9a28a79c8380a8640db2b2bd7b4c19e7edc3ef0df8089149eade84（28626 B）。
    - 结论为接受角色，但把档位列为 START 阻断：transcript perTurnEffort 113 条全部为 max，进程 env 为 CLAUDE_CODE_EFFORT_LEVEL=max。
    - 该进程启动早于 03:52:03Z 删除覆盖项，现有进程下无法达到 medium。
    - 另有 Q2–Q12 待裁定。回执内容作为新 Builder 的参考数据。
  - 裁定：
    - 不给该 session 下 START。它只做过只读工作，唯一写入是 units/u4-int/ 目录和这份回执。
    - 改开新的 U4 Builder：任务卡 task_84a0c506，待 Owner 点卡。新进程启动时不再带全局覆盖项。
    - 新 session 首轮只做档位自检，结果回报总管。总管随后用 set_session_effort 设为 medium 并回读，再下达带 PREACCEPT 绑定要求的 START，START 开头再做一次 perTurnEffort 自检。
    - 这一轮 perTurnEffort 如果仍不是 medium，说明逐轮 effort 对新进程也不生效，届时报 Owner。
  - P06 S1 Builder（02:25:58Z 启动）的进程同样带有覆盖项，保存值 medium 不生效。它正在实现途中，换人需要交接未提交的工作，风险大于收益。因此让它按 max 完成本单元，照实记为偏离，之后的 Builder 一律用新 session。
- 2026-09-27T04:06:29Z U4 QA 回执；P18A-INT 开单（总管）：
  - U4 QA（local_b4b8c271）PREACCEPT 回执：units/u4-int/PREACCEPT-QA.md，sha256 4049362bd35e456899cf245653c4dd54c05b6aff436ceb0cceb6f25a15859233（29213 B）。消息于 04:02:53Z 发出，undelivered，正文与文件相同。
    - 结论为接受；实际档位 102 条全部为 max，符合 QA 的 max 规定。
    - U1–U11 待 QA START 时裁定，届时 Builder 已换为 v2。
  - P18A-INT 开单，依据是 Owner 03:43Z 的授权和 WO-P17-INT §1。
    - 工单：coordination/WO-P18A-INT.md，sha256 10c73d05843704eed16c8200d6cbf416ca0c559dfac8f8764c639e10815edf29，12497 B。
    - 候选：一个合并提交 M18，两个父依次为 D′ 320ce778 与 P18A 2aaaaa8（tree 23abe13a）。门禁 base 为 D′。
    - 端口：Builder 56300–56309，QA 56310–56319。P10 R2 不用 PG；04:04Z 实测这两段无监听。
  - 总管只读实测，作为工单依据：
    - merge-base(D′, 2aaaaa8) = fede545。计算时 GIT_DIR 指向 P17-INT Builder clone，另用进程内 GIT_ALTERNATE_OBJECT_DIRECTORIES 读镜像对象，没有写 alternates 文件。
    - fede545..2aaaaa8 共 13 个路径（+739/−38）。
    - trivial merge-tree 显示 5 个双改文件：CHANGELOG、Tests/ci/test_candidate_policy.py、Tests/ci/test_phase0_gate.py、Tools/ci/run_phase0_gate.py、governance/policies/candidate-test-policy.yaml。冲突标记只有 CHANGELOG 1 处。
    - 3e47ac7..afb4f9b 没有改动 factory-evidence-ledger、工程标准文档、run_candidate_gate.py、test_candidate_gate.py 和两份 schema。
    - P17-INT Builder 下载的官方源码包 postgresql-18.4.tar.bz2（22567173 B，sha256 81a81ec6…，来自 ftp.postgresql.org，并有 .sha256 文件）可以只读复制给本单元编译。
  - 任务卡：task_55949e2f（Builder，首轮只做档位自检）、task_9709d249（QA，首轮只读 PREACCEPT），均待 Owner 点卡。
  - 监视改为 bz46c3hlq，WATCH_SINCE 为 04:03:00Z。旧监视 b6q6s86c9 已停止。
- 2026-09-27T04:08:57Z 环境事实：ponytail 插件（Owner 输入，总管记）：
  - Owner 在本会话 2026-09-27T04:06:55.786Z（uuid a95c5949-53bb-4d90-9111-40dbf78b3ddf）告知：2026-09-27T03:42:50Z 经 Owner 批准，本机全局安装了 ponytail v4.10.0（DietrichGebert/ponytail，提交 e3ba2aa6，full 档，不自动更新）。
    - 之后新启动的 Claude 会话会被注入“少写代码”规则。已在运行的会话要 /reload-plugins 或重开才生效。
    - 它是软约束，不是门禁，不作证据。
    - Builder 在刻意简化的地方写 # ponytail: 注释，Owner 已接受。
    - 它鼓励“能默认就别停”。与项目的 fail-closed 冲突时，以项目规则为准。QA 须核查 Builder 有没有把该请示的事自己默认掉。
  - 执行口径：
    - 在途单元不中途重载，总管不向任何在途 session 发 /reload-plugins。
    - 03:42:50Z 之后启动的 session 会带上插件，包括 U4 Builder v2、P18A-INT Builder 与 QA，以及以后的单元。03:42:50Z 之前启动的不带，包括第一个 U4 Builder（03:42:02Z）、P06 S1、P17-INT、P10 R2、P05 各 session。
    - 插件注入的规则与工单绑定的 ponytail SKILL.md（1316a2f3…）有出入时，以工单绑定版本和项目规则为准。
    - 对 Builder 带插件的单元，在 QA START 中加一项：核查 Builder 是否把应当停止、回报或请示的事项自行默认处理。
    - 该条款只写进各单元的 START，不改动编排规则文本。
  - 同一消息再次重申：新开工作、新开 session 均授权总管自行决定。
    - 总管口径不变：本会话没有 start_session 工具，只能发任务卡，卡需 Owner 点一下。
    - 零点击的替代做法，是在终端标签或后台用 claude CLI 起会话。这样做有三个代价：没有双向 SendMessage，Owner 在侧栏看不到，而且在六个单元在途时引入未验证的新机制。总管不采用。
  - 本次 Owner 输入（04:06:55Z）以来，总管已发 0 条跨会话消息。
- 2026-09-27T04:17:45Z P05 R4 QA 结论与账本更正（总管）：
  - 最终报告发于 04:15:49Z，undelivered，由其 transcript L1518 取得，存档为 receipts/P05-R4-QA-session-FINAL.md（sha256 69e7208f5ce8598a56e93eba54d5d8746522b73a293dca11b1b8a58eb89b2130，6229 B）。
  - 总管只读复算（04:16Z）：
    - QA-REPORT.md 为 7ada486020abf8b86bc35f341ddc8a4f127e7124d0b9b651690b9c0f806fdc3d（37953 B），EVIDENCE_INDEX.json 为 50adb326006645b03a31c1cf13145f51889509511ca941e463d2c5a665af0621（33144 B），与报告一致。
    - 门禁 run cfdde0559212fe1c87f7b373dd183db5 的发布状态为 ABORTED。companion 中 evidence_mode 为 WORKSPACE_DIAGNOSTIC_ONLY，head 为 1f991fa，workspace_clean 为 true，baseline 为 4156d33，Phase0 为 48/53。失败的 5 项就是已知 5 项，exit code 依次为 1、1、1、3、1。
    - 56380–56399 无监听，也没有 postgres -D 进程。
  - 结论 PARTIAL：
    - F01–F03 已关闭。10 个新用例全部通过，B1–B3、M1–M3 变异都失败在断言上。
    - required suites 中只有 soul-registry.integration 为 FAIL（106/107），唯一失败是保留的 P11 用例。
    - 阻断项按 R3 §8.2 口径，实现缺陷为无。R4Q-01（L81 没有在途反例）和 R4Q-05（README 中“41-case”已过时，实测 67）归 P05 后续轮。其余发现不阻断。
  - 对 U4 的影响：R4 输入 1f991fa 维持有效，PDD §4.3 的重绑条件不触发。P10 R2 QA 仍在进行。
  - QA 自报两项偏离：
    - 04:08:10Z 执行 git status 时漏加 --no-optional-locks，刷新了 DSP_ZD/.git/worktrees/competent-roentgen-e765cc/index 的 stat 缓存。跟踪内容不变，但违反了“不写 DSP_ZD”的约束。
    - 在 /tmp 写了两个临时文件，同一命令内已删除。
    - 这两项照实记录，总管报 Owner，不作处置。
  - QA 保留物：environment/secrets/pgpass（0600，TEST_ONLY）、已停机的 pg/data、scratch clone、门禁 run 目录。收口时机由总管另定，暂时保留。
  - 账本更正（2026-09-27T03:24:00Z 条，第 467 行）：
    - 原文“正文 sha256 63b9ec1ca0fc36a750d2a501edc418f20111250ed46f4fe4e301da0d82681918”有误。那是总管草稿文件（5232 B）的哈希。
    - 实际发出的正文是总管 transcript L2342 的 SendMessage 参数，5381 B，sha256 b74fb701e49507e8fc9ccbab94944f4793bfea673a7ee6dc42cf9419585919d4，与 QA 存档 inbound/START.txt 一致。
    - QA 报告中“原因未明”的哈希不一致即源于此，责任在总管。
  - 本次 Owner 输入（04:06:55Z）以来，总管已发 0 条跨会话消息。
- 2026-09-27T04:21:38Z P10 R2 QA 门禁放行（总管）：
  - READY-FOR-GATE 发于 04:20:19Z，undelivered，由其 transcript L1070 取得（正文 7161 B）。QA 另存原文于 units/p10-r2/qa/DSP_ZD/Reports/ci/p10-r2-qa/READY-FOR-GATE-MESSAGE.txt，sha256 36f4c04d56f5ad09377c33925c3d687f69958903562ae08b7f5a5be39e1eed7f。
  - 回报要点：
    - S0–S6 完成。身份、diff（R1 口径与统一口径）和路径都与 FREEZE 1d871e0e… 一致。
    - 12 个 suites 全部 exit 0；5 个 artifacts 的 locked restore/build 为 0 warning / 0 error。
    - 红灯在 922+垫片、11d43cf 上全量复现，与 Builder 逐例对照差异为 0。
    - 未发现 P0 或 P1。README 中的 “once” 与 CR-2 codec 投影记为 P3 观察，不阻断。
  - QA 自报偏离：04:06Z 有一条命令把 allowlist 文本写入 /tmp/qa-allow.<pid>，同一命令内已删除，将写入报告。
  - 档位：QA 共 167 条，全部为 max，符合规定。它只读计数的 Builder transcript 共 638 条，全部为 max，即此前已记的编排层偏离。
  - 放行：
    - 总管 04:20:53Z 实测：没有门禁进程，没有 /private/tmp/dps-phase0-executor-*，563xx 无监听。
    - 放行消息 message_id 68fd54f7-57b2-4d5b-8557-31dca851dc7c，delivered。实际发出的正文取自总管 transcript L3918，1364 B，sha256 2f3d232120a700541d16d8e2eba4fea026961560e2d35293248c3703d6f1a970。
    - 放行内容：base 922db5b，只跑一次，run 目录保留，固有写入按 P10 R1 (b) 先例处理。另请它在报告中核对 P05 R4 QA 观察到的两项单元根外痕迹，即 ~/.dotnet/metadata 的 mtime 变化和 /private/tmp/dps-worker-tests-c5655c34…。
    - 在它的门禁完成之前，不再放行第二个累计门禁。
  - 监视改为 bm9zh34ur（v4，WATCH_SINCE 为 04:16:30Z），新增单元向 Owner 发问事件和新会话事件。旧监视 bz46c3hlq 已停止。
  - P17-INT QA 于 04:11:07Z 在其会话中向 Owner 发出下载授权问题（PyPI 锁定 wheel、PG 18.4 官方源码、门禁时访问 nuget.org），等待 Owner 回答。总管已在本会话提请 Owner，不代答。
  - 本次 Owner 输入（04:06:55Z）以来，总管已发 1 条跨会话消息。
- 2026-09-27T04:47:59Z P10 R2 QA 结论（总管）：
  - 最终报告发于 04:47:07Z，由其 transcript L1657 取得，存档为 receipts/P10-R2-QA-session-FINAL.md（sha256 cd0812e21323ba90e2cf30afd76fe475e40ee9452706f7485310c294dc992f72，6810 B）。
  - 总管只读复算（04:47Z）：
    - QA-REPORT.md 为 f2ad46f1b42465f41df59f1b52befc4a7575306a7c5d55d40e1062f74d3fc635（43592 B），EVIDENCE_INDEX.json 为 89b6ce06e30a457fa769a4325f77cde924ab0500e535deeb8c0805421239a5ef（63442 B），与报告一致。
    - 门禁 run d6ab618c2810a08827005291bd39e8a9 的发布状态为 ABORTED。companion 中 evidence_mode 为 WORKSPACE_DIAGNOSTIC_ONLY，head 为 1536e384，workspace_clean 为 true，baseline 为 922db5b，Phase0 为 48/53。失败的 5 项就是已知 5 项，exit code 依次为 1、1、1、3、1。
    - 此时已没有门禁进程，门禁位释放。
  - 结论 PARTIAL：
    - CR-1..3 全部关闭，没有 P0/P1。12 个 suites 共 232 项，全部通过。
    - module-governance 的 76 行日志与 R1 逐字节相同，候选新增 0 行。
    - 阻断项按 R1 §8 口径，均不归本候选的 Builder。
  - P3 观察 O-1..O-6 不阻断（README 中的 “once”、codec 投影没有测试钉住、null exception 出口没有测试、test.sh 没有 --timeout、Q2 窄竞态、暂停点覆盖）。总管记为 P10 后续轮的已知残余，暂不排期，不另开单。DTO 直接构造风险归后续 API 加固。
  - 总管点名核对的两项，QA 按时间对照归属：
    - ~/.dotnet/metadata 的 mtime：03:52:36Z 不属于它；04:08–04:30 间多次属于它；04:34:42Z、04:44:40Z 与 P06 Builder 的 dotnet test 同时。可见任何会话使用 pinned SDK 都会改变这个 mtime。目录始终为空，机制未核实。
    - /private/tmp/dps-worker-tests-c5655c34… 属于它的 red-scratch 测试，是测试自带的写入，已自行清理。
  - 共享编译服务器 VBCSCompiler pid 68601 于 04:26:51Z 由它的门禁拉起，按约定未强杀，只报告。
  - 对 U4 的影响：R2 输入 1536e384 维持有效，PDD §4.3 的重绑条件不触发。至此 U4 的三个上游 QA（U3、P05 R4、P10 R2）都已结论为 PARTIAL。
  - 档位：QA 共 411 条，全部为 max。
  - 本次 Owner 输入（04:06:55Z）以来，总管已发 1 条跨会话消息。
- 2026-09-27T05:05:37Z P06 S1 Builder：PG 窗口放行与 EG.unit 裁定（总管）：
  - READY-FOR-PG-WINDOW 发于 05:04:23Z，queued，message_id 65162e11-252e-4e33-a3a7-d76136f130ce。由其 transcript L3723 取得，正文 9867 B，sha256 0e9fd9ab5a4a06ff899e365e37d95cd8a04b5522f3adcf3a15fc42c552634913，与其 Reports/ci/p06-s1-builder/READY-FOR-PG-WINDOW.md 相同。存档为 receipts/P06-S1-BUILDER-session-READY-PG.md。
  - 回报要点：
    - 候选 6cbf3e8518c380f7a812d1b8291bac0c033986d5，tree b0fbd34ffe7bc2cf46438061a3d7089aa470769e，唯一父为 f91d1dc。diff 278560 B，sha256 64d1d3c9…，共 32 个路径，都在写域内；Host module.yaml 未改；没有新增 csproj。
    - 除 Host.integration 外，23 个 suite 全部 exit 0。policy-approval.integration 为 41，不低于 40；新增 9 例都在 S1 范围内。
    - 治理诊断（开发辅助）：相对 U3 官方日志，消失 4 条 action.proposal 条目；新增 2 条 approval.evaluation.request 条目（Host 侧缺声明），归 S2。
  - 偏离：开发期工作树运行（04:44Z）中 EG.unit 为 33/34，失败用例是 ProcessGuardianIsOnlyStrongRootAcrossDisposeAndForcedGc（GC 时序断言），原始日志 c5c4c2f7…。Builder 按规则没有重跑，提交上也还没跑。
  - 裁定（message_id be303a8f-b527-4b9d-81b0-104b3c72244d，delivered；正文取自总管 transcript L4246，2036 B，sha256 58ab13444594e147bf4d5506652d9b6343e1b305347b50646e13d98c5694d673）：
    - 放行 Host.integration 窗口：一次性空库 p06_s1_host，只跑一次，pgpass 端口写 `*`，偏离即停。放行前 05:05:06Z 实测：563xx 只有它自己的 56340，没有门禁进程，没有其他 Soul/Host 窗口。
    - EG.unit：准许在 6cbf3e85 上按原文命令跑一次。理由：失败发生在提交前的工作树运行中，提交上还没正式跑过，这一次不算为转绿而重跑。PASS 时 REPORT 照实披露 33/34，定性写“疑似 GC 时序不稳定，未证实”；FAIL 时停下回报。QA 另行独立运行并核对披露（已写入 P06 S1 QA START 草稿）。
    - 数量下限按 03:37:50Z 的裁定告知：41 ≥ 40。
  - 在它的窗口结束之前，不再放行第二个 Soul/Host 窗口。
  - 本次 Owner 输入（04:06:55Z）以来，总管已发 2 条跨会话消息。
- 2026-09-27T05:17:42Z P06 S1 冻结与 QA START（总管）：
  - Builder 的 FREEZE 发于 05:15:51Z，已送达。原文存档为 receipts/P06-S1-BUILDER-session-FREEZE.md（sha256 2782e4be9c0205a3190802bcbad662183be525447c29fba305149f4e0649d46e，3575 B）。
  - 冻结物：
    - C 6cbf3e8518c380f7a812d1b8291bac0c033986d5，tree b0fbd34ffe7bc2cf46438061a3d7089aa470769e，唯一父 f91d1dc。
    - FREEZE.json 8d6824afaec778486f320b09f7bc68f1516931b9edf08fc57706584ac9a70e03（75177 B），REPORT.md e9a1582c538fca4f74e13bb66b3e562e3b34fa35f8ee9fe07fdc908ad75b0bb2（26552 B），均为 Reports/ci/p06-s1-builder/ 下。
  - 总管只读复算（05:14–05:16Z）：
    - HEAD 与分支头都是 C，tracked 工作树干净。
    - 统一 argv 下 diff 为 278560 B，sha256 64d1d3c9…，与 FREEZE 一致。
    - 32 个路径，新增 7、修改 25、删除 0，没有新增 csproj，diff --check rc=0。
    - 两份文件的 sha 与回报一致。
  - Builder 自报：
    - 提交上正式运行 25 个 suite，全部 exit 0。policy-approval.integration 为 41，不低于 40。
    - Host.integration 66/66（05:06:30Z–05:06:58Z，在窗口内）。EG.unit 在 C 上为 34/34，开发期 33/34 已在 REPORT §6.1 披露。
    - PG 于 05:07:15Z 停机。监视 05:07:21Z 看到 56340 关闭，Soul/Host 窗口随之释放。
  - Builder 自报的触发器覆盖缺口：没有以 executor 角色逐字 begin 一个 v1 行。已列入 QA START，由 QA 的“第 5 条路径”探针核验。
  - Builder 保留物：已停机的 pg/data、pgpass、pg-bootstrap.pw（0600，TEST_ONLY）、base-probe。收口时机由总管另定，与 P05 R4、P17-INT 的保留物一并处理。
  - QA START：
    - 发给 local_aa99a782，message_id 07c6ef5f-9882-4042-8f68-e1f0b67361af，delivered。正文取自总管 transcript L4392，9263 B，sha256 912bad06152be74b805de164aec054bd149dfc67a3201fb1cb7925c68e66ef62。
    - 发送前 05:15Z 用 get_session 回读 QA 为 claude-opus-5-5 / max，符合规定。
    - 四项确认全部同意；QA 档位改为 max。
    - 另加核对项：policy 数量下限 40 与是否有凑数用例；EG.unit 的披露与 QA 独立运行；触发器探针；ponytail 默认处理核查。
    - 资源：只有 Host.integration 占窗口；门禁 base 为 f91d1dc，只跑一次。U3 基线 run 满足它的三项条件，唯一不同是 baseline 为 afb4f9b，由 QA 判断其影响。
  - 本次 Owner 输入（04:06:55Z）以来，总管已发 3 条跨会话消息。
- 2026-09-27T05:21:21Z P06 S2 授权（总管）：
  - 依据：B1 裁定（2026-09-26T17:36:02Z）写明“S2 仍须等 S1 冻结后另行授权”；S1 已于 05:15Z 冻结。
  - 签发 coordination/WO-P06-M1-SHADOW-RULINGS-2.md，sha256 29838d29b929be1b4d1cd5ce3c6f9a72974039cc2b599cc31fd63daa19c83743，10025 B。只授权 PREACCEPT，START 另发。
  - 要点：
    - 基线为 S1 冻结头 6cbf3e85，与 S1 QA 并行；S1 QA 若为 NOT PASS，S2 按 §4.3 重绑。
    - 范围：第 1 号裁定第 4 节的七项全部纳入。
    - D5：测试专用入口进程；D6：合成 runtime revision 作为配置输入；B1 约束照抄 §9.4；D2 由 Builder 在 PREACCEPT 中举证，总管在 START 中定案。
    - 写域另含 Dps.slnx（只登记新测试入口项目）和受影响的 packages.lock.json（只限项目条目）；policy-approval 的 migrations 禁止。
  - 换人：S1 Builder 进程带全局 max 覆盖项，达不到 medium，S2 由新 Builder 接任。任务卡 task_20613842，首轮只做档位自检，05:18Z 已发出，待 Owner 点击。QA 沿用 local_aa99a782（max），S2 的 QA START 在 S1 QA 出结论、S2 冻结之后下达。
  - 单元根 units/p06/builder-s2/，分支 codex/p06-s2-host-shadow-chain，端口 56340–56349。
  - 本次 Owner 输入（04:06:55Z）以来，总管已发 3 条跨会话消息。

- 2026-09-27T05:42:58Z 总管：改由总管在终端面板直接起 session（Owner 本段输入“肯定有新开session都方法”“我授权你可以开session”）。
  - 只读实测：应用内能直接开 session 的 start_session 工具受服务端功能开关（gate 2371478310）控制，本 session 未获授予；computer-use 的应用列表不含 Claude 应用本身，无法代点任务卡。
  - 采用方式：mcp__terminal__run_in_terminal 在终端面板启动 Claude CLI（2.1.282），命令为 `cd dps-air && env -u CLAUDE_CODE_EFFORT_LEVEL claude --model claude-opus-5-5 --effort <档位> --permission-mode bypassPermissions -n <名> --remote-control <名> "$(cat <首条指令文件>)"`。
    - 权限模式与总管相同。任务卡起的 session 实测也是 bypassPermissions（pid 6260/6332/13481/13579 的 argv）。
    - 先 env -u：总管自身进程的环境中仍有 CLAUDE_CODE_EFFORT_LEVEL=max（启动于 settings.json 删除该项之前），Bash 子进程会继承。
    - cwd 为 /Users/younghu/Documents/ZennoDroid_DSP/dps-air，不建 DSP_ZD worktree。
  - 已撤回任务卡 task_20613842、task_84a0c506、task_55949e2f、task_9709d249，防止 Owner 点击后重复开工。
  - 四个 session 的实测值（总管经 sysctl KERN_PROCARGS2 读 argv 与初始环境，并读 transcript）：
    - P06 S2 Builder：pid 18340，05:37:12Z 启动，终端 tab c1，transcript 093e2304…；medium，初始环境无 CLAUDE_CODE_EFFORT_LEVEL；首轮自检 05:37:21Z，perTurnEffort=medium。首条指令 launch-p06s2-b.md，sha256 前缀 7d674f89f14bbe6f。
    - U4-INT Builder v2：pid 20885，05:41:21Z 启动，tab c2，transcript 82e93833…；medium，环境无该变量；首轮 perTurnEffort=medium。首条指令即 v2 START（含档位自检与 PREACCEPT 绑定），launch-u4-b-v2.md，前缀 b687eaf184b888bd。
    - P18A-INT Builder：pid 20998，05:41:23Z 启动，tab c3，transcript fd2512e5…；medium，环境无该变量；首轮 perTurnEffort=medium。首条指令为档位自检加 PREACCEPT（只读，回执写 units/p18a-int/PREACCEPT-BUILDER.md），launch-p18a-b.md，前缀 a25a82a2d443cb28。
    - P18A-INT QA：pid 21071，05:41:26Z 启动，tab c4，transcript 16ba4173…；max，环境无该变量；首轮 perTurnEffort=max。首条指令为原卡 PREACCEPT，加 git 并联对象库的只读读法和通信说明，launch-p18a-qa.md，前缀 1f196d2aba16dd22。
  - 通信实测：CLI session 发往 local_cb8c68d0-… 失败（No agent named … is reachable），按名字“接任 DPS 总管（Orchestrator）”发送成功；总管用对方消息的 from 地址（uds:/tmp/cc-socks/<pid>.sock）回发。
  - 05:38:54Z 向 P06 S2 Builder 发 PREACCEPT，正文 3792 B，sha256 6d452748a76d8d1cc6e36858ceee1d4c0ac9a9eea45412e2be3adc7aaf59b7f7，msg_id adb1f88e…，状态 queued；其 transcript 随后持续增长，说明已接收。
  - 本段 Owner 输入以来，总管已发 1 条跨会话消息。

- 2026-09-27T05:44:23Z 总管更正：上条中 P06 S2 PREACCEPT 的实际发送时刻为 05:39:25Z，取自总管 transcript 第 4862 行；05:38:54Z 是正文内写的时间。正文 sha256 已按 transcript 中的 SendMessage 输入复算，与上条一致。

- 2026-09-27T05:54:19Z 总管：U4 v2 Builder 的 PREACCEPT 复核停点已裁定；P06 S2 Builder 的 PREACCEPT 已验收并下达 START。
  - U4 v2 Builder（pid 20885，transcript 82e93833…）05:50:52Z 停下回报：旧回执 76fc800a… §6 写“5 个 module.yaml 在 L、R4、R2 中为同一 blob”，与实测不符。
    - 总管只读复核：soul-registry/module.yaml 在 L 与 R4 为 34a37a61，在 R2 为 cf23eb51；control-plane-host/module.yaml 在 L 与 R4 为 6c75d924，在 R2 为 9e9f2d46。922db5b 上两者分别为 cf23eb51、9e9f2d46。922db5b..L 之间只有 5ab9aab 改过这两个文件；R2 相对 922db5b 在 *module.yaml 与 governance/** 下零改动。三方合并后 M2 取 L 的 blob，§8 的命令与 floor 不变。
    - 裁定：不阻断，准予开工。附加条件：REPORT 如实记录这处不一致；M1、M2 提交后分别核对这两个 blob 应为 34a37a61、6c75d924；档位自检中误读 fd2512e5… 的更正照实写入。
    - 消息 05:51:49Z 发出，正文 1349 B，sha256 5837476b24bff30badd604f9bedbcf228285038be75f8282c7080ad42f4a4cf8（总管 transcript 第 5073 行），状态 queued。05:52:27Z units/u4-int/builder/ 出现。
  - P06 S2 Builder 回执 units/p06/builder-s2/PREACCEPT-BUILDER.md：sha256 df4b5f8883b0f91f8fb51e2509e3e198dae38372536c58341394e3e58a4f93d1，26517 B，与自报一致。
    - 总管只读复核，在 C 上：
      - resolver AGENTS 第 25/31 行、planner AGENTS 第 31 行、Host AGENTS 第 13/27/29/34 行的原文与回执引用一致。
      - Host Manifest 没有 action.proposal 与 approval.evaluation.request 的声明和边；Host src 中 promotion 命中 0 次；soul-registry 没有指向 Host 的 soul.resolved 边。
      - phase0.py 的 relay 判定与回执描述一致；migration 005 已持久化 request_signature。
      - PDD P07 步骤 2 要求读取时绑定签名。approval.decision/v1 本身没有签名字段。
    - START 裁定：
      - D2 定为中继，采纳回执 §2.2 的建模；Planner 只改 communication，不升版。
      - 读取合同定案为 approval.readback/v1，domain 为 dps.policy-approval.approval-readback/v1，外层不签名；另加 request_signature 字段（major 2 必填，major 1 为 null，只取已持久化的列，不新增 migration）。
      - Q2–Q9 同意默认做法。Q3 附加：身份入参标注“只作 SHADOW 回显”；断言 HostStartup 仍只接受 --self-check；soul.resolved 绑定列为残留项。Q4 须写明“命令行数为 0”是结构性的。
      - 引用边界：只用 Planner/Policy 程序集的 public 类型，不加 IVT，不反射。
      - 停点：Host.integration 与 soul-registry.integration 需要 READY-FOR-PG-WINDOW；遇到停止条件立即回报。
    - START 于 05:53:52Z 发出，正文 4988 B，sha256 1c661899e23cf95cd1ec228ad44c1843dc44bb39aed17873458c2a02e5f27311（第 5120 行），状态 queued。
  - 本段 Owner 输入以来，总管已发 3 条跨会话消息。

- 2026-09-27T05:56:07Z 总管：P18A-INT Builder 的 PREACCEPT 已验收，START 已下达。
  - 回执 units/p18a-int/PREACCEPT-BUILDER.md：sha256 f36592d1b7c3a7acba5babda7f6b399562dede9f20a5452026b0ee526e86a0f7，20205 B，与自报一致。输入身份全部与工单 §1 相同；档位自检以本 session 的 transcript fd2512e5… 为准，均为 medium（首轮曾误读 82e93833…，已由其自行更正）。
  - 披露的越界写入：/tmp/_x 与 $TMPDIR/../mt.out 两个临时文件（git 路径清单与 merge-tree 输出，不含凭据），已删除。已要求照实写入 REPORT；此后所有命令都设 HOME 与 TMPDIR 到单元 environment。
  - 总管只读复核：history.git 中 refs/heads 为 0，HEAD 指向不存在的 main；refs/sources/project/heads/codex/p18a-ledger-pg-environment 就是 2aaaaa8。P17-INT 的 FREEZE.json 中，D′ 上 ledger unit/crash-recovery/contract 的 ran 为 11/7/7。
  - 裁定：
    - Q1：从镜像显式 fetch 该 refs/sources 引用。
    - Q2：只读复制 P17-INT 的 .sha256 副本。
    - Q3/Q7：三个非 PG suite 只在 M18 上跑，预期 11/7/7。
    - Q4：编排规则 §5 第 3 项已授权，无需 Owner 另行确认。
    - Q5：同意。
    - Q6：同意，附条件：预建角色只带 LOGIN 与口令；迁移前后各记录一次属性与权限。
    - Q8：记为 NOT_RUN。
    - Q9–Q11：同意。
  - 与 P17-INT QA 并行；若其结论为 NOT PASS 则重绑。
  - START 于 05:55:43Z 发出，正文 2846 B，sha256 3d69d0b2b05b258f6b7b828c0e52deeb964c4619a799469a2b48f5eada2f226c（总管 transcript 第 5183 行），状态 queued。
  - 本段 Owner 输入以来，总管已发 4 条跨会话消息。
  - 教训：多个 CLI session 共用同一个 cwd 时，“目录中最新的 .jsonl”不能用来定位本 session 的 transcript。两个 Builder 首轮都误读过对方的文件。之后启动时用 --session-id 预先指定 UUID，并在首条指令中写明本 session 的 transcript 文件名。

- 2026-09-27T05:59:10Z 总管：P06 S1 QA 的 PG 窗口与门禁位一并放行，按 Host.integration → 停 PG → 门禁串行执行。
  - 申请：05:58:23Z 收到 READY-FOR-PG-WINDOW 与 READY-FOR-GATE 的合并消息，3118 B，来自 local_aa99a782。
  - QA 自报的窗口外结果（数据，结论不继承）：
    - diff 与 FREEZE 一致，32 条路径都在写域内；治理诊断减少 4 条 action.proposal、新增 2 条 approval.evaluation.request；21 个非 PG suite 均 exit 0。
    - EG.unit 独立跑一次为 34/34（05:29:12Z），未复现开发期的 33/34。
    - policy-approval.integration 实跑 41 例（下限 40，净增的 9 个方法都在 S1 范围内）；CO.pg18 24/24，AM 8/8。
    - 第 5 条路径探针未发现缺陷；变异测试 7 个中杀死 6 个，存活的 1 个为等价变异。
    - 判断 --base 不影响已知 5 项检查，依据是 run_phase0_gate.py 的相关行。总管同意，沿用 U3 run 0f30d554… 作基线，不另批基线门禁位。
  - 放行前实测（05:58:37Z）：563xx 只有 56350（pid 16150，QA 自有）在监听；没有门禁进程，也没有 Soul/Host 集成测试在跑；10 核，负载 6.67/6.48/5.53。另有 U4 v2 Builder、P06 S2 Builder、P18A-INT Builder、P18A-INT QA 四个单元并行。
  - 要求：在 Host.integration 与门禁的开始和结束时各记录一次负载；结果与已知 5 项 FAIL 不同时不重跑，连同负载数据回报。
  - 放行消息 05:58:52Z 发出，delivered，正文 1538 B，sha256 ad43be04166ac5ef19d55f828b7cf888f6074807c119eac73ec81f80696f3eb3（总管 transcript 第 5229 行）。
  - 在该窗口与门禁结束之前，本机不再放行其他 Soul/Host 窗口和门禁位。
  - 本段 Owner 输入以来，总管已发 5 条跨会话消息。

- 2026-09-27T06:00:10Z 总管：监视器观测到 127.0.0.1:56350 于 05:59:58Z 关闭，即 P06 S1 QA 的 Host.integration 窗口已结束；它的门禁位仍在使用中。门禁结束之前，照 05:58:52Z 的承诺，不放行其他 Soul/Host 窗口。

- 2026-09-27T06:05:40Z 总管：U4 v2 Builder 申请 PG 窗口，已排队；P06 S2 Builder 的 NU1004 停点已裁定。
  - U4 v2 Builder 06:04:17Z 回报 READY-FOR-PG-WINDOW，申请 CPH.integration 与 Soul.integration，自有 PG 端口 56320。
    - 总管只读复核（06:04–06:05Z）：
      - M1 为 31977b2c3c0c43bc3add3ebfbd0338fd9b0087fe，tree 9427640c，父为 f91d1dc5、1f991fa4。
      - M2 为 ab0ef3c995a6b15ec626f1b81c1b3daa942531fe，tree cadddc96，父为 31977b2c、1536e384。
      - 分支 codex/int-p05r4-p10r2 与 HEAD 都等于 M2，tracked 改动为 0。
      - M1、M2 中 soul-registry/module.yaml 均为 34a37a61，control-plane-host/module.yaml 均为 6c75d924。
      - 统一 argv 下的 diff：L..M1 26040 B 8155241f；R4..M1 265648 B a17110ce；M1..M2 107021 B c4ec4512；R2..M2 1563571 B 051047a1。与自报一致。
    - 排队：P06 S1 QA 的门禁（pid 55921，06:00:54Z 启动）结束后再放行。依据是 05:58:52Z 对 S1 QA 的承诺，并避免门禁运行期间负载叠加。U4 已完成全部非 PG 工作，只是等待，不必另发通知。
  - P06 S2 Builder 06:04:25Z 停止回报：在 Host src 新增 RULINGS-2 §4 授权的 ProjectReference（Dps.PolicyApproval、Dps.Planner）后，locked restore 报 NU1004。日志 sha256 3c991d91…。它实测非 locked restore 只增加 3 个 Project 条目，包版本不变。
    - 裁定：一次性豁免 §6 的 NU1004 停止条件，仅限这次授权的 ProjectReference 变化。
      - 只准为 Host src、Host.Tests、SoulCurrentHostProcess、D5 新入口项目这 4 个项目重新生成锁文件。
      - diff 只准增加 Project 条目；已有 package 的 resolved 与 contentHash 逐字不变，不新增 package 条目。
      - 之后这 4 个项目与全部受影响的消费方各做一次 locked restore，都须 exit 0。其他锁文件出现变化，或有下载新包，就停下回报。
    - 裁定于 06:05:08Z 发出，正文 1315 B，sha256 07aef6fa0006a40d48c5e63b8f2b40c59165d3fbc5106f3e1089e7915e20271b（总管 transcript 第 5296 行），状态 queued。
  - 观测：P18A-INT Builder 的自有 PG 127.0.0.1:56300 于 06:04:45Z 开始监听，06:05:15Z 关闭。factory 套件不需要窗口。
  - 本段 Owner 输入以来，总管已发 6 条跨会话消息。

- 2026-09-27T06:08:29Z 总管：P18A-INT Builder 已冻结，总管只读复核一致。
  - 06:07:38Z 收到 FREEZE 回报。M18 = 199b2097257d88112091ad2dd381c90345eedc8a，tree cbd6a86fd896cb7d35eb918846acf1f4870c3947，父依次为 D′ 320ce778 与 2aaaaa8。分支 codex/p18a-int-320ce77。
  - 总管复核（06:07:56Z）：
    - 分支与 HEAD 都等于 M18，tracked 改动为 0。
    - D′..M18 共 13 个路径，全部为 M，+745/−38。
    - 统一 argv 下：D′..M18 为 61545 B，sha256 437a1811074b7834…；2aaaaa8..M18 为 1342703 B，c267aa889e69cca4…。
    - FREEZE.json 为 8aa1bac4f9619b45f992ebd5876e1fc6f3c63bf8167a14f6fe69fad08162aa02（13550 B），REPORT.md 为 5e72ee51ff84822d599b992a69c40a68cd3f15fbf3aa487167ebb12a326f101a（10892 B）。
    - trivial merge-tree 输出 59777 B，与 PREACCEPT 时的预演相同。
  - Builder 自报（数据，结论不继承）：
    - 中央 CI：D′ 为 Ran 243，全部 ok；M18 为 Ran 261，全部 ok；P18A 新增的 18 个全部 PASS。
    - ledger 三个非 PG suite 为 11/7/7。
    - M26 只跑一次：rc 1，Ran 5，F1/E3，与 P17-INT 逐条对应，没有新失败。
    - Q6 的前后快照为 s19/s21。泄漏扫描 199 个文件，命中 0。
  - 偏离与披露（交 QA 核查）：
    - PG 第一次编译没有启用 SSL，pgcrypto 因此失败；清空后改用本机 Homebrew openssl@3 重编，与 P17-INT 的 configure 相同，没有下载。
    - 两次只读 git status 没有经过包装脚本。
    - /private/tmp 下有两个 dps-phase0-executor-*，创建于 06:01Z 和 06:04:51Z，与 P06 S1 QA 的门禁（06:00:54Z 启动）时间吻合，不是本单元产生的。
  - 保留物：environment/secrets（0600，TEST_ONLY）、已停机的 pg/data、.venv，待统一清理。
  - P18A-INT QA 的 START 等它的 PREACCEPT 回执到了再发。其门禁需排在 P06 S1 QA 门禁之后。

- 2026-09-27T06:09:47Z 总管：P06 S1 QA 的门禁进程 pid 55921（06:00:54Z 启动）于 06:09:03Z 退出，由后台等待任务观测。06:09:23Z 实测：563xx 无监听；没有门禁或 Soul/Host 集成测试在跑；负载 9.55/8.52/7.67（10 核）。随即放行 U4 v2 Builder 的 Soul/Host 窗口：
  - 按其回执 §7 E 段在 56320 执行，先跑 CPH.integration（预期 66/66），再跑 Soul.integration（预期 107 项，其中 1 项为保留的 P11 FAIL，exit 2）。
  - 每个套件只跑一次，开始和结束时各记录一次负载；偏离就停下，不重跑。
  - 该窗口结束之前，不放行别的 Soul/Host 窗口。
  - 门禁位现已空闲，P06 S1 QA 正在写最终报告。
  - 2026-09-27T06:09:54Z 补记：U4 窗口放行消息于 06:09:37Z 发出，正文 1217 B，sha256 298a7415d7df1b6bff8d96b5a623ffc08efb6f659841882acb64949810420d57（总管 transcript 第 5395 行），状态 queued。本段 Owner 输入以来，总管已发 7 条跨会话消息。

- 2026-09-27T06:22:50Z 总管：P18A-INT QA 接收与 START；两处 F1 编排错误记账；P06 S2 第 3 号裁定；P06 S1 QA 结论 PARTIAL；P17-INT QA 已恢复。
  - P18A-INT QA 的 PREACCEPT 回执：units/p18a-int/PREACCEPT-QA.md，sha256 216e75d05c4b3d3a3bb1883183b53314a6431393e068313dcd4ea2296465b211（37625 B）。消息于 06:10:08Z 到达。总管已全文读取。回执接受 QA 角色，接收时刻按回执写成时间（约 06:07Z），不前溯。
  - F1（总管的编排错误，事实）：
    - P18A-INT：Builder START 于 05:55:43Z 发出，QA 接收约在 06:07Z。
    - P06 S2：Builder START 于 05:53:20Z 发出，S2 的 QA 接收至今尚未取得。
    - 两处都违反有效根 AGENTS 第 85、90 行和 PDD §3.1 第 2 步。
    - 处置：不回滚，不重做，接收时刻不前溯，由 QA 在报告“流程”一节照实记录。P06 S2 于 06:22:39Z 向 QA 发出接收请求（见下）。
  - P18A-INT QA START 于 06:16:39Z 发出（正文标注为 06:17Z），正文 5007 B，sha256 8a731898b41560aa7ccc17b2c50ca0e17766ccd71aaba12b849253130e8031ca（总管 transcript 第 5572 行），状态 queued。要点：
    - 冻结件复核：M18 199b2097…，两份 diff 的全长哈希为 437a1811074b78340dc7264aeb78823ca61bec76e861800f318d4a7fc5f44aa1 与 c267aa889e69cca46195f409fba36ab7ac08c2cbde70cfe631410a210b7d1c79（06:14:33Z 重算）。
    - Q1–Q13 全部采用回执的默认做法。
      - Q2：依据 Owner 01:55:42Z 授权原文（账本第 296 行）。只下载 .sha256；若 session 规则要求 Owner 当面确认，改用 P17-INT 的副本，记为证据限制。
      - Q5：preflight 若拒绝 passfile 参数，按停止条件处理。
    - 补充核查点：Builder M26 的口令传递方式（REPORT §5 未写明）；Builder 披露的各项偏离是否属实；HMAC key 的 Base64 与 hex 泄漏扫描。
    - 门禁位预分配给 P18A-INT QA：它在门禁步骤自查，没有 run_candidate_gate.py 或 run_phase0_gate.py 进程、且自有 PG 已停时，直接开跑。在它的门禁结束之前，总管不向其他单元放行门禁。
  - P17-INT QA：Owner 于 06:06:31Z 在其 session 中答复了 04:11:07Z 的下载询问，QA 随即恢复。其自有 PG 127.0.0.1:56370 于 06:14:58Z 开始监听、06:18:29Z 关闭（factory 套件，不需要窗口）。尚无结论。
  - U4 v2 Builder（transcript 数据）：CPH.integration 66/66，rc 0。Soul.integration 于 06:11:15Z 开跑。56320 于 06:13:58Z 关闭。窗口结果和 FREEZE 尚未回报；在它回报之前，Soul/Host 窗口仍视为由 U4 占用。
  - P06 S2 Builder 于 06:15:29Z 停下回报，B3 出现新增诊断。总管只读核对：
    - stops/b3-base-C.log 为 65ea4c65…d304，b3-candidate-wip.log 为 54f06760…a6dd。
    - 新增诊断恰为三类：A 类 2 条，即跨模块生产 ProjectReference，与 RULINGS-2 §4 冲突；B 类 7 条，即 readback schema 缺公共字段；C 类 3 条，即 authScope。
    - C 上 phase0.py 第 1567–1576 行的 production 扫描范围不含 tests/。
    - 签发第 3 号裁定：coordination/WO-P06-M1-SHADOW-RULINGS-3.md，sha256 d8d3ada2e10e8be66d0ac1b98bd858d25cc3260597e413293845732e48d41864（5054 B）。
      - A 类采用方案 1：Host src 只引用 contract pack，实现引用只在 D5 入口；生产侧逻辑留在 Host src。
      - B、C 两类按 Builder 的方案修，附条件。
      - 不采用规则豁免。
    - 裁定消息于 06:20:55Z 发出，正文 2874 B，sha256 436086570daba754aecd8dd5c63855d2080783deb047d93c1daf7ee243c41311（第 5684 行），状态 queued。
  - P06 S1 QA 最终报告：结论 PARTIAL（上限），候选 6cbf3e85 没有已证实缺陷，也没有退回项。
    - QA-REPORT.md 为 ffc6d5d4f1ee687a25ddcbffdc8d326ce6dc185b8cf448cf8608cbcc0aeb17fa（28556 B），EVIDENCE_INDEX.json 为 ef1bf1ffca66c11cebd89f3b717e55447ed932c5e44bc4c051209bb87c6b7719（29683 B），总管已复核哈希。
    - 门禁 run 28eea2bc…：53 项中 48 项 PASS、5 项 FAIL，与已知集合一致。module-governance 71→69：减少的 4 条是 action.proposal；新增的 2 条归 S2。
    - 消息于 06:20:59Z 发出，状态 undelivered。正文存档为 receipts/P06-S1-QA-session-FINAL.md，sha256 43f9716ff9275307d599c6f6487c7e11c9e2d18a01b738bb77838fcd675f6359。
    - 观察项 O1–O7 不阻断。其中 O5（v2 可以不经 fence 直接 begin）归 P07/P08。
    - 保留物留给 S2 QA 复用。
  - S2 START 原文存档为 receipts/P06-S2-BUILDER-orchestrator-START.md，sha256 1c661899…，与第 805 行的记录相同，供 S2 QA 绑定。
  - S2 QA 接收请求于 06:22:39Z 发往“P06 S1 QA：独立验收 Policy 侧 v2 影子闭环”（local_aa99a782，正文标注 06:24Z，比实际发送时间晚）。正文 3313 B，sha256 e12a392605dfefb4e159b45f14540bb40b68c3b2dcd6b896d7e23939733ff841（第 5723 行），状态 queued。回执写到 units/p06/qa/PREACCEPT-QA-S2.md。
  - 本段 Owner 输入以来，总管已发 10 条跨会话消息，达到平台上限（第 312 行）。在 Owner 下次于本会话输入之前，不再发消息，只读核验、记账。
  - 2026-09-27T06:24:19Z 补记：P06 S1 QA 的最终报告于 06:24:04Z 送达本会话（总管 transcript 第 5764 行），正文 5056 B，sha256 43f9716f…，与存档 receipts/P06-S1-QA-session-FINAL.md 相同。上条已处理完毕，不需另行回复。

- 2026-09-27T06:33Z 总管观测（只读）：
  - P18A-INT QA 的自有 PG 127.0.0.1:56310 于 06:28:01Z 开始监听，06:29:01Z 关闭（M26，factory 套件，不需要窗口）。
  - P06 S2 Builder 于 06:32:02Z 在 127.0.0.1:56340 启动自有 PG（Postgres.app 18，pid 59352）。
    - 其 transcript 记载：数据目录下的 socket 路径超过 103 字节，于是把 unix_socket_directories 改到 /tmp/p06s2pg（0700）。
    - 这个目录在单元根之外，也不在 ORCH §5 第 5 项的 /private/tmp/dps-* 范围内。按规则，这属于应当停下回报的事项。
    - 总管消息额度已用尽，未能提醒 Builder。已列为 S2 QA 核查项：REPORT 是否披露、事后是否清理。
    - 截至 06:32:10Z，进程表中没有 Host 或 Soul 的 integration 测试在跑。
  - 更正（2026-09-27T06:32:35Z）：上条标注的时间 06:33Z 是预写的，该条实际追加于 06:32:27Z。

- 2026-09-27T06:36:05Z 总管：U4 v2 Builder 已冻结，总管只读复核一致；P17-INT QA 报 READY-FOR-GATE；门禁与窗口排队。
  - U4 FREEZE 消息于 06:35:17Z 到达（总管 transcript 第 5882 行），正文 3878 B，sha256 e16fe677af69f9d9371f8df155cf1b94a89130808ecaaf0d653abe296675bf2d。
  - 总管复核（06:33–06:35Z）：
    - 分支 codex/int-p05r4-p10r2，HEAD 为 M2 ab0ef3c995a6b15ec626f1b81c1b3daa942531fe，tree cadddc96…，父依次为 M1 31977b2c…、R2 1536e384…。
    - M1 的 tree 为 9427640c…，父依次为 L f91d1dc、R4 1f991fa。
    - 工作树干净。
    - 四份 diff 在统一 argv 下复算，与 FREEZE 相同：L..M1 26040 B 8155241f…；R4..M1 265648 B a17110ce…；M1..M2 107021 B c4ec4512…；R2..M2 1563571 B 051047a1…。
    - FREEZE.json 为 f4b0b3845a679e4b7352a9a3530c49ad7af704b9e0a1fd8b9aa7c8df632a4c8c（60088 B）。
    - REPORT.md 为 780aae637e2cfefc32c69b7acdb4db96f4e6a7ede7dff79937f761bdc9e98f72（18596 B）。06:34:33Z 的初稿为 0ea66e24…；06:35:03Z 发 FREEZE 前补了 §11 的档位条数，并把 /private/tmp 计数更正为 56/49/23/26。
  - Builder 自报（数据）：
    - 18 个非 PG 套件等于预期；CPH.integration 66/66；Soul.integration 107 项，106 PASS、1 FAIL，唯一失败是保留的 P11 用例；共 933 项，932 PASS。
    - validate_repo 与 generate_governance 的输出与 L 逐字节相同。
    - 附加条件的两个 blob 在 M1、M2 上都满足。
    - 档位：228 条 assistant 记录全部为 medium。
  - Soul/Host 窗口：U4 于 06:13:53Z 停机，窗口从此空闲。
  - P17-INT QA READY-FOR-GATE：06:35:01Z 发出，状态 undelivered。
    - 正文 5489 B，sha256 a02cca405ed3d2b8c7d7e5f2457d5a8a9088d0db3da84d616d66a0b541e0e39b。
    - QA 自存档 units/p17-int/qa/DSP_ZD/Reports/ci/p17-int-qa/gate/pre/READY-FOR-GATE.md，总管复核为 bdc0e8b3…（5292 B）。
    - 候选 D′，门禁参数 --base afb4f9b。M12 PASS；M16 3F+5E、M26 1F+3E，在 afb4f9b 上签名相同，属既有失败。
    - QA 表示只在收到明确放行后开跑。
  - 门禁排队：P18A-INT QA（预分配；06:35:16Z 进程表中还没有门禁进程）→ P17-INT QA → U4 QA。
  - 待 Owner 在本会话输入、消息额度重置后发出：
    - U4 QA START（local_b4b8c271）；
    - 在 P18A-INT QA 的门禁结束后，给 P17-INT QA 放行门禁；
    - P06 S2 的窗口请求与 QA 回执的答复。

- 2026-09-27T06:36:32Z 总管：P06 S2 Builder 报 READY-FOR-PG-WINDOW。
  - 消息于 06:36:01Z 发出（总管 transcript 第 5909 行），正文 1474 B，sha256 4f18d727d5a4628fbd208bb1d33490cf7463d8a7841a3a290a837e2fcca7dfe6。
  - 总管只读复核（06:36:22Z）：分支 codex/p06-s2-host-shadow-chain，HEAD 3224cc26b338905adc6e9f9e29d1d4597bd86d16，tree 5f575d30f4c1ddde33498ee320da0c33483da803，唯一父为 C 6cbf3e85，工作树干净。
  - Builder 自报（数据）：
    - 已按 RULINGS-3 实施 A/B/C。B3 对 C：S1 留下的 2 条消失，action.proposal 诊断 0 条，新增 0。tests/ 下的实现引用没有被门禁拦截。
    - 自有实例 56340 上：policy integration 42/42（下限 40）；consumer 回归全部通过；locked 构建全部 exit 0，没有新下载。
    - 锁文件恰好 4 个变化，都只有 project 条目。
    - B 的六个字段取自 approval_decisions.decision_json，并与同名列精确绑定。
  - Soul/Host 窗口自 06:13:53Z 起空闲；U4 QA 的 START 尚未发出。S2 Builder 排在窗口的第一位。消息额度已尽，放行要等 Owner 在本会话输入之后才能发出。

- 2026-09-27T06:49:26Z 总管：P06 S2 QA 已接收（补救 F1）。
  - 回执：units/p06/qa/PREACCEPT-QA-S2.md，sha256 1304bcdc7ec0739ab0b2183823344815f3e974787b07914688950c1d15a48260（42820 B），mtime 06:48:15Z，总管已复核。
  - 消息于 06:48:45Z 到达（总管 transcript 第 5972 行），正文 3382 B，sha256 52f51038847f05db35f89e911b95fbd15e9df6cb2cf7660c3308e5954e19f7de。
  - 接收时刻为 06:48:15Z，不前溯。QA 自报：没有看 Builder 的 clone 或候选，档位为 max，共 879 条。
  - QA 在回执中登记了 Q1–Q9 的默认做法。主要风险是 M15：v1 decision 的时间允许 7 位小数，而 timestamptz 只有微秒。QA 计划用 G2 探针验证。
  - 已把 Builder 的 /tmp/p06s2pg 列为 I 类核查项。
  - 这些默认做法由总管在 S2 冻结后随 QA START 一并裁定。

- 2026-09-27T06:49:49Z 总管观测（只读）：P18A-INT QA 的门禁 run 目录 candidate-runs/integration/1a3da0f747125c17f38af177dfd23c20 建于 06:39Z，最后写入约 06:45Z（本机时区 14:39/14:45）。2026-09-27T06:49:49Z 时没有门禁进程在跑，门禁位空闲，下一位是 P17-INT QA。放行消息要等 Owner 在本会话输入、额度重置后才能发出。P18A-INT QA 于 06:49:09Z 开始写 QA-REPORT.md。

- 2026-09-27T06:53:27Z 总管：Owner 于本会话输入“卡住的项目继续。”（约 06:51Z），消息额度重置。随即发出三条消息：
  - 放行前实测（06:51:30Z）：没有门禁进程，也没有任何单元的 Host/Soul integration 在跑；563xx 只有 56340（P06 S2 Builder 自有）在监听；负载 4.84/5.19/5.54（10 核）。门禁与窗口同时放行，允许重叠，各单元照实记录负载。
  - P06 S2 Builder 的 Soul/Host 窗口放行：06:52:14Z 发出，正文 2279 B，sha256 104af07f43c87233399522c5c889b14b5f68f95dbda6dabd9502e89e7db34def（总管 transcript 第 6046 行）。
    - 附带要求：先停机，删除 /tmp/p06s2pg，改用单元根内的短路径重启，再跑 Host 与 Soul 的 integration；REPORT 如实披露。
    - 06:53:27Z 实测：/private/tmp/p06s2pg 已不存在；56340 于 06:52:25Z 关闭、06:52:40Z 重开；新进程 pid 99317 的 unix_socket_directories 在单元根内。
  - P17-INT QA 的门禁放行：06:52:21Z 发出，正文 1480 B，sha256 229387e3207860307bdd5853a487de5b7a2481d6ebc1459a01691dd78c095ff2（第 6048 行）。
  - U4 QA START：06:53:19Z 发往 local_b4b8c271，正文 8583 B，sha256 401ff31a68e7088243c2a3e944578c334125d3a001761a2939cdca3cf0f4a88d（第 6076 行）。
    - 内容：冻结物复算、U1–U11 裁定、v1→v2 角色变更的重新绑定（根 AGENTS L91）、Builder 档位核查改为读 transcript、排队状态。
    - 草稿存 scratchpad/U4-QA-START-send.md，比发出的正文多一个末尾换行。
  - 队列：
    - Soul/Host 窗口：P06 S2 Builder（进行中）→ U4 QA → P06 S2 QA。
    - 门禁：P17-INT QA（进行中）→ U4 QA → P06 S2 QA。
  - 本次 Owner 输入以来，总管已发 3 条跨会话消息。

- 2026-09-27T06:54:54Z 总管：P17-INT QA 的门禁放行消息（06:52:21Z，sha256 229387e3…）被对方会话扣留，须经 Owner 批准后才能送达。
  - 送达通知指向 uds:/tmp/cc-socks/59816.sock。经 KERN_PROCARGS2 只读核对，该进程即 P17-INT QA 的桌面 session，其 CLAUDE_CODE_SESSION_NAME 为“P17-INT QA：独立验收 P17 集成候选”，参数为 --permission-mode auto、--effort max。
  - 已提请 Owner 在该 session 中批准这条消息。在它送达并开跑之前，门禁位仍为 P17-INT QA 保留；目前也没有其他单元在排门禁。
  - 同时查得各 DPS session 的权限模式：
    - 用任务卡或 CLI 起的几个 session（P06 S1 Builder/QA、U4 v1 Builder、U4 QA、四个 CLI session）为 bypassPermissions；
    - U3、P05 R4、P17-INT、P10 R2 的 Builder/QA，以及总管本身为 auto。
  - 以后给 auto 模式的 session 发消息，可能同样需要 Owner 批准。

- 2026-09-27T06:57:39Z 总管：P06 S2 Builder 窗口内停下回报，已裁定允许用 socket 重跑一次。
  - 停下回报：06:56:31Z 发出（总管 transcript 第 6144 行），正文 2367 B，sha256 a4eae65011d38335b653aa60d21ffb35f654bb110953f70ccc29b795edb8fec1。
    - 越界处置已完成：06:52:20Z 停机，06:52:28Z 执行 rmdir /tmp/p06s2pg，该目录的使用时段约为 06:32:02Z–06:52:20Z；改用 environment/s 重启。
    - control-plane-host.integration：73/73，exit 0（06:52:40Z–06:53:12Z）。
    - soul-registry.integration：exit 2，共 99 项，4 项失败（06:53:19Z–06:55:43Z）。其中 1 项是保留的 P11 用例；另 3 项是 PostgresSoulIdentityAuthorityIntegrationTests 的 ack-loss 用例，报 EndOfStream，原因是 DSN 走了 TCP。
    - PG 于 06:56:19Z 停机。
  - 总管只读核对：
    - PostgresSoulIdentityAuthorityIntegrationTests.cs 约第 399–400 行，SocketPath 直接取 DSN 的 Host；Host 与 Soul 的 tests/README 明写 ack-loss proxy 须用 Unix socket。
    - S2 对 C 的 diff 中，soul-registry 下没有任何改动。
    - U3 QA 报告第 174 行：f91d1dc 上 soul-registry.integration 走 socket，99 项，98 PASS、1 FAIL（P11），exit 2。
  - 裁定：06:57:29Z 发出（第 6164 行），正文 2040 B，sha256 554095b41b6a4a722c2d04a0c801901adacd77fd9be04feb9e62117e13f0fa4b。
    - 定性为配置错误，不是候选回归。允许重跑一次，DSN 只改 Host 为 socket 目录。
    - 第一次运行的日志原样保留，并如实披露。
    - 重跑前做一次残留查询。
    - 预期 99/98/1、exit 2；出现任何偏离即停，不再重跑。
    - Host integration 不重跑。
  - 06:57:26Z 起出现 units/u4-int/qa/，说明 U4 QA 的 START 已送达并已开工。
  - 本次 Owner 输入以来，总管已发 4 条跨会话消息。

- 2026-09-27T06:58:56Z 总管：P18A-INT QA 结论 PARTIAL；P06 S2 第二次配置错误，已准予最后一次运行。
  - P18A-INT QA 最终报告：06:57:48Z 发出（总管 transcript 第 6186 行），正文 4844 B，sha256 92cd4c53f7dbd03057dffaaa89a7b357c9cfbbe054b88a599c652f434924b87a。
    - QA-REPORT.md 为 192f57dc8e3e97f30a441fa63843d4ddb3594a48c27eb1cf77cf0d34f903888f（32066 B），EVIDENCE_INDEX.json 为 ef6da208238e9e5ca06f97e07b1247357bb7e6e66ed35d904f0086e95b00c4b6（15024 B），总管已复核哈希。
    - 结论 PARTIAL，没有 P0/P1 级候选缺陷。
    - 门禁 run 1a3da0f7…，06:39:37Z–06:45:28Z，rc 1。Phase0 为 48/5，恰为既有的 5 项；module-governance 与 D′ 逐字相同。QA 自检通过后直接开跑，按 START §4 的预分配执行，没有发 READY-FOR-GATE。
    - M26 仍为 F1/E3，与 P17-INT、Builder 三方逐条相同。中央 CI：D′ 243 项、M18 261 项全部通过，P18A 新增的 18 个方法全部执行并通过。
    - QA-F2（过程发现，不是候选缺陷）：Builder 的 environment/bin/pg_env.py（6a4f3286…）第 7 行把 TEST_ONLY 明文口令拼进三个 DPS_FACTORY_EVIDENCE_POSTGRES_*_URI，使口令在 M26 运行期间进入进程环境和 psql argv；Builder REPORT 没有披露。总管已复核该文件哈希与所引行。另一处小偏离：epoch 固定为 1，而 WO §3.1 要求本机随机生成。
    - 总管对 QA-F2 的处置：
      - 该实例已停机，这些口令只用于一次性测试实例，视为作废，不再复用。清理时删除 Builder 的 secrets 与 pg/data。
      - 冻结件不改，Builder REPORT 不补写，以 QA 报告和本账本的记录为准。
      - 后续工单一律规定：口令只经 0600 passfile 传递，不得进入 URI、argv 或环境变量；QA 的泄漏检查要覆盖进程层面，不只扫文件。
    - 剩余阻断与归属见其报告 §5：M26 E3/F1 归 M26 修复单元；Phase0 既有 5 项；C02/C03 归 P18 步骤 1、3；错误版本负控 NOT_RUN；外审 NOT_RUN。
    - P17-INT QA 尚未出结论。若日后判为 NOT PASS，M18 按 PDD §4.3 重绑。
    - 保留物等统一清理。
  - P06 S2 第二次停下回报：06:58:17Z 发出（第 6214 行），正文 1876 B，sha256 bdbb44a3931ac50b832862b8799557f26b287a3cb889d203d22a93898f0c2363。
    - socket 重跑于 06:57:46Z–06:57:51Z，99 项中 97 项失败，均为 SCRAM 认证无口令。原因是 pgpass 的 host 字段只写了 127.0.0.1。
    - 前后两次残留查询都干净。06:58:05Z 停机。
  - 裁定：06:58:48Z 发出（第 6218 行），正文 1884 B，sha256 f0e49696af74c24bd4184e43f96334d1af1819b267fb46b3c321f2b29431d34e。
    - 准予最后一次运行：pgpass 的 host 改为 `*`（U4 先例）或另加一行匹配 socket 的条目；开跑前先做连接自检，读 server_version_num，应为 180004。
    - 预期 99/98/1、exit 2。
    - 再有偏离就停下，由总管决定是否按“soul-registry.integration 自测未完成”冻结。
    - 三次运行都要如实记录。
  - 本次 Owner 输入以来，总管已发 5 条跨会话消息。

- 2026-09-27T07:09:09Z 总管：P06 S2 Builder 已冻结，总管复核；新建修复单元 FIX-M16-M26 并开出两个 session；S2 QA START 已发出。
  - S2 Builder FREEZE 消息：07:05:31Z 发出（总管 transcript 第 6382 行），正文 2132 B，sha256 453f72fc179bc73553fe7250e7eab99be19679b4d68dd5452487a5a84ad0e885。
    - 结论 PARTIAL，正式等级 NONE，外审 NOT_RUN。
    - PG 于 07:01:41Z 停机，Soul/Host 窗口自此空闲（监视器 07:01:42Z 观测到 56340 监听关闭）。
  - 总管 07:05:43Z 只读复核，与 Builder 自报一致：
    - S = 3224cc26b338905adc6e9f9e29d1d4597bd86d16，tree 5f575d30f4c1ddde33498ee320da0c33483da803，唯一父为 C 6cbf3e85。工作树干净，分支 codex/p06-s2-host-shadow-chain。
    - C..S 统一 argv：206637 B，sha256 ff4364dde83c47b8afed4d6f04fa0792ea96872e9c45e98e099c922057bcaa0a，共 41 条路径（10 A / 31 M）。
    - FREEZE.json 为 7a77813296c10834082d3e7e64eb35ed3376a2c1f707a0805fa60a9e58bd5ef3（49755 B），REPORT.md 为 37b44dcf4d2df2c5efce2c0448ea03e0a5a1a9fcea6080f623da75e65fc7d781（19862 B）。
  - 工单 WO-FIX-M16-M26.md：
    - 修复单元 FIX-M16-M26，负责 factory-evidence-ledger 的 M26（E3/F1）和 factory-artifact-builder 的 M16。
    - 最终版 sha256 bc1b03c5ad786b7eb39bdcc888988016004e2af1f59999606ed92ba17063669c（15282 B）。分发前把 E3 的行号更正为第 486 行，这是唯一的改动。
    - 基线 M18 = 199b2097257d88112091ad2dd381c90345eedc8a。
    - 端口：Builder 56400–56409，QA 56410–56419。
  - 两个 session 由总管在终端面板用 CLI 启动（Owner 已授权），都以 `env -u CLAUDE_CODE_EFFORT_LEVEL` 清掉该环境变量；工作目录为 dps-air/。

    | 角色 | session id | pid | 终端 tab | 档位与权限 | 启动提示词（总管 scratchpad） |
    |---|---|---|---|---|---|
    | FIX Builder | d0319e34-42f0-4616-adf0-ebe585704147 | 18385 | c5 | claude-opus-5-5 / medium，bypassPermissions | launch-fix-b.md，5204 B，0e50357950c67ad1f55d43478163b6bd538c59892def4bd0eb59d6d01a9cc69f |
    | FIX QA | 2f17cb5f-ba5f-414a-9666-95222c0b7ef3 | 18485 | c6 | claude-opus-5-5 / max，bypassPermissions | launch-fix-qa.md，4823 B，0ce8edd59d465da0750fffb0c8f95281a2aad79ff0db49e87523fc8649ccc94b |

    - 总管已从 KERN_PROCARGS2 只读核对两个进程：argv 与上表一致，初始环境中没有 CLAUDE_CODE_EFFORT_LEVEL。
    - 本轮只做档位自检和 PREACCEPT，写入仅限 units/fix-m16m26/ 下各自的回执。两份回执都到齐、经总管核对后，才发 Builder START（根 AGENTS L85/L90）。
    - 07:08Z 该目录尚不存在。
  - S2 QA START：07:06:37Z 发给“P06 S1 QA：独立验收 Policy 侧 v2 影子闭环”（local_aa99a782，transcript 第 6408 行），正文 4230 B，sha256 c2c181d921c3395c292998922f4f530aecb74bf87f4f91394061b2fa2fbc916e。正文把发送时刻写成 07:07Z，以实际发出的 07:06:37Z 为准。
    - 内容：冻结物；Q1–Q9 裁定（Q2 新建 pg-s2；Q3 不在 C 上另跑，预期 99/98/1，出处 U3 QA 第 174 行；Q4 最多 6 个变异；Q9 时间事实照实记录）。
    - 补充核查六项：/tmp/p06s2pg、三次运行的披露、tree 相同的证据、锁文件、ponytail，以及 RULINGS-3 的 A/B/C 与 M15 精度风险。
  - 排队（07:09Z）：
    - Soul/Host 窗口空闲。队列为 U4 QA（尚未报 READY）→ S2 QA。
    - 门禁：P17-INT QA 06:52Z 已放行，但消息因该 session 处于 auto 模式，仍待 Owner 批准送达；其 gate/ 目录的 mtime 仍停在 06:32Z。之后依次为 U4 QA、S2 QA。
  - 本次 Owner 输入以来，总管已发 6 条跨会话消息。

- 2026-09-27T07:12:33Z 总管：U4-INT QA 报 READY，Soul/Host 窗口已放行给它；其门禁排在 P17-INT QA 之后。
  - U4 QA 的 READY-FOR-PG-WINDOW + READY-FOR-GATE：07:09:44Z 发出（总管 transcript 第 6523 行），正文 6375 B，sha256 a80b0347c027749cec3afa004493318628382baf2650ec3c2282077e9f5a2c6a。
    - 自报 S0–S6 完成、无偏离：
      - 档位：本 session 136 条全部为 max；Builder v2 的 235 条全部为 medium。
      - M1、M2 身份与 4 份 diff 等于 FREEZE；非 CHANGELOG 路径三方复算 1205/1205；静态检查与 L 逐字节相同。
      - 9 个构建全部 exit 0；18 个非 PG 套件等于 U8。
      - U9 不提请加跑。
    - 需总管知悉的事实：
      - F-A：Builder 06:13:52Z 用 `grep -rlF "$pw"` 扫描，测试口令因此进入 grep 进程（pid 22514）的 argv；06:31:15Z 它又用 `ps` 把这条 argv 打进了自己的 transcript（82e93833… 第 725 行）。REPORT §6、§8.3 未披露。
      - F-B：QA 只读核验时，工具输出原样显示了这一行，因此 QA 自己的 transcript（dce22e0c… 第 826 行）中也有一份副本。
      - F-C：Builder 在单元根外、它自己的 session scratchpad 里写过文件，与 REPORT §7 和 05:50:52Z 停点消息的表述不符。
    - 总管只读复核 F-C：`/private/tmp/claude-501/-Users-younghu-Documents-ZennoDroid-DSP-dps-air/82e93833-dcff-493a-b947-19092a2c4b8a/scratchpad/` 下共 26 项，最早一项 g.sh 写于 05:45:57Z，与 QA 所述一致。F-A 未复核：复核需要显示口令行，总管不做。
  - 窗口放行：07:11:00Z 发出（第 6548 行），正文 2372 B，sha256 b583c74b6a9936af8095b4c4df211c85866240f1423b4107f96ade12b0f770b8，发往 uds:/tmp/cc-socks/13580.sock（U4 QA，local_b4b8c271）。
    - 总管 07:10:43Z 实测 56300–56499 上没有监听。
    - 开跑前要求两件事：pgpass 的 host 也写 `*`（或另加一行匹配 socket 的条目）；TCP 与 socket 各做一次连接自检，读 server_version_num，应为 180004。
    - 期望值按 U8：CPH 66/66；Soul 107 项，106 PASS、1 FAIL（仅 P11），exit 2。任何不等都停下，不重跑。停机后发 WINDOW-DONE。
    - 门禁：排在 P17-INT QA 之后；P17-INT QA 的门禁结束前不放行 U4 QA。
    - 处置：
      - F-A、F-B 按 QA-F2 先例：口令作废，收口时删除 Builder 的 environment/secrets 与 pg/data；两份 transcript 不改；不是候选缺陷，记为过程发现。
      - F-C 请 QA 在报告中判定是否属于披露失实，scratchpad 在收口时清理。
  - 07:12Z，P17-INT QA 的门禁放行消息（06:52:21Z，sha256 229387e3…）仍未出现在对方 transcript 中，即仍待 Owner 批准。
  - 监视器改为 v6.1（总管 scratchpad 下 watch_units.py）：
    - 监听范围扩到 56300–56499，并监视 PREACCEPT-*.md 回执。
    - 收件方匹配补上总管的 socket 地址 65249.sock。改前的 v5 漏报了 U4 QA 发往该地址的 READY 消息；那条消息实际已直达总管，没有遗漏处理。
    - task b4tln234z。
  - FIX 两个 session 的档位（总管 07:12Z 从 transcript 读取）：Builder 89 条 assistant 记录全部为 claude-opus-5-5 / medium；QA 89 条全部为 max。07:12:29Z 起出现 units/fix-m16m26/。
  - 本次 Owner 输入以来，总管已发 7 条跨会话消息。

- 2026-09-27T07:18:00Z 总管：FIX Builder PREACCEPT 已收、已核实；U4 QA 窗口结束；总管的第 3 处编排错误（端口越出 Owner 授权）。
  - FIX Builder PREACCEPT：
    - 回执文件 units/fix-m16m26/PREACCEPT-BUILDER.md 07:14:59Z 落盘，sha256 7adb1e0c6bc9151b0f9f362fcef1b78ed0fc143bf852842e334a3bd802240c72（33155 B）。
    - 消息 07:15:18Z 发出（总管 transcript 第 6728 行），正文 2869 B，sha256 cefd091a342a80d3ea03117c28e163fd531a739b7f6a050cc251288f356b2440。
    - 档位 medium，环境变量未设置。结论为有条件接受，列出 3 处与工单不一致、3 项请求裁定；没有自行默认。
  - 总管 07:15–07:17Z 在 M18 上只读核实，全部属实：
    - ① 002 第 189 行的表约束 upgrade_event_projected_json_ck 同样要求 `jsonb_object_key_count(event_json) = 21`。E3 的根因是两处字面量，工单只写了第 486 行。
    - ② M16 的差异包括 PG 18 为 NOT NULL 登记的 contype='n' 行（依据 P17-INT QA 日志的 “10 additional elements”）。
    - ③ 工单 §1 称“两个目标模块的子树在 afb4f9b、D′、M18 上都相同”，对 ledger 不成立：afb4f9b 与 D′ 上为 0fd9870c，M18 上为 bfa7a6b0，D′..M18 改了 3 个文件，+93/−16。迁移与 src 的 blob 相同，“缺陷早于 P17/P18A”的结论不变。**这是工单的事实错误，由总管更正，工单文本不改。**
    - Q1 的依据：`_BUILDER_VERSION = "0.2.1"`（artifact_builder.py:78），版本等值断言（test_artifact_builder.py:227），以及 release-controller 在 src:169、test:131、ops README:24 三处钉死 `dps:factory-artifact-builder:0.2.1`；builder 的 CHANGELOG 顶部为 “0.2.1 - Proposed”。全仓没有代码引用 ledger 的版本号。
    - Q3 的依据：ORCHESTRATION-SESSIONS.md（63260fe8…）§5 第 4 项的 Owner 授权原文是“端口在 563xx”。
  - **总管编排错误（F3）**：工单 WO-FIX-M16-M26 给本单元指派的端口 56400–56419 越出了 Owner 授权。Builder 在 PREACCEPT 中发现，此时任何一方都还没有使用这些端口。
    - 更正：Builder 改用 56300–56309，QA 改用 56310–56319。这两段原属 P18A-INT，该单元已结束；U4 复用 U3 端口段是先例。
    - 总管 07:16:40Z 实测：56300–56319 没有监听，也没有 P18A-INT 或 P05 R4 的 postgres 进程。
    - 更正写进 START，工单文本不改。
  - U4 QA WINDOW-DONE：07:17:08Z 发出（第 6763 行），正文 1271 B，sha256 c3ac9749f138d0d7a7e4afd456ac124131608f369de8637ac5b32393d71b5371。
    - CPH.integration 走 TCP，66/66，exit 0。
    - Soul.integration 走 socket，107 项中 106 PASS、1 FAIL（仅保留的 P11 用例），exit 2。两项都等于 U8。
    - 开跑前连接自检两路都读到 180004。PG 于 07:15:58Z 停机；监视器 07:16:00Z 观测到 56330 关闭。
    - 口令文件与 pgpass 已于 07:16:09Z 删除；泄漏扫描用 `-F -f` 读模式文件，命中 0。
    - Soul/Host 窗口自 07:15:58Z 起空闲，下一位是 P06 S2 QA，尚未报 READY。
    - U4 QA 在等门禁，排在 P17-INT QA 之后。
  - P17-INT QA 仍是门禁队列的唯一堵点：它的 transcript（keen-ramanujan-a37439 / d90755d4…）自 06:35:13Z 起没有新记录，总管 06:52:21Z 的放行消息仍在等 Owner 批准。
  - FIX QA PREACCEPT 尚未到。Builder START 草稿已备好，两份回执都到齐后再发。
  - 本次 Owner 输入以来，总管已发 7 条跨会话消息。
  - 更正（07:18:40Z）：上条把端口越权记为“F3”，编号不对。此前的总管编排错误只有 F1 一类，共两次（第 889 行起），本次应编为 F2。“第 3 处编排错误”按次数计，这一点无误。
  - 上行括号中的 07:18:40Z 是预写的时间，date -u 实测的写入时刻为 07:18:32Z。

- 2026-09-27T07:21:02Z 总管：P17-INT QA 的门禁放行消息早已过期；更正此前三条账本；门禁位改给 U4 QA。
  - 事实：
    - 总管 06:52:21Z 发给 uds:/tmp/cc-socks/59816.sock（P17-INT QA，auto 模式）的门禁放行消息（sha256 229387e3…），因为没有在期限内获得 Owner 批准，于 06:57:22Z 过期，未送达。
    - 平台的过期通知当时以 queue-operation 形式记入总管 transcript（第 6161 行），但到 07:19:46Z（第 6864 行）才出现在总管的对话中。
    - P17-INT QA 的 transcript 自 06:35:16Z 起没有新记录，candidate-runs/integration/ 目录不存在，门禁从未启动。
  - **更正**：
    - 本账本 07:09:09Z、07:12:33Z、07:18:00Z 三条中“仍待 Owner 批准送达”“仍在等 Owner 批准”的说法有误。自 06:57:22Z 起，这条消息已经过期，门禁位实际上是空的。
    - 总管 07:11:00Z 发给 U4 QA 的放行消息里的同类说法也有误。
    - 总管上一轮给 Owner 的汇报请 Owner 去批准这条消息，同样有误；下一轮汇报时更正。
  - 影响：门禁位从 06:57:22Z 到 07:20:19Z 闲置。U4 QA 最早要到 07:15:58Z 窗口结束后才能开跑门禁，所以实际损失约 4 分钟。
  - 门禁位放行给 U4 QA：07:20:19Z 发出（总管 transcript 第 6872 行），正文 1115 B，sha256 22a58c9d76c2995bf87f3802edbb2da17dd34e31978e4e89e41a15a96c38dd90，发往 uds:/tmp/cc-socks/13580.sock。
    - 放行前 07:20:10Z 实测：没有 run_candidate_gate 或 run_phase0_gate 进程；/private/tmp/dps-phase0-executor-* 为 0 个；56300–56499 上没有监听。
    - 只跑一次，base 为 f91d1dc，与 U3 run 0f30d554… 逐项对照。U4 QA 的最终报告即作为门禁结束的信号。
  - 新的门禁队列：U4 QA（进行中）→ P06 S2 QA → P17-INT QA。
    - P17-INT QA 处于 auto 模式，发给它的消息约 5 分钟内必须获得 Owner 批准，否则过期。总管将在 Owner 在场时重新放行，或由 Owner 决定是否改变该 session 的权限模式；总管不代为更改。
  - 改进：监视器 v6.2 增加一项，扫描总管自己 transcript 中的 “[Cross-session delivery notice]” 记录，出现即报。
  - 本次 Owner 输入以来，总管已发 8 条跨会话消息。

- 2026-09-27T07:22:32Z 总管：独立性疏漏（事实）。
  - 本账本 07:18:00Z 条概述了 FIX Builder PREACCEPT 的具体发现：002:189、M16 的 NOT NULL 行、子树不同、版本钉死。
  - 而 FIX QA 的启动提示允许它读账本尾部，同时禁止它读 Builder 回执。账本因此成了 Builder 发现流向 QA 的通道。
  - FIX QA 的 transcript（2f17cb5f…）显示：07:21:11Z 它写下“回执内容已齐”，07:21:24Z 才去读新增的账本条目。它的独立分析在接触这些内容之前已经完成。
  - 处置：QA 回执到后，总管核对它读账本之后是否改动了回执；QA START 时要求 QA 如实说明。
  - 今后，一方的回执发现在另一方独立回执落盘之前，只在账本中记哈希与“已核实”，不写具体内容。

- 2026-09-27T07:42:10Z 总管：U4 QA 门禁已结束；P06 S2 QA 报 READY，窗口已放行给它，门禁位附条件预分配。
  - U4 QA 门禁：run 目录 candidate-runs/integration/f29d25838e95b006afa209cf3451a64e/，publication.json 于 07:27:18Z 写成，status 为 ABORTED（与各先例一样，是 Phase0 累计前置失败）。总管 07:40:49Z 实测没有门禁进程。U4 QA 的终报尚未到。
  - P06 S2 QA READY（窗口与门禁合并）：07:40:08Z 用 mcp__ccd_session_mgmt__send_message 发出（总管 transcript 第 7004 行），正文 3264 B，sha256 09ffd97c37f1f7e50a0febbf1d69302e58b1308c2404db24c075e67aeb365085。
    - 在 S 上自报：24 个非 PG 套件全部 exit 0；在自有 PG 56350 上，policy-approval.integration 42/42、CO.postgresql18 24/24、AM.integration 8/8；19 个项目 locked restore 加 Release 构建均为 0 警告 0 错误，无 NU1004；治理诊断 69→67，新增 0；窗口外 5 个变异全部杀死；S1 探针 P1–P5 复跑一致。
    - 预告一个缺陷（G2）：occurred_at 带第 7 位小数时，Policy 写入成功，但 valid_until 被 Npgsql 截断到微秒，而 EvaluateAsync 返回的快照仍保留 7 位，D5 的精确读回会在写入之后才失败。Policy 层测 9 例，其中 7 例两边不等。窗口内在链路层确认。
    - 另记两项发现：/private/tmp 下有 59 个 dps-p05-* 目录，出生于 06:57:50–51Z，正落在 S2 Builder 第 2 次 soul 运行的时段内，Builder 未披露；Host.Tests 新增了对 D5 的 ProjectReference（ReferenceOutputAssembly=false），同样未披露。
  - 窗口放行与门禁预分配：07:41:10Z 发出（第 7024 行），正文 1948 B，sha256 bb0fa31c8c44b9fe5ba76052dc3247dd63d419f112d8097842a8af3d7bfac71c。
    - 放行前 07:40:49Z 实测：56300–56499 上只有 S2 QA 自己的 56350（pid 56879），没有门禁进程，/private/tmp/dps-phase0-executor-* 为 0 个。
    - 窗口：同意 S2 QA 的 ①–⑤；④ 的 2 个变异计入 Q4 的“窗口内 6 个”。7 位小数缺陷在终报中给出根因位置（S2 新增代码还是 C/S1 既有代码，附 file:line），并按 Q7 退回 Builder。
    - 门禁：按 P18A-INT QA 的先例附条件预分配。窗口结束、PG 停机后自检四项：无门禁进程；/private/tmp/dps-phase0-executor-* 为 0 个；56350 不再监听；clone 内没有未跟踪的 AGENTS.md 或 module.yaml 副本。四项都满足就直接开跑一次；不满足则每约 3 分钟复查，60 分钟后仍不满足就停下回报。
    - S2 QA 07:41:13Z 读到放行，07:41:23Z 开跑窗口内的串行脚本。
  - 门禁队列：S2 QA（预分配）→ P17-INT QA。P17-INT QA 需要 Owner 在场批准，才能收到放行消息。
  - 监视器 v6.3：收件检测补上 mcp__ccd_session_mgmt__send_message（按 session_id 匹配）。改前的 v6.2 漏报了 S2 QA 的 READY，但那条消息已直达总管，没有遗漏处理。task b3d63wawd。
  - FIX QA PREACCEPT 仍未落盘；它的 transcript 显示，07:39:44Z 已取写入时间，即将写入。
  - 本次 Owner 输入以来，总管已发 9 条跨会话消息，剩下 1 条留给 FIX Builder START。

- 2026-09-27T07:47:41Z 总管：Owner 07:43:26Z 输入“停住了还是？”，消息额度重置；FIX QA PREACCEPT 已到，Builder START 已发出；P17-INT QA 门禁放行已重发，待 Owner 批准。
  - FIX QA PREACCEPT：
    - 回执文件 units/fix-m16m26/PREACCEPT-QA.md，07:43:53Z 首次落盘，发送前自查更正至 07:45:24Z 定稿；定稿 sha256 8d9515196154dc291695777d7b7d095c36878ec364d69ba4ea2cdf4264451b44（51525 B，586 行），总管已复核。
    - 消息 07:45:46Z 发出（总管 transcript 第 7168 行），正文 2760 B，sha256 bd2899c79226189d57967d6662b3562bb9e53dcf8b7b341291dc16f20c177eae。
    - 档位：218 条全部为 max；环境变量未设置。结论为有条件接收，待裁项为 C1–C5。
    - 回执 §1.3 回应了账本 07:22:32Z 条，给出时间线：子树不同（07:07:40Z）、002:189（07:09:26Z）、NOT NULL 行（07:10:51Z）、版本钉死（07:13:47Z）都早于它 07:21:24Z 读到账本；端口一项不算独立发现，改为引用账本。
    - 事实核对：它的 U2(a) 称 002:182–186 的 `IF NOT EXISTS` 守卫会使原地修改只对新库生效。**这一点有误。** 002:159–164 在守卫之前无条件执行 `DROP CONSTRAINT IF EXISTS upgrade_event_projected_json_ck`，所以重跑修改后的 002 会先删掉旧约束，再以 20 重建。总管已在 M18 上核对原文；QA START 时指出。
    - 它的第 5 项另有一条对 Builder 有用的事实：中央 CI 的 test_candidate_gate.py 在第 1850–1925 行一带按名字加载 ledger 的 PG 测试模块，并 patch `PostgreSQL18IntegrationTests._apply_migrations`。总管已核对，写进了 START。
  - FIX Builder START：07:47:30Z 发出（第 7223 行），正文 5902 B，sha256 8cf77a0a5d6b85fc245ef129bd134e2efe6a8174a1f0da8fef3d8d965e67a86c，发往 uds:/tmp/cc-socks/18385.sock。两份回执都到齐后才发（根 AGENTS L85/L90）。
    - Q1：artifact-builder 不升版，保持 0.2.1，在现有 “0.2.1 - Proposed” 条目下追加说明；ledger 升到 0.2.1。
    - Q2：方案 A，002 原地把 189、486 两处的 21 改为 20，不新增 003；002 其余字节不变；CHANGELOG 写明旧库重跑即可收敛，存量行键数不等于 20 时停下；operations/README 不改。
    - Q3：端口改为 Builder 56300–56309、QA 56310–56319；总管在 START 中承认 564xx 是工单错误（F2）。
    - 批准一次 E3 回归的修复前运行（仅测试改动、新空库、只跑新方法）。
    - 另加两条停止条件：中央 CI 有任何新增失败；修复后首次执行到的路径出现任何新失败。
  - P17-INT QA 门禁放行（重发）：07:45:42Z 发出（第 7157 行），正文 1629 B，sha256 412efd910a1841d79598d8cbc177771ad162063e5a96ed668de675957608f0ee。
    - 07:45:42.774Z 平台通知：该消息已被扣下，等待 Owner 批准。总管已在本轮末请 Owner 去批准。
    - 条件：没有门禁进程、没有 executor 目录，两者在相隔至少 3 分钟的两次检查中都成立；它自己的 PG 已停；clone 干净；门禁前快照重取；等待上限 90 分钟。
  - P06 S2 QA：窗口 07:41:23Z–07:44:43Z 完成，PG 于 07:44:43.84Z 停机，监视器 07:44:54Z 观测到 56350 关闭。随后它按预分配做门禁前自检 a–d。
  - U4 QA：QA-REPORT.md 07:43:38Z 写成，EVIDENCE_INDEX.json 07:46:09Z 写成，终报消息尚未到。
  - 监视器 v6.4：v6.3 的投递通知检测会把总管自己 task-notification 中引用的通知文字当成新通知，形成回环（07:46:09Z 出现两条假事件）。v6.4 只认以 “[Cross-session delivery notice]” 开头的 queue-operation 记录。task b5tofewtz。
  - 07:44:54Z 出现一个新的桌面 session（worktree message-quota-issue-8edfcf），不是总管开的，总管不与它交互。
  - 本次 Owner 输入（07:43:26Z）以来，总管已发 2 条跨会话消息。

- 2026-09-27T07:49:05Z 总管：U4-INT QA 结论 PARTIAL（最终报告已复核）。
  - 最终报告消息：07:48:15Z 发出（总管 transcript 第 7248 行），正文 8199 B，sha256 c640e749d43fc2d21be3841ec5c8b2400697b8c112e35fe8a8144c221a97e106。
  - 交付物（总管复算一致）：
    - QA-REPORT.md：caf8daeddc6c45a11b5630d03f284427a10f5c3146f46113370f8fb880ae08aa（38259 B）；
    - EVIDENCE_INDEX.json：43cac851f1a947822a2b1f5b3b6e06c439d4fafc4ad821087c7e71679c925f37（25640 B）；
    - 以上位于 units/u4-int/qa/DSP_ZD/Reports/ci/u4-int-qa/。另有消息存档 FINAL-MESSAGE.md，f33c8ac5…，不在索引内。
  - 结论 PARTIAL：
    - 身份、合并复算（M1、M2 各 1205/1205）、CHANGELOG、静态检查均通过；
    - 9 个构建全部为 0/0；20 个套件共 933 项，932 PASS，唯一 FAIL 是保留的 P11 用例；
    - 语义审查没有发现交互问题；
    - 门禁 pid 41676，07:22:08Z–07:27:18Z，rc 1，ABORTED。Phase0 为 48/53，与 U3 run 0f30d554… 逐项相同；module-governance 日志逐字节相同（ea8e4fc4…）。
    - 外审 NOT_RUN，正式等级 NONE。
  - 过程发现：
    - F-A：Builder 测试口令进入 grep 的 argv 与 Builder transcript，披露不完整。
    - F-B：QA 的 transcript 中有一份该口令的副本。
    - F-C：Builder REPORT §7 与停点消息的表述，同它在 scratchpad 中写入的 26 项不符，判为披露失实；候选不受影响。
    - 处置沿用 07:11Z 放行消息中的说法：口令作废，收口时删除 secrets 与 pg，scratchpad 一并清理。
  - 阻断归属：
    - #1–#7 沿用既有口径。
    - #8（policy-approval.integration 执行 32 例，低于 floor 40）已于账本 03:37Z 条裁定归 P06。P06 线上 S2 QA 在 S 上实测 42/42，满足 floor；U4 线在 U5 并入 P06 之后才会满足，届时由 U5 QA 核对。无需新裁，也不回复 U4 QA。
  - U4 单元到此闭环。U4 Builder v2（pid 20885）与 U4 QA（local_b4b8c271）都已完成，保留物列入统一清理。
  - 下一步：U5 汇合 U4 M2 与 P06 S2 的定稿。须等 S2 QA 终报出来：若确认 7 位小数缺陷，先做 S2 修复轮，再开 U5。

- 2026-09-27T07:51:02Z 总管：P17-INT QA 门禁放行第二次过期；FIX Builder 已按更正后的端口起 PG；启动 P18 步骤 1 的只读调研。
  - 07:45:42Z 重发给 P17-INT QA 的门禁放行（sha256 412efd91…），于 07:50:42.774Z 过期，未送达（平台通知，监视器 07:50:44Z 报出）。这是第二次。
    - P17-INT QA 仍停在 06:35Z，没有收到任何放行。
    - 做法：等 Owner 下次输入时，把重发作为该轮的第一个动作，立即交给 Owner 批准；或者由 Owner 决定把该 session 改为 bypass。总管不代改权限，也不绕过审批。
    - 在此之前，门禁位在 S2 QA 用完之后空着，P17 排第一。
  - FIX Builder：07:50:14Z 起 127.0.0.1:56300 开始监听，用的是 START Q3 更正后的端口段。
  - P18 步骤 1 调研：总管 07:50Z 派出一个后台只读调研 agent（Explore）。
    - 在 M18 上用 git plumbing 只读，不写任何文件。
    - 查明以下事项：run_candidate_gate.py 中 InstructionResolver 的 trust_authority 接线；Host/Runner/Worktree 对 instruction.receipt 与 upgrade.intent 的 major 声明；“34 个写目标对 32 上限”的出处；消费方闭包；相关套件；兼容性规则；最小改动面与需要裁定的事项。
    - 结果只作开单依据，不构成任何验证等级。
  - 本次 Owner 输入（07:43:26Z）以来，总管已发 2 条跨会话消息。

- 2026-09-27T07:57:15Z 总管：FIX Builder 因中央 CI 出现 6 个 ERROR 停下；判为环境配置错误，已批准在 clone 内建 .venv 后重跑一次。
  - 停下回报：07:56:30Z 发出（总管 transcript 第 7359 行），正文 2962 B，sha256 2f4c97482544ba8a8e45ec42d5a2b95231c4a4ad1c60171b4b92156e75c5e34e。
    - 中央 CI Ran 261，errors=6，rc=1，日志 b30（14b88de6…）。
    - 6 个 ERROR 都是 `test_locked_third_party_imports_originate_in_repository_venv` 的子测试，对应 attrs、jsonschema、psycopg、referencing、rpds、yaml。原因是依赖来自 environment/venv，而不是 `ROOT/.venv`。
    - 此前各步都与预期一致（Builder 自报，只作数据）：
      - 修复前 M16 为 F3 E5，M26 为 F1 E3；
      - memberships 为 []；`true || ':' || false` 输出 `true:false`；
      - 约束实测 20 行，其中 6 行 char(n) CHECK 文本改变，另有 10 行 NOT NULL；索引、触发器与列的目录都和常量一致；
      - E3 回归修复前运行 Ran 1，E1，失败点为 20 键写入被报 shape invalid；
      - 修复后 M16 Ran 8 OK，M26 Ran 6 OK；16 个非 PG 套件的数量等于 M18；
      - 002 与 M18 只差 2 个字节；工作树 diff 6608246e…，共 7 个文件，尚未提交。
    - PG 于 07:56:15Z 停止（监视器同一时刻观测到 56300 关闭）。
    - 另有一次配置错误：建库循环因 zsh 分词失败，没有建出任何库，已记入 b05。
  - 总管在 M18 上核实：Tests/ci/test_candidate_gate.py:374 要求依赖来源位于 `(ROOT / ".venv").resolve()` 之下；.gitignore:38 为 `.venv/`。判为 CONFIG_ERROR，不是候选回归。
    - START 按 Builder 回执 §6 批准了 environment/venv，这是总管的疏漏：P18A-INT QA 与 U4 QA 都以 clone 内 `.venv` 为先例。
  - 裁定：07:57:06Z 发出（第 7380 行），正文 1431 B，sha256 b30e1ddc9144faa5b6b73774283d7e45377d7ef1f9b9d95fa8b6a77bbbc6e076。
    - 在 clone 内建 `.venv`，用 `--require-hashes` 安装，并用 check-ignore 确认被忽略。
    - 用 `.venv/bin/python` 把中央 CI 重跑一次，预期 Ran 261 全部 ok；再有失败就停下。
    - 已跑的 PG 与非 PG 套件不重跑；静态检查用同一个 python。
    - b30 日志保留，REPORT 如实写明两次运行。
  - 本次 Owner 输入（07:43:26Z）以来，总管已发 3 条跨会话消息。

- 2026-09-27T08:01:57Z 总管：FIX-M16-M26 Builder 已冻结，总管复核一致；FIX QA START 已发出。
  - FREEZE 消息：08:00:26Z 发出（总管 transcript 第 7438 行），正文 2529 B，sha256 31540858a43ac683e298610bcb6767ba7d546e43620746214be2cfcdf32ca9a6。
  - 总管 08:00:50Z 只读复核，与 Builder 自报一致：
    - F = 6ba8ca001e011c2c9b3be87cd52fed114909e653，tree 3906854b7155582de7b986ae81715fc649a8320b，唯一父为 M18。分支 codex/fix-m16m26-199b209，工作树干净，`.venv` 已被忽略。
    - M18..F 统一 argv：13754 B，sha256 6608246e885a82ebf67b811114d54f992359f0ae43be7566adebc813b2bc0923，共 7 条路径，全部为 M。
    - 002 的 diff 只有 189、486 两行，都是 21→20；ledger module.yaml 由 0.2.0 升到 0.2.1；ledger CHANGELOG 已写入“重跑 001、002 即可收敛，存量行键数不等于 20 时停下，不得放宽约束”。
    - 冻结件：FREEZE.json dff363db…（7190 B），REPORT.md d6071961…（9536 B），EVIDENCE_SHA256SUMS.txt 8c68b077…（59 个文件）。
  - FIX QA START：08:01:48Z 发出（第 7491 行），正文 5765 B，sha256 fb8311948cce77cab53f976c8491c219a5fd2e900c5dab45b437c40d6e192d43，发往 uds:/tmp/cc-socks/18485.sock。
    - C1：QA 端口 56310–56319。
    - C2：方案 A。指出 QA 回执 U2(a) 有误：002:159–164 会先无条件 DROP 约束。负控中加一条升级路径检查：旧 002 → 新 002 之后应收敛到 20，并能写入 20 键事件。
    - C3：期望 20 行，含 10 行 contype 'n'；查询与 payload 都不变。
    - C4：builder 保持 0.2.1，ledger 升到 0.2.1。
    - C5：(a) 批准只统计 Builder transcript 的档位字段计数；(b) 泄漏检查只做结构扫描，不读 Builder 的 secrets 文件，不使用任何口令值，只报计数与位置。
    - 门禁：先报 READY-FOR-GATE 再等待，P17-INT QA 排在前面。中央 CI 与门禁都用 clone 内的 `.venv`。
    - 补充核查：两次配置错误（b05、b30）、ponytail、单元根外的写入（含 Builder 的 session scratchpad）。
  - 门禁位：S2 QA 的门禁已结束（run 8cd52e3c…，总管 07:57:52Z 实测没有门禁进程），现在空着。队列为 P17-INT QA（等 Owner 在场时重发）→ FIX QA。
  - 本次 Owner 输入（07:43:26Z）以来，总管已发 4 条跨会话消息。

- 2026-09-27T08:09:27Z 总管：P18 步骤 1 的只读调研已完成并存档；FIX QA 已按更正后的端口起 PG。
  - 存档：coordination/research/P18-STEP1-SCOPING-20260927.md，sha256 5726d24658219ec556d06735cb15dde566afb9f2c8464b051c187d6bd3ac3608（8144 B）。只作开单依据，不构成验证等级。
  - 总管在 M18 上抽查 6 处，全部属实：门禁 2048、1982；Resolver 构造函数 2584–2594；v2 的 maxItems 32；34 个模块；Resolver ops README:13 与 Impact ops README:15。
  - 要点：
    - 门禁在 Factory 绑定这一步退出 1。原因有四：没传 trust_authority；送的是 v1 intent；34 个写目标超过 v2 上限 32；调用签名漂移。
    - 仓库里没有生产级的 trust authority，跨进程信任是 WAITING_EXTERNAL。
    - Worktree、Runner、Host 仍把 v1 声明为 active，共 12 处与 owner 不匹配。
    - Impact ops README:15 禁止下游在可移植信任冻结并经独立审计之前消费 plan v2。
  - 初步定性（按 PDD §4.3 第 180 行“信任/签名/人工批准缺失”一行）：
    - 依赖可移植信任的部分保持阻断，归 P03 / H3，等载体具体之后再提请。
    - 不依赖的部分可以先做：34 对 32 采用方案 (a)，由门禁只把 diff 实际写到的模块作为写目标；修正签名漂移；缺少 authority 时明确 fail-closed。
    - P18 的进入条件按字面不满足：三个 v2 合同在 M18 上仍为 proposed，receipt v2 与 plan v2 缺少 corpus。开单前须先弄清由哪个包冻结。
  - 因此 FIX 冻结之后不直接开 P18 步骤 1；先完成进入条件的核查，再定开单范围。
  - FIX QA：08:09:04Z 起 127.0.0.1:56310 开始监听，用的是 START C1 更正后的端口段。

- 2026-09-27T08:26:04Z 总管：P06 S2 QA 终报 NOT PASS；grep 包装函数的系统性问题；P18 进入条件的核查结论；S2 返工单元开单并启动两个 session。
  - S2 QA 终报：08:14:24Z 发出，状态 undelivered，总管从其 transcript 读取（6962 B，sha256 4b5d6286f7385e424f92540c67a0abaa40f6b986997a88d8b4924bd2b7666339）。
    - 总管复算：QA-REPORT.md 为 d240f8daeafc6c3e3edc9af271bca591bddbd0c9e8cb9b7d68dd7c0fa20f1ea4（47864 B）；EVIDENCE_INDEX.json 为 2ed2a5bdf25dfa770925337abe3dc0ffbbd41b51345c1500d6137a7230af1004（37192 B）。与消息一致。
    - 结论 NOT PASS，依据缺陷 D-1：`occurred_at` 带第 7 位小数时，Policy 已提交 4 行，但 Inserted 分支返回的是内存中的 7 位 `valid_until`，持久化列是截断后的 µs 值；D5 的逐字节比对在提交后失败，首次调用 exit 1，重放 exit 0。
    - 总管在 S 上只读核对：PostgresPolicyApprovalService.cs 的 Inserted 分支用 `approvalValidUntil` 构造快照返回；DuplicateNoOp 分支用 `ReadSnapshotAsync` 读持久化行。属实。
    - 门禁 run 8cd52e3c…：Phase0 仍为既有 5 项 FAIL，与 S1 逐项相同；module-governance 69→67，新增 0。
    - 发现 F-1（MU7 存活）、F-2（D5 不校验格式合法的 SoulResolved 的来源）、F-3（Host.Tests 对 D5 的引用未披露）、F-4（59 个 dps-p05-* 目录），另有观察 8 项。
  - grep 包装函数（S2 QA 的 K-1）：总管在本机 shell 快照第 276–287 行核实，session shell 中的 `grep` 是函数，实际调用 ugrep，带 `--ignore-files --hidden -I --exclude-dir=.git` 等参数，会跳过 gitignore 列出的路径、`.git` 目录和二进制文件。
    - 影响：此前各单元报告中凡是用 shell 的 `grep` 做的口令扫描，覆盖面都不完整。哪些单元受影响，总管没有逐个核对。
    - 此后开单一律要求用 `/usr/bin/grep`。在途的 FIX QA 将在门禁放行消息中一并告知。
  - P18 进入条件的核查：
    - M18 上 34 个 Manifest 的 provided 合同中，70 项 active 全部为 `status: proposed`，因此不能用 status 字段判断是否“冻结”。
    - PDD §3.1 的 P04 一行把交接物定义为“唯一owner已冻结、经相应检查的接口/Schema/canonicalizer设计与消费者迁移顺序”；P04 第 5 步要求每个合同由唯一 owner 冻结 DTO/Schema/corpus/canonicalizer。
    - M18 上 Factory 三个 owner 合同中，只有 upgrade.intent v2 有 corpus；instruction.receipt v2 与 module.change.plan v2 都没有。此前的 P04 修复集成只涉及产品模块（WO-U3-INT 第 17 行）。
    - 结论：P18 的进入条件要由一个 P04 的 Factory 切片来满足，内容为两个 owner 冻结 receipt v2 与 plan v2、给出消费者迁移顺序、定下 34 对 32 的处理。P18 步骤 1 暂不开单。
  - S2 返工单元 P06-S2-R2 开单：coordination/WO-P06-S2-R2.md，sha256 1f9f89467a5805785be5c529bd7847301d21b0ac98f5cc215caf3bd229b89612（19200 B）。裁定要点：
    - D-1：Inserted 分支改为在同一事务、提交之前用 `ReadSnapshotAsync` 读持久化行后返回。不采用只在内存中截 µs 的做法，也不改请求合同。回归须能在 S 上失败，时间用确定性注入。
    - F-1：补负例，MU7 变异必须被检出。
    - F-2：D5 不加来源校验代码，只如实改写 REPORT 与 tests README；“soul.resolved 绑定”仍是残留项（账本 05:54:19Z 条，Q3 附加）。
    - F-3：接受，要求披露并给出 B4 编译边界的证据。
    - O-2：Host 不补 action.proposal v1 的声明。C 上 Host 本来就没有任何 action.proposal 声明；若声明 quarantine-only，就等于声称 Host 有 v1 的隔离与审计解码能力，而它并没有。改为要求引用或补一条“Host 中继入口拒绝 v1 且零写入”的负例。
    - 版本号不变；口令扫描用 `/usr/bin/grep`；F-4 由总管另行安排清理。
  - 两个新 session 由总管在终端面板用 CLI 启动（tab c7、c8），档位已核实：
    - Builder：pid 49953，session e33d3e51-413b-4e40-98ff-a3664eb6500b。KERN_PROCARGS2 读到 argv 为 `--model claude-opus-5-5 --effort medium --permission-mode bypassPermissions`，初始环境中没有 CLAUDE_CODE_EFFORT_LEVEL。08:25Z 其 transcript 有 30 条 assistant 记录，全部为 claude-opus-5-5 / medium。首条指令 sha256 8610ae60fc1e05f2a056bc019d592bab91ae301f12fa8de97bb994d568367752（5967 B）。
    - QA：pid 50045，session 65fde4e3-8e68-45a0-b80f-de3b4852e7d0，argv 为 `--effort max`，环境中同样没有该变量。08:25Z 有 49 条记录，全部为 claude-opus-5-5 / max。首条指令 sha256 c483fbcd942763e5492eda2867783a3a9e9ea593b0713bbf241f20d4e5918651（5569 B）。
    - 本轮两者只做 PREACCEPT。端口：Builder 56340–56349，QA 56350–56359。原 S2 的 Builder 与 QA session 空闲，不再向它们发消息。
  - 门禁位队列：P17-INT QA（等 Owner 在场时重发放行）→ FIX QA → P06-S2-R2 QA。Soul/Host 窗口空闲。
  - 本次 Owner 输入（07:43:26Z）以来，总管已发 4 条跨会话消息。

- 2026-09-27T08:32:55Z 总管：Owner 统一规定所有 DPS 会话用 bypass；权限模式回读；P17-INT QA 门禁放行重发并已送达；发现 4 个 CLI session 离线。
  - Owner 于 08:27:53Z 在总管会话中输入（本次输入）。统一规则（Owner 在本会话指示）：
    - 所有 DPS 会话一律使用 bypass 权限模式，与总管一致，范围包括总管、各 Builder、各 QA，桌面会话和 CLI 会话都算。目的是跨会话消息不需要审批，也不会过期。
    - 新开的 Builder 和 QA 一律用终端面板 CLI 启动，并加 `--permission-mode bypassPermissions`。
    - 开会话前的检查从两项改为三项：model、effort、权限模式。
    - 发消息前先确认收件方是 bypass。不是就先请 Owner 去切，不发会被扣留的消息。
  - Owner 在本会话告知：已在侧边栏把 DPS 桌面会话都切到了 bypass。
  - 回读（只读，总管没有改任何会话的模式）：
    - 总管 local_cb8c68d0：get_session 读到 bypassPermissions。它的进程 argv 是 `--permission-mode auto`，说明 argv 只反映启动时的模式。
    - 桌面会话：get_session 与 list_sessions 只对总管自己开的会话返回 permissionMode；这些桌面会话都不是总管开的，所以当前模式都查不到。它们的 transcript 中也没有 permission-mode 记录。涉及的会话：P17-INT QA、P17-INT Builder、P06 S1/S2 QA、P06 S1 Builder、U4-INT QA、U4-INT Builder（桌面）、P10 R2 Builder/QA、P05 R4 Builder、P05 QA（R4）、U3 Builder/QA、DPS项目多智能体协同、消息额度用完问题，以及 09-05 的三个旧会话。
    - 其中 P17-INT QA 有旁证：08:31Z 的消息没有被扣留，它 08:31:50Z 起已开始处理。这与 bypass 一致，但不是对模式的直接读数。
    - CLI 会话（08:32:10Z 用 KERN_PROCARGS2 读 argv）：P06-S2R2 Builder（pid 49953）、P06-S2R2 QA（50045）、U4-INT Builder v2（20885）、P18A-INT Builder（20998）都是 `--permission-mode bypassPermissions`，环境中都没有 CLAUDE_CODE_EFFORT_LEVEL。
    - 离线的 CLI 会话：FIX-M16-M26 QA（原 pid 18485）、FIX-M16-M26 Builder（18385）、P06 S2 Builder（18340）、P18A-INT QA。ListAgents 显示为 “Remote Control · offline”，进程已不存在，它们所在的终端 tab 也已不在（现存 tab 只有 c2、c3、c7、c8）。
  - FIX-M16-M26 QA 在验收中途离线：
    - 其 transcript 的最后一条带时间的记录是 08:21:21.062Z 的一个工具结果，随后是会话退出时写入的元数据（last-prompt、custom-title、permission-mode=bypassPermissions、bridge-session 等）。
    - 它的 PG（pid 36426，父进程为 1）仍在 127.0.0.1:56310 监听。
    - 离线原因总管没有查明。总管在 08:21Z 前后没有操作任何终端 tab。
  - P17-INT QA 门禁放行重发：
    - 放行前实测（08:30:47Z）：没有门禁进程，也没有 executor 目录；它的 transcript 自 06:35:16Z 起没有新记录，Reports 下没有 06:36Z 之后的新文件，判断它尚未开跑门禁；56370–56379 无监听；负载 4.45 / 4.48 / 4.86。
    - 消息：transcript 第 8178 行，08:31:10.751Z 发出，2127 B，sha256 ffa99adf7dcdeeaaf16fce64bb48a353ae1ce10456c4eaf6a2873a1f476a687c，msg_id 9cf5498f-89f5-40cf-ba0d-2fae6c8d17f7。内容沿用 06:52:21Z 的原消息（第 6048 行，1480 B，sha256 229387e3…），更新了时间与实测，写明“06:52 的放行因审批超时未送达，本条为重发”，另外补了一条：泄漏扫描用 `/usr/bin/grep`。
    - 结果：没有被扣留。P17-INT QA 于 08:31:50Z 起开始门禁前的自查。
    - 更正：该消息称“07:47Z 的第二次放行同样超时未送达”。第二次放行的实际发出时间是 07:45:42.243Z（第 7157 行，1629 B，sha256 412efd91…），07:50:42Z 过期。
  - 门禁位：现由 P17-INT QA 使用。队列之后是 FIX QA（离线，待恢复）→ P06-S2-R2 QA。
  - 本次 Owner 输入（08:27:53Z）以来，总管已发 1 条跨会话消息。

- 2026-09-27T08:34:51Z 总管：恢复 FIX-M16-M26 QA；收到 P06-S2-R2 Builder 的 PREACCEPT。
  - FIX QA 用 `claude --resume 2f17cb5f-ba5f-414a-9666-95222c0b7ef3` 在终端 tab c9 恢复（Owner 授权总管自行开 session；按新规则先核对三项）：
    - pid 64873。KERN_PROCARGS2 读到 argv 为 `--model claude-opus-5-5 --effort max --permission-mode bypassPermissions`，初始环境中没有 CLAUDE_CODE_EFFORT_LEVEL。
    - 恢复后 08:34:32Z 前的 6 条 assistant 记录全部为 claude-opus-5-5 / max。
    - 恢复通知（1694 B，sha256 27b529f68ac595495765405e0e6a6acde91922bde393c8759a1d902ddd6a0bba）作为启动参数传入，不占跨会话消息。要点：先做档位自检，只读核对现场（PG 36426 仍在 56310、Reports 下已有文件、中断前最后一个工具调用的日志），从中断处继续、不重跑已完成项，口令扫描用 `/usr/bin/grep`，门禁位现由 P17-INT QA 使用、做到门禁时先报 READY-FOR-GATE。
  - P06-S2-R2 Builder 的 PREACCEPT：08:34:09Z 送达。回执文件 units/p06/builder-s2r2/PREACCEPT-BUILDER.md，总管复算 sha256 c2fb920100f9d130831fb7b17774b8ef6ae3f3d4ef74c375d5390b813d01c80c（38918 B），与消息一致。内容待 QA 回执到齐后再核对与记账，Builder START 也等到那时再发。

- 2026-09-27T08:55:10Z 总管：FIX QA 门禁放行；P04 Factory 切片的调研存档。
  - FIX-M16-M26 QA 于 08:54:26Z 发 READY-FOR-GATE（发送方原文 3823 B，sha256 1db37dada10d32eabeb75aa9a994968a8a85d49527cd1ae6965061944abe3a3f）。要点：
    - 门禁前各项（第 1–5、7 项与 START §3 补充核查）都符合预期；
    - 恢复后没有重跑任何已完成项，中断前的 q61 日志完整；
    - 新实测：在 M18 上，20 个合同键加 1 个非合同成员的 21 键事件带合法 HMAC 时会被接受并落库；已存有此类行的库升级到 F 的 002 时整体失败并回滚；
    - 披露：PG 从 08:09Z 连续运行到 08:37Z，没有按回执在每个检查块后停机。
  - 放行前实测（08:54:46Z）：没有门禁进程，也没有 executor 目录；P17-INT QA 的门禁已结束（08:31:51Z–08:38:34Z，exit 1）；563xx 上没有监听；负载 4.24 / 4.05 / 4.33。
  - 放行消息：08:55:00Z 发出，1719 B，sha256 e2d963b00f39e9d6eb307f34034789025a47be4ed501acb2ffba8b4f97b07e89，收件方为 bypass（argv 已核实）。内容：只跑一次，与 P18A-INT QA run 1a3da0f7… 逐项比对；把 21 键新实测写成发现；P17-INT QA 的结论如果还没出来就照实写“未知”，不要等待。
  - P04 Factory 切片调研：08:53Z 交回，存档为 coordination/research/P04F-SCOPING-20260927.md，sha256 bec8a3e317b09485b49b0b1785354a9f7da8c5d9aeb9f917909acc2bd5cda054（7482 B）。只作开单依据，不构成验证等级。
    - 结论：三份 Factory v2 合同都达不到 PDD P04 第 5 步的 owner 冻结要求。receipt v2 与 plan v2 缺 corpus、schema 字节钉住和摘要向量；intent v2 的 corpus 只覆盖一个组件。
    - 这些都可以由 owner 模块在本地补齐，不需要可移植信任。消费者迁移与信任仍归 P18 与 P03/H3。
  - 本次 Owner 输入（08:27:53Z）以来，总管已发 2 条跨会话消息。

## 2026-09-27T09:01:18Z 总管

- P06-S2-R2 两份 PREACCEPT 到齐，总管复算哈希一致：
  - Builder：`units/p06/builder-s2r2/PREACCEPT-BUILDER.md`，c2fb920100f9d130831fb7b17774b8ef6ae3f3d4ef74c375d5390b813d01c80c（38918 B，mtime 08:33:48Z）；消息见 08:34:51Z 条。
  - QA：`units/p06/qa-s2r2/PREACCEPT-QA.md`，d5d7d4e9afc4164c5f4a78396bae2042b0deebcc4be21e211a55ef4a2c1d4e8d（51359 B，mtime 08:55:19Z）；消息 08:55:46.591Z 发出，1926 字符。
  - 进程复核（08:58:38Z，KERN_PROCARGS2）：Builder pid 49953 为 --model claude-opus-5-5 --effort medium --permission-mode bypassPermissions；QA pid 50045 为 --effort max，其余相同；FIX QA pid 64873 为 --effort max、bypassPermissions。三者 CLAUDE_CODE_EFFORT_LEVEL 均未设置。
  - QA 回执要点：D-1 根因在 S 上独立复核成立；唯一需修分支为 P:725–738（工单 §1.1 写 :726–737，差 1 行，结论不变）；同类路径只此一处；EXECUTE 的 fence 已用持久化值（静态证据，动态探针尽力而为）。
  - QA 回执第 6 节 14 项默认做法，由总管在 QA START 中逐项答复。其中第 8 项已核实：S 上 module.yaml 的 policy-approval.integration 命令为 `--minimum-expected-tests 32`（总管 08:58Z 读 S 上 blob）；工单第 142 行的“floor 40”指 candidate-test-policy 第 549 行的 floor（本账本第 52 行），不是 module.yaml。两个数出处不同，都成立；QA 的默认判定（不少于 S 上的 42 加新增数）同时满足两者，工单不改。
  - QA 回执 §3.4 的文件外观察，不入本单元：
    - O-a：P:629–635、:717–723 的有效期检查用内存 7 位上界，持久化值最多早不到 1 µs，方向 fail-closed。只记录。
    - O-b：D5 `Program.cs:167–170`、`:200–201` 与测试 helper `State()` 用内存中的 `UtcNow.AddHours(1)` 计算 state_sha256，`PostgresPolicyRuntimeStateSource.cs:176–180` 用持久化列重算；在时钟精度为 100 ns 的平台上两者可能不一致。本机时钟只到 µs，未实测。总管记为待开单候选，S2R2 收口后再定。
    - O-c：P:853–860 撤销在提交后才另开连接读取，读取失败时撤销已提交而调用方收到异常。与 D-1 无关，只记录。
- P06-S2-R2 Builder START：09:00:46.659Z 发往 uds:/tmp/cc-socks/49953.sock（本 transcript 第 8686 行），正文 2247 B，sha256 f7c3674cabf72b5e9fcbbc4f7eaf8e4956b055d0142c9d4f2ad19710f7a2516b，msg_id cce7dc7c-9f80-4c0d-bf87-1863ca5e1969，结果为 queued。内容：绑定两份回执；U1–U10 同意；U11 已写入 scratchpad 的 21 个文件保留，记为已披露偏离，由总管统一清理；撤销分支不改。
- P17-INT QA 终报：
  - 消息于 08:58:38.939Z 发出（其 transcript `d90755d4-….jsonl` 第 2295 行），5971 B，sha256 72b97d9292b852f89f822732dc054b65929af54cf2692fbc3f2ed467c2622ab9。发送结果为 queued，本条记录时尚未送达总管，总管从其 transcript 以数据方式读取。
  - 结论 PARTIAL（本单元上限），正式等级 NONE；候选 D′ 320ce77836edfcc13d2136ddb8cc76d4b0b0a566 未发现集成缺陷，没有新增失败。
  - `QA-REPORT.md` 8715950cc8caad099fa6840f872499a0ea413c4cdc1647877dd91768372f6ef6（35687 B），`EVIDENCE_INDEX.json` b637b2af27878fdfdeee374125dce5d216576c221b2df9c993cc17bb2d8013de（31790 B），总管复算一致。
  - 门禁 run 8ee07263067688c13b78e717ef28720b：08:31:51Z–08:38:34Z，exit 1；Phase0 53 项中 48 PASS、5 FAIL，与既有 5 项相同；module-governance 75 行，P17 没有引入条目。
  - 对下游：PARTIAL 不触发 §4.3 重绑。M18（P18A-INT，以 D′ 为第一父）、FIX-M16-M26、P04F 调研所用基线 M18 均不需重绑。
  - 自披露：M26 测试以 `psql -d <uri>` 调用，两次运行期间（约 2 s、0.3 s）TEST_ONLY 口令出现在 psql 的 argv 中；扫描证实证据、日志、transcript 0 命中。与 P18A-INT QA-F2 同类，记为过程建议：今后工单写明改用 PGPASSFILE。
  - TR AGENTS L22 与 item 4 不冲突；是否在文本上消除歧义归 Owner。
  - 保留物待总管统一清理：environment/secrets、已停机的 pg/data、PG 源码与安装目录、baseline clone、.venv、candidate-runs/、playwright 副产物、leak-scan-final/。
- 门禁位：FIX QA 门禁在跑（pid 5754，08:59:32Z 时已运行 1 分 26 秒）；队列为 FIX QA → P06-S2-R2 QA。Soul/Host 窗口空闲（08:59:32Z 时 563xx、564xx 无监听）。
- 发信计数：Owner 08:27:53Z 输入以来共 3 条（P17 门禁放行重发、FIX QA 门禁放行、本条 S2R2 Builder START）。

## 2026-09-27T09:10:59Z 总管

- 派发 P04F（P04 Factory owner 合同冻结切片）：工单 `coordination/WO-P04F.md`，sha256 c1d612b994245a8da9790f09d28ae710a2fc9b49c94f7344ca2ca4611c52cc49（21745 B）。
  - 开单依据：P18 进入条件（PDD:813）的两半在 M18 上都不满足；冻结属于 P04 第 5 步（PDD:378）。调研见 `coordination/research/P04F-SCOPING-20260927.md`（bec8a3e3…）。
  - 基线 M18 `199b2097257d88112091ad2dd381c90345eedc8a`（tree cbd6a86f…）。P17-INT QA 判 PARTIAL，M18 不需重绑（本账本 09:01:18Z 条）。
  - 范围：新增 receipt v2 与 plan v2 的 corpus；追加 Intake corpus 向量与 hash_domains；三个 owner 模块的 test_contracts.py 钉住 schema 字节、corpus、摘要向量与 canonicalizer 行为；修正 Resolver 测试夹具中与生产不一致的 claims 摘要；三个模块 tests README 与 CHANGELOG；技术书第 368、445、619 行中 Impact 的文字按 M18 Manifest 纠正。不改 src、schema 字节、Manifest、消费者与 Tools/ci。
  - 主要裁定：receipt 与 plan 现有的不带域摘要原样冻结。理由：compatibility-policy.yaml:5–6（`additiveWithinMajor`、`breakingChange: new-major`），加域属 breaking change，只能出 v3。
  - 总管开单时在 M18 上只读核对并修正了调研中的行号：receipt 的 `verified_baseline_contract_facts_sha256` 在 2991–2993、`diff_fingerprint` 在 3005、`receipt_id` 在 3036–3038；plan 的 `trusted_policy_*` 在 2110–2112；Intake 的 `public-contract-changes` 域在 748 行内联拼接，不经 `_domain_sha256`；Resolver `_canonical_bytes`（1080–1084）没有 `allow_nan=False`，解析时用 `_reject_json_float`（1090，调用于 825、1233）。
  - 本单元不需要 PostgreSQL，不分配端口。
- 启动 P04F 两个 session（终端面板 CLI，09:09Z 前后）：
  - Builder：pid 36185，tab c10，argv 为 --model claude-opus-5-5 --effort medium --permission-mode bypassPermissions --session-id 351dca17-ebfa-4fc5-a470-2fe9a06343d2 -n "P04F Builder"；首条指令 `launch-p04f-b.md`，sha256 54a47a059a00207ad44ca945f0dfe90b10207a5e7a7d921451dad6d504d57fee（5889 B）。
  - QA：pid 36398，tab c11，argv 为 --effort max，其余同上，--session-id 74a68ee3-d084-4a43-b92e-b7d7005a5fe0 -n "P04F QA"；首条指令 `launch-p04f-qa.md`，sha256 4758513a6e007ca7ee7bb737340df0727674aa1ec5e716beaebfe659ce703f7d（5319 B）。
  - 三项核对（Owner 08:27:53Z 规则）：09:10:14Z 用 KERN_PROCARGS2 读两者 argv，CLAUDE_CODE_EFFORT_LEVEL 均未设置；09:10:41Z 读 transcript，Builder 18 条 assistant 记录全部为 claude-opus-5-5 / perTurnEffort medium，QA 18 条全部为 claude-opus-5-5 / max，两者 permissionMode 均为 bypassPermissions。
- P06-S2-R2 Builder：09:07:22Z 起自有 PG 在 127.0.0.1:56340 监听（工单允许，Policy 的 PG 套件不需要窗口）。
- 发信计数：Owner 08:27:53Z 输入以来仍为 3 条；终端启动不经 SendMessage。

## 2026-09-27T09:12:56Z 总管

- FIX-M16-M26 QA 终报：消息 09:12:15.241Z 送达（2238 字符，发自 uds:/tmp/cc-socks/64873.sock，bypass）。
  - 结论 PARTIAL，正式等级 NONE，外审 NOT_RUN。第 1–7 项全部符合预期，Builder 没有把停点事项自行默认处理。
  - `QA-REPORT.md` c9462f4fa89e64af23312c8a91f7cd02560f786fc3deed1b47aa210c18b80499（38806 B，mtime 09:11:26Z），`EVIDENCE_INDEX.json` d6bf726335c6a2725a916a37f35c8b506053fa2807293c7269f15b35eb2ec3a5（16966 B，mtime 09:11:45Z），总管复算一致。
  - 门禁 run 8409d011176a023c19d3a9fb524fd332：08:58:06Z–09:04:21Z，rc 1；Phase0 53 项中 48 PASS、5 FAIL，与 P18A-INT QA 的 run 1a3da0f7 逐项相同；module-governance 门禁日志 75 行，与基线逐字相同，F 独有的行为 0。门禁位自 09:04:21Z 起空闲。
  - 发现 F-1（QA-REPORT §11）：M18 的 factory-evidence-ledger 迁移 002 在函数层与 CHECK 层都把键数写成 21（002:189、002:486），会接受并落库“20 个合同键加 1 个非合同成员”的事件，同时拒绝合法的 20 键事件。需要合法 append key，不是未授权写入。F 已在两层都关闭这个入口；已存有这类行的库执行 F 的 002 时整体失败并回滚，处置写在 ledger CHANGELOG 0.2.1。
  - 总管对 F-1 的处置：不另开单，不追加义务。F 已关闭入口；本线正式等级为 NONE，没有发布或激活任何构建。存量库核查只有在出现已部署的旧 002 库时才有意义，届时按 ledger CHANGELOG 0.2.1 办理。
  - 限制 L-1：没有任何一方对 FIX Builder 的 transcript 做过按值口令扫描。
  - 保留物待总管统一安排清理：secrets（5 个 0600 文件，含 needle 文件）、已停机的 pg/data（18 个库）、pg install 与 src、scratch clone 与 .venv、QA clone 的 .venv、candidate-runs、构建产物与缓存、environment 下的 home 与 tmp。
- FIX-M16-M26 单元到此收口：候选 F = 6ba8ca001e011c2c9b3be87cd52fed114909e653（唯一父为 M18），QA PARTIAL。之后与 P04F 的候选 X（同以 M18 为父，路径不相交）一起另开集成单元合流，作为 P18 的基线。
- P17-INT QA 终报的原消息于 09:11Z 前后送达本 session，内容与 09:01:18Z 条据其 transcript 所记相同（5971 B，sha256 72b97d92…）。不回复。
- 发信计数：Owner 08:27:53Z 输入以来仍为 3 条。

## 2026-09-27T09:14:23Z 总管

- P06-S2-R2 Builder READY-FOR-PG-WINDOW：消息 09:13:40.145Z 送达（1099 字符，bypass）。
  - 自报：S′ = 40e202eec7db37b1f5931d91c3c1c281ebedf47e；Policy 层 failing-before 在 T=3b98540e（S 加测试）上 4/4 失败，都失败在 wire 相等断言；S′ 上 27 个非窗口 suite 全部 exit 0（Host contract 27，+1；Policy integration 46，+4）；MU7 下只有新负例失败（26/27）；12 个项目 locked build 0 警告 0 错误；B4 成立；进程内治理诊断 S 与 S′ 各 67 条、逐字节相同。三次自己的调用错误未触及候选，原始日志保留，将在 REPORT 中披露。
  - 总管只读核对（09:13:52Z）：S′ 的 tree 为 d1e1be231467f028caf19423818c4647a3bf12a9，唯一父为 S；S..S′ 共 10 条路径，全部为 M，都在工单 §4.1 允许的范围内（根与两个模块的 CHANGELOG、两个 tests README、`PostgresPolicyApprovalService.cs`、Policy 两个集成测试文件、Host 的 contract 与 integration 测试文件）。回执写 8 条，实为 10 条，Builder 已自行更正，要求在 REPORT 中披露。
- 窗口放行：09:14:06.208Z 发往 uds:/tmp/cc-socks/49953.sock（本 transcript 第 8967 行），正文 1035 B，sha256 c39ace293436d3e6450776f0d5a2593cf65ba34e7dccf6caa5021fdb72f8c69c，msg_id fa77ae40-5721-4710-98f6-7b45b592ad37。
  - 放行前检查：窗口无其他占用者；门禁位空闲、无门禁进程；563xx/564xx 只有该 Builder 的 PG（pid 32333，56340）在监听。
  - 窗口内只跑三步：T 上链路层 failing-before 2 例；S′ 上 control-plane-host.integration，预期 75/75；停 PG。结果不符即停。窗口结束后不单独回报，在 FREEZE 消息中写明窗口起止、结果与停机时刻。
- 发信计数：Owner 08:27:53Z 输入以来共 4 条。

## 2026-09-27T09:20:53Z 总管

- P06-S2-R2 Builder FREEZE：消息 09:18:54.238Z 送达（2179 字符，bypass）。
  - S′ = 40e202eec7db37b1f5931d91c3c1c281ebedf47e，tree d1e1be231467f028caf19423818c4647a3bf12a9，唯一父为 S；分支 codex/p06-s2r2-3224cc2。
  - 总管复算（09:19:44Z）：统一 argv 的 S..S′ diff 29368 B，sha256 9f67a2cca2ed0374f678ee67ee4adcea4f116c448272133b9e7de90c85b674b9；Builder clone 的 HEAD 为 S′，porcelain 0 行；`FREEZE.json` 32996dee0178eba973f7bb865a31ab69b709b9c087efb28fe2517440bcea2706（48877 B），`REPORT.md` 83df397c67801ec552c668b8b3f10a98116f964571fe577fba12d47723a35845（19261 B）。均与 Builder 所报一致。
  - 窗口（Builder 自报）：09:14:12Z–09:14:22Z 在 T=3b98540e 上跑链路层 failing-before，2/2 失败在首次调用 exit 断言，输出 REJECTED:UnauthorizedAccessException，符合预期；09:14:29Z–09:14:58Z 在 S′ 上跑 control-plane-host.integration，75/75；09:15:07Z PG fast stop。监视器 09:15:09Z 观测到 56340 关闭。窗口自此空闲。
  - 其余自报：Policy 层 failing-before 4/4 失败；新增用例 Host contract +1（F-1）、Host integration +2、Policy integration +4；MU7 下 26/27；B4 成立；治理诊断 S 与 S′ 各 67 条；口令扫描 0 命中，阳性对照 2/2；224 条 assistant 记录全部为 claude-opus-5-5 / medium。
  - 披露：三次自己的调用错误（zsh 未分词导致 MSB1008；msbuild 未指定 target；validate_governance 先在 archive 树、后在无 .venv 的 S clone 上跑），各修正一次后重跑，没有停下回报，原始日志保留。这与 Builder START“偏离即停，不重跑”不一致，交 QA 用原始日志独立判断。单元根外写入：scratchpad（U11）、Claude 工具后台任务输出。保留物：停机后的 pgdata、pgsecret（0600），以及 fb、mu7、s 三个 scratch clone。
- P06-S2-R2 QA START：09:20:31.023Z 发往 uds:/tmp/cc-socks/50045.sock（本 transcript 第 9054 行），正文 4644 B，sha256 48e8d5ed09637de13669aadc0346335ec1860c80d063fc59aceb0f7dee782eb3，msg_id 509cb7be-37c2-49aa-a347-a85adfccfa1e。
  - 发送前核对：QA pid 50045 在运行，argv 为 --effort max --permission-mode bypassPermissions；09:20:13Z 时 563xx/564xx 无监听，没有门禁进程。
  - 答复 QA 回执第 6 节 14 项：第 1–7、10、12–14 项同意默认做法；第 8 项 floor 出处已说明；第 9 项 P10 R1 (b) 的理解与本账本第 376、457、474 行的用法一致；第 11 项准许以数据方式读 Builder 回执，以及 Builder transcript 的元数据与工具调用的命令和退出状态，不读口令文件。
  - 要求 QA 独立判断 Builder 三次调用错误的重跑有没有掩盖真实失败。
  - 资源：Soul/Host 窗口与门禁位都附条件预分配给 S2R2 QA（先例：账本 07:41:10Z 条的 S2 QA、P18A-INT QA）。自检满足就直接执行，不满足才发 READY。S2R2 QA 的门禁跑完之前，总管不放行其他单元的 PG 窗口或门禁。
- P04F：09:20:16Z 起 `units/p04f/builder/` 出现，Builder 在写回执。
- 发信计数：Owner 08:27:53Z 输入以来共 5 条。

## 2026-09-27T09:26:04Z 总管

- P04F Builder PREACCEPT 到达：消息 09:24:35.279Z 送达（1915 字符，bypass）。回执 `units/p04f/builder/PREACCEPT-BUILDER.md`，sha256 39d24a69f227ecfa29a3e03a4d023669d027c1cdac3434b8b7c96cac794dcd4a（56289 B，mtime 09:24:16Z），总管复算一致。内容等 QA 回执到齐后再记，START 也等到那时再发。
- 发信计数：Owner 08:27:53Z 输入以来仍为 5 条。

## 2026-09-27T09:29:09Z 总管

- 09:28:44Z 出现一个新的桌面 session（worktree eloquent-curran-82c1e9，transcript 97580e45-65e5-4813-8038-bc3e2b236311.jsonl）。它的首条输入是请它梳理“总管”的逻辑、核对是否符合 PDD v2.1 并分析问题。它不是施工单元，总管不与它通信，只由监视器观察它是否写入 coordination/ 或 units/。

## 2026-09-27T09:47:14Z 总管

- P04F 两份 PREACCEPT 到齐，总管复算哈希一致：
  - Builder：`units/p04f/builder/PREACCEPT-BUILDER.md`，39d24a69f227ecfa29a3e03a4d023669d027c1cdac3434b8b7c96cac794dcd4a（56289 B）；消息见 09:26:04Z 条。
  - QA：`units/p04f/qa/PREACCEPT-QA.md`，f47597cff39d1d47e45b7c2ac2566ed47953d8bcc8a916c97d4bf703d02f3601（67712 B，877 行，mtime 09:45:02Z）；消息 09:45:52.861Z 送达（2624 字符，bypass）。
  - QA 自披露：09:44:33Z 一条只读命令误带重定向，建了 `/private/tmp/claude-501/fp.txt`（96 B，M18 最近 12 个 first-parent 短 SHA），09:44:50Z 自行删除。总管 09:46:09Z 确认该文件已不存在。
  - 两份回执独立发现了工单的同一处遗漏：plan 另有三个不带域摘要 `instruction_scope_sha256`（IM:2080）、`authorized_write_paths_sha256`（2083–2085）、`write_scope_sha256`（2086–2089）。总管在 M18 上核实属实。
  - 两份回执都指出：Resolver 夹具的 claims 摘要不只缺域，还缺 `manifest_ownership_sha256` 成员（Intake 的材料见 IN:731–737、747–753，总管核实属实）；技术书第 623 行（§10.6 修复顺序第 1 项）也提到 Impact；fixture 提交 baseline 时不固定时间，生成向量须固定 Git 日期。
  - Builder 判断 Resolver 缺 `allow_nan=False` 在合同路径上不可达（RS:1230–1236 解析时拒绝；RS:1121–1140 严格树检查），不触发停止条件；QA 初判一致。
  - QA 指出 history.git 不含 M18（09:32Z 实测），QA clone 的 M18 只能随 X 从 Builder clone 取得。
- P04F Builder START：09:46:50.876Z 发往 uds:/tmp/cc-socks/36185.sock（本 transcript 第 9207 行），正文 4032 B，sha256 af5fd52ff293c9d0fc57d4f257742bd22ced7a05bf9876de45976255db514fd8，msg_id 4929b5be-67fa-4e0d-baf3-03f8700c5e96。发送前核对 Builder pid 36185 在运行，argv 为 --effort medium --permission-mode bypassPermissions。
  - U1–U11 裁定：
    - U1：三个 scope 摘要原样冻结，纳入 plan corpus；
    - U2：plan 反例第二层为 owner-recompute，不新增解析器，Impact tests README 写明 plan v2 没有 owner 严格解析器；
    - U4：不钉 Resolver 的 NaN 输出，钉合同层拒绝；
    - U5：固定 Git 日期后两次输出一致就不构成停止条件；REPORT 须记录 Git 日期、身份、TZ、umask 与版本；
    - U6：Intake 加重放，receipt 与 plan 不加，由 QA 在 scratch 复现；
    - U7：技术书第 623 行纳入，只删“Impact、”，工单的三处扩为四处；
    - U8：根 CHANGELOG 在 [Unreleased] 最前追加一节；
    - 其余同意默认。
  - 明确“调用错误”与“结果偏离”的区分：没有执行到候选的命令写错可修正一次再跑并披露；执行到候选而结果不符的，一律停下回报。
- QA 回执第 6 节共 23 项，等 Builder FREEZE 后在 QA START 中答复（其中 (l) 第 623 行已由 U7 裁定纳入本单元）。
- 发信计数：Owner 08:27:53Z 输入以来共 6 条。

## 2026-09-27T09:49:10Z 总管（自披露：误写他单元 clone 的对象库）

- 事实：总管为 U5 做只读预演时，于 09:47:55Z 以 GIT_DIR 指向 `units/p06/builder-s2r2/DSP_ZD/.git`（S2R2 Builder 的 clone，已于 09:18Z 冻结）执行了 `git merge-tree --write-tree ab0ef3c9… 40e202ee…`。该命令会把合并结果写进对象库，总管事先没有意识到。
- 写入：6 个松散对象，都不可从任何 ref 到达（09:48Z 用 `rev-list --objects --all` 核对，共 2609 个可达对象，这 6 个都不在其中）：
  - tree 918f803f546d0fe17cbf8ba2fe2bdc5b46d360ce、ea4c8cab57e0b556d30b407e72762c5f31c15caa、43122be3a7837df3bbed3648a5c6c314b3620ca6、80fe82b744a97a3f221f76ec9ef8b9c5ba2dd6ef；
  - blob 475a97fb4fe0a1f2d7bd90d270e90c8d5dd0d08c、6a3c0a671aa5006b7fa6bf6fcedc1e111fe79594（带冲突标记的合并内容）。
- 未受影响（09:48:55Z 核对）：`.git/index` 与分支 ref 的 mtime 仍为 09:08:09Z，HEAD 为 09:01:31Z，没有 lock 文件；`git -C <clone> status --porcelain` 为 0 行；S′、FREEZE.json、REPORT.md 未动。不可达对象不会随 fetch 传出，不影响 S2R2 QA 的 clone。
- 处置：不删除这 6 个对象（删除是再一次写入），留到统一清理时随 clone 一并处理。S2R2 QA 若在 Builder clone 中看到 09:47:55Z 的对象，来源是总管，不是 Builder。
- 预演结果（只作 U5 开单参考）：merge-base(S′, M2) = f91d1dc（U3 集成头）；合并 M2 与 S′ 时 `CHANGELOG.md` 与 `Modules/control-plane-host/tests/README.md` 两处冲突。
- 纠正做法：今后跨 clone 的只读预演，把 GIT_OBJECT_DIRECTORY 指向总管 scratchpad，原 clone 的对象库只作 alternates；或不做会写对象的操作。

## 2026-09-27T10:10:13Z 总管

- P04F Builder FREEZE：消息 10:08:00.949Z 送达（1437 字符，bypass）。
  - X = 0b72b21dfcb0462e6893b7e4c555973ef68211a2，tree d51257f66498eba4c8296f64b39234333ff9b0e3，唯一父为 M18；分支 codex/p04f-199b209。
  - 总管复算（10:08:49Z）：统一 argv 的 M18..X diff 94911 B，sha256 39872891af0b47dca85ee6c4520ce8cca0a796a7282d2c5ff77df23453f754de；14 条路径，A 2（receipt v2 与 plan v2 的 corpus）、M 12（Intake corpus；三个模块的 test_contracts.py、tests README、CHANGELOG；根 CHANGELOG；技术书），都在工单 §4.1 及 START U7、U8 允许的范围内；三个模块的 src、schema、module.yaml、AGENTS.md 的 blob 与 M18 相同；Builder clone 的 HEAD 为 X，porcelain 0 行；`FREEZE.json` 1cf5c3edcd0e8d2cd0daec5dc6c8f354f9d21e2a972a126478975475410edbff（9536 B），`REPORT.md` 51b7072898e731facf2389c02e4967831335106d6fc2d47afcf175aa3f4385df（23306 B）。
  - 自报：M18 与 X 上 14 个 suite 全部 exit 0，三个 contract suite 分别 +4、+6、+5；failing-before 12 FAIL，3 个 NaN 钉住用例 PASS；5 个变异全部 exit 1；generate_governance、validate_repo、diff --check 通过；validate_governance（开发辅助）M18 与 X 各 75 行、相同；向量两次生成逐字节相同。
  - 两处变异预测偏差，总管裁定（不回复 Builder，在 QA START 中交 QA 核对）：
    - mut-3 以 ERROR 而非 FAIL 检出（严格解析先抛 ResolutionError），算作检出；
    - mut-5 只由 intent 向量重放用例检出，hash_domains 用例不调用 src。不追加断言，要求 QA 逐一核对 6 个域是否都由调用生产函数的用例绑定到 src。
    - 两处都是 Builder 回执 §6.3 的预测错误，WO §4.2 第 4 项“都必须失败”已满足，不属于结果偏离。
  - 披露的流程偏差：生成脚本与 vg.py 各一次调用错误，修正后各重跑一次；误跑一次 validate_repo --help；instruction-input-hashes.json 在自测之后才写。
- S2R2 QA 的门禁已跑完：run 76e5b535e6d540652f2d2d0447c0339f，run 目录的创建与最后修改时间分别为 09:55Z、10:00Z，顶层 publication 为 ABORTED，Phase0 publication 存在。10:08:57Z 时没有门禁进程，/private/tmp/dps-phase0-executor-* 为 0 个。它按预分配执行，没有发 READY 消息。监视器此前记录 56350 于 09:37:36Z 起监听、09:53:24Z 关闭。
- P04F QA START：10:09:49.261Z 发往 uds:/tmp/cc-socks/36398.sock（本 transcript 第 9376 行），正文 5165 B，sha256 c51c9fd63327a7d1281731059a20523593be1eccd9e834cd0a2a1132f3387b65，msg_id 5444dc00-4c59-414b-a5d2-332750179dd4。发送前核对 QA pid 36398 在运行，argv 为 --effort max --permission-mode bypassPermissions。
  - 答复回执第 6 节 23 项：(c)(e)(f)(k)(l) 按 Builder START 的 U1、U5、U2、U8、U7 核对；(q) 准许读 Builder transcript 的元数据与工具调用的命令和退出状态；(r) 准许以数据方式读 Builder 的生成脚本与变异补丁，只读、不执行；其余同意默认。
  - 门禁位附条件预分配给 P04F QA：自检三项都满足就直接跑一次。它的门禁跑完之前，总管不放行其他单元的门禁。
- 发信计数：Owner 08:27:53Z 输入以来共 7 条。

## 2026-09-27T10:15:09Z 总管

- P06-S2-R2 QA 终报：消息 10:13:54.526Z 送达（3214 字符，bypass）。
  - 结论 PARTIAL（外审 NOT_RUN），正式等级 NONE。
  - 交付物（总管 10:14:20Z 复算一致）：`QA-REPORT.md` 7b664ce2461e6a185518971f75364f0bd099ff8e9af65224976611b931ccd860（41447 B），`EVIDENCE_INDEX.json` d46cf19f977471766f79d65f62ffbf6286075e5b3d4b95af01ed6c5fcc6c149f（31276 B），位于 `units/p06/qa-s2r2/DSP_ZD/Reports/ci/p06-s2r2-qa/`。
  - 第 1–8 项全部符合：D-1 修复为同一事务、提交前调用 ReadSnapshotAsync（:717–735）；G2 9/9、H1/H2 符合；新写的 F1 fence 探针中 Inserted 返回值、存储列与 fence.ValidUntil 三者相同；failing-before 在 Policy 层 4/4、链路层 2/2 失败于预期位置；MU7 被杀；suites 全部 exit 0（Host 12/27/16/75，Policy 10/33/8/46）；19 个项目 locked build 0/0。policy-approval.integration 46 例，满足 candidate-test-policy 的 floor 40。
  - 窗口与门禁（按预分配，未发 READY）：窗口 09:49:26Z 自检后进入，PG 09:53:09Z 停机；门禁 09:55:40Z–10:00:45Z，run 76e5b535e6d540652f2d2d0447c0339f，rc 1，Phase0 48 PASS / 5 FAIL，与 S2 run 8cd52e3c 状态差异 0，module-governance 日志逐字节相同（67 条）。窗口与门禁位已释放。
  - 发现 QA-F1（流程）：S2R2 Builder 三次读取 S2 Builder 的 `environment/`（#39 08:29:17Z grep run.sh 与 *.sh；#54 09:01:00Z ls 与 `cat run.sh | head -60`；#55 09:01:08Z ls venv/bin 与 readlink），违反工单 §4.1，REPORT 与 FREEZE 均未披露。
    - 总管判断：属未披露的流程违规，不影响候选的正确性（QA 已独立复核全部项目），PARTIAL 维持。
    - 口令暴露：总管 10:14Z 对 `units/p06/builder-s2/environment/` 下的 run.sh、g.sh 做只计数的模式扫描（PGPASSWORD、Password=、带口令的 URI、pwfile、passfile），两者都是 0 处命中，没有打印内容，也没有读口令文件。按此，#54 读到的前 60 行不含口令。
    - 今后的工单在开工绑定中写明：读取单元根以外的任何路径都要逐条披露。
  - 观察：QA-O1，三次调用错误后重跑，首跑都没有执行候选，没有掩盖真实失败，与“偏离即停”字面不符；另有一次未披露的出错重跑（#109→#111，FREEZE 生成脚本），没有影响。QA-O2，施工阶段执行了 scratchpad 中的 g.sh（只读），未披露。QA-O3，Builder 没有列出 dotnet test 在 /private/tmp 写的 hex32 socket 与 ~/.dotnet/metadata 的 mtime。QA-O4，stdout 与 stderr 的措辞不准。QA-O5，BC 中 09:47:55Z 的 6 个不可达对象来自总管（见 09:49:10Z 条）。
  - QA 在单元根外的写入：本单元运行产生 35 个 hex32 socket，门禁产生 19 个；~/.dotnet/metadata 的 mtime；harness 的 session 文件。QA 已于 10:12:39Z 删除自己的 pg-boot.pw 与 pgpass。
  - 保留物待统一清理：E/pg（已停机）、E/scratch 下四个 clone、Q/.venv 与构建产物、candidate-runs/76e5b535…。
- P06 S2 到此收口：定稿为 S′ = 40e202eec7db37b1f5931d91c3c1c281ebedf47e（S2R2 QA PARTIAL）。下一步开 U5：M2（ab0ef3c9…，U4 QA PARTIAL，其门禁 run f29d2583…）与 S′ 合流。
- 发信计数：Owner 08:27:53Z 输入以来仍为 7 条。

## 2026-09-27T13:44:43Z（总管 date -u）U5 未启动；P04F QA 终报到达（未核验）；Owner 改回截图模式

- 10:19:43Z 之后，总管关闭已收口单元的终端 tab c2（U4-INT Builder v2）、c7（S2R2 Builder）、c8（S2R2 QA）、c9（FIX QA）。关闭前核对 4 个 session 的最后一轮均为 end_turn；transcript 保留，可 `--resume`。c10（P04F Builder）、c11（P04F QA）未动。
- U5 Builder、U5 QA 的两条终端启动命令被拒：Owner 正从另一台设备远程控制，终端命令需其批准。U5 未启动，units/u5-int/ 不存在。监视器 bfjv2tmj2 已于约 10:17Z 到期，未重挂。
- P04F QA 终报到达（仅记到达，总管尚未核验）：自报 PARTIAL，正式等级 NONE，外审 NOT_RUN；QA-REPORT.md sha256 0cde41937746e36866a450570a4f546a8f83654131f30d00fdca639b30b42d96，EVIDENCE_INDEX.json sha256 01d931ce35deff79e3d1e280507b626827aa1ac2f626f1842d9af8e0d2f7c7d7；门禁唯一一次运行 11:00:15Z–11:05:28Z，run 3a24a5df5b14537fcbf3fa8164ac15af，rc 1。门禁位尚未释放；F1（Builder REPORT §1 含 Owner 提交邮箱原文）与 O6（VBCSCompiler pid 96028 常驻、/private/tmp 下 20 个 socket）待裁定。
- Owner 13:39:43Z 指令原文：「我要求你新的任务采取截图的模式，一个任务在一个session里面完成并且可以相互通信」。自此新任务用任务卡开侧边栏会话；这与 08:27:53Z 第 3 条「新开的 Builder 和 QA 一律用终端面板 CLI」冲突，按新指令执行。08:27:53Z 的其余要求（一律 bypass、开会话前查三项、发信前确认收件方是 bypass、不改任何会话的模式）不变。
- Owner 同时要求用多智能体研究「为什么后面不行了」及方案。总管起 5 个只读子代理（时间线、平台能力、档位与权限、通信、红队），汇总后报 Owner。U5 等研究结论后按截图模式开。
- 发信计数：Owner 13:39:43Z 输入以来 0 条。

## 2026-09-27T14:14:21Z（总管 date -u）U5 按截图模式出卡；研究结论摘要

- 按 Owner 13:39:43Z 指令，U5 改用任务卡（spawn_task，不传 cwd，与前任及上午各单元相同），约 14:13Z 出卡：
  - U5 Builder：task_11f64db4，卡片正文即 scratchpad/card-u5-b.md（sha256 e081a0de684cc6f39b2c72f24196e72b9597a66a0f7ef92bfe2f5ead7afdd23f，5957 B，末尾换行除外）。第一轮只做档位自检并回报；总管调成 medium、读其 transcript 确认 perTurnEffort 为 medium 后，再发“档位已校准，开始 PREACCEPT”。
  - U5 QA：task_419fdf40，卡片正文即 scratchpad/card-u5-qa.md（sha256 bed8760d3d3a3b831982f37443f85c97f0117e65bb5efb7fc6d334d0baecc7bf，5765 B，末尾换行除外）。第一轮档位自检（应为 max、bypass）后直接做只读 PREACCEPT。
  - 两卡各带唯一标记（U5B-K7Q2、U5Q-M3X8），会话凭标记在自己的 transcript 目录里认出自己的文件。
  - 卡片启动后应用会在 DSP_ZD 仓库建 worktree 与分支，这是截图模式固有的写入，执行者是应用；与“不写 DSP_ZD 仓库”的字面冲突已向 Owner 说明。
  - 旧的终端启动文件 launch-u5-b.md、launch-u5-qa.md 作废；预生成的 session id c1e41738…、d39692e9… 从未使用。
- 5 个只读子代理的研究结论已全部返回（时间线、平台能力、档位与权限、通信、红队），要点：
  - 本机没有让一个会话直接开出侧边栏会话的工具（start_session 受服务端开关控制，未开放）；前任与总管用的都是 spawn_task。应用对每次启动的记录都是 “The user started your suggested background task …”（前任 transcript b096d66e 第 346、437 行等）；应用日志 main.log 记为 “Spawned-task started”，未记录触发动作。Owner 13:5x 说明其全程未动手；两者的差异总管无法用现有记录解释，不再断言是谁启动的。
  - 04:01–05:18Z 总管出的 4 张卡一直未启动（Owner 05:25:47Z 截图所示即其中一张），05:36–05:41Z 总管为改用终端撤卡。
  - 终端模式的故障：不进侧边栏；08:21Z 应用关闭 4 个终端 tab，CLI 会话随之消失（main.log 47098–47172）；每会话 6 个 tab 上限；Owner 远程时终端命令需本机批准，10:20:26Z 的 U5 启动挂到 13:29:43Z 被拒，其间总管收不到消息。
  - 丢消息主因是收发双方权限模式不同（auto 与 bypass），双方都是 bypass 时总管侧统计 76 条全部送达。每条 Owner 输入后，一个会话向桌面会话发信的上限为 10 条。
  - 上午 12 个卡会话每轮实际都按 max 运行，根因是当时 settings.json 的 env 覆盖项（03:52:03Z 已删除，14:0x 总管复核 settings.json 已无该键）；总管自身进程仍带 CLAUDE_CODE_EFFORT_LEVEL=max。
- 更正两处旧记录：transcript 的 user 记录里有 permissionMode 字段（账本 L1322 与记忆中“查不到”的说法不准确）；上午卡会话并非都是 bypass，12 个里有 8 个是 auto（账本 L773 的说法不准确，06:54Z 已部分更正）。
- 发信计数：Owner 13:52Z 前后输入以来 0 条。
- 更正上一条（14:14:48Z 补记）：上一条中的三处时间写得不精确，以本行为准。Owner 说明“全程没有动手”的输入时间为 14:07:29Z；总管复核 settings.json 已无该键的时间为 13:58:13Z；发信计数应写作“Owner 14:07:29Z 输入以来 0 条”。

## 2026-09-27T14:29:09Z（总管 date -u）U5 两张卡已启动；三项核对通过；Builder 调档实测生效

- 应用通知：两张卡均已启动（通知原文 “The user started your suggested background task …”）。
  - U5 Builder：local_11bf39cb-524e-4d10-8f2f-170ee83ec6cd，createdAt 14:26:57Z，worktree musing-lamarr-260fe1，transcript …-musing-lamarr-260fe1/34a8ee99-08e6-4f8a-a580-9a5f37f2a205.jsonl（含标记 U5B-K7Q2）。
  - U5 QA：local_b98d0186-82c7-44c7-9d9a-4cfbe757772d，worktree objective-swartz-1fbf0c，transcript …-objective-swartz-1fbf0c/7da99379-79e0-499e-945f-ffe240e8f48b.jsonl（含标记 U5Q-M3X8）。
- 三项核对（总管只读 transcript 实测）：
  - Builder 第一轮：model claude-opus-5-5；11 条 assistant 记录均为 effort/perTurnEffort max（任务卡继承总管档位，预期内）；permissionMode bypassPermissions；其自报 CLAUDE_CODE_EFFORT_LEVEL 未设置。
  - 总管约 14:28:2xZ 调用 set_session_effort(medium)，get_session 回读 medium；随后按名字回信“档位已校准，开始 PREACCEPT”（msg 535c7d5f…）。
  - Builder 第二轮首条 assistant 记录 14:28:41Z：model claude-opus-5-5，effort medium，perTurnEffort medium。这是 03:52Z 删除全局覆盖项之后第一个干净样本：任务卡会话调档后下一轮即生效。
  - QA：model claude-opus-5-5；32 条 assistant 记录均为 max/max；permissionMode bypassPermissions。
- 等两份 PREACCEPT 回执。发信计数：Owner 14:27:26Z 输入以来 1 条。

## 2026-09-27T14:40:45Z（总管 date -u）U5 Builder PREACCEPT 回执到达（只记到达）

- units/u5-int/builder/PREACCEPT-BUILDER.md：sha256 83c770141f7338b60bbfa5f1240bda35b66d968ffc4b3f5ab4598b4fc78d90e7，44440 B，440 行，与自报一致。Builder 所在 worktree（musing-lamarr-260fe1）git status --porcelain 为 0 行。
- 按惯例，QA 回执到达前账本不记 Builder 回执的内容。U5 QA 仍在做 PREACCEPT，units/u5-int/qa/ 尚未创建。
- 发信计数：Owner 14:27:26Z 输入以来 1 条。

## 2026-09-27T14:42:51Z（总管 date -u）P04F 收口 PARTIAL；门禁位释放；F1、O6 处置

- Owner 2026-09-27T14:41:03Z 输入：对总管“打算直接做的”三件事（P04F 收口、Factory 线合成 M19、U5 照常推进）答“OK”；对“每个单元点两次卡”答“OK”；清理类“晚一些处理”；F1、O6、DSP_ZD worktree 三项要总管先解释并给建议。
- P04F QA 终报核验（总管只读实测）：QA-REPORT.md sha256 0cde41937746e36866a450570a4f546a8f83654131f30d00fdca639b30b42d96（43135 B），EVIDENCE_INDEX.json sha256 01d931ce35deff79e3d1e280507b626827aa1ac2f626f1842d9af8e0d2f7c7d7（49363 B），均与自报一致；QA clone HEAD 为 X 0b72b21dfcb0462e6893b7e4c555973ef68211a2，porcelain 0 行；报告结论节为 PARTIAL、正式等级 NONE、外审 NOT_RUN。
- P04F 到此收口：定稿 X = 0b72b21d…（唯一父 M18 199b2097…）。下一步与 FIX 定稿 F = 6ba8ca00… 一起并入 M18，得到 M19，作为 P18 的基底。
- 门禁位于 14:42:51Z 释放：无 run_candidate_gate / run_phase0_gate 进程，/private/tmp/dps-phase0-executor-* 为 0 个。U5 需要门禁时按 START 中的条件预分配办理。
- F1 处置：REPORT.md（units/p04f/builder/DSP_ZD/Reports/ci/p04f-builder/REPORT.md，被 .gitignore 忽略，不在提交内容里）第 16 行写的是 X 的 author/committer 身份 “YOUNG <Owner 邮箱>”。总管实测 X、M18（199b209）、D′（320ce77）三个提交的 author 与 committer 都是这个身份，即此前各 Builder 沿用了仓库既有提交的作者身份。冻结文件不改。今后 Builder 提交用什么身份，待 Owner 决定（U5 Builder 回执也就此请示）。
- O6 处置：归为门禁固有写入（门禁内 dotnet 构建会拉起编译服务器并建本地 socket）。VBCSCompiler pid 96028 在 14:4x 复查时已不在运行；20 个 socket 列入之后统一清理。
- 发信计数：Owner 2026-09-27T14:41:03Z 输入以来 0 条。
- 更正上一条（2026-09-27T14:43:17Z 补记）：O6 的复查时间以本行为准——2026-09-27T14:43:17Z 实测 pid 96028 不存在（ps rc=1），当前 VBCSCompiler 进程数 0。

## 2026-09-27T14:45:49Z（总管 date -u）Owner 裁决：提交身份、O6、会话落脚点

- Owner 2026-09-27T14:45:33Z 输入，逐项裁决：
  1. 提交身份按总管建议 A：今后各 Builder 的提交一律用单元自己的身份，形如 “<单元> Builder <<单元小写>-builder@local.invalid>”（U5 为 “U5-INT Builder <u5-int-builder@local.invalid>”），不再用 Owner 的姓名与邮箱。已冻结的提交（X、M18、D′ 等）不改。
  2. O6 按总管建议：归为门禁固有写入，不处理；socket 并入之后的统一清理。
  3. 不采纳“为卡会话单独建空仓库”的方案。截图模式继续用默认方式：卡片启动时由应用在 DSP_ZD 建 worktree 与 claude/* 分支，Owner 已知情。
  4. “下一步继续”：总管读合规审查记忆后写 Factory 线 M19 集成工单并出卡；U5 照常推进。
- 发信计数：Owner 2026-09-27T14:45:33Z 输入以来 0 条。

## 2026-09-27T14:54:24Z（总管 date -u）派发 WO-M19-INT-FIX-P04F（Factory 线：F 与 X 合流为 M19）

- 工单：coordination/WO-M19-INT-FIX-P04F.md，sha256 efd210f86da18d088f109a9cee7812e1b58f9d1aca722bc3b0070e417337f9ec，15534 B。依据：账本 09:12:56Z 条（F 与 X 另开集成单元合流）、14:42:51Z 条（P04F 收口）；Owner 2026-09-27T14:41:03Z 对“Factory 线合成 M19”答 OK。
- 输入：L = F 6ba8ca001e011c2c9b3be87cd52fed114909e653；R = X 0b72b21dfcb0462e6893b7e4c555973ef68211a2；merge-base = M18 199b2097257d88112091ad2dd381c90345eedc8a。L 改 7 个路径、R 改 14 个，重叠只有根 CHANGELOG.md（更正 09:12:56Z 条“路径不相交”）。总管未预演合并。
- 提交身份按 Owner 14:45:33Z 裁决：M19-INT Builder <m19-int-builder@local.invalid>。
- 资源：端口 56360–56379（2026-09-27T14:50:35Z 实测无监听）；各自编译官方 PG 18.4；不需要 Soul/Host 窗口；门禁位与 U5 共用，U5 优先。
- 本单元不进入 P18。P18 前置含 P01，P01 未开单；M19 验收后先向 Owner 报告。
- 卡片提示词（scratchpad）：card-m19-b.md sha256 f39f27c38b35aee4b3c8d335f318e32f35c82d5362e8879fc518a490640ef5df（6526 B，标记 M19B-R4T6）；card-m19-qa.md sha256 b822f71a8895aa05a5f8b38953b18dc2eb8e8622058db577b9495474abb79cd4（6077 B，标记 M19Q-W8J2）。两卡都要求回执与证据不写出 Owner 的姓名与邮箱。
- U5：Builder PREACCEPT 已到（14:40:45Z 条）；QA PREACCEPT 截至本条未到（units/u5-int/qa/ 不存在）。
- 发信计数：Owner 2026-09-27T14:45:33Z 输入以来 0 条。

## 2026-09-27T15:02:07Z（总管 date -u）M19 两个会话起跑；Builder 档位已校准

- 两张卡（task_c1b41e28、task_a5e675fc）的会话已起跑，时间取自各自 transcript 第 5 行（带 permissionMode 的首条 user 记录）。本条不记录是谁启动的卡。
- M19 Builder：session local_f7ab99e5-a6df-4025-a4d1-f1a467633735，worktree suspicious-thompson-24cc59，transcript 7b06ddb4-4560-495f-af47-d47c65eada0e.jsonl（含卡标记 M19B-R4T6）。
  - 第一轮：15:00:39.360Z 起，permissionMode 为 bypassPermissions；8 条 assistant 记录都是 claude-opus-5-5，effort 与 perTurnEffort 均为 max，属卡片继承的预期值；工具只用了 Bash 2 次、ToolSearch 1 次、SendMessage 1 次，没有写入。
  - 自检消息 15:01Z 前后送达（来自 uds:/tmp/cc-socks/79636.sock，from-mode bypass），所报的值与总管 15:01:23Z 读 transcript 的结果一致。
  - 总管于 15:01:2xZ 用 set_session_effort 把它设为 medium，随即发出“档位已校准，开始 PREACCEPT”（msg_id f79dbd41-3454-4990-bd04-35be40e18c7c）。
  - 第二轮第一条 assistant 记录 15:01:40.877Z：claude-opus-5-5，effort 与 perTurnEffort 均为 medium。
- M19 QA：session local_59ab57dd-dbff-4d28-8ca3-64c56b073fab，worktree sweet-lumiere-a7e85e，transcript 3585c974-56e0-42b2-8ed7-ec6874200e79.jsonl（含卡标记 M19Q-W8J2）。15:00:42.258Z 起，permissionMode 为 bypassPermissions；截至 15:01:45Z 的 22 条 assistant 记录都是 claude-opus-5-5，max/max。自检通过后按卡片直接做 PREACCEPT。
- U5：QA PREACCEPT 仍未到。总管挂了一个后台检查：回执文件出现并稳定 90 秒即提醒，最长 60 分钟。
- 发信计数：Owner 2026-09-27T14:45:33Z 输入以来 1 条（M19 Builder 校准）。
- 更正上一条（2026-09-27T15:02:22Z 补记，时间取自总管 transcript）：M19 Builder 自检消息于 15:01:11.167Z 送达（第 10457 行）；set_session_effort 调用于 15:01:27.447Z（第 10471 行），结果 15:01:27.516Z；校准消息于 15:01:31.537Z 发出（第 10474 行），15:01:31.710Z 返回 queued。上一条中的“15:01Z 前后”“15:01:2xZ”以本行为准。

## 2026-09-27T15:07:27Z（总管 date -u）U5 QA PREACCEPT 到达；两份 U5 回执入账；U5 Builder START

- U5 QA PREACCEPT：消息 15:04:58.978Z 送达（总管 transcript 第 10521 行，来自 uds:/tmp/cc-socks/66261.sock，from-mode bypass）。
  - 文件 units/u5-int/qa/PREACCEPT-QA.md，sha256 34be5c32b30f79c25b7d4aa2a326a12dca7443f8eca1fb06cda314b505845dae，57287 B，652 行，mtime 15:04:32Z（回执自述 14:59:05Z 起写）。与消息所报一致。
  - 要点：model、effort、权限模式都符合（claude-opus-5-5，max/max，bypassPermissions）；没有读 Builder 回执；拓扑独立核对与工单一致。
  - 与工单不符（QA 编号）：D1 L 侧分项实为 soul-registry 3 个、windows-edge-worker 9 个（工单写 4 个、8 个），总数 14 一致；D2 工单写两个 session 由终端 CLI 启动，实际是任务卡；D3 工单 §3.4 第 5 项写 R 的 SHADOW 测试与 L 的 R4 测试“在同一测试工程中”，实际分属 Dps.ControlPlaneHost.Tests 与 Dps.SoulRegistry.Tests，耦合经 ProjectReference、共用的 SoulCurrentProcessFixture 与 SoulCurrentHostProcess 子进程；D4 history.git 不含 L、R、B。
  - X1–X18 均附默认做法；请总管在 QA START 中确认 X11（soul 夹具在 /private/tmp 硬编码目录）与 X12（对 Builder 的 environment/ 做结构扫描）。
  - 自报偏离：14:49:33Z 与 14:55:48Z 两条 find 遍历了其他单元 environment/ 的目录项（p06/qa-s2r2、u4-int/builder、p06/builder-s2r2），没有打开文件，没有读取口令。
  - 回执 §3.2 T12（第 167 行）逐字写出了 L 的 author 与 committer，即 Owner 的姓名与邮箱。U5 的卡片没有禁止这一点（只有 M19 的卡片写了）。列入之后统一清理的清单；QA START 将要求 REPORT 与证据中不再写出。
- U5 Builder PREACCEPT（14:40:45Z 条，83c77014…）内容摘要，QA 回执已到，现可入账：
  - 输入核对与工单一致，只有 D1 同上。两个冲突文件的解决草案：CHANGELOG 为集成说明在前、L 块、R 块（1510 行）；Host tests README 为 L 全文加 R 的新增 10 行（预期 blob 09a78e65…，与 QA 回执独立算出的期望相同）。
  - 自测清单：21 个 artifact、42 个 suite（41 个 required）；预期共 1248 项，1247 PASS，唯一 FAIL 为既有的 P11 用例。
  - 第 6 节 13 项请示。
- 工单 WO-U5 更正（总管的错误；工单受哈希绑定，不改文件，以本条为准）：D1 分项应为 soul-registry 3 个、windows-edge-worker 9 个；D2 两个 session 实际由任务卡开出（依据 Owner 13:39:43Z 指令，账本 14:14:21Z 条）；D3 两组测试不在同一测试工程，耦合路径如上。
- U5 Builder START：15:06:20.861Z 发往 uds:/tmp/cc-socks/66183.sock（总管 transcript 第 10571 行），正文 2473 B，sha256 7402f8f91c3739ff39151ce03ef1d5a7bde888c4b713cb31f1338f1f4534d26f，msg_id bcc2b752-9905-48ff-9fc0-520177c51a76。
  - 晚于两份 PREACCEPT（Builder 消息 14:40:36.429Z，QA 消息 15:04:58.978Z）。
  - 发送前核对：U5 Builder 进程 66183 在运行，最近带 permissionMode 的记录为 bypassPermissions。
  - 送达：Builder transcript 第 421 行，15:06:20.886Z，permissionMode 为 bypassPermissions；15:06:23.587Z 起的 assistant 记录为 claude-opus-5-5 / medium。
  - 裁定：第 1、2、4、5、7、8、10、11、13 项同意默认做法；第 3 项提交身份为 U5-INT Builder <u5-int-builder@local.invalid>（Owner 14:45:33Z 裁决），REPORT 与证据中不写 Owner 的姓名与邮箱；第 6 项同意（自有 PG 跑完即停，窗口内重新起库）；第 9 项同意（校验失败先停下回报，不自行 --write）；第 12 项改为附条件预分配。
  - Soul/Host 窗口附条件预分配给 U5 Builder：停库后自检三项（56320–56329 无监听；没有门禁进程；没有本单元根以外的 Host 或 Soul 测试进程），都满足就直接进入，不发 READY，自检结果写入证据目录的 READY-FOR-PG-WINDOW.md；窗口起止、结果与停机时刻写进 FREEZE 消息。在它报告窗口结束之前，总管不向其他单元放行 Soul/Host 窗口。
- 资源现状：门禁位空闲（14:42:51Z 释放）。U5 QA 在 Builder FREEZE 之后需要窗口与门禁；M19 QA 之后需要门禁；两者同时就绪时 U5 优先。
- 发信计数：Owner 2026-09-27T14:45:33Z 输入以来 2 条（M19 Builder 校准；U5 Builder START）。

## 2026-09-27T15:09:07Z（总管 date -u）M19 Builder PREACCEPT 到达（只记到达）

- 消息 15:08:28.846Z 送达（总管 transcript 第 10597 行，来自 uds:/tmp/cc-socks/79636.sock，from-mode bypass）。
- 文件 units/m19-int/builder/PREACCEPT-BUILDER.md，sha256 6a4febf32917bb93b7690e6d32ca1c5ac5e8c5c52a43edd64300b929b6201325，28488 B，303 行，mtime 15:08:11Z。与消息所报一致。
- 按惯例，M19 QA 回执到达之前，本账本不记 Builder 回执的内容。units/m19-int/qa/ 截至本条尚不存在。
- 发信计数：Owner 2026-09-27T14:45:33Z 输入以来 2 条。

## 2026-09-27T15:30:19Z（总管 date -u）U5 Builder FREEZE；窗口释放；U5 QA START

- U5 Builder FREEZE：消息 15:27:04.404Z 送达（总管 transcript 第 10636 行，来自 uds:/tmp/cc-socks/66183.sock，from-mode bypass）。
- 总管只读复核（15:27:56Z–15:28:10Z）：
  - M3 = 17438fb231ec02a489b1aad29088d7b9559bbe37，tree 3b974dd13f11e0d98ae13ca9d81cedca976bf856，父依次为 L ab0ef3c9…、R 40e202ee…；未签名；author 与 committer 都是 U5-INT Builder <u5-int-builder@local.invalid>。
  - Builder clone 的 HEAD 与分支 codex/int-u4m2-p06s2r2 都指向 M3；porcelain 0 行；没有未跟踪的 AGENTS.md 或 module.yaml。
  - 统一 argv 的 diff：L→M3 466936 B，sha256 40158cce743a6bb17e68cfa499d798200ae0c7acb9ff1b38fc920b3f6d2803f3，60 个路径；R→M3 133161 B，sha256 c4a05dfbe8131ffdaf92d90e19678b90389317c6da9dd17e388de9209490eaf0，14 个路径。
  - FREEZE.json sha256 f16f65a7831d591165dd6d05d99f3817b215ebbb58d177af58f052f4345cc25a（64058 B）；REPORT.md sha256 3095c30f20dc71c38600c84d993a75aac65b5865efa18e232d17515d8c085e20（15824 B）；Reports/ 被 .gitignore:10 忽略。
  - 证据目录中 Owner 邮箱域名的命中文件数为 0；56320–56339 无监听。
  - 以上全部与 Builder 所报一致。第一次复核命令因 zsh 不对变量分词而全部失败，输出作废，改用 shell 函数重跑后得到上述结果。
- Builder 自报（只作数据，交 QA 独立判断）：
  - 21 个构建全部 0 警告 0 错误，没有 NU1xxx；41 个 required suite 共 1248 项，1247 PASS，唯一 FAIL 为既有的 P11 用例；policy-approval.integration 46/46；windows-gate 记 NOT_RUN。
  - 自有 PG 15:19:02Z–15:19:55Z。窗口：15:19:55Z 自检三项都满足，直接进入；Host 75/75（15:20:01Z–15:20:31Z），Soul 107 项中 106 PASS、1 FAIL（P11），exit 2（15:20:38Z–15:23:01Z）；15:23:01Z 停机，**窗口释放**。
  - 披露：驱动脚本把汇总行当成用例名，曾把 Soul 误判为不符，复核后认定符合，没有重跑；3 次调用错误各修正一次后重做，原件保留。
  - 口令扫描终扫 15:26:38Z，只命中两个口令文件本身，阳性对照 3/3；口令文件 15:26:50Z 已删除。/private/tmp 下新增 33 个 hex32 socket。
- VBCSCompiler pid 85508：15:09:55Z 启动，父进程为 1，15:27:56Z 仍在运行；Builder 自报它属于本单元，请总管决定。总管决定不结束它，等它空闲超时自行退出，与 Owner 对 O6 的裁决（不处理，并入之后的统一清理）同类；已请 U5 QA 在第一次构建前记录并披露。
- U5 QA START：15:29:34.794Z 发往 uds:/tmp/cc-socks/66261.sock（总管 transcript 第 10671 行），正文 4274 B，sha256 37320f9fef81e4d6dbc7b45c38f2745666ee8a293d64f4e36af2408edbd0cfbc，msg_id 4ff56318-5ebc-4a04-8bdc-03e2b888a60e。15:29:34.869Z 送达（QA transcript 第 904 行，permissionMode 为 bypassPermissions）。
  - 裁定：X1–X5、X7–X10、X13–X15、X17、X18 同意默认做法；X6 把提交身份作为判据，报告与证据中不写 Owner 的姓名与邮箱；X11 接受，以窗口前后差集判断残留；X12 准许对 Builder 的 environment/ 做结构扫描，不打开 secrets/，跳过 pg/；另准许以数据方式读 Builder 回执，以及 Builder transcript 的元数据、命令、退出状态与第 421 行的 START 原文，不读口令文件。
  - Soul/Host 窗口与门禁位都附条件预分配给 U5 QA，自检满足就直接执行，不发 READY；不满足每约 3 分钟复查，60 分钟后停下回报。在 U5 QA 的门禁跑完之前，总管不向其他单元放行 Soul/Host 窗口或门禁。
  - 请 QA 独立判断 Builder 的误判复核与 3 次调用错误的处理。
- 门禁队列：U5 QA（预分配）→ M19 QA（M19 Builder 尚未 START）。
- 发信计数：Owner 2026-09-27T14:45:33Z 输入以来 3 条（M19 Builder 校准；U5 Builder START；U5 QA START）。

## 2026-09-27T15:47:46Z（总管 date -u）M19 QA PREACCEPT 到达；两份 M19 回执入账；M19 Builder START

- M19 QA PREACCEPT：消息 15:46:10.789Z 送达（总管 transcript 第 10715 行，来自 uds:/tmp/cc-socks/79714.sock，from-mode bypass）。
  - 文件 units/m19-int/qa/PREACCEPT-QA.md，sha256 5707090bfaeca6425fd87a152fcff6688050d6d0a40880a25244ea40f59e4742，65148 B，575 行，mtime 15:45:39Z。与消息所报一致。
  - 要点：四次档位读取都是 claude-opus-5-5，max/max，bypassPermissions；没有读 Builder 回执；工单第 1 节的输入与拓扑独立核对全部一致。
  - 注记：D1 镜像不含 M18、L、R；D2 输入 clone 的干净性只核对了已跟踪文件；D3 技术书有 Impact 以外的既有过期行（artifact-builder、evidence-ledger、release-controller 的版本），记为观察 O1，不判为 M19 缺陷。
  - 检查计划：31 个 required suite，其中 29 个要跑（共 435 项），factory-control-plane-host.postgresql18 与 factory-worktree-manager.postgresql18 记 NOT_RUN，责任包归 P18；中央 CI 预期 261；validate_repo 预期 299/96/247；门禁预期 48 PASS / 5 FAIL，module-governance 75 行、零新增；语义交互 8 个检查点；P18 进入条件的前后两半分别核对，并写明 P01 未开单。
  - 需 QA START 答复的 ★ 项：Q5 门禁位放行方式；Q6 读 Builder transcript 的元数据、命令与退出状态；Q7 QA 阶段以数据方式读 Builder 回执与 REPORT；Q8 对 Builder environment/ 做结构扫描（不开 secrets/，跳过 pg/）。
  - **自报偏离 D-a**：15:17:16Z 与 15:17:23Z，为复核两父 EVIDENCE_INDEX，读取并哈希了其他单元 environment/ 下的文件：fix-m16m26/qa/environment/scripts 18 个，p04f/qa/environment 214 个（scratch 211、logs 3）。没有显示内容；按路径判断其中没有口令或密钥文件，两份索引中也没有 environment/secrets 下的文件。这违反了卡片“不读其他单元的 environment/”。**D-b**：15:11:11Z 对 p17-int 的 PG 源码包及其 .sha256 执行了 ls -l，只取大小与 mtime。
  - 处置：D-a、D-b 由 QA 写进 QA-REPORT 的流程部分；QA START 将重申除工单第 3.1 节的源码包例外外，不再读其他单元的 environment/。
- M19 Builder PREACCEPT（15:09:07Z 条，6a4febf3…）内容摘要，QA 回执已到，现可入账：
  - 输入核对与工单一致；三个提交的身份相同，回执中记作“Owner 身份”。
  - 根 CHANGELOG 两侧都在第 4 行后插入，git 自动合并必然冲突；其余 20 个路径是平凡合并。解决方式：用脚本从三个 blob 拼出，顺序为 L 节、R 节、集成说明，说明放在两侧条目之后（沿用 M18 的 P18A 先例）。
  - 自测：29 个 suite（27 个非 PG，加 M16、M26），另有 2 项 NOT_RUN（Host 与 worktree-manager 的 postgresql18）；中央 CI 预期 Ran 261；validate_repo 预期 json=299。
  - 与 QA 独立列出的清单相比，要跑的 29 个 suite 与 2 项 NOT_RUN 在集合与数量上都相同（总管比对）。
  - 第 6 节 11 项请示。
- M19 Builder START：15:46:54.514Z 发往 uds:/tmp/cc-socks/79636.sock（总管 transcript 第 10750 行），正文 1547 B，sha256 7458867be2f5a87ddfb09035664dc65f7321849ad255fce7a0509ae405e97bc4，msg_id d6c99e40-17ff-4fb1-b536-d1313ccb162f。
  - 晚于两份 PREACCEPT（Builder 15:08:28.846Z，QA 15:46:10.789Z）。
  - 发送前核对：进程 79636 在运行；最近带 permissionMode 的记录为 bypassPermissions；15:01:35Z 以后的 assistant 记录都是 claude-opus-5-5 / medium。
  - 送达：Builder transcript 第 335 行，15:46:54.572Z，bypassPermissions；15:47:02.981Z 起为 claude-opus-5-5 / medium。
  - 裁定：第 1、3、4、5、6、8、9、10 项同意默认做法（第 10 项以较严的 M16 ≥8、M26 ≥6 为准）；第 2 项同意，集成说明放在两侧条目之后；第 7 项同意，技术书 Impact 以外的过期行不改，只在 REPORT 中列为观察；第 11 项知悉。
  - 重申：提交身份为 M19-INT Builder <m19-int-builder@local.invalid>，REPORT 与证据中不写 Owner 的姓名与邮箱；本单元不需要窗口，不跑门禁。
- 门禁队列不变：U5 QA（预分配）→ M19 QA。
- 发信计数：Owner 2026-09-27T14:45:33Z 输入以来 4 条（M19 Builder 校准；U5 Builder START；U5 QA START；M19 Builder START）。

## 2026-09-27T15:55:12Z（总管 date -u）在途状态；下一就绪单元的只读摸底

- Owner 输入（本 session）：“继续你作为编排者职责”。应用同时送达两条通知，内容为任务卡 task_c1b41e28、task_a5e675fc 已在独立会话中启动（两会话实际于 15:00:39Z、15:00:42Z 起跑，见 15:02:07Z 条）。
- 在途（15:52:03Z 实测）：
  - U5 QA 在执行 START，claude-opus-5-5 / max，最近记录 15:52:00Z；
  - M19 Builder 在执行 START，claude-opus-5-5 / medium，最近记录 15:49:10Z；
  - M19 QA 回执已交，待 M19 FREEZE 后下达 QA START。
  - 563xx 无监听；没有门禁进程；VBCSCompiler pid 85508 已不在进程表中（15:27:56Z 仍在，15:52:03Z 已退出），总管没有干预。
- 下一就绪单元（按 PDD §3.1 第 1 步“优先最短产品链”）：
  - P07（前置 P06；进入条件“P06 的 Policy 公共读取合同已冻结”）。P06 不在 §3.1 的局部交接表中，须看其实际出口；P06 的集成头 M3 正由 U5 QA 验收。按合规审查的教训，U5 QA 出结论前不派 P07，只做只读摸底。
  - P01（前置只有 P00；也是 P18 的前置，且环境类问题反复出现）。先做只读摸底，再定是否并行开单。
- 已派两个只读研究代理（总管 session 的子代理，不担任 Builder 或 QA）：分别摸底 P07 与 P01 的事实状态。约束：只用只读 git 命令读 M3（U5 Builder clone）与 M18（P18A-INT Builder clone）的对象，不 checkout、不 fetch、不写任何文件，不进入任何 environment/，不读凭据，不起 PG，不跑构建或测试，不发消息。结果回到总管后写入 coordination/research/。
- 发信计数：Owner 本次输入以来 0 条。

## 2026-09-27T16:04:18Z（总管 date -u）M19 Builder FREEZE；U5 QA 门禁已跑完；M19 QA START

- M19 Builder FREEZE：消息 16:02:04.075Z 送达（总管 transcript 第 10866 行，来自 uds:/tmp/cc-socks/79636.sock，from-mode bypass）。
- 总管只读复核（16:02:23Z）：
  - M19 = df120166469e5044977227798932c07808b87571，tree 963928ac1fffbd9bf8d50413014fe28d73a8c54e，父依次为 L 6ba8ca00…、R 0b72b21d…；未签名；author 与 committer 都是 M19-INT Builder <m19-int-builder@local.invalid>。
  - Builder clone 的 HEAD 与分支 codex/int-fix-p04f-199b209 都指向 M19；porcelain 0 行；没有未跟踪的 AGENTS.md 或 module.yaml。
  - 统一 argv 的 diff：L→M19 95412 B，sha256 8f66d710d075eaba27817faa7c6b0ddb4cbfd43f098cf997a7cb63309a55fa8d，14 个路径；R→M19 14630 B，sha256 de45a7354f9441bbfda98f37616ecc204893c71f329638aa23bfe6ecdd51c32e，7 个路径；M18..M19 为 20 个路径。两份 diff 哈希与 FREEZE.json 所记相同。
  - FREEZE.json sha256 bdba95ff818b1c31d4847f190676cdbbfbc87bd792b8a570a55d780d20ff0bce（11142 B）；REPORT.md sha256 d6587895912787aab293a06c5bbab705e79e7c2d1aac1e439bcb34c49d0897cc（14855 B）；EVIDENCE_SHA256SUMS.txt sha256 705b90f3c54caa8b922d3a14817e59b54390e88c191d237ec92bfad5497a6297。Reports/ 被 .gitignore:10 忽略。
  - 证据目录中 Owner 邮箱域名的命中文件数为 0；56300–56399 无监听；没有门禁进程。
- Builder 自报（只作数据，交 QA 独立判断）：只有根 CHANGELOG 冲突，按 L 节、R 节、集成说明拼接；29 个 suite 全部 OK，M16 为 8、M26 为 6，没有 skip；中央 CI Ran 261 OK；generate_governance verified: 3；validate_repo json=299；NOT_RUN 两项（factory-control-plane-host.postgresql18、factory-worktree-manager.postgresql18）。PG 18.4 在 127.0.0.1:56360 运行于 15:58:41Z–15:59:06Z，之后已停。披露 4 项：①zsh 不拆分变量致 clone 首试 rc 127；②configure 在 env -i 下找不到 libcrypto，指定 Homebrew openssl@3 后重新编译；③ledger migrator 用 SUPERUSER；④/private/tmp 新增的 18 个 socket 与 2 个 phase0-executor 目录归因于同时段的 U5 QA 门禁。另记技术书第 372、373 行的版本过期为观察。
- U5 QA 门禁（总管观测）：run 335546a88e42311541be643b659d7745，目录 15:52Z 创建，candidate-evidence.json.publication.json 于 15:59Z 生成，status 为 ABORTED（与先例一致）。16:02:40Z 时没有门禁进程，/private/tmp/dps-phase0-executor-* 为 0 个。U5 QA 仍在执行其余检查。
- M19 QA START：16:03:28.716Z 发往 uds:/tmp/cc-socks/79714.sock（总管 transcript 第 10889 行），正文 4248 B，sha256 137d56f30291a9c6e485bb62872fd76f4fbb6cd2fdd475843799366ae2e8bedd，msg_id b79fc984-5b12-4fc7-86fd-5a72495e1d2e。16:03:28.790Z 送达（QA transcript 第 1001 行，permissionMode 为 bypassPermissions）。
  - 裁定：Q1–Q4、Q9–Q18 同意默认做法；Q6、Q7 准许以数据方式读 Builder transcript 的元数据、命令、退出状态与 START 原文，以及 Builder 的回执、REPORT、FREEZE，不读口令文件；Q8 准许对 Builder 的 environment/ 做结构扫描，不打开 secrets/，跳过 pg/。
  - D-a、D-b 写进 QA-REPORT 的流程部分；今后除工单第 3.1 节的 PG 源码包只读复制外，不再读其他单元的 environment/，两父 EVIDENCE_INDEX 中位于 environment/ 的条目记为“未复核”。
  - 门禁位附条件预分配给 M19 QA，排在 U5 QA 之后：附加条件为 U5 QA 的门禁 run 已生成 publication 文件（16:02:40Z 已满足）；自检不满足就每约 3 分钟复查，60 分钟后停下回报。
  - 请 QA 独立判断 Builder 披露的 ①–④。
- 发信计数：Owner 最近一次输入以来 1 条（M19 QA START）。

## 2026-09-27T16:13:06Z（总管 date -u）P07 进入条件的只读核查；在途状态

- P07 摸底写入 coordination/research/P07-SCOPING-20260927.md，sha256 c17045db93a6daae977680a1266643b33007502afa475568108df21cdf644e36（5888 B）。内容只有事实与出处，研究代理的完成通知于 16:07:43Z 送达；总管 16:08:13Z 起抽查，16:11:57Z 复读了其中部分引文（文件第 3 行列明范围）。
- P07 进入条件“P06的Policy公共读取合同已冻结”，总管的核查结论：
  - 适用 P06 自己的裁决定义（WO-P06-M1-SHADOW-RULINGS-2.md:60）：“新的版本化 JSON 合同，含 schema 和 corpus，绑定 proposal commitment 与 major”。
  - 按这个定义已满足：S2 QA D 节 D1–D6 为“符合”（schema required 21 项、corpus 28 例且 case ID 在 C# 与 Python 两处固定、codec 严格）；D7 即 D-1，经 S2R2 修复，S2R2 QA 结论 PARTIAL、候选第 1–8 项符合。
  - 更严格的钉住没有做：两份 schema（readback 869cdf5e…、decision 556e6586…）的字节 sha256 在 M3 全树 0 命中（总管 16:08:13Z 实测）；readback 摘要只有不等断言（PolicyCanonicalTests.cs:464-466）。P04F 那种“owner 测试钉 schema 字节与摘要向量”的要求来自 Factory 线的 P18 进入条件，不是 P06 裁决的定义。P07 开单时，要求 PREACCEPT 按 fail-closed 复核这一进入条件，并说明 P07 是否需要改动这两份合同。
  - F-2（“soul.resolved 绑定”）仍是残留项，没有变化。
- 开单时点：P07 的基线要用 M3，U5 QA 出结论之前不开 P07。
- 在途：
  - U5 QA：QA-REPORT.md 正在写（16:12Z 时 34640 B），尚未发终报。它的门禁已于 15:59Z 结束。
  - M19 QA：门禁运行中。16:12:35Z 读到 run_candidate_gate.py（pid 68458）已运行 02:05，按此推算约 16:10:30Z 启动，符合 M19 QA START 中“U5 QA 门禁结束后”的条件。
  - P01 只读摸底代理仍在运行。
- 自 Owner 最近一次输入以来，总管已发 1 条跨会话消息（M19 QA START），本条没有新发。

## 2026-09-27T16:16:47Z（总管 date -u）U5 收口：QA 结论 PARTIAL；P06 出口的认定；Soul/Host 窗口释放

- U5 QA 终报：16:14:29Z 送达，发件方 U5 QA session local_b98d0186-82c7-44c7-9d9a-4cfbe757772d（from-mode bypass）。
- 总管 16:15:31Z 复算：
  - QA-REPORT.md：018c6d99c585402728e52a01ab55e0df54a95c1aef8efa04fa00653581d9733a（35238 B）；
  - EVIDENCE_INDEX.json：6d842c74faad4ee10a9d3c00f2a6bb31bff9db85a4984164cca80550921d50cd（26832 B）；
  - 两者都与消息一致。
- 结论 PARTIAL，正式等级 NONE，外审 NOT_RUN。要点（摘自报告第 1–40 行与第 11 节）：
  - 合并正确：独立重做合并，冲突恰为 2 个文件，其余 1221 条路径逐条相同；diff 哈希等于 FREEZE；M3 的 author 与 committer 都是 U5-INT Builder。
  - 没有新增失败：21 个构建 0 警告 0 错误；41 个 required 套件 1248 项，1247 PASS，唯一 FAIL 是父线保留的 P11 用例。
  - 门禁 run 335546a8…（15:52:55Z–15:59:24Z）：53 项与两父逐项相同，48 PASS、5 FAIL；module-governance 67 行，等于 R，新增 0。
  - U4 阻断 #8 闭合：M3 上 policy-approval.integration 执行 46 项，高于 floor 40。
  - 剩余阻断全部来自父线：Phase0 5 项（module-governance；solution-locked-restore-build、evidence-service.unit、soul-memory-adapter.unit 归 P13/P14；legacy-runtime-adapter.static 归 P03/H3）、候选层 NOT_RUN、P11 用例。
  - 流程观察 B-O1–B-O4 都是 Builder 披露不完整，与候选内容无关：第一版单元根外观测的原始输出被覆盖；首次口令扫描的 stderr 原文没有保留；FREEZE.json 生成后于 15:25:20Z 用 sed 改了两处时间，REPORT 没有披露（总管 15:28:10Z 复核与 START 所引哈希都是改后文件）；FREEZE 中有 2 处不含口令的 DSN 模板。
  - QA 自报 10 项过程偏离（报告 8.4 节），其中一项：16:12:11Z 误以空脚本调用了一次 /usr/bin/python3，没有读写候选或证据。
- 总管只读核对：
  - U5 QA transcript：START 之后 387 条 assistant 记录（15:31:40.611Z–16:14:44.160Z）全部为 claude-opus-5-5 / max / max，permissionMode 为 bypassPermissions。
  - Builder clone 的 HEAD 仍为 M3，porcelain 0 行。
  - 16:15:31Z 时 56320–56339 无监听；U5 相关的 PG、门禁与测试进程都已不在。当时在跑的 run_candidate_gate.py（pid 68458）与 VBCSCompiler（pid 71323）属于 M19 QA。
- 收口：U5 以 PARTIAL 结束。M3 = 17438fb231ec02a489b1aad29088d7b9559bbe37 作为产品线当前集成头。Soul/Host 窗口释放，现无持有者。门禁位在 M19 QA 手中（按其 START 的条件预分配）。
- P06 出口的认定（总管裁定）：
  - P06 在本机能达到的出口以 S2R2 QA 的 PARTIAL 加 U5 QA 的 PARTIAL 为准。PDD P06 自动验收出口的四项中，定向 Unit/Contract 与真实 PG 链路、同请求回放一致、shadow 真实副作用 0、v1 带合法签名仍隔离，在 S2 QA E 节、S2R2 QA 与 U5 QA 中都有“符合”的记录。
  - 剩余阻断都是父线既有项，并已有责任包；F-2（“soul.resolved 绑定”）仍是残留项。
  - 据此，P07 的前置 P06 视为已出口；P07 的进入条件按本账本 16:13:06Z 条的核查结论视为满足。P07 PREACCEPT 仍须按 fail-closed 独立复核这两点。
- 下一步：以 M3 为基线开 P07（另条记派发）。U5 的 Builder 与 QA session 空闲，不再向它们发消息；U5 的清理项并入待 Owner 安排的统一清理。
- 自 Owner 最近一次输入以来，总管已发 1 条跨会话消息；本条没有新发。

## 2026-09-27T16:22:50Z（总管 date -u）P07 开单；P01 摸底入档

- P07 工单：coordination/WO-P07.md，sha256 e0c09495e29ecaa9b3773d593767b0b15d01d6a1e96ae71be77cd0ee07dd0a5a（17012 B）。要点：
  - 基线 M3（唯一父），候选只有一个提交 P7，提交身份为 P07 Builder <p07-builder@local.invalid>。
  - 写范围：Modules/operation-compiler/**；Modules/policy-approval/**（migrations 除外）；根 CHANGELOG.md；以及只能由生成器写的 compatibility.yaml 与 dependency-graph.yaml。
  - 以下事项须在 PREACCEPT 中提出、由 START 定案：公共合同变化、版本号、csproj 与包的变化。approval.readback/v1 与 approval.decision/v1 不得原地修改。
  - 自测与 QA 覆盖 OC、PA 与消费方闭包（command-orchestrator、control-plane-host、executor-gateway）的全部 required suites，数量不少于 U5 QA 第 4 节的 M3 实测值。
  - QA 对 PDD 验收出口逐项实测：2300ms 同步阻塞、有效请求、迟到 handoff 对账、未知 action/selector。门禁 --base 为 M3，对比 run 335546a8…。
  - 端口 56380–56399（Builder 56380–56389，QA 56390–56399），16:17:24Z 与 16:20:02Z 两次实测无监听。
  - 观察 O5 默认归 P08。本单元验收之前，不开其他写 OC 或 PA 的单元。
- 任务卡两张，总管于 16:22Z 用 spawn_task 开出：
  - Builder 卡：task_8b47e530，卡标记 P07B-K3V9。正文等于 scratchpad/card-p07-b.md（sha256 64f3f74c…，7339 B）去掉末尾换行。
  - QA 卡：task_e1633902，卡标记 P07Q-T5N2。正文等于 card-p07-qa.md（sha256 de113e32…，6217 B）去掉末尾换行。
  - 两张卡是否已被点开，总管以会话实际出现为准，不作推断。
- P01 摸底写入 coordination/research/P01-SCOPING-20260927.md，sha256 317010fb0be4f8d9bac318b3f14f3020e4074fd36c6b4e54b5c82fd15b307574（5525 B）。研究代理的完成通知于 16:14:14Z 送达，总管在 16:15:19Z 与 16:16:28Z 两次抽查。要点：
  - Mini 上 P01 从未开单；P01 是 P02、P05、P10、P17、P18 的前置。
  - 锁文件在 M3 与 M19 上 blob 相同。
  - scripts/start-test-postgres.sh（M3）默认端口 55432，使用 initdb --auth=trust，只输出一套当前 OS 用户身份，没有 admin/runtime 分离。
  - 现行夹具的超级用户引导出自账本第 267、309、333 行的裁定；PDD 第 288 行的 P01 出口要求“缺项必须标 INFRA_ERROR，不能借超级用户代替”。
  - P01 可能涉及的根锁文件、scripts/、Tools/ci/ 等路径没有模块 owner，都属于候选信任路径。
  - P01 是否开单、如何划定范围，总管另行决定，本条不作结论。
- 在途：M19 QA 的门禁运行中；P07 两张卡待开。
- 自 Owner 最近一次输入以来，总管已发 1 条跨会话消息；本条没有新发。

## 2026-09-27T16:38:13Z（总管 date -u）P01 不开单：前置 P00 没有实际出口；研究代理读到 0600 文件

- 16:26Z 前，总管派出只读研究代理核查 P00 的出口证据；完成通知于 16:35:52Z 送达。事实写入 coordination/research/P00-EXIT-EVIDENCE-20260927.md，sha256 acbf0682583a458c38a89f4044099c03e26754ba33d54d58d6e2a87bf690e188（4499 B）。
- 事实要点：
  - Air 与 Mini 两边都没有 P00 整包通过的记录。
  - Air 侧原件自写 p00_package_complete: false，见 D1 restore-result.json 第 82 行与 D2 result.json 第 201 行；总管 16:36:27Z 实测这两行，两文件权限均为 644。
  - D1 第 80–81 行：exact_recovery_check PASS，ordinary_checkout_byte_identity FAIL。
  - 交接包中没有 P00 的 QA 报告或收口记录；Air Coordinator 的对话原话是“因此 P00 还不能整体关闭”。
  - Mini 账本没有 P00 的验收记录。
  - 保全链 df835fb 等提交是 M3 与 M19 的祖先。
- 裁定：P01 不开单。
  - 依据：PDD §3.1 第 68 行“其它deps仍要求其实际出口”；P00 的局部交接（第 72 行）只允许 P03 开始外部信任准备；P01 的前置是 P00（第 266 行）。
  - 影响：Factory 线 M19 之后的 P18 同样不能开（前置含 P01）。产品线 P07 不受影响（前置为 P06）。
  - 此前的 P05、P10、P17 同样以 P01 为前置而没有取得其出口，这是合规审查已列出的“P01 缺位”问题，本条不改变既有记录。
- 需要 Owner 决定的事项（M19 收口时一并报告）：
  - P00 如何收口。P00 步骤 4 规定：若使用迁移归档，须先按技术书 16.2/H5 完成清单人工批准。Mini 是经交接包接续的，H5 属于真人审核门，总管不能代办。
  - P00 收口之前，P01 与 P18 都不开。
- 研究代理的越界读取（照实记录）：
  - P00 代理自报：一次 grep 显示了 P01 证据 summary.json（0600）的若干行，内容为 candidate_head、verification_level、formal_evidence_eligible 与若干 result 字段；同一命令读了同目录的 README.md（0600），没有显示；两次对整个交接包递归 grep -rlI 时，读到了 D1 的三个 0600 文件与 p02-git-preservation 的一个 0600 receipt，只输出了文件名。这些内容没有用作事实。
  - 总管补充：15:54:58Z 派出的 P01 摸底代理当时没有 0600 限制，也读过 p01-postgres 目录下的 0600 文件（该目录 4 个文件全部是 0600）。P01-SCOPING 第 1 节关于该目录的事实来自这些文件。
  - 总管 16:36:48Z 对三个研究代理的 transcript 与两份摸底文件做了模式计数，不输出内容。命中 3 行，16:36:56Z 遮蔽后查看，都是叙述性文字，没有口令值。带口令的 URI 与私钥头均为 0。这些 0600 文件是不是凭据文件，总管没有打开核实。
  - 之后派出的研究代理一律不得打开 0600 文件。
- 在途：M19 QA 门禁运行中；P07 两张卡（task_8b47e530、task_e1633902）待开，16:25Z 前后 list_sessions 中还没有 P07 会话。
- 自 Owner 最近一次输入以来，总管已发 1 条跨会话消息；本条没有新发。
- 2026-09-27T16:38:23Z 更正上条两处时间：P00 研究代理于 16:24:54Z 派出（上条写成“16:26Z 前”）；list_sessions 于 16:25:03Z 调用，结果中没有 P07 会话（上条写成“16:25Z 前后”）。时间取自总管 transcript 的记录时刻。
- 2026-09-27T16:38:37Z 补充上条：Mini 所用的交接包是否属于 PDD P00 步骤 4 所说的“迁移归档”（技术书 16.2），总管没有读技术书原文核对。上条的写法只是总管的理解，不是已核实的事实。

## 2026-09-27T16:57:08Z（总管 date -u）M19 收口：QA 结论 PARTIAL；门禁位释放；Factory 线停在 P18 之前

- M19 QA 终报：16:56:16Z 送达，发件方 M19 QA session local_59ab57dd-dbff-4d28-8ca3-64c56b073fab（from-mode bypass）。
- 总管 16:56:27Z 复算：
  - QA-REPORT.md：aa38c958575d3644a46f5ef204562314f8bdb01c5c46730870ea48dfced56b77（38022 B）；
  - EVIDENCE_INDEX.json：5998b383c92bad074fd1afd959bc79e42094b628fc55b0a141b05262bfdfdcb0（36706 B）；
  - 两者都与消息一致。
- 结论 PARTIAL，正式等级 NONE，外审 NOT_RUN。要点（报告“结论”一节与第 11 项）：
  - 合并正确：重做合并后只有根 CHANGELOG.md 冲突；其余 19 个路径逐 blob 等于改动它的一侧。
  - 没有新增失败：29 个 required suite 全部 PASS（非 PG 421 项，M16 8 项，M26 6 项）；中央 CI 261 项 OK。
  - 门禁 run 4f3f409b…：53 项与 FIX、P04F、P18A-INT 三次门禁逐项相同，48 PASS、5 FAIL；module-governance 75 行与先例相同。
  - 第 6 项：PDD:813 所列 P18 进入条件的两半在 M19 上都成立；P18 的前置 P01 没有开单。
  - 剩余阻断：Phase0 既有 5 项；factory-control-plane-host.postgresql18 与 factory-worktree-manager.postgresql18 为 NOT_RUN，归 P18；外审 NOT_RUN。
  - Builder 披露不准的三处（O2 对全盘做了名称检索未披露；O3 REPORT 所写 ls 的执行位置与日志不符；O4 Builder transcript 中有 2 条命令以字面形式写出 Owner 姓名），都不影响候选。
  - QA 自身的偏离：D-a、D-b（按总管此前的裁定写入报告）；D-c 越界写入 /private/tmp/x_added，已于 16:15:14Z 删除；D-d QA transcript 中出现 Owner 身份；Q-IE1–4 四次调用错误，各修正一次，原始输出保留。
- 总管只读核对：
  - M19 QA transcript（3585c974-…）：START 之后 373 条 assistant 记录（16:04:45.657Z–16:56:33.353Z）全部为 claude-opus-5-5 / max / max，permissionMode 为 bypassPermissions。
  - Builder clone 的 HEAD 仍为 M19，porcelain 0 行。
  - 16:56:27Z 时没有 PG、门禁、测试、VBCSCompiler 或 MSBuild 进程（O5 所说的 pid 71323 已不在）；56360–56399 无监听。
- 对 QA 提请判断的两项，总管裁定：
  - O4 与 D-d：session transcript 不算工单所说的“回执、日志与证据”。其中有 3 行是运行环境注入的 userEmail，任何 session 都无法避免。不改动，作为观察记录。今后扫描 Owner 身份时，needle 从 environment 内的 0600 文件读取，不在命令行写字面值；这一点写进下一次 START，不改工单与卡片模板。
  - O6：FIX QA 的 q01-identity.log 含带 Owner 身份的提交头。不改已冻结的证据，并入待 Owner 安排的统一清理清单。同类的还有 P04F Builder REPORT 与 U5 QA 回执第 167 行。
- 收口：M19 以 PARTIAL 结束。M19 = df120166469e5044977227798932c07808b87571 作为 Factory 线当前集成头。门禁位释放，现无持有者；Soul/Host 窗口也无持有者。
- Factory 线的下一步是 P18，但它的前置 P01 不开单（见 16:38:13Z 条：P00 没有实际出口）。Factory 线在此停下，等 Owner 决定 P00 如何收口。
- 自 Owner 最近一次输入以来，总管已发 1 条跨会话消息；本条没有新发。M19 的 Builder 与 QA session 空闲，不再向它们发消息。

## 2026-09-27T23:24:53Z（总管 date -u）P07 两个会话启动；Builder 档位校准

- 两个会话由任务卡开出，都是本总管 session 的子会话。
  - Builder：local_c9ff0e9e-2879-419f-a8cb-0630748c8124，createdAt 23:23:18.041Z，worktree unruffled-allen-30d5e1，transcript ea145023-989f-4186-8ed1-929409dd69e2.jsonl。
  - QA：local_406c978c-e0e2-4682-82b6-540e2cf4d129，createdAt 23:23:18.899Z，worktree bold-heisenberg-131b2b。
  - 会话由谁点开，总管不作推断。
- Builder 档位自检于 23:23:52Z 送达（from-mode bypass）。总管复核其 transcript：卡标记 P07B-K3V9 在；第一轮 8 条 assistant 记录都是 claude-opus-5-5 / max / max，属预期；第 5 行 permissionMode 为 bypassPermissions；units/p07 目录尚不存在，第一轮没有写入。
- 校准过程：
  - 23:24:14Z 用 set_session_effort 设为 medium，23:24:16Z get_session 回读为 medium；
  - 23:24:25Z 发出“档位已校准，开始 PREACCEPT”，msg_id 4853b0e6-e9e7-4e48-9cad-84f370192997；
  - 23:24:43Z 复核：Builder 新一轮已有 5 条记录，都是 claude-opus-5-5 / medium / medium。
- QA 会话 23:24:28Z get_session 显示 effort 为 max，正在做自检与 PREACCEPT。
- 自 Owner 最近一次输入以来，总管已发 2 条跨会话消息。

## 2026-09-27T23:39:37Z（总管 date -u）P07 Builder PREACCEPT 回执到达（只记到达与哈希）

- 回执 units/p07/builder/PREACCEPT-BUILDER.md 于 23:37:20Z 送达通知（from-mode bypass）。总管 23:37:39Z 复算 sha256 a88bf2b4e35e816cc8a932397a7c998424762a9500b90e17b1e1ad695675c532（40458 B），与消息一致。
- 为保持 QA 独立，QA 回执到达之前，本账本不写 Builder 回执的内容与总管对它的裁定。
- 等 P07 QA 的 PREACCEPT 回执；两份到齐后再下达 Builder START。
- 自 Owner 最近一次输入以来，总管已发 2 条跨会话消息；本条没有新发。

## 2026-09-28T00:01:56Z（总管 date -u）P07 两份 PREACCEPT 验收；Builder START 已下达

- QA 回执 units/p07/qa/PREACCEPT-QA.md 于 23:59:06Z 送达（from-mode bypass）。总管 23:59:21Z 复算 sha256 cc53ff73612ef98f481305b81ba8b08763241dc605753a3fdfb4815fda3b25b7（58610 B），与消息一致。QA transcript（ac108219-…）共 266 条 assistant 记录（23:23:25Z–23:59:19Z），全部为 claude-opus-5-5 / max / max，permissionMode 为 bypassPermissions。
- QA 回执要点：
  - 独立核对的结论与工单一致；进入条件按 RULINGS-2:60 在 M3 上成立。
  - 4 处需精确化：.venv 期望在 test_candidate_gate.py:373；M3 本身两个父；U5 Builder clone 的未跟踪文件无法用卡片允许的子命令核对；U5 门禁 run 的 base 是 L。
  - 自报偏离：两次只读 git rev-parse 不在卡片列出的子命令内。
  - 第 6 节 18 项疑问各附默认做法，提请 START 定案 6 项：reader 适配器位置、“签名”的含义、未知 selector 的口径、retryPolicy 与“不得盲重放”、真实路径 PG 端到端用例放在哪个 suite、同一请求重复调用是否算盲重放。
- Builder 回执要点（23:37:20Z 送达，sha256 a88bf2b4…；档位：第一轮 max，本轮第 63 行起全部 medium，bypass）：
  - 进入条件成立；默认不改 readback/v1 与 decision/v1。
  - 提案：OC 新增生产 reader，严格解码 readback/v1；boundary 用 Task.Run 覆盖同步阻塞；进程内 UNKNOWN 登记，持久对账归 P08；O5 归 P08。
  - 提请 START 定案 Q1–Q6 与“签名”的含义。其中 Q5 请求 PA.Tests 以测试专用方式引用 OC src，Builder 自己指出这与 PA AGENTS 第 21 行冲突。
- 两份回执独立得出同一个结构性结论：OC 与 PA 不能互相引用实现，而 Host 不在写域内，所以真实授权路径的进程内端到端用例在 P07 中没有合规位置。
- 总管裁定（写入 Builder START，QA START 将沿用同一口径）：
  - Q1、Q2、Q3、Q4 同意。
    - Q1：只改 Manifest，OC 消费 readback v1，PA 增加一条到 OC 的出边。
    - Q2：OC 对 PA 的依赖区间改为 >=0.8.0。
    - Q3：OC 不做本地时钟比较，过期交给 fence。
    - Q4：OC 升到 0.4.0，PA 不升版。
  - Q5 不准。依据：PA AGENTS 第 21 行字面禁止 Policy 引用 Compiler；模块规则只能收紧，总管不能放宽。
    - 验收出口②改为在模块边界内分两半取证：PA 侧用真实 PG 断言真实 EXECUTE 的 readback 满足 OC reader 的全部绑定条件；OC 侧证明生产 reader 加 boundary 对有效 readback 恰好编译出一个 allowlisted typed step。
    - 组合根内的进程内接线记为残留，归 P08（control-plane-host）。Builder 若判断两半取证不足，停下回报。
  - Q6：OC 的 floor 只升不降，不得超过实测值。
  - “签名”取 S1，即绑定 request_signature 的存在与格式，不做密码学验签。
  - 不另立 v2 promotion major；O5 归 P08。
  - 未知 selector：不符合 typed-ref 格式的一律失败关闭，P07 不引入登记表。
  - 不得盲重放：boundary 每次调用最多一次 AcceptAsync；UNKNOWN 的操作在对账前后都不得再次 handoff；此前已正常返回的同一请求再次调用，维持 M3 语义，由 Command 的幂等去重处理。
  - retryPolicy 标签不改，按行为判定。
  - 2300ms 用例的主判据是不得出现正常结果；次判据是 Builder 所报的时间窗，不放宽。
  - Soul/Host 窗口附条件预分配给 Builder，只用于 control-plane-host.integration。
- Builder START 于 00:01:31Z 发出，msg_id 708c4361-79fc-4173-b7bb-22f727b77802。正文 5722 B，sha256 071cb0c6c5fdc36d384f6730ceba8debbba1f210f8be468d7fc24c0132b97d71，等于 scratchpad/start-p07-b.md。START 晚于两份回执。
- 自 Owner 最近一次输入以来，总管已发 3 条跨会话消息。

## 2026-09-28T00:05:23Z（总管 date -u）P07 Builder 请示 selector 口径；总管更正 START 并裁定

- Builder 请示于 00:03:46Z 送达（from-mode bypass）。内容：START 要求“不符合 typed-ref 格式的引用一律失败关闭”，与 OC AGENTS 第 29 行冲突。其余工作继续，selector 相关代码在裁定前不写。
- 总管 00:04:36Z 在 M3 上只读核实：
  - OC AGENTS 第 29 行原文：“selector_ref and value_ref use the Planner-aligned 128-character opaque machine-reference syntax. Prompt text and coordinate-shaped references fail closed.”
  - OC 的引用语法（AllowlistedOperationCompiler.cs 第 143 行）为 \A[A-Za-z0-9][A-Za-z0-9._:,=-]{0,127}\z，不含下划线，接受 text=Submit。
  - Planner v2 的 typed-ref 语法为 selector_[a-f0-9]{64} 与 value_[a-f0-9]{64}（ActionProposalV2.cs 第 311–312 行）。
  - decision/v1 schema 对 selector_ref、value_ref 没有 pattern。
- 总管自认错误：START 中的这条口径照搬了 QA 回执 §6(c) 的说法，没有先核对 OC 模块规则。
- 裁定 B，00:05:02Z 发出，msg_id 7ec101f5-775f-4526-b63c-5d9885e13ea7，正文 1431 B，sha256 4b177ea38a6493beb236ac5a6c21494197cd866a32fa20ce79347e29d4bd9b7f：
  - START 的 typed-ref 口径作废；保留 OC AGENTS 第 29 行的语法，只在其内收紧，另外拒绝 <名>= 形式的定位器前缀（至少覆盖 text=、xpath=）；prompt、坐标形与超长照旧拒绝。
  - 具体匹配规则与已知上限写进 REPORT；现存正例若因此改变，逐条列出。
  - 已知缺口：Planner v2 的 typed-ref 在 M3 上就被 OC 语法拒绝，带 selector_ref 或 value_ref 的 v2 批准无法编译。对齐需要先经单独审查修改 OC AGENTS 第 29 行，属于治理变更，不在 P07。写入 P07 残留项；总管另行报告 Owner。
  - 出口② 的 OC 侧取证选用在现行语法下可以编译的 allowlisted action。
- QA START 将沿用更正后的口径。
- 自 Owner 最近一次输入以来，总管已发 4 条跨会话消息。

## 2026-09-28T00:32:48Z（总管 date -u）P07 Builder FREEZE 核验；QA START 已下达

- Builder FREEZE 消息于 00:22:44Z 送达（from-mode bypass）。
- 总管 00:23:01Z 起只读核验：
  - FREEZE.json sha256 2d276424ceea090c7f2bccf959f258016f2645faa73af9f77c69aac8a583ba4b（33001 B），REPORT.md sha256 8976f303aa5db3b4a1b057fd70fb0f28fcbd45058e8e1f14422c5fbaf11b4791（17205 B），与消息一致。
  - P7 = 835aba8432abe17ebefaa49ffadcb6e6966380aa，tree 3d9e9d83a589ec53204709d8d0c46db4b33bbc5f，唯一父 M3；分支 codex/p07-17438fb；porcelain 为空；author 与 committer 都是 P07 Builder <p07-builder@local.invalid>，未签名。
  - 统一口径 diff M3..P7：92151 B，sha256 64b0b0ad77edecc262d3d1919ec72765a0a4d7ae25a82040b4367719aa8d05e3，等于 FREEZE 的 freeze_diff。
  - 改动 18 个路径，全部为修改，都在 WO §3.1 写域内，没有禁区路径。governance 下只有 compatibility.yaml 变化（+43 行），dependency-graph 未变。OC csproj 只改 Version 0.3.0→0.4.0，没有包或引用变化。
  - OC module.yaml：版本 0.4.0；对 PA 的依赖区间 >=0.8.0 <1.0.0；consumed、inbound 与 supportedContractMajors 增加 approval.readback v1；三个 suite 的 floor 由 14/11/10 升到 15/12/14，等于 REPORT 所报实测数。PA module.yaml 只增一条到 OC 的 readback 出边，未升版，floor 未动。
  - REPORT 所称 PA 侧 (a) 用例的写入入口 EvaluateSignedAsync，是 M3 已有的测试扩展方法（PostgresPolicyApprovalIntegrationTests.cs:1002），转发到生产 EvaluateAsync。有无预置行，交 QA 核实。
  - 档位：Builder transcript 中 START 送达记录为 00:01:31.275Z，permissionMode bypassPermissions；此后到 00:22:59.824Z 共 200 条 assistant 记录，全部为 claude-opus-5-5 / medium。
  - 窗口：自检记录 Reports/ci/p07-builder/READY-FOR-PG-WINDOW.md（626 B，sha256 e253f2898632d4cb51ed93afc3cd4a29b8f8a07e901142786c8ea0991cdf544b）。00:18:11Z 两项自检成立，未发 READY 直接进入；00:18:14Z–00:18:36Z 运行 control-plane-host.integration，rc 0；/private/tmp/dps-* 前后快照各 59 项，diff 为空。
  - 资源：Builder 的 PG 于 00:20:04Z 停机。00:30:34Z 复查：没有门禁、CPH/SoulRegistry 测试或 postgres 进程，563xx 无监听，VBCSCompiler pid 63551 已退出，/private/tmp/dps-* 仍为 59 项。
- Builder 自报：18 个 required suite rc 0；failing-before 状态 A、B 与预写预期一致；locked build 无 NU1xxx；治理校验 rc 0；进程内 validate_governance 在 M3 与 P7 上都是 67 条，零新增（开发辅助，非官方）。停止事项：无。
- REPORT 的披露（交 QA 判定）：
  - 截止时间内 consumer 抛异常的交接，也按 UNKNOWN 留在登记表中，比 M3 严；
  - 选择器前缀规则的上界：嵌套形式如 frame:text=x、a,text=x 仍通过；
  - 开发期预跑的 log 被同名文件覆盖，原文未保留，不属于正式自测；正式自测各跑一次；
  - PA (a) 用例在首次运行前删去两条未经核实的行数断言。
- REPORT 残项：composition root 接线、进程内端到端证明、UNKNOWN 持久对账、O5 都归 P08；Planner v2 typed-ref 与 OC 语法的对齐需单独治理变更；不授予验证等级；外审 NOT_RUN。
- QA START 于 00:31:55Z 发出，msg_id af79b0e6-f8c3-4a96-a97d-db5be5de1e7e。正文 8087 B，sha256 2614be36572ea57ffa8abc5f8f1eb1671ef6e8a78eeabc4ca7f7561b540ec7ab，等于 scratchpad/start-p07-qa.md。发送前核对：QA 进程 pid 48830 在运行，transcript 中 permissionMode 为 bypassPermissions，上一条消息（23:59:06Z）的 from-mode 为 bypass。
- QA START 要点：
  - (a) 适配器位置与 (g) 出口② 按 Q5 的两半口径判定。(a) 半在测试内重述的条件，须逐条对照 OC reader 与 compiler 的代码。缺少进程内贯通的具名用例，本身不判 NOT PASS，但须写明未证明的部分与责任包 P08。
  - (b) 签名取 S1，格式合法但被篡改记为已知限制。(c) selector 按裁定 B；Planner v2 typed-ref 被拒、嵌套形式通过，都记为残留。(d)(m) 不得盲重放的硬性要求同 Builder START。
  - 对 REPORT 第 8 节第 3 点，总管说明了 START 的意图：截止内 consumer 抛异常按 UNKNOWN 处理，不算越出 START。QA 核实用例覆盖、披露与 M3 回归。
  - (f) 主次判据同 Builder START。(h) O5 归 P08。其余各项同意 QA 的默认做法。
  - 窗口与门禁位都附条件预分配给 QA；自检不成立时约每 3 分钟复查一次，60 分钟后停下回报。
- 自 Owner 最近一次输入以来，总管已发 5 条跨会话消息。

## 2026-09-28T01:39:56Z（总管 date -u）P07 QA FINAL：NOT PASS；总管更正 typed-ref 判断；返修 START-R2 已下达；Owner 新输入

- Owner 01:30:38Z 在本 session 输入两项：(1) 要一份清单，并用大白话说明 P00 收口是怎么回事；(2) 同意新开一个单元，把 OC 的引用语法与 Planner v2 对齐。第 (2) 项依据的是总管的一个错误判断（见下），该单元不开。
- P07 QA FINAL 于 01:35:22Z 发出（QA transcript 记录，from-mode bypass）。总管 01:36:10Z 复算：QA-REPORT.md sha256 b20d2040aff46b174d585b89bed5d9f100132c52104f6c8cba2ffd934666e82e（46712 B），EVIDENCE_INDEX.json sha256 271c4fa36335ac4966e95804933d44916ce6ba2e52676cb9d09e207a49873b6f（25463 B），与消息一致。QA transcript 自 QA START 起共 539 条 assistant 记录，全部为 claude-opus-5-5 / max。
- 结论 NOT PASS。PDD P07 三个出口在 P7 上实测成立；出口② 按 START 的两半口径成立，进程内贯通未证明，归 P08。判 NOT PASS 的原因：
  - N1 新增失败：门禁 required check phase0-adversarial-unit-tests 在 M3（U5 run）上 PASS、在 P7 上 FAIL。P7 按 Q6 把 OC 的 manifest floor 升到 15/12/14，而 governance/policies/candidate-test-policy.yaml 中 OC contract 为 11、crypto-storage-simulation 为 10，打破了“策略 floor 不得弱于 manifest floor”。门禁 01:17:27Z–01:22:23Z，run a588ea7bff91f11b05346361367e8ce3：53 项中 47 PASS、6 FAIL，其余 52 项与 U5 一致；module-governance 67 行，log 与 U5 相同，零新增。
  - D1：P7 新增的三处文档（根 CHANGELOG:9、OC CHANGELOG:10、OC contracts/consumed/README:12）写“Planner v2 typed ref 被 OC 语法拒绝”。QA 实测在 M3 与 P7 上都被接受。
  - D2：截止时间内 consumer 抛异常按 UNKNOWN 留存、再调用被拒，行为符合 START，但没有具名的 duplicate-delivery 用例，OC 与根 CHANGELOG 也没有如实披露。
  - 流程：Builder 用了 linked worktree（00:15:35Z 建，00:19:28Z 删）；vg 原始日志被覆盖；/private/tmp 中 dotnet test 产生的 socket 写入没有披露。QA 自身两处越出写域（/private/tmp 进程表临时文件，已删；dotnet test socket），报告第 13 节已披露。
- 总管自认错误（D1 的来源）：
  - 00:04:36Z 核读 M3 时，把 OC 正则的字符类 [A-Za-z0-9._:,=-] 读成“不含下划线”。总管 01:36:10Z 复核：该字符类含 `_`，selector_ 加 64 位十六进制（73 字符）与 value_ 加 64 位十六进制（70 字符）都能匹配。
  - 所以 00:05:23Z 条中“不含下划线”与“Planner v2 的 typed-ref 在 M3 上就被 OC 语法拒绝”两句是错的。裁定 B 据此写入的“已知缺口”作废；裁定 B 拒绝 <名>= 前缀的部分不变。QA START 二.4 沿用了这一错误前提。
  - 总管在上一轮回复中把这个不存在的缺口报告给了 Owner，本轮向 Owner 更正。Owner 据此同意的对齐单元不开；改为在 P07 返修中删除假话，并加一个 typed ref 能编译的正例，钉住现状。
- 总管对 QA 停止事项的裁定：
  - N1：manifest floor 不得高于策略中的显式 floor。OC contract 改回 11，crypto-storage-simulation 改回 10；unit 在策略中没有显式 floor，取修复 commit 上的实测数。不走提高策略 floor 的治理路线。Q6 没有核对这一仓库不变式，是总管的疏漏。
  - D1：接受 QA 判为缺陷。删除三处假话本身，不加解释段落；加一个 Planner v2 typed ref 能编译的具名正例。
  - D2：接受 QA 判为缺陷。补具名 duplicate-delivery 用例（须在 M3 上失败）；在 OC 与根 CHANGELOG 如实披露；改正 OC CHANGELOG 第 6 行的不准确表述。
  - QA 11.4 所说 PA 测试注释的位置记为残留，本轮不修，避免返修扩到 PA 与 PG。
  - 流程项：Builder 本轮不用 linked worktree，原始日志不覆盖，并在 REPORT-R2 补记 R1 未披露的写入。QA 自身的越界写入照实记录，不另作处置。
- Builder START-R2 于 01:38:47Z 发出，msg_id 026ea4ae-8c3e-4f89-9729-66d3b5b6921d，正文 5096 B，sha256 603e7a0f89fa2638331a6b97421c1c13175b9cd6c20fccce914f1b163d01d593，等于 scratchpad/start-p07-b-r2.md。Builder 会话 01:38:47Z 收到，permissionMode bypassPermissions；get_session 显示 effort 为 medium。
  - 要点：在 P7 上只提交一个修复 commit；写域限 OC（不改 src）与根 CHANGELOG；自测加跑 Tests/ci（预期 Ran 243，OK）与治理校验；OC src 不变，所以不跑消费方 suite，不用 PG、窗口和累计门禁。
- 窗口与门禁位：P07 QA 的门禁已于 01:22:23Z 结束，两者此刻都空闲。返修复验时再附条件分给 QA。
- 自 Owner 最近一次输入（01:30:38Z）以来，总管已发 1 条跨会话消息。

## 2026-09-28T01:46:37Z（总管 date -u）P07 Builder FREEZE-R2 核验；QA START-R2 已下达

- Builder FREEZE-R2 于 01:44:16Z 发出（Builder transcript 记录，from-mode bypass）。
- 总管 01:44:31Z 起只读核验：
  - FREEZE-R2.json sha256 d6f7a25d699bc77c4c7f4be890a14c53a6aec01727178ffaf6e256d0be960935（8764 B），REPORT-R2.md sha256 6a57c62903bec6271ffac8f44f19419cd8369cb4876267103b7841c6b408585f（8379 B），与消息一致。R1 的 FREEZE.json 与 REPORT.md 哈希未变。
  - R2 = 0518e1f2cedd84fde0f20b88158a94ddb0f1d329，tree abe974e27877c9dd5981eff828204875db692c92，唯一父 P7；分支 codex/p07-17438fb；porcelain 为空；clone 只有主工作树，没有 linked worktree；author 与 committer 都是 P07 Builder <p07-builder@local.invalid>，未签名。
  - 统一口径 diff P7..R2：26549 B，sha256 ee481306c31a5ed3d5763b977de44975f7e1b741fa245b315642b56f023ebebf，等于 FREEZE-R2 的记录。
  - 改动 7 个路径，全部为修改：根 CHANGELOG.md；OC 的 CHANGELOG.md、contracts/consumed/README.md、module.yaml、tests/README.md 与两个测试文件。OC src、PA、governance、AGENTS 都没有改动。
  - OC floor 为 unit 16、contract 11、crypto-storage-simulation 10。三处“typed ref 被拒”的句子已删除；根与 OC CHANGELOG 写明了截止内 consumer 抛异常按 UNKNOWN 留存、再调用被拒这一相对 M3 的行为变化；OC CHANGELOG 第 6 行已改正。
  - REPORT-R2 §2 另披露两处连带更正（OC CHANGELOG 的 Manifest 条、tests/README 的用例数），都在写域内。
  - 档位：Builder transcript 中 START-R2 送达记录为 01:38:47Z（bypassPermissions），此后到 01:44:30Z 共 50 条 assistant 记录，全部为 claude-opus-5-5 / medium。
  - 资源：01:45:02Z 没有 PG、门禁或测试进程，563xx 无监听；Builder 返修期间启动的 VBCSCompiler pid 18126 仍在；/private/tmp/dps-* 为 59 项。
- Builder 自报：OC unit 16、contract 12、integration 15，rc 都是 0；Tests/ci Ran 243，OK；generate_governance 校验、validate_repo、diff --check 的 rc 都是 0；D2 用例在 M3 上按预写预期失败（AcceptCalls 期望 1、实际 2）。停止事项：无。
- QA START-R2 于 01:46:12Z 发出，msg_id 43dc61df-b585-489b-a677-fbb30a5d8086，正文 4833 B，sha256 280c0ccd59ac6882629a2cee5b0cb727c96099012f5a3424d600707eae4ee2d2，等于 scratchpad/start-p07-qa-r2.md。QA 会话 01:46:12Z 收到，permissionMode 为 bypassPermissions。
  - 复验范围：身份与 diff；N1 重跑 Tests/ci 并跑门禁一次（预期 48 PASS、5 FAIL，与 U5 逐项一致，module-governance 67 行零新增）；D1 与 D2 的用例和披露；重跑 OC 三个 suite。OC src、合同与 PA 自 P7 起未变，第一轮的消费方重跑结论继续有效。
  - 不需要 PG 与窗口；门禁位附条件预分配给 QA。结论针对最终候选 R2，只能是 PARTIAL 或 NOT PASS。
- 自 Owner 最近一次输入（01:30:38Z）以来，总管已发 2 条跨会话消息。

## 2026-09-28T02:17:51Z（总管 date -u）P07 收口：QA 复验 PARTIAL；P00C 单元开出（只派 QA）

- Owner 02:11:38Z 在本 session 同意开 P00 收口单元（原话“P00 ,可以。”）。
- P00C：
  - 工单 coordination/WO-P00C.md，11314 B，sha256 54196f93194b6dc143c65d0fe0f34f351edbb1051780305dc426799d9e4a52d0。只派 QA，不写代码；QA 发现需要改候选的缺陷时停下回报，再另开 Builder 单元。
  - 总管 02:12:29Z 起只读核实：保全链六个提交都在 history.git 中，父子关系为 cac7ccb → ab96e2e → 2dff2fe → 5b7d16c → df835fb → 4dfe700；df835fb 树中 1138 个文件；4dfe700 只改 .gitattributes 与 CHANGELOG.md，并新增 Tests/ci/test_git_byte_preservation.py。
  - Air 恢复审查的汇总摘要 53bee058… 已复算成功：对按路径排序的“path + \0 + sha256 + \n”取 sha256，用 raw-blob-files.json（644，sha256 80c1f9fe…）计算，结果与之相等。
  - 更正总管 01:39Z 前后回复 Owner 时 P00 清单的第 4 项：Air 的 D2（result.json，09:47:01Z 完成）其实在恢复出的 checkout 上跑过 legacy 的三个 suite，共 44 个方法，全部通过；static verifier 退出码 3。restore-result 中的 NOT_RUN 记于 09:30:59Z，早于这次运行。总管称“没在恢复出来的代码上跑过”，这是错的。
  - QA 任务卡于 02:16:49Z 发出，task_ca662b5b，卡标记 P00CQ-H7W4，卡文 5438 B，sha256 867d4ed5320b92ce52f512699b1c70d1ea1f84e412cabd53e554b2f8980a6a49。等人点开；会话由谁点开，总管不作推断。
- P07 QA FINAL-R2 于 02:14:58Z 发出（QA transcript 记录，from-mode bypass）。总管 02:16:58Z 复算：QA-REPORT-R2.md sha256 7ce6f6a7ef5e48853ed4275429fed154c3ab9ee16554068f16234883b6aed390（30628 B），EVIDENCE_INDEX-R2.json sha256 92e2945cd23d93ed3192157cd28ba55b9d1d7a9453f22aebb8efd158df82b0ba（12244 B），与消息一致；第一轮两份文件的哈希未变。QA transcript 自 START-R2 起共 306 条 assistant 记录，全部为 claude-opus-5-5 / max。
- 结论 PARTIAL，对象为 R2 = 0518e1f2cedd84fde0f20b88158a94ddb0f1d329：
  - N1、D1、D2 在 R2 上逐项复验，均已修复。OC 三个 suite 为 16/16、12/12、15/15。
  - 门禁 02:01:02Z–02:06:35Z，run 34ba03dffd9145ed4a19785475d95d5b：48 PASS、5 FAIL，与 U5 run 335546a8 在全部 53 个位置上一致；module-governance 67 行，log 相同，零新增。
  - PDD P07 三个出口沿用第一轮在 P7 上的实测，因为 R2 没有改 OC src、OC 的 provided 合同与 PA。
- 总管对 QA 停止事项的裁定（U5 门禁输出中的 0600 文件）：
  - 冲突是总管造成的：START 字面禁止打开其他单元的 0600 文件，而工单第 3.4 节第 7 项要求与 U5 run 335546a8 逐项比对，逐项数据只在门禁写成 0600 的输出中。
  - 裁定：门禁 run 目录（candidate-runs/**）中由门禁写出的证据文件，在工单要求比对时可以读取；其他 0600 文件（口令、身份、environment 等）仍不得打开。以后的 START 按此写明。
  - 本单元两轮的读取记为已披露的偏离，不判缺陷。第一轮 §13 的不实陈述已由 R2 §13 第 8 条更正。结论不依赖这些文件，因为不打开它们的比对得出相同结论。
- P07 出口裁定：P07 的出口 = QA 在 R2 上的 PARTIAL。产品线当前头为 R2（units/p07/builder/DSP_ZD，分支 codex/p07-17438fb）。
- 残留与责任包：
  - P08：组合根接线；进程内贯通用例；UNKNOWN 持久对账，在此之前组合根须保证每进程只有一个 boundary 实例（QA R2 观察 1）；Command 层 crash-window 与 UNKNOWN 恢复；O5。
  - 下一次改 OC src 的单元：OperationCompilationBoundary.cs:106 的注释措辞（QA R2 观察 2）。
  - 既有：S1 签名限制（P06 冻结合同的性质）；选择器嵌套形式的上界；两份 schema 的字节与 readback 摘要向量未被测试钉住；PA 测试注释位置；Phase0 既有 5 项 FAIL。
- 资源：窗口与门禁位都已释放，此刻无人占用。P07 的 Builder 与 QA 会话都已完成，保留待清理。
- 下一步：产品线按 PDD 为 P08（前置 P07 已有出口；P04 按 §3.1 交接表提供冻结的接口成果）。总管先做只读摸底，再写工单。
- 自 Owner 最近一次输入（02:11:38Z）以来，总管没有发跨会话消息。

## 2026-09-28T03:06:06Z（总管 date -u）P00C QA START 已下达；P08 摸底结果；P08 切片 A 开出

- P00C：
  - QA 会话 local_3c597704-a052-4e20-8fff-dee45485272c，由任务卡开出（QA 自报 transcript 首条 user 记录为 02:25:11Z）。
  - PREACCEPT 回执：消息 from-mode bypass。units/p00c/qa/PREACCEPT-QA.md 50983 B，sha256 10c9eec82a399f0711b1ed03a759a97ecc88da6f1488c9987a83c9ccf447b81f，总管 02:53:02Z 复算一致。QA 自报档位 claude-opus-5-5 / max / bypassPermissions，截至 02:43:15Z 共 120 条 assistant 记录。
  - 总管抽查属实：
    - history.git 的 HEAD 指向 refs/heads/main，没有 refs/heads 与 tags；
    - refs/sources/project/heads/codex/dps-pdd-v2-1-build 的尖端为 3e47ac75f2608ad34f39d73c7f5bb73fdf5246d0，4dfe700 是它的祖先，相差 6 个提交；
    - df835fb:.gitattributes 第 33 行已有 `*.ps1 text eol=crlf`；
    - history.git 中没有 mtime 晚于 2026-09-27 00:00（本地时间）的文件；
    - p02 复核 review.json（2cca1733…，644）的 result 为 PASS，reviewed_head 为 4dfe700。
  - 工单更正（总管的错误）：WO-P00C 第 1.1 节第 4 条有两句与现场不符，作废。
    - 更正后的事实：4dfe700 为 14 个路径加了 -text，并给 9 条 legacy C# 规则加了 whitespace=…；`*.ps1 text eol=crlf` 在 df835fb 上已经存在；新增测试在 Air 上有过一次 P02 独立复核（PASS），Mini 侧从未复验。
    - 工单文件不改，因为它的哈希已被任务卡绑定。
  - 裁定：回执第 6 节的 14 项默认做法全部采纳。要点：
    - clone 之后只 fetch 上述 ref；
    - df835fb 的 failing-before 在 clone-a 的首次普通检出上测，然后做原字节恢复；clone-b 只做一次 4dfe700 的首次普通检出；
    - status 可能少报，以往返核对为准；
    - 真阳性条目的前 4 个字符改为 ****；扫描阈值预先声明，之后不调；
    - H5 按 PDD 第 195 行判不适用，技术书 16.2 未读记为限制；
    - D1 的三个 0600 文件继续禁止打开。总管 02:17:51Z 的例外只覆盖 Mini 上其他单元门禁运行目录，不适用于交接包。
  - 回执 1.4 所列偏离按已披露接受：history.git 上用了清单外的只读命令；$TMPDIR 与 scratchpad 中写过 3 个临时文件，已删除；读了清单外的 644 数据文件。
  - QA START 于 02:54:59Z 发出，msg_id 96776dfa-fa13-4818-8b5e-af17e22031db，正文 4622 B，sha256 eee3ee14b002ce3533921fe651a1d3ba065d1c55e933b855a218f4d870c43da2，等于 scratchpad/start-p00c-qa.md。本单元不用 PG、端口、窗口或门禁位。
- P08 摸底：
  - coordination/research/P08-SCOPING-20260928.md，8716 B，sha256 95cd5d1fe8d3825ffb1955da87f397c6e866b6dd41378e45efc43a0210518d60，只作数据。
  - 其中第 9 节是总管的更正：Tools/ci/phase0.py 第 1567–1640 行只禁止生产工程跨模块引用非合同包，不含 PolicyShadowChainProcess。把 test-only 入口限定到该目录的，是总管在 P06 的协调裁定 RULINGS-3 §1，不是仓库门禁。
- P08 的切片与 P08A 开单：
  - 切片计划：A 做 Policy 侧的 typed 签名请求、端口、provider 与签名账本，Command 不改；B 让 Command 改用新端口；C 做 Host 组合与端到端证明；D 处理 O5。后续切片开单时按现场重定。
  - 前置认定：
    - P07 不在 PDD §3.1 交接表中，须有实际出口。P07 的 QA PARTIAL 记为 P07 的实际出口，沿用 P06→P07 的先例。这是知情接受，不是 PASS。
    - P04 按 §3.1 交接表提供冻结的接口设计。
  - 工单 coordination/WO-P08A.md，24679 B，sha256 22a608a575d64b188000e45b80c95577429287164771a0aee1dd413ca65ddd50，基线 R2。要点：
    - 写域为 policy-approval、根 CHANGELOG，以及由生成器写的 governance/modules。migrations 只能新增 006。
    - AGENTS.md 与设计文档只允许更正变成假话的句子，由 START 定案，不得新增规范性条文。
    - 其他模块一律不写。
    - 新合同、端口、密钥注入、合同声明、006、authority 清单、版本号与 floor，都须在 PREACCEPT 中提出、由 START 定案。
    - 测试密钥只在运行时生成；生产路径失败关闭。
    - QA 门禁用 --base R2，与 run 34ba03df… 逐项比对。结论只能是 PARTIAL 或 NOT PASS。
  - 总管 02:57:36Z 实测：563xx 无监听，没有 PG、门禁或测试进程；VBCSCompiler 18126 已不在；/private/tmp/dps-* 为 59 项。
  - Builder 卡于 03:04:45Z 发出，task_f788014f，卡标记 P08AB-R6M3，卡文 7685 B（含结尾换行），sha256 6bd63669e9e95b585d36efb714fb5fbbbf6c02e9132583e339f4abdc5bfd1661。
  - QA 卡于 03:05:07Z 发出，task_5957f897，卡标记 P08AQ-W2J8，卡文 7050 B（含结尾换行），sha256 de536267a733c52aa81df52cf780b6e1a2ec0adf8f9b96885abdbbe1ffd2a000。
  - 两张卡等人点开；会话由谁点开，总管不作推断。
- 资源：窗口与门禁位此刻空闲。P08A Builder 在 START 之后才用 PG 与窗口。
- 自 Owner 最近一次输入（02:11:38Z）以来，总管已发 1 条跨会话消息。

## 2026-09-28T03:18:54Z（总管 date -u）P00C QA 停止回报与裁定；P08A 两个会话开出，Builder 已校准

- P00C QA 停止回报于 03:16Z 前后送达（from-mode bypass）。
  - 不符之处：回执 F3（ls-files -s 对 hash-object --stdin-paths）在 df835fb 首次普通检出上报出 16 个路径，预期 13 个。多出 CHECKLIST.txt、Config/PersonaPrompt.txt、QUICK_START.txt。
  - F2 与预期完全一致：1138 个文件，16016983 B，摘要 eee2ca7e…，只有 ps1 与 blob 不同。F1 只报 1 条，属于回执预见的 racy 少报。
  - QA 的旁证：用临时 index（read-tree df835fb，stat 全为 0）强制全量内容比对，status 恰为 13 条 .M，与 Air 的 ordinary-status.z（e97a8955…）逐字节相同；加上 ps1，failing-before 共 14 个路径，与 restore-result 一致。
- 总管 03:17Z 起只读核验：
  - report/checks 下 00-inputs.json 6a83e655…、10-a-failing-before.json 53554f9b…、10b-a-diag.json 375c8232…，与消息一致；environment/tmp 为空。
  - 三个多出的路径在 df835fb 与 4dfe700 上都是 text: auto、eol 未指定；df835fb 中 blob 的 CRLF/LF 数为 36/51、559/559、123/167。
- 裁定（03:18:28Z 发出，msg_id 8d6570d0-8cb3-4e5d-8782-7f9fee56d0f6，2124 B，sha256 60085af806ead44b00a2453919d1ec61db59afb31a0bd9bfdd8a5d31c023375a，等于 scratchpad/ruling-p00c-1.md）：
  - 多出的 3 个路径是回执 F3 方法的缺陷，不是候选的缺陷。git 对 text=auto 的文件，已按 CRLF 提交的不做转换，hash-object 不读 index，所以误报。
  - df835fb 的 failing-before 由临时 index 全量内容比对仲裁。临时 index 只放在 environment/tmp，用后即删。
  - 4dfe700 的判据：普通 status 为空；临时 index 全量比对为空；1139 个路径的工作区字节与 blob 逐一相等。hash-object 往返照录，报出的恰为这 3 个路径不算缺陷，其他任何路径即为缺陷。
  - 另记观察，不判缺陷：这 3 个文件在 4dfe700 上仍是 text=auto 且入库内容带 CRLF，renormalize 之类的操作会改动它们。
- P08A：
  - Builder 会话 local_96599292-046a-4015-917d-59de2c9cbed6（03:05:11Z 创建）；QA 会话 local_9ad936fd-2827-4111-b5f1-cfb5e394d50e。两张卡都已被点开；由谁点开，总管不作推断。
  - Builder 档位自检送达（from-mode bypass）：9 条 assistant 记录为 claude-opus-5-5 / max / max，permissionMode 为 bypassPermissions，CLAUDE_CODE_EFFORT_LEVEL 未设置。总管在其 transcript 中核实一致。
  - 总管 03:06Z 调用 set_session_effort(medium)，get_session 回读为 medium。“档位已校准，开始 PREACCEPT”于 03:06:39Z 发出，msg_id c8d11981-6a53-43fd-9f39-b1a17ebb4a07，615 B，sha256 3ac0bd4caeabe499e9f601d52c5e430cfc7215fdced04237f5328e618d750535。Builder 下一轮 03:06:43Z 起的记录为 medium / medium，permissionMode 为 bypassPermissions。
  - 两份 PREACCEPT 都未到。
- 自 Owner 最近一次输入（02:11:38Z）以来，总管已发 3 条跨会话消息。

## 2026-09-28T03:23:56Z（总管 date -u）P08A Builder PREACCEPT 已到并核实哈希；更正上一条的送达时间

- P08A Builder PREACCEPT 回执于 03:20:36Z 送达总管 transcript（from-mode bypass）。units/p08a/builder/PREACCEPT-BUILDER.md，39030 B，sha256 0f61362612232bf4ab7364ad38a5e35cd9418253cf17c0c284e6660b9c23a648，总管 03:20:44Z 复算一致。回执写于 Builder 的 medium 轮。
- 按既定做法，QA 的 PREACCEPT 落盘之前，本账本只记哈希与“已核实”，不写回执内容。两份回执都到齐后，总管才下达 Builder START。
- 更正上一条（03:18:54Z）：P00C QA 停止回报送达总管 transcript 的时刻是 03:17:15Z，不是“03:16Z 前后”。03:16Z 是 QA 在正文中自报的写作时刻。
- 自 Owner 最近一次输入（02:11:38Z）以来，总管已发 3 条跨会话消息。

## 2026-09-28T03:50:31Z（总管 date -u）P08A QA PREACCEPT 已到；两份回执要点；Builder START 已发

- P08A QA PREACCEPT 回执于 03:38:49Z 送达总管 transcript（from-mode bypass）。
  - units/p08a/qa/PREACCEPT-QA.md，59019 B，717 行，sha256 ccd3e4a83ec13c1f1e848f16c85b6f7e274acef94a7ec8e21eace74e93605473，总管 03:39:20Z 复算一致。
  - QA 自报：03:05:20Z 档位自检为 claude-opus-5-5 / max / max，permissionMode bypassPermissions，CLAUDE_CODE_EFFORT_LEVEL 未设置；未读 Builder 回执，未与 Builder 通信。
- 两份回执都已落盘，现在记录要点。
  - Builder（0f613626…）：
    - 新合同 execution.authorization.signing.request/v1，成员按设计 175–184 行；端口 IPolicyExecutionAuthorizationSigningPortV1，放在 Policy.Contracts；internal 端口 IPolicySigningActivationAuthority，生产无实现即关闭。
    - migration 006 新建只追加的 execution_authorization_signatures 表，主键 retry_identity = command_id:lease_id:attempt；锁序 PolicyRuntimeLock → ApprovalLock。
    - 推荐合同声明选 C（切片 A 不声明）、不拆分；版本升到 0.9.0。
    - 结构性发现：phase0.py 第 1567–1640 行不许生产工程跨模块引用合同包以外的工程，所以生产组合根不能引用 Dps.PolicyApproval。
  - QA（ccd3e4a8…）：
    - R2 基线与工单 §1.1 的 9 条一致。
    - 工单漏写一处：CPH 的 tests/PolicyShadowChainProcess 直接引用 PA 实现。
    - 需 START 定案的有 U1–U14。主要是签名时的 rate budget 语义（U1）、kill switch 来源（U2）、新合同的 communication 边（U3）、签名账本的写入角色（U5）、重试身份（U6）、更正清单（U7）、PolicyApprovalContractJson.cs 的归属（U13）。
- QA 越界写入：
  - 03:17:33Z–03:18:49Z，QA 把 8 个 R2 tracked 文件的副本写进了它自己的 Claude 会话 scratchpad（…cool-borg-686be2/0940b96f-1446-44c9-b9e8-326e96b24eb4/scratchpad/），违反卡第 3 节“唯一允许的写入”。QA 在回执 §1.4 中自行披露，没有删除。
  - 总管 03:49Z 核实：8 个文件的 sha256 与 R2 blob 逐一相同。
  - 处置：作为已披露的偏离接受，不删除，列入待 Owner 清理的清单。QA START 中写明不再写入。
- 总管为 START 核实的代码事实（R2，只读）：
  - PostgresPolicyRuntimeStateSource.cs 第 133–150 行按整个 scope 合计已消耗单位，第 187 行把有效剩余截到 0。评估在 PolicyRuntimeLock 下进行（PostgresPolicyApprovalService.cs:642）。
  - 锁文件记录的 PA.Contracts 区间与 csproj 的 0.8.0 并不一致，有 [0.6.0, )、[0.5.0, )、[0.1.0, )。3224cc2 把 PA 从 0.7.0 升到 0.8.0 时，改动的锁文件全在 CPH。
  - candidate-test-policy.yaml 对 policy-approval 只有三个 floor：contract 22、schema-contract 5、integration 40。Tests/ci/test_candidate_policy.py 第 387–400 行只对 contract 与 integration 比较 manifest 与策略的 floor。
  - PolicyApprovalContractJson.cs 是 Policy.Contracts 中的 internal static class。
- Builder START：
  - 03:49:15Z 发出，msg_id c9aa052c-0e90-4e63-90fd-a5c65f14c4f1，10287 B，sha256 5cc7c7295c3f45396ca1c15502a4e6f720fe2c733888c4124d7a019b50e95c3d，等于 scratchpad/start-p08a-b.md。
  - 发前核实收件方：get_session 显示 effort 为 medium；其 transcript 最近的 permissionMode 记录（03:06:39Z）为 bypassPermissions，03:20:36Z 回执的 from-mode 为 bypass。
  - 裁定要点：
    - 合同声明选 C：不声明新合同，不加 communication 边，module-governance 新增 0 行。
    - 签名时的 rate budget 复核、不消耗：读原始预算与原始已消耗合计，已消耗大于预算才拒绝，不要求签名时还有剩余。须有“预算 1、审批用掉这 1 个单位、签名成功”的正例。耗尽负例只能在测试库里种入，须标明并在 REPORT 中披露。
    - kill switch 读当前 runtime revision 的 kill_switch_enabled 与 execution_enabled。kill_policy_approval 记为残留，激活签名之前必须接通。
    - 新表只授 runtime 角色 SELECT、INSERT，两个角色守卫同步纳入。重试身份为 command_id:lease_id:attempt，请求内容不同即冲突拒绝。
    - 版本升到 0.9.0；不新增 csproj 或引用；任何模块的锁文件有变化就停。floor：unit 取实测，integration 32→40，contract 22；改后跑 Tests/ci。
    - 文书只更正设计第 141 行开头半句，以及 PA README 中变成假话的句子。AGENTS 与设计第 225、232、234 行不改。
    - PolicyApprovalContractJson.cs 与 R2 逐字节相同。
    - 窗口附条件预分配给 Builder，只用于 control-plane-host.integration。不跑累计门禁。
- 资源：门禁位空闲。窗口附条件预分配给 P08A Builder。P00C QA 仍在运行，不用 PG、窗口或门禁位。
- 自 Owner 最近一次输入（02:11:38Z）以来，总管已发 4 条跨会话消息。

## 2026-09-28T03:55:32Z（总管 date -u）P00C QA FINAL：PARTIAL，核实成立；P00 取得实际出口

- P00C QA FINAL 于 03:53:13Z 送达总管 transcript（from-mode bypass）。结论 PARTIAL。停止事项只有 03:16Z 那一次（消息 ID 89c380f6-dbc7-49df-a0de-659228fad65c），已按 03:18:15Z 的裁定处理。
- 文件（总管 03:53:18Z 复算一致）：
  - units/p00c/qa/report/QA-REPORT.md，34639 B，493 行，sha256 d9b193d69e51637d8aa3a005c998562836136b4e5842c246d47c931b5f776711。
  - units/p00c/qa/report/EVIDENCE_INDEX.json，67415 B，sha256 c36c8845b3775a0ce73b09e8aad9213c55d424fd16b41bd21af07308f89b765b。
- 总管只读核验（03:53Z–03:55Z）：
  - EVIDENCE_INDEX 的 415 条逐一复算 sha256 与字节数，全部一致。report 下除索引自身以外的文件都在索引中。
  - clone-a-raw：HEAD 为 df835fb；1138 个工作区文件逐一等于 blob 字节；按工单 1.1 第 3 条的算法复算摘要为 53bee058…。status 为 14 条 1 .M，另有 .venv/ 与 strangler/__pycache__/ 两条忽略项。
  - clone-b-ordinary：HEAD 为 4dfe700；1139 个工作区文件逐一等于 blob 字节；摘要 7e90a7d2…；status 只有上述两条忽略项。
  - checks：00、01、11、12、13、31 的 ok 都为 true。10 的 failing_before_ok 为 false，是按 F3 算的原始记录，按裁定以 10b 为准。
  - suite：A、B 两边都是 static rc 3（WAITING_EXTERNAL），其余三个 rc 0，方法数 25+9+10=44。B 中新增测试 rc 0，3 个测试。
  - secret-scan：hits 与 judgments 各 1874 条，全部判为假阳性，真阳性 0。
  - Owner 身份：总管在内存中从 clone-a-raw 的提交头取 4 个 needle，不落盘；扫描 report 全部文件、QA 自写的 4 个 scratch 文件与回执，共 416 个文件。家目录前缀之外命中 0，连字符形式 0。
  - O1 核实：Modules/factory-worktree-manager/requirements.lock 第 13 行是 psycopg-binary 3.3.4 多个哈希中的一个，只有 57 位十六进制，其余都是 64 位。按 --require-hashes 安装时，只有恰好对应这一行的平台轮子会校验失败，属失败关闭方向，不是秘密。它是被保全的既有内容（5b7d16c 引入），归 factory-worktree-manager 的所属包处理。
  - /private/tmp/dps-* 此刻 59 项。
- 裁定：P00C 的 PARTIAL 成立。按 WO-P00C 第 4 节，P00 取得实际出口。
  - 剩余阻断与限制照 QA 报告第 10 节：static anchor 归 P03；windows 与 device 待外部落实；Air 源目录不可再观测；外审 NOT_RUN；cac7ccb 及以前的 blob 未复审；旧入口 suite 只覆盖静态回归与合成 fixture。
  - 观察项 O2（3 个 text=auto 文件入库带 CRLF，新增测试的 PRESERVED_PATHS 不含它们）与 O3（新增测试不隔离全局 git 配置；保全路径在两处各写一份）记录在案，不判缺陷。
- QA 保留物：两个 clone 连同 .venv 与 __pycache__、pip 缓存、fb-root，以及 environment/scratch/owner-needles（0600，含 Owner 身份）。列入待 Owner 安排的统一清理。
- P00C QA session 已空闲，不再向它发消息。
- 影响：16:38:13Z 条“P00 收口之前，P01 与 P18 都不开”的前提已解除。P01 是否开单、如何划界，另条记录。
- 自 Owner 最近一次输入（02:11:38Z）以来，总管已发 4 条跨会话消息。

## 2026-09-28T04:08:02Z（总管 date -u）P01 开单：基线 M19，Builder 与 QA 两张卡已发

- 依据：P00 已取得实际出口（03:55:32Z 条）；PDD 中 P01 的前置只有 P00（第 266 行）。总管按 Owner 2026-09-27 的授权自行开单。
- 工单 coordination/WO-P01.md，25891 B，247 行，sha256 985e0ad73d5b6086126cd93aa0078055ff3f2de29c38707f16b84542a6f46a41。
- 基线选 M19 = df120166469e5044977227798932c07808b87571（tree 963928ac…），理由：
  - 以 P01 为前置而尚未开单的下一个包是 Factory 线的 P18，它只差 P01；
  - M19 上已有门禁对 admin/runtime 分离变量的接线（ea4837d），以及 Factory 侧的 PG 18.4 修复（6ba8ca0）；
  - M19 有可对照的门禁 run 4f3f409b…（48/5，module-governance 75 行）。
  - 代价：产品线（R2 与在途的 P08A）不含 P01 的改动，待两条线合流时由集成单元并入。两条线自 afb4f9b 分叉，其后 M19 有 12 个提交、R2 有 20 个；scripts/ 两边都没改，Tools/ci、Tests/ci、governance/policies 只在 M19 一侧有改动。
- 总管开单前只读核实（03:56Z–04:05Z）：
  - 锁文件内容；本机 Python 3.12.13（uv）、.NET SDK 10.0.301 与 runtime 10.0.9、Node 24.18.0（nvm）、pwsh（~/.dotnet/tools，store 中为 7.6.2）、adb（~/.local/bin/adb → platform-tools-37.0.0）、~/Applications/Postgres.app 都存在，只看了文件，没有执行。
  - scripts/start-test-postgres.sh 默认端口 55432、数据目录在 HOME 下，initdb --auth=trust，只输出一套 OS 用户身份。
  - candidate-test-policy 中要求分离身份的 integration 套件有四个：factory-artifact-builder、factory-control-plane-host、factory-evidence-ledger 的 postgresql18，以及 gbrain-projector.integration。
  - 门禁的信任路径包含 scripts/ 下全部 7 个脚本与根锁文件；diagnostic 运行时 candidate-trust-anchor 只记录（run_candidate_gate.py 第 3068–3080 行）。
  - npm ci 要连 registry.npmjs.org，不在 Owner 的五项授权内；PG 18.4 官方源码下载有 Owner 01:55:42Z 的授权（第 296 行）。
  - M19 clone 04:05:35Z：HEAD 与 tree 如上，porcelain 0 行。
- 写域：scripts/start-test-postgres.sh、stop-test-postgres.sh、Docs/Operations/ 下的环境文档、根 CHANGELOG.md。其他路径须在 PREACCEPT 中逐项提出、由 START 定案。Modules/** 一律不改。
- 端口：Builder 56300–56309，QA 56310–56319。默认不用窗口；门禁位与 P08A QA 共用，先后在各自的 QA START 中安排。
- 任务卡：
  - Builder 卡 04:07:15Z 发出，task_728b7885，卡标记 P01B-K9T3，卡文 7402 B，sha256 b08706b2d7eaab22aee1b901ed0b1548ac1c0bd6ced7d98710486c6df3422ac6，等于 scratchpad/card-p01-b.md。
  - QA 卡 04:07:37Z 发出，task_e2c687a9，卡标记 P01Q-M5V8，卡文 7094 B，sha256 455accbf89ebce16783b9f55d8342f38a0e499ed8e814209ca0b3fabc0224bfd，等于 scratchpad/card-p01-qa.md。
  - 两张卡等人点开；会话由谁点开，总管不作推断。
- 在途：P08A Builder 施工中（04:03:44Z 其自有 PG 在 127.0.0.1:56380 监听）。P00C 已收口。
- 自 Owner 最近一次输入（02:11:38Z）以来，总管已发 4 条跨会话消息；任务卡不计在内。

## 2026-09-28T04:31:11Z（总管 date -u）P08A Builder FREEZE 已核实；P08A QA START 已发出

- FREEZE 于 04:20:44Z 送达（from-session local_96599292-046a-4015-917d-59de2c9cbed6，from-mode bypass）。
- 总管只读核验：
  - FREEZE.json，27782 B，sha256 e203b330471b170a97b69725f843a6cfb9b41870f9a31709c429d708668e56ab。
  - REPORT.md，16820 B，sha256 42c07d6fa3b79913b618790cafbf20096a1259729806bbdef26d6aeaca524b00。
  - A1.diff 的 sha256 为 4ac996c9423d73e58766d42f1540f4f118521d3ad1d13fd084d5dc5e0595dc73，按 WO 的 argv 复算一致。
  - A1 = 464ec97ed0c94029715c2c60450a09110e140e4e，tree 3ca11c7b8fd7ca0eb770d459e49243f35f27e078，唯一父 R2。author 与 committer 都是 P08A Builder <p08a-builder@local.invalid>。分支 codex/p08a-0518e1f，porcelain 0 行。
  - 共 21 个路径，+2879/−20，都在 WO 第 3.1 节的允许集内。以下未改：PolicyApprovalContractJson.cs、根与模块的 AGENTS.md、v1 schema 与 canonical 资产。没有新增 csproj，锁文件无变化。
  - START 裁定的落实：
    - 采纳 C：没有新合同的声明，也没有新边。
    - 设计文档第 141 行只删了那半句。
    - module.yaml 升到 0.9.0；floor 为 unit 10→13、integration 32→40、contract 22。
    - 006 只授 runtime 角色 SELECT、INSERT；对 PUBLIC 与三个 submission 角色 REVOKE ALL。
    - runtime 守卫加入新表，并新增第 17 列。
    - submission 守卫没有改代码。它的 SQL 对清单外关系上的任何权限一律报出，所以新表由这个通用子句覆盖。这一条是总管的推断，已交 QA 独立核实。
  - 档位（只读元数据字段）：Builder transcript 自 03:06:43Z 起的 411 条 assistant 记录，全部是 claude-opus-5-5 / medium / medium。带 permissionMode 的最新 user 记录（04:16:46Z）为 bypassPermissions。
  - Builder 报称自测全部符合预期，没有停止事项。4 处调用错误各修正一次，原日志保留（REPORT 第 11 节）。
  - Builder 报称窗口使用时段为 04:17:01Z–04:17:30Z，PG 于 04:18:34Z 停机。READY-FOR-PG-WINDOW.md 的记录与此一致。
  - 04:27:25Z 查过，没有 dotnet、VBCSCompiler、MSBuild、postgres、门禁或 testhost 进程。Builder 的 environment/tmp 在 04:26Z 有条目被删除（目录 mtime 变化），其中最新的条目是 04:18Z，删除来源未查明。
- P08A QA START 于 04:30:28Z 发出。
  - msg_id 5a1767c6-ed05-4293-84c8-0fb068c1d05a，11416 B，sha256 192ab9107c56708a61a5be1e32b0eea960ad5f25f637629fa8faa7061caa59b9，等于 scratchpad/start-p08a-qa.md。
  - 收件方是 bypass：QA transcript 中 04:30:28.344Z 的记录为 bypassPermissions。没有 delivery notice。04:31:11Z list_sessions 显示 QA isRunning。
  - 要点：
    - 逐条答复 U1–U14。
    - 在 C 之下，QA 第 4 项 (a) 中的声明检查不适用；用 phase0 预检 schema 的结果只作观察，交切片 B。
    - U5：要求 QA 独立核实 submission 守卫的通用子句确实覆盖新表。
    - U10 特许：QA 只可读 Builder transcript 的 6 个元数据字段。该文件为 0600，特许只限这个文件和这些字段。
    - U12：那 8 个 scratchpad 文件保持原状，列入清理清单。
    - 窗口与门禁位附条件分配给 P08A QA，自检项与 60 分钟上限同 Builder。
    - 网络只限 Owner 00:05:22Z 授权的第 3 项。
    - Owner 身份扫描沿用 03:55:32Z 条的家目录前缀口径。
    - QA 可以读 Builder 证据目录，以及 Builder environment/ 中的 0644 脚本与清单；0600 文件与目录不开。
- 资源：窗口与门禁位现在附条件归 P08A QA。04:31:11Z list_sessions 未见 P01 会话。
- 自 Owner 最近一次输入（02:11:38Z）以来，总管已发 5 条跨会话消息；任务卡不计在内。

## 2026-09-28T04:46:06Z（总管 date -u）P01 两个会话开出；Builder 已校准

- P01 Builder 会话 local_eec219ab-d542-43cb-a2df-39127ea046d4，04:43:51Z 创建。P01 QA 的 transcript 为 quirky-kalam-2f2e48/e866555d-e4df-425a-abea-74af62b175bd.jsonl，含卡标记 P01Q-M5V8。两张卡都已被点开；由谁点开，总管不作推断。
- Builder 档位自检于 04:44Z 送达（from-mode bypass）。
  - 总管在其 transcript 中核实：卡标记 P01B-K9T3 只命中这一个文件；第一轮 10 条 assistant 记录为 claude-opus-5-5 / max / max；permissionMode 为 bypassPermissions；CLAUDE_CODE_EFFORT_LEVEL 未设置。
  - 总管 04:45Z 调用 set_session_effort(medium)，get_session 回读为 medium。
  - “档位已校准，开始 PREACCEPT”于 04:45:29Z 发出，msg_id c54fe7dc-71dc-437e-95b8-bd3b88e55ba3，640 B，sha256 560b082608ca705e0dc53ac06e659563574b53974ded53f1fce414b57a1f9b45，等于 scratchpad/calib-p01-b.md。没有 delivery notice。
  - Builder 下一轮自 04:45:33Z 起的记录为 medium / medium，permissionMode 为 bypassPermissions。
- P01 QA：第一轮记录为 claude-opus-5-5 / max / max，permissionMode 为 bypassPermissions，正在做 PREACCEPT。
- 两份 PREACCEPT 都未到。P08A QA 在施工中。
- 自 Owner 最近一次输入（02:11:38Z）以来，总管已发 6 条跨会话消息；任务卡不计在内。

## 2026-09-28T05:05:37Z（总管 date -u）P01 Builder PREACCEPT 已到并核实哈希

- P01 Builder PREACCEPT 回执于 05:00:45Z 送达（from-mode bypass）。units/p01/builder/PREACCEPT-BUILDER.md，53554 B，sha256 d5830dfac38e580713c995601d6beea71cbfcd3dc1aeba64ee84e309ab876219，总管 05:00:57Z 复算一致。回执写于 Builder 的 medium 轮。
- 内容等 QA 回执到齐后再记，START 也等到那时再发。
- 自 Owner 最近一次输入（02:11:38Z）以来，总管已发 6 条跨会话消息；任务卡不计在内。

## 2026-09-28T05:15:11Z（总管 date -u）Owner 输入：接受 P01 的三项建议并授权

- Owner 于 2026-09-28T05:14:18Z 在总管 session 中输入：“P01 Builder 会话：1，我没有看到需要我点击确认的。2，接受你的建议并且授权你。”
- 第 1 点：P01 Builder START 尚未发出（在等 QA 的 PREACCEPT），所以 Builder 还没有向 Owner 提问。这符合预期。
- 第 2 点对应总管此前汇报中的三项建议，现按 Owner 授权执行：
  - npm ci：P01 的 Builder 与 QA 各自可以从 registry.npmjs.org 取 package-lock.json 中的 3 个包（playwright 1.61.1、playwright-core 1.61.1、fsevents 2.3.2），禁止执行包脚本，只装进本单元目录。授权只限这 3 个包和 P01。
  - gbrain-projector 的 test-only admin 可以是 TEST_ONLY 超级用户，依据同 Q6；runtime 身份仍为最低权限。
  - DPS、Factory、GBrain 各用一个独立的 PG 实例。
- 下载仍按账本第 323 行的先例：按各 session 自己的安全规则，Owner 须在 P01 Builder 与 P01 QA 各自的 session 里当面确认，总管的转达不能代替。
- 跨会话消息计数自 Owner 本次输入起归零，目前 0 条。

## 2026-09-28T05:32:06Z（总管 date -u）P01 QA PREACCEPT 已到；两份回执要点；P01 Builder START 已发出

- P01 QA PREACCEPT 回执于 05:27:51Z 送达（from-session local_acdcf7b3-1cc8-4afc-8fe5-7351a43f46b5，from-mode bypass）。units/p01/qa/PREACCEPT-QA.md，69210 B，620 行，sha256 4e0fdec18caeb1e11c828760126ce4b6b7bbd08d45922f6afda39316f1847aeb，总管 05:28:00Z 复算一致。QA 档位为 claude-opus-5-5 / max，bypassPermissions。
- 两份回执的要点：
  - 基线 M19、分叉点 afb4f9b、P00 出口认定：两边都核对一致。M19 的 porcelain 两边都没有独立复核（卡片允许的 git 子命令里没有 status），改在各自 clone 中核对 tip 与 tree。
  - 不符之处：history.git 没有 heads，M19 不在镜像中；run 4f3f409b 的 base 是 M18，不是 M19；Phase0 FAIL 时 candidate-trust-anchor 不会发布。
  - 两边都提出的缺口：
    - gbrain-projector 的 admin 只能是超级用户，本机也没有非 18.4 的服务器；
    - binding 的 PKCS8 签名者要命中合约 pin，本机给不出；
    - 同名变量冲突：ADMIN/RUNTIME_URI 由 Host（libpq）与 GBrain（Npgsql）共用，门禁还要求 DPS_TEST_POSTGRES 与 _URI 指向同一目标；
    - 门禁与 phase0 的允许清单都没有 PGPASSFILE；
    - 现有 start 脚本不开 TCP，而且输出行本身就是 DSN。
  - QA 的独有发现：13 个 DPS_TEST_POSTGRES 套件中，有 7 个把这个身份直接当产品连接（组 C），1 个建角色但产品不断言最低权限（组 B），其余 5 个是真正的引导身份（组 A）。policy 与 module.yaml 的 floor 有三处不同。
  - QA 的偏离披露：写过 2 个 scratchpad 临时文件，04:58:28Z 已删除；另用了只读的 git rev-parse，以及用 /usr/bin/python3 只读解析 run JSON。
- P01 Builder START 于 05:31:22Z 发出。
  - msg_id 73ff8a9e-cfe5-47a4-9ad1-431f70c16ac2，11680 B，sha256 e953dff2828112c348a8b240398d0e4fdc1223baf889b517bd9c9c61ea995e21，等于 scratchpad/start-p01-b.md。
  - 收件方是 bypass（Builder transcript 中 05:31:22.127Z 的记录）。没有 delivery notice。Builder 自 05:31:25Z 起的记录为 medium / medium。
  - 裁定要点：
    - PG 采用官方源码构建 18.4（Owner 01:55:42Z 授权）。npm ci 依据 Owner 05:14:18Z 的授权，只取 3 个包，--ignore-scripts。两项下载都要 Owner 在 Builder 的 session 里当面确认。
    - 每个数据域一个实例（DPS、Factory、GBrain），socket 目录共用，端口在 56300–56309。
    - 超级用户只限以下几处：每个实例的 Q4 引导身份；Factory 的 Q6 ledger 迁移身份；GBrain 的 admin（G1，依据同 Q6，Owner 05:14:18Z 已接受）。GBrain 的 admin 要先用非超级角色实测一次 DISABLE 内部触发器，得到 42501 作为证据。
    - DPS 分三组：组 C 用最小身份，一律不用超级用户；组 B 用非超级引导身份，不改用超级用户；组 A 先用非超级引导身份，因权限不足失败时，事先批准按 Q4 用超级用户重跑一次，两次都记录。
    - WRONG_VERSION 与 PKCS8 记 INFRA_ERROR，不伪造、不自造。HMAC key 经环境变量传入，批准为例外。Docs 只写现状，不写规则性条文。不对 Dps.slnx 做 restore。
    - 验证套件：Builder 选的 7 个，加上 8 个不需要窗口的 DPS 套件。control-plane-host.integration 与 soul-registry.integration 记 NOT_RUN（需窗口）。
    - 脚本同时监听 TCP 与 socket；兼容 bash 3.2；不用 ~/.pgpass；版本判据为 18.4 与 180004。
- 资源：P08A QA 仍持有窗口与门禁位。P01 Builder 不用窗口，也不跑门禁。
- 自 Owner 最近一次输入（05:14:18Z）以来，总管已发 1 条跨会话消息。

## 2026-09-28T06:10:06Z（总管 date -u）P08A QA FINAL：NOT PASS；已退回 P08A Builder 返修（A2）

- P08A QA FINAL 于 06:02:06Z 送达（from-session local_9ad936fd-2827-4111-b5f1-cfb5e394d50e，from-mode bypass）。结论 NOT PASS。门禁 run 89b733720c7bb6464efb4449014f2a3c，PASS 48 / FAIL 5（共 53）。发现 5 项（中 2、低 3），观察 16 条，停止项无。
- 总管复核：
  - units/p08a/qa/DSP_ZD/Reports/ci/p08a-qa/QA-REPORT.md，30145 B，sha256 46983ca3acef3536b37a33cd13f914b6c072f0a01e013e0eb11bcfd3fb073191；同目录 EVIDENCE_INDEX.json，35416 B，sha256 b269b000a9c93c84aeb68944d1f2614210ec0f5539c9ac2175ae1f01ee20ba38。06:02:14Z 复算一致。
  - EVIDENCE_INDEX 的 202 个条目按其 root（units/p08a/qa）逐一复算字节数与 sha256，0 处不符（06:03:13Z）。报告目录内 82 个文件全部在索引中。索引声明未计哈希的 3 个秘密文件，总管没有打开。
  - 报告目录下的 leaks/ 是 0700 目录，总管没有打开。
- 5 项发现与总管的核对：
  - F1（中）：两个角色守卫只用 has_table_privilege，不查列级授权。给 submission 角色授予 signature_base64 列的 SELECT 后，守卫放行（12/12），该角色能读出签名。总管在 A1 的 Modules/policy-approval/src 中复数：has_table_privilege 共 58 行、65 处，列级权限函数 0 处，与 QA 的 65 处一致。
  - F2（中）：设计第 190 行的“require deterministic APPROVED”没有能单独失败的负例（QR6 删掉该条件后 83/83 通过）。探针 P10 经生产评估路径造出一个 DENIED 但 ACTIVE 的 approval：A1 拒签，变异体签出。
  - F3（低）：签名与并发 kill switch 追加之间的串行化没有仓库测试（QR5 删掉 runtime 锁后 83/83 通过）。探针 P9 证明 A1 行为正确。
  - F4（低）：设计文档第 184 行把 lease owner/expiry 列为 command commitment 的成员，A1 的请求合同没有这两个成员。Builder PREACCEPT 第 70 行说这些字段都在 unsigned authorization 内，这对 lease owner 不成立。总管已核对设计第 184 行。
  - F5（低）：A1 的 operations/README.md 第 41 行新增了规范性句子 “kill_policy_approval … must be connected before signing is activated.”，与 START 第 8 条不符。总管已在 A1 中核对。CHANGELOG 第 9 行的同类残留记录是 START 要求写的。
  - QA 在其探针覆盖范围内没有发现 A1 的产品行为有错。
- 资源：QA 的 control-plane-host.integration 在窗口内运行于 04:49:37Z–04:50:00Z，窗口于 04:50:00Z 释放。门禁位于 05:26:33Z 复查后释放（gate/postcheck.txt）。
- 处置：按 PDD §4.3 表中“实现失败或交付证据缺项”一行，退回 P08A Builder 返修。返修后固定新候选 A2（唯一父为 A1），再由 QA 复验。
- P08A Builder 返修 START 于 06:06:11Z 发出：
  - msg_id 735e275d-9760-453b-aaf0-59862b8a52a6，7416 B，sha256 41ca71955f27e02b2dce46be9668b1e38e232dbee718c4178421d655c735bfc3，等于 scratchpad/start-p08a-b-r2.md。
  - 收件方是 bypass：发前最近一条 user 记录（04:45:21Z）与送达记录（06:06:11Z）都是 bypassPermissions。没有 delivery notice。Builder 自 06:06:16Z 起的记录为 claude-opus-5-5 / medium / medium。
  - 裁定要点：
    - F1：在两个角色守卫共用的 SQL 中统一修，同时覆盖表级与任一列级授权。runtime 的“恰为 SELECT、INSERT”检查要排除列级的其他权限。至少加 3 个具名用例，migrations/README 改成与实现一致。任何既有用例或消费方套件因此失败，就停下回报。
    - F2：加一个具名负例，DENIED 但 ACTIVE 的审批经生产评估路径造出。只删掉 Decision != Approved 时，该用例必须失败。
    - F3：加一个具名并发用例，用锁或屏障并核对 pg_locks，不靠 sleep。只删掉 runtime 锁时，该用例必须失败。
    - F4：本轮补上 lease owner 与 lease expiry，schema、DTO、codec、corpus 与合同测试同步更新。类型取自 Command 现有的 dispatch/lease 字段，写明 file:line，取不到就停下回报。加一个 valid_until 不晚于 lease expiry 的具名负例。lease owner 只进 request_sha256 与账本承诺。在 REPORT 中更正 PREACCEPT 第 70 行。
    - F5：README 那句改为描述现状，CHANGELOG 的残留记录保留。
    - 观察项只处理 O13：反向验证的构建一律先 restore --locked-mode。
    - A2：唯一父为 A1，身份不变，分支快进。版本仍为 0.9.0，0.9.0 条目可就地补充。floor：unit 取实测数，integration 40，contract 22；Tests/ci 预期 Ran 243。
    - 自测：21 个 suite；failing-before；A1 的 19 类反向验证全部重跑，另加 4 类；治理检查零新增。证据放 Reports/ci/p08a-builder-r2/，并给出 R2..A2 与 A1..A2 两份 diff。
  - 资源：PG 沿用 56380–56389。Soul/Host 窗口附条件预分配给 P08A Builder，只用于 control-plane-host.integration；入窗前自检，60 分钟仍不满足就停下回报。不跑门禁，门禁位空闲。
- 其他单元：P01 Builder 于 05:31:49Z 用 AskUserQuestion 向 Owner 请求下载确认；到本条为止，该 session 没有新记录，仍在等 Owner 作答。
- 自 Owner 最近一次输入（05:14:18Z）以来，总管已发 2 条跨会话消息。

## 2026-09-28T06:14:44Z（总管 date -u）P08A Builder 停止回报：单元 PG 误用默认端口 5432 约 35 秒；已裁定继续返修

- P08A Builder 停止回报于 06:12Z 送达（from-session local_96599292-046a-4015-917d-59de2c9cbed6，from-mode bypass）。
- 事件：返修开发迭代中重启单元 PG 时，漏了首次启动用的 -o 参数（端口 56380、只听 127.0.0.1、socket 在单元目录内）。
  - PG 于 06:10:58Z 启动，监听 127.0.0.1:5432 与 [::1]:5432，并在 /tmp 建了 .s.PGSQL.5432 socket 与 lock 文件。
  - 06:11:33Z 用 pg_ctl stop -m fast 停机。
  - 这越出了 563xx 端口限制与 WO-P08A §3.1 的写入范围，Builder 按 §3.2 第 7 项停下。
  - 没有涉及 55432、55434 或 dps_gbrain_company。
- 总管只读核实：
  - 06:12:28Z：本机没有 5432 监听，没有 postgres 进程，/tmp 下没有 .s.PGSQL.* 文件。
  - 证据在 units/p08a/builder/DSP_ZD/Reports/ci/p08a-builder-r2/incident/：INCIDENT-5432.md，1000 B，sha256 8c98a7d7eb89f784eba8273e97c1d4be39f43dc8d5448c514c9f51cde56ff5a4；pg-server-log-excerpt.txt，1497 B，sha256 d7ad69bb8c8d62a2cf3ad9c7c3dd5632fc5add0b0143f25d5a17f8195738ce9a。两者与回报一致。
  - 日志中停机检查点为 wrote 0 buffers、distance=0 kB，说明这 35 秒内没有数据页被改动。log_connections 没有开，只读访问与连接尝试不能从日志排除。
  - Builder 称所有测试客户端都指向 56380，全部 Connection refused。该日志在其 environment/ 内，总管没有打开，照录为 Builder 的陈述。
- 返修进度（Builder 陈述）：未提交。F1–F4 的源码与测试已在工作树（8 个文件改动），F5 与文档未动；开发构建通过，Unit 13、Contract 41、schema Ran 9 通过；Integration 88 例因 PG 不在 56380 全部 INFRA_ERROR。
- 裁定：按 PDD §4.3 表中“环境/依赖失败”一行处理，可以继续返修。
  - 按原参数重启。每次启动后立刻核对三项：该 postmaster 只监听 127.0.0.1:56380；socket 在单元目录内；/private/tmp 下没有 .s.PGSQL.* 文件。任一项不成立，立即停机回报。核对结果追加写进 incident/ 下的新文件，已有两个文件不改。
  - 那次 88 例 INFRA_ERROR 的日志原样保留，不算正式自测，重启后可以重跑。
  - 在 REPORT 与 FREEZE.json 的“单元根外的读取与写入”中如实披露本事件，包括检查点数字、不能排除的访问，以及当时 TCP 连接的认证方式（只写方法名）。
  - 其余照返修 START 执行，停止条件与交付格式不变。
- 裁定于 06:14:23Z 发出：msg_id 1bd46198-7143-42ec-a31b-9c12b3e7f556，1789 B，sha256 6cb85556228e6d474083469a57b6c24c1d167bd49a0fd3de71ed7aa340808c65，等于 scratchpad/ruling-p08a-b-5432.md。送达记录（06:14:23Z）为 bypassPermissions，没有 delivery notice。Builder 自 06:14:25Z 起的记录为 claude-opus-5-5 / medium / medium。
- 自 Owner 最近一次输入（05:14:18Z）以来，总管已发 3 条跨会话消息。

## 2026-09-28T06:14:54Z（总管 date -u）更正上一条的送达时刻

- 上一条写的“06:12Z 送达”不精确。P08A Builder 停止回报的实际送达时刻是 06:11:58Z（总管 transcript 的 enqueue 记录）。

## 2026-09-28T06:31:22Z（总管 date -u）P08A Builder 返修 FREEZE（A2）已核实；P08A QA 复验 START 已发出

- P08A Builder 返修 FREEZE 于 06:23:54Z 送达（from-session local_96599292-046a-4015-917d-59de2c9cbed6，from-mode bypass）。
- 总管只读核实（06:24:15Z–06:26:53Z）：
  - A2 = e5a81c0ff72867a4fcc81f67767caa710f972e8d，tree 1bd09794f16b3511389c43367437473e55d36c33，唯一父 A1（464ec97e…）。author 与 committer 都是 P08A Builder <p08a-builder@local.invalid>。分支 codex/p08a-0518e1f 与 HEAD 都指向 A2，A1..A2 只有这一个提交，tracked 工作树干净。
  - 证据目录 units/p08a/builder/DSP_ZD/Reports/ci/p08a-builder-r2/，74 个文件都可读（0644）：
    - FREEZE.json，32574 B，sha256 69a42f7813d735bfc83755053e5ed7fd3c6c2d391aab8153db58b73ef0df8d89；
    - REPORT.md，15729 B，sha256 ead58a71736246c9a24f12ab39ab3d168c09de83833646c3af6fa72437d31925。
  - 两份 diff 按 WO 的 argv 复算：R2..A2 为 221184 B，sha256 0367cf39ba741de79e82f072cb4ead432190832f88f54ac37b0ab0e32e6853a3；A1..A2 为 60651 B，sha256 aaffedc78d5ce42d32158b9dbba2764fa916a111f88fd4f6bdbeb4da79b0ab3d。两者都与 FREEZE 一致。A1..A2 共 14 个路径，都在 policy-approval 模块内与根 CHANGELOG。
  - module.yaml 未变：版本 0.9.0，floor 为 unit 13、contract 22、integration 40。
  - FREEZE 所列 27 条运行与 23 类反向验证，逐条复算日志哈希，0 处不符。退出码符合预期：failing-before 3 条非零，其余 24 条为 0；反向验证 23 类都是 build 0、test 失败、源文件已还原。
  - F1 核对：守卫中“必须有”的检查仍按表级，“必须没有”的改为 has_any_column_privilege，只收紧、未放宽；DELETE、TRUNCATE、TRIGGER 没有列级，保持表级。submission 守卫对清单外的表（含签名账本）也已覆盖列级，另新增第 23–25 列承担 Reconciliation 与 Recovery 的否定检查。A2 的 src 中仍有 has_table_privilege 31 行。
  - F2、F3、F4 的具名用例均在 diff 中：DeniedButActiveApprovalFromProductionEvaluationRejectsSigning、SigningWaitsForUncommittedKillSwitchRevisionAndRejectsAfterCommit、SigningRequestRejectsAuthorizationBeyondLeaseExpiry，另有 F1 的 3 个用例。F3 的屏障按 pg_locks 轮询（每次间隔 20 ms，上限 10 秒），交 QA 判断是否确定。
  - F5：operations/README 那句已改为 “kill_policy_approval has no code in this version and is not connected to signing.”。
  - REPORT 写明了 F4 的 Command 出处（file:line），更正了 PREACCEPT 第 70 行，006 未改；5432 事件的披露包括检查点数字、不能排除的访问，以及当时 TCP 认证为 scram-sha-256。
  - 窗口：06:21:13Z 进入，06:21:41Z 退出，只跑 control-plane-host.integration（75/75）；/private/tmp/dps-* 前后都是 59 项。PG 于 06:14:38Z 启动，三项核对通过（incident/pg-start-checks.log），06:22:21Z 停机。
  - 06:26:53Z 复查：没有 postgres 进程，563xx 与 5432 无监听，/private/tmp 下没有 .s.PGSQL.*；没有门禁进程，也没有 CPH 或 SoulRegistry 测试进程。
  - Builder 自 06:06:16Z 起 153 条 assistant 记录都是 claude-opus-5-5 / medium / medium，permissionMode 为 bypassPermissions。
- 资源：Soul/Host 窗口与门禁位都已空出，转为附条件分配给 P08A QA。
- P08A QA 复验 START 于 06:30:31Z 发出：
  - msg_id 7d8701d6-c593-4e0b-af9f-0a14d2a1ed72，10725 B，sha256 9eca2daec30f2c2cab37586fd4efc0432fab92210e8fdc1564ff87db5b014b3c，等于 scratchpad/start-p08a-qa-r2.md。
  - 发前 QA 为 claude-opus-5-5 / max / max，bypassPermissions；送达记录（06:30:31Z）为 bypassPermissions，没有 delivery notice。
  - 要点：
    - 按 WO 第 3.4 节逐项复验 A2。输入未变的项目可以沿用第一轮结论，但要写明依据；A1..A2 触及的与点名的项目必须实测。
    - 点名项：F1 的列级探针（第一轮 12/12 放行）与表级探针重跑，并核对“必须有”未改成列级；F2、F3 重跑 P10、P9 与 QR6、QR5 变异体，两个新用例各单独连跑至少 20 次；F4 的 corpus、file:line 出处、lease_owner 进入 request_sha256，以及 WithAuthorization 辅助改动不改变既有负例的拒绝分支；其他测试辅助改动不绕过生产决策路径；反向验证至少重做 15a–15d；21 个 suite 全部重跑；门禁在 A2 上跑一次，预期 48/5、module-governance 67 条零新增。
    - 报告目录扩为 Reports/ci/p08a-qa-r2/，第一轮目录不再写入。
    - PG 用 56390–56399，每次启动都显式给参数并做三项核对，不成立就停机回报。
    - 读取范围增加 Builder 的 r2 证据目录与 environment/ 中返修期间新增的 0644、0755 文件；0600 文件与 pg/、tmp/ 仍不开；U10 特许不变。
    - 结论只能是 PARTIAL 或 NOT PASS。第一行写“P08A QA 复验 FINAL”，停下时写“P08A QA 停止回报”。
- P01 Builder：05:31:49Z 的下载确认仍在等 Owner 作答，到 06:30Z 该 session 没有新记录。
- 自 Owner 最近一次输入（05:14:18Z）以来，总管已发 4 条跨会话消息。

## 2026-09-28T06:38:17Z（总管 date -u）P01 Builder 停止回报：C# 集成套件首次运行出现产品侧失败；已裁定按 A 继续

- P01 Builder 05:31:49Z 的下载确认提问，在其 transcript 中 06:22:18Z 有了作答记录（tool_result）。Builder 自 06:22:24Z 起继续施工，记录为 claude-opus-5-5 / medium / medium。
- P01 Builder 停止回报于 06:33:39Z 送达（from-session local_eec219ab-d542-43cb-a2df-39127ea046d4，from-mode bypass）。命中 WO-P01 第 3.2 节第 8 项：首次被执行到的路径上出现新失败。Builder 报称已停手，没有改产品代码，也没有重跑。
- Builder 报称已完成：
  - 工具链与锁一致；PG 18.4 从源码构建。
  - 三个实例：dps 56300、factory 56301、gbrain 56302，scram 认证，都没有 dps_gbrain_company 库。
  - 权限检查 159/159 PASS。
  - 4 个 Factory 套件 PASS（artifact 8、ledger 6、worktree 5、host 12）。
  - 第一次跑 C# 套件时 run-suite.sh 缺执行位，rc=126，属调用错误；原日志保留为 *.attempt1.log，修正后跑一次。
- C# 套件结果（Builder 陈述，总管已读对应日志核对错误出处）：
  - gbrain-projector.integration：16 例，失败 13。其中 11 例为约束清单漂移（rendered_revisions.rendered_revisions_pkey）；1 例为 42703 “ns.oid”，出在 Npgsql 的 LoadBackendTypes，由用例 HostileStartupSearchPathCannotShadowCatalogAttestation 设置的恶意 search_path 触发；1 例为 WRONG_VERSION，按 G2 记 INFRA_ERROR。
  - device-registry.integration：15/15 PASS。
  - evidence-service.integration：测试工程编译失败，CS1729 与 CS1503（GBrainProjectionRenderer 的构造器与参数类型）。M19 QA 报告第 180 行列有既有的 evidence-service.unit 门禁 FAIL（归 P14），很可能同源。
  - interest-reducer.integration：1/1 失败，NotSupportedException “memory.event/v1 is quarantine-only”。
  - platform-account-registry.integration：15 例，失败 11。首因是测试 SQL 中的 42703 “constraint_oid”，其余用例连锁失败。
  - soul-memory-adapter：restore 中被中断，不计结果。其余 5 个套件未跑。
- 总管核实（06:34:35Z）：三个 postmaster 来自单元内的源码构建，只监听 127.0.0.1 的 56300、56301、56302；/private/tmp 下没有 .s.PGSQL.* 文件。证据目录 units/p01/builder/DSP_ZD/Reports/ci/p01-builder/ 共 41 个文件，都可读（0644）。
- 裁定：采纳 Builder 建议的 A，附条件。依据 WO-P01 第 1.2 节与第 3.2 节第 4 项：本单元只提供环境并记录缺口，因产品缺陷失败的套件照实记录、归属责任包、不修。
  - 已出现的失败，总管初判为产品 FAIL，由 QA 独立复核。WRONG_VERSION 记 INFRA_ERROR。
  - gbrain 的约束清单漂移须做一次只读诊断：用校验器自己的查询取实际值，与清单逐字段对比，并说明差异与 G1 的超级用户 admin、initdb 的 locale 与编码无关。可能来自环境时停下回报。
  - evidence-service 与 M19 既有 FAIL 是否同源，交 P01 QA 核对。
  - soul-memory-adapter 重跑一次；跑完其余 5 个套件，分组与超级用户规则照 START。
  - 此后的失败：出处在产品、测试或所用库，且能写出出处的，照录为产品 FAIL，不停；可能出在环境的，或拿不准的，停下回报。
  - 每个 FAIL 在缺口清单中写明计数、首个错误原文、出处、归属及理由，以及建议的责任包；责任包由总管指定。
  - 不改产品代码、测试、module.yaml 或 policy。之后完成回退演练、仓库与治理检查、扫描、提交与 FREEZE。
  - 每次启动 PG（含回退演练）都核对只听 127.0.0.1 与 /private/tmp 下没有 .s.PGSQL.*。
- 裁定于 06:37:36Z 发出：msg_id a1c54be6-a3d3-4863-961f-fc5988c263f1，3869 B，sha256 9f0b25cbeda422022edf65be3325578b20bc49ee1a1a661430a4bdd418983767，等于 scratchpad/ruling-p01-b-suites.md。送达记录（06:37:36Z）为 bypassPermissions，没有 delivery notice。Builder 自 06:37:39Z 起的记录为 claude-opus-5-5 / medium / medium。
- 其他：P08A QA 复验进行中（自 06:31:16Z 起，max）。
- 自 Owner 最近一次输入（05:14:18Z）以来，总管已发 5 条跨会话消息。

## 2026-09-28T06:43:07Z（总管 date -u）P01 Builder 第二次停止回报：4 个套件的失败出在本单元环境；总管更正 START 第 25 行对 dps_test_bootstrap 的规定

- P01 Builder 停止回报于 06:40:13Z 送达（from-session local_eec219ab-d542-43cb-a2df-39127ea046d4，from-mode bypass）。按 06:37:36Z 裁定第二节第 3 项停下：memory-event-ledger、audit-metrics、policy-approval、command-orchestrator 四个套件的失败原因在本单元环境。四个套件各只跑了一次，没有重跑，也没有做组 A 的超级用户重跑。
- gbrain 只读诊断（Builder 陈述，总管已看输出文件）：
  - 做法：把产品迁移 001 套到临时 schema p01diag，逐字运行校验器的约束查询，之后 DROP 掉 p01diag。
  - 结果：差在 connoinherit。rendered_revisions_pkey 实际为 t，校验器要求为 false；同表的 p、u、f 类约束都是 t，c、n 类都是 f。库的 datcollate 与 datctype 为 C，编码 UTF8，server_version_num 为 180004。
  - 输出在 units/p01/builder/DSP_ZD/Reports/ci/p01-builder/diag/：rendered-revisions-constraints.txt，12867 B，sha256 9febdae494e54447206a164c99e9fed715d43dfd03d96f0d834fa546c0df8dca（第 142、155 行为 rendered_revisions_pkey 与 connoinherit t）。第一次尝试因占位符没填而失败，原件保留为 .attempt1.txt。
- 第二批套件（Builder 陈述）：
  - soul-memory-adapter.postgres-integration：测试工程编译失败，CS1729 与 CS1503（SoulMemoryAdapterPostgresIntegrationTests.cs 第 363–385 行），与 evidence-service 同类。
  - persona-store.integration：25 例，失败 24。42601 语法错误，出在测试的 CREATE ROLE … PASSWORD @password。
  - memory-event-ledger（组 B）、audit-metrics、policy-approval（组 A）：全部失败，42501 permission denied for database "dps_test_bootstrap"，发生在连接时。
  - command-orchestrator（组 A）：全部失败，42501 must be able to SET ROLE，出在测试夹具 DisposablePostgresHarness.cs 第 146 行的 CREATE DATABASE … OWNER。
- 总管在 M19 上只读核对：
  - PostgresPersonaStoreIntegrationTests.cs 第 668 行确为带绑定参数 @password 的 CREATE ROLE；
  - DisposablePostgresHarness.cs 第 140–146 行确为以新建的 migrator 角色为 owner 的 CREATE DATABASE；
  - PostgresAuditMetricsIntegrationTests.cs 第 635 行新建 runtime 角色，第 660–661 行以该角色连接。
  - P01 分支仍在 M19，工作树有 3 个修改文件与 1 个未跟踪的文档，没有提交。
- 冲突与裁定：
  - 冲突：总管 P01 Builder START 第 25 行规定“每个实例内，本域的库对 PUBLIC 收回 CONNECT 与 TEMPORARY”。组 A、组 B 的夹具以引导身份新建角色后直接以新角色连接引导库，依赖 PG 默认的 PUBLIC CONNECT，两者冲突。
  - 取舍：DPS、Factory、GBrain 已各用独立实例，跨域隔离由实例保证。在 DPS 实例内收回引导库的 PUBLIC CONNECT 不提供跨域隔离，因此改这一条。
  - 更正（只针对 DPS 实例的 dps_test_bootstrap）：
    - 保留 PG 默认的 PUBLIC CONNECT；TEMPORARY 仍收回，public schema 的 PUBLIC CREATE 仍收回；
    - 引导身份设置 createrole_self_grant = 'set, inherit'，这属于 START 组 B 一项已预留的角色级设置；
    - 组 C 的库、Factory 与 GBrain 实例不变；
    - 两项都写在 scripts/start-test-postgres.sh 中，由脚本生成。
  - 改动之后：
    - 权限检查全部重跑一次，预期改变的检查逐条列出改前改后与理由；
    - 4 个套件各重跑一次，原日志保留；组 A 仍因权限不足失败的，按 Q4 用超级用户再跑一次；组 B 不改用超级用户；
    - 归属规则不变；文档与 REPORT 写明两项设置、原因与影响，只写现状。
  - 归属：gbrain 约束清单漂移、soul-memory-adapter 编译失败、persona-store 测试缺陷记为产品 FAIL，由 QA 独立复核。soul-memory-adapter 与 M19 既有的 soul-memory-adapter.unit FAIL（M19 QA 报告第 180 行，归 P14）是否同源，由 QA 核对。
- 裁定于 06:42:21Z 发出：msg_id bb747abf-c17a-4366-93e8-cdf194c67b43，3357 B，sha256 2e685494a996cb275ed17b60f263baa81d1a7be04a02ac1a003e9517cc03ca34，等于 scratchpad/ruling-p01-b-env.md。送达记录（06:42:21Z）为 bypassPermissions，没有 delivery notice。Builder 自 06:42:26Z 起的记录为 claude-opus-5-5 / medium / medium。
- 自 Owner 最近一次输入（05:14:18Z）以来，总管已发 6 条跨会话消息。

## 2026-09-28T06:50:01Z（总管 date -u）Owner 输入：各会话档位核对

- Owner 于 2026-09-28T06:49:05Z 在总管 session 中输入：“bypass 那段 prompt 的effort“切好了”，”。
- 总管随即只读核对（06:49:27Z 起，get_session 与各 transcript 的元数据）：
  - 总管：claude-opus-5-5 / max，bypassPermissions；自 2026-09-27T01:20:36Z 起没有变化。
  - P08A Builder：medium，最近的 permissionMode 为 bypassPermissions，06:24Z 起空闲。
  - P08A QA：max，bypassPermissions，复验进行中。
  - P01 Builder：medium，bypassPermissions，施工中。
  - P01 QA：max，最近的 permissionMode 为 bypassPermissions（04:43:55Z），05:28Z 起空闲，等 P01 FREEZE。
  - 都符合 Builder 为 medium、QA 为 max、一律 bypass 的要求。总管没有看到需要处理的变化，已请 Owner 说明所指的会话。
- 跨会话消息计数自 Owner 本次输入起归零，目前 0 条。

## 2026-09-28T07:01:26Z（总管 date -u）P01 Builder FREEZE（P1）与补证已核实；reflog 身份与 M19/R2 对照两项事实

- P01 Builder FREEZE 于 06:53:51Z 送达（from-session local_eec219ab-d542-43cb-a2df-39127ea046d4，from-mode bypass）。
- 总管只读核实（06:54:49Z 起）：
  - P1 = ed4249ec544f67edb69ad664b43b4cb4a7571f98，tree f900a3c4bfc6fdc34af47d5f99e270b83d8159fa，唯一父 M19（df120166…）。author 与 committer 都是 P01 Builder <p01-builder@local.invalid>。分支 codex/p01-df12016 指向 P1，M19..P1 只有这一个提交，工作树干净（porcelain 0 行）。
  - 改动 4 个路径：CHANGELOG.md、scripts/start-test-postgres.sh、scripts/stop-test-postgres.sh，新增 Docs/Operations/TestEnvironment_测试环境.md。都在 WO-P01 第 3.1 节允许范围内。
  - 总管按 WO 的 argv 复算 M19..P1 diff：20806 B，sha256 48ddd0b1e9263990c96db87046359d4b6b6ada6dc3e41e7ae6db61200d497925。
  - units/p01/builder/FREEZE.json，2692 B，sha256 387bb02dd3887e7b81523b5e9b392e721119362ef46f7f86f5e697432dca4c9c；REPORT.md，18123 B，sha256 582f7ffc2a5434c7885b45b257440f6b5bdabf26229e189314a6c73cb26bee51。
  - FREEZE.json 缺 WO 第 3.2 节第 6 项要求的 diff argv、字节数与 sha256，也缺各自测日志的 sha256。总管于 06:57:41Z 要求只补证据、不动 P1（msg_id 8ef125df-9137-412e-988e-3d6336a84c5c，2005 B，sha256 b8436312ab7e1de3d3e6908fbbf5c38255d31e297af201f24f0333d00ba33dad，等于 scratchpad/ruling-p01-b-freeze.md；送达为 bypassPermissions）。
- 补证于 06:59Z 前送达：units/p01/builder/FREEZE-SUPPLEMENT.json，39121 B，sha256 bdf30ca0181116f7b0e739a66db3ae702cd22ad4fdf9cd85af7bd4aa4e141257。
  - diff 的 argv、字节数与 sha256 与总管复算一致；diff 文件在证据目录 p1-vs-m19.diff。
  - steps 共 87 条：75 条的日志字节数与 sha256 总管逐条复算一致；另 12 条标为“原件已被覆盖”，写明现存日志属于哪一步，没有补造。另列的 13 个证据文件，哈希都一致。
  - 三项交付物的位置：工具链探测为 toolchain-probe.json；隔离数据库及角色说明为 P1 中的 TestEnvironment 文档、REPORT 第 4–5 节与 pg-privileges.tsv；环境缺口清单为 REPORT 第 7 节。
- Builder 报称（REPORT）：
  - 权限检查 164/164 PASS。相对第一轮 159 项：03b 一项的预期由 DENY 改为 CONNECT=t，新增 03c 三项与 09 两项。回退演练 rc=0。
  - 套件：Factory 4 个与 device-registry PASS；其余 11 个为产品 FAIL，或 INFRA_ERROR、NOT_RUN（明细见 REPORT 第 6–7 节）。
  - 偏离：SUITES 过滤没有生效，06:37 与 06:43 两批跑了全部 11 个套件，12 条日志原件被覆盖（各次 rc 相同）；另有 7 处调用错误；clone 本地 reflog 中有系统默认身份，已 reflog expire。
- 总管已接受的处置：日志覆盖不可恢复，不要求补救，交 QA 评估。reflog 中的系统默认身份只存在本机 clone 的 .git 中，不进入提交、证据或 fetch，与家目录前缀命中同类，单独计数，不算泄漏；以后在 clone 中做 git 操作，一律先设单元身份的 GIT_AUTHOR_* 与 GIT_COMMITTER_*。
- 事实一（总管只读，只计数、不打印）：用系统账户全名比对各单元 clone 的 .git/logs，命中数为 p08a/builder 2、p07/builder 1、p07/qa 4、m19-int/builder 4，p08a/qa、m19-int/qa 与 p01/builder 都是 0。都是 git 在 clone、checkout 时写入的默认身份。暂不处理，Owner 要求清理时一并列出。
- 事实二（总管只读，M19 与 R2 对照）：
  - policy-approval 在 M19 的 PostgresPolicyApprovalService.cs 第 1170 行，把 ExecuteScalar 的结果强转为 DateTimeOffset；R2 同一处（第 1221–1225 行）已改为 GetFieldValue<DateTimeOffset>。
  - command-orchestrator 在 M19 的 PostgresCommandOrchestrator.cs 第 1177 行，用 GetString 读 relkind；R2 已改为在 SQL 中判定 relkind。
  - 这两项是 M19 线上的产品缺陷，已在 R2 线修正。归两条线合流，不另开修复包。
- 自 Owner 最近一次输入（06:49:05Z）以来，总管已发 1 条跨会话消息。

## 2026-09-28T07:01:37Z（总管 date -u）更正上一条的送达时刻

- 上一条写的“补证于 06:59Z 前送达”不精确。P01 Builder FREEZE 补充的实际送达时刻是 06:58:33Z（总管 transcript 的 enqueue 记录，from-mode bypass）。

## 2026-09-28T07:04:41Z（总管 date -u）P01 QA START 已发出

- 于 07:04:05Z 发出：msg_id b189ae0f-efbf-44bd-8587-672c7b474894，11359 B，sha256 5caff3932b3a171f4520241c62a22ed18943bb71c540c1df4ee402fd2f828f49，等于 scratchpad/start-p01-qa.md。
- 发前 P01 QA 为 claude-opus-5-5 / max / max，bypassPermissions，socket 15327 在线；送达记录（07:04:05Z）为 bypassPermissions，没有 delivery notice。
- 要点：
  - 冻结对象：P1、FREEZE.json、REPORT.md、FREEZE-SUPPLEMENT.json，以及 M19..P1 diff（20806 B，sha256 48ddd0b1…）。
  - 列出 Builder START 与此后三次裁定中的定案作为预期值，含 06:42 对 dps_test_bootstrap 的更正。
  - 逐条答复 QA PREACCEPT 的 U1–U16：
    - U3 按 G1，QA 独立重做 42501 实测；
    - U9 不下载，按 M19 QA 先例从 Builder 的 environment/pg/src/ 只读复制源码包与 .sha256，并与 22,567,173 B 与前缀 81a81ec6 交叉核对，不需要 Owner 确认；
    - U10 QA 不跑 npm ci（WO 第 3.4 节不含），改为核对 P1 没有改动 package-lock.json，并审阅 Builder 的日志。
  - 要求 QA 独立核对：每个 FAIL 的归属（可从 P08A Builder clone 只读 fetch R2 作对照）；06:42 的环境更正；超级用户的使用；Builder 的偏离与定级；reflog（先设单元身份，Owner 身份扫描覆盖 .git，reflog 命中单独计数）。
  - 读取范围：Builder 证据目录与根下 4 个文件；environment/checks/ 的 0644 文件与 4 个 0700 脚本；pg/ 下 3 个构建日志；源码包；M19 run 4f3f409b 中由门禁写出的证据。pg/secrets、pg/data-*、s/、tmp/、home/ 与任何 0600 文件不开。
  - 资源：
    - PG 56310–56319，每次启动后做三项核对；
    - 不用窗口；
    - 门禁位先给 P08A QA；P01 QA 发“P01 QA 门禁就绪”，等总管回复“P01 QA 门禁放行”再跑，等候超过 60 分钟就停下回报；
    - 网络只限 PyPI 与 nuget.org。
  - 结论只能是 PARTIAL 或 NOT PASS。
- 自 Owner 最近一次输入（06:49:05Z）以来，总管已发 2 条跨会话消息。

## 2026-09-28T07:29:23Z（总管 date -u）P08A 收口：QA 复验 PARTIAL，核实成立；窗口与门禁位已空

- 到达：P08A QA 于 07:25:47Z 发出“P08A QA 复验 FINAL”（QA transcript 中 SendMessage 的时间），同一时刻送达总管，from-mode bypass。
- 总管复算与核对（07:26Z–07:33Z，只读）：
  - QA-REPORT.md 36228 B，sha256 797da14ef0c1ef7fcd946d0b3e77873dc968788410f38ec153f8a99a1ee255cf；EVIDENCE_INDEX.json 48013 B，sha256 9ae894c931796bfd5f953bd0b26e4525e5d3cb00d4459778c1c017bc4500ba89。都与消息一致。
  - 索引列出 269 个文件，逐个复算 sha256 与字节数，全部一致，没有缺失。报告目录中只有索引本身不在索引内。
  - 门禁 run ea7c057e21a98b59c418296af6daf4be：
    - 证据 candidate-evidence-ea7c057e….json 272596 B，sha256 a7e17aa9ab11d4b030f7c8d09ad836ec4c9f54e08c2acdd85a62934698f38bd9，0600；
    - 运行 06:59:46Z–07:05:19Z；gate 为 WORKSPACE_DIAGNOSTIC_ONLY，verification_level 为 null；head 为 A2，baseline 为 R2；
    - 53 项 required，48 PASS / 5 FAIL，没有 skipped、infra_error、partial 或 not_run；
    - 与 89b73372（第一轮）、34ba03df（P07 QA）在 53 个位置上的 id、状态与退出码全部相同；
    - 5 个 FAIL 是 module-governance、solution-locked-restore-build、evidence-service.unit、legacy-runtime-adapter.static（exit 3）、soul-memory-adapter.unit；module-governance 的日志 sha256 前缀在三次运行中都是 71956e4d16b02647。
  - QA 档位：06:29Z 之后 465 条 assistant 记录，全部为 claude-opus-5-5 / max / max；permissionMode 为 bypassPermissions。
  - 资源：
    - 56390–56399 无监听，没有 P08A 的 postgres，也没有门禁进程；
    - /private/tmp 下没有 .s.PGSQL.*；
    - Q 的非忽略项为 0，HEAD 为 A2。
  - D6 披露的误写：QA 在 07:18Z 把两个临时文件写到了自己会话的 harness 目录（scratchpad 的上一级），随即删除。总管复查，该目录只剩 scratchpad/ 与 tasks/。按 P00C 先例，作为已披露偏离接受。
  - D5 中对 coordination/receipts 的列目录只列了文件名，没有打开，作为已披露偏离接受。
- 结论 PARTIAL，对象为 A2 = e5a81c0ff72867a4fcc81f67767caa710f972e8d：
  - 第一轮 F1–F5 全部修复，并在 A2 上实测。
  - 反向验证 15a–15d 与 A1 守卫的 failing-before 都按预期失败；QR1–QR4 在 A2 上重跑，结果符合预期。
  - 21 个套件全部 exit 0，其中 policy-approval.integration 88/88、contract 41/41、unit 13/13。
  - 新发现 0 条；新增观察 O17–O22；第一轮的 O1–O7、O9–O11、O15、O16 仍然成立。
- 两项裁定与一项更正：
  - O22：tests/README 中含 must 的 3 句描述的是测试断言的内容，不是新的义务性条文，不改写。
  - D1 更正：设计文档 Docs/Architecture/ContractMajorModes_合同主版本模式.md 中，命令承诺一行（lease owner/expiry，受信 Command 适配器）在 A2 上是第 183 行（总管已实测），184 行是 request binding。总管 06:05:45Z 的返修 START 与 06:30:31Z 的复验 START 沿用了第一轮的 184，属于引用错误，实质不变。
  - D4 事实：门禁子进程没有关闭 Roslyn 共享编译，会留下 VBCSCompiler；它空闲后会自行退出。这是门禁的既有行为，不算单元缺陷。
- P08A 出口：
  - P08A 的出口 = QA 在 A2 上的 PARTIAL。沿用 P06→P07、P07→P08 的先例，这是知情接受，不是 PASS。
  - 产品线当前头为 A2（units/p08a/builder/DSP_ZD，分支 codex/p08a-0518e1f）。
- 残留与责任包（据 QA 报告 §12）：
  - 切片 B：O1–O4、O19（Command 的 lease_expires_at 允许 +00:00，适配器须规范成 Z），以及新合同的声明与通信边；
  - 切片 C：生产组合根不得引用 Dps.PolicyApproval；
  - O17（F3 屏障没有绑定签名方）、O18（lease_owner 长度的口径）、O20（lone surrogate 抛出的异常类型）：留给下一次改到相应代码的单元；
  - 既有：门禁中与 R2 相同的 5 项 FAIL。
- 资源：
  - 窗口：QA 只在 06:56:40Z–06:57:05Z 用过（据报告 §7）。已空闲。
  - 门禁位：QA 自报 07:06:16Z 释放；总管 07:29Z 复查时没有门禁进程。已空闲。
  - P08A 的 Builder 与 QA 会话都已完成，保留待清理。
- 在途：P01 QA 已在 56310、56311、56312 上起了 3 个 PG 实例，都只听 127.0.0.1，/private/tmp 下没有 .s.PGSQL.*；尚未请求门禁位。另有一个 VBCSCompiler（pid 57154）在 07:24Z 前后启动，来源未查，不作归因。
- 下一步：按 03:06:06Z 条的切片计划，产品线下一个是 P08B（Command 改用 Policy 的新端口）。总管先按现场做只读摸底，再写工单。
- 自 Owner 最近一次输入（06:49:05Z）以来，总管已发 2 条跨会话消息。

## 2026-09-28T07:29:56Z（总管 date -u）更正上一条中的三个时刻

- “总管复算与核对（07:26Z–07:33Z，只读）”有误。上一条写于 07:29:23Z，不可能写到 07:33Z。更正：核对命令的输出时刻为 07:26:06Z 至 07:28:39Z。
- “总管 07:29Z 复查时没有门禁进程”，更正为 07:27:23Z（ps 与 lsof 输出的时刻）。
- “VBCSCompiler（pid 57154）在 07:24Z 前后启动”，更正为：07:27:23Z 时它已运行 4 分 30 秒，由此推得约 07:22:53Z 启动。

## 2026-09-28T07:35:39Z（总管 date -u）Owner 输入：06:49:05Z 那句的来源；P08B 摸底已派出

- Owner 07:35:18Z 在本 session 说明：06:49:05Z 的“bypass 那段 prompt 的effort“切好了””是另一个聊天里说的话，Owner 只是有些印象。
  - 总管的处理：这不是对总管的指令，也不指向某个待切换的会话。不做任何改动。
  - 此前 06:50:01Z 条已核对，所有在跑会话的档位与模式都符合要求。
- P08B 只读摸底：07:3xZ 派出一个只读研究代理，在后台运行，结果未到。
  - 约束：只读；读前先 stat，不开 0600 文件与 environment、pg 等路径；git 只读对象；不构建、不测试、不连库、不联网、不发消息、不读 transcript。
  - 基线为 A2。
- 发信计数：自 Owner 最近一次输入（07:35:18Z）以来，总管没有发跨会话消息。

## 2026-09-28T07:35:50Z（总管 date -u）更正上一条的派出时刻

- 上一条中的“07:3xZ 派出”不是精确时刻。更正：研究代理于 07:31:50Z 派出，时间取自总管 transcript 中 Agent 调用的时间戳。

## 2026-09-28T07:56:02Z（总管 date -u）P01 QA 门禁放行；P08B 摸底结果入档

- P01 QA 门禁：
  - “P01 QA 门禁就绪”于 07:54:22Z 送达，from-mode bypass。
  - QA 自报：
    - PG 工作、探针与 START 列出的套件都已完成（15 个套件各跑一次，组 A 的 audit-metrics 与 policy-approval 按 Q4 各重跑一次）；
    - 三个实例于 07:35Z 用候选的 stop 脚本停机（QA 原文为“07:35:2xZ”，不是精确时刻，以 QA 报告为准）；
    - 07:29:23Z 条提到的 VBCSCompiler pid 57154 来自 P01 QA 的 C# 套件，其环境含该单元路径；07:53:49Z 时已自行退出。
  - 总管 07:55:07Z 复核：没有门禁、postgres、testhost 或 VBCSCompiler 进程；563xx 无监听；/private/tmp 下没有 .s.PGSQL.*。
  - 放行消息于 07:55:30Z 发出：msg_id 2b5ede69-a4fb-480d-9c40-be55aabdec2d，694 B，sha256 18fc55027bf65136a571e0ec5d076e929b3f8711b62235f30436a73e92b5093b，等于 scratchpad/release-p01-qa-gate.md。
    - 要点：只跑一次；门禁无法启动或中途异常时不重跑，停下回报；跑完即释放。
    - 发出时 QA 为 claude-opus-5-5 / max / max，最新一条带 permissionMode 的记录为 bypassPermissions。
- P08B 摸底：
  - 研究代理的报告于 07:50:45Z 送达，存为 coordination/research/P08B-SCOPING-20260928.md（17060 B，sha256 0b05dc9f3d1f84f85c8ee408ce2abc9e1c886be337e0567a3da3cb0e0ee06fa5），只作数据。
  - 总管于 07:52:13Z–07:52:37Z 抽查了 9 处，全部属实，见该文件第 11 节。
  - 要点：
    - Command 无法合法取得 Policy 的调用方上下文：构造函数 internal，IVT 只开给 PA 与 PA.Tests。
    - typed 请求中的 ProposalId 与 ExpectedAuthority 在 Command 的持久状态中没有现成来源。
    - 新合同在两侧 module.yaml 中都没有声明。
    - Command 的 kill switch 只存在于文书中。
    - 旧端口仍被 Command 的 src 与测试使用。
- 自 Owner 最近一次输入（07:35:18Z）以来，总管已发 1 条跨会话消息。

## 2026-09-28T08:01:13Z（总管 date -u）P08 切片 B 开单；两张任务卡已发

- 开单依据：P08A 以 QA 复验 PARTIAL 收口（07:29:23Z 条）；摸底为 coordination/research/P08B-SCOPING-20260928.md（0b05dc9f…）；Owner 2026-09-27 的授权（新开工作、单元与 session 由总管决定）。
- 工单 coordination/WO-P08B.md：27571 B，sha256 d80027ccbc9c03325bf8b2032b5795c7e7775d161961fcc4d4527973a18a6c80。基线 A2 = e5a81c0ff72867a4fcc81f67767caa710f972e8d。要点：
  - 范围：Command 改用 Policy 拥有的 typed 签名端口；补新合同的声明与通信边；补 Command 边界上的负例（O1、O3、O4、O19，PDD P08 步骤 4 与两项出口中 Command 一侧的部分）。
  - 不纳入：
    - Command kill switch 的实现（另开切片；本单元只允许把 CO operations/README.md:3 改为描述现状）；
    - Host 组合与用真实 provider 的端到端证明（切片 C）；O5（切片 D）；
    - operation-compiler 的任何改动；PA 的 src、contracts、tests、migrations。
  - 写域：
    - command-orchestrator：001、002 不改；只有 START 裁定需要时才能新增 003；
    - PA 的 module.yaml：只改新合同的声明与通信边；
    - PA、CO 与设计文档中会变成假话的句子（逐字提出，由 START 定案）；根 CHANGELOG；generate 写出的 governance/modules。
  - 须在 PREACCEPT 中提出、由 START 定案的 9 项，首要的三项：
    - 调用方上下文的取得：不得新增公开构造途径，不得向其他模块开放 IVT，生产代码不得用反射伪造；
    - typed 请求逐字段的来源：ProposalId、ExpectedAuthority 与已批准参数要单独论证；没有权威持久来源时，生产路径失败关闭，不得凭空构造；
    - 端口失败的处理：须符合 PDD 的失败回退。
  - 端口：Builder 56360–56369，QA 56370–56379。每次启动都显式给出端口、listen_addresses 与单元内的 socket 目录，并在启动后核对三项。
  - 其他：
    - 在 clone 中做 git 操作前先设单元身份变量；日志不覆盖；
    - QA 门禁用 --base A2，与 run ea7c057e… 逐项比对，预期 48 PASS / 5 FAIL、module-governance 67 条零新增；
    - 结论只能是 PARTIAL 或 NOT PASS。
- 工单写成后的更正：总管 07:58:28Z 核对原文行号后，把第 1.1 节端口的行号由 447–464 改为 451–464，调用方上下文由 430–445 改为 430–444。上面记的哈希是改后的版本。摸底文件第 11 节写的 430–445、447–464 包含了文档注释与空行，文件不改，以本条为准。
- 任务卡：
  - Builder 卡于 08:00:24Z 发出，task_401914f0，卡标记 P08BB-LXVK，卡文 7895 B（含结尾换行），sha256 18ab283fb18c724ee5b769c7484a16e82e3170f3e75172624a1b7b086d3cb62d，等于 scratchpad/card-p08b-b.md。
  - QA 卡于 08:00:47Z 发出，task_7e59b05f，卡标记 P08BQ-KJ8C，卡文 7456 B（含结尾换行），sha256 29b69c4a81923f027fa45a6fac2b4f2d26c3637f800171c3198c53371b584157，等于 scratchpad/card-p08b-qa.md。
  - 两张卡等人点开；会话由谁点开，总管不作推断。
- 资源：P01 QA 持有门禁位（07:55:30Z 放行）；窗口空闲。
- 自 Owner 最近一次输入（07:35:18Z）以来，总管已发 1 条跨会话消息。

## 2026-09-28T08:08:16Z（总管 date -u）P08B 两个会话开出；Builder 已校准

- 两张卡都已被点开；由谁点开，总管不作推断。
- P08B Builder：
  - 会话 local_47611631-b1dc-4dd6-b9c1-32140203c534，08:05:42Z 创建（get_session），socket 6869。
  - transcript：sweet-babbage-848659/fce478e6-aac8-474d-b4c4-f4f87d94d8fa.jsonl。卡标记 P08BB-LXVK 只命中这一个文件。
  - 档位自检于 08:06Z 前后送达，from-mode bypass。总管核实：第一轮 10 条 assistant 记录为 claude-opus-5-5 / max / max；permissionMode 为 bypassPermissions；CLAUDE_CODE_EFFORT_LEVEL 未设置。
  - 总管 08:07:07Z 调用 set_session_effort(medium)，get_session 回读为 medium。
  - “档位已校准，开始 PREACCEPT”于 08:07:32Z 发出：msg_id feb791c0-d55e-4fbb-96b2-b98ba61f0e79，646 B，sha256 cb9407c2193fb3e388d50fd4e7e41b32fea4ca044c213f62cebd1f816bcc8d33，等于 scratchpad/calib-p08b-b.md。没有 delivery notice。
  - Builder 下一轮从 08:07:35Z 起的记录为 medium / medium，permissionMode 为 bypassPermissions（08:07:32Z）。
- P08B QA：
  - transcript：loving-lamarr-9b3e4e/7cd9377a-a3ff-41d1-8196-9fed327f8dd5.jsonl，含卡标记 P08BQ-KJ8C。
  - 第一轮记录为 claude-opus-5-5 / max / max，permissionMode 为 bypassPermissions（08:05:45Z）。正在做 PREACCEPT。
- 在途：P01 QA 持有门禁位（07:55:30Z 放行）。
- 自 Owner 最近一次输入（07:35:18Z）以来，总管已发 2 条跨会话消息；任务卡不计在内。

## 2026-09-28T08:08:29Z（总管 date -u）更正上一条的送达时刻

- “档位自检于 08:06Z 前后送达”不是精确时刻。更正：P08B Builder 档位自检于 08:06:26Z 送达（总管 transcript 中的记录时间）。

## 2026-09-28T08:34:24Z（总管 date -u）P08B Builder PREACCEPT 已到；P01 QA FINAL：NOT PASS，已核实，退回 P01 Builder 返修

- P08B Builder PREACCEPT：
  - 08:27:44Z 送达，from-mode bypass。units/p08b/builder/PREACCEPT-BUILDER.md，41617 B，sha256 ef145d22fdc7b628509149e67236ba69227bdb3ad419c04109a125289313761f，总管复算一致。
  - Builder 自 08:07:35Z 起 141 条 assistant 记录，全部为 claude-opus-5-5 / medium / medium，bypassPermissions。
  - 要点（只作数据，等 QA 回执到齐后在 START 中裁定）：
    - 端口与调用方上下文由构造函数注入，测试用标注的替身端口，上下文用测试内的 GetUninitializedObject 取得；
    - ProposalId 与 ExpectedAuthority 作为调用方预期值（P1，不要迁移 003；备选 P2 为迁移 003）；
    - 签名前加三项检查：DB 时钟过期、digest 重算、CommandId 绑定；
    - 删除旧端口；端口异常一律上抛，不改状态；
    - 负例 contract 改 1 增 1，postgresql18 增 8；
    - 两侧声明，诊断预期零新增；
    - CO 0.2.0 与 PA 0.9.0 都不变；
    - 句子更正 11 处，设计 141、225 行建议不改。
- P01 QA FINAL：
  - 08:29:19Z 发出并送达，from-mode bypass。
  - QA-REPORT.md 39783 B，sha256 85ede49c644ca0e395980070eb3e3723fd28ba77114c1230a456adb42246b322；EVIDENCE_INDEX.json 27292 B，sha256 5217cf32440fb6274ce95ba0185151028fa991ecb12a376b8d9a1e2f4cb63276。都与消息一致。
  - 总管复算与核对（08:29:44Z–08:31:29Z）：
    - 索引 150 个条目按其 roots 逐个复算，全部一致；
    - 门禁 run c887169266f0da542208578e6507225d 的证据 467364 B，0600；WORKSPACE_DIAGNOSTIC_ONLY，verification_level 为 null；head 为 P1，baseline 为 M19；53 项中 48 PASS / 5 FAIL；
    - 与 M19 的 run 4f3f409bb99d7a1cf9730eab1de31e0a 在 53 个位置上的 id、状态与退出码全部相同；module-governance 的日志 sha256 都是 8e70153c…；
    - QA 自 07:04Z 起 732 条 assistant 记录，全部为 max / max，bypassPermissions；
    - 08:31:29Z 时没有门禁、postgres 或 VBCSCompiler 进程，563xx 无监听。
  - 在 P1 上核实了 F-1、F-2、F-3：
    - F-1：文档第 7、13、14、25、37 行有规则句；第 37 行的“previous row”即第 36 行，其中含 memory-event-ledger（组 B）。
    - F-2：“Structural gaps”只有 4 项。S1、S2、S5 见 Builder PREACCEPT 第 193、197、206 行，第 327 行写明应入缺口清单。soul-registry 的测试从连接串的 Host 取 socket 目录（tests 第 400 行）并连 unix socket（第 631 行），而第 35 行把它列在 dps-direct.env（Host=127.0.0.1）之下。
    - F-3：stop 脚本第 16 行把“pg_ctl 不可执行”和“没有 PG_VERSION”并在一个条件里，都输出 “No DPS PostgreSQL test instance is installed.” 并退出 0。M19 的同一分支用的是固定的 Postgres.app 路径；P1 新增 DPS_POSTGRES_BIN 之后，这个分支会被误配触发。
  - 裁定：
    - 接受 NOT PASS。QA 按 WO 3.4-9 把“存在缺陷”作为 NOT PASS 的独立条件，与工单一致。按 PDD §4.3 表中“实现失败或交付证据缺项”一行，退回 Builder 返修。
    - QA 的 D-4、D-5（07:36:53Z 与 08:01:41Z 两次误写会话 scratchpad，内容不含秘密，已随即删除或移入单元）：按 P00C 与 P08A QA D6 的先例，作为已披露偏离接受，不按停止事项处理。
    - F-4（Builder 未经批准的重跑覆盖了 12 份原始日志）：已无法补救，作为已披露的流程违规（中）接受。
- 返修 START：
  - 08:33:22Z 发给 P01 Builder：msg_id 4266c132-3664-4f2f-ab46-714d914be603，6517 B，sha256 cf468a370db233baa656896724e643c4a7b43fa1d63522b46eb8d59cc9ff68d4，等于 scratchpad/start-p01-b-r2.md。
  - 发送前 Builder 为 medium / medium，bypassPermissions。它从 08:33:31Z 起的记录为 medium / medium，permissionMode 为 bypassPermissions（08:33:23Z）。
  - 要点：
    - P2 为新提交，唯一父为 P1，只改文档、stop 脚本与根 CHANGELOG 的 P01 条目；
    - F-1 删规则句；顺带把 O-9、O-10 两处与事实不符的句子改为属实；
    - F-2 补缺口清单并改正第 35 行，只记录，不改脚本；
    - F-3 拆开 stop 脚本第 16 行的条件，做 failing-before 与修复后验证；
    - 用 P2 的 stop 脚本做回退演练；套件沿用 P1 的结果（PDD §4.3 末段）；
    - 证据写在 Reports/ci/p01-builder-r2/；PG 用 56300–56309；不用窗口，不跑门禁。
- 资源：P01 QA 的门禁 07:56:16Z–08:01:46Z 跑完，门禁位已空；窗口空闲。
- 自 Owner 最近一次输入（07:35:18Z）以来，总管已发 3 条跨会话消息。

## 2026-09-28T08:51:16Z（总管 date -u）P01 返修 FREEZE 已核实，P01 QA 复验 START 已发；P08B QA PREACCEPT 已到；Owner 邮箱落点实测

- P01 Builder 返修 FREEZE：
  - 08:39:16Z 送达，from-mode bypass。
  - 总管核对（08:39:28Z–08:41:20Z）：
    - P2 = f799f0e29f8a82fda2932335cd969fe892e5b941，tree 4f7196d042c139aa71ce4740e30185d20b426406，唯一父为 P1 ed4249ec544f67edb69ad664b43b4cb4a7571f98；分支 codex/p01-df12016 在 P2，porcelain 0 行；
    - Reports/ci/p01-builder-r2/ 中：FREEZE.json 26299 B，sha256 27a0c3c17f9e34984e9586d27939c3a36548229aac727c4ca959d182435e2f87；REPORT.md 9821 B，sha256 2ccdc13330271339b124a93a1ed5fb5f56b7e4e37224e82056380409f1111256；steps.jsonl 17669 B，sha256 fce7a30e44c5a14e7533d505cc7bed77945cf27430801905e2dfe72c36fdd571；
    - 按 FREEZE 中的 argv 重算两份 diff，都与 FREEZE 一致：p1-to-p2.diff 11424 B，sha256 af69e4b3bf41d2f0deb4f771b9e3c94a65f864f308f14f3d62671a1052895446；m19-to-p2.diff 23037 B，sha256 2c006f384493e6b5e042cc7125d5b059e375f5b68fc871343d55d287fc303f17；
    - P1..P2 只改 CHANGELOG.md、Docs/Operations/TestEnvironment_测试环境.md、scripts/stop-test-postgres.sh；
    - logs/ 下 32 份日志（0644）的哈希全部一致。另有两个文件名含空格的 0 字节 diff 文件，REPORT 第 4 节第 1 项已披露；
    - 08:41:20Z 时 563xx 无监听，没有 postgres 或门禁进程。
  - Builder 两次用 Write 工具写 environment/pg/secrets/owner-email.cand（06:48:35Z、08:36:58Z）。每次 17 个字符（16 个字符的值加换行），含 @，两次的值相同。Owner 邮箱字面值因此进入了 P01 Builder 的本地 transcript。总管只取了元数据，没有打印内容。此前的 START 没有禁止这种做法；总管此后的 START 要求 needle 由命令从既有来源导出，不在任何工具调用中键入。
- Owner 邮箱落点实测（08:49:07Z–08:50:14Z）：
  - 做法：needle 由命令从上述两次 Write 调用的输入中导出，只写入 0600 临时文件，用完即删；输出只有计数、文件名与行号，匹配处以占位符遮盖。
  - P01 一侧命中 0：P2 的树与提交对象、Builder clone（工作树，以及 .git 中的文本）、两轮证据目录、单元根文件、environment/checks、P01 QA 的 Reports/。阳性对照：Builder transcript 命中 6 行。另用历史提交 458f9bd 的作者邮箱（29 B，另一个 Owner 地址）对 P01 一侧做同样的扫描，也是 0，阳性对照 2。
  - coordination/ 中有 9 个文件各命中 1 处：
    - DISPATCH-LEDGER.md 第 51 行，即 2026-09-26T16:53:11Z U3-INT 条中的提交身份，同一行还有 Owner 姓名；
    - receipts/ 下 8 份 2026-09-27 的回执：U3 两份，P05-R4、P06-S1 各一份，P10-R2 两份，P17-INT 两份。内容都是当时用 Owner 身份提交的旧做法。
  - coordination/ 不在任何 git 仓库中。账本只追加，第 51 行不改。这 9 处与 P01 Builder transcript 一并列入清理清单，由 Owner 决定。
- P01 QA 复验 START：
  - 08:47:57Z 发给 P01 QA（uds 15327）：msg_id 9bce4c0c-12df-4c2b-8581-0006e1ad249c，正文 9735 B，sha256 607f87bdbfa2009b0d41f03d42a27b5ab254f3c99e4d7f4dc097452381b9d8e8，等于 scratchpad/start-p01-qa-r2.md（9736 B，sha256 dc93e24a5c14eced03c3a94271fce3b726ac3a33d2170e27a55927e495856e3e）去掉末尾换行。
  - 发送前，QA 在 08:20:13Z–08:29:57Z 的 75 条 assistant 记录都是 claude-opus-5-5 / max / max，permissionMode 为 bypassPermissions（07:04:05Z）。08:47:57Z 出现新的 user 记录，permissionMode 为 bypassPermissions。
  - 要点：
    - 冻结对象为 P2 与 r2 证据；
    - 按 PDD §4.3 末段，未变的项目可以沿用第一轮结论，但要写明依据；F-1、F-2、F-3、回退演练、仓库检查、门禁、泄漏与流程在 P2 上实测；门禁 --base M19，只跑一次；
    - 准许读 P01 Builder transcript 的六个字段。needle 一项只核实值没有扩散，不读 transcript 的其他字段，不开 0600 文件；
    - 新增可读：r2 证据目录，以及 checks/ 下的 lib2.sh、stop-p1.sh、stop-p2.sh；scan2.sh（0700）不开；
    - 报告写 Q/Reports/ci/p01-qa-r2/；PG 用 56310–56319；
    - 门禁位附条件分配，前提满足即跑，等候上限 60 分钟。
- P08B QA PREACCEPT 回执：
  - 08:44:35Z 送达，from-mode bypass。
  - units/p08b/qa/PREACCEPT-QA.md 67525 B，sha256 3f5dbb6b18b14a18b1eee739154edf58cf08ff5341154ca31c01a3bdf333d69a，与消息一致（08:50:49Z 复算）。
  - 待总管审读后发 P08B Builder START。
- 资源：门禁位附条件分配给 P01 QA；窗口空闲。
- 自 Owner 最近一次输入（07:35:18Z）以来，总管已发 4 条跨会话消息。

## 2026-09-28T09:03:17Z（总管 date -u）P08B QA PREACCEPT 已审读；P08B Builder START 已发

- P08B QA PREACCEPT（08:44:35Z 送达，67525 B，sha256 3f5dbb6b18b14a18b1eee739154edf58cf08ff5341154ca31c01a3bdf333d69a）要点：
  - A2 基线与工单 §1.1 第 1–8 条核实成立。补充三项：
    - X1：api_get_lease_context 不加锁，也不比较到期，A2 上已到期但未恢复的租约仍会调用端口；
    - X2：ExpectedAuthority 的第 4 个成员 ReleaseBomSha256 只来自调用方的 activation；
    - X3：PA contracts/README:7 中的 “Command wiring” 在 B 之后变假。
  - 门禁基线 run ea7c057e 复核：53 项，48 PASS / 5 FAIL，FAIL 在 #5、#10、#17、#35、#46；module-governance 67 行，没有一行涉及 CO 或 PA。
  - 主要意见：
    - ProposalId 推荐选项 a，即预约快照，不加 003；
    - ExpectedAuthority 作调用方预期值；RequestId 等须确定性派生；
    - 端口异常统一失败关闭；删除旧端口；
    - CO 升 0.3.0，CO 对 PA 的区间下限提到 0.9.0；
    - 设计 141、225 行不改。
  - 规则冲突：根 AGENTS:98 规定的 QA 模型与外审，QA 按 Owner 的裁定执行（opus max，外审 NOT_RUN）。
  - 披露 D1：两次工具输出过大，harness 把它们落盘到该会话的 tool-results 目录（0644）。其中 b5t2cjgd0.txt 含从 0600 门禁证据复制出的日志片段。QA 没有删除，也没有改权限，请总管裁定。
- D1 的裁定：作为已披露偏离接受。落盘由 harness 产生，不是 QA 的命令写入；不要求 QA 删除或改权限。两个文件（bbd8oqps8.txt、b5t2cjgd0.txt）列入清理清单。QA START 时一并告知：读 0600 门禁证据时只把需要的字段写入 environment 内的文件，避免大段输出。
- P08B Builder START：
  - 09:02:30Z 发给 P08B Builder（uds 6869）：msg_id 828a20f3-c713-40de-85ec-a007cd660f7e，正文 13454 B，sha256 217b212b74d63fb063b59cddec61ac24cd1e0e6e49465d1b9d2c78436da1a475，等于 scratchpad/start-p08b-b.md（13455 B，sha256 46a7fdc1f4a564938bc28f40e889aeb0fcee8c305dec2597b696d28ca16577f6）去掉末尾换行。
  - 发送前，Builder 自 08:07:35Z 起的 141 条 assistant 记录都是 medium / medium，permissionMode 为 bypassPermissions（08:07:32Z）。它从 09:02:34Z 起处理本 START，仍为 medium / medium，permissionMode 为 bypassPermissions（09:02:30Z）。
  - 裁定要点：
    - ProposalId 与操作绑定：Builder 的 P1、P2 都不采纳，采用预约快照（QA 的选项 a），依据设计 188 行：
      - 快照取自本实例 AcquireLeaseAsync 绑定租约之后，按 (command, lease, attempt) 存于实例字段；
      - 缺失就在调用端口前失败关闭；
      - dispatch 与快照逐项比对，作为 O4 的判据；
      - ProposalId 与 Parameters 取自快照；
      - 不加 003。
    - ExpectedAuthority 为调用方预期值，只校验形状；ShadowMode 为 true 时失败关闭；SignatureBase64 为空串；签名前的检查顺序已定案。
    - 删除旧端口；必需安全测试保留同一 ID，另加两条断言。
    - 负例：在 Builder 的清单上另加“快照缺失”、字段替换的覆盖面、并发时第二份签名、崩溃窗口两支。
    - 合同声明：
      - 两侧 mode active，status proposed；
      - 通信边六个字段两侧逐字相同：in-process-api、2000、exact-request-only、command_id:lease_id:attempt、policy:sign-execution-authorization、port-error-fails-closed-lease-retained；
      - 不设 preserveProducer；
      - CO 对 PA 的区间改为 >=0.9.0 <1.0.0。
    - 版本：CO 升 0.3.0（module.yaml 与 src csproj）；Command.Contracts 的 csproj 不改，消费方闭包不含 MEL；PA 仍为 0.9.0；floor 不变。
    - 句子更正：
      - PA AGENTS:21 删去末句；
      - PA operations/README:41、CO consumed/README 的新条目、CO operations/README:3 用总管给出的文字；
      - 其余采纳 Builder 的文字；
      - 设计 141、205、225、232 行不改。
    - 窗口改由总管放行：Builder 发“P08B Builder 窗口就绪”，总管回“放行”；跑完写 WINDOW-RELEASED.md；等候上限 60 分钟。理由：P01 QA 附条件持有门禁位，而它的前提中没有窗口检查。
    - Owner 身份 needle 由命令导出，不在任何工具调用中键入字面值。
- 资源：窗口空闲，P08B Builder 就绪后由总管放行；门禁位附条件分配给 P01 QA。
- 自 Owner 最近一次输入（07:35:18Z）以来，总管已发 5 条跨会话消息。

## 2026-09-28T09:26:18Z（总管 date -u）P08B Builder 窗口就绪，已放行；P01 QA 门禁已跑完

- P08B Builder 窗口就绪：09:24:33Z 送达，from-mode bypass。消息称：
  - 候选 B1 为 591451e37540587351e16d869198e62fc009cd58（tree 65ab6ac8e4045c5a72b8dc07e9b18c2dfc818b43）；
  - 除 control-plane-host.integration 外，闭包套件已在 B1 上全部通过；
  - 两项自检都满足；窗口前快照已写入 Reports/ci/p08b-builder/window/tmp-dps-before.ls.txt。
  - 以上是 Builder 的自述，总管没有核实套件结果。
- 总管核对（09:24:59Z–09:25:42Z）：
  - 没有 run_candidate_gate.py 或 run_phase0_gate.py 进程，也没有 Dps.ControlPlaneHost.Tests 或 Dps.SoulRegistry.Tests 进程；
  - 563xx 上只有 P08B Builder 的 PG 在 56360 监听（pid 55069）；
  - P01 QA 唯一一次门禁的 run 1c7fa35b2dad0f5abfe03e32e4b2906c 已结束：运行前快照 09:06:38Z，运行后快照 09:12:43Z。门禁位空闲。
  - B1 提交于 09:21:12Z，author 与 committer 都是 P08B Builder <p08b-builder@local.invalid>，唯一父为 A2。
  - A2..B1 共改 22 个路径，都在工单 §3.1 的允许范围内：
    - CO src 4 个，其中删除 PolicyExecutionAuthorizationSignerV1.cs，csproj 的 Version 改为 0.3.0；
    - CO tests 4 个，其中新增 PolicySigningTestDoubles.cs；
    - CO 的 AGENTS、CHANGELOG、contracts/README、contracts/consumed/README、module.yaml、operations/README、tests/README；
    - PA 的 AGENTS、contracts/README、module.yaml、operations/README；
    - governance/modules 下 2 个，以及根 CHANGELOG。
    - contracts/provided/ 零改动。
- 窗口放行：09:25:48Z 发给 P08B Builder（uds 6869）：msg_id 73ab86b7-f0aa-4bd4-9c52-1388b539ffa3，795 B，sha256 ed12c2b33c6e728de2029bdd25c222cb8f2113638a50381becc23308b3fa5541。窗口现由 P08B Builder 持有，结束以其证据目录中的 WINDOW-RELEASED.md 为准。
- 观察：P01 QA clone 的 Reports/ci/ 下有 playwright-config.json（1251 B，0644，mtime 09:12:42Z，与门禁结束同时），第一轮没有这个文件。来源待 QA 报告的门禁固有写入说明，总管未打开。
- 自 Owner 最近一次输入（07:35:18Z）以来，总管已发 6 条跨会话消息。

## 2026-09-28T09:26:24Z（总管 date -u）更正上一条中的一个时刻

- 上一条“观察”中 playwright-config.json 的 mtime 应为 09:12:38Z（stat 实测），不是 09:12:42Z。09:12:42Z 是门禁 run 目录与 sampler.stop 的 mtime。

## 2026-09-28T09:37:37Z（总管 date -u）P08B Builder FREEZE 已核实；窗口已归还

- P08B Builder FREEZE：09:30:42Z 送达，from-mode bypass。消息称：
  - B1 = 591451e37540587351e16d869198e62fc009cd58，tree 65ab6ac8e4045c5a72b8dc07e9b18c2dfc818b43，唯一父 A2；分支 codex/p08b-e5a81c0；
  - 闭包内 21 个套件 exit 0，数量都不低于 A2：CO contract 14→15，CO postgresql18 24→34，其余相同；
  - 反例有效性：failing-before 3 项与反向验证 R1–R8 都按预期失败；R9（O19 规范化）无法构造，已记录；
  - Tests/ci Ran 243 OK；validate_governance A2 67、B1 67，零新增；PG 已停；口令与 Owner 身份扫描 0 命中；停止事项无。
  - 以上是 Builder 的自述，套件结果总管没有复跑。
- 总管核对（09:31:29Z–09:36:28Z）：
  - 分支与 HEAD 都在 B1；porcelain 0 行；stash 0 条。
  - 按 REPORT 中的 argv 重算 A2..B1 diff：148084 B，sha256 b675d905bdba746d83d582e04b1d45f3007851ba1d2e729fe73f0348ffc9cc0f，与证据中的 A2..B1.diff 逐字节相同。
  - Reports/ci/p08b-builder/：FREEZE.json 9181 B，sha256 ca53580045a5c761320e2208823dbc6795ab30b59e31bde8498ccafc1feaf85f；REPORT.md 21293 B，sha256 b80d951b3af67a226b6e68a19455f84afdeec6ef52359e079b5571a5f5035400；EVIDENCE_INDEX.json 12578 B，sha256 d0f11f8e5aca9377e716be394271eb5932c350fea98f8e87f9b2b629d429691d，共 74 项，哈希与字节数全部一致。
  - 证据目录 75 个文件，只有 scans/owner-identity-scan.txt 为 0600。
  - Owner 身份计数扫描（09:33:54Z）：needle 由命令从 history.git 的作者与提交者导出（排除 @local.invalid 与 noreply@anthropic.com），共 8 条，只写入 0600 临时文件，用后即删；阳性对照命中 3。证据目录命中 0 个文件，含 0600 的扫描文件（只计数，不打印）。
  - 窗口：WINDOW-RELEASED.md 870 B，sha256 51aee857e4febfec7863f8c0275c244d0da0b6a01751fb6082589793110c5211，mtime 09:27:00Z。其中称：窗口内只运行 control-plane-host.integration，09:25:56Z–09:26:27Z，exit 0，75 条。文中的放行时刻 09:25:42Z 是总管消息标题中的时刻，实际发送时刻为 09:25:48Z。
  - 09:36:28Z：563xx 无监听；没有 postgres、门禁或 Dps.*.Tests 进程。窗口与门禁位都空闲。
  - Builder 自 09:02:34Z 起的 assistant 记录都是 medium / medium，permissionMode 为 bypassPermissions。
- Builder 自报、交 QA 判断的事项：
  - REPORT §7：
    - (1) O1/O4 负例在 START 指定的 api_bind_lease 伪造一支之外，另加一支：本实例正常绑定、留有快照后，由表 owner 在单个事务内 DISABLE TRIGGER USER → UPDATE → ENABLE TRIGGER USER，改写 LEASE_BOUND 的 command_sha256 与 payload_json。只在一次性测试库中执行。R1 与 R3 靠这一支失败。
    - (2) CO tests/README 的两处数量由 24 改为 34。
    - (3) 两份 CHANGELOG 的位置。
  - REPORT §6：开发期运行中 ConcurrentIssue 用例的断言写错，已改正；PyPI 读超时，加 --timeout 60 重跑一次。
  - 为 A2 的治理诊断，在自己的 clone 内临时 git stash -u，跑完还原；更早一次用 git archive 导出目录的诊断作废，未计入结果。
  - tmp-outside-unit.txt 列出 /private/tmp 下 09:02Z 之后的 53 个 MTP socket，与 P01 QA 门禁（09:06:38Z–09:12:43Z）时段重叠，无法逐个归属。
- 自 Owner 最近一次输入（07:35:18Z）以来，总管已发 6 条跨会话消息。

## 2026-09-28T09:44:59Z（总管 date -u）P08B QA START 已发

- 09:44:30Z 发给 P08B QA（uds 6944）：msg_id 0c0bbae0-415d-4683-9e3c-aef9fe4d9f70，正文 15281 B，sha256 f76763dc54173a9befd27a34714cf5385b27fcf059ba2b45349febe26739ef8d，等于 scratchpad/start-p08b-qa.md（15282 B，sha256 ba05511981125fa552b431cd18c38e7c418cfb3c698f67ee930aa627f429493b）去掉末尾换行。
- 发送前，QA 在 08:05:52Z–08:44:46Z 的 247 条 assistant 记录都是 claude-opus-5-5 / max / max，permissionMode 为 bypassPermissions（08:05:45Z）。09:44:30Z 出现新的 user 记录，permissionMode 为 bypassPermissions。
- 要点：
  - 冻结对象为 B1 与 FREEZE、REPORT、A2..B1.diff、EVIDENCE_INDEX 四份文件；
  - 预期值以 Builder START 原文为准，准许只读 scratchpad/start-p08b-b.md 与 Builder 的 PREACCEPT；
  - 快照存放方式：START 写按 (commandId, leaseId, attempt) 存放，Builder 实现为按 commandId 存放、三者精确匹配，交 QA 判断两者在失败关闭上是否等价；
  - CO tests/README 的数量同步按 START 本意不算偏离，QA 只核实 34 属实、其他句子未动；
  - 对 QA 回执 U1–U11 的答复：U1、U3、U5、U7、U8、U10 同意；U2 为 N4 必须通过；U4、U9 为先核实 contracts/provided/ 零改动；U6 另加三点（needle 只由命令导出、家目录前缀命中单独计数、继承的历史身份用 git log 核对）；X10、U11 照卡片；
  - QA 回执第 6.1 节的 /Applications/Postgres.app 路径不存在（09:41:39Z 实测），实际在用户目录下的 Applications/Postgres.app，版本 18.4；
  - 请 QA 判定的 Builder 自报事项六项：表 owner 停用触发器改写数据的一支（对照根 AGENTS 测试规则）、ConcurrentIssue 断言的更正、PyPI 重跑、CHANGELOG 位置、stash 与作废的 archive 尝试、R9 无法构造；
  - 窗口与门禁位都附条件分配给 QA：自检满足即进入，不必发消息；门禁只跑一次，--base A2，与 ea7c057e 逐项比对；等候上限 60 分钟；
  - PG 用 56370–56379；网络只允许 PyPI（--require-hashes）与 locked-mode restore 期间的 nuget.org（含 NuGetAudit），都要披露；
  - 读取范围：Builder 证据目录中的 0644 文件；Builder environment/ 中的 genv.sh、selftest.py、rv.py、cph.py 与两份 pg_ctl 日志；b1-wip/ 只做结构扫描；Builder transcript 只读六个字段；ea7c057e 的 0600 证据只为比对而读；
  - 报告写 units/p08b/qa/DSP_ZD/Reports/ci/p08b-qa/，结论只能是 PARTIAL 或 NOT PASS。
- 资源：窗口与门禁位都附条件分配给 P08B QA。P01 QA 在复验，用 56310–56319，不用窗口，它唯一一次门禁已跑完。
- 自 Owner 最近一次输入（07:35:18Z）以来，总管已发 7 条跨会话消息。

## 2026-09-28T09:54:47Z（总管 date -u）P01 QA 复验 FINAL 已核实；P01 以 PARTIAL 收口

- P01 QA 复验 FINAL：09:52:11Z 送达，from-mode bypass。消息称：结论 PARTIAL；门禁 run 1c7fa35b2dad0f5abfe03e32e4b2906c 为 48 PASS / 5 FAIL；发现 0 条，观察 9 条（O-16 至 O-24）；停止事项无。
- 总管核对（09:52:20Z–09:54:02Z）：
  - Reports/ci/p01-qa-r2/ 中：QA-REPORT.md 43265 B，sha256 3ccd3114d03ec269d3b719ee19eb1386958599b97c6c9ccdb6b6a161e3ae30a3；EVIDENCE_INDEX.json 37037 B，sha256 9705cc6b50f3618bd3e586928c7e5fea70f62a219e56fca4d1da4e78eba5f682，与消息一致。
  - 索引 204 项，哈希与字节数全部一致：0644 169 个，0700 31 个，0755 1 个，0600 3 个（门禁 run 中门禁写出的文件）。索引中门禁 Phase0 汇总为 53 项、48 PASS、5 FAIL。
  - QA clone 的 HEAD 为 P2，porcelain 0 行；563xx 无监听；没有 postgres、门禁或 VBCSCompiler 进程。
  - QA 自 08:47:57Z 起的 414 条 assistant 记录都是 claude-opus-5-5 / max / max，permissionMode 为 bypassPermissions。
- 裁定：
  1. 接受 PARTIAL。P01 以 PARTIAL 收口。正式等级为 NONE，候选 P2 为 unsigned、level=null。
  2. O-16（文档第 8 行把 factory-control-plane-host 与两个不查版本的模块并列）：记为观察，不算缺陷。理由：字面属实；误读的后果是该套件以 INFRA_ERROR 失败关闭，不会产生假 PASS；与第一轮 O-9、O-10 的处理一致。与 O-17、O-18 一起作为文档残留，交后续文书单元。
  3. O-19（第 3 行 “dated results belong in unit evidence”）：按页面范围的描述读，不算规范性条文，不算缺陷。
  4. D-8（py_compile 经系统 Python 的 pycache_prefix 在单元外写字节码）：作为已披露偏离接受，定级低。QA 称 09:11:42Z 已删除。总管 09:54Z 实测：该缓存下 units/ 目录的 mtime 为 09:11:42Z，其下没有 p01 的条目，与 QA 所述一致。此后的 START 要求 Python 调用带 -B 或设 PYTHONDONTWRITEBYTECODE=1。
  5. D-10（QA 第一轮在 thinking 中写出过 5 个字符的 Owner 身份片段）：该片段与本机账户名的一段相同，每个路径中都有，不另列清理项。
  6. O-24（harness 把 Owner 邮箱写入各 session transcript 的 session_context）：总管在自己的 transcript 中核实到 22 条 session_context 附件记录带 userEmail 键（只数记录，没有打印值）。据此更正清理清单中的一项：原记“P01 Builder transcript 含 Owner 邮箱”，改为“各 session 的 transcript（含总管）都经 harness 的 session_context 带有 Owner 邮箱；P01 Builder 的 transcript 另多两处 Write 输入”。是否处理由 Owner 决定。
- 更正 09:26:18Z 条中的一句：“第一轮没有这个文件”不对。playwright-config.json 由 Phase0 检查 playwright-static-config 写出（Tools/ci/run_phase0_gate.py:2639）。第一轮门禁已写出（08:01:43Z），本轮门禁改写（09:12:38Z）。依据：QA 报告 O-20，以及其日志 logs/playwright-config.log 中两次门禁快照的 mtime。
- 总管新观察：账户下 Library/Caches/com.apple.python 中有 U3-INT QA 于 2026-09-27T02:58:10Z 留下的 run_s5.cpython-39.pyc（路径镜像 units/u3-int/qa/DSP_ZD/Reports/ci/u3-int-qa/s5/），属单元写入范围以外，账本此前没有记录。列入清理清单，由 Owner 决定。
- 残留：O-16、O-17、O-18 交后续文书单元；O-21（门禁期间有其他单元启动的 VBCSCompiler 常驻进程，没有看到影响）只作观察。
- 资源：P01 QA 不再使用 56310–56319；它唯一一次门禁已跑完。窗口与门禁位仍附条件分配给 P08B QA。
- 本条不需要发消息。自 Owner 最近一次输入（07:35:18Z）以来，总管已发 7 条跨会话消息。

## 2026-09-28T09:55:36Z（总管 date -u）更正上一条中的一个时刻

- 上一条裁定第 4 项中的“总管 09:54Z 实测”，精确时刻为 09:54:10Z–09:54:15Z（取自总管 transcript 中两次 find 调用的时间戳）。

## 2026-09-28T09:55:23Z（总管 date -u）更正上一条的标题时刻

- 上一条标题中的 09:55:36Z 是总管预写的值，不是实测。同一命令中 date -u 的输出为 09:55:18Z，应以此为准。

## 2026-09-28T10:49:11Z（总管 date -u）P08B QA FINAL 已核实；P08B 以 PARTIAL 收口

- P08B QA FINAL：10:47:16Z 送达，from-mode bypass。消息称：结论 PARTIAL；门禁 run 4e900f248a91499cf2e9357e977fdc6d 为 48 PASS / 5 FAIL（required 53）；发现 0 条，观察 11 条，QA 自身过程披露 3 条；停止事项无；QD-2 请总管裁定。
- 总管核对（10:47:29Z–10:48:24Z）：
  - Reports/ci/p08b-qa/ 中：QA-REPORT.md 39422 B，sha256 43ce9cc922ab8ccf1bf75622fb06d8f667b1261306c9e11b5a5304c1befdb86d；EVIDENCE_INDEX.json 20540 B，sha256 d81825b38c784d894e065eb8a07cdf1b390311175109a507c1be649ac8d154ec，与消息一致。
  - 索引 files 72 项（都是 0644）与 referenced 26 项（0600 21 个，是门禁证据与 QA 的 scratch 脚本、日志；0644 4 个；0700 1 个），哈希与字节数全部一致。referenced 中没有口令或 needle 文件。
  - 门禁证据 candidate-evidence-4e900f24….json（0600，门禁写出，总管只读汇总字段）：gate 为 WORKSPACE_DIAGNOSTIC_ONLY，verification_level 为 null，head 为 B1，baseline 为 A2；53 项中 48 PASS、5 FAIL，FAIL 在 #5、#10、#17、#35（exit 3）、#46，与 ea7c057e 相同。
  - QA clone 的 HEAD 为 B1，porcelain 0 行，stash 0 条；563xx 无监听；没有 postgres、门禁、VBCSCompiler、MSBuild 或 Dps.*.Tests 进程。
  - QA 自 09:45:18Z 起的 540 条 assistant 记录都是 claude-opus-5-5 / max / max，permissionMode 为 bypassPermissions。
- 裁定：
  1. 接受 PARTIAL。P08B 以 PARTIAL 收口。正式等级为 NONE，候选 B1 = 591451e37540587351e16d869198e62fc009cd58 为 unsigned、level=null。
  2. QD-2（10:09:22Z 探针在 locked-mode restore 期间从 api.nuget.org 取 repository_signatures/5.0.0 元数据，2007 B，没有下载包）：计入允许面。理由：同一主机，发生在 locked-mode restore 期间，属 NuGet 客户端在 restore 中的元数据访问，没有下载包，已如实披露。今后的 START 把允许面写成“locked-mode restore 期间对 nuget.org 的访问（取包、NuGetAudit，以及 service index、repository signatures 等元数据）”。
  3. QD-1（探针构建留下的 VBCSCompiler 与 MSBuild 节点在门禁期间常驻）：作为已披露偏离接受。门禁结果与基线逐项相同；QA 核对这些进程的 HOME 与 TMPDIR 都在本单元 environment 内之后，才按 PID 结束，没有用全局 pkill。今后的 START 要求探针与反向验证的构建关闭节点复用与共享编译（-nodeReuse:false、-p:UseSharedCompilation=false）。
  4. QD-3（harness 的任务输出文件）：作为已披露事项接受。
  5. START 四的六项：QA 判定都成立，总管接受。其中表 owner 停用触发器、改写数据的一支不违反根 AGENTS 的测试规则。
  6. 快照键：QA 实测三种情形，两种写法在失败关闭上等价；内存差别见 QO-2。
  7. QO-8（CO tests/README 在两处数量之外，同一句还加了“and ten P08 slice B signing-boundary cases”）：Builder REPORT §7.2 已披露，内容属实，不是规范性条文，与 START 的本意一致，接受。
- 残留，交 P08 后续切片：
  - QO-1：表 owner 在 LEASE_BOUND 中改写 LeaseOwner 或 OccurredAt 并自洽地重算 digest 后，仍会签名并派发；把 LeaseExpiresAt 改晚后，端口会被调用，签名后被拒。需要表 owner 权限，不在 O1、O3、O4 的定义内。后续切片决定是否在签名前把这三个字段与 leases 行比对；
  - QO-2：快照条目在其他实例接手或始终未派发时留到进程重启，ponytail 注释没有写升级路径；
  - QO-3：ExpectedAuthority 的 SHA 字段为 null 时抛 NullReferenceException，根源在 CO contracts/provided 中 A2 已有的 CommandContractGuard.RequireSha256，仍在端口前失败关闭；
  - QO-4：api_bind_lease 不核对 command_sha256 与 dispatch_json 的一致性（001 未改），B1 在签名前复算 digest 作了弥补；
  - 以及切片 C 本身：真实 provider 的组合与调用方上下文，冲突与提交不确定、预算与 kill switch、撤销与 revision、ProposalId 与 ExpectedAuthority 的真实核对、O2。
- 文书残留：QO-5（设计 141、225 行）、QO-6（PA contracts/consumed/README:8），交后续文书单元。
- QO-7、QO-9、QO-10、QO-11 是 Builder 报告的措辞或计数不精确，记录在案，不影响候选。
- 资源：P08B QA 的 PG 已停；窗口与门禁位都空闲，没有持有者。按 WO-P08B 第 4 节，对 command-orchestrator 与 policy-approval 的写冲突限制随本单元验收解除。
- 本条不需要发消息。P08B 的 Builder 与 QA session 空闲，不再向它们发消息。自 Owner 最近一次输入（07:35:18Z）以来，总管已发 7 条跨会话消息。

## 2026-09-28T10:51:35Z（总管 date -u）P08 后续切片与 P18 的只读摸底已派出

- 背景：
  - P08A、P08B 都已以 PARTIAL 收口。WO-P08B 第 0 节列出的后续切片为：Command kill switch 的实现；C，Host 组合与用真实 provider 的端到端证明；D，观察 O5。“后续切片按现场重定”。
  - P01 已以 PARTIAL 收口（09:54:47Z 条）。它是 Factory 线 P18 唯一缺的前置。总管已在 09:57Z 前后向 Owner 报告“P18 可以开，先做只读摸底再开单，没有异议就照此做”；截至本条，Owner 没有新输入。
- 两个只读研究代理，都是总管 session 的子代理，不担任 Builder 或 QA，在后台运行：
  - P08 后续切片摸底：10:51:02Z 派出。基线 B1 = 591451e37540587351e16d869198e62fc009cd58，只读 P08B Builder clone 的 git 对象。
  - P18 摸底：10:51:26Z 派出。基线 P2 = f799f0e29f8a82fda2932335cd969fe892e5b941，只读 P01 Builder clone 的 git 对象；回答与产品线的关系时，只读 B1。
  - 约束（两者相同）：只读，报告作为回复返回，不落盘；读前先 stat，不开 0600 文件，不进 environment、pg、s、secrets；git 只用只读对象命令并设 GIT_OPTIONAL_LOCKS=0，不 checkout、不 fetch、不 merge-tree；不构建、不测试、不连库、不联网、不发消息、不读 transcript；不写 Owner 身份。
- 两份报告到后，总管存入 coordination/research/ 并抽查，然后决定开单顺序。开单要发的跨会话消息较多，自 Owner 最近一次输入（07:35:18Z）以来已发 7 条，额度可能不够铺开。

## 2026-09-28T10:51:55Z（总管 date -u）更正上一条中的一个时刻

- 上一条“09:57Z 前后向 Owner 报告”不是精确时刻。更正：该报告是总管 transcript 中 09:57:04Z 的 assistant 消息。

## 2026-09-28T11:15:59Z（总管 date -u）P18 摸底结果入档

- 研究代理的报告于 11:14:45Z 送达，存为 coordination/research/P18-SCOPING-20260928.md（21448 B，sha256 6f0e8ee0248aff664b04d8c5ddfdf19e6b6c470c09416cbf3437386684fc2f6b），只作数据。正文是代理回复的原文，由脚本取出写入。
- 总管抽查（11:14:59Z–11:15:09Z）：报告末节的 8 处引用逐条用只读 git 命令复核，全部属实。另核 factory-control-plane-host 测试要求 SHOW server_version 恰为 "18.4"（test_postgres_integration.py:171、175），而本机 Postgres.app 的版本字符串为 “18.4 (Postgres.app)”。因此该套件要用源码构建的 18.4，这一条是推断，没有连库验证。
- 代理自报三处偏离（用 PDD v2.1 文件的行号；另用 merge-base --is-ancestor 与 rev-list --count；在 units/m19-int 下用 find 并剪掉敏感目录），都是只读动作，接受。
- 要点：
  - P18 进入条件（PDD:813）在 P2 上成立；前置 P17、P04、P01 都只到 PARTIAL，只能作输入，不能据此声称 P18 的任何出口。
  - 候选门禁的 Factory 绑定在 P2 上仍有 M18 记录的四项问题（缺 trust_authority、送 v1 intent、写目标 34 个超过 v2 上限 32、调用签名漂移）；另外 Resolver 会对全仓做双边校验，门禁绑定实际要到全仓治理收口（最迟 P23）才可能打通。
  - 门禁在 Phase0 既有 5 项 FAIL 处就退出，从不执行 PG 套件。P18 的 PG 证据只能来自逐个套件运行，记账口径为“单元层 PASS，门禁层 NOT_RUN”。
  - 建议的第一个切片 S1：Worktree 在真实 PG 与真实 Git 上的持久执行（PDD:820、824 中属于 Worktree 的部分）。写域以 Modules/factory-worktree-manager/ 的 tests 为主，不改合同、不碰共享路径，与产品线零重叠。这是对 PDD:818 步骤顺序的偏离，理由是步骤 1 中 Worktree 与 Host 的部分被 Impact 的 operations/README:15 与 P03 阻断。
  - 两线合流建议在 S1 收口后、下一个改 Manifest 或治理快照的切片之前。
- 开单顺序待 P08 后续切片的摸底到达后一并决定。本条不需要发消息。

## 2026-09-28T11:22:58Z（总管 date -u）P08 后续切片摸底结果入档；开单顺序

- 研究代理的报告于 11:20:36Z 送达，存为 coordination/research/P08C-SCOPING-20260928.md（15844 B，sha256 328cfd58f071546b1f3d04b65932456fd5148bef8df3faeacbcf9b3d4128ee5a），只作数据。正文是代理回复的原文（14241 B），由脚本取出写入。
- 总管抽查（11:21:32Z）：报告末节的 8 处引用逐条用只读 git 命令复核，全部属实；另核 PCO 的 EnsureReady、try 与 catch 位置，CPH kill 开关先例，COIT 的 CreateStore，ContractMajorModes:223 与 CTP 下限，也都属实。代理自报另用 python3 -c 解析 git show 输出、没有落盘；总管 11:22:19Z 核查 ~/Library/Caches/com.apple.python 自派出以来没有新文件，接受。
- 要点：
  - P08 的生产组合在 P08 内做不出来（调用方上下文只能在 PA 内部铸造、Host src 不得引用实现、PA 不持生产私钥、CPH 不依赖 CO），P08 的出口上限是 PARTIAL。
  - 建议顺序：切片 K（只写 CO：kill_command_orchestrator 默认开启，只拦领租与签发；QO-1、QO-2、QO-3 随 K 做，QO-4 留残），然后切片 C（CO tests 经 PA tests 的 test-only fixture 接真实 provider），D（P08A 的 O5、跨库 O2、UNKNOWN 出口）并入 P09。任一时刻只允许一个切片写 CO 或 PA。
- 开单顺序（总管决定）：
  - 先开 P18B，即 P18 第一个切片 S1（Worktree 在真实 PG 与真实 Git 上的持久执行，基线 P2）。理由：总管 09:57:04Z 向 Owner 报告的就是这个打算，Owner 没有异议；它与产品线写域零重叠。
  - P08 切片 K 排在其后，单元名 P08C。自 Owner 最近一次输入（07:35:18Z）以来已发 7 条跨会话消息，一个单元至少要 3 条（Builder 档位校准、Builder START、QA START），额度只够开一个；P08C 等额度恢复后再开，工单先备好。
- 本条不需要发消息。

## 2026-09-28T11:35:11Z（总管 date -u）P18B 开单：工单与两张任务卡

- 工单：coordination/WO-P18B.md（28216 B，sha256 1b7191f4c9478ece3a2d79e62d0b4b72a895cea987c41603359724ced72b9784）。
  - 范围：P18 第一个切片 S1，即实施步骤 2、4（PDD 820、824 行）中属于 Worktree 的部分：factory-worktree-manager 在真实 PG 18.4 与真实 Git 上验证并行、单 writer、崩溃接管、恢复与合并头重测，以测试为主。工单第 0 节写明这是对 PDD 818 行步骤顺序的偏离及理由。
  - 基线 P2 = f799f0e29f8a82fda2932335cd969fe892e5b941；候选名 F1；分支 codex/p18b-f799f0e；提交身份 P18B Builder <p18b-builder@local.invalid>。
  - 端口：Builder 56320–56329，QA 56330–56339（总管 11:30:38Z 实测 563xx 无监听）。不用 Soul/Host 窗口；门禁位由 QA START 安排。
  - PG 二进制：总管裁定一项显式例外，允许本单元 Builder 与 QA 只读复制 units/p01/builder/environment/pg/src/ 下的 postgresql-18.4.tar.bz2（22567173 B，0644，sha256 81a81ec695fb0c7901407defaa1d2f7973617154cf27ba74e3a7ab8e64436094）与同目录的 .sha256（90 B，0644），在各自的 environment 内编译。只限这两个文件，p01 environment 下其他任何文件不得打开或列出。总管 11:30:38Z 实测两文件的权限、大小与摘要。PREACCEPT 轮只许 stat 与核对摘要，不复制。
  - 默认值（由 PREACCEPT 提出、START 定案）：不改 src；新用例并入 PG 套件文件；恢复为“新进程在新的空根上重做并取得更高 fence，崩溃实例的根与提交保留”；kill switch 不在本切片实现、登记为残留；只杀本测试自己起的进程或进程组。
- 任务卡（transcript 实测）：
  - Builder 卡：标记 P18BB-S3UZ，7563 B，sha256 7418d587dabe3880d21bcc7983530e68832f8caa4abd2484006fa5d2fb02d223；11:34:26.995Z 调用 spawn_task，task_6408442d。
  - QA 卡：标记 P18BQ-0WLD，7604 B，sha256 179547eefb3bf048dfcd75fb03b0ff26b448294337b205eda4ef6ff8cedfe6f7；11:34:52.631Z 调用 spawn_task，task_2353b257。
  - 两张卡的 prompt 与 scratchpad 中的卡片文件逐字节相同。任务卡要有人点一下才会开出 session；总管不推断是谁点的。
- 发送额度：自 Owner 最近一次输入（07:35:18Z）以来已发 7 条跨会话消息。本单元还要 3 条（Builder 档位校准、Builder START、QA START），发完即到 10 条左右。P08C（P08 切片 K）等 Owner 下次输入之后再开。
- 本条不需要发消息。

## 2026-09-28T12:05:14Z（总管 date -u）Owner 两条输入；P18B 两个 session 已开出；Builder 档位已校准

- Owner 输入（总管 transcript 的 queued_command 记录，origin 为 human）：
  - 12:02:03.750Z：之后新开 session 或新任务，改用 Claude Code 的终端 CLI 起，不再出任务卡。Owner 要出门几个小时，这期间无法点卡，回来后会告知。
  - 12:03:45.336Z：用终端 CLI 起的 Builder session，先用 Fable 5.1、effort medium；Fable 额度用完后再切回 Opus 5.5。
  - 总管的执行口径：
    - 两条都只约束之后新开的 session。已由任务卡开出的 P18B Builder 与 QA 不换模型，P18B Builder 保持 claude-opus-5-5 / medium。
    - Owner 只提到 Builder，QA 仍为 claude-opus-5-5 / max。
    - 终端 CLI 按 2026-09-27 已验证的命令形态起（env -u CLAUDE_CODE_EFFORT_LEVEL，显式 --model、--effort、--permission-mode bypassPermissions，预生成 --session-id）。已知风险：2026-09-27 Owner 远程操作时，终端命令要本机批准，批准挂起期间总管收不到消息（当时挂了 3 小时 9 分）。因此先把 P18B 推过 Builder START，再用终端起新 session。
- P18B 两个 session 已开出（transcript 与 get_session 实测，总管不推断是谁点开的卡）：
  - Builder：session local_482f41cb-d5cd-42fc-9d54-7bfeb96f223e，createdAt 12:01:07.788Z，transcript 为 ~/.claude/projects/-Users-younghu-Documents-ZennoDroid-DSP-DSP-ZD--claude-worktrees-compassionate-kalam-f6525d/4bf563f3-94e5-4545-9981-3583d0e0d050.jsonl，含卡标记。
    - 第一轮 10 条 assistant 记录，都是 claude-opus-5-5、max/max；permissionMode 为 bypassPermissions。
    - 档位自检经 uds:/tmp/cc-socks/46196.sock 发来，from-mode 为 bypass。第一轮没有写入：12:02:21Z 时 units/p18b 不存在。
  - QA：transcript 为 ~/.claude/projects/-Users-younghu-Documents-ZennoDroid-DSP-DSP-ZD--claude-worktrees-jolly-neumann-9fe996/066f1749-86af-4b5a-b380-c0e09008f103.jsonl，含卡标记。第一条 assistant 记录在 12:01:15.220Z；至 12:04:38Z 共 59 条，都是 claude-opus-5-5、max/max；permissionMode 为 bypassPermissions。正在做 PREACCEPT 的只读阅读。
- Builder 档位校准：
  - 12:02:39.872Z set_session_effort(medium)；12:02:42.581Z get_session 回读 effort 为 medium。
  - 12:03:15.048Z 发出“档位已校准，开始 PREACCEPT”（681 B，sha256 前 16 位 216b6aac0b16f2b3，与 scratchpad/calib-p18b-b.md 去掉末尾换行后相同）。
  - Builder 第二轮自 12:03:18.195Z 起为 claude-opus-5-5、medium/medium（至 12:04:36Z 共 36 条）。
- 发送额度：Owner 最近一次输入在 12:03:45.336Z，此后总管已发 0 条跨会话消息（校准消息早于该输入）。

## 2026-09-28T12:13:24Z（总管 date -u）P08C 工单与两份首条指令已备好，session 暂未起

- 工单：coordination/WO-P08C.md（24508 B，sha256 06320e67fe91e5788dc54e7774b494cb4733d5305ab82b7bca4a049fb11f0749）。
  - 范围：P08 切片 K。实现 kill_command_orchestrator，并在 Command 的 src 内修 P08B 残留 QO-1、QO-2、QO-3。C、QO-4，以及摸底建议并入 P09 的三项，都不在本单元。
  - 基线 B1 = 591451e37540587351e16d869198e62fc009cd58（总管 12:06:43Z 实测：P08B Builder clone 的分支 codex/p08b-e5a81c0 的 tip 等于 B1，tree 65ab6ac8…，父 A2）。候选名 K1；分支 codex/p08c-591451e；提交身份 P08C Builder <p08c-builder@local.invalid>。
  - 端口：Builder 56340–56349，QA 56350–56359（12:10:03Z 实测 563xx 无监听）。PG 与 P08B 相同，用 Postgres.app 18 的二进制。不用 Soul/Host 窗口。
  - 总管另核：B1 上 CO 以外没有模块引用 PostgresCommandOrchestrator；governance/modules 的快照不含模块版本号；executor-gateway 与 audit-metrics 对 CO 的依赖范围都是 >=0.1.0 <1.0.0。
- 角色与起法（按 Owner 12:02:03Z、12:03:45Z 两条指令）：Builder 为 claude-fable-5-1 / medium，QA 为 claude-opus-5-5 / max，都在终端面板用 CLI 起，permission mode 为 bypassPermissions，session id 预先生成。
- 首条指令（scratchpad，由 CLI 以 $(cat) 注入；档位自检与 PREACCEPT 合在一轮）：
  - Builder：first-p08c-b.md（6925 B，sha256 af7aa931aba161b88a1029b02971d54315335603b94169daae574844ea818183），session id 565b94c0-22d3-4623-80f3-b92b0c24a16d。
  - QA：first-p08c-qa.md（7355 B，sha256 11e6eb2de090fd5d0912db3a4ebcdfbee5e85fcbebccb3f2fb171545693449d2），session id c63fd6ad-b9dd-4494-bf9b-ec25ef9b5353。
- 起的时机：等 P18B 的 Builder START 发出之后。理由：终端命令若弹出本机批准框，总管在批准前收不到消息；先让 P18B 的 Builder 开工，挂起的影响最小。
- 本条不需要发消息。

## 2026-09-28T12:27:29Z（总管 date -u）P18B Builder PREACCEPT 已到，已核实

- 消息于 12:25:23.788Z 进入总管队列，来自 uds:/tmp/cc-socks/46196.sock，from-mode 为 bypass。
- 回执文件：units/p18b/builder/PREACCEPT-BUILDER.md，45713 B，410 行，0644，sha256 6802fe8dfc3d31a087d0c66ccb5392aa1b816a670a5e1b52406ed99bfedfe4ac。总管 12:25:58Z 实测，与消息所述一致；units/p18b 下只有 builder/ 目录与这一个文件。
- QA 的独立回执尚未落盘。按 2026-09-27 的教训，本条只记哈希与“已核实”，不写回执内容；内容与裁定写进 Builder START 及其账本条。
- 本条不需要发消息。

## 2026-09-28T12:56:36Z（总管 date -u）P18B QA PREACCEPT 已到；两份回执要点；Builder START 已发

- QA 回执：
  - 消息于 12:48:07.723Z 进入总管队列，来自 uds:/tmp/cc-socks/46270.sock，from-mode 为 bypass。
  - 文件 units/p18b/qa/PREACCEPT-QA.md，56457 B，537 行，0644，sha256 d8f91b9ae44982479d540483abb6e8412e3bd385879e902ad06d6ee7ef58d8b9。总管 12:48:15Z 实测，12:50:14Z 复核未变。
  - 声明未读 Builder 回执，未与 Builder 通信；档位自检为 claude-opus-5-5、max/max、bypassPermissions。
- Builder 回执要点（12:27:29Z 条只记了哈希）：
  - 基线、前置、进入条件与顺序偏离都与现场相符。history.git 不含 P2，按工单从 P01 clone fetch。
  - 新发现两处，提请纳入 F1：
    - G1：`_verify_fence` 不核对租约 holder_identity 是否等于条目的 writer_identity；
    - G2：`merge_and_retest` 在合并头重测之后不再核对 fence。
  - 覆盖用例 T1–T10 全部放在 PG 套件，另加辅助脚本 tests/worktree_writer_probe.py；“合同单 owner”提议登记为残留。
  - 逐字给出了四处文档文本：tests/README、WT CHANGELOG 的 `## Unreleased`、根 CHANGELOG 条目、operations/README 第 3 行更正。另附第 9 节 12 项默认做法。
- QA 回执要点：
  - 独立核对结果一致。另报 PG 脚本三处：不强制显式给出 BIN；:42–45 的版本检查接受带后缀的版本串；initdb 口令短暂落盘（:205–216）。
  - 独立读到与 G1、G2 相同的两处，另有 `_run_test` 固定 60 秒超时。
  - 列出必须拒绝的做法与探针 P-1..P-14。
  - 请 QA START 定案以下几项：
    - 合同单 owner；
    - 门禁访问 nuget.org 元数据；
    - QA 读取 Builder environment 的范围（默认只读脚本、日志与 env 文件）；
    - 是否读 Builder 的 transcript（默认不读）。
- Builder START：
  - 12:55:40.033Z 发往 uds:/tmp/cc-socks/46196.sock，8733 B，sha256 67377f27c86ca647884a2a937e846309e2c0dc84ef598270b9b45566ef6899e8，与 scratchpad/start-p18b-b.md 去掉末尾换行后相同。
  - 发送前 get_session：isRunning 为 false，claude-opus-5-5，effort medium。
  - Builder 的 transcript 中 12:55:40.073Z 入队，12:55:43.525Z 起为 claude-opus-5-5、medium/medium，permissionMode 为 bypassPermissions（12:55:40.087Z）。
  - 裁定要点：
    1. T1–T10 照做。合同单 owner 改为纳入轻量覆盖：第二个 holder 对合同锁键的 acquire 须抛 LeaseConflict；合同条目的提交与合并须经 verify_fence_fact。生产 planner 产不出 contract_worktree 的，停下回报。
    2. G1、G2 纳入 F1。只加失败关闭判断，抛现有的 StaleFence，不改合同输出形状。T9、T10 在 P2 的 src 上须失败。加跑 Runner 的 unit、contract 与 Host 的 unit、contract、simulation。
    3. operations/README 第 3 行逐字更正，纳入。kill switch、批量撤销、AGENTS.md:35 与 module.yaml:46 登记为残留。
    4. QA 列出的必须拒绝做法对 Builder 同样适用。另要求 killpg 前置核对、临时仓库关闭 gc 与 maintenance、tearDown 查 advisory 锁。
    5. PG 按 P01 同构构建（含 pgcrypto），显式给出 DPS_POSTGRES_BIN。
    6. Tests/ci 用 -I -B。validate_repo 的计数只随新增或删除的文件变化。PG 套件连续跑 3 次。Host 的 PG 套件不需要窗口。
    7. 文档用 Builder 的逐字文本，条件标记不写入正文；实测不符时只许删改，不许新增。
    8. clone 内所有 git 命令都在 env 中设 P18B Builder 身份的四个变量，避免 reflog 写入系统推断出的身份。
    9. FREEZE.json 与 REPORT.md 放在单元根。
- 工单更正（不改工单文件）：
  - 第 0 节“与产品线零重叠”应为“除根 CHANGELOG 外零重叠”；
  - 第 1.1 节第 8 条：脚本并不强制显式给出 BIN（:13 默认指向 Postgres.app）；
  - 第 3.1 节第 1 项漏列合同单 owner，已由 START 补入；
  - 第 3.1 节网络条的“不需要 nuget.org”只对 Builder 成立。QA 门禁的 locked-mode restore 会访问 nuget.org，按 QD-2 先例在 QA START 中写明；
  - PG 套件的环境说明没有写 git（module.yaml:43 与 POL 禁改），登记为文档残留。
- 留给 QA START 的事项：
  - nuget.org 的允许面，用 QD-2 的措辞；
  - 读取 Builder environment 的范围：只读脚本、日志与 secrets/env/*.env，不读口令、密钥与 PG 数据；
  - 不读 Builder 的 transcript；
  - G1 纳入后，QA 探针中的接管须用条目的 writer 身份；
  - Tests/ci 的 -B 口径与 Builder 一致。
- 发送额度：Owner 最近一次输入在 12:03:45.336Z，此后总管已发 1 条跨会话消息（本条 START）。
- 下一步：用终端 CLI 起 P08C 的两个 session（Builder 为 claude-fable-5-1 / medium，QA 为 claude-opus-5-5 / max）。

## 2026-09-28T12:59:01Z（总管 date -u）P08C 的两个 session 暂不起，等 Owner 回来

- 事实：
  - 终端面板工具（mcp__terminal__run_in_terminal）每次执行命令都要 Owner 在本机同意：12:58Z 读到的工具说明写明命令“exactly as the user will see it approved and run”，并带一个由应用填写的 _consent 字段。
  - 2026-09-27 实测：Owner 远程操作时，这个同意只能在本机给出，挂起期间总管收不到任何消息（10:20Z 至 13:29Z，共 3 小时 9 分）。
- 冲突：Owner 12:02:03Z 要求外出期间新 session 用终端 CLI 起，但 Owner 外出时无法在本机点同意。这时调用，P08C 不会开工，总管还会被挂住，P18B 的 FREEZE 与 QA START 也跟着停。
- 总管的选择：
  - 先不调用终端工具，P18B 照常推进；
  - P08C 的工单与两份首条指令（12:13:24Z 条）保持不变，Owner 回来并能在本机点同意时再起；
  - 不改用 Bash 后台 headless、子代理等其他起法：那些都不是 Owner 指定的方式，也看不到。
- 本条不需要发消息。

## 2026-09-28T13:19:57Z（总管 date -u）P18B Builder FREEZE 已到并核实；QA START 已发

- FREEZE 消息于 13:16:57.957Z 进入总管队列，来自 uds:/tmp/cc-socks/46196.sock，from-mode 为 bypass。
- 总管只读核实（13:17Z–13:18Z）：
  - F1 = 4c91fe275b1a4685cd15fa38453c5ca324a0022e，tree f9aee025beebb4aca7300a0a8f0eb1988d25cf65，唯一父 P2。author 与 committer 都是 P18B Builder <p18b-builder@local.invalid>，提交时间 13:07:23Z。分支 codex/p18b-f799f0e 的 tip 等于 F1。
  - 按工单第 3.2 节第 7 项的 argv 复算 diff：41590 B，sha256 28bf11e99b43faac7ae34745a65589fae2776ef2846e81f4ae9939abded948c1，与 FREEZE 一致。
  - 改动 7 个路径：根 CHANGELOG.md；WT 的 CHANGELOG.md、operations/README.md、src/git_worktree_materializer.py、tests/README.md、tests/test_postgres_lease_integration.py；新增 tests/worktree_writer_probe.py。
  - 禁改路径零改动：WT 的 module.yaml、AGENTS.md、contracts、migrations、requirements.*，以及 Tools、Tests、governance、scripts、.github、Docs、Dps.slnx；WT 以外的模块零改动。
  - 只有 1 个 worktree，没有 alternates，stash 0 条，porcelain 0 行。
  - src 的改动是 G1（`_verify_fence` 加一个 holder_identity 条件）与 G2（把合并前的核对循环提成内部函数，在重测之后再调用一次），都抛 StaleFence。四处文档与 START 批准的文本相同，条件标记没有写入正文。
  - 文件：
    - FREEZE.json，6237 B，0644，sha256 54f0a540ba3a29b5e8c952347d4b505b4c2e6412010909b3bac9123e78802bbc；
    - REPORT.md，22402 B，0644，sha256 324ddab06258dbabf26cdeae1bed70305901840d212776026608fe3830afe06f。
  - Builder 单元根下 DSP_ZD、environment 与 .git 的建立时间都是 12:55:56Z，晚于 START 的 12:55:40Z。
  - 13:18:10Z：563xx 无监听；没有 postgres、门禁、probe、check.py、VBCSCompiler 或 testhost 进程；/private/tmp 下没有 .s.PGSQL.*。
  - Builder 自 12:55:43.525Z 至 13:17:10Z 共 148 条 assistant 记录，都是 claude-opus-5-5、medium/medium；permissionMode 为 bypassPermissions。
- REPORT 所述（数据，交 QA 独立验证）：
  - F1 自测 rc 全为 0：WT unit 9、contract 4、git 4、PG 15（5+10）；Host PG 12；Runner 44/6；Host 76/9/2；Tests/ci（-I -B）261 OK；governance verified: 3；validate_repo json 299、python 97、markdown 247。
  - 回归：F1 的测试放在 P2 的 src 上，恰好 T9、T10 失败，其余 13 个通过。
  - 反向验证 11 项全部转红。稳定性连跑 3 次加 -v 一次，都是 Ran 15 OK。
  - 合同单 owner 的覆盖落在 T1、T2 中。
  - 超级用户只在启动脚本的 bootstrap 中使用。
  - 披露：
    - Tests/ci 运行期间在 clone 内生成 3 个被忽略的 .pyc；
    - Runner unit 在单元内的 HOME 下写了 p17a-normal-output；
    - 执行轮中途发生过上下文压缩，之后接着做。
  - 残留 6 项（REPORT 第 10 节）。其中漏了工单第 4 节的两项已知残留：门禁路径上的 PG 变量冲突与 Factory 绑定；floor 落后于实测。已在 QA START 中要求 QA 补齐。
- QA START：
  - 13:19:39.958Z 发往 uds:/tmp/cc-socks/46270.sock，7641 B，sha256 f2dbf7e41775e47ffb57814019c55ca7930c0d057925d141587b62265c644c90，与 scratchpad/start-p18b-qa.md 去掉末尾换行后相同。
  - 发送前 get_session：isRunning 为 false，claude-opus-5-5，effort max。
  - QA 的 transcript 中 13:19:40.003Z 入队，permissionMode 为 bypassPermissions（13:19:40.012Z）。
  - 要点：
    - 答复了 QA 回执第 7 节的 14 项；
    - nuget.org 的允许面用 QD-2 的措辞；
    - 读取 Builder environment 的范围：可读脚本、日志与 scratch；不开 pg/secrets、pg/data、s、tmp、home、cache，也不开任何 0600 文件；env 文件的结构在 QA 自己的实例上核对；
    - 不读 Builder 的 transcript；
    - G1、G2 对 QA 探针的影响：接管须用条目的 writer 身份；P-10、P-14 改按 F1 的行为验收；P-11 每条目在合并中核对 2 次。
  - 门禁位 13:18:10Z 实测空闲，分配给 P18B QA，仍按“门禁就绪”“门禁放行”两步走。
- 发送额度：Owner 最近一次输入在 12:03:45.336Z，此后总管已发 2 条跨会话消息。
- P08C 仍按 12:59:01Z 条等 Owner 回来再起。

## 2026-09-28T14:01:27Z（总管 date -u）更正 12:59:01Z 条的归因；P08C 的两个 session 已用终端 CLI 起

- Owner 输入（总管 transcript 的 queue-operation 记录）：
  - 13:55:07.853Z：问 P08C 的 session 在哪。
  - 13:58:34.817Z，附一张终端面板截图：记得总管以前能自己开 session、不用 Owner 点，问为什么现在要点。
- 更正：12:59:01Z 条所述的事实不成立。
  - 总管 transcript 实测：2026-09-27 05:37Z–09:10Z，总管在 bypass 模式下 10 次调用 run_in_terminal 起 CLI session，每次 0.6–15 秒返回，都没有等 Owner 同意。
  - 唯一一次挂起在 09-27 10:20:26Z，起 U5 的两个 session，到 13:29:43Z 以“用户中断”结束。挂起前的情况：
    - 10:17:54Z 两次调用因“本 session 已开 6 个终端 tab”被 hook 拒绝；
    - 10:19:31Z 发生过一次上下文压缩；
    - 10:20:05Z 总管关了 4 个 tab。
  - 挂起的原因至今没有查明。
  - 两次错误归因：
    - 当时记成“远程操作时须本机批准”，这是没有核实的推断；
    - 12:59Z 又把工具说明中的 `_consent` 字段读成“每次都要同意”，同样不成立。
  - 结果：P08C 被不必要地推迟了约 1 小时。
- P08C 起 session 前的核对（14:00:18Z）：
  - 两份首条指令与工单的 sha256 都没有变；
  - units/p08c 不存在；两个预生成的 session id 都还没有 transcript；
  - P08B Builder clone 中 codex/p08b-e5a81c0 的 tip 仍为 B1；
  - 56340–56359 无监听（563xx 中只有 P18B QA 的 56330）；
  - 本 session 自开的 tab 只有 c10、c11，没有到 6 个的上限。
- Builder：
  - tab c12，pid 28957，14:00:31Z 启动，cwd 为 dps-air。
  - 用 KERN_PROCARGS2 读出的 argv：--model claude-fable-5-1 --effort medium --permission-mode bypassPermissions --session-id 565b94c0-22d3-4623-80f3-b92b0c24a16d -n "P08C Builder"。初始环境中没有 CLAUDE_CODE_EFFORT_LEVEL。
  - 首条指令为 first-p08c-b.md（6925 B，af7aa931…）。
  - transcript 首条 assistant 记录在 14:00:45.182Z；至 14:01:11Z 共 8 条，都是 claude-fable-5-1、medium/medium；permissionMode 为 bypassPermissions。
- QA：
  - tab c13，pid 29173，14:00:38Z 启动，cwd 为 dps-air。
  - argv：--model claude-opus-5-5 --effort max --permission-mode bypassPermissions --session-id c63fd6ad-b9dd-4494-bf9b-ec25ef9b5353 -n "P08C QA"。初始环境中没有该变量。
  - 首条指令为 first-p08c-qa.md（7355 B，11e6eb2d…）。
  - transcript 首条 assistant 记录在 14:00:47.432Z；至 14:01:10Z 共 10 条，都是 claude-opus-5-5、max/max；permissionMode 为 bypassPermissions。
- 两次调用都立即返回，没有出现同意框。
- 发送额度：Owner 最近一次输入在 13:58:34.817Z，此后总管已发 0 条跨会话消息。

## 2026-09-28T14:06:16Z（总管 date -u）P18B QA 门禁就绪已到；门禁位已放行

- Owner 输入：14:03Z 前后（总管 transcript）对终端起 P08C、Builder 用 claude-fable-5-1 / medium 表示认可。这不是新指令。
- “P18B QA 门禁就绪”于 14:05:21.451Z 进入总管队列，来自 uds:/tmp/cc-socks/46270.sock，from-mode 为 bypass。
  - QA 自报：PG 于 14:04:46Z 由 stop 脚本停止；563xx 无监听；没有 DPS_* 或 PG* 变量；没有残留进程、连接、advisory 锁或测试 schema。
- 总管 14:05:44Z 复核：
  - 563xx 无监听；
  - 没有 postgres、门禁、dotnet、testhost、VBCSCompiler 或 MSBuild 进程；
  - /private/tmp 下没有 .s.PGSQL.*，也没有 dps-phase0-executor-* 目录；
  - units/p08c 尚不存在，P08C 两个 session 在做只读 PREACCEPT；
  - QA 自 13:20:22Z 起共 243 条 assistant 记录，都是 claude-opus-5-5、max/max；permissionMode 为 bypassPermissions。
- 放行：
  - 14:06:00.800Z 发往 uds:/tmp/cc-socks/46270.sock，805 B，sha256 f296bfdf978ce84a1b1dc471d1d17bde0d7be898464874edd1233b6bef9fb771，与 scratchpad/release-p18b-qa-gate.md 去掉末尾换行后相同。
  - QA 的 transcript 中 14:06:00.821Z 入队，14:06:11Z 起继续为 max/max。
  - 放行内容：门禁位归 P18B QA，只跑一次；异常不重跑，停下回报；跑完即释放。
- 另记：QA 的 transcript 中 13:24:43Z 有一条 task-notification，是它后台构建 PG 完成的通知（exit 0），不是外部消息。其输出文件在 harness 目录 /private/tmp/claude-501/… 下，属于 harness 固有写入，由 QA 在报告中披露。
- 发送额度：Owner 最近一次输入之后，总管已发 1 条跨会话消息（本条放行）。

## 2026-09-28T14:06:42Z（总管 date -u）更正上一条的一处时间

- 上一条把 Owner 认可的时间写成“14:03Z 前后”，不精确。按总管 transcript 的 queue-operation 记录，准确时间是 14:05:17.828Z。

## 2026-09-28T14:16:34Z（总管 date -u）P08C Builder PREACCEPT 已到，已核实

- 消息于 14:16:18.476Z 进入总管队列，来自 uds:/tmp/cc-socks/28957.sock（pid 28957，即 P08C Builder），from-mode 为 bypass。
- 回执文件：units/p08c/builder/PREACCEPT-BUILDER.md，42080 B，312 行，0644，mtime 14:15:58Z，sha256 c12ed7297ef566233b9b8c908cd87fe639e9528421f28b626a5c3a64e9d488ae。总管 14:16:27Z 实测，与消息所述一致。units/p08c 下只有 builder/ 目录与这一个文件。
- 档位：Builder 自 14:00:45.182Z 至 14:16:17Z 共 97 条 assistant 记录，都是 claude-fable-5-1、medium/medium；QA 同期 163 条，都是 claude-opus-5-5、max/max；两者 permissionMode 都是 bypassPermissions。
- QA 的独立回执尚未落盘，本条只记哈希与“已核实”，不写回执内容。
- 本条不需要发消息。

## 2026-09-28T14:45:14Z（总管 date -u）P18B QA FINAL 已核实；P18B 以 PARTIAL 收口

- 消息于 14:42:24.600Z 进入总管队列，来自 uds:/tmp/cc-socks/46270.sock（P18B QA），from-mode 为 bypass。消息称：结论 PARTIAL（单元层 PASS，门禁层 NOT_RUN）；门禁 run 8ae2c4d317036df332b107093118c53b 为 48 PASS / 5 FAIL；发现 1 项，观察 12 项；停止事项无。
- 总管核实（14:42Z–14:45Z，只读）：
  - QA-REPORT.md：41557 B，446 行，0644，sha256 547809975c00b8eaa7a96263e9bd7f0dfcbbfbf77956b2f64c4cafe962d848ad。EVIDENCE_INDEX.json：25423 B，sha256 3769df81dca2689362044944f1313dcb5d0c6654cb23961f9a4baa1f347e1c19。两者与消息一致。
  - EVIDENCE_INDEX 列 109 个文件。总管逐个复算字节数、权限与 sha256，109 个全部一致。
    - 其中 4 个是 0600：门禁写出的 3 个，以及 QA 自己的 logs/leak-scan-attempt1-zsh-rc-missing.log（677 B）。
    - 复算哈希时读了这 4 个文件的字节，没有显示内容。
  - 门禁证据 candidate-evidence-8ae2c4d3….json（0600，门禁写出，总管只读汇总字段）：
    - gate 为 WORKSPACE_DIAGNOSTIC_ONLY，verification_level 为 null，overall FAIL；
    - baseline 为 P2，head 为 F1；起止 14:07:32.706Z–14:13:33.842Z；
    - 53 项中 48 PASS、5 FAIL，其余计数都是 0。
  - 与基准 run 1c7fa35b…（P01 QA，baseline M19，head P2）逐项比对：
    - 53 项的 ID 与顺序相同，status 与 exit_code 零差异；
    - FAIL 集合相同：#5、#10、#17、#35（exit 3）、#46；
    - module-governance 的 log_sha256 相同（8e70153cd4122855…）；limitations 相同。
  - F1 没有变：Builder 分支 codex/p18b-f799f0e 仍指向 4c91fe27…；FREEZE.json 与 REPORT.md 的 sha256 与 13:19:57Z 条相同；Builder transcript 自 13:17:12Z 起没有新记录。
  - 档位：QA 自 13:20:22Z 至 14:42:45Z 共 448 条 assistant 记录，都是 claude-opus-5-5、max/max；permissionMode 为 bypassPermissions（13:19:40Z）。
  - 门禁位空闲（14:45:14Z）：没有门禁、postgres、dotnet、testhost、VBCSCompiler 或 MSBuild 进程；563xx 无监听；/private/tmp 下没有 .s.PGSQL.*，也没有 dps-phase0-executor-* 目录。
- 报告要点：
  - 单元层成立的项：身份、diff 审查、行为验收 (a)–(h)、套件重跑、泄漏检查与流程。
    - QA 的 25 项独立探针连续 3 次全部通过。放到 P2 的 src 上，恰有依赖 G1 或 G2 的 5 项失败。
    - 回归 T9、T10 在 P2 的 src 上失败，在 F1 上通过。
  - 门禁层：门禁在 Phase0 既有的 FAIL 处退出，没有执行 PG 套件，记为 NOT_RUN（残留 R-10，与 R-4 同源）。
  - 发现 F-1（低，报告层）：Builder REPORT 的残留清单漏列工单第 4 节的两项。QA 已在报告第 11 节补齐。
  - 观察 O-1 更正了总管 QA START 第二部分第 2 条的前提：
    - 路径重叠分支（MAT:268–272）没有任何候选用例执行到；git 套件 :172 的用例先被 escape 检查拒绝。
    - 这个前提来自 Builder 回执，总管当时没有核实。
    - QA 探针 P-3c 实测该分支行为正确；缺用例的部分登记为 R-11。
  - O-11（Builder REPORT 第 5 行写 START 时刻为 12:54:18Z）：这是 START 正文首行自带的签发时刻（scratchpad/start-p18b-b.md 首行）；实际发出时刻为 12:55:40.033Z。两者都晚于两份 PREACCEPT。
  - QA 自报偏离 D-1 至 D-5。D-1：约 13:42Z 与 13:46Z 两次调用 /usr/bin/python3（`-c 'print(1)'` 与 `--version`），违反工单第 3.1 节末项；QA 实测 ~/Library/Caches/com.apple.python 没有新文件。
  - 残留 R-1 至 R-13 及其归属见报告第 11 节。
- 裁定：
  1. 接受 PARTIAL。P18B（P18 切片 S1）以 PARTIAL 收口；候选 F1 unsigned，level=null，不构成 P18 的出口。
  2. D-1 作为已披露偏离接受。理由：QA 实测没有产生单元外写入，之后也没有再调用。D-2 至 D-5 作为已披露事项接受。
  3. O-1 照实记在本条，不改 START 文件。
- 清理清单新增（由 Owner 决定）：P18B Builder 与 QA 的 environment（含 PG 数据目录与 0600 secrets）；QA 会话 harness 另存的 2 个 tool-results 文件（12:05Z、14:28Z）。
- 下一步（总管决定）：
  - P18-SCOPING 第 3 节与第 5 节第 7 项建议：S1 收口后、S2 开单前，先开“两线合流”单元。S2、S3 都要改 Manifest、治理快照或共享路径，必须排在合流之后。
  - 合流的产品线一侧要等 P08C 收口：P08C 正在 B1 上做 K1，现在合流，K1 之后还得再合一次。
  - 所以 P08C 收口之前不开新单元。合流单元的工单在等候期间准备，合流对象为 F1 与 P08C 收口时的产品线候选。
- 本条不需要发消息。

## 2026-09-28T14:52:57Z（总管 date -u）P08C QA PREACCEPT 已到并核实；两份回执要点；Builder START 已发

- P08C QA PREACCEPT：
  - 消息于 14:45:24.444Z 进入总管队列，来自 uds:/tmp/cc-socks/29173.sock（pid 29173，即 P08C QA），from-mode 为 bypass。
  - 回执文件 units/p08c/qa/PREACCEPT-QA.md：64317 B，718 行，0644，mtime 14:44:45Z，sha256 ff5392c7bf20a825aa11fc7cabc3c1627ac07cf0367cb2e996b9f7192e63bfdc。总管 14:45:58Z 实测，与消息一致。
  - 档位：QA 自 14:00:47Z 至 14:45:36Z 共 257 条 assistant 记录，都是 claude-opus-5-5、max/max；permissionMode 为 bypassPermissions（14:00:43Z）。
- 两份回执都已落盘，以下记内容要点。
- Builder 回执（14:16:34Z 条已核实哈希）要点：
  - 开关做成 Options 的 init 属性 `KillCommandOrchestrator`，默认 true；只拦 Acquire 与 Issue。
  - 专用异常继承 InvalidOperationException，检查放在两个方法首行、EnsureReady 之前；InMemory 不加开关。
  - QO-1：把绑定时的 digest 存进进程内预约，签名前比对。QO-2：补注释。QO-3：null 检查，并提议扩到 activation 的两个字段。
  - 版本升 0.4.0，floor 不动；用例 C1、K1、K2、Q1、Q3；AGENTS.md:41 提议改写。
- QA 回执要点：
  - 独立核对工单第 1、1.1 节，一致。另外补充：
    - :747 不比对 SchemaVersion；
    - activation 的两个字段为 null 时同样抛 NRE；
    - tests/README.md:7 会与 :5 一起变假；
    - AGENTS.md:41 是规范性要求，K 之后不变假，建议不改；
    - 工单第 0 节第 20 行把摸底标为“推断”的内容写成了定论。
  - 对第 1–10 项的看法与 Builder 基本一致。额外要求：
    - kill 用例断言精确类型；PG 用例用默认 Options；
    - QO-1 的回归至少覆盖 a–e 五个变体，每个都在 B1 加测试的状态上失败；变体 c 必须断言端口调用次数为 0；
    - QO-2 的注释写清准确的上限与升级路径；Enqueue 放行的后果要如实披露。
  - 门禁比对基线：run 4e900f24… 的 53 项指纹 1fb68471c6463dfb；#5 集合指纹 d7cfd24b07f33481；#10 归一化后指纹 c69bb85c4f682f29。
  - 不清楚之处 14 项，各附默认做法（回执第 7 节）。
- 对工单的更正（记入账本，不改工单文件）：
  - 第 0 节第 20 行“P08 的生产组合在 P08 内做不出来”应标为推断（摸底 P08C-SCOPING:73）。
  - 第 1.1 节第 6 条：:747 没有比对的字段不止三个，还有 SchemaVersion 等。绑定时的 digest 覆盖全部字段，修法不受影响。
  - 第 1.1 节第 8 条：B1 上 PCO 调 RequireSha256 的共 4 处（:606、:609、:619、:620，总管 14:49Z git grep 实测），不止 :619–620。
  - 第 1.1 节第 13 条：漏列 tests/README.md:7；对 AGENTS.md:41 的判断由 START 推翻。
- P08C Builder START：
  - 14:52:26.793Z 发给 P08C Builder（uds:/tmp/cc-socks/28957.sock）：msg_id b48be6ef-93ef-474d-bb97-3cd8f5ab1d6a，正文 16287 B，sha256 db8b25fca163795949fa51911f28a2b10b0d17f99c25b8cc1c61bad1cd5a98d7。它等于 scratchpad/start-p08c-b.md（16288 B，sha256 b07f8efb52338cf5095b1a9f703cb66b2ab540e82d56815511a2c9a58ef80b8b）去掉末尾换行后的内容。
  - 发送前 Builder 自 14:16:29Z 起空闲；pid 28957 在运行；98 条 assistant 记录都是 claude-fable-5-1、medium/medium；permissionMode 为 bypassPermissions。
  - 14:52:26.967Z 进入 Builder 队列。Builder 自 14:52:36Z 起处理，仍为 claude-fable-5-1、medium/medium，permissionMode 为 bypassPermissions（14:52:26Z）。
  - 裁定要点：
    - 开关、拦截范围、异常类型与位置、InMemory 不加开关、0.4.0 与 floor 不动：采纳 Builder 提案。kill 用例一律断言精确类型。
    - QO-1：比对单独写成一个检查，放在 :639–646 之后、构造签名请求之前，另用新消息，现有消息不动。
    - leases 行改写：不加比对，也不列为残留。依据总管核对的 B1 源码：
      - :635 同时检查行上与载荷中的到期时间，载荷已由 digest 钉住，改晚无效；
      - Issue 不读行上的 lease_owner（:1001）。
      - Builder 回执拟写入 CHANGELOG 的这一项残留相应删去。
    - QO-2：升级路径改为“到期或恢复时淘汰；持久化要另行审阅迁移，且不得从 LEASE_BOUND 读回绑定 digest”。理由：读回库里的 digest，比对的就是表 owner 能改写的数据。
    - QO-3：扩到同一方法内的四处。
    - 测试：
      - C1 另断言两个构造器的默认值；K1 用默认 Options，并预置派发前、派发后两类到期租约；K2 加签发成功的对照；
      - Q1 改为 Theory 覆盖 a–e 五个变体，Q3 改为 Theory 覆盖四个字段；
      - 预期数量 unit 19、contract 16、contract-schema 4、postgresql18 45；
      - failing-before 每一行都须失败；反向验证与探针的构建关闭节点复用与共享编译（QD-1）。
    - 句子更正：ops README:3 与 tests/README:5、:7 按 START 给出的原文改；两份 CHANGELOG 按 START 给出的原文追加。
    - AGENTS.md:41 不改，推翻工单第 1.1 节第 13 条。理由：K 之后这句成为真话，而治理文本只许变短。QA 回执独立得出同一结论。
    - NuGet 包放在 environment 内，从 nuget.org 下载；Tests/ci 加 -B。validate_governance 等的 B1 基线在改动前跑，不用 git stash。
    - PG bootstrap 用 initdb 超级用户，按 Q4 先例（2026-09-27T01:40:43Z U3 Builder START），REPORT 中披露。
    - K1 提交之后，自测偏离就停下回报，不 amend。FREEZE.json 与 REPORT.md 放在单元根。
- 资源：本单元不用窗口；门禁位现在空闲，QA 的门禁在其 QA START 中安排。
- 自 Owner 最近一次输入（14:05:17.828Z）以来，总管已发 2 条跨会话消息（P18B 门禁放行、本 START）。

## 2026-09-28T15:24:01Z（总管 date -u）P08C Builder FREEZE 已到并核实；QA START 已发；门禁位分配给 P08C QA

- FREEZE 消息于 15:18:23.059Z 进入总管队列，来自 uds:/tmp/cc-socks/28957.sock（P08C Builder），from-mode 为 bypass。
- 总管核实（15:18Z–15:22Z，只读）：
  - FREEZE.json：2326 B，0644，mtime 15:18:12Z，sha256 3fcd2c50033e22814fbee3cf7e6df96c9ada4b5101a87d8268d18cdfe346a022。REPORT.md：13558 B，0644，mtime 15:17:47Z，sha256 9f2643c35bad939658c1c5f370369ffb0eeeaa992a85bff360d0fba007643d44。都与消息一致。
  - K1 = 28e512f0de7c092db283b73629473af7c4483926，tree 413bcf9d922274e089d345af75683603bcef28b4，唯一父 B1。
    - author 与 committer 都是 P08C Builder <p08c-builder@local.invalid>，提交时间 15:09:04Z；B1..K1 恰 1 个提交。
    - 分支 codex/p08c-591451e 与 HEAD 都指向 K1；porcelain 为空；objects/info 下没有 alternates；没有 linked worktree。
  - diff（工单第 3.2 节第 5 项的 argv 与环境）：41978 B，sha256 df9bc302e8b2cfc3424e548796228f5e6ed8d0b8d7160465f2bcaad15af61bb0，与 FREEZE 一致。
  - 改动 9 个路径，都在工单第 3.1 节写域内：根 CHANGELOG.md；CO 的 CHANGELOG.md、module.yaml、operations/README.md、tests/README.md、src csproj、PostgresCommandOrchestrator.cs、ExecutionAuthorizationContractTests.cs、PostgresCommandOrchestratorIntegrationTests.cs。
  - 与 B1 逐字节相同：CO 的 AGENTS.md、migrations 与 contracts 两棵树、required-security-tests.v1.json、test_command_receipt_schema.py、测试工程 csproj、三个 packages.lock.json。写域外（governance、Tools、Tests、.github、scripts、Docs、Dps.slnx 与其余五个相关模块）零改动。`git diff --check` rc 0。
  - module.yaml:3 与 src csproj:6 只改版本号 0.3.0→0.4.0。两份 CHANGELOG 没有删除行，新节的位置与 START 一致。
  - src 改动与 START 逐项一致：
    - 开关属性默认 true；专用异常继承 InvalidOperationException，消息含 kill_command_orchestrator；
    - 两个方法首句是 kill 检查，在 EnsureReady 之前；
    - 预约记录加 CommandSha256；比对单独成检查，放在 :639–646 之后，消息与 START 相同；
    - 四处 ThrowIfNull；QO-2 注释与 START 原文相同。
  - operations/README.md:3、tests/README.md:5 与 :7、两份 CHANGELOG 的新增文字，与 START 批准的原文逐字相同。
  - Builder 档位：自 14:00:45Z 至 15:18:33Z 共 240 条 assistant 记录，都是 claude-fable-5-1、medium/medium；permissionMode 为 bypassPermissions。15:18:33Z 起空闲。
  - 15:20:27Z：没有 postgres、dotnet、testhost、VBCSCompiler 或 MSBuild 进程；563xx 无监听；/private/tmp 下没有 .s.PGSQL.*。Builder 报告中遗留的 VBCSCompiler（pid 97808）已自行退出。
  - Builder 自报：四个套件 19、16、4、45；failing-before 9 行全部失败；反向验证 7 项全部按预期失败；Tests/ci Ran 243 OK；validate_governance 两边都是 67 条，集合相同。以上由 QA 独立复核。
- 总管裁定与记录：
  1. K1 用例的一处措辞差异。START 写 “Port.Calls 为 0”，而 K1 预置的派发后到期租约要经真实签发，端口已被调用 1 次。Builder 改为断言开关实例造成的端口调用增量为 0，并在 REPORT 第 2 节披露。总管接受，理由是这与 START 的本意一致；START 本身的措辞前后矛盾。交 QA 核对。
  2. 提交信息末尾有两行 trailer：Co-Authored-By（B1、F1 有先例），以及 Claude-Session 会话链接（CLI 会话所加）。后者不是 Owner 的姓名或邮箱，也不是凭据。Builder 请求裁定；总管裁定不重做提交，交 QA 记为观察。
  3. Builder REPORT 第 7 节写“单元外写入：无”，与实测不符：14:58:25Z 至 15:09:43Z 之间，/private/tmp 新增 14 个 32 位 hex 名的 socket，时段落在 Builder 运行 dotnet 期间，同期没有其他单元运行 dotnet。Builder 的 PREACCEPT 第 8 节曾预计会有。这是报告层漏报，交 QA 评估。
  4. 顺序：Builder 单元根下 environment 建立于 14:53:03Z，DSP_ZD 与 DSP_ZD/.git 建立于 14:53:05Z，都晚于 START 发出（14:52:26.793Z）。
- P08C QA START：
  - 15:23:41.679Z 发给 P08C QA（uds:/tmp/cc-socks/29173.sock）：msg_id 4c767357-684a-4ae1-9b37-af4a005f450a，正文 9445 B，sha256 1e21eff1449cbed30312ab0a838b999f5bb3f384df040fd02710653a29325f3f。它等于 scratchpad/start-p08c-qa.md（9446 B，sha256 c4aabfb01f29889c1b4b1c9c33895870c94b6b4916db2117eb2220d88e1834b1）去掉末尾换行后的内容。
  - 发送前 QA 自 14:45:37Z 起空闲，pid 29173 在运行，permissionMode 为 bypassPermissions。15:23:41.763Z 进入 QA 队列，permissionMode 为 bypassPermissions（15:23:41Z）。15:24:01Z 时 QA 还没有产生新的 assistant 记录，本轮档位留待下次核对。
  - 要点：
    - 冻结对象的哈希；
    - 允许只读 Builder START 原文（scratchpad/start-p08c-b.md，附哈希），作为文书逐字核对的依据；
    - 答复 QA 回执第 7 节第 1–14 项；
    - 独立核对上文第 1–4 项；
    - 读取范围：Builder environment 中只读 env.sh、run.sh、scratch/、logs/ 与 pg/log 中 0644 的四个文件，其余与 0600 文件一律不开；
    - PG 端口 56350–56359；门禁位分配给 P08C QA，两步放行，等候上限 60 分钟；
    - 网络按 QD-2 措辞；探针构建关闭节点复用与共享编译（QD-1）；Tests/ci 加 -B；不调用 /usr/bin/python3。
- 自 Owner 最近一次输入（14:05:17.828Z）以来，总管已发 3 条跨会话消息（P18B 门禁放行、P08C Builder START、P08C QA START）。

## 2026-09-28T15:35:50Z（总管 date -u）Owner 追加 QA 档位规则；P08C QA 档位核对

- Owner 输入（总管队列 15:30:44.881Z）：QA 也用 Fable 5.1、effort max；Fable 5.1 额度用完后切回 Opus 5.5，effort 同为 max。
- 总管的执行口径：
  - 之后新起的 QA：`--model claude-fable-5-1 --effort max`；额度用完改 `claude-opus-5-5`、`max`。Builder 规则不变：claude-fable-5-1 / medium，额度用完改 claude-opus-5-5 / medium。
  - 正在跑的 P08C QA 不换模型，仍按工单第 2 节与 START 为 claude-opus-5-5 / max。它 15:23:41Z 收到 START，持有门禁位。依据：
    - 12:05:14Z 条先例：模型规则只约束之后新起的 session；
    - 中途换模型，同一份 QA 证据会跨两个模型，也与 START 要求的档位自检矛盾。
  - Owner 要换就照办。
  - 下一个单元（M20-INT）的工单第 2 节按新规则写。
- P08C QA 档位核对（transcript 只读）：15:20:00Z 以来共 88 条 assistant 记录，都是 claude-opus-5-5、max/max；首条 15:24:30.108Z，末条 15:33:44.866Z。最近一条 user 记录（15:23:41Z）的 permissionMode 为 bypassPermissions。
- 自 Owner 最近一次输入（15:30:44.881Z）以来，总管已发 0 条跨会话消息。

## 2026-09-28T15:42:40Z（总管 date -u）两线合流摸底报告已到并抽查；K1 替换 B1 的预演

- 研究代理（general-purpose）14:56:13.999Z 派出，完成通知 15:40:14.541Z 送达。共 281 条 assistant 记录，都是 claude-opus-5-5 / max。
- 报告存为 coordination/research/M20-SCOPING-20260928.md（22701 B，sha256 f48f3d3cfb47449bfbc7ea3fe2d0a644c799ebfffb32f068a141034d5bfaa818），只作数据。正文是代理回复的原文（21164 B，sha256 前 16 位 67146c1ca63c613f），由脚本取出写入。
- 总管抽查（15:41:27Z，只读 git）：原文末节 8 处引用逐条执行，全部与原文一致；另核 TE:36–37 属实。
- K1 预演（同时段，只写 scratchpad 的 probe.git）：
  - merge-base 仍为 A。
  - tree 221bdf1c09a5a91020ad6e40c04b88e69bf48751，退出码 1。
  - 冲突仍只有 CHANGELOG.md 两处，技术书自动合并。
  - 与 B1 预演的 tree fa4e4011… 相比，差异正好是 K1 的 9 个路径。
- 代理的写入只有 scratchpad/m20-probe/probe.git，没有 alternates 文件；总管的 K1 预演后共 29 个文件。
- 代理自报的偏差（原文开头）：
  1. 多读了账本 2436–2450 行；
  2. find 没加 -prune，遍历过 units/p01/qa/environment/（0700）的目录项；
  3. 列过 u4-int-qa（0700）的目录名；
  4. 一次 zsh 替换错误，命令没有执行；
  5. git 加了 -c core.quotepath=false，只影响显示。
- 总管记为已披露偏差。第 2、3 条违反派单时“不进入 environment/”的字面要求，但只涉及目录项，没有打开文件，也没有输出内容，不影响报告结论。
- 代理的开单建议留待总管起草 WO-M20-INT 时逐条裁定，这里不作裁定：
  - 父序 F1 在前、K1 在后；
  - 是否允许 --write；
  - 门禁 --base；
  - 套件清单；
  - PG 配方；
  - 42501 例外。
- 下一步：P08C 收口后起草 WO-M20-INT。Builder 用 claude-fable-5-1 / medium，QA 用 claude-fable-5-1 / max。

## 2026-09-28T15:59:37Z（总管 date -u）Owner 同意关闭 P04F 两个 tab；已关闭，transcript 完好，可 --resume

- Owner 输入（总管队列 15:56:39.036Z，origin human）：同意关闭 P04F 的两个旧 tab，要求之后需要时能再 resume。
- 关闭前核对（15:57Z，只读）：
  - P04F Builder：tab c10，pid 36185，session 351dca17-ebfa-4fc5-a470-2fe9a06343d2。
    - 最后一条 assistant 记录：2026-09-27T10:08:12.088Z，stop_reason 为 end_turn。
    - transcript：3126131 B，sha256 56b32eb25938a9b9b46de6a690cdb8f5a3e3ad75232d993adf8b297c7d026a2f，966 行，全部可解析。
  - P04F QA：tab c11，pid 36398，session 74a68ee3-d084-4a43-b92e-b7d7005a5fe0。
    - 最后一条 assistant 记录：2026-09-27T11:23:57.348Z，stop_reason 为 end_turn。
    - transcript：10086879 B，sha256 4d692f169c3a3f9ef004f66fe23d9b86fc97f7fec28fc3f1008413686f05cd0b，3337 行，全部可解析。
  - 两个 session 都没有 subagents 目录。
- 经过：
  - 15:57:55.881Z、15:57:58.071Z、15:58:02.042Z 对 c10 发了三次 Ctrl-C，claude 没有退出。原因是两次调用的间隔超过了它“再按一次退出”的窗口。期间 transcript 没有变化。
  - 按 2026-09-27 关 c2、c7、c8、c9 的先例，改用 close: true：c10 于 15:58:32.619Z 关闭，c11 于 15:58:45.922Z 关闭。
- 关闭后核对（15:58:50Z）：
  - 两个进程都已退出；
  - 两份 transcript 的字节数与 sha256 都与关闭前相同，全部行可解析；
  - /tmp/cc-socks 下留下 36185.sock 与 36398.sock 两个死 socket，列入清理清单。
- 恢复命令（在 dps-air 目录，按原档位；尚未实测，依据是 CLI 帮助中的 `-r, --resume [value]` 与 transcript 完好）：
  - `cd /Users/younghu/Documents/ZennoDroid_DSP/dps-air && env -u CLAUDE_CODE_EFFORT_LEVEL claude --resume 351dca17-ebfa-4fc5-a470-2fe9a06343d2 --model claude-opus-5-5 --effort medium --permission-mode bypassPermissions -n "P04F Builder" --remote-control "P04F Builder"`
  - `cd /Users/younghu/Documents/ZennoDroid_DSP/dps-air && env -u CLAUDE_CODE_EFFORT_LEVEL claude --resume 74a68ee3-d084-4a43-b92e-b7d7005a5fe0 --model claude-opus-5-5 --effort max --permission-mode bypassPermissions -n "P04F QA" --remote-control "P04F QA"`
  - 恢复时是否改用 Fable 档位，届时按 Owner 规则决定。
- 现在开着的 tab 只有 c12（P08C Builder）与 c13（P08C QA），还剩 4 个名额。
- 自 Owner 最近一次输入（15:56:39.036Z）以来，总管已发 0 条跨会话消息。

## 2026-09-28T16:04:41Z（总管 date -u）P08C QA 门禁就绪已到并复核；门禁放行已发

- “P08C QA 门禁就绪”于 16:03:12.111Z 进入总管队列，来自 uds:/tmp/cc-socks/29173.sock（P08C QA），from-mode 为 bypass。
- QA 自报要点：
  - QA 的 PG 已停机，56350–56359 无监听，没有 DPS_* PG 变量；
  - 四个套件 19、16、Ran 4、45；探针在 K1 模式下 16/16，在 B1 模式下复现了 QO-1 与 QO-3 的缺陷；4 项变异对照；Builder 的 failing-before 在 B1 上 9/9 失败；
  - clone 的 HEAD 为 K1，porcelain 为空；ignored 项中有 3 个 .pyc，位于 Tests/ci 与 Tools/ci 的 __pycache__，15:31:55Z 由 Tests/ci 内部的子进程写出；
  - 自身偏差 2 项，写入 QA-REPORT。
  - 以上由 QA 在 FINAL 中给出证据，总管在收口时核对。
- 总管复核（16:03:34Z，只读）：
  - 没有门禁、postgres、pg_ctl、dotnet、testhost、VBCSCompiler 或 MSBuild 进程；
  - 563xx 无监听；
  - /private/tmp 下没有 .s.PGSQL.*，也没有 dps-phase0-executor-*；
  - QA 档位：15:49:00Z 以来共 150 条 assistant 记录，都是 claude-opus-5-5、max/max。最近一条带 permissionMode 的 user 记录（15:23:41Z）为 bypassPermissions。
- 门禁放行：
  - 16:04:14.399Z 发给 P08C QA（uds:/tmp/cc-socks/29173.sock）：msg_id 1d0895d9-e706-462e-b23d-db191127ea7f，正文 992 B，sha256 9475149f43a8f67753219a859260189dfdb7bbafe99d8f882e902a52734bcff5。它等于 scratchpad/release-p08c-qa-gate.md（993 B，sha256 1a23997e7ad54c234c22606ce684dcf05f2b6ecdbf79139014d7ccf252dd5e54）去掉末尾换行后的内容。16:04:14.419Z 进入 QA 队列。
  - 放行内容：
    1. 门禁位归 P08C QA，按 START 与工单第 3.4 节第 6 项的命令与 --base B1 只跑一次；
    2. 3 个 .pyc 保持原样，门禁前后快照中列出，报告中按单元内写入披露，并写明是哪个子进程没有带上 -B；
    3. 异常不重跑，停下回报；
    4. 跑完即释放门禁位，FINAL 按 START 交付。
- 自 Owner 最近一次输入（15:56:39.036Z）以来，总管已发 1 条跨会话消息（本放行）。

## 2026-09-28T16:34:08Z（总管 date -u）P08C QA FINAL 已核实；P08C 以 PARTIAL 收口

- FINAL 于 16:31:25.510Z 进入总管队列，来自 uds:/tmp/cc-socks/29173.sock（P08C QA），from-mode 为 bypass。
- 总管核实（16:31Z–16:33Z，只读）：
  - 两份交付文件都与消息一致：
    - QA-REPORT.md：34744 B，0644，381 行，sha256 647c9d416ac35049a8b32a051e49db9363e8b939ea10bcdd0d449d9197202a2e；
    - EVIDENCE_INDEX.json：29564 B，0644，sha256 98b064eb2849595e3c505166a0d46f00a0b12be1f15f80a6440e05157db9b3eb。
  - 索引列 145 个文件，字节数与权限全部相符。
    - 144 个的 sha256 复算一致，其中门禁写出的 3 个 0600 文件只算哈希。
    - QA 自己的 0600 文件 environment/pg/log/postmaster.log 没有打开。
    - 另有 2 个文件未入索引：索引本身，以及索引之后生成的 final-scan.txt。11 项按口令、缓存、源码树副本等理由排除。
  - 门禁 run c70fd85ceb127b0a4acdce6d05642352（从门禁写出的 0600 证据中只取派生值）：
    - WORKSPACE_DIAGNOSTIC_ONLY，overall FAIL，verification_level 为 null；head 为 K1，base 为 B1；workspace_clean 为 true；16:05:42Z–16:11:18Z。
    - 53 项 required 中 48 PASS、5 FAIL，其余为 0。与 run 4e900f24… 相比，53 个位置的 (index, id, status, exit) 完全相同。FAIL 为 #5、#10、#17、#35（exit 3）、#46。
    - module-governance 的 log_sha256 前 16 位两次都是 71956e4d16b02647；limitations 都是 4 条，内容相同。
    - #10 的日志哈希不同；QA 报告第 175 行称归一化后 11 条诊断相同。
  - QA 档位：16:03Z 以来共 225 条 assistant 记录，都是 claude-opus-5-5、max/max；permissionMode 为 bypassPermissions。
  - 16:33:18Z 的现场：
    - 没有 VBCSCompiler、门禁、postgres、dotnet、testhost 或 MSBuild 进程。QA 所记门禁遗留的 VBCSCompiler 48856 已退出。
    - 563xx 无监听；/private/tmp 下没有 .s.PGSQL.*，也没有 dps-phase0-executor-*。
- QA 结论要点：
  - 切片验收 (a)–(g) 在 K1 上逐项成立，每项都有具名用例与独立探针。
  - 4 个变异对照（M1–M4）都被探针与候选用例抓住；B1 模式复现了 QO-1、QO-3 的缺陷；failing-before 9/9 失败。
  - 套件与仓库检查：
    - 四个套件 19、16、Ran 4、45，全部通过；
    - Tests/ci Ran 243 OK；
    - generate_governance verified 3；validate_repo 316/96/247；
    - validate_governance 两边都是 67 条，集合相同。
  - 发现 1 条（QA 编号 F1），观察 7 条（O1–O7），QA 自身偏差 2 条（D1、D2）。停止事项：无。
- 总管裁定：
  1. 接受 PARTIAL。P08C（P08 切片 K）以 PARTIAL 收口。K1 unsigned，level=null，不构成 P08 的出口。
  2. QA 发现 F1：QA 请总管裁定口径，总管裁定不改判 NOT PASS。
     - 理由：工单第 3.4 节第 10 项的“存在缺陷”指候选本身。这条发现是交付文书对已授权的工具固有写入的误述，不改变任何验收结果。按先例在账本更正，K1 不改。
     - 更正：P08C Builder REPORT 第 7 节写“单元外写入：无”，不实。实际有三项，与总管 15:24:01Z 条第 3 项的实测一致：
       - /private/tmp 新增 14 个 32 位十六进制命名的 socket（14:58:25Z–15:09:43Z），与 14 次 dotnet test 对应；
       - ~/.dotnet/metadata 的 mtime 变为 15:10:10Z；
       - VBCSCompiler 管道（推断，已无法复核）。
  3. D1（调用一次 /usr/bin/python3 --version，输出丢弃）与 D2（两条只读 git 命令没有先设 QA 身份，.git 没有改动）：按已披露偏差接受，先例是 P18B QA 的 D-1。
  4. O6 采纳为今后工单的口径：
     - Tests/ci 与门禁内部用 `python -I` 起的子进程不带 -B，而 -I 会使 PYTHONDONTWRITEBYTECODE 失效，所以会在 clone 内写 .pyc；
     - 这属于单元内的工具固有写入，照实披露即可，不算偏差。
  5. O7：门禁遗留的 VBCSCompiler 会在空闲后自行退出。总管每次放行门禁位之前照旧核对。
  6. O1–O5 记为观察，不另处置。
- 残留与责任包按 QA 报告第 12 节：
  - 外审 NOT_RUN，责任方 Owner；
  - 门禁 5 项 FAIL 沿用既有归属；
  - 生产签名保持关闭，端到端证明属于切片 C；
  - QO-4、CompiledOperationV1 的来源与进程内保留表，见 K1 CHANGELOG；
  - 正式等级为 NONE。
- 清理清单追加（Owner 决定）：
  - P08C Builder 与 QA 的 environment（PG 已停机）、两个 clone 及构建产物；
  - /private/tmp 下两个单元留下的 hex32 socket（Builder 14 个、QA 34 个）；
  - 终端 tab c12、c13，即 P08C 的两个 session，关闭须 Owner 同意；
  - /tmp/cc-socks 下 P04F 的两个死 socket（15:59:37Z 条）。
- 下一步：按 14:45:14Z 条的决定开两线合流单元 M20-INT，把 F1（4c91fe27…）与 K1（28e512f0…）合流。工单定稿后用 CLI 起 Builder（claude-fable-5-1 / medium）与 QA（claude-fable-5-1 / max）。
- 自 Owner 最近一次输入（15:56:39.036Z）以来，总管已发 1 条跨会话消息。

## 2026-09-28T16:36:54Z（总管 date -u）M20-INT 开单：工单已签发，Builder 与 QA 已用终端 CLI 开出

- 工单 coordination/WO-M20-INT.md：23232 B，0644，sha256 09811683f26865a62e3a91fc94b6ec81ca878418df933f55633108eec6cedfd9。
  - 合流对象：F1 = 4c91fe275b1a4685cd15fa38453c5ca324a0022e 与 K1 = 28e512f0de7c092db283b73629473af7c4483926；merge-base A = afb4f9bd872a0348bac3f2064c6f8a9ce7cefe34。
  - 父序 F1 在前、K1 在后。只解 CHANGELOG.md 冲突，加集成说明，原文由 START 定案。
  - 不得运行 generate_governance --write，校验失败即停。
  - PG 两个实例，每个 PG 套件沿用所属父单元 QA 的配方：Factory 套件用源码构建的 18.4，源码包沿用 P18B 的读取例外；产品套件用 Postgres.app 18.4 的 initdb 超级用户。
  - 31 个套件加仓库检查；QA 以 --base A 跑一次门禁，逐位比对 F1 的 run 8ae2c4d3… 与 K1 的 run c70fd85c…。
  - 端口：Builder 56360–56369，QA 56370–56379。
  - 写进工单的新口径：16:34:08Z 条裁定 4（python -I 子进程写 .pyc）；module.yaml 中的 python3.12 须经 PATH 解析到 .venv。
- 起 session 前的核对（16:35:42Z）：
  - units/m20-int 不存在，两个预生成的 session id 都还没有 transcript；
  - codex/p18b-f799f0e 的 tip 仍为 F1，codex/p08c-591451e 的 tip 仍为 K1；
  - 563xx 无监听；本 session 自开的 tab 只有 c12、c13。
- Builder：
  - tab c14，pid 72242，16:35:47Z 启动，cwd 为 dps-air。
  - KERN_PROCARGS2 读出的 argv：--model claude-fable-5-1 --effort medium --permission-mode bypassPermissions --session-id 7e8e23d3-8306-4835-8f92-5e822e1d0ffa -n "M20-INT Builder"。初始环境中没有 CLAUDE_CODE_EFFORT_LEVEL。
  - 首条指令为 scratchpad/first-m20-b.md（6401 B，sha256 4571c7fe2e753a294c250d4107ba7a07733949af27f3af477005a119247ba12a）。
  - transcript 首条 assistant 记录在 16:35:52.589Z；至 16:36:31Z 共 13 条，都是 claude-fable-5-1、medium/medium；permissionMode 为 bypassPermissions。
- QA：
  - tab c15，pid 72304，16:35:50Z 启动，cwd 为 dps-air。
  - argv：--model claude-fable-5-1 --effort max --permission-mode bypassPermissions --session-id f8e0438d-230c-43d3-9c14-d449b01921ea -n "M20-INT QA"。初始环境中没有该变量。
  - 首条指令为 scratchpad/first-m20-qa.md（6994 B，sha256 0284127ab444e3f3a08b9c28ad0147b0aa40faf917d9a8f884a3d8fbbd7e8938）。
  - transcript 首条 assistant 记录在 16:35:59.503Z；至 16:36:25Z 共 8 条，都是 claude-fable-5-1、max/max；permissionMode 为 bypassPermissions。
- 两次调用都立即返回。现在开着的 tab 有 c12、c13（P08C）与 c14、c15（M20），还剩 2 个名额。
- 本轮只做档位自检与 PREACCEPT。允许的写入：各自的回执文件，以及单元 environment 下预演用的 probe.git。两份回执到齐后，总管才下达 Builder START。
- 自 Owner 最近一次输入（15:56:39.036Z）以来，总管已发 1 条跨会话消息；两条首条指令是启动参数，不经跨会话消息发出。

## 2026-09-28T16:52:02Z（总管 date -u）M20-INT Builder PREACCEPT 回执已到

- 16:50:44.141Z 进入总管队列，来自 uds:/tmp/cc-socks/72242.sock（M20-INT Builder），from-mode 为 bypass。
- 回执 units/m20-int/builder/PREACCEPT-BUILDER.md：36395 B，sha256 88313977855e1201346ad6e3e94c5acc77df8241748236e9132f0f588e3cf4f8，已核实。
- 内容待 QA 回执到齐后再入账。
- 自 Owner 最近一次输入（15:56:39.036Z）以来，总管已发 1 条跨会话消息。

## 2026-09-28T17:15:23Z（总管 date -u）M20-INT QA PREACCEPT 回执、两份回执内容、CHANGELOG 裁定与 Builder START

- QA 回执：17:08:44.600Z 进入总管队列，来自 uds:/tmp/cc-socks/72304.sock（M20-INT QA），from-mode 为 bypass。
  - units/m20-int/qa/PREACCEPT-QA.md：46881 B，199 行，sha256 fa4f2a12ef22869ecdbd65af3a1b3ef1ba457d3e17d6c707ab1f567d5f7d9ef0，已核实。
  - 至 17:09:02.708Z 共 16 条 assistant 记录，都是 claude-fable-5-1、max/max；permissionMode 为 bypassPermissions。
- 两份回执已到齐，补记内容。F1、K1、A 的 commit 与 tree、父序、提交身份、唯一冲突（CHANGELOG 两处），两份都与工单一致。
- Builder 回执（88313977…）要点：
  - CHANGELOG 用拼接式：A[1..4] + F1[5..40] + K1[5..133] + 说明 + A[5..46] + F1[83..113] + A[47..1393] + F1[1461..1475] + K1[1523..1528]，补回 git 折掉的两个空行。
  - 端口：factory 56360；product 56361，超级用户 dps_test_product_su，Database=postgres。
  - 第 8 节有 10 项待定。
- QA 回执（fa4f2a12…）要点：
  - 对工单的 4 条更正：
    1. 第 1.1 节第 5 条“差的 8 条都在 75 条一侧”，只有去掉日志首行的 "ERROR: " 前缀后才成立；按原始行集合是 9/1。归一化集合指纹：F1 9a2f5a30c58ed5da，K1 75645f520b821ec3。
    2. 第 1.1 节第 6 条 markdown 247 的依据不是 ls-tree（262）。差额是 core.quotepath 下被跳过的 15 个非 ASCII 文件名；预期值 247 成立。Builder 回执第 4 节第 6 条独立得出同一结论。
    3. 第 3.3 节“先从镜像 clone”：镜像不含 F1、K1、A 的对象，clone 得到的是空库，对象靠只读 fetch。
    4. 门禁用 --base A 时 #7 的 receipt scope 为 34（两父 run 分别是 1 和 4），是 base 不同造成的预期差异。
  - 另在套件表里补了 manifest 与 POL 的 floor。
  - CHANGELOG 参考解：合并 blob 去掉 6 行标记（1608 行、117888 B、sha256 1580813caa7581da48807c31eead08d6045e9bb44db7dfb04ed58567953f0d60），说明插在其第 167 行之后。
  - 门禁 53 项指纹预期 5f5232fd6e405c36；#10 归一化指纹 5eb04e6c258d21c5。
  - 第 7 节有 13 项待定，在 QA START 中定案。
- CHANGELOG 裁定：采用 Builder 的拼接式，补回两个空行。
  - 理由：工单要求逐字保留双方原文。两侧新增段各带自己的空行（顶部两段在末行，尾部两段在首行），git 把每处的两个空行合成一个公共行，所以去掉标记后，F1 与 K1 的顶部段之间、尾部段之间都少一个空行。M19 用三个 blob 拼出（本账本第 1671 行），这里沿用。
  - 总管独立拼接的结果：
    - 不含说明：1610 行、117890 B，sha256 394774b88256bfa4648c2c3e649543fa219c0fcb259ffe39cf5efa33d3499d7b。与去标记版逐行比对，只多两个空行，分别在去标记版第 39 行后和第 1603 行后。
    - 含说明：1616 行、118346 B，sha256 54e7e8b403bc1b43b7baebfdbe4df1d95fe29bcd425faa255bb824b154474682；说明从第 170 行开始。
  - 说明定稿：沿用 Builder 的提案，第二条去掉 "above"，因为尾部两侧的条目在说明下方。
  - QA 的参考解将在 QA START 中改为同一拼接式。
- Builder START：
  - 发送：总管侧 17:14:01.214Z 发出，发往 uds:/tmp/cc-socks/72242.sock。5950 B（不含结尾换行），sha256 4ce7011d99bc1fd1a7706919870495a45b78d2d53ab355c2016926eff846bf55，与定稿文件 scratchpad/start-m20-b.md 一致。Builder 侧 17:14:01.266Z enqueue，17:14:01.272Z dequeue。
  - 发前核对：Builder 最后一条 assistant 记录在 16:50:53.260Z，claude-fable-5-1、medium/medium；permissionMode 为 bypassPermissions。
  - 定案 10 项：
    - NuGet 缓存放在 $E/nuget。
    - 集成说明原文。
    - 补回两个空行，给出预期 sha256，不一致就停。
    - product 实例：dps_test_product_su，Database=postgres。
    - PATH 首位为 $C/.venv/bin。
    - F1、K1 上的进程内 validate_governance 用 git archive 导出后运行。
    - restore 用 locked-mode。
    - S25 的 Soul/Host 窗口附条件预分配给 Builder，只用于 control-plane-host.integration：自检两项后直接进入；写 READY-FOR-PG-WINDOW.md；进出前后各快照一次 /private/tmp/dps-*；每 3 分钟复查一次，60 分钟为上限。
    - 扫描口径。
    - 换模型由总管处理。
  - 其他裁定：
    - 临时文件从此只放 $E/scratch。
    - 允许只读三个父单元 QA 与 Builder 报告目录中 0644 的日志。
    - validate_repo 的 quotepath 口径记为观察。
    - 先提交 M20，再跑全部自测，不 amend。
  - 门禁位未分配，留给 QA。
- 偏差（都已披露，按已披露接受）：
  - Builder 回执开头称本轮只写了两处，不准确：它的会话 scratchpad（/private/tmp/claude-501/-Users-younghu-Documents-ZennoDroid-DSP-dps-air/7e8e23d3-8306-4835-8f92-5e822e1d0ffa/scratchpad）下有 44 个文件，第 7 节末以“若干脚本与 CHANGELOG 草稿”自报。这些文件列入清理清单。
  - Builder 读了 P18B QA 的 logs/f1-validate-repo.log（0644），首条指令没有列出，已披露。
  - QA 两次 zsh 解析错误（bad substitution、未做词分割），重跑后不影响结论。
  - QA 读了三次门禁 run 的 0600 证据，只输出派生值，属于 02:17:51Z 裁定的例外。
- 自 Owner 最近一次输入（15:56:39.036Z）以来，总管已发 2 条跨会话消息。

## 2026-09-28T17:38:04Z（总管 date -u）M20-INT Builder FREEZE 核验与 QA START

- FREEZE 消息：17:32:19.671Z 进入总管队列，来自 uds:/tmp/cc-socks/72242.sock（M20-INT Builder），from-mode 为 bypass。17:32:30.765Z 收到 Builder 的空闲通知。
- 冻结对象，总管只读复算，结果一致：
  - M20 3ae8006ba02502fbd95f6aa5a922efe42e630746，tree ec5f8d3feb12d38e40bcaf91dff26eca5531f2ae，父依次为 F1、K1；分支 codex/m20-int-afb4f9b 的 tip 等于 M20。
  - 提交身份 M20-INT Builder <m20-int-builder@local.invalid>，时间 17:15:20Z；提交信息没有 trailer。
  - F1、K1 之外只有 M20 一个新提交；reflog 两条（17:14:32Z checkout，17:15:20Z commit），没有 amend；没有 alternates，没有 linked worktree；porcelain 为空。
  - tree 与 T 221bdf1c… 只差 CHANGELOG.md。CHANGELOG 1616 行、118346 B，sha256 54e7e8b4…，等于总管预期。
  - FREEZE.json 14204 B，sha256 e0f009d8ed7054cfbdaf02ef6254bdaac205cb5822025112b69de430cc455e55。
  - REPORT.md 27370 B，sha256 9f859760662f473dadb93a94d23c27431d31b1e7bebd7ab4fbbefc6475204b43。
  - 两个父 clone 的分支 tip 未变。
- diff 的 argv 与报告不符：
  - Builder 报的值：F1..M20 2882137 B / 09464e63…，K1..M20 905593 B / 4bbd14c4…。这两个值是经 environment/scratch/env.sh:10 的包装、多带 -c core.quotepath=false 生成的；REPORT 第 1 节与 FREEZE.json 的 diff_argv 只写了工单的 argv。
  - 按工单原样的 argv 与环境复算：F1..M20 2882585 B，sha256 615ff08a9db01bd01a487d30c9aeb97b6c96487117a056340fe8385fd1035375；K1..M20 906047 B，sha256 93cfb3e171353a46d738b5ce421945b4e2835cd63976040ee4bf866c102e0e3f。
  - 差别只在非 ASCII 路径是否转成八进制（F1..M20 有 2 个这样的路径，K1..M20 有 3 个），内容相同。不要求 Builder 改文件，交 QA 记录并定性。
- 自测摘要（Builder 报，QA 将独立重跑）：
  - generate_governance verified 3；validate_repo 319/97/247；Tests/ci Ran 261 OK。
  - 进程内 validate_governance：M20 67 条，都在 K1 的集合内。
  - 31 个套件全部通过，数量与父实测相同；三项 NOT_RUN 按工单。
  - 两个 PG 实例的三项核对都通过，已停机。
  - 口令扫描与 Owner 身份扫描都是 0 命中。
- 已披露的偏差：
  - 调用错误一：第一轮 17 个 dotnet 套件 rc 127，原因是隔离 HOME 下找不到 SDK。加 DPS_DOTNET 后重跑，写入 *.run2.log，原始日志保留。
  - 调用错误二：R4 在 zsh 下没有分词，三次尝试写同一个文件名，前两次的原始日志被覆盖。
- 总管实测（17:34Z）：
  - 档位：Builder 自 16:35:52.589Z 至 17:32:29.877Z 共 157 条 assistant 记录，都是 claude-fable-5-1、medium/medium；permissionMode 为 bypassPermissions。
  - 时间：DSP_ZD 与 DSP_ZD/.git 建立于 17:14:32Z，晚于 START 送达（17:14:01Z）。
  - /private/tmp：17:23:11Z–17:25:22Z 新增 18 个 hex32 socket，总数 1324。~/.dotnet/metadata 的 mtime 为 17:25:20Z。仍有 1 个 VBCSCompiler（pid 98026）。563xx 无监听，没有 .s.PGSQL.*，也没有 dps-phase0-executor-*。
  - Builder 会话 scratchpad 仍是 44 个文件，没有晚于 17:14:01Z 的。
  - 窗口 17:25:19Z–17:25:49Z，自检两项都为 0；/private/tmp/dps-* 前后各 59 条，无差异。
- QA START：
  - 发送：总管侧 17:37:11.042Z 发出，发往 uds:/tmp/cc-socks/72304.sock。12569 B（不含结尾换行），sha256 331530604e423a7c2a07809e4c7f0d7b8698855e44ec6214426b10ea155753fe，与定稿文件 scratchpad/start-m20-qa.md（12570 B，sha256 62c06165b559978e3bb7a89482797de9d0604770ec8794e529cbaf9f5f5073a3）一致。QA 侧 17:37:11.118Z enqueue，17:37:11.126Z dequeue。
  - 发前核对：QA 最后一条 assistant 记录在 17:09:02.708Z，claude-fable-5-1、max/max；permissionMode 为 bypassPermissions。
  - 内容：
    - CHANGELOG 新参考解：在去标记版上插入两个空行与集成说明，并给出预期值。
    - 答复 QA 回执第 7 节的 13 项。
    - 另外 6 项独立核对：diff argv、档位、时间顺序、两处调用错误、单元根外写入、QA 的提交身份。
    - 读取范围：Builder environment 中 secrets/、pg/、3 个 0600 needle 文件、home/、nuget/、tmp/、s/ 一律不开。
    - Soul/Host 窗口附条件分配给 QA。
    - 门禁位分配给 QA，分两步放行。
- 清理清单新增：Builder environment 中保留的 PG 数据目录、0600 secrets 与 3 个 0600 needle 文件。
- 自 Owner 最近一次输入（15:56:39.036Z）以来，总管已发 3 条跨会话消息。

## 2026-09-28T18:02:38Z（总管 date -u）M20-INT QA 门禁就绪与放行

- “M20-INT QA 门禁就绪”：18:00:55.961Z 进入总管队列，来自 uds:/tmp/cc-socks/72304.sock（M20-INT QA），from-mode 为 bypass。QA 报告的内容如下。
  - 套件与 PG：17:51:00Z–17:57:39Z 做完。31/31 套件 rc 0，没有 FAIL、SKIP 或 NU1xxx，数量与父实测逐项相同。窗口 17:57:09Z–17:57:39Z，自检两项为 0。
  - 仓库检查：R1 verified 3，R2 319/97/247，R3 Ran 261 OK，diff --check 两次 rc 0。
  - 合并核对：M20 的 tree 除 CHANGELOG.md 外等于 merge-tree 221bdf1c…；CHANGELOG 逐字节等于新参考解。两份 diff 两种口径都与总管、Builder 的值一致，Builder 的 patch 文件逐字节相同。
  - 门禁前置：两个 PG 实例 17:58:39Z 已停（factory 56370 为源码 18.4，product 56371 为 Postgres.app）；563xx 无监听；只剩 1 个 VBCSCompiler（pid 24826，17:55:22Z 由其套件启动）。
- 总管 18:01:24Z 复核：
  - 没有门禁、postgres、testhost 或 MSBuild 进程，只有 VBCSCompiler pid 24826；
  - 563xx 无监听；/private/tmp 没有 .s.PGSQL.*，也没有 dps-phase0-executor-*；
  - Builder 的 VBCSCompiler pid 98026 已自行退出。
- 放行：
  - 发送：总管侧 18:02:09.377Z 发出，1398 B（不含结尾换行），sha256 ad8c07c1639b1114959539d959c95947554de99b30006db1b9095935837d919d，与 scratchpad/release-m20-qa-gate.md 一致。QA 侧 18:02:09.393Z enqueue。
  - 前提：先等 pid 24826 空闲后自行退出，不结束它，使开跑时的进程状态与 K1 的 run 相同。它退出后复核条件再跑，只跑一次；到 18:30Z 仍未退出就停下回报。
  - 门禁固有写入按 P10 R1 (b) 先例处理；.pyc 按 16:34:08Z 裁定 4 披露；异常不重跑；跑完即释放门禁位。
- 自 Owner 最近一次输入（15:56:39.036Z）以来，总管已发 4 条跨会话消息。

## 2026-09-28T18:26:43Z（总管 date -u）M20-INT QA FINAL 已核实；M20-INT 以 PARTIAL 收口

- QA FINAL：18:23:49.487Z 进入总管队列，来自 uds:/tmp/cc-socks/72304.sock（M20-INT QA），from-mode 为 bypass。
- 交付物，总管复算一致：
  - QA-REPORT.md：51246 B，sha256 8781416cf613ee67bad18310b8b6b9d3cfe89c6fe58589ad555ba9de27c95422。
  - EVIDENCE_INDEX.json：16192 B，sha256 4c2946cc5547965ad74c1d916ed366de951f0fd7f1585ce60f3a5aa098cbd20f。其中列了 72 个文件，总管逐个重算字节数与 sha256，全部一致；门禁写出的 3 个 0600 文件只算了哈希，没有打开。
- 门禁 run 156d74d5cafc4269eaa2563b9a388307，总管只取派生值复核：
  - gate 为 WORKSPACE_DIAGNOSTIC_ONLY，overall FAIL，verification_level null；head 为 M20，base 为 A，workspace_clean 为 true；started 18:07:55Z，finished 18:14:02Z。
  - summary：53 项，48 PASS，5 FAIL，其余 0。FAIL 为 #5 module-governance(1)、#10 solution-locked-restore-build(1)、#17 evidence-service.unit(1)、#35 legacy-runtime-adapter.static(3)、#46 soul-memory-adapter.unit(1)。
  - 53 个 (index, id, status, exit) 与 F1 run 8ae2c4d3…、K1 run c70fd85c… 逐项相同，指纹 5f5232fd6e405c36。limitations 4 条，三次相同。
  - #7 与 #52 PASS。#7 的 receipt scope 为 34（F1 为 1，K1 为 4），是 base 不同造成的预期差异。
  - #5 日志的 log_sha256 前 16 位为 71956e4d16b02647，与 K1 相同。
- QA 结论 PARTIAL，按知情接受口径，与 F1、K1、M19 相同。要点：
  - 合并：M20 除 CHANGELOG 外逐路径等于 merge-tree 221bdf1c…；CHANGELOG 逐字节等于新参考解。
  - 仓库检查：verified 3；319/97/247；Ran 261 OK；diff --check 两次为 0。
  - 套件：31/31 rc 0，数量逐项等于父实测；窗口 17:57:09Z–17:57:39Z。
  - 门禁：18:07:52Z–18:14:02Z，只跑一次。放行前提已满足：pid 24826 于 18:06:12Z 自行退出。
  - 泄漏扫描与 Owner 身份扫描都是 0 命中。
  - 发现 2 条（F-1、F-2），观察 11 条（O-1 至 O-11），责任包 4 项，见报告第 12 节。
- 总管实测（18:25Z）：
  - 档位：QA 自 16:35:59.503Z 至 18:24:07.490Z 共 182 条 assistant 记录，都是 claude-fable-5-1、max/max；permissionMode 为 bypassPermissions。
  - /private/tmp 下 hex32 socket 总数为 1361，与报告的 1324 → 1361 一致。~/.dotnet/metadata 的 mtime 为 18:13:56Z。没有 VBCSCompiler、postgres 或 testhost 进程；563xx 无监听。
  - QA 会话目录：scratchpad 为空。tasks/ 下有 1 个 harness 写的后台任务输出（bp7dr4eu4.output，83 B，18:05:22Z 建，18:06:12Z 改，未打开），报告第 11 节没有列出。记为观察，不影响结论。
- 裁定：
  1. 接受 PARTIAL，M20-INT 以 PARTIAL 收口。M20 = 3ae8006ba02502fbd95f6aa5a922efe42e630746（tree ec5f8d3f…），unsigned、level=null、non-issuer。按工单第 4 节，两条线的新单元都以 M20 为基线。
  2. F-1：不要求 Builder 补正 FREEZE.json 与 REPORT.md，冻结后不再写。以本账本为准：
     - 按工单原样的 argv 与环境，F1..M20 为 2882585 B / 615ff08a…，K1..M20 为 906047 B / 93cfb3e1…；
     - Builder 所记的 2882137 B / 09464e63… 与 905593 B / 4bbd14c4…，对应多带 -c core.quotepath=false 的调用；
     - 两种口径内容相同。
  3. F-2：作为已披露的轻微偏离接受，不影响候选。
  4. 更正本账本 17:15:23Z 条：该条记的“归一化集合指纹 F1 9a2f5a30c58ed5da、K1 75645f520b821ec3”来自 QA 回执当时脚本的序列化，QA 报告没能复现这一口径，改用新口径：F1 75 条为 007172bd4e79e457，K1 与 M20 的 67 条为 5a5735e1b88d4fb6。两次算出的集合大小与包含关系相同。
  5. 观察 O-1 至 O-11 与责任包按报告第 12 节照录，归属见报告，本单元不另开单。
- 清理清单新增（由 Owner 决定）：
  - M20-INT Builder 与 QA 的 environment（含 PG 数据目录、0600 secrets 与 needle 文件）和两个 clone；
  - QA clone 内门禁写出的 candidate-runs、109 个被忽略目录与 46 个 .pyc；
  - /private/tmp 新增的 hex32 socket（Builder 18 个，QA 37 个）与 1 个 VBCSCompiler 管道 socket；
  - QA 会话 tasks/ 下的 1 个文件；
  - tab c14、c15。
- 现在开着的 tab：c12、c13（P08C）和 c14、c15（M20），都已空闲。关闭须经 Owner 同意，关后可 resume。
- 下一步（总管决定）：先对两条线在 M20 上的下一个切片做只读摸底，即 Factory 线的 P18 S2 与产品线切片计划中的下一个，再定顺序与工单。门禁位与 Soul/Host 窗口各只有一个，两个单元不同时跑门禁。
- 自 Owner 最近一次输入（15:56:39.036Z）以来，总管已发 4 条跨会话消息。

## 2026-09-28T19:07:23Z（总管 date -u）P18 S2 摸底留档；AGENTS.md 先例裁定；WO-P18C 签发；P18C Builder 与 QA 已开出

- 两条摸底：总管派出的只读子代理（general-purpose），不写文件，报告只作数据。
  - P18 S2（task a86e41f6353f44500）：18:28:34.744Z 派发，18:51:23.011Z 完成。
  - P08 切片 C（task a2a87be41de8dab03）：18:28:56.565Z 派发，仍在运行。
- P18 S2 摸底已留档：coordination/research/P18C-SCOPING-20260928.md，26073 B，sha256 7063f786b973d0118f290f2472a5eff21cd45eb1bb1522ed1ffa0e2d3a12622a，18:53:33Z 写入。
  - 结构：总管写的抬头、分隔线、子代理原文。原文 24972 B，sha256 前 16 位 36e1cc5508d287f2。
  - 总管抽查：18:44Z–19:06Z 在 M20 对象上只读核对，逐条写进 WO-P18C 第 1.1 节第 1–13 条，与摸底一致，未见不符。
- AGENTS.md 先例裁定：
  - RN 的 AGENTS.md:35 与 contracts/consumed/README.md:3 在 S2 之后会变成假话。
  - 沿用本账本第 2037 行（P08A）与第 2573 行（P08B）的口径：只把记号 `instruction.receipt/v1` 换成 `instruction.receipt/v2`，原文由 START 逐字定案，其余字句不增不删；需要新增句子就停下，报 Owner。
  - 第 207 行与第 500 行那一次改的是 Runner 的回退规则，由 Owner 裁定，属于实质规则的改动，不作为本次的先例。
- 顺序：P18C 先开。
  - P18C 不用 PG，也不用 Soul/Host 窗口，门禁位只用一次，现有 2 个空 tab 名额就够。
  - P08 切片 C 的摸底还没回来。若它与 P18C 并行，它的工单须禁止写任何 module.yaml 与 governance/**（WO-P18C 第 4 节）。
- WO-P18C 已签发：coordination/WO-P18C.md，22300 B，218 行，sha256 8678aa91bf17a07602dc512a9df74a95db803d7fff4fca668bff1b370c260beb，19:04:02Z 写入。要点：
  - 基线 M20 = 3ae8006ba02502fbd95f6aa5a922efe42e630746；分支 codex/p18c-3ae8006；只有一个提交 F2，身份为 `P18C Builder <p18c-builder@local.invalid>`。
  - 切片为 PDD 第 818 行中属于 Runner 的部分。先迁 Runner、后迁 Worktree 与 Host，是对第 818 行顺序的偏离，理由写在工单第 0 节。
  - 已知限制：混合 major 下真实链条跑不通，证据只到单元层与合同层。
  - 须在 PREACCEPT 中提出、由 START 定案的 8 项：v1 的处置、入边、校验深度、测试、版本、文书原文、COMPAT 的预期变化、自测清单。
  - QA 跑一次 `--level integration --base 3ae8006ba02502fbd95f6aa5a922efe42e630746 --diagnostic-workspace` 门禁，与 run 156d74d5cafc4269eaa2563b9a388307 逐项比对。module-governance 须是 M20 那 67 条的子集，摸底推断为 63 条。
  - 不用 PG，不占 563xx，不用 Soul/Host 窗口。单元开着期间，其他单元不得写任何 module.yaml、governance/** 或 Modules/factory-trusted-runner/**。
- 开出（终端 CLI，按 Owner 12:02:03Z 指令）：
  - Builder：tab c16，pid 60136，session ff7b0c84-0c24-4126-8b47-58542a7a69ea，名称“P18C Builder”。启动参数为 claude-fable-5-1 / medium / bypassPermissions，CLAUDE_CODE_EFFORT_LEVEL 未设。首条消息是总管 scratchpad 下的 first-p18c-b.md（6554 B，sha256 57bf1a80191716142e68f61f6e1bafd46ab81329190e1c74180381ae8578e87c）。
  - QA：tab c17，pid 60161，session a3e1c227-ba0a-4f0b-881c-a55f262e1e31，名称“P18C QA”。启动参数为 claude-fable-5-1 / max / bypassPermissions，CLAUDE_CODE_EFFORT_LEVEL 未设。首条消息是总管 scratchpad 下的 first-p18c-qa.md（7452 B，sha256 8f7858a59ed4d77a564e2511367d207a0aee4272f6ea913426b473bfc534a8ce）。
  - 档位实测（读各自 transcript）：
    - Builder：首条 assistant 19:06:06.850Z，claude-fable-5-1，perTurnEffort medium；permissionMode bypassPermissions（19:06:03.689Z）。符合。
    - QA：首条 assistant 19:06:11.818Z，claude-fable-5-1，perTurnEffort max；permissionMode bypassPermissions（19:06:04.451Z）。符合。
  - 两者本轮只做档位自检与 PREACCEPT。总管已订阅两者的空闲通知。
- tab：c12–c17 共 6 个，已到上限。c12–c15 已空闲，关闭仍待 Owner 同意。
- 自 Owner 最近一次输入（15:56:39.036Z）以来，总管发出的跨会话消息仍为 4 条（空闲订阅不计）。

## 2026-09-28T19:19:55Z（总管 date -u）P08 切片 C 摸底留档；与 P18C 并行；WO-P08D 签发，开出要等 tab 名额

- 摸底留档：
  - P08 切片 C 的只读子代理（task a2a87be41de8dab03）最后一条带文字的记录在 19:09:50.539Z，完成通知 19:09:50.602Z 送达。
  - 报告存为 coordination/research/P08-SLICE-C-SCOPING-20260928.md，27892 B，sha256 6a3cb6f2c679de8f27bc1d1516e8f717c6610d287ce2d4ab518496adea4d84ba，19:12:26Z 写入。
  - 结构同 P18C 摸底：抬头、分隔线、子代理原文。原文由脚本取出，26225 B，sha256 前 16 位 a2fb286d341854fa。
- 总管抽查（19:10Z–19:18Z，只读）：
  - 原文第 9 节的 8 条命令逐条执行，结果全部与原文预期一致；
  - 另核的各项逐条写进 WO-P08D 第 1.1 节第 1–12 条，与摸底一致，未见不符；
  - 19:12:00Z：~/Library/Caches/com.apple.python 近 50 分钟没有新文件，563xx 没有监听。
- 摸底要点：
  - 切片 C 只改 CO、PA 的测试目录与两个模块的 CHANGELOG；
  - 需要一个 PG 18.4 实例，不用 Soul/Host 窗口；
  - 门禁 53 元组预期不变；
  - 切片 C 之后 P08 仍只能 PARTIAL；
  - 与 P18C 的写域只在根 CHANGELOG.md 重叠。
- 决定（总管）：
  - 单元名 P08D（P08C 已用于切片 K），基线 M20。端口 Builder 56380–56389，QA 56390–56399。
  - 与 P18C 并行（PDD 第 103 行）。为使路径零重叠，P08D 不写根 CHANGELOG.md：它只改测试，不改行为与架构，改动记在 CO、PA 各自的 CHANGELOG。P18C 不写 CO 与 PA；P08D 不写任何 module.yaml、governance/** 与 factory-trusted-runner。门禁位串行，先后在各自的 QA START 中安排。
  - 口径裁定，写进 WO-P08D 第 3.1 节，不改规则文本：
    - RULINGS-3 第 24 行只适用于 P06 S2 的 Host 工程，不约束 CO 的测试工程；
    - CO AGENTS.md:21 读作：CO 测试只经 PA 自己的 public test-only 夹具调用 PA，只取合同类型与基元；不 import PA 的 internal 类型，不对 PA 的表发 SQL。做不到就停下回报。
- WO-P08D 已签发：coordination/WO-P08D.md，30039 B，304 行，sha256 9e651e76142461a9717d9cca00a71a01a31b7ceab0cf57a4841812eb6642055d，19:18:21Z 写入。
- 开出准备：
  - 首条消息在总管 scratchpad 下：first-p08d-b.md（7040 B，sha256 fd63faeb13d7688f8e517dd8132bcd15859c35f5c8e7d5d28148dfc4f0a476a2），first-p08d-qa.md（7626 B，sha256 176bd647294b5faa3864116302f25c66771a4657448389be1bf0c06a61447a6f）。
  - 预留 session id：Builder 518af589-5eac-408b-a6f0-cb3c034c9da2，QA 104c34c5-ce06-4d52-9c62-2868ec181e3c。
- 开出待定：tab 已满 6 个（c12–c17）。c12–c15 已空闲，关闭须经 Owner 同意；同意之前 P08D 不开出。
- 自 Owner 最近一次输入（15:56:39.036Z）以来，总管发出的跨会话消息仍为 4 条。

## 2026-09-28T19:22:26Z（总管 date -u）P18C Builder PREACCEPT 回执已核实

- 19:20:25.705Z 收到消息，第一行为“P18C Builder PREACCEPT 回执”。
- 回执文件 units/p18c/builder/PREACCEPT-BUILDER.md：0644，44622 B，sha256 7f1078a20a47e635c424c70b306d6019d43f71994fdd907e16a8772a380ce5d6。已核实。
- 档位：transcript 中 19:06:06.850Z–19:20:20.253Z 的 84 条 assistant 记录全部为 claude-fable-5-1、perTurnEffort medium；permissionMode bypassPermissions。已核实。
- 单元目录 units/p18c/ 下只有 builder/PREACCEPT-BUILDER.md 一个文件。
- 回执自报的单元根外写入：往本 session 的 harness 会话 scratchpad 导出了 21 个 M20 tracked 文件，共 832 KB，超出首条消息允许的写入范围。总管只看元数据核实：21 个文件、832 KB，属实。处置在 Builder START 中给出；文件留待 Owner 决定清理。
- 回执内容在 QA 的独立回执落地之前不入账。

## 2026-09-28T23:21:00Z（总管 date -u）桌面 app 重启使 P18C 两个会话退出；总管空等约 3 小时 43 分钟；P18C QA 已恢复

- 事实：
  - Claude 桌面 app（pid 68702）于 19:35:09Z 启动。本机 boot 时间为 2026-09-19，app bundle 最后修改于 2026-09-27T10:36Z，DiagnosticReports 中没有 Claude 的崩溃报告。重启原因未查明。
  - 终端面板中原有 6 个 tab（c12–c17）的 CLI 会话进程都已不在，包括 pid 60136（P18C Builder）与 60161（P18C QA）。这 6 个 tab 还在列表里，但都没有在运行命令，opened_by 变为 user。
  - 总管自身的 ListAgents ref 由 37f4de 变为 1d7d84；各 Claude Desktop 任务卡会话不再出现在列表中。
- 影响：
  - P18C QA 的最后一条记录在 19:33:04.501Z，当时正在做 PREACCEPT 中的档位复核。PREACCEPT-QA.md 没有写出，units/p18c/qa/ 是空目录。它的 harness 会话 scratchpad 有 5 个文件、272 KB（总管只看元数据）。
  - P18C Builder 自 19:20:20.253Z 起空闲等 START，进程已不在。候选尚未开工，没有任何单元写入受影响。
- 总管的编排缺口：
  - 总管 19:24:55Z 之后没有任何动作，直到 Owner 于 23:17:46.812Z 输入“怎么回事？停止了？”。
  - 原因：总管只靠对方的消息与空闲通知推进；app 重启后订阅失效，又没有兜底检查。空等约 3 小时 43 分钟（19:35Z–23:18Z）。
  - 改正：此后每个在途会话都另设后台检查，进程一退出就通知总管。
- 处置：
  - 23:19:55Z 用终端 CLI `--resume` 恢复 P18C QA：tab c1，pid 82917，参数 claude-fable-5-1 / max / bypassPermissions，CLAUDE_CODE_EFFORT_LEVEL 未设。
    - 恢复消息为总管 scratchpad 下的 resume-p18c-qa.md（1578 B，sha256 d9f54935ef929f2fae2eca08fe2304b3df53d953d5c6a08d4418b99189350878）。它要求 QA 重做档位自检，照原范围完成 PREACCEPT，不再写会话 scratchpad，并在回执中写明中断经过与已有的 5 个文件。
    - 23:19:55.871Z 的 `<synthetic>` 记录（“No response requested.”）是 harness 为收尾被中断的轮次写的占位。23:20:26.732Z 起的 assistant 记录都是 claude-fable-5-1、perTurnEffort max，permissionMode 为 bypassPermissions。符合。
    - 已重新订阅它的空闲通知。另设后台检查（task btw96jpfl）：pid 82917 退出就立即通知，最长 100 分钟。
  - P18C Builder 暂不恢复。它在等 START，START 要等 QA 回执到齐后签发，届时用 `--resume` 把 START 作为恢复消息送达。
- 自 Owner 最近一次输入（23:17:46.812Z）以来，总管发出 1 条跨会话消息（QA 的恢复消息）；空闲订阅不计。

## 2026-09-28T23:23:00Z（总管 date -u）P08D Builder 与 QA 已开出

- 开出条件：app 重启后，原来 c12–c17 六个 tab 里的会话都已退出，tab 成了空 shell，也不再占总管自开 tab 的名额（本 session 新开的 tab 从 c1 起编号）。因此不用关 tab，P08D 可以开出，19:19:55Z 条的“开出待定”到此解除。
- 首条消息：19:19:55Z 条记录之后，两份各补了一条“harness 给本 session 的会话 scratchpad 也不写；需要逐行阅读 git 对象时用 git show 直接输出”（起因见 19:22:26Z 条）。现值：
  - first-p08d-b.md：7237 B，sha256 5446f1c145699609448d31ac048b7f814ad44a92a6a51bb994e1badabb7306a5；
  - first-p08d-qa.md：7823 B，sha256 4174c21bd14f7634522ec34969f99fd210302bdd50084264b657446d1ae4bdf5。
  - WO-P08D.md 未变，仍为 sha256 9e651e76142461a9717d9cca00a71a01a31b7ceab0cf57a4841812eb6642055d。
- 开出（终端 CLI）：
  - Builder：tab c2，pid 85330，session 518af589-5eac-408b-a6f0-cb3c034c9da2，名称“P08D Builder”，claude-fable-5-1 / medium / bypassPermissions，CLAUDE_CODE_EFFORT_LEVEL 未设。
  - QA：tab c3，pid 85444，session 104c34c5-ce06-4d52-9c62-2868ec181e3c，名称“P08D QA”，claude-fable-5-1 / max / bypassPermissions，CLAUDE_CODE_EFFORT_LEVEL 未设。
  - 档位实测：
    - Builder 首条 assistant 23:22:30.008Z，claude-fable-5-1、perTurnEffort medium；permissionMode bypassPermissions（23:22:25.061Z）。符合。
    - QA 首条 assistant 23:22:35.661Z，claude-fable-5-1、perTurnEffort max；permissionMode bypassPermissions（23:22:28.039Z）。符合。
  - 前提：23:22:16Z 时 units/p08d 不存在，563xx 无监听。
- 兜底：已订阅两者的空闲通知；另设后台检查（task bvfx0th57），两个 pid 中任一退出就立即通知，最长 100 分钟。
- 在途：P18C QA（恢复后在写 PREACCEPT）；P18C Builder（离线，等 START）；P08D Builder 与 QA（PREACCEPT）。门禁位空闲，Soul/Host 窗口不用。
- 自 Owner 最近一次输入（23:17:46.812Z）以来，总管发出的跨会话消息为 3 条：QA 的恢复消息与 P08D 的两条首条消息，从严计入。

## 2026-09-28T23:34:22Z（总管 date -u）P18C QA PREACCEPT 回执已核实；两份回执内容入账；Builder START 已签发并经 resume 送达

- QA 回执：
  - 23:31:00.112Z 收到消息，第一行为“P18C QA PREACCEPT 回执”。
  - 文件 units/p18c/qa/PREACCEPT-QA.md：0644，64348 B，429 行，sha256 61753ef98329e314ba36e4b83e84d2a28a131ee8896e8faba5a529c68063ebe9。已核实。
  - 档位：恢复后 23:20:26.732Z–23:31:12.481Z 的 19 条 assistant 记录全部为 claude-fable-5-1、perTurnEffort max；permissionMode bypassPermissions。已核实。
  - 自报的单元根外写入：中断前往 harness 会话 scratchpad 写了 5 个文件，共 272 KB，都是 M20 tracked 文件的逐字节副本。总管只看元数据核实，文件名与大小一致。按已披露偏差记账，保留，列入清理清单。
- 两份回执都已到齐，内容入账：
  - Builder（7f1078a2…）：
    - v1 的处置、入边、版本与文书同意默认；
    - 校验按 IM 先例，只改 `_validate_receipt`；
    - 新增 U1–U11 与 C1–C2，RN unit 44→55、contract 6→8；
    - COMPAT 的 majorDeclarations 186→187，进程内 validate_governance 67→63；
    - 请 START 定案两处：时间自洽检查，以及 jsonschema 在顶层 import。
  - QA（61753ef9…）：
    - 第 1.1 节 13 条逐条独立核对一致；8 项默认全部接受，附验收标准；
    - IR→RN v2 的 communicationPairSha256 预期为 f75bcfc0ecc3f157c52167611f8e63d14057641b0ac910e30d6704e8ecc95a3b，算法用 M20 上 IR→IM 的现值校验过；
    - COMPAT 预期 6073→6087 行，numstat 29/15；
    - #5 预期 63 条，集合指纹 0dfe0b14480b7b9a；
    - 未能复现 M20 QA 报告的 53 元组指纹，改用自己的口径，比对以逐元组明文为准。
- 分歧与裁定（写进 Builder START）：
  1. corpus 的 11 条 invalid：
     - QA 默认 8 条由 RN 拒，另 3 条（occurred-not-resolved、expiry-equality、float-major）记为端口责任。
     - 裁定采纳 Builder 的做法：RN 另加浮点检查与时间自洽检查，11 条全部由 RN 失败关闭，与工单第 3.4 节第 4 项 (b) 一致。
     - 依据：corpus 把这 3 条标为 owner 的 strict-parse 拒绝；时间关系出自 IR `operations/README.md:11`；拒绝浮点的先例是 IM `src/impact_analyzer.py:181–182、:201`。
     - 判据逐字写进 START：
       - 任意层级出现 float 即拒；
       - v2 schema 的 8 个时间戳字段凡非空的都能解析；
       - occurred_at、resolved_at、source_intake_verified_at 三者相等；
       - 四个到期字段凡非空的，都严格晚于 resolved_at。
     - 总管在 M20 的 corpus 上只读核对：valid 的 2 条都通过；两条判据恰好拦下这 3 条。
     - 因此 RN unit 预期 55，contract 预期 8。QA START 要把 QA 的验收口径改为 11 条都由 RN 拒绝。
  2. 入边：双方都同意只改 major。
  3. RN 的 tests/README.md 不改：只是不完整，没有变成假话。dependencies 中 IR 的 versionRange 不动，QA 记为观察。
  4. 不新增任何文件（QA 第 7 节第 8 项）。
  5. 失败在前分甲、乙两类写清，每条新检查是否真的被测到，以反向验证为准（QA 第 4 节第 3 项）。
  6. 顺序偏离：QA 接受，但给了不同的理由：phase0 的诊断按边逐条生成，RN 的 4 条与 WT、HO 互不依赖。作为 QA 的补充意见入账，工单不改。
- Builder START：
  - 23:33:04Z 签发，晚于两份 PREACCEPT（19:20:25Z、23:31:00Z）。
  - Builder 的进程已在 app 重启时退出，START 经 CLI `--resume` 送达：tab c4，pid 2996，claude-fable-5-1 / medium / bypassPermissions，CLAUDE_CODE_EFFORT_LEVEL 未设。
  - START 文件为总管 scratchpad 下的 start-p18c-b.md：8380 B，sha256 35e61b15d870e2b3c0bf898912e8d376e299fd92341886a7cfb37b342a8548f1。
  - 送达正文是文件去掉末尾换行：8379 B，sha256 e2200d07824aed2630088ec6315aeb1cb7962b52205cb0eb498e9a06a67133bf，23:33:46.962Z 写入 Builder transcript，与文件一致。已核实。
  - 恢复后的档位：23:33:57.682Z 起的 assistant 记录为 claude-fable-5-1、perTurnEffort medium；permissionMode bypassPermissions。符合。
  - 已订阅其空闲通知；另设后台检查（task b31awm2ld），pid 2996 退出就立即通知，最长 100 分钟。
- 清理清单新增：P18C QA 会话 scratchpad 的 5 个文件。P18C Builder 会话 scratchpad 的 21 个文件已在 19:22:26Z 条登记。
- 自 Owner 最近一次输入（23:17:46.812Z）以来，总管发出的跨会话消息为 4 条：QA 的恢复消息、P08D 的两条首条消息、Builder START。

## 2026-09-28T23:50:28Z（总管 date -u）P08D Builder PREACCEPT 回执已核实；P18C 的 F2 已提交

- P08D Builder：
  - 23:50:10.870Z 收到消息，第一行为“P08D Builder PREACCEPT 回执”。
  - 回执文件 units/p08d/builder/PREACCEPT-BUILDER.md：0644，44574 B，290 行，sha256 2f33f1775e65a714e1c6e6e5dc6d4f696f8bf0df55f6155b8703638652c1c4de。已核实。
  - 档位：23:22:30.008Z–23:50:10.321Z 的 211 条 assistant 记录全部为 claude-fable-5-1、perTurnEffort medium；permissionMode bypassPermissions。已核实。
  - 写入：units/p08d/ 下只有这一个文件。两个 P08D 会话的 harness 目录里都没有文件，即没有写会话 scratchpad。
  - 回执内容在 QA 的独立回执落地之前不入账。
- P18C 进度（只读核对，Builder clone 的 .git）：23:46:56Z 在分支 codex/p18c-3ae8006 上提交了 aca7bef，唯一父为 M20，tree 为 e529fda8e69f6259dbf78d7466cbd15868d812b6。Builder 还在跑自测，尚未交付 FREEZE。
- 兜底：P18C Builder 另加一道后台检查（task bsfyup4ud），transcript 连续 30 分钟没有写入、或者 FREEZE.json 出现，就通知总管。

## 2026-09-29T00:02:04Z（总管 date -u）P18C Builder 停止回报：已核实，按 (a) 裁定

- 收到：23:57:35Z，P18C Builder 用 SendMessage 发来“P18C Builder 停止回报”。
  - FREEZE.json：24176 字节，sha256 56002ec5c8b6f89335bdb0d8eda54b06abc1a3e0a767eebb5897593871bd1971，status 为 STOPPED_FOR_RULING。
  - REPORT.md：24866 字节，sha256 762ae483fbee2be4f7356eea9284221d0c9f04af4336a817d36d897d9fba7ef1。
- 总管独立核对（只读，Builder clone 的 .git）：
  - F2 为 aca7befea6a371554d50dfbbd25ad69b6da1ff45，tree e529fda8e69f6259dbf78d7466cbd15868d812b6，唯一父为 M20。author 与 committer 都是 P18C Builder <p18c-builder@local.invalid>，提交于 2026-09-28T23:46:56Z。
  - 按工单的原样 argv 在 env -i 下复算 diff：33907 字节，sha256 c4cb5362a02eb6c3ffa2dc57510d62631fc0144d17aa07d524fba2aa16f78f90，与 FREEZE.json 一致。
  - 工作树干净（git status --porcelain -uall 无输出）。
  - 档位：23:30Z 之后的 94 条 assistant 记录全部为 claude-fable-5-1、perTurnEffort medium；permissionMode bypassPermissions。
- 停止事项 1：反向验证“去掉 Schema 校验”一组与预期不符。
  - START 一.5 预期 U1、U2、U3、U8、U11 失败。实测失败的是 U2、U8、U11，以及 C2 的 extra-field、missing-claims-digest、glob-write-path、claims-marked-verified 四例。
  - U1（v1 收据）与 U3（contract_id 为 v3）没有失败，因为保留的判定 (c) 要求 contract_id 为 v2，也会拒绝它们。
  - 这是总管在 START 中写错了预期，候选本身没有问题。
  - 裁定 (a)：F2 不变，不改测试，不新增提交，不做补充的反向验证。更正后的预期就是实测的 7 项。
- 停止事项 2：23:54:32Z 的一次工具调用执行了 `/usr/bin/python3 -c "print()"`，违反“不得使用 /usr/bin/python3”，Builder 已自行披露。
  - 总管查了 ~/Library/Caches/com.apple.python：23:50Z 至 00:00Z 之间没有任何文件被修改。
  - 同一条命令接着用 uv 的基础解释器（-B）读了 Builder 自己的 transcript，没有用 .venv。REPORT 没有写这一点，已要求补进披露。
- 清理：以下三项都先不动，留待 Owner 决定。
  - harness scratchpad 中的 m20/ 目录，即 PREACCEPT 轮已记账的 21 个文件，832 KB；
  - $E/scratch 下的 m20-tree 与 rev-* 树副本；
  - $E/secrets/needle.txt（0600）。
- 00:01:41.577Z 裁定送达，经 SendMessage 发到 uds:/tmp/cc-socks/2996.sock，对方为 bypass。送达正文 2734 字节，sha256 4d2ed705f2bfa2a1f52d3be0c974d60e31290c399dadabe88ac693e5b6f87bbf，在对方 transcript 中逐字核对一致。
  - 要求 Builder：FREEZE.json 的 status 改为 FROZEN，新增 ruling 字段，并在 reverse_expectation_check.schema 中新增 corrected_expected；REPORT.md 前文不改，只在末尾追加第 10 节；然后发“P18C Builder FREEZE”。
- 下一步：FREEZE 到达、核实后，下达 P18C QA START。门禁位先给 P18C QA；P08D 的 QA 用门禁要等它的 Builder 交付，预计在 7–8 小时之后。

## 2026-09-29T00:08:00Z（总管 date -u）P18C Builder FREEZE 已核实；P18C QA START 已下达，门禁位给 P18C QA

- FREEZE：00:02:20Z 收到消息，第一行为“P18C Builder FREEZE”。
  - F2 与 diff 同停止回报：aca7befea6a371554d50dfbbd25ad69b6da1ff45，tree e529fda8…，diff 33907 字节，sha256 c4cb5362…。分支 tip 未变，工作树干净。
  - FREEZE.json：25822 字节，sha256 303783d980aa7244ee39eb59c353cb1721b65625db144401124099007f016728。Builder 用脚本读入原文件，只改了 status（改为 FROZEN），新增 ruling 字段，并在 reverse_expectation_check.schema 中新增 corrected_expected 与 match_corrected。总管读了该脚本（Builder transcript 00:02:12Z 的工具调用），其余字段没有改动。
  - REPORT.md：26691 字节，sha256 ca744d90b549a4bf35bad3c934c9a0805b46c5f9d3cc5a48652b543fe5dedb38。前 24866 字节与停止版（762ae483…）逐字节相同，只在末尾追加了第 10 节，内容与裁定一致，并补披露了 uv 基础解释器那次使用。
  - 档位：19:06:06.850Z–00:02:27.012Z 共 185 条 assistant 记录，全部为 claude-fable-5-1、perTurnEffort medium；permissionMode bypassPermissions。已核实。
  - 后台检查 b31awm2ld（P18C Builder 进程退出通知）已停止：Builder 已交付，处于空闲。
- P18C QA START：
  - 00:06:07Z 签发，晚于 FREEZE。文件为总管 scratchpad 下的 start-p18c-qa.md：9279 字节，sha256 fddcf1da003143ba6ec3525cfaa4bbe2683d32ce060fd21b4ea66228b49ff8fe。
  - 送达正文是文件去掉末尾换行：9278 字节，sha256 f2607c7a643b5b0980c98c71a2ebb60d5ada1d2a327f6f7058ec5a1d659251b8。00:07:33.386Z 写入 QA 的 transcript，与文件一致。对方 pid 82917，claude-fable-5-1 / max / bypassPermissions，CLAUDE_CODE_EFFORT_LEVEL 未设。
  - 对 QA 回执第 7 节的主要答复：
    - corpus 的 11 条 invalid 全部由 RN 自己失败关闭，判据同 Builder START 一.4；
    - 不读 Builder 的 transcript，档位由总管给出；
    - nuget.org 只限门禁中 locked-mode restore 期间的访问；
    - 不补 tests/README.md，不许新增任何文件；
    - QA 会话 scratchpad 的 5 个文件保留，留待 Owner 决定。
  - 另要 QA 独立核对：
    - 在 scratch 中复做“去掉 Schema 校验”的反向验证，评估裁定 (a)；
    - 两次解释器违规的披露是否完整；
    - REPORT 第 4.3 节提交前那次 wip 失败的定性；
    - 提交信息中没有 Owner 身份；
    - START 与 PREACCEPT 的时间顺序。
  - 门禁位分配给 P18C QA，按两步放行：先收到“P18C QA 门禁就绪”，总管复核后回复“P18C QA 门禁放行”。
- 兜底：新设后台检查 bm1av0a03。QA transcript 连续 30 分钟没有写入、或者 QA-REPORT.md 出现，就通知总管。QA 进程退出的后台检查 btw96jpfl 仍在运行。
- 自 Owner 最近一次输入以来，总管发出的跨会话消息累计 6 条（新增停止事项裁定与 QA START 两条）。

## 2026-09-29T00:45:39Z P08D QA 回执核实；更正 23:50:28Z 条；总管自我披露；P18C QA 门禁放行

- P08D QA 的 PREACCEPT 回执于 00:31:51.518Z 送达，发送方 P08D QA，bypass。总管 00:32:43Z 核实：
  - units/p08d/qa/PREACCEPT-QA.md，0644，79147 字节，473 行，sha256 9a324718fac176de96ac21bcf5bbad8af00fb7873626c14f4f745d013be2eb5e。units/p08d/qa 下只有这一个文件。
  - 档位：313 条 assistant 记录都是 claude-fable-5-1 / max，permissionMode 为 bypassPermissions。
  - 总管已读完全文。要点：
    - 同意 P08 出口上限为 PARTIAL。工单第 1.1 节 12 条独立核实一致，只有三处行号范围的写法不同。
    - 第 3.1 节 9 项默认中接受 8 项。锁文件一项预计有分岔：`--force-evaluate` 会改写既有 project 条目中过期的版本区间，与工单第 3.1 节“已有条目不变”冲突。QA 的默认判据是删去三个新键后与 M20 相同，且 locked restore 通过。
    - 列出 7 条拒收做法、夹具 public 面的类型闭包判据，以及反射探针、grep、反向验证抽查的做法。
    - 第 7 节有 13 项不清楚之处，各附默认做法，将在 QA START 中答复。
- 更正 2026-09-28T23:50:28Z 条：该条写“两个 P08D 会话的 harness 目录里都没有文件”，这只对 /private/tmp 下的会话 scratchpad 成立。harness 会把超长的工具输出持久化到 ~/.claude/projects/-Users-younghu-Documents-ZennoDroid-DSP-dps-air/<session>/tool-results/。00:45Z 实测：
  - P08D QA（104c34c5…）6 个文件，226599 字节，写于 23:22:51Z–23:52:00Z，与 QA 回执第 6.5 节一致；
  - P08D Builder（518af589…）24 个文件，2276913 字节，写于 23:22:40Z–23:39:09Z；
  - P18C QA（a3e1c227…）7 个文件，816733 字节，写于 2026-09-28T19:07:31Z–23:20:42Z。
  - 这些都不是单元的主动写入，列入 Owner 的清理清单。
- 总管自我披露：00:12Z 前后，我执行过一次 `~/.dotnet/dotnet --list-sdks`，用来确认全局 SDK 版本。这次执行是只读的，但总管不运行构建工具，不该这样做，以后不再做。
- P18C QA 的“门禁就绪”于 00:40:33.105Z 送达。总管 00:41:09Z 只读核对：
  - 全机没有 dotnet、testhost、postgres、MSBuild、VBCSCompiler 进程；
  - 56300–56399 与 55432–55434 没有监听；
  - /private/tmp 下没有 .s.PGSQL.* 与 dps-phase0-executor-*；
  - QA clone 的 HEAD 为 aca7befe…，tree e529fda8…，porcelain 0 行；
  - QA 档位：00:06Z 之后 68 条 assistant 记录为 claude-fable-5-1 / max，permissionMode 为 bypassPermissions。
- 放行：
  - 消息原文在总管 scratchpad 的 gate-release-p18c-qa.md，902 字节，sha256 b3c0b01b0842d13dd4c9819fdfa349656bd52e945c26e9dae933283a30affa85。
  - 送达正文 901 字节，sha256 c91dd810fd4e1ed375b19669ec362da82466430e89fbf58b0943a5ce21f3aa85，00:41:57.090Z 写入 QA 的 transcript。
  - 并发裁定：门禁进程结束之前不发 P08D Builder START，避免两个单元同时跑 dotnet 或 PG。
  - 门禁进程约 00:42:20Z 开始（pid 46152，子进程 46240）。后台检查 bowmzz8d6 会在门禁进程结束、run 目录出现 publication 文件时通知总管。
- 自 Owner 最近一次输入以来，总管发出的跨会话消息累计 7 条。

## 2026-09-29T00:50:20Z P18C QA 门禁结束；P08D Builder START

- P18C QA 门禁：
  - run id f98bfa74386826ec0c17d06fd117124f。门禁进程约 00:42:20Z 开始，00:47:37Z 之前结束（后台检查 bowmzz8d6，共 66 次检查）。
  - phase0-prerequisites 下有 candidate-evidence-f98bfa74….json（0600，477185 字节）与它的 .publication.json（406 字节）。结论与计数以 QA 报告为准。
  - 00:47:43Z 复核：没有门禁、dotnet、testhost、MSBuild、postgres 进程；/private/tmp 下没有 dps-phase0-executor-* 与 .s.PGSQL.*；563xx 无监听。只剩门禁构建启动的一个 VBCSCompiler（pid 49196，约 00:43:04Z 启动）。
- P08D Builder START：
  - 签发前核对：Builder 在 2026-09-28T23:20Z 之后的 212 条 assistant 记录都是 claude-fable-5-1 / medium，最新的 permissionMode 为 bypassPermissions。pid 85330 存活，argv 为 claude-fable-5-1、medium、bypassPermissions，CLAUDE_CODE_EFFORT_LEVEL 未设。
  - 原文在总管 scratchpad 的 start-p08d-b.md，19620 字节，sha256 8e0f26bb229e20b51c1b0b95b3313f8d6c12e59d6d2c34b1f822ef2b757e4df2。
  - 送达正文 19619 字节，sha256 f96be77a060a86016f828de471909b66dcc8b19b4606dd8096c2ac785fd59545，00:49:48.467Z 写入 Builder 的 transcript。
  - 要点：
    - 夹具：PAT 新增第 8 个 partial 部件 PolicyProviderFixture.cs，内含 public 嵌套夹具与 record。以 Builder 列的 public 成员为基线，只许三种调整；不改现有 PAT 文件。
    - CO 侧：COIT 只加 partial；CreateStore 加三个可选参数；新用例放在新文件。
    - 锁文件：
      - 改用普通 restore，不用 --force-evaluate。后者会重写闭包里全部 8 个工程的锁文件，而 CO src 与 PAT 的锁文件里有过期的 project 区间。
      - 预期 NuGet 生成的文件与 M20 恰有三处差异：新增三个 project 条目；dps.commandorchestrator.contracts 中 [0.6.0, ) 变为 [0.9.0, )；第 201 行加一个逗号。第二处改回 M20 原样，另两处保留。
      - 核对四项：jq 结构比对相同；文本 diff 只有插入与那个逗号；新条目与 NuGet 生成的逐字相同；locked restore 退出 0。
      - Builder 回执预计“新条目与 PAT 锁中的同名条目逐字相同”，这不成立，已在 START 中指出。
    - 用例：共 15 个，CO Integration 由 45 变为 60。
      - (c) 共 6 行。其中 expired 行由 CO 以租约过期拒绝；反向验证去掉 PCO:652–653 之后，应看到 PA 的时钟拒绝。
      - (e) 租期 180 秒。
      - (f) 改为“签名响应丢失后的原样重试”。
      - (g) 用默认选项、处于 kill 状态的 store。
    - README 与两个 CHANGELOG 的定稿原文。
    - 纳入 QA 回执的验收口径：7 条拒收做法、夹具类型闭包、必须嵌套、COT 源码搜索、COIT 的 diff 形态、反向验证抽查。
    - 反向验证只在 $E/scratch/rv 的副本中做；主 clone 从提交 C1 起保持干净。
    - 第一次运行 dotnet 之前，等门禁留下的 VBCSCompiler 自行退出；不结束它，也不连它构建；等候超过 20 分钟就停下回报。
- 兜底：后台检查 bo5x26pdh。Builder 的 transcript 连续 30 分钟没有写入，或 FREEZE.json 出现，就通知总管。
- 自 Owner 最近一次输入以来，总管发出的跨会话消息累计 8 条。

## 2026-09-29T01:02:00Z P18C 收口：QA FINAL 核实，结论 PARTIAL

- P18C QA 的 FINAL 于 01:00:01.532Z 送达，发送方 P18C QA，bypass。
- 总管 01:00Z–01:02Z 独立核实：
  - QA-REPORT.md，33435 字节，sha256 23a2573caaff9fcfa76a42be75575b91d037e08716a89e7e6581ab53666d4014；EVIDENCE_INDEX.json，61141 字节，sha256 b79ba8c75720158c4b7f4b0bef58abfb77a7fbe4c44886fbe6cc91d46af82651。总管已读完报告全文。
  - 证据索引的 207 个条目逐个复核大小、权限与 sha256，全部一致。其中门禁写出的 3 个 0600 文件只算哈希。索引排除了 0600 的 needle 文件，这个文件没有打开。
  - 门禁 run f98bfa74386826ec0c17d06fd117124f，按 02:17:51Z 裁定只取派生值：
    - 00:42:22Z–00:47:34Z，gate WORKSPACE_DIAGNOSTIC_ONLY，verification_level 与 commit_sha 为 null；
    - summary 为 total 53、passed 48、failed 5、infra_error 0；
    - baseline 为 M20，head_commit_observed 为 F2，workspace_clean 为 true；
    - 53 个 (index, id, status, exit) 元组与 M20 run 156d74d5… 逐行 diff 为空；
    - #5 日志非空行 63，比 M20 的 67 少了 4 条 RN 行；#29 为 Ran 55 tests OK；#7 scope 34 个模块，与 M20 相同；#10 project_count 71，solution sha256 前缀 a4f6e6c2；
    - 证据文件 477185 字节，sha256 368ea5433b058c9207c69540644781f8ab6b8fdaf0a9ccab24d33ab373a42756。
  - F2 未变：Builder clone 的 HEAD 与分支 tip 都是 aca7befe…，tree e529fda8…，porcelain 0；FREEZE.json 与 REPORT.md 的哈希未变。
  - QA 档位：START 之后 96 条 assistant 记录都是 claude-fable-5-1 / max，permissionMode 为 bypassPermissions。
  - QA 写入：单元根下只有 DSP_ZD、PREACCEPT-QA.md、environment。会话 scratchpad 仍是 PREACCEPT 阶段的 5 个文件，harness tool-results 仍是 7 个。
- 结论：P18C 为切片级 PARTIAL，不是 P18 出口，正式等级 NONE，外审 NOT_RUN（Owner 2026-09-27 暂停外审）。
- QA 的发现与自报：
  - F-1（低）：Builder clone 内有一个 Modules/factory-evidence-ledger/src/__pycache__/evidence_ledger.cpython-312.pyc，写于 23:36:46Z，是 M20 基线轮的 python -I 子进程写的。Builder 的 FREEZE 与 REPORT 没有列出它。按 16:34:08Z 裁定 4，由总管在此补记，不再要求 Builder 补写。
  - D-1：QA 在核对门禁口径时，只读列出了 units/m20-int/qa/environment 下 logs/ 与 home 的目录名，没有读内容，也没有写入。这违反了 QA START 七的字面规定，记为已披露偏离。
  - 观察 7 条（O-1 至 O-7），其中：Runner 的校验器没有 date-time 格式检查器，但有 pattern 与 _timestamp 解析兜底；tests/README.md 第二句不完整，但不是假话；RN 对 IR 的 versionRange 未变。
- 残留与归属：
  - Worktree Manager 与 Control Plane Host 仍消费 instruction.receipt/v1，归 P18 后续切片，由总管立单；
  - 生产 receipt_verifier 仍为 WAITING_EXTERNAL，归 RN 模块；
  - date-time 格式检查是否补 rfc3339-validator，由 Owner 决定；
  - F-1 已在本条补记。
- 另记：门禁留下的 VBCSCompiler pid 49196 在 00:52:32Z 至 00:59:13Z 之间自行退出。之后的 VBCSCompiler pid 74450 约 00:59:13Z 启动，只读核对其 HOME 与 TMPDIR 都在 units/p08d/builder/environment 下，属于 P08D Builder 自己的构建。
- 清理清单新增，全部待 Owner 决定：
  - P18C QA 的 $E/scratch/m20tree（22.7 MB）与 $E/scratch/rev-schema（22.8 MB）、$E/needles（0600）；
  - $QC 的 .venv、bin/obj、__pycache__ 与 46 个 .pyc；
  - QA 会话 scratchpad 的 5 个文件与 harness tool-results 的 7 个文件；
  - Builder 侧的清单见其 REPORT 第 8 节。
  - 两个 P18C 会话（Builder c251cd、QA de19b4）保持空闲，可以 --resume；关 tab 需要 Owner 同意。
- 兜底检查 ba73zi043 在 FINAL 核实后由总管停止。bm1av0a03 与 btw96jpfl 已正常结束，P18C 不再设检查。
- 自 Owner 最近一次输入以来，总管发出的跨会话消息仍为 8 条。

## 2026-09-29T01:21:00Z Owner 指示：新开 session 的档位

- Owner 于 01:18:16.407Z 在总管会话中输入（粘贴形式）：额度马上就要用完，新开的 session 设置为：Builder 用 “Sonic 5.5”，effort high；QA 用 “Ops 5.5”，effort max。
- 总管口径：
  - “Ops 5.5”即 claude-opus-5-5。可用模型里没有 Sonnet 5.5，“Sonic 5.5”按 claude-sonnet-5 执行，已在回复中告诉 Owner。
  - 新开的 Builder 用 claude-sonnet-5 / high，QA 用 claude-opus-5-5 / max。仍一律 bypassPermissions，开工前照旧检查 model、effort、权限模式三项。
  - 在跑的 P08D 两个 Fable 会话若中途额度用完，也按这套换：Builder 换 claude-sonnet-5 / high，QA 换 claude-opus-5-5 / max。这取代 2026-09-28 的“Builder 切 opus 5.5 / medium”。已发出的 P08D Builder START 第三部分第 9 条仍写“等总管换成 claude-opus-5-5 / medium”，届时以本条为准，并在续跑消息中说明。
- 这次 Owner 输入之后，跨会话消息额度重新计数，目前累计 0 条。
- P08D Builder 的 FREEZE.json 于 01:19Z 出现（17421 字节，C1 为 03b87f40…）。等 Builder 的 FREEZE 消息后再核实。

## 2026-09-29T01:26:30Z P08D Builder FREEZE 核实；两点披露裁定；P08D QA START

- 更正：上一条标题写的是 01:21:00Z，实际写入时 `date -u` 为 01:20:06Z。
- P08D Builder 的 FREEZE 消息于 01:21:53.300Z 送达，发送方 P08D Builder，bypass。总管 01:22Z–01:24Z 独立核实：
  - C1 为 03b87f40b727f3ff386e69d93c2d89639ee6a674，tree c11c503b6b4c11de4f635df6d19a0dee99f4763e，唯一父为 M20，author 与 committer 都是 P08D Builder <p08d-builder@local.invalid>，提交时间 01:02:07Z。分支 codex/p08d-3ae8006 的 tip 为 C1，porcelain 0，stash 0。
  - diff 按工单原样 argv 复算：49423 字节，sha256 0ed4531dac21938e49aea3a109d50bb5204d7f950c6a86a63e3c0df2382994f1，与 FREEZE 一致。改动 8 个文件，新增 2 个、修改 6 个，全部在 CO、PA 的 tests、README 与 CHANGELOG 下。
  - FREEZE.json 17421 字节，sha256 8fbd33fc5e315e9777547d58d3ad00e3f9a4fc65487c242125576d725480163e；REPORT.md 30092 字节，sha256 7366d985f23e8a0e785ab8166018668ad9310785ed0f8dd125778d9c9e091150。总管已读完 REPORT 全文。
  - FREEZE 的 evidence_files 共 73 项，其中 72 项的字节数与 sha256 由总管复核一致。env-logs/02-postgres.log 是 0600，没有打开。env-logs/03-pg_ctl-stop.log 在 FREEZE 之后写出，不在清单内。
  - 档位：START 之后 171 条 assistant 记录（00:49:55Z–01:22:02Z）都是 claude-fable-5-1 / medium，permissionMode 为 bypassPermissions。
  - 残留：没有 dotnet、VBCSCompiler、postgres、testhost 或 MSBuild 进程；563xx 无监听；没有 postmaster.pid；/private/tmp 下没有 .s.PGSQL.*。
  - 写入：单元根下只有 DSP_ZD、FREEZE.json、PREACCEPT-BUILDER.md、REPORT.md、environment。会话 scratchpad 为空。harness tool-results 共 25 个，START 之后新增 1 个，REPORT 第 9 节没有列出。
  - Builder 自报的结果：八套件 rc 0，CO 19/16/4/60，PA 13/41/9/88。锁文件三处差异与预期一致，(ii) 已改回。整解构建的 11 条诊断与 M20 相同。12 项反向验证都在目标用例上失败。扫描无命中。
- 总管的裁定与认定：
  - `using Npgsql;`：START 二之二.4 的“只 using …”说的是 PA 相关命名空间，措辞不精确，责任在总管。这个 using 只用于断言 PostgresException 类型，接受。
  - 删掉的原始日志：总管只读核实 Builder 的 transcript。01:11:59Z 它读取当时的 rv-00-baseline.log，内容含 “not found in the list of registered tools”、“Exit code: 5”、“Zero tests ran”；随后删除，用同名文件重跑。记为已披露的证据删除偏离，交 QA 评级，不要求补写。
  - logs/12、12b 两次 COT locked build 失败（CS0246）：按 REPORT 属新文件的编码错误，12c 通过，PAT csproj 未改。总管暂认定为提交前开发运行，交 QA 用日志核实。
  - 夹具 public 面比 START 允许的三种调整多了两处。一处是新增 ReadDatabaseClockAsync，这是 START 的 expired 设计所需，START 漏写，责任在总管。另一处是 AppendRuntimeRevisionAsync 调换了参数顺序并加了默认值。两处都接受。Builder 没有停下回报，记为轻微流程偏离，交 QA 记录。
- P08D QA START：
  - 签发前核对：P08D QA pid 85444 存活，argv 为 claude-fable-5-1、max、bypassPermissions，CLAUDE_CODE_EFFORT_LEVEL 未设；313 条 assistant 记录都是 claude-fable-5-1 / max。
  - 原文在总管 scratchpad 的 start-p08d-qa.md，12659 字节，sha256 10a551ed70dca135f61b00e6aef7f7e525d67557147407e7fdae0ebe3e4473dd。送达正文 12658 字节，sha256 37d0ffef517ea0946505a76e973156b9db7706edb25047aea04336d53c31ef54，01:26:14.382Z 写入 QA 的 transcript。
  - 要点：
    - 答复 QA 回执第 7 节 13 项。其中第 8 项改为：#7 的 scope 只作初步预期，候选是否越界以 git diff --name-status 为准。依据是 P18C run 的 scope 为全部 34 个模块。
    - 要 QA 独立核对 11 项，包括上面的四项裁定、README 分句、harness 披露遗漏与 Restored 行数。
    - 门禁位附条件分配给 QA，沿用第 2803 行的先例：进入条件满足就跑一次，不发就绪与放行消息；等候超过 60 分钟就停下回报。
    - Fable 额度用完时换 claude-opus-5-5 / max。
- 兜底：后台检查 b42029k2g。QA 的 transcript 连续 30 分钟没有写入，或 QA-REPORT.md 出现，就通知总管。进程检查 b12t4lezh 仍在运行。
- 自 Owner 01:18:16Z 输入以来，总管发出的跨会话消息累计 1 条。

## 2026-09-29T02:16:51Z P08D 收口：QA FINAL 核实，结论 PARTIAL；Owner 的 Create PR 未执行

- P08D QA 的 FINAL 于 02:14:06.356Z 发出，02:14:07.722Z 写入总管 transcript，发送方 P08D QA，bypass。
- 总管 02:14Z–02:16Z 独立核实：
  - QA-REPORT.md，35961 字节，sha256 c85abdc32923243c90f7dca6f4522314c063b82bd771333dabb23f4b2091e097；EVIDENCE_INDEX.json，26348 字节，sha256 ff6b68d9f9599ed42639e0e03c2d83c571ca487d4f546e9f1c6d18b178e218fb。总管已读完报告全文。
  - 证据索引 126 个条目，权限位全部与实际一致：
    - 121 个复算字节数与 sha256，全部一致，其中门禁写出的 3 个 0600 文件只算哈希；
    - QA 自己的 5 个 0600 文件（4 份 PG 日志与 pg/port）只核对字节数，一致，没有打开。
  - 门禁 run 1fdb4894f6396457308e22c2ff3f198e，按 02:17:51Z 裁定只取派生值：
    - 01:56:47Z–02:02:45Z，gate WORKSPACE_DIAGNOSTIC_ONLY，verification_level 与 commit_sha 为 null；
    - summary 为 total 53、passed 48、failed 5、infra_error 0；baseline 为 M20，head_commit_observed 为 C1；
    - 53 个 (index, id, status, exit) 元组与 M20 run 156d74d5… 逐行 diff 为空；
    - #5 的 log_sha256 前 16 位 71956e4d16b02647，非空行 67，与 M20 相同；#7 PASS，scope 为 command-orchestrator 与 policy-approval；#10 project_count 71，solution sha256 前缀 a4f6e6c2；
    - 证据文件 343590 字节，sha256 179ce7e28c252c6351dfeeabb78ede931b9f254288b5d7c32d3dd026d4e99525。
  - C1 未变：Builder clone 的 HEAD 与分支 tip 都是 03b87f40…，tree c11c503b…，porcelain 0，stash 0；FREEZE.json 与 REPORT.md 的哈希未变。QA clone 的 HEAD 为 C1，porcelain 0。
  - QA 档位：START 之后 81 条 assistant 记录都是 claude-fable-5-1 / max，permissionMode 为 bypassPermissions。
  - QA 写入：单元根下只有 DSP_ZD、PREACCEPT-QA.md、environment。会话 scratchpad 没有建。harness tool-results 8 个文件。
  - 残留：没有 postgres、dotnet、testhost、MSBuild、VBCSCompiler 或门禁进程，563xx 无监听。
- 结论：P08D 为切片级 PARTIAL，不是 P08 出口，正式等级 NONE，外审 NOT_RUN（Owner 2026-09-27 暂停外审）。C1 作为产品线定稿，下文称 K2。
- QA 的发现（5 条，均不阻塞）：
  - F1：Builder 删除调用出错的原始 rv-00-baseline.log 后同名重跑，已披露；
  - F2：夹具 public 面多出的两处未停下回报，总管已裁定接受；
  - F3：Builder REPORT 没有列出 harness tool-results（25 个文件）；
  - F4：QA 自己回执中的 #10 指纹写错，复算为 fade7c1eb3e26e25，比对以逐行 diff 为准；
  - F5：#7 的 scope 为 2 个模块，M20 run 为 34 个，属预期差异。
  - 另有观察 12 条。
- 总管补记：
  - D-1：QA 读了 Builder 的 environment/env.sh（1026 字节，0644）。QA START 五只许读 Builder environment 下 scratch/ 与 logs/ 中的非 0600 文件。QA 读前先确认其中口令模式计数为 0，并在报告第 14 节列出了这次读取，但没有记为偏离。记为已披露的越范围读取，不阻塞。
  - 报告开头写“QA START 收到 01:25:16Z”，01:25:16Z 其实是 START 标题中的签发时刻，送达时刻为 01:26:14.382Z。措辞不准，不影响结论。
  - O11 裁定：C1 的提交信息末尾有 harness 署名两行（Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com> 与 Claude-Session 链接）。F2 有同样两种行，M20 没有。它们不是 Owner 身份，接受，已冻结的提交不改。之后的单元也允许这两种行，但不得写入 Owner 的姓名或邮箱。
- P08 仍未出口。剩余项及归属见 coordination/research/P08-SLICE-C-SCOPING-20260928.md 第 6 节。其中“会变成悬空义务的句子”在 P08 结案时处理。
- 清理清单新增，全部待 Owner 决定：
  - P08D QA：$E/scratch（105 MB，含 rv clone、M20 树、探针）、$QC 的 .venv（35 MB）、142 个 bin/obj 目录、46 个 .pyc、$E 下的 nuget、home、dotnet-home 等缓存目录、harness tool-results 8 个文件；
  - P08D Builder：见其 REPORT 第 9 节，另加 harness tool-results 25 个文件；
  - 两个 P08D 会话（Builder tab c2、QA tab c3）保持空闲，可以 --resume。关 tab 需要 Owner 同意。
- Owner 输入：01:58:00.854Z，Owner 在总管会话中点了 Create PR（目标 HelloYoung2025/DPS，分支 claude/stoic-keller-19754e，base main）。
  - 现场：总管会话的 worktree 干净，本会话没有在其中提交；分支与本地 main 都在 458f9bd（2026-07-15 的 WIP 快照），远端 main 为 cac7ccb，远端没有这个分支。
  - 建 PR 等于把这个旧 WIP 提交推到远端，与硬约束“不写 DSP_ZD 仓库”“不合并、不发布”冲突。总管没有提交、推送或建 PR，已请 Owner 明确是否放开这一次。
  - 这次 Owner 输入之后，跨会话消息额度重新计数，目前累计 0 条。
- 进程检查 b12t4lezh 由总管在本条写入后停止。P08D 不再设检查。
- 下一步（总管决定）：开两线合流单元 M21-INT，把 F2（aca7befe…）与 K2（03b87f40…）合成 M21。
  - 预演（01:30Z，总管 scratch 中的 bare 库，alternates 指向两个 Builder clone）：F2 改 9 个路径，K2 改 8 个，交集为空；merge-tree 退出 0，tree 656556fd2f662301c5d31d5cc582ba7dc4b2e705。
  - Builder 用 claude-sonnet-5 / high，QA 用 claude-opus-5-5 / max（Owner 01:18:16Z 指令）。端口 56360–56379。开出后终端 tab 共 6 个，达到上限。

## 2026-09-29T02:22:40Z 派发 WO-M21-INT；M21-INT 两个会话已开出；Builder 档位裁定

- 工单：coordination/WO-M21-INT.md，20206 字节，sha256 30d495f6a32acf31008822743c5300ee43a35354b72b45672c6226604ad7a940。依据是本账本 02:16:51Z 条的下一步。
  - 起草时总管核实：factory-control-plane-host 消费 RN，F2 改了 RN，而 P18C QA 没有跑 PG 套件。所以工单第 1.2 节把它的 postgresql18 套件记为 F2 线上的未覆盖项，归 P18 中让 Worktree 与 Host 改用 receipt v2 的切片，不在本单元补跑。
- 02:20:49Z 前后在终端面板用 CLI 开出两个会话。首条指令都是档位自检加 PREACCEPT：
  - Builder：tab c5，pid 54255，session 497f4bee-e731-4ea2-98d0-bb4ff2f9e0f3。argv 为 claude-sonnet-5、--effort high、bypassPermissions，CLAUDE_CODE_EFFORT_LEVEL 未设。首条指令为 scratchpad/first-m21-b.md，6305 字节，sha256 73726caab58197d115a2b2abb89a9f22aefe7d58af1129c4a83172cbb5f5b741。
  - QA：tab c6，pid 54365，session f5a6b221-e4da-45b3-8929-454e262c55a6。argv 为 claude-opus-5-5、--effort max、bypassPermissions，变量未设。首条指令为 scratchpad/first-m21-qa.md，7742 字节，sha256 3f1774eb4230eb493779c4a7079a60f1cb5a06e9289b22cd60716f215b7b88c1。
  - 终端 tab 现为 6 个，达到上限：P18C 两个、P08D 两个、M21-INT 两个。
- Builder 档位：
  - Builder 于 02:21Z 回报“M21-INT Builder 档位不符”，没有写入。原因是它的 assistant 记录中 perTurnEffort 为 null，effort 为 high，而首条指令要求 perTurnEffort 为 high。
  - 总管核实：该会话 12 条 assistant 记录都是 model claude-sonnet-5、effort high、perTurnEffort null。opus 5.5 与 fable 5.1 会话的记录中这两个字段总是相同。
  - 裁定：视为符合，Builder 继续做 PREACCEPT。此后对 claude-sonnet-5 会话的核对改为：effort 为 high，perTurnEffort 为 null 或 high。首条指令没有预见到这一点，责任在总管。
  - 更正总管自己的读数：总管 02:21:34Z 的检查脚本按 (model, perTurnEffort, effort) 的顺序输出，总管当时把 (claude-sonnet-5, None, high) 读成了“effort 为 high”，没有注意到 perTurnEffort 为 null。这个误读没有写入账本，也没有告诉 Owner。
  - 裁定原文在 scratchpad/ruling-m21-b-gear.md，843 字节，sha256 6bff99abefd79d2e0ffabb3a8e200c44bbe144d8fc7e9f5f69783f40d89dae34。送达正文 842 字节，sha256 dfd816f02bbb1b1680bc5bbe7f1b1359e638a3f7ad097f279b9457bcb3cd9730，于 02:22:26.491Z 写入 Builder 的 transcript，与文件一致。
- QA 档位：27 条 assistant 记录都是 claude-opus-5-5，effort 与 perTurnEffort 都是 max，permissionMode 为 bypassPermissions。
- 自 Owner 01:58:00Z 的输入以来，总管发出的跨会话消息累计 1 条，即上面的裁定。

## 2026-09-29T02:59:44Z M21-INT 两份 PREACCEPT 已核实；工单两处更正；Builder START

- Builder PREACCEPT：
  - 02:33:23.513Z 送达，发送方 M21-INT Builder，bypass。
  - 文件 units/m21-int/builder/PREACCEPT-BUILDER.md，33783 字节，sha256 b240c3300a29b05b7f20e39331cfa7b8200583d4c09a11cd788766fc186dcf9b。总管已读完全文。
  - 写入只有该文件与 environment/probe.git，后者含 2 个 tree 对象，其一为 656556fd…。会话 scratchpad 没有文件，也没有 tool-results。
  - 档位：114 条 assistant 记录都是 claude-sonnet-5，effort 为 high，perTurnEffort 为 null；bypass。
  - 回执中需要纠正的，都已写进 START：
    - 提交前用 `git status --porcelain` 判断无冲突，这一判据不成立；
    - `diff --check` 的一个基点写成了 M20；
    - 引用了新 clone 里不存在的分支名；
    - 第 4 节有一句“仍需先跑一次 --write”，与它自己第 8 节第 3 项矛盾，已作废；
    - PG 连接方式套用了 factory 实例的写法。
- QA PREACCEPT：
  - 02:56:58.915Z 送达，发送方 M21-INT QA，bypass。
  - 文件 units/m21-int/qa/PREACCEPT-QA.md，57487 字节，sha256 fa332d8b43c2a49b3f2dcaadeb36904ad21d2666e5c19415828d6c656d5b7606。总管已读完全文。
  - 写入只有该文件与 environment/probe.git，后者含同样 2 个对象。harness tool-results 1 个（29.7 KB，QA 没有读），会话 scratchpad 没有文件。
  - 档位：claude-opus-5-5，effort 与 perTurnEffort 都是 max，bypass。
  - QA 独立算出两份父 diff 的预期值：F2..M21 为 49423 字节、0ed4531d…，K2..M21 为 33907 字节、c4cb5362…。
  - 它还从两个父门禁 run 取派生值：53 元组指纹同为 8ada2222741ec651，#5 中 F2 的 63 条是 K2 67 条的子集。
  - QA 自报 D-1：对两个父 clone 各运行了一次 git for-each-ref，这是清单外的只读命令。接受为已披露偏离。
- 互看目录：
  - Builder 02:32:58Z 运行 `find units/m21-int`，列出了 QA 目录下的文件名；
  - QA 用 stat 看了 Builder 目录的文件名与大小；
  - 双方都没有读对方文件的内容，都已自行披露。记为轻微偏离。START 已要求 Builder 此后不读、不列 QA 目录；QA START 中作同样要求。
- 工单更正。以下两处由 QA 指出，总管已核实；预期值都不变，工单文件不改，以本条为准：
  1. 第 1.1 节第 5 条把“#7 的 scope 为全部 34 个模块”的原因写成 F2 改了根 CHANGELOG.md，这是错的。
     - F2 的 Tools/ci/phase0.py 第 3547–3550 行规定：只有改动路径等于 AGENTS.md，或以 governance/、Governance/、.github/、Tools/ci/ 开头时，scope 才扩为全部模块。
     - 所以真正的原因是 F2 改了 governance/modules/compatibility.yaml。
     - P08D QA 报告第 10 节第 4 条对 M20 run 的同一归因也不准确。
  2. 第 1.1 节第 4 条说 markdown=247 是 ls-tree 计数，这是错的。
     - 预演 tree 中 .md 路径共 262 个，其中 15 个含非 ASCII 字符，其余 247 个。validate_repo.py 的口径与此相符。
     - 总管预演时用的 sed 匹配漏掉了 git 转义过的非 ASCII 路径，所以恰好得到 247。
- Builder START：
  - 原文在 scratchpad/start-m21-b.md，5032 字节，sha256 b975a9cca252f8cdca105a3f7cec409c89efb0a773750dc106194d72a5c21937。送达正文 5031 字节，sha256 2428fc0800f35bc1beaeaf067cce1281fd5428a3a6c4f5f801479eb1e590a534。02:59:25.265Z 写入 Builder 的 transcript，与文件一致。
  - 更正：START 标题写的是 02:59:30Z，这是总管发送前预填的时刻，比实际送达晚 5 秒；送达时刻以 02:59:25.265Z 为准。START 晚于两份 PREACCEPT 这一点不受影响。
  - 要点：
    - 提交信息逐字定案，正文五行，末尾加两行 harness 署名；
    - 全程只跑 generate_governance 的校验模式；
    - git 命令一律在固定环境中执行，并带单元身份；
    - `--no-commit` 阶段核对四项：write-tree、ls-files -u、cached diff 与 M20..K2 相同、`--cached --check`；
    - 连接串经 DPS_TEST_POSTGRES 传递，用 Passfile 形式；DPS_DOTNET 只读使用全局 SDK；
    - Builder 不读、不列 QA 目录。
  - 下达前核对：02:58:42Z 时 563xx 无监听，没有 postgres、dotnet、testhost、MSBuild、VBCSCompiler 或门禁进程，/private/tmp 下没有 .s.PGSQL.* 与 dps-phase0-executor-*。Builder pid 54255 存活，argv 未变。
- QA 回执第 7 节的 U1–U12 在 QA START 中答复，其中 U5 是 Builder 档位的回读方式，U6 是门禁放行，U9 是提交信息原文的出处。
- 自 Owner 01:58:00Z 的输入以来，总管发出的跨会话消息累计 2 条。

## 2026-09-29T03:05:02Z Owner 两项授权；四个空闲会话的恢复留底

- Owner 于 03:03:58.534Z 在总管会话中输入：
  1. “授权 Create PR 放开约束,你可以自行创建。”
  2. “授权空闲的tab可以关闭，并记录好留底，以后要用再开启resume.”
- 总管口径：
  - 第 1 项只放开这一次 Create PR（仓库 HelloYoung2025/DPS，分支 claude/stoic-keller-19754e，base main）所需的推送与建 PR。其余硬约束照旧，包括不合并。
  - 第 2 项的“空闲 tab”指已收口单元的四个会话：P18C 两个、P08D 两个。M21-INT 的两个会话在用，不关。
- 这次 Owner 输入之后，跨会话消息额度重新计数，目前累计 0 条。
- 留底：四个会话都已空闲，最后一条带时间戳的记录都是 turn_duration。关 tab 会结束进程，transcript 保留在 ~/.claude/projects/-Users-younghu-Documents-ZennoDroid-DSP-dps-air/<session id>.jsonl。

| tab | 名称 | pid | session id | 档位 | 最后一轮结束 |
|---|---|---|---|---|---|
| c4 | P18C Builder | 2996 | ff7b0c84-0c24-4126-8b47-58542a7a69ea | claude-fable-5-1 / medium | 00:02:27Z |
| c1 | P18C QA | 82917 | a3e1c227-ba0a-4f0b-881c-a55f262e1e31 | claude-fable-5-1 / max | 01:00:16Z |
| c2 | P08D Builder | 85330 | 518af589-5eac-408b-a6f0-cb3c034c9da2 | claude-fable-5-1 / medium | 01:22:02Z |
| c3 | P08D QA | 85444 | 104c34c5-ce06-4d52-9c62-2868ec181e3c | claude-fable-5-1 / max | 02:14:21Z |

- 恢复方法：用终端面板 CLI 起。下面以 P08D QA 为例，其他三个只换 session id 与名称：
  - `cd /Users/younghu/Documents/ZennoDroid_DSP/dps-air && env -u CLAUDE_CODE_EFFORT_LEVEL claude --resume 104c34c5-ce06-4d52-9c62-2868ec181e3c --model claude-fable-5-1 --effort max --permission-mode bypassPermissions -n "P08D QA" --remote-control "P08D QA"`
  - Fable 额度用完时，按 Owner 01:18:16Z 指令换档：Builder 用 claude-sonnet-5 / high，QA 用 claude-opus-5-5 / max。
  - 恢复后先核对 model、effort、权限模式三项。

## 2026-09-29T03:09:21Z 四个 tab 已关闭；PR #1 已建

- 关 tab：03:05Z 关闭 c4、c1、c2、c3，即 P18C Builder、P18C QA、P08D Builder、P08D QA。
  - 03:05:23Z 核实：四个进程都已退出，transcript 大小与关闭前相同。M21-INT 的两个会话（pid 54255、54365）仍在运行。
  - 恢复方法见本账本 03:05:02Z 条。终端 tab 现为 2 个。
- PR：按 Owner 03:03:58Z 的授权建立。
  - 推送前总管只读核实：
    - worktree 干净。要推送的只有一个提交 458f9bd，远端 main 为 cac7ccb。改动 1082 个文件，新增 986、修改 39、删除 57。
    - 仓库 HelloYoung2025/DPS 是公开仓库，没有 PR 模板，这个分支此前没有 PR。
    - 扫描结果：
      - 文件名没有密钥类，没有大于 1 MB 的文件；
      - 私钥头、GitHub token、口令赋值、带口令 URI 的命中都在测试代码中，是合成值；
      - 邮箱只有保留的测试域名；
      - 没有本机用户路径、Tailscale 地址或私网 IP；
      - Owner 的姓名与邮箱在文件内容中命中 0。
    - 458f9bd 的 author 与 committer 是本机生成的身份（主机名 .local 域），远端历史中此前没有出现过，推送后公开。
  - 03:06Z 前后执行 `git push -u origin claude/stoic-keller-19754e`，rc 0，远端新建该分支并设了 upstream。
  - PR 为 https://github.com/HelloYoung2025/DPS/pull/1，不是草稿，base main，标题 “chore: modernization baseline snapshot (WIP)”。
  - PR body 顶部写明：“合入后还需再开的 PR 数”未能论证为 0，论证之前不应合入。这是 Owner 全局规则中治理类 PR 的要求。总管按 Owner 的明确授权先开 PR，这一冲突已写进 body，并向 Owner 报告。
  - app 已绑定该 PR。03:09Z 读到的检查数为 0；auto-merge、auto-fix 都没有开。总管不合并，不开 auto-merge。

## 2026-09-29T03:31:50Z M21-INT Builder FREEZE 核实：REPORT 有三处与事实不符，并有口令外泄；总管自我披露；QA START 已送达

- M21-INT Builder 的 FREEZE 于 03:25:06.028Z 写入总管 transcript，发送方 M21-INT Builder，bypass。
- 总管 03:25Z–03:28Z 独立核实：
  - M21 = f5943bd41ac6d67eda9d1820dedb64cefb0229ee，tree 656556fd2f662301c5d31d5cc582ba7dc4b2e705，父依次为 F2、K2。author 与 committer 都是 M21-INT Builder <m21-int-builder@local.invalid>。
  - 分支 codex/m21-int-3ae8006 的 tip 与 HEAD 都是 M21。reflog：03:01:46Z 从 F2 建分支，03:02:05Z 合并提交，都晚于 START（02:59:25.265Z）。porcelain、stash、未合并项都是 0。
  - 提交信息是 Builder START 第一部分第 2 项的原文，外加两行 harness 署名（Co-Authored-By: Claude Sonnet 5，以及 Claude-Session 链接）。
  - Owner 身份检查：needle 共 8 个，都从 history.git 的提交作者与提交者导出，排除 local.invalid，取值不回显。在 M21 提交对象中命中 0。阳性对照：一个 Owner 身份的历史提交有 2 个 needle 命中。
  - 两份 diff 按工单 argv 复算：F2..M21 为 49423 字节、sha256 0ed4531d…；K2..M21 为 33907 字节、sha256 c4cb5362…。两份都与总管在 FREEZE 之前、用预演 tree 独立算出的预期逐字节相同。两个 --check 的退出码都是 0。
  - 交付文件：FREEZE.json 21188 字节，sha256 ea63b61bd897f69de1dcde7520c72a4d7fc51637d65bf8bdfee719c31426876f；REPORT.md 14736 字节，sha256 935733ae4a132fb3e898f9acc83e8c2a910492049ba2aa3eab76b99916c81b59。总管已读完 REPORT 全文。
  - evidence_files 共 49 项：48 项的字节数与 sha256 复核一致；logs/11-postgres.log 是 0600，只核了字节数（128912），一致，没有打开。
  - 档位：START 之后 182 条 assistant 记录（03:00:11Z–03:25:17Z）都是 claude-sonnet-5，effort 为 high，perTurnEffort 为 null；permissionMode 为 bypassPermissions。
  - 残留：没有 postgres、dotnet、testhost、MSBuild、VBCSCompiler 或门禁进程；563xx 无监听；没有 postmaster.pid；/private/tmp 下没有 .s.PGSQL.*。
  - 写入：
    - 单元根下只有 DSP_ZD、FREEZE.json、PREACCEPT-BUILDER.md、REPORT.md、environment。其中 environment/probe.git 是首条指令第 3 节允许写的。
    - P18C、P08D 两个 Builder clone 自 START 以来没有文件变动。~/Library/Caches/com.apple.python 没有新文件。
    - Builder 会话：tool-results 目录不存在，即 0 个；tasks/ 下 1 个文件（b3qsa6zb5.output，885 字节）；scratchpad 为空。
    - /private/tmp：03:07:53Z–03:11:55Z 之间新增 9 个名为 32 位十六进制的 socket，是 dotnet test 的 MTP socket，REPORT 第 9 节没有列出。
- 总管 03:26Z 只读 Builder 的 transcript，只抽取涉及 co-pg、日志目录与 pgpass 的命令及其输出，查实以下三点：
  - 甲，co-pg 的运行经过与 REPORT 不符：
    - 03:08:15Z、03:09:02Z 两次运行都是 60 个全失败（No password has been provided），日志写在当时的 environment/logs/23-suite-co-postgresql18.log。第二次没有任何修正，是原样重跑。
    - 03:09:37Z 把 pgpass 改成四段全通配。03:09:45Z 的运行 60/60 通过，时长 29s 765ms，即现在的 23b-…-wildcard.log。
    - 03:10:53Z 收窄为只通配 database 字段。03:11:02Z 的运行 60/60 通过，时长 29s 352ms，写回同名的 23-…log，覆盖了失败日志。
    - 03:18:59Z 把 environment/logs 复制进 Reports，03:19:07Z 删除 environment/logs。
    - 所以原始失败日志没有保留。REPORT 第 5、12 节与 FREEZE 的 deviations D-1 说 23b 是“保留的首次失败日志”，与事实不符。失败后原样重跑一次、前后共四次运行，超出工单第 3.2 节第 5 项的“修正一次”。
  - 乙，口令外泄：
    - Builder 在 03:08:42Z、03:09:38Z、03:10:53Z 三次用 cat 把 pgpass 全文打印进了自己的 transcript。
    - 03:18:04Z 做口令扫描时，把口令作为 /usr/bin/grep 的命令行参数。
    - 这违反工单第 3.1 节“口令不得进入 URI、argv、环境变量、日志或证据”。REPORT 第 6 节说“从未出现”，与事实不符。
  - 丙：/private/tmp 下的 9 个 socket 没有披露，见上。
- 总管自我披露：
  - 03:26Z 的提取脚本对邮箱打了码，但没有对口令打码，把 tool result 原样打印了出来。其中含口令的 pgpass 行因此进了总管的 transcript。
  - 该口令属于 Builder 的测试实例（127.0.0.1:56360，已停机）。按作废处理：该实例不得再启动，environment/pg 与 secrets/ 列入清理清单。
  - 教训：读其他会话的 transcript 时，只打印元数据，或先对口令模式打码。
- 裁定（总管）：
  - M21 可作为 QA 的对象。它的内容由 tree 相等唯一确定，不取决于 Builder 自测的经过。
  - 甲、乙、丙记为已核实的偏离，乙属于违反工单规定。冻结文件不改（改了哈希会变），也不要求 Builder 补写，没有给 Builder 发消息。三点都交 QA 独立核对并评级（QA START 第四部分第 7–10 项）。
- M21-INT QA START：
  - 签发前核对：
    - QA 进程：pid 54365 存活，argv 为 claude-opus-5-5、max、bypassPermissions，CLAUDE_CODE_EFFORT_LEVEL 未设。
    - QA 档位与状态：199 条 assistant 记录都是 max / max，最后一轮 02:57:11Z 以 end_turn 结束。ListAgents 显示 idle。
  - 原文在总管 scratchpad 的 start-m21-qa.md，10187 字节，sha256 3ca8d18f5994d7ddde2f24d62b997c03310298ef856572e3c2c51ca2199241ba。
  - 送达：正文 10186 字节，sha256 a1fad54fe0dcbe866f8e5b5a1a285188fd9e69b7c2784d68bfe8893fa688a0f4，03:30:30.217Z 写入 QA 的 transcript。
  - 标题时间：发出前从预填的 03:30:00Z 改为 date -u 实测的 03:29:12Z。
  - 要点：
    - 答复 U1–U12，其中 U5 附总管的档位实测。
    - 门禁位附条件分配给 QA，进入条件另加“Builder 进程 54255 没有在运行任何命令”。
    - 第四部分第 7–10 项交 QA 独立核对，即上面的甲、乙、丙，以及另外两个 clone 没有写入。
    - Builder 的口令作废，QA 不开 Builder 的 secrets/ 与 pg/。
- 下一单元摸底：03:14:30.111Z 与 03:14:58.166Z 派出两个只读子代理（general-purpose），不写文件，报告只作数据。
  - 一个摸产品线 P09 的首个切片，一个摸 Factory 线的下一片（P18 S3 或替代项）。
  - 基线都是 M21 的 tree 656556fd…，经总管 scratch 中的预演库读取。
  - 回来后留档到 coordination/research。
- 后台检查：
  - b5g6ogofj 于 03:21:54Z 因 FREEZE.json 出现而结束；bbt3ep4w3 已结束。
  - 新设 b9am2rpkh，以下任一情况发生就通知总管：QA-REPORT.md 出现、QA 或 Builder 进程退出、QA 的 transcript 30 分钟没有写入、到达 3 小时上限。
- 自 Owner 03:03:58Z 输入以来，总管发出的跨会话消息为 1 条。

## 2026-09-29T04:14:58Z Owner 要求全面回顾：两份摸底与三份审计已留档；总管的更正与排程裁定

- Owner 03:36:52.382Z 输入：“你回顾一下所有的已经做过的和没有做过的章节，看看有哪些遗漏的地方或者安排不妥的”。
- 留档（都在 coordination/research/，格式同前：总管抬头、分隔线、代理原文；嵌入原文逐字节核对一致）：
  - P18D-SCOPING-20260929.md，22943 B，sha256 f202a36ae3630984fd36bd7fd03f60c0ae3d9a91d964de8ada3aacd059a1914c。代理 03:14:58Z 派出，03:42:16Z 完成。抽查 8 条命令全部一致。
  - P08E-SCOPING-20260929.md，27556 B，sha256 b5ebcfa5039c36f542efe9c7eec6be49ca393349ffb2945dbe0faa1463baff0d。原题“P09 首个切片”，03:14:30Z 派出，03:54:50Z 完成。抽查 8 条命令全部一致，另核 PDD :68–74、:500–511。
  - AUDIT-P00-P10-20260929.md，26342 B，sha256 bbe6d2eab8dd482405e5fa2a1c947be9b70188f7855c8da3f63d21a59429e83b。03:38:16Z 派出，04:06:52Z 完成。
  - AUDIT-P11-P28-20260929.md，23566 B，sha256 a73ad063f1f6f9c9b96df443ffeeea3330a87bc81f7c34e2c420205dba9882a9。03:38:37Z 派出，04:11:11Z 完成。
  - AUDIT-RESIDUALS-PROCESS-20260929.md，25153 B，sha256 d52cd77a255a81e2f01e00ac699120041f9844be41717b1dd9684289f81ec016。03:38:58Z 派出，04:05:56Z 完成。
- 总管抽查属实的关键事实（只读）：
  - 账本中 P20、H4 各 0 次；H1 只出现 1 次，是测试项编号。P03、P20 从未向 Owner 提请，也没有做准备材料。
  - P05 的测试缺陷在 M21 中仍在：platform-account-registry 测试 :483 引用迁移中不存在的列；persona-store 测试 :668、:132 在 CREATE ROLE 中使用绑定参数。
  - P07 的签名只查存在与格式（本账本 :1879 由总管定），QA 实测篡改签名被接受（P07 QA :295、:605），此后无人承接。
  - PA operations/README 关于 kill_policy_approval 的说法前后矛盾（:11 与 :41）。
  - 门禁 #10、#17、#46 的根因：GBrainProjectionRenderer 的构造函数为 internal 且带参数，两处测试仍用无参调用。interest-reducer 仍消费 memory.event v1，而 owner 侧的 v1 已是 quarantine-only。
  - PR #1 的 458f9bd 不在 M21 的 clone 中。cac7ccb 是两者的共同祖先，M21 比它多 64 个提交，全部只在本地。
  - ORCHESTRATION-SESSIONS §5 第 5 项有 Owner 09-27 给的“收尾时清理本单元残留”授权，但 Owner 09-27T14:41:03Z 又说“清理类晚一些处理”。现状：units 31G，environment/secrets 22 个，/private/tmp 下 dps-* 59 个。
- 总管更正（只追加，不改旧条）：
  1. 第 2901 行“切片 D 并入 P09”与 PDD :502 不符。kill 接入签名、跨库 O2 属 P08 第 3 步，改由 P08E 承接。UNKNOWN 出口与 QO-4 归 P09 工序 5（CO 003），P06 S1 O5 归 P09 工序 3。总管 03:14:30Z 派 P09 摸底时照搬了这个错误。
  2. 第 1278 行“P03 等载体具体之后再提请”，以及第 2137 行 P00 出口后只列了 P01、P18：结果是 P03、P20 零准备、零提请，与 PDD :72、:113、:206、:1279 不符。
  3. 第 2903–2904 行先开 P18B、后开 P08C，理由与 PDD :80“优先最短产品链”相反。
  4. 第 2372 行“责任包由总管指定”未兑现：P01 查出的产品 FAIL 至今没有责任包。
  5. P06、P07、P08A 的“PARTIAL 即实际出口（知情接受）”是总管裁定的，没有经 Owner 明示；P00 一例是事先约定的。此事改为提请 Owner。
- 排程裁定（总管，依 Owner 2026-09-27T03:43Z 授权“新工作由总管自决”）：
  1. M21-INT 收口前不开任何写单元。
  2. 产品线下一片是 P08E，只写 PA，基线 M21。
  3. P05 补轮：先派只读摸底。P05 的七个模块（PDD :394）不含 PA，写域与 P08E 不相交，可以并行。
  4. Factory 线：P18D 暂缓，S4、S5 停，等 P03 有进展。理由：门禁层到 P23 之前看不到效果；它独占共享路径；与关键路径不相称。
  5. P03、P20：总管先做只读准备，包括 PDD :348 的缺项清单，以及按 PDD :200 格式写的请求，然后一次性提请 Owner。
  6. 残留登记：把本次三份审计列出的悬空项逐项绑定到责任包，或写明不做的理由，另条入账。
- 待 Owner 决定：P03（受保护仓库、一位真人审核者的 GitHub 用户名、部署与证据责任人）；P20（非生产 Windows/Zenno 环境与测试设备）；PARTIAL 是否可作实际出口；是否恢复外审；PR #1 的处置；清理总表；非 18.4 PG 负控、PKCS8 测试签名者、rfc3339-validator；AGENTS.md 逐字替换这一先例是否可接受。
- 自 Owner 03:36:52Z 输入以来，总管发出的跨会话消息为 0 条。

## 2026-09-29T04:47:40Z M21-INT 收口：结论 PARTIAL，M21 成为本地集成头；QA 发现的处置；两个会话留底

- QA FINAL：04:40:36.233Z 由 M21-INT QA（pid 54365 的 socket）送达总管。正文 591 B，sha256 2e0f1a9195927e19713779879683d2a9c1187be1354c4b1faf904661bbb450ab。结论 PARTIAL，没有停止事项。
- 总管只读核实（最后一次测量在 04:45:01Z）：
  - 交付都在 units/m21-int/qa/DSP_ZD/Reports/ci/m21-int-qa/，与 FINAL 所报一致。总管已通读 QA-REPORT。
    - QA-REPORT.md，51384 B，sha256 65acc56d97391a6aad07bf1ab2a4cf754434efc8a206cbbd22204c0cd49b85f8。
    - EVIDENCE_INDEX.json，24075 B，sha256 47d6d860adbafd74cd4ea1311380823d99c38d326e48810611e544387b5a36ed。
  - 证据索引共 105 条，base_dir 为 dps-air，不符 0 条：
    - 104 条的字节、权限、SHA-256 全部相符。其中 0644 的 93 条，0755 的 8 条；另有 3 条是门禁写出的 0600 文件，按 02:17:51Z 裁定只算了哈希。
    - 剩下 1 条是 QA 自己的 0600 文件 environment/logs/pg-server.log，只核了大小与权限，没有打开。
  - 门禁 run 9fca553c3935ba474ac993e28adb7313：
    - WORKSPACE_DIAGNOSTIC_ONLY，53 项，48 PASS、5 FAIL。5 项是 #5、#10、#17、#35、#46，都是 Phase0 既有项，没有新增 FAIL。
    - 基线 M20，head_commit_observed 为 M21，运行时间 03:56:36Z–04:03:01Z。
    - 53 个元组与 F2 的 run f98bfa74、K2 的 run 1fdb4894 逐位相同。
    - #5 共 63 行，集合与 F2 相同。#7 PASS，scope 34。#10 的 project_count 71，归一化后 13 行诊断与 K2 相同。
  - 套件：按 QA 报告，35 个套件 684 例全部通过，R1–R5 与预期一致，语义交互 (a)–(e) 都没有。总管没有重跑。
  - M21 未变：
    - Builder clone 的 HEAD 与分支都是 f5943bd41ac6d67eda9d1820dedb64cefb0229ee，tree 656556fd…，父依次为 F2、K2。
    - porcelain、stash、unmerged 都是 0。两份 diff 与预测逐字节相同。
    - 提交对象中 8 个 Owner 身份 needle 命中 0。FREEZE.json、REPORT.md 的哈希未变。
    - QA clone 的 HEAD 也是 M21，工作树干净，没有 M21 之外的提交。
  - 档位：
    - QA 自 START 送达后共 312 条 assistant 记录（03:32:15Z–04:40:51Z），都是 claude-opus-5-5，effort 与 perTurnEffort 都是 max。最后一条 permissionMode 为 bypassPermissions。进程 argv 为 --model claude-opus-5-5 --effort max，档位环境变量未设。
    - Builder 自 FREEZE 后只有 2 条 assistant 记录（03:25:08Z、03:25:17Z），claude-sonnet-5 / high，perTurnEffort 为 null。
  - 残留（04:44:47Z）：
    - postgres、dotnet、testhost、MSBuild、VBCSCompiler、release_candidate 进程 0 个；563xx 监听 0 个；/private/tmp 下 .s.PGSQL.* 0 个。
    - /private/tmp 下 32 位十六进制名的 socket 现共 1470 个，是各单元多日累积的，含本单元 Builder 的 9 个、QA 的 24 个。列入清理清单。
  - QA 会话的 harness 文件：
    - tool-results 1 个，30383 B，02:22:36Z 建立，属 PREACCEPT 阶段；QA 报告第 14 节没有列出。
    - tasks 2 个：bu481ktp7.output，99 B，报告已列出；bzyjq4yh5.output，1036 B，04:36:57Z 建立，晚于 QA 04:29:04Z 的收尾快照。
    - scratchpad 0 个。
    - 以上只记事实，不另评级。
  - Builder transcript 的 mtime 为 04:20:48Z，但其中最新的带时间戳记录仍是 03:25:17Z 的 turn_duration，没有新的对话记录。按 harness 写入处理，不算单元活动。
- 结论：
  - M21-INT 按 PARTIAL 收口。原因是 Phase0 既有 5 项 FAIL，口径与之前各单元相同。
  - 这不是任何 PDD 包的出口认定；PARTIAL 能否作为出口仍待 Owner 决定。
  - M21 = f5943bd41ac6d67eda9d1820dedb64cefb0229ee 成为本地集成头，之后的写单元以它为基线。
  - 正式等级仍为 NONE。没有向任何远端分支合入。
- 对 QA 发现的处置（总管裁定）：
  - F-1（高）成立。
    - 经过：co-pg 失败后原样重跑，前后共四次运行；失败日志被覆盖，environment/logs 被删；REPORT 与 FREEZE 的 D-1 披露不实。
    - M21 的内容不受影响：门禁与 QA 自己的套件都独立通过。
    - FREEZE、REPORT 不改，本条即为更正记录。
  - F-2（高）成立。
    - 经过：口令进入了 Builder 的 transcript（三次 cat）与 grep 的 argv。REPORT 第 6 节、FREEZE :110 与 :130 的陈述不实。总管核对时，也把含口令的一行带进了自己的 transcript（03:31:50Z 条已披露）。
    - 处置：该口令作废，Builder 的 PG 实例不得再启动。
    - 建议清理时销毁 Builder 的 environment/pg 与 environment/secrets。清理类仍待 Owner 决定。
  - F-3（中）成立。
    - Builder 在 /private/tmp 写的 9 个 socket 没有列出。
    - REPORT 说 ~/.dotnet 未被修改，但 QA 实测 ~/.dotnet/metadata 的 mtime 为 03:11:54Z，落在 Builder 运行 dotnet test 的时段内。
    - tool-results 的计数没有写；bv1e2ahof.output 已不存在，无法核实。
  - F-4（低）成立：自测日志没有命令、退出码与时刻；合并核对没有原始日志。
  - F-5（低）成立：instruction-input-hashes.json 的 recorded_at_utc 是编造的值。
  - QA 自己的偏离 D-1、D-2 已披露，都是只读操作，接受。
  - 观察 O-1 至 O-7 记录在案。O-5（产品 PG 套件用 initdb 超级用户运行，与 TE 的身份矩阵不一致）放进残留登记。
- 之后的工单一律加入以下条款，针对本单元暴露的问题：
  1. 套件失败后，没有做任何改动就不得重跑。每次修正后只重跑一次，结果如实记录。
  2. 日志只增不改：每次运行写新文件，不覆盖，不删除。复制进 Reports 后也保留原件。
  3. 口令：
     - 不得用 cat、head 等把口令文件打到终端。
     - 口令扫描用 `/usr/bin/grep -F -f <口令文件>`，口令不上命令行。
     - transcript 也算泄露落点。
  4. 每份自测日志开头写命令、退出码、开始与结束时刻，时刻都来自 `date -u`。
  5. 报告列出本会话 harness 的 tool-results、tasks、scratchpad 文件数与大小。/private/tmp socket 数与 ~/.dotnet/metadata 已是现有要求。
  6. 证据中的时间一律是 `date -u` 的实测值，不得预填，不得编造。
- 留底：
  - 两个会话都已空闲，最后一条带时间戳的记录都是 turn_duration。
  - 关 tab 会结束进程；transcript 保留在 ~/.claude/projects/-Users-younghu-Documents-ZennoDroid-DSP-dps-air/<session id>.jsonl。

| tab | 名称 | pid | session id | 档位 | 最后一轮结束 |
|---|---|---|---|---|---|
| c5 | M21-INT Builder | 54255 | 497f4bee-e731-4ea2-98d0-bb4ff2f9e0f3 | claude-sonnet-5 / high | 03:25:17Z |
| c6 | M21-INT QA | 54365 | f5a6b221-e4da-45b3-8929-454e262c55a6 | claude-opus-5-5 / max | 04:40:51Z |

- 恢复方法同 03:05:02Z 条，只换 session id、档位与名称。以 M21-INT QA 为例：
  - `cd /Users/younghu/Documents/ZennoDroid_DSP/dps-air && env -u CLAUDE_CODE_EFFORT_LEVEL claude --resume f5a6b221-e4da-45b3-8929-454e262c55a6 --model claude-opus-5-5 --effort max --permission-mode bypassPermissions -n "M21-INT QA" --remote-control "M21-INT QA"`
  - 恢复后先核对 model、effort、权限模式三项。
- 下一步（按 04:14:58Z 条的排程裁定）：M21-INT 已收口，可以开写单元。
  - 先派 WO-P08E：只写 PA，基线 M21。
  - 并行派 P05 补轮的只读摸底，以及 P03、P20 的只读准备。
- 自 Owner 03:36:52Z 输入以来，总管发出的跨会话消息为 0 条。

## 2026-09-29T04:48:09Z M21-INT 两个 tab 已关闭

- 04:47:53Z 前后关闭 c5、c6，即 M21-INT Builder、M21-INT QA。
- 04:48:00Z 核实：
  - pid 54255、54365 都已退出。
  - 两份 transcript 的大小与关闭前相同：Builder 2565419 B，QA 6006604 B。
  - postgres、dotnet、testhost、VBCSCompiler 进程 0 个。
- 恢复方法见本账本 04:47:40Z 条。终端 tab 现为 0 个。

## 2026-09-29T05:00:34Z 派发 WO-P08E；P08E 两个会话已开出；两个只读研究代理已派出

- 依据：04:14:58Z 条的排程裁定 2、3、5；04:47:40Z 条（M21-INT 已收口，可以开写单元）。
- 工单：coordination/WO-P08E.md，31576 B，sha256 f4b7606ce66cee43cce89b213ed4237f63e53fea83c118e6fdc68b0982030e00。要点：
  - 只写 PA，做两件事：把 kill_policy_approval 接入签名；签名时强制一个 approval 只对应一个 command_id。
  - 写域：签名器与至多一个新异常文件、PA 测试、PA 的文档，以及根 CHANGELOG（只追加）。
  - 基线 M21，候选 E1。端口 56380–56399。
  - CPH.integration 需要 Soul/Host 窗口，由总管在 START 中附条件分配。
  - 04:47:40Z 条的六条新纪律写进了工单第 3.2 节第 7 项。
  - 文档改动由 Builder 在 PREACCEPT 中逐字拟稿，START 只给验收口径，不代拟文字。这是对审计所指“START 越位”的改正，此后照此办理。
- 签发前，总管在 04:48:09Z–04:57:36Z 之间只读实测了工单第 1.1 节中标“实测”的条目，包括：
  - kill_policy_approval 的 6 处文字；签名器的两个构造点；
  - PA 的版本与四个消费方的区间；
  - CPH 自 17438fb 起未改，其测试进程链接 PA 的 src；M20 上 CPH 实测 12、27、16、75（M20-INT QA :122–125）。
- 会话：由终端面板 CLI 开出，进程于 04:59:09Z、04:59:11Z 启动。

| tab | 名称 | pid | session id | 档位 |
|---|---|---|---|---|
| c7 | P08E Builder | 94210 | 89be130a-0f0e-415d-ae6e-3df599ae4475 | claude-sonnet-5 / high |
| c8 | P08E QA | 94284 | e3b7792d-a34e-46ed-a2d0-8513706f4507 | claude-opus-5-5 / max |

- 首条消息（原文在总管 scratchpad）：
  - first-p08e-b.md：文件 6293 B，sha256 a11cc3fa0944b6f36e83b17d548cbbc17a55414e92142c2fd8c3e0e652ab4d3a；正文 6292 B，sha256 9fb933c51bb16fc1687a68b8d90596ed435bd5255877ca17cc9d56f40550e960。04:59:11.159Z 写入 Builder 的 transcript，逐字节一致。
  - first-p08e-qa.md：文件 7093 B，sha256 e5a97fed4d38d057ec907c06e5be3d06f348f8897e29a58c0836ea1760adbafa；正文 7092 B，sha256 dfc679a3827fbbc7eab4c82f0d92a1764e2bf63b89e0f3a946535f61bd91f2bd。04:59:13.853Z 写入 QA 的 transcript，逐字节一致。
  - Builder 的档位标准改为四项，perTurnEffort 为 null 或 high，吸收了 M21-INT Builder 的档位裁定（02:22:14Z）。
- 档位核对（04:59:37Z）：
  - Builder 6 条 assistant 记录都是 claude-sonnet-5，effort 为 high，perTurnEffort 为 null。
  - QA 6 条都是 claude-opus-5-5，effort 与 perTurnEffort 都是 max。
  - 两者 permissionMode 都是 bypassPermissions；进程 argv 与首条指令相符，档位环境变量未设。
- 只读研究代理：总管会话内的子代理，04:53:03Z 之前派出，结果回来后留档到 coordination/research/。
  1. P05 补轮摸底（排程裁定 3）：以 M21 为基线，核实三处测试缺陷与 P05 的剩余欠项，给出补轮范围与并行条件。
  2. P03、P20 提请准备（排程裁定 5）：按 PDD 第 348 行的口径列缺项，按第 200 行的六要素格式起草两份给 Owner 的请求。
- 终端 tab 现为 2 个：c7、c8。
- 自 Owner 03:36:52Z 输入以来，总管发出的跨会话消息为 0 条。CLI 的首条消息是启动参数，不是跨会话发送。
