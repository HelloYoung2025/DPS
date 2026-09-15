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


if __name__ == "__main__":
    unittest.main()
