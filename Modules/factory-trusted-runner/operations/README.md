# Operations

`trusted-runner-policy.v1.json` is a non-production candidate template. Deployment requires a separately approved Runner artifact, policy digest, currentness providers, RSA signer and downstream public-key trust store. Repository content alone cannot authorize execution or install a trusted key. The constructor's existing process-bound verifiers provide auth, receipt, commit/workspace and fencing facts. A deployment must bind those ports to independent current facts; synthetic test callbacks are not deployed providers.

The entrypoint `python3.12 Modules/factory-trusted-runner/src/cli.py` now exits `2` with `WAITING_EXTERNAL` before opening input/signing files or executing a test. Its old six-file format under `DPS_RUNNER_INPUT_DIR` only authenticates static HMAC bytes; it cannot establish post-execution currentness. Even a valid old HMAC cannot issue a result. Restoring CLI execution requires separately bound independent currentness support. No new provider service or wire protocol is installed by this change.

The in-process Runner freezes request, plan, receipt, lease and policy values. After the controlled process lifecycle ends and output is frozen, it rechecks current auth/receipt/fencing facts, lease expiry, plan, exact Git HEAD, Manifest and the complete workspace file set. Workspace hashing still includes untracked files and excludes only `.git`; a private identity snapshot also detects directory/file replacement. Manifest parsing and hashing use the same bytes from one no-follow descriptor read, with file and parent identity checks. A changed protected input, unsafe read, clock rollback, expired lease (including equality), revoked fact or unresolved cleanup prevents every issuer call.

Workspace traversal explicitly enumerates directory descriptors opened without following symlinks. Directory open, iteration, classification or identity-read errors reject the entire snapshot before issuance; a searchable but unlistable directory cannot contribute a partial digest. Directory and file identities are checked during traversal. File records retain their global relative-path ordering, and only the top-level `.git` entry is excluded; nested directories with that name remain protected. Real permission fixtures restore their original modes in `finally` and record both pre-execution rejection and post-execution rejection with zero issuer calls. The prior R2 candidate and its BLOCKED QA report remain historical evidence; this correction requires a new frozen candidate and independent QA.

Each POSIX command starts a new session. Cleanup always performs a bounded leader wait. A still-running, unreaped leader reserves its PID for any necessary group termination; after reaping, only group existence is observed. Normally exiting descendants may finish within the five-second cleanup bound. Signal 0 EPERM remains an UNKNOWN observation within that same deadline; it is never absence. Only an actual ESRCH before the deadline resolves group absence and permanently ends signals to that numeric group. Nonzero termination-signal errors remain terminal failures, even if the group later disappears. An unresolved group or expired cleanup deadline prevents issuance. Descendant time counts toward the command timeout, and descendant output is frozen before postchecks. This is a local POSIX lifecycle bound: escaped sessions, unrelated same-UID writers and transient restored mutations are not contained or fully observed. Windows process-tree control is unverified and execution is refused there. No production containment or P03 claim follows.

Commands retain exact argv, `shell=False`, the existing environment allowlist and bounded output. The supervisor must place HOME/TMPDIR and diagnostic output outside the candidate workspace. The Runner retains `PYTHONDONTWRITEBYTECODE=1`; this change adds neither a compileall policy template nor `PYTHONPYCACHEPREFIX` to its allowed environment. The outer developer compileall check uses its own external cache.

After every integrity/currentness/cleanup check succeeds, results retain `trusted.test.result/v1`, its identity and digest fields and the existing RSA-PSS payload/domain/salt behavior. Ordinary FAIL/SKIP and timeout/output-limit INFRA_ERROR may retain signed failure diagnostics with `release_allowed=false`. Local crypto tests generate temporary synthetic RSA keys, validate the real result Schema, verify the signature with OpenSSL and reject payload/signature tampering. These tests do not deploy a signer or confer evidence eligibility. Merge request signature coverage remains separate P17B work; consumer implementations and public contracts are unchanged.

`factory_disable_trusted_execution` stops new processes. Rollback routes to the previous signed runner and preserves emitted hashes.
# Complete merge request issuance

`TrustedRunner.issue_merge_request(metadata, run_inputs, policy)` accepts a
finite metadata proposal and one work item per required check in the trusted
Runner policy. Each item contains only `request`, `worktree_plan`,
`instruction_receipt` and `lease`. Evidence comes exclusively from sequential
internal `run` calls. Caller evidence, attestations, required sets and roles
are forbidden. The producer fixes schema, contract, producer and trusted time.
Metadata supplies the remaining owner-contract fields, including exact receipt
summaries bound to the complete BOUND run receipts by ID, hash, status and diff.

The sole new constructor port is optional `merge_request_verifier(request,
runner_identity)`. Without it, issuance returns WAITING_EXTERNAL by raising
`TrustedRunnerError` before any run or signer call. The trusted composition
must supply a provider that independently authenticates and refreshes complete
scope, authorized integration commit and branch relationships, diff and paths,
conflicts, complete current receipts, Merge policy, allowed issuer and separate
roles, recognized Runner policy, entire required set, expiry and revocation.
Unprovable facts must fail. Hashing and returning True is not authentication.
There is no production provider in this slice; all providers in tests are
TEST_ONLY. JSON cannot select or install this capability. The CLI remains
WAITING_EXTERNAL before reading input or signing keys.

The provider receives copies of identical complete frozen unsigned request
bytes at PRE and POST. Its existing verified-fact response must bind
`verified is True`, exact `fact_id == merge_request_id` and `fact_sha256` equal
to SHA-256 of canonical unsigned bytes without the signature domain prefix.
Runner independently computes that hash and checks the callback copy unchanged.
Extra returned fields grant no authority. Provider exceptions fail closed.

A nonblocking instance lock rejects reentry and concurrent issuance and is
released in finally. Each call freezes metadata, all run inputs and policy,
records initial auth, receipts, plans, Manifest, HEAD, workspace identities,
leases, fences and time, runs the complete batch, then rechecks every item.
Required results must be PASS, required and release-allowed and match request
scope, integration commit, Runner policy and required set. Full batch checks
run after PRE, and again after the signer and POST before returning a copy.
Lease time is `acquired_at <= now < expires_at`; equality at expiry and clock
regression fail. Result signing counts remain distinct from request signing:
PRE failure signs zero requests; POST failure can leave one signed request
internally but returns none. Historical result issuance retains its diagnostic
semantics.

The owner profile is in Merge's provided `merge.request.v1.schema.json`:
prefix `b"dps-merge-request-attestation/v1\n"` plus the complete request minus
only its own top-level attestation, including all embedded attestations.
Canonical JSON uses UTF-8, sorted keys, compact separators, Unicode text and
finite numbers; array order is preserved. Both digest and RSA-PSS signature
bind prefix-included bytes. OpenSSL explicitly uses SHA-256, MGF1-SHA-256 and
32-byte salt. No runtime import of Merge private implementation is introduced.
All four existing test files read the shared owner corpus. A decoded Mapping
cannot recover duplicate keys; the product rejects unsupported raw strings
and non-finite Mapping values without claiming a strict upstream raw parser.

Synchronous callbacks are not preemptibly cancelled. Production bounded I/O
and trust remain external requirements. Rechecks do not provide same-UID
atomic isolation, transient change-and-restore detection or global anti-replay.
Failure handling stops new execution, candidate evidence consumption and merge,
preserves history, and never rolls back to the known partial signer. Local
synthetic tests do not authorize bootstrap, production trust, deployment,
protected approval, Windows or device operation.
