# M19-INT Builder 自测报告

- 单元：M19-INT（FIX 定稿 F 与 P04F 定稿 X 合流为 M19）。工单 `coordination/WO-M19-INT-FIX-P04F.md`（efd210f8…）。
- 依据：
  - PREACCEPT `units/m19-int/builder/PREACCEPT-BUILDER.md`（6a4febf3…）；
  - 总管 START（2026-09-27T15:46:47Z）。
- Builder session：`local_f7ab99e5-a6df-4025-a4d1-f1a467633735`。
- 冻结：2026-09-27T16:00Z。
- 路径：以下均相对于 clone `units/m19-int/builder/DSP_ZD`，证据在 `Reports/ci/m19-int-builder/`（`.gitignore:10` 忽略）。`<E>` = `units/m19-int/builder/environment`。
- 逐文件 sha256 见 `EVIDENCE_SHA256SUMS.txt`（含 `<E>/run/` 下的脚本；不含本文件和 FREEZE.json）。
- 口径：正式等级 NONE；候选 unsigned、level=null、non-issuer；外审 NOT_RUN（Owner 2026-09-27 暂停外审）。不合并、不发布、不激活、不签发证据。本报告是开发自测，不替代独立 QA。

## 1. 候选

| 项 | 值 |
|---|---|
| commit M19 | df120166469e5044977227798932c07808b87571 |
| tree | 963928ac1fffbd9bf8d50413014fe28d73a8c54e |
| 父（依次） | L = F 6ba8ca001e011c2c9b3be87cd52fed114909e653；R = X 0b72b21dfcb0462e6893b7e4c555973ef68211a2 |
| merge-base | M18 199b2097257d88112091ad2dd381c90345eedc8a |
| 分支 | codex/int-fix-p04f-199b209（从 L 起） |
| author / committer | M19-INT Builder <m19-int-builder@local.invalid>（两者相同） |
| 父提交的身份 | Owner 身份（L、R、M18，已冻结不改，本文不写出） |
| diff L..M19 | 95412 B，sha256 8f66d710d075eaba27817faa7c6b0ddb4cbfd43f098cf997a7cb63309a55fa8d（`freeze-diff-6ba8ca0-df12016.patch`） |
| diff R..M19 | 14630 B，sha256 de45a7354f9441bbfda98f37616ecc204893c71f329638aa23bfe6ecdd51c32e（`freeze-diff-0b72b21-df12016.patch`） |

- diff 的 argv：`git diff --binary --full-index --no-renames --no-color --no-ext-diff --no-textconv <parent> df12016…`。环境：`env -i GIT_CONFIG_GLOBAL=/dev/null GIT_CONFIG_NOSYSTEM=1 PATH=/usr/bin:/bin`。
- 冻结时工作树干净（porcelain 为空）。被忽略的只有：`.venv/`、`Reports/`，以及 3 个 `__pycache__`（`Tools/ci`、`Tests/ci`、ledger `src`，见第 6 节第 5 项）。

## 2. 合并与 CHANGELOG 的解决

**过程**

- b02：`git merge --no-ff --no-commit R` 的 rc 为 1。只有 `CHANGELOG.md` 冲突（UU，stage 1/2/3 分别为 ac72c5f1/5e209fce/32a21c20），与 PREACCEPT 第 3.2 节的预判一致。其余 13 个 R 路径都由 git 自动暂存。
- b03：用 `<E>/run/changelog.py` 从三个 blob 拼出 CHANGELOG。拼法是 M18 前 4 行，接 L 插入的 531 B，接 R 插入的 569 B，接集成说明 295 B，最后接 M18 其余全部行。
  - 脚本断言：M18/L/R 的头尾一致；去掉 R 插入等于 L；去掉 L 插入等于 R。
  - 文件中没有冲突标记。
  - 然后 `git add`，`git diff --cached --check` rc 0，再提交。
- 结果：blob 303b0436…，97491 B，sha256 eef056d5…。
- L..M19 与 R..M19 的 CHANGELOG diff 都记在 b04。结果是：L 侧条目在前，R 侧条目在后，集成说明在两者之后（START 第 2 项裁定），M18 原有文字不变。

**集成说明全文**

```
### M19 Factory FIX and P04F integration

- This merge integrates only the frozen FIX-M16-M26 (`6ba8ca0`) and P04F (`0b72b21`) candidates, both based on `199b209`; both sides' changelog entries are kept verbatim.
- It does not change product behavior.
- It authorizes no release or activation.
```

**逐路径核对**（b04，`<E>/run/b04.py`）
- L 改 7 个路径，R 改 14 个，重叠只有 CHANGELOG.md。
- M19 共 1170 个路径。除 CHANGELOG.md 外，每个路径都等于改动它的那一侧的 blob（没被改的等于 M18），不符为 0。
- M18..M19 改动 20 个路径，恰好等于 L 与 R 改动的并集。
- 没有使用 `generate_governance.py --write`，因为校验通过。

## 3. 自测结果

- 命令逐字取自 M19 的 module.yaml：`<E>/run/suites.py` 与 `pgsuites.py` 按 suite id 读取 `tests.suites[].command`，并断言 `required=true`。
- 在 clone 根目录以 `/bin/sh -c` 执行。环境为 `env` 字典：
  - `PATH=<clone>/.venv/bin:/usr/bin:/bin`，HOME 与 TMPDIR 在 `<E>` 下，`LANG`/`LC_ALL=C`，`PYTHONDONTWRITEBYTECODE=1`；
  - PG 套件另加 manifest 规定的 DPS_* 变量。
- 解释器：clone 内 `.venv`，CPython 3.12.13，`pip install --require-hashes -r requirements-ci.txt` rc 0（b05）。
- 每个 suite 只跑了一次。日志在 `suites/` 与 `pg-suites/`，`summary.tsv` 中有退出码、数量与日志 sha256。
- 所有日志中都没有 skip、expected failure 或 unexpected success。

| suite | rc | Ran | 结果 | 两父较大值（出处） |
|---|---|---|---|---|
| factory-evidence-ledger.unit | 0 | 11 | OK | 11（FIX FREEZE/QA） |
| factory-evidence-ledger.crash-recovery | 0 | 7 | OK | 7 |
| factory-evidence-ledger.contract | 0 | 7 | OK | 7 |
| **factory-evidence-ledger.postgresql18（M26）** | 0 | 6 | OK | 6（FIX QA q21） |
| factory-artifact-builder.unit | 0 | 35 | OK | 35 |
| factory-artifact-builder.contract | 0 | 6 | OK | 6 |
| **factory-artifact-builder.postgresql18（M16）** | 0 | 8 | OK | 8（FIX QA q20） |
| factory-control-plane-host.unit / contract / simulation | 0 | 76 / 9 / 2 | OK | 76 / 9 / 2（两父相同） |
| factory-release-controller.unit / candidate-gate / contract / simulation | 0 | 19 / 18 / 14 / 2 | OK | 19 / 18 / 14 / 2 |
| factory-rollback-controller.unit / contract | 0 | 12 / 4 | OK | 12 / 4 |
| factory-merge-controller.unit / contract | 0 | 15 / 5 | OK | 15 / 5 |
| factory-upgrade-intake.unit / contract | 0 | 16 / 10 | OK | 16 / 10（P04F Builder s10、QA 第 9 项） |
| factory-instruction-resolver.unit / contract | 0 | 43 / 20 | OK | 43 / 20 |
| factory-impact-analyzer.unit / contract | 0 | 12 / 11 | OK | 12 / 11 |
| factory-worktree-manager.unit / contract / git-worktree | 0 | 9 / 4 / 4 | OK | 9 / 4 / 4 |
| factory-trusted-runner.unit / contract | 0 | 44 / 6 | OK | 44 / 6 |

- 29 个 suite 全部 PASS。每个的数量都**等于**两父中的较大值，没有更少，也没有更多。M16 ≥8、M26 ≥6 都满足（START 第 10 项）。
- **NOT_RUN**：
  - `factory-control-plane-host.postgresql18`（M12）与 `factory-worktree-manager.postgresql18`：两父都没有跑过，按工单第 3.2 节第 3 项不跑。
  - 这两项是 required 的非 PASS 结果，如实保留。
- **中央 CI**（b08）：`python3.12 -m unittest discover -s Tests/ci -p 'test_*.py'`，rc 0，Ran 261，OK。与 L 的 261 相同；R 没有跑过。运行前后 HEAD 都是 M19，porcelain 都为空。
- **静态**（b06，HEAD 为 M19，porcelain 为空）：
  - `generate_governance.py` 输出 `governance snapshots verified: 3`，rc 0。与 L、R 相同。
  - `validate_repo.py` 输出 `validated json=299 python=96 markdown=247`，rc 0。json 数等于 R（L 为 297 加上 R 的两份 corpus）。
- **git diff --check**（b03、b04）：`--cached` rc 0；M18..L、M18..R、L..M19、R..M19 都是 rc 0。

## 4. PostgreSQL 18.4

- **源码**（b09、b09b）：
  - 只读复制 `units/p17-int/builder/environment/pg/src/postgresql-18.4.tar.bz2` 与 `.sha256`，只读了这两个文件；
  - sha256 为 81a81ec6…，与工单一致，`shasum -c` OK。
- **编译**：`configure --prefix=<E>/pg/install --with-openssl --without-icu --with-includes=/opt/homebrew/opt/openssl@3/include --with-libraries=/opt/homebrew/opt/openssl@3/lib`（openssl 3.6.3），然后 make、install，以及 pgcrypto install，rc 都为 0，版本 18.4。
- **实例**（b10）：
  - initdb 用 scram-sha-256，superuser 口令经 0600 的 pwfile 传入；
  - `listen_addresses='127.0.0.1'`，端口 56360（启动前实测 56360–56369 无监听）；
  - `unix_socket_directories=''`（实测为空，即关闭）；
  - pg_hba 只有 `host all all 127.0.0.1/32 scram-sha-256` 一行。
- **身份**（6 个，全部 TEST_ONLY、口令随机、SCRAM）：
  - `m19_super`（initdb 超级用户）；
  - `m19_ledger_migrator`（LOGIN SUPERUSER NOINHERIT，见第 6 节第 3 项）；
  - `dps_factory_evidence_admin`、`dps_factory_evidence_runtime`（LOGIN NOINHERIT，无特权）；
  - `m19_artifact_admin`（artifact 库的属主，无特权）；
  - `m19_artifact_runtime`（LOGIN NOINHERIT，无特权）。
- **库**：`m19_ledger`（属主为 migrator）、`m19_artifact`（属主为 artifact admin），都是新建的空库。
- 连接自检：6 个身份都返回 180004、127.0.0.1:56360，current_user 与 session_user 相同。
- **运行**：15:58:41Z 启动，15:59:06Z 以 fast 模式停止（b11）。之后 56360–56369 无监听，也没有 postgres 进程。

## 5. 口令与密钥

- 口令：
  - 用 `secrets.token_urlsafe(32)` 生成，只存在 `<E>/secrets/pgpass`（0600）。另有 superuser.pw 与 `.pw.json`，同样是 0600，放在 secrets 内；
  - 连接 URI 形如 `postgresql://<role>@127.0.0.1:56360/<db>?passfile=<E>/secrets/pgpass&sslmode=disable`，**URI 中不含口令**；psql 另用 `PGPASSFILE`；
  - 建角色时，`SET log_statement='none'` 之后，经 psql 的 stdin 传入口令。
- HMAC key（32 字节，b64）与 epoch（随机，≥2）各存一个 0600 文件。由 `pgsuites.py` 读入后，只放进 M26 子进程的 env 字典。日志只记录变量名，不记录值。
- 泄漏扫描（b12，`/usr/bin/grep -rlaF -D skip -f <needles>`）：
  - needle 共 10 个：6 个口令，加上 key 的 b64、hex、urlsafe 两种写法；
  - 阳性对照命中 1 个，之后删除；
  - 扫描范围：整个 clone（含 `.git`、Reports、`.venv`）、回执，以及 environment 中除 secrets 外的全部目录（含 pg 数据目录）；
  - 结果 **0 命中**。提交信息 0 命中。
- 保留物（TEST_ONLY，等总管统一清理）：
  - `<E>/secrets/`（pgpass、superuser.pw、.pw.json、hmac_key.b64、epoch、needles）；
  - 已停机的 `<E>/pg/data`；
  - `<E>/pg/` 下的 install、build、src；
  - clone 内 `.venv`；
  - `<E>/home` 中的 pip 缓存。

## 6. 偏离与披露

1. **调用错误 1（clone，b01 attempt0）**：第一次把 git 前缀放在 zsh 变量里，zsh 不按空格拆分变量，整段命令以 rc 127 失败，git 一条都没有执行。原始日志保留为 `b01-clone.attempt0-zsh-wordsplit.log`。同一 shell 里的一条 `ls .git/objects/info/` 在任务卡 worktree 中执行了，只读，报 “No such file”。之后改用 bash 脚本（`<E>/run/*.sh`）重做，见 `b01-clone.log`。
2. **环境问题（PG 编译，b09）**：在 `env -i` 的最小 PATH 下，configure 找不到 libcrypto，rc 1，后续 make 全部未执行。这没有执行到候选，原日志保留。之后按回执第 5.1 节的编译步骤，显式加上 `--with-includes`/`--with-libraries` 指向 Homebrew 的 openssl@3，并重新核对 tarball，重建成功（b09b）。另外，b09b 末尾的 `ls .../lib/pgcrypto*` 路径写错了；实际安装位置是 `lib/postgresql/pgcrypto.dylib`，见 install 输出。
3. **ledger migrator 为 SUPERUSER**：
   - 原因：迁移 002 要 `CREATE ROLE`、`ALTER ROLE`、`CREATE EXTENSION pgcrypto`，还要把对象属主改给 `dps_factory_evidence_owner`；而套件断言 `dps_factory_evidence_%` 不能有任何 role membership。用 CREATEROLE 的非超级用户建角色，在 PG 16 以上会自动产生一条 membership，所以 migrator 只能用超级用户。
   - 效果：DML 与 TRUNCATE 触发器对超级用户同样生效，“migration 身份不能静默篡改”的用例照常通过。
   - admin、runtime 以及 artifact 的两个身份都没有特权。
   - FIX 当时 migrator 的具体属性，本单元无从得知（其 environment 不可读）。
4. **/private/tmp 快照**：开工时 768 项，收尾时新增 20 项、少了 1 项（`dps-p05-05daff9cb6aa`）。新增的是 18 个 32 位十六进制名的 socket 和 2 个 `dps-phase0-executor-*` 目录，创建时间在 15:53Z–15:59Z。同一时段 U5 QA 的累计门禁在运行（pid 29027，15:52:55Z 起，`units/u5-int/qa/DSP_ZD/.venv/bin/python Tools/ci/run_candidate_gate.py --level integration …`）。本单元的 PG 没有 socket，测试的 TMPDIR 指向 `<E>/tmp`，也没有运行 dotnet 或门禁。所以这些变化归属 U5 的门禁，不归本单元。本单元没有删除或改动 /private/tmp 下的任何条目。**另需知悉**：本单元 PG 在 15:58:41Z–15:59:06Z 运行于 56360，与 U5 门禁的运行时段重叠，端口不在 U5 的段内。
5. **被忽略的 `__pycache__`**：`Tools/ci`、`Tests/ci` 与 ledger `src` 下共有 3 个目录。我设置了 `PYTHONDONTWRITEBYTECODE=1`，但中央 CI 及部分测试的子进程用干净环境重新启动解释器，因此仍会写入。这与 FIX QA 记录的现象相同。它们被忽略，不影响 porcelain，本单元没有删除。
6. **任务卡 worktree**（DSP_ZD）：没有写入。
7. **单元根外的读取**：
   - `history.git`（clone，得到空仓库）；
   - FIX 与 P04F Builder clone：只读 fetch 各自的分支；
   - p18a-int Builder clone 的 git 对象（PREACCEPT 轮已读）；
   - p17-int 的 PG 源码包及 `.sha256`；
   - `/opt/homebrew/opt/openssl@3`（编译时的头文件与库）、`~/.local/bin/python3.12`（uv 管理的 3.12.13）、系统工具；
   - PyPI 下载（`--require-hashes`）；
   - coordination 下的文件、交接包，以及父线的报告与日志（PREACCEPT 轮）；
   - 为判断 /private/tmp 变化的归属，只读列出了其顶层条目及 stat，并用 ps 查看了门禁进程。
8. **单元根外的写入**：Claude Code 自身的 transcript 与会话目录；一个后台检索任务的输出文件（位于会话 tasks 目录，是 find openssl 的检索，已终止）。此外没有写入。

## 7. 观察（不改动，供 QA 第 5 项参考）

- 技术书第 373 行仍写 `factory-evidence-ledger` 为 0.1.0，而 M19 的 Manifest 是 0.2.1（M18 上是 0.2.0，本来就过期）。第 372 行 artifact-builder 写的 0.1.0，同样与 Manifest 的 0.2.1 不符。这些都不是 Impact 行，按 START 第 7 项不改。
- 初步只读判断：X 的 owner 测试只读取仓库中的 schema 字节与 Intake 的 src，不依赖 L 改过的任何文件。29 个 suite 在合并后全部通过，没有出现合并后才有的失败。

## 8. 档位

| 轮次 | assistant 记录数 | model / perTurnEffort |
|---|---|---|
| 第一轮（档位自检） | 8 | claude-opus-5-5 / max（卡片继承） |
| 第二轮（PREACCEPT） | 75 | claude-opus-5-5 / medium |
| 第三轮（START 至今） | 全部（冻结时 76 条） | claude-opus-5-5 / medium |

- permissionMode 始终为 bypassPermissions（transcript 第 5、62、335 行）。
- `CLAUDE_CODE_EFFORT_LEVEL` 在 15:00:51Z 与 15:47:04Z 两次检查时都未设置。
