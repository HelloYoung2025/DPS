import base64
import copy
import contextlib
import datetime as dt
import hashlib
import hmac
import importlib.util
import io
import json
import os
import re
import shutil
import shlex
import subprocess
import sys
import tempfile
import time
import unittest
from pathlib import Path
from unittest import mock


SOURCE = Path(__file__).parents[1] / "src" / "trusted_runner.py"
SPEC = importlib.util.spec_from_file_location("factory_trusted_runner", SOURCE)
MODULE = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)
sys.modules["trusted_runner"] = MODULE

CLI_SOURCE = Path(__file__).parents[1] / "src" / "cli.py"
CLI_SPEC = importlib.util.spec_from_file_location("factory_trusted_runner_cli", CLI_SOURCE)
CLI = importlib.util.module_from_spec(CLI_SPEC)
sys.modules[CLI_SPEC.name] = CLI
CLI_SPEC.loader.exec_module(CLI)

TrustedRunner = MODULE.TrustedRunner
TrustedRunnerError = MODULE.TrustedRunnerError
TrustedRunnerPolicy = MODULE.TrustedRunnerPolicy
canonical_bytes = MODULE.canonical_bytes
sha256_bytes = MODULE.sha256_bytes
workspace_sha256 = MODULE.workspace_sha256


ZERO = "0" * 64
COMMIT_ZERO = "0" * 40


class RunnerFixture:
    def __init__(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.module_root = self.root / "Modules" / "demo"
        self.tests = self.module_root / "tests"
        self.tests.mkdir(parents=True)
        self.test_file = self.tests / "test_fixture.py"
        self.test_file.write_text(
            "import unittest\nclass T(unittest.TestCase):\n    def test_ok(self): self.assertTrue(True)\n",
            encoding="utf-8",
        )
        self.argv = [
            sys.executable, "-m", "unittest", "discover", "-s",
            "Modules/demo/tests", "-p", "test_*.py",
        ]
        self.manifest = {
            "tests": {
                "suites": [{
                    "id": "demo.unit", "type": "unit", "required": True,
                    "command": shlex.join(self.argv),
                    "environment": "synthetic",
                    "evidenceLevel": "REPOSITORY_STATIC_VERIFIED",
                }]
            }
        }
        self.manifest_path = self.module_root / "module.yaml"
        self._write_manifest()
        self._git("init", "-q")
        self._git("add", ".")
        self._git("-c", "user.name=DPS Test", "-c", "user.email=dps@example.invalid", "commit", "-qm", "fixture")
        self.commit = self._git("rev-parse", "HEAD").strip()
        self.policy_document = self._policy_document()
        self.policy_digest = sha256_bytes(self.policy_document)
        self.policy = TrustedRunnerPolicy.from_verified_document(
            self.policy_document,
            self.policy_digest,
            lambda _document, digest: self.fact(digest, digest),
        )
        self.request = {
            "request_id": "request:12345678", "check_id": "demo.unit",
            "module_id": "demo", "worktree_plan_id": "worktree:" + "1" * 32,
            "instruction_receipt_id": "instruction:" + "2" * 32,
            "auth_context_id": "auth-context-12345678", "soul_id": None,
            "device_binding_id": None, "platform_account_id": None,
            "trace_id": "trace_" + "1" * 32, "idempotency_key": "idem_" + "2" * 64,
            "occurred_at": "2026-07-14T00:00:00Z",
        }
        self.plan = {
            "contract_id": "worktree.plan/v1", "producer_module": "factory-worktree-manager",
            "plan_id": self.request["worktree_plan_id"],
            "instruction_receipt_id": self.request["instruction_receipt_id"],
            "trusted_policy_sha256": self.policy_digest,
            "soul_id": None, "device_binding_id": None, "platform_account_id": None,
            "entries": [{
                "module_id": "demo", "writer_identity": "implementer-1",
                "lease_keys": ["module:demo", "path:Modules/demo"],
            }],
        }
        self.receipt = self._receipt()
        self.lease = {
            "contract_id": "worktree.lease/v1", "producer_module": "factory-worktree-manager",
            "lease_id": "lease:" + "3" * 32, "plan_id": self.plan["plan_id"],
            "status": "ACTIVE", "lock_tokens": {"module:demo": 4, "path:Modules/demo": 9},
            "fencing_token": 9,
            "acquired_at": (dt.datetime.now(dt.timezone.utc) - dt.timedelta(seconds=1)).isoformat().replace("+00:00", "Z"),
            "expires_at": (dt.datetime.now(dt.timezone.utc) + dt.timedelta(minutes=5)).isoformat().replace("+00:00", "Z"),
        }
        self.runner = self._runner()
        self.processes = {}
        popen, killpg = subprocess.Popen, os.killpg

        def spawn(*args, **kwargs):
            process = popen(*args, **kwargs)
            if kwargs.get("start_new_session"):
                record = {"pid": process.pid, "pgid": process.pid, "signals": [],
                          "spawned_at": dt.datetime.now(dt.timezone.utc).isoformat()}
                try:
                    record["observed_pgid"] = os.getpgid(process.pid)
                except OSError as exc:
                    record["identity_errno"] = exc.errno
                self.processes[process.pid] = (process, record)
                print("P17A_PROCESS_SPAWN " + json.dumps(record, sort_keys=True), flush=True)
            return process

        def signal_group(pgid, number):
            event = {"signal": int(number), "utc": dt.datetime.now(dt.timezone.utc).isoformat(), "errno": 0}
            try:
                return killpg(pgid, number)
            except OSError as exc:
                event["errno"] = exc.errno
                raise
            finally:
                if pgid in self.processes:
                    self.processes[pgid][1]["signals"].append(event)

        self.spawn_patch = mock.patch.object(subprocess, "Popen", new=spawn)
        self.signal_patch = mock.patch.object(os, "killpg", new=signal_group)
        self.spawn_patch.start()
        self.signal_patch.start()

    def close(self):
        self.signal_patch.stop()
        self.spawn_patch.stop()
        unknown = []
        for process, record in self.processes.values():
            record["native_exit"] = process.returncode
            record["group_absent"] = bool(record["signals"] and record["signals"][-1]["errno"] == 3)
            print("P17A_PROCESS_END " + json.dumps(record, sort_keys=True), flush=True)
            if process.returncode is None or not record["group_absent"] or record.get("observed_pgid") != process.pid:
                unknown.append(process.pid)
        if unknown:
            self.temp._finalizer.detach()
            raise KeyboardInterrupt("unresolved registered fixture groups: " + repr(unknown))
        self.temp.cleanup()

    def _git(self, *args):
        completed = subprocess.run(
            ["git", "-C", str(self.root), *args], check=True,
            stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True,
        )
        return completed.stdout

    def _write_manifest(self):
        self.manifest_path.write_text(json.dumps(self.manifest, sort_keys=True), encoding="utf-8")

    def _policy_document(self, **overrides):
        policy = {
            "schema_version": "dps.trusted-runner-policy/v1", "policy_id": "policy-1",
            "status": "non-production-template", "runner_identity": "runner-service",
            "allowed_executables": [Path(sys.executable).name],
            "allowed_environment": ["HOME", "PATH", "PYTHONUTF8", "TMPDIR"],
            "maximum_output_bytes": 200000,
            "roles": {
                "implementers": ["implementer-1"],
                "evidence_issuers": ["runner-service"],
                "release_approvers": ["human-approver"],
            },
            "checks": [{
                "check_id": "demo.unit", "module_id": "demo", "argv": self.argv,
                "cwd": ".", "timeout_seconds": 10, "required": True,
                "success_marker": "OK",
                "forbidden_output_markers": ["FAILED", "SKIP", "skipped=", "PARTIAL", "NOT_RUN", "INFRA_ERROR"],
            }],
        }
        policy.update(overrides)
        return canonical_bytes(policy)

    def _receipt(self):
        return {
            "contract_id": "instruction.receipt/v1",
            "producer_module": "factory-instruction-resolver",
            "receipt_id": self.request["instruction_receipt_id"],
            "auth_context_id": self.request["auth_context_id"],
            "status": "BOUND", "invalidated_reason": None, "scope": ["demo"],
            "soul_id": None, "device_binding_id": None, "platform_account_id": None,
            "manifests": [{
                "path": "Modules/demo/module.yaml",
                "sha256": sha256_bytes(self.manifest_path.read_bytes()),
            }],
        }

    @staticmethod
    def fact(fact_id, digest, **values):
        return {"verified": True, "fact_id": fact_id, "fact_sha256": digest, **values}

    def _runner(self, **overrides):
        callbacks = {
            "auth_context_verifier": lambda auth_id, _runner: self.fact(auth_id, "a" * 64),
            "receipt_verifier": lambda receipt: self.fact(
                receipt["receipt_id"], sha256_bytes(canonical_bytes(receipt))
            ),
            "commit_workspace_verifier": lambda _root, commit, workspace: self.fact(
                self.plan["plan_id"], "b" * 64, commit=commit, workspace_sha256=workspace
            ),
            "fence_verifier": lambda lease_id, tokens: self.fact(
                lease_id, sha256_bytes(canonical_bytes(self.lease)),
                plan_id=self.plan["plan_id"], lock_tokens=tokens, fencing_token=9,
            ),
            "attestation_issuer": lambda payload, runner, _policy: {
                "algorithm": "rsa-pss-sha256", "key_id": "runner-key-1",
                "signer_identity": runner, "payload_sha256": sha256_bytes(payload),
                "signature_value": "A" * 128,
            },
        }
        callbacks.update(overrides)
        return TrustedRunner(self.root, self.root, "runner-service", **callbacks)

    def run(self):
        return self.runner.run(self.request, self.plan, self.receipt, self.lease, self.policy)

    @contextlib.contextmanager
    def real_signer(self):
        """Ephemeral synthetic keys, never a deployed signer or trust root."""
        openssl = shutil.which("openssl")
        if openssl is None or Path(openssl).is_symlink():
            raise RuntimeError("the fixture requires a bound real OpenSSL binary")
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            key, public = root / "runner-key.pem", root / "runner-public.pem"
            subprocess.run([openssl, "genrsa", "-out", str(key), "2048"], check=True,
                           stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=15)
            key.chmod(0o600)
            subprocess.run([openssl, "rsa", "-in", str(key), "-pubout", "-out", str(public)],
                           check=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=15)
            print("P17A_SYNTHETIC_KEYS " + json.dumps({"private_key_path": str(key), "public_key_path": str(public)}, sort_keys=True), flush=True)

            def verify(payload, signature):
                signature_path = root / "signature.bin"
                signature_path.write_bytes(signature)
                result = subprocess.run(
                    [openssl, "dgst", "-sha256", "-verify", str(public), "-signature", str(signature_path),
                     "-sigopt", "rsa_padding_mode:pss"], input=payload,
                    stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=15, check=False,
                )
                print("P17A_OPENSSL_VERIFY " + json.dumps({"native_exit": result.returncode, "payload_sha256": sha256_bytes(payload), "signature_sha256": sha256_bytes(signature)}, sort_keys=True), flush=True)
                return result.returncode

            environment = {"DPS_RUNNER_RSA_PRIVATE_KEY": str(key), "DPS_RUNNER_RSA_KEY_ID": "runner-key-1", "DPS_RUNNER_OPENSSL": openssl}
            with mock.patch.dict(os.environ, environment, clear=False):
                yield CLI._rsa_pss_issuer, verify


class TrustedRunnerTests(unittest.TestCase):
    def setUp(self):
        self.fx = RunnerFixture()

    def tearDown(self):
        self.fx.close()

    @contextlib.contextmanager
    def _after_execution(self, change):
        execute = self.fx.runner._execute
        completed = []

        def mutated(*args):
            result = execute(*args)
            completed.append(True)
            change()
            return result

        with mock.patch.object(self.fx.runner, "_execute", side_effect=mutated):
            yield
        self.assertEqual([True], completed, "the intended postcheck must be reached")

    def test_postcheck_rejects_tracked_and_untracked_runtime_writes(self):
        for target in ("Modules/demo/tests/test_fixture.py", "untracked-input.txt"):
            with self.subTest(target=target):
                original = self.fx.test_file.read_bytes()
                self.fx.test_file.write_text(
                    "import pathlib, unittest\nclass T(unittest.TestCase):\n"
                    "    def test_write(self): pathlib.Path(" + repr(target) + ").write_text('changed', encoding='utf-8')\n",
                    encoding="utf-8",
                )
                with mock.patch.object(self.fx.runner, "attestation_issuer", wraps=self.fx.runner.attestation_issuer) as issuer:
                    with self.assertRaises(TrustedRunnerError):
                        self.fx.run()
                    issuer.assert_not_called()
                self.fx.test_file.write_bytes(original)
                if target == "untracked-input.txt":
                    (self.fx.root / target).unlink(missing_ok=True)

    def test_workspace_unlistable_known_file_rejects_issuance(self):
        self.assertNotEqual(0, os.geteuid(), "the permission fixture requires an ordinary user")
        directory = self.fx.root / "search-only"
        directory.mkdir(mode=0o700)
        target = directory / "known.txt"
        target.write_text("before", encoding="utf-8")
        target.chmod(0o600)
        self.fx.test_file.write_text(
            "import pathlib, unittest\nclass T(unittest.TestCase):\n"
            "    def test_write(self): pathlib.Path('search-only/known.txt').write_text('changed', encoding='utf-8')\n",
            encoding="utf-8",
        )
        descriptor = os.open(directory, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW)
        original_mode = os.fstat(descriptor).st_mode & 0o7777
        try:
            os.fchmod(descriptor, 0o100)
            with self.assertRaises(PermissionError):
                with os.scandir(directory) as entries:
                    list(entries)
            with target.open("r+", encoding="utf-8") as stream:
                self.assertEqual("before", stream.read())
                stream.seek(0)
                stream.write("before")
            with mock.patch.object(self.fx.runner, "attestation_issuer", wraps=self.fx.runner.attestation_issuer) as issuer:
                error = None
                try:
                    self.fx.run()
                except TrustedRunnerError as exc:
                    error = exc
                finally:
                    print("P17A_PERMISSION_CASE " + json.dumps({
                        "phase": "pre", "enumeration_denied": True,
                        "known_file_after": target.read_text(encoding="utf-8"),
                        "issuer_calls": issuer.call_count, "run_rejected": error is not None,
                        "controlled_processes": len(self.fx.processes),
                    }, sort_keys=True), flush=True)
                self.assertIsNotNone(error, "incomplete workspace enumeration reached the issuer")
                issuer.assert_not_called()
                self.assertEqual("before", target.read_text(encoding="utf-8"))
                self.assertEqual({}, self.fx.processes)
        finally:
            try:
                os.fchmod(descriptor, original_mode)
                self.assertEqual(original_mode, os.fstat(descriptor).st_mode & 0o7777)
                print("P17A_PERMISSION_RESTORED " + json.dumps({"path": str(directory), "mode": original_mode}), flush=True)
            finally:
                os.close(descriptor)

    def test_workspace_postcheck_unlistable_directory_has_no_issuer(self):
        self.assertNotEqual(0, os.geteuid(), "the permission fixture requires an ordinary user")
        directory = self.fx.root / "post-search-only"
        directory.mkdir(mode=0o700)
        target = directory / "known.txt"
        target.write_text("before", encoding="utf-8")
        target.chmod(0o600)
        self.fx.test_file.write_text(
            "import os, pathlib, unittest\nclass T(unittest.TestCase):\n"
            "    def test_write(self):\n"
            "        pathlib.Path('post-search-only/known.txt').write_text('changed', encoding='utf-8')\n"
            "        os.chmod('post-search-only', 0o100)\n",
            encoding="utf-8",
        )
        descriptor = os.open(directory, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW)
        original_mode = os.fstat(descriptor).st_mode & 0o7777
        try:
            with mock.patch.object(self.fx.runner, "attestation_issuer") as issuer:
                with self.assertRaisesRegex(TrustedRunnerError, "workspace cannot be enumerated safely"):
                    self.fx.run()
                issuer.assert_not_called()
                self.assertEqual(1, len(self.fx.processes))
                with self.assertRaises(PermissionError):
                    with os.scandir(directory) as entries:
                        list(entries)
                self.assertEqual("changed", target.read_text(encoding="utf-8"))
                print("P17A_PERMISSION_CASE " + json.dumps({
                    "phase": "post", "enumeration_denied": True,
                    "known_file_after": "changed", "issuer_calls": issuer.call_count,
                    "run_rejected": True, "controlled_processes": len(self.fx.processes),
                }, sort_keys=True), flush=True)
        finally:
            try:
                os.fchmod(descriptor, original_mode)
                self.assertEqual(original_mode, os.fstat(descriptor).st_mode & 0o7777)
                print("P17A_PERMISSION_RESTORED " + json.dumps({"path": str(directory), "mode": original_mode}), flush=True)
            finally:
                os.close(descriptor)

    def test_workspace_enumeration_errors_fail_closed_before_and_after_execution(self):
        scandir, read_stat = os.scandir, os.stat
        for phase in ("pre", "post"):
            for fault in ("open", "iteration", "stat"):
                active, observed = [phase == "pre"], []

                def scan(path):
                    if active[0] and isinstance(path, int) and fault == "open":
                        observed.append(fault)
                        raise OSError("synthetic enumeration open failure")

                    @contextlib.contextmanager
                    def opened():
                        with scandir(path) as entries:
                            if active[0] and isinstance(path, int) and fault == "iteration":
                                def interrupted():
                                    yield next(entries)
                                    observed.append(fault)
                                    raise OSError("synthetic enumeration iteration failure")
                                yield interrupted()
                            else:
                                yield entries
                    return opened()

                def classify(path, *args, **kwargs):
                    if active[0] and fault == "stat" and path == "Modules" and kwargs.get("dir_fd") is not None:
                        observed.append(fault)
                        raise OSError("synthetic enumeration stat failure")
                    return read_stat(path, *args, **kwargs)

                before = len(self.fx.processes)
                boundary = self._after_execution(lambda: active.__setitem__(0, True)) if phase == "post" else contextlib.nullcontext()
                with self.subTest(phase=phase, fault=fault), boundary, mock.patch.object(os, "scandir", side_effect=scan), mock.patch.object(os, "stat", side_effect=classify), mock.patch.object(self.fx.runner, "attestation_issuer") as issuer:
                    with self.assertRaisesRegex(TrustedRunnerError, "workspace cannot be enumerated safely"):
                        self.fx.run()
                    issuer.assert_not_called()
                    self.assertEqual([fault], observed)
                    self.assertEqual(before + (phase == "post"), len(self.fx.processes))
                    print("P17A_ENUMERATION_FAULT " + json.dumps({
                        "phase": phase, "fault": fault, "issuer_calls": issuer.call_count,
                        "controlled_processes": len(self.fx.processes) - before,
                    }, sort_keys=True), flush=True)

    def test_workspace_digest_preserves_global_path_order_and_nested_git(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            contents = {
                ".hidden": b"hidden", "a.txt": b"first", "a/.hidden": b"nested hidden",
                "a/nested.txt": b"second", "z/.git/kept.txt": b"nested Git name",
                ".git/ignored.txt": b"excluded",
            }
            for name, content in contents.items():
                path = root / name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(content)
            expected = [{"path": name, "sha256": hashlib.sha256(contents[name]).hexdigest()} for name in (
                ".hidden", "a.txt", "a/.hidden", "a/nested.txt", "z/.git/kept.txt",
            )]
            digest = hashlib.sha256(json.dumps(expected, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode("utf-8")).hexdigest()
            actual, identities = MODULE._workspace_snapshot(root)
            self.assertEqual(digest, actual)
            self.assertEqual({".", ".hidden", "a", "a.txt", "a/.hidden", "a/nested.txt", "z", "z/.git", "z/.git/kept.txt"}, set(identities))

    def test_postcheck_rejects_changed_head_and_manifest(self):
        for change in (
            lambda: self.fx._git("-c", "user.name=DPS Test", "-c", "user.email=dps@example.invalid", "commit", "--allow-empty", "-qm", "changed fixture head"),
            lambda: self.fx.manifest_path.write_text("{}", encoding="utf-8"),
        ):
            with self._after_execution(change), mock.patch.object(self.fx.runner, "attestation_issuer") as issuer:
                with self.assertRaises(TrustedRunnerError):
                    self.fx.run()
                issuer.assert_not_called()
            self.fx._write_manifest()

    def test_postcheck_rejects_mutable_request_receipt_plan_lease_and_policy(self):
        changes = (
            lambda: self.fx.request.__setitem__("trace_id", "trace_" + "f" * 32),
            lambda: self.fx.receipt.__setitem__("status", "STALE"),
            lambda: self.fx.plan["entries"][0].__setitem__("writer_identity", "other"),
            lambda: self.fx.lease["lock_tokens"].__setitem__("module:demo", 10),
            lambda: self.fx.policy.checks.clear(),
        )
        for change in changes:
            saved = copy.deepcopy((self.fx.request, self.fx.receipt, self.fx.plan, self.fx.lease, self.fx.policy))
            with self._after_execution(change), mock.patch.object(self.fx.runner, "attestation_issuer") as issuer:
                with self.assertRaises(TrustedRunnerError):
                    self.fx.run()
                issuer.assert_not_called()
            self.fx.request, self.fx.receipt, self.fx.plan, self.fx.lease, self.fx.policy = saved

    def test_postcheck_refreshes_auth_receipt_workspace_and_fence_ports(self):
        for name in ("auth_context_verifier", "receipt_verifier", "commit_workspace_verifier", "fence_verifier"):
            original = getattr(self.fx.runner, name)
            calls = []

            def revoked(*args):
                value = original(*args)
                calls.append(True)
                if len(calls) > 1:
                    value["verified"] = False
                return value

            with self.subTest(port=name), mock.patch.object(self.fx.runner, name, side_effect=revoked), mock.patch.object(self.fx.runner, "attestation_issuer") as issuer:
                with self.assertRaises(TrustedRunnerError):
                    self.fx.run()
                issuer.assert_not_called()
            self.assertGreaterEqual(len(calls), 2)

    def test_postcheck_rejects_expiry_equality_and_clock_rollback(self):
        self.fx.lease["acquired_at"] = "2026-09-15T07:00:00Z"
        self.fx.lease["expires_at"] = "2026-09-15T07:00:02Z"
        for finished in ("2026-09-15T07:00:02Z", "2026-09-15T07:00:03Z", "2026-09-15T06:59:59Z"):
            samples = iter(("2026-09-15T07:00:01Z", finished))
            last = ["2026-09-15T07:00:01Z"]

            def clock():
                last[0] = next(samples, last[0])
                return last[0]

            with self.subTest(finished=finished), self._after_execution(lambda: None), mock.patch.object(MODULE, "_utc_now", side_effect=clock), mock.patch.object(self.fx.runner, "attestation_issuer") as issuer:
                with self.assertRaises(TrustedRunnerError):
                    self.fx.run()
                issuer.assert_not_called()
        with self._after_execution(lambda: None), mock.patch.object(MODULE, "_utc_now", side_effect=["2026-09-15T07:00:01Z", "2026-09-15T07:00:01Z", "2026-09-15T07:00:02Z"]), mock.patch.object(self.fx.runner, "attestation_issuer") as issuer:
            with self.assertRaisesRegex(TrustedRunnerError, "not current"):
                self.fx.run()
            issuer.assert_not_called()

    def test_postcheck_rejects_reverified_changed_auth_and_fence_facts(self):
        for name, field, value in (
            ("auth_context_verifier", "fact_sha256", "f" * 64),
            ("fence_verifier", "fencing_token", 10),
            ("fence_verifier", "lock_tokens", {"module:demo": 10}),
        ):
            original = getattr(self.fx.runner, name)
            calls = []

            def changed(*args):
                fact = original(*args)
                calls.append(True)
                if len(calls) > 1:
                    fact[field] = value
                return fact

            with self.subTest(port=name, field=field), self._after_execution(lambda: None), mock.patch.object(self.fx.runner, name, side_effect=changed), mock.patch.object(self.fx.runner, "attestation_issuer") as issuer:
                with self.assertRaises(TrustedRunnerError):
                    self.fx.run()
                issuer.assert_not_called()
            self.assertEqual(2, len(calls))

    def test_postcheck_rejects_untracked_directory_and_symlink(self):
        for symlink in (False, True):
            target = self.fx.root / "runtime-entry"
            def change():
                if symlink:
                    target.symlink_to(self.fx.manifest_path)
                else:
                    target.mkdir()
            try:
                with self.subTest(symlink=symlink), self._after_execution(change), mock.patch.object(self.fx.runner, "attestation_issuer") as issuer:
                    with self.assertRaises(TrustedRunnerError):
                        self.fx.run()
                    issuer.assert_not_called()
            finally:
                target.unlink() if symlink else target.rmdir()

    def test_postcheck_io_failure_has_no_issuer(self):
        open_file = os.open
        finished = [False]

        def read(path, *args, **kwargs):
            if finished[0] and str(path) == "module.yaml":
                raise OSError("synthetic post-execution read failure")
            return open_file(path, *args, **kwargs)

        with self._after_execution(lambda: finished.__setitem__(0, True)), mock.patch.object(os, "open", read), mock.patch.object(self.fx.runner, "attestation_issuer") as issuer:
            with self.assertRaises((TrustedRunnerError, OSError)):
                self.fx.run()
            issuer.assert_not_called()

    def test_manifest_uses_one_safe_byte_read_per_validation(self):
        original = MODULE._read_protected_file
        calls = []

        def read(path):
            calls.append(path)
            return original(path)

        with mock.patch.object(MODULE, "_read_protected_file", side_effect=read):
            result = self.fx.runner._verified_manifest_suite("demo", self.fx.policy.checks["demo.unit"], self.fx.receipt)
        self.assertEqual([self.fx.manifest_path], calls)
        self.assertEqual(sha256_bytes(self.fx.manifest_path.read_bytes()), result[0])

    def test_protected_read_rejects_parent_replacement_during_read(self):
        original_stat = os.stat
        parent = self.fx.manifest_path.parent
        changed = []

        def replace(path, *args, **kwargs):
            if path == "module.yaml" and kwargs.get("dir_fd") is not None and not changed:
                changed.append(True)
                data = self.fx.manifest_path.read_bytes()
                parent.rename(parent.with_name("previous-demo"))
                parent.mkdir()
                self.fx.manifest_path.write_bytes(data)
            return original_stat(path, *args, **kwargs)

        with mock.patch.object(os, "stat", side_effect=replace):
            with self.assertRaisesRegex(TrustedRunnerError, "parent identity"):
                MODULE._read_protected_file(self.fx.manifest_path)
        self.assertEqual([True], changed)

    def test_postcheck_rejects_same_bytes_file_identity_replacement(self):
        def replace():
            temporary = self.fx.manifest_path.with_name("replacement")
            temporary.write_bytes(self.fx.manifest_path.read_bytes())
            temporary.replace(self.fx.manifest_path)

        with self._after_execution(replace), mock.patch.object(self.fx.runner, "attestation_issuer") as issuer:
            with self.assertRaises(TrustedRunnerError):
                self.fx.run()
            issuer.assert_not_called()

    def test_cleanup_finishes_inherited_group_before_return(self):
        # The child is bounded even on the unfixed implementation. The parent
        # records its PID/PGID in an external fixture directory for readback.
        with tempfile.TemporaryDirectory() as external:
            root = Path(external)
            late, identity = self.fx.root / "late-write", root / "child.json"
            code = "import os,json,pathlib,time;pathlib.Path(" + repr(str(identity)) + ").write_text(json.dumps({'pid':os.getpid(),'pgid':os.getpgrp()}));time.sleep(0.4);pathlib.Path(" + repr(str(late)) + ").write_text('late');print('late stdout')"
            self.fx.test_file.write_text(
                "import subprocess,sys,unittest,time,pathlib\nclass T(unittest.TestCase):\n"
                "    def test_child(self):\n"
                "        subprocess.Popen([sys.executable,'-c'," + repr(code) + "])\n"
                "        end=time.monotonic()+0.2\n"
                "        while not pathlib.Path(" + repr(str(identity)) + ").exists() and time.monotonic()<end: time.sleep(0.001)\n",
                encoding="utf-8",
            )
            try:
                with mock.patch.object(self.fx.runner, "attestation_issuer") as issuer:
                    with self.assertRaisesRegex(TrustedRunnerError, "workspace"):
                        self.fx.run()
                    issuer.assert_not_called()
            finally:
                child = json.loads(identity.read_text(encoding="utf-8")) if identity.exists() else {"identity": "UNKNOWN"}
                print("P17A_SYNTHETIC_CHILD " + json.dumps(child, sort_keys=True), flush=True)
            self.assertIn(child["pgid"], self.fx.processes)
            self.assertTrue(late.exists())
            observed = late.read_bytes()
            time.sleep(0.45)
            self.assertEqual(observed, late.read_bytes())

    def test_cleanup_failure_never_reaches_issuer(self):
        cleanup = TrustedRunner._terminate

        def fail_after_safe_cleanup(process):
            cleanup(process)
            raise TrustedRunnerError("synthetic cleanup acknowledgement failure")

        with mock.patch.object(TrustedRunner, "_terminate", side_effect=fail_after_safe_cleanup), mock.patch.object(self.fx.runner, "attestation_issuer") as issuer:
            with self.assertRaises(TrustedRunnerError):
                self.fx.run()
            issuer.assert_not_called()

    def test_descendant_runtime_counts_toward_timeout(self):
        with tempfile.TemporaryDirectory() as external:
            identity = Path(external) / "child.json"
            code = "import os,json,pathlib,time;pathlib.Path(" + repr(str(identity)) + ").write_text(json.dumps({'pid':os.getpid(),'pgid':os.getpgrp()}));time.sleep(1.2);print('bounded child output')"
            self.fx.test_file.write_text(
                "import subprocess,sys,unittest,time,pathlib\nclass T(unittest.TestCase):\n"
                "    def test_child(self):\n"
                "        subprocess.Popen([sys.executable,'-c'," + repr(code) + "])\n"
                "        end=time.monotonic()+0.2\n"
                "        while not pathlib.Path(" + repr(str(identity)) + ").exists() and time.monotonic()<end: time.sleep(0.001)\n",
                encoding="utf-8",
            )
            raw = json.loads(self.fx.policy_document)
            raw["checks"][0]["timeout_seconds"] = 1
            document = canonical_bytes(raw)
            digest = sha256_bytes(document)
            policy = TrustedRunnerPolicy.from_verified_document(document, digest, lambda _doc, value: self.fx.fact(value, value))
            plan = dict(self.fx.plan)
            plan["trusted_policy_sha256"] = digest
            executed = []
            execute = self.fx.runner._execute
            def capture(*args):
                value = execute(*args)
                executed.append(value)
                return value
            try:
                with mock.patch.object(self.fx.runner, "_execute", side_effect=capture):
                    result = self.fx.runner.run(self.fx.request, plan, self.fx.receipt, self.fx.lease, policy)
                self.assertEqual("INFRA_ERROR", result["status"])
                self.assertEqual(0, result["exit_code"])
                self.assertFalse(result["release_allowed"])
                elapsed = dt.datetime.fromisoformat(result["finished_at"].replace("Z", "+00:00")) - dt.datetime.fromisoformat(result["started_at"].replace("Z", "+00:00"))
                self.assertGreaterEqual(elapsed.total_seconds(), 1)
                self.assertEqual(1, len(executed))
                self.assertIn(b"bounded child output", executed[0][1] + executed[0][2])
            finally:
                child = json.loads(identity.read_text(encoding="utf-8")) if identity.exists() else {"identity": "UNKNOWN"}
                print("P17A_SYNTHETIC_CHILD " + json.dumps(child, sort_keys=True), flush=True)
            self.assertIn(child["pgid"], self.fx.processes)

    def test_esrch_is_terminal_and_no_signal_follows_reap(self):
        process = mock.Mock(pid=123, returncode=None)
        process.wait.side_effect = [subprocess.TimeoutExpired("synthetic", 0.05), 0]
        with mock.patch.object(os, "killpg", side_effect=ProcessLookupError) as send:
            TrustedRunner._terminate(process)
        self.assertEqual(1, send.call_count)
        self.assertEqual(2, process.wait.call_count)
        process.returncode = 0
        process.wait.side_effect = None
        with mock.patch.object(os, "killpg", side_effect=ProcessLookupError) as send:
            TrustedRunner._terminate(process)
            send.assert_called_once_with(123, 0)

    def test_permission_errors_preserve_bounded_wait_and_reject_issuance(self):
        for live in (False, True):
            clock = [0.0]
            process = mock.Mock(pid=123, returncode=None if live else 0)
            process.wait.side_effect = [subprocess.TimeoutExpired("synthetic", 0.05), 0] if live else [0]
            with self.subTest(live=live), mock.patch.object(time, "monotonic", side_effect=lambda: clock[0]), mock.patch.object(time, "sleep", side_effect=lambda _seconds: clock.__setitem__(0, 5.0)), mock.patch.object(os, "killpg", side_effect=PermissionError(1, "synthetic EPERM")) as send, mock.patch.object(self.fx.runner, "_execute", side_effect=lambda *_: TrustedRunner._terminate(process)), mock.patch.object(self.fx.runner, "attestation_issuer") as issuer:
                with self.assertRaises(TrustedRunnerError):
                    self.fx.run()
                self.assertEqual(2 if live else 1, process.wait.call_count)
                send.assert_called_once_with(123, 9 if live else 0)
                issuer.assert_not_called()

    def test_signal_zero_unknown_resolves_only_before_original_deadline(self):
        process = mock.Mock(pid=123, returncode=0)
        clock = [0.0]
        with mock.patch.object(time, "monotonic", side_effect=lambda: clock[0]), mock.patch.object(time, "sleep", side_effect=lambda seconds: clock.__setitem__(0, clock[0] + seconds)), mock.patch.object(os, "killpg", side_effect=[PermissionError(1, "synthetic EPERM"), ProcessLookupError]) as send:
            TrustedRunner._terminate(process)
            self.assertEqual([mock.call(123, 0), mock.call(123, 0)], send.call_args_list)
        self.assertLess(clock[0], 5)

        def late_absence(*_args):
            clock[0] += 5
            raise ProcessLookupError

        with mock.patch.object(time, "monotonic", side_effect=lambda: clock[0]), mock.patch.object(os, "killpg", side_effect=late_absence) as send, mock.patch.object(self.fx.runner, "_execute", side_effect=lambda *_: TrustedRunner._terminate(process)), mock.patch.object(self.fx.runner, "attestation_issuer") as issuer:
            with self.assertRaisesRegex(TrustedRunnerError, "deadline"):
                self.fx.run()
            send.assert_called_once_with(123, 0)
            issuer.assert_not_called()

    def test_supported_normal_external_outputs_preserve_workspace(self):
        self.fx.test_file.write_text(
            "import os,pathlib,tempfile,sys,unittest\nclass T(unittest.TestCase):\n"
            "    def test_outputs(self):\n"
            "        print('normal stdout'); print('normal stderr',file=sys.stderr)\n"
            "        with tempfile.NamedTemporaryFile() as f: f.write(b'temp')\n"
            "        pathlib.Path(os.environ['HOME'],'p17a-normal-output').write_text('normal')\n",
            encoding="utf-8",
        )
        before = workspace_sha256(self.fx.root)
        result = self.fx.run()
        self.assertEqual("PASS", result["status"])
        self.assertEqual(before, workspace_sha256(self.fx.root))
        self.assertNotIn("PYTHONPYCACHEPREFIX", self.fx.runner._environment(self.fx.policy))

    def test_dynamic_current_result_has_real_rsa_pss_and_tamper_rejection(self):
        from jsonschema import Draft202012Validator
        with self.fx.real_signer() as (signer, verify):
            self.fx.runner = self.fx._runner(attestation_issuer=signer)
            result = self.fx.run()
            payload = canonical_bytes({k: v for k, v in result.items() if k != "runner_attestation"})
            signature = base64.b64decode(result["runner_attestation"]["signature_value"], validate=True)
            self.assertEqual(0, verify(payload, signature))
            self.assertNotEqual(0, verify(payload + b" ", signature))
            self.assertNotEqual(0, verify(payload, bytes([signature[0] ^ 1]) + signature[1:]))
            schema = json.loads((SOURCE.parents[1] / "contracts/provided/trusted.test.result.v1.schema.json").read_text(encoding="utf-8"))
            Draft202012Validator(schema).validate(result)

    def test_exact_policy_check_passes_and_emits_only_hashes(self):
        result = self.fx.run()
        self.assertEqual("PASS", result["status"])
        self.assertTrue(result["release_allowed"])
        self.assertEqual(self.fx.commit, result["tested_commit"])
        self.assertEqual("demo.unit", result["suite_id"])
        self.assertEqual("REPOSITORY_STATIC_VERIFIED", result["evidence_level"])
        self.assertEqual(self.fx.policy_digest, result["trusted_policy_sha256"])
        self.assertEqual(9, result["fencing_token"])
        self.assertNotIn("stdout", result)
        self.assertEqual(result["runner_attestation"]["payload_sha256"], sha256_bytes(canonical_bytes({k: v for k, v in result.items() if k != "runner_attestation"})))

    def test_request_cannot_supply_command_roles_required_or_commit(self):
        for key, value in (
            ("command_argv", ["sh", "-c", "id"]), ("roles", ["approver"]),
            ("required", False), ("tested_commit", COMMIT_ZERO),
        ):
            request = dict(self.fx.request); request[key] = value
            with self.assertRaises(TrustedRunnerError):
                self.fx.runner.run(request, self.fx.plan, self.fx.receipt, self.fx.lease, self.fx.policy)

    def test_policy_digest_external_fact_and_role_separation_are_required(self):
        with self.assertRaises(TrustedRunnerError):
            TrustedRunnerPolicy.from_verified_document(
                self.fx.policy_document, "f" * 64,
                lambda _doc, digest: self.fx.fact(digest, digest),
            )
        with self.assertRaises(TrustedRunnerError):
            TrustedRunnerPolicy.from_verified_document(
                self.fx.policy_document, self.fx.policy_digest,
                lambda _doc, digest: {"verified": True, "fact_id": digest, "fact_sha256": "0" * 64},
            )
        document = self.fx._policy_document(roles={
            "implementers": ["same"], "evidence_issuers": ["same"],
            "release_approvers": ["human"],
        })
        digest = sha256_bytes(document)
        with self.assertRaises(TrustedRunnerError):
            TrustedRunnerPolicy.from_verified_document(
                document, digest, lambda _doc, value: self.fx.fact(value, value)
            )

    def test_candidate_policy_replacement_cannot_change_process_bound_digest(self):
        candidate = json.loads(self.fx.policy_document)
        candidate["checks"][0]["required"] = False
        replacement = canonical_bytes(candidate)
        with self.assertRaises(TrustedRunnerError):
            TrustedRunnerPolicy.from_verified_document(
                replacement, self.fx.policy_digest,
                lambda _doc, value: self.fx.fact(value, value),
            )

    def test_shell_interpreter_and_shell_like_tokens_rejected_even_in_signed_policy(self):
        raw = json.loads(self.fx.policy_document)
        raw["allowed_executables"] = ["sh"]
        raw["checks"][0]["argv"] = ["sh", "-c", "echo OK"]
        document = canonical_bytes(raw); digest = sha256_bytes(document)
        with self.assertRaises(TrustedRunnerError):
            TrustedRunnerPolicy.from_verified_document(
                document, digest, lambda _doc, value: self.fx.fact(value, value)
            )

    def test_stale_receipt_plan_writer_and_fence_fail_before_execution(self):
        receipt = copy.deepcopy(self.fx.receipt); receipt["status"] = "STALE"
        with self.assertRaises(TrustedRunnerError):
            self.fx.runner.run(self.fx.request, self.fx.plan, receipt, self.fx.lease, self.fx.policy)
        plan = copy.deepcopy(self.fx.plan); plan["entries"][0]["writer_identity"] = "request-self-appointed"
        with self.assertRaises(TrustedRunnerError):
            self.fx.runner.run(self.fx.request, plan, self.fx.receipt, self.fx.lease, self.fx.policy)
        runner = self.fx._runner(fence_verifier=lambda _lease, _tokens: self.fx.fact(self.fx.lease["lease_id"], sha256_bytes(canonical_bytes(self.fx.lease)), plan_id=self.fx.plan["plan_id"], lock_tokens=self.fx.lease["lock_tokens"], fencing_token=10))
        with self.assertRaises(TrustedRunnerError):
            runner.run(self.fx.request, self.fx.plan, self.fx.receipt, self.fx.lease, self.fx.policy)

    def test_workspace_symlink_and_manifest_drift_fail_closed(self):
        (self.fx.module_root / "escape").symlink_to("/etc/hosts")
        with self.assertRaises(TrustedRunnerError):
            self.fx.run()
        (self.fx.module_root / "escape").unlink()
        self.fx.manifest["tests"]["suites"][0]["required"] = False
        self.fx._write_manifest()
        with self.assertRaises(TrustedRunnerError):
            self.fx.run()

    def test_skip_output_never_releases(self):
        self.fx.test_file.write_text(
            "import unittest\nclass T(unittest.TestCase):\n    @unittest.skip('no')\n    def test_skip(self): pass\n",
            encoding="utf-8",
        )
        result = self.fx.run()
        self.assertEqual("SKIP", result["status"])
        self.assertFalse(result["release_allowed"])

    def test_timeout_and_output_limit_are_infrastructure_errors(self):
        self.fx.test_file.write_text(
            "import time, unittest\nclass T(unittest.TestCase):\n    def test_slow(self): time.sleep(2)\n",
            encoding="utf-8",
        )
        raw = json.loads(self.fx.policy_document); raw["checks"][0]["timeout_seconds"] = 1
        document = canonical_bytes(raw); digest = sha256_bytes(document)
        policy = TrustedRunnerPolicy.from_verified_document(document, digest, lambda _doc, value: self.fx.fact(value, value))
        plan = dict(self.fx.plan); plan["trusted_policy_sha256"] = digest
        result = self.fx.runner.run(self.fx.request, plan, self.fx.receipt, self.fx.lease, policy)
        self.assertEqual("INFRA_ERROR", result["status"])
        self.assertFalse(result["release_allowed"])

        self.fx.test_file.write_text(
            "import unittest\nclass T(unittest.TestCase):\n    def test_loud(self): print('X' * 20000)\n",
            encoding="utf-8",
        )
        raw["checks"][0]["timeout_seconds"] = 10; raw["maximum_output_bytes"] = 200
        document = canonical_bytes(raw); digest = sha256_bytes(document)
        policy = TrustedRunnerPolicy.from_verified_document(document, digest, lambda _doc, value: self.fx.fact(value, value))
        plan["trusted_policy_sha256"] = digest
        result = self.fx.runner.run(self.fx.request, plan, self.fx.receipt, self.fx.lease, policy)
        self.assertEqual("INFRA_ERROR", result["status"])

    def test_malformed_identity_and_attestation_are_rejected(self):
        request = dict(self.fx.request); request["soul_id"] = "soul_bad"
        with self.assertRaises(TrustedRunnerError):
            self.fx.runner.run(request, self.fx.plan, self.fx.receipt, self.fx.lease, self.fx.policy)
        runner = self.fx._runner(attestation_issuer=lambda _payload, runner, _policy: {
            "algorithm": "rsa-pss-sha256", "key_id": "key", "signer_identity": runner,
            "payload_sha256": "0" * 64, "signature_value": "A" * 128,
        })
        with self.assertRaises(TrustedRunnerError):
            runner.run(self.fx.request, self.fx.plan, self.fx.receipt, self.fx.lease, self.fx.policy)

    def test_restricted_cli_rejects_static_hmac_before_execution_or_key_read(self):
        with tempfile.TemporaryDirectory() as external:
            external_root = Path(external)
            facts_payload = {
                "policy": self.fx.fact(self.fx.policy_digest, self.fx.policy_digest),
                "auth_context": self.fx.fact(self.fx.request["auth_context_id"], "a" * 64),
                "instruction_receipt": self.fx.fact(
                    self.fx.receipt["receipt_id"], sha256_bytes(canonical_bytes(self.fx.receipt))
                ),
                "commit_workspace": self.fx.fact(
                    self.fx.plan["plan_id"], "b" * 64, commit=self.fx.commit,
                    workspace_sha256=workspace_sha256(self.fx.root),
                ),
                "lease": self.fx.fact(
                    self.fx.lease["lease_id"], sha256_bytes(canonical_bytes(self.fx.lease)),
                    plan_id=self.fx.plan["plan_id"], lock_tokens=self.fx.lease["lock_tokens"],
                    fencing_token=9,
                ),
            }
            hmac_key = bytes(range(32))
            documents = {
                "request.json": self.fx.request, "worktree-plan.json": self.fx.plan,
                "instruction-receipt.json": self.fx.receipt, "worktree-lease.json": self.fx.lease,
                "trusted-policy.json": json.loads(self.fx.policy_document),
                "runtime-facts.json": {
                    "payload": facts_payload,
                    "hmac_sha256": hmac.new(hmac_key, canonical_bytes(facts_payload), hashlib.sha256).hexdigest(),
                },
            }
            for name, value in documents.items():
                (external_root / name).write_text(json.dumps(value, sort_keys=True), encoding="utf-8")
            environment = {
                "DPS_RUNNER_REPOSITORY_ROOT": str(self.fx.root),
                "DPS_RUNNER_EXECUTION_ROOT": str(self.fx.root),
                "DPS_RUNNER_INPUT_DIR": str(external_root),
                "DPS_RUNNER_POLICY_SHA256": self.fx.policy_digest,
                "DPS_RUNNER_FACT_HMAC_KEY_HEX": hmac_key.hex(),
                "DPS_RUNNER_RSA_PRIVATE_KEY": str(external_root / "must-not-open.pem"),
                "DPS_RUNNER_RSA_KEY_ID": "runner-key-1",
                "DPS_RUNNER_OPENSSL": shutil.which("openssl"),
                "DPS_RUNNER_IDENTITY": "runner-service",
            }
            with mock.patch.dict(os.environ, environment, clear=False):
                self.assertEqual(facts_payload, CLI._runtime_facts(documents["runtime-facts.json"]))
                with mock.patch.object(CLI, "_rsa_pss_issuer") as issuer, mock.patch.object(CLI, "_read_fixed_json") as read, mock.patch.object(subprocess, "Popen") as execute:
                    with self.assertRaisesRegex(TrustedRunnerError, "currentness"):
                        CLI.run_from_environment()
                    with contextlib.redirect_stderr(io.StringIO()):
                        self.assertEqual(2, CLI.main())
                    issuer.assert_not_called()
                    read.assert_not_called()
                    execute.assert_not_called()
                bad = copy.deepcopy(documents["runtime-facts.json"])
                bad["hmac_sha256"] = "0" * 64
                with self.assertRaises(TrustedRunnerError):
                    CLI._runtime_facts(bad)
                path = external_root / "request.json"
                path.unlink()
                path.symlink_to(external_root / "trusted-policy.json")
                with self.assertRaises(TrustedRunnerError):
                    CLI._read_fixed_json(external_root, "request.json")


class MergeBatchFixture(RunnerFixture):
    """TEST_ONLY authorization, real run subprocesses; no deployed trust."""

    def __init__(self, nonnull=False):
        super().__init__()
        second_suite = {**self.manifest["tests"]["suites"][0], "id": "demo.contract", "type": "contract"}
        self.manifest["tests"]["suites"].append(second_suite)
        self._write_manifest()
        self._git("add", ".")
        self._git("-c", "user.name=DPS Test", "-c", "user.email=dps@example.invalid", "commit", "-qm", "two required suites")
        self.commit = self._git("rev-parse", "HEAD").strip()
        document = json.loads(self._policy_document())
        document["checks"].append({**document["checks"][0], "check_id": "demo.contract"})
        self.policy_document = canonical_bytes(document)
        self.policy_digest = sha256_bytes(self.policy_document)
        self.policy = TrustedRunnerPolicy.from_verified_document(
            self.policy_document, self.policy_digest, lambda _document, digest: self.fact(digest, digest)
        )
        self.plan["trusted_policy_sha256"] = self.policy_digest
        self.receipt = self._receipt()
        self.receipt["diff_fingerprint"] = "d" * 64
        if nonnull:
            values = {"soul_id": "soul_" + "1" * 64, "device_binding_id": "db_" + "2" * 32,
                      "platform_account_id": "pa_" + "3" * 32}
            for value in (self.request, self.plan, self.receipt):
                value.update(values)
        self.work = []
        for index, check_id in enumerate(("demo.unit", "demo.contract")):
            item = copy.deepcopy({"request": self.request, "worktree_plan": self.plan,
                                  "instruction_receipt": self.receipt, "lease": self.lease})
            item["request"].update(check_id=check_id, request_id="request:batch-" + str(index),
                                   auth_context_id="auth-context-batch-" + str(index),
                                   instruction_receipt_id="instruction:batch-" + str(index))
            item["worktree_plan"]["instruction_receipt_id"] = item["request"]["instruction_receipt_id"]
            item["instruction_receipt"].update(receipt_id=item["request"]["instruction_receipt_id"],
                                                auth_context_id=item["request"]["auth_context_id"])
            self.work.append(item)
        self.auth_hashes = {item["request"]["auth_context_id"]: "a" * 64 for item in self.work}
        self.merge_policy = {
            "schema_version": "dps.merge-policy/v1", "policy_id": "merge-policy-batch",
            "required_checks": ["demo.unit", "demo.contract"], "implementers": ["implementer-1"],
            "evidence_issuers": ["runner-service"], "merge_decider": "merge-decider",
            "release_approvers": ["human-approver"], "trusted_runner_policy_sha256": self.policy_digest,
        }
        self.metadata = {name: self.request[name] for name in (*MODULE._IDENTITIES, "trace_id", "idempotency_key")}
        self.metadata.update(privacy_class="internal", merge_request_id="merge-request-batch", integration_commit=self.commit,
                             branch_heads=[{"module_id": "demo", "commit": self.commit}],
                             changed_paths=["Modules/demo/tests/test_fixture.py"],
                             current_diff_fingerprint="d" * 64,
                             conflicts={"merge": [], "path_ownership": [], "contract_ownership": []},
                             trusted_policy_sha256=sha256_bytes(canonical_bytes(self.merge_policy)),
                             instruction_receipts=[{
                                 "receipt_id": item["instruction_receipt"]["receipt_id"], "status": "BOUND",
                                 "diff_fingerprint": "d" * 64,
                                 "receipt_sha256": sha256_bytes(canonical_bytes(item["instruction_receipt"])),
                             } for item in self.work])
        self.authorized_metadata = copy.deepcopy(self.metadata)
        self.authorized = True
        self.merge_calls, self.result_signatures, self.request_signatures = [], [], []
        self.base_issuer = self.runner.attestation_issuer
        self.runner = self._runner(
            auth_context_verifier=lambda auth_id, _runner: self.fact(auth_id, self.auth_hashes[auth_id]),
            merge_request_verifier=self.verify_merge,
            attestation_issuer=self.issue_attestation,
        )

    def verify_merge(self, request, runner):
        """TEST_ONLY exact proposal authority with explicit revocation and policy checks."""
        self.merge_calls.append(canonical_bytes(request))
        if not self.authorized or runner != "runner-service":
            raise TrustedRunnerError("TEST_ONLY merge authorization revoked")
        if any(request.get(name) != value for name, value in self.authorized_metadata.items()):
            raise TrustedRunnerError("TEST_ONLY proposal differs from authorized merge facts")
        if (request.get("schema_version") != "1.0.0" or request.get("contract_id") != "merge.request/v1"
                or request.get("producer_module") != "factory-trusted-runner"):
            raise TrustedRunnerError("TEST_ONLY wrong producer contract")
        evidence = request.get("evidence", [])
        if sorted(item["result"]["check_id"] for item in evidence) != sorted(self.merge_policy["required_checks"]):
            raise TrustedRunnerError("TEST_ONLY incomplete current Merge required set")
        if self.merge_policy["trusted_runner_policy_sha256"] != self.policy_digest:
            raise TrustedRunnerError("TEST_ONLY Runner policy is not authorized by Merge")
        if any(item["basis"] != "merge-head" or item["result"]["tested_commit"] != self.commit for item in evidence):
            raise TrustedRunnerError("TEST_ONLY commit or branch relationship is not authorized")
        return self.fact(request["merge_request_id"], sha256_bytes(canonical_bytes(request)))

    def issue_attestation(self, payload, runner, policy):
        calls = self.request_signatures if payload.startswith(b"dps-merge-request-attestation/v1\n") else self.result_signatures
        calls.append(sha256_bytes(payload))
        return self.base_issuer(payload, runner, policy)

    def issue(self):
        return self.runner.issue_merge_request(self.metadata, self.work, self.policy)


class MergeBatchTests(unittest.TestCase):
    def test_shared_owner_corpus_schema_and_runtime_boundaries(self):
        path = Path(__file__).resolve().parents[2] / "factory-merge-controller/tests/fixtures/merge.request.v1.corpus.json"
        corpus = json.loads(path.read_text(encoding="utf-8"))
        with self.fixture() as fx:
            for case in corpus["cases"]:
                with self.subTest(case_id=case["case_id"]): assert_producer_corpus_case(self, case, fx)
            for value in (float("nan"), float("inf"), float("-inf")):
                metadata = copy.deepcopy(fx.metadata)
                metadata["branch_heads"][0]["commit"] = value
                with self.assertRaises(TrustedRunnerError): fx.runner.issue_merge_request(metadata, fx.work, fx.policy)
            self.assertEqual([], fx.result_signatures + fx.request_signatures)

    @contextlib.contextmanager
    def fixture(self, nonnull=False):
        fx = MergeBatchFixture(nonnull)
        try:
            yield fx
        finally:
            fx.close()

    def test_real_two_check_batch_openssl_to_merge_null_and_nonempty(self):
        path = Path(__file__).resolve().parents[2] / "factory-merge-controller" / "src" / "merge_controller.py"
        spec = importlib.util.spec_from_file_location("p17b_real_merge_subject", path)
        merge = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(merge)
        for nonnull in (False, True):
            with self.subTest(nonnull=nonnull), self.fixture(nonnull) as fx, fx.real_signer() as (issuer, verify):
                fx.base_issuer = issuer
                key = os.environ["DPS_RUNNER_RSA_PRIVATE_KEY"]
                openssl = os.environ["DPS_RUNNER_OPENSSL"]
                modulus = subprocess.run([openssl, "rsa", "-in", key, "-noout", "-modulus"], check=True,
                                         stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=15).stdout.decode("ascii").strip().split("=", 1)[1]
                public = subprocess.run([openssl, "pkey", "-in", key, "-text_pub", "-noout"], check=True,
                                        stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=15).stdout.decode("ascii")
                exponent = int(re.search(r"Exponent: (\d+)", public).group(1))
                consumer = merge.MergeController("merge-decider", fx.merge_policy, merge.RsaPssTrustStore({
                    "runner-key-1": {"identity": "runner-service", "algorithm": "rsa-pss-sha256",
                                     "modulus_hex": modulus, "exponent": exponent},
                }))
                signed = fx.issue()
                from jsonschema import Draft202012Validator, FormatChecker
                from referencing import Registry, Resource
                owner_root = path.parents[1]
                owner_schema = json.loads((owner_root / "contracts/provided/merge.request.v1.schema.json").read_text(encoding="utf-8"))
                result_schema = json.loads((Path(__file__).resolve().parents[1] / "contracts/provided/trusted.test.result.v1.schema.json").read_text(encoding="utf-8"))
                Draft202012Validator(owner_schema, format_checker=FormatChecker(), registry=Registry().with_resource(
                    result_schema["$id"], Resource.from_contents(result_schema))).validate(signed)
                decision = consumer.evaluate(signed)
                self.assertEqual("APPROVED", decision["outcome"])
                self.assertEqual(decision, consumer.evaluate(copy.deepcopy(signed)))
                self.assertEqual([2, 1, 2], [len(fx.result_signatures), len(fx.request_signatures), len(fx.merge_calls)])
                self.assertEqual(fx.merge_calls[0], fx.merge_calls[1])
                payload = b"dps-merge-request-attestation/v1\n" + fx.merge_calls[0]
                self.assertEqual(sha256_bytes(payload), signed["runner_attestation"]["payload_sha256"])
                self.assertEqual(0, verify(payload, base64.b64decode(signed["runner_attestation"]["signature_value"])))
                print("P17B_REAL_BATCH " + json.dumps({"nonnull": nonnull, "result_signatures": 2,
                      "request_signatures": 1, "request_outputs": 1, "outcome": decision["outcome"],
                      "authorization": "TEST_ONLY", "rsa": "real OpenSSL SHA256 MGF1-SHA256 salt32"}), flush=True)

    def test_missing_verifier_and_caller_overrides_run_nothing(self):
        with self.fixture() as fx:
            fx.runner.merge_request_verifier = None
            with mock.patch.object(fx.runner, "run", wraps=fx.runner.run) as run:
                with self.assertRaisesRegex(TrustedRunnerError, "WAITING_EXTERNAL"):
                    fx.issue()
                run.assert_not_called()
            self.assertEqual([], fx.result_signatures + fx.request_signatures)
        for key in ("evidence", "runner_attestation", "required_checks", "roles", "merge_request_verifier", "occurred_at"):
            with self.subTest(key=key), self.fixture() as fx:
                fx.metadata[key] = []
                with mock.patch.object(fx.runner, "run", wraps=fx.runner.run) as run:
                    with self.assertRaises(TrustedRunnerError):
                        fx.issue()
                    run.assert_not_called()
                self.assertEqual([], fx.result_signatures + fx.request_signatures)

    def test_required_batch_is_exact_and_receipts_are_complete(self):
        for case in ("omitted", "duplicate", "extra", "receipt-hash", "receipt-id", "receipt-status", "receipt-diff", "receipt-missing", "receipt-extra", "receipt-duplicate"):
            with self.subTest(case=case), self.fixture() as fx:
                if case == "omitted": fx.work.pop()
                elif case in ("duplicate", "extra"): fx.work.append(copy.deepcopy(fx.work[0]))
                elif case == "receipt-missing": fx.metadata["instruction_receipts"].pop()
                elif case in ("receipt-extra", "receipt-duplicate"):
                    fx.metadata["instruction_receipts"].append(copy.deepcopy(fx.metadata["instruction_receipts"][0]))
                else:
                    field = {"receipt-hash": "receipt_sha256", "receipt-id": "receipt_id",
                             "receipt-status": "status", "receipt-diff": "diff_fingerprint"}[case]
                    fx.metadata["instruction_receipts"][0][field] = "0" * 64
                with self.assertRaises(TrustedRunnerError): fx.issue()
                self.assertEqual([], fx.result_signatures + fx.request_signatures)

    def test_verifier_wrong_fact_mutation_exception_and_post_rejection(self):
        for case in ("bool", "shape", "id", "hash", "unverified", "exception", "mutate-rehash", "post"):
            with self.subTest(case=case), self.fixture() as fx:
                def verifier(request, runner):
                    fact = fx.verify_merge(request, runner)
                    if case == "bool": return True
                    if case == "shape": return {}
                    if case == "id": fact["fact_id"] = "merge-other-id"
                    if case == "hash": fact["fact_sha256"] = "0" * 64
                    if case == "unverified": fact["verified"] = 1
                    if case == "exception": raise OSError("TEST_ONLY unavailable provider")
                    if case == "mutate-rehash":
                        request["branch_heads"][0]["commit"] = "0" * 40
                        fact["fact_sha256"] = sha256_bytes(canonical_bytes(request))
                    if case == "post" and len(fx.merge_calls) == 2: fact["verified"] = False
                    return fact
                fx.runner.merge_request_verifier = verifier
                with self.assertRaises(TrustedRunnerError): fx.issue()
                self.assertEqual(2, len(fx.result_signatures))
                self.assertEqual(1 if case == "post" else 0, len(fx.request_signatures))

    def test_test_only_provider_denies_each_authority_dimension(self):
        for case in ("scope", "branches", "diff", "paths", "conflicts", "receipts", "policy", "required", "revoked"):
            with self.subTest(case=case), self.fixture() as fx:
                if case == "required": fx.merge_policy["required_checks"].append("demo.other")
                elif case == "revoked": fx.authorized = False
                else:
                    field = {"scope": "soul_id", "branches": "branch_heads", "diff": "current_diff_fingerprint",
                             "paths": "changed_paths", "conflicts": "conflicts", "receipts": "instruction_receipts",
                             "policy": "trusted_policy_sha256"}[case]
                    fx.authorized_metadata[field] = "not-current-authorized-value"
                with self.assertRaisesRegex(TrustedRunnerError, "TEST_ONLY"): fx.issue()
                self.assertEqual(2, len(fx.result_signatures))
                self.assertEqual(0, len(fx.request_signatures))

    def test_first_work_item_drift_after_later_run_is_rejected(self):
        for case in ("auth", "receipt", "plan", "manifest", "head", "workspace", "lease", "fence", "policy"):
            with self.subTest(case=case), self.fixture() as fx:
                run = fx.runner.run
                completed = []
                def changed_run(*args, **kwargs):
                    result = run(*args, **kwargs)
                    completed.append(result)
                    if len(completed) == 2:
                        first = fx.work[0]
                        if case == "auth": fx.auth_hashes[first["request"]["auth_context_id"]] = "c" * 64
                        elif case == "receipt": first["instruction_receipt"]["status"] = "STALE"
                        elif case == "plan": first["worktree_plan"]["entries"][0]["writer_identity"] = "other-writer"
                        elif case == "manifest": fx.manifest_path.write_text("{}", encoding="utf-8")
                        elif case == "head": fx._git("-c", "user.name=DPS Test", "-c", "user.email=dps@example.invalid", "commit", "--allow-empty", "-qm", "changed head")
                        elif case == "workspace": fx.test_file.write_text("# persistent change\n", encoding="utf-8")
                        elif case == "lease": first["lease"]["status"] = "RELEASED"
                        elif case == "fence": fx.lease["lock_tokens"]["module:demo"] += 1
                        elif case == "policy": fx.policy.checks.pop("demo.unit")
                    return result
                with mock.patch.object(fx.runner, "run", new=changed_run):
                    with self.assertRaises(TrustedRunnerError): fx.issue()
                self.assertEqual(2, len(fx.result_signatures))
                self.assertEqual(0, len(fx.request_signatures))

    def test_pre_issuer_post_drift_expiry_and_backwards_clock(self):
        for phase in ("PRE", "ISSUER", "POST"):
            for case in ("input", "workspace", "auth", "fence", "expiry", "equal-expiry", "backwards", "revoked"):
                with self.subTest(phase=phase, case=case), self.fixture() as fx:
                    clock = MODULE._utc_now
                    current = [None]
                    def drift():
                        if case == "input": fx.work[0]["request"]["trace_id"] = "trace_" + "9" * 32
                        elif case == "workspace": fx.test_file.write_text("# issuer drift\n", encoding="utf-8")
                        elif case == "auth": fx.auth_hashes[fx.work[0]["request"]["auth_context_id"]] = "c" * 64
                        elif case == "fence": fx.lease["lock_tokens"]["module:demo"] += 1
                        elif case in ("expiry", "equal-expiry"):
                            expiry = dt.datetime.fromisoformat(fx.lease["expires_at"].replace("Z", "+00:00"))
                            current[0] = (expiry + dt.timedelta(seconds=case == "expiry")).isoformat()
                        elif case == "backwards": current[0] = fx.lease["acquired_at"]
                        elif case == "revoked": fx.authorized = False
                    def verifier(request, runner):
                        fact = fx.verify_merge(request, runner)
                        if (phase == "PRE" and len(fx.merge_calls) == 1) or (phase == "POST" and len(fx.merge_calls) == 2):
                            drift()
                            if case == "revoked": raise TrustedRunnerError("TEST_ONLY revoked during refresh")
                        return fact
                    def issuer(payload, runner, policy):
                        result = fx.issue_attestation(payload, runner, policy)
                        if phase == "ISSUER" and payload.startswith(b"dps-merge-request-attestation/v1\n"): drift()
                        return result
                    fx.runner.merge_request_verifier = verifier
                    fx.runner.attestation_issuer = issuer
                    with mock.patch.object(MODULE, "_utc_now", side_effect=lambda: current[0] or clock()):
                        with self.assertRaises(TrustedRunnerError): fx.issue()
                    self.assertEqual(2, len(fx.result_signatures))
                    self.assertEqual(0 if phase == "PRE" else 1, len(fx.request_signatures))

    def test_instance_reentry_concurrency_and_finally_release(self):
        import threading
        with self.fixture() as fx:
            refused = []
            def verifier(request, runner):
                with self.assertRaisesRegex(TrustedRunnerError, "already active"): fx.issue()
                def concurrent():
                    try: fx.issue()
                    except TrustedRunnerError as exc: refused.append(str(exc))
                thread = threading.Thread(target=concurrent)
                thread.start()
                thread.join(2)
                self.assertFalse(thread.is_alive())
                return fx.verify_merge(request, runner)
            fx.runner.merge_request_verifier = verifier
            fx.issue()
            self.assertEqual(2, len(refused))
            self.assertTrue(all("already active" in value for value in refused))
            fx.runner.merge_request_verifier = lambda *_args: (_ for _ in ()).throw(OSError("TEST_ONLY"))
            with self.assertRaises(TrustedRunnerError): fx.issue()
            fx.runner.merge_request_verifier = fx.verify_merge
            self.assertEqual("merge.request/v1", fx.issue()["contract_id"])

    def test_nonpass_batch_preserves_historical_result_counts(self):
        with self.fixture() as fx:
            execute = fx.runner._execute
            def failed_execute(*args, **kwargs):
                _code, out, err, timed_out, limited = execute(*args, **kwargs)
                return 1, out, err, timed_out, limited
            with mock.patch.object(fx.runner, "_execute", new=failed_execute):
                with self.assertRaisesRegex(TrustedRunnerError, "not release-allowed PASS"): fx.issue()
            self.assertEqual(1, len(fx.result_signatures))
            self.assertEqual(0, len(fx.request_signatures))


def assert_producer_corpus_case(test, case, fx):
    """The producer accepts proposals, never caller-signed wire requests or results."""
    owner_path = Path(__file__).resolve().parents[2] / "factory-merge-controller/tests/test_merge_controller.py"
    spec = importlib.util.spec_from_file_location("p17b_runner_owner_corpus_fixture", owner_path)
    owner = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(owner)
    owner.assert_owner_corpus_case(test, case)
    test.assertEqual("REJECTED_CALLER_WIRE_OR_RAW", case["expected_runner_boundary"])
    with test.assertRaises(TrustedRunnerError):
        fx.runner.issue_merge_request(copy.deepcopy(case["input"]), fx.work, fx.policy)


if __name__ == "__main__":
    unittest.main()
