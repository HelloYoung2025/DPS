import hashlib
import importlib.util
import json
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock


ROOT = Path(__file__).resolve().parents[2]
VERIFIER_PATH = ROOT / (
    "Modules/legacy-runtime-adapter/operations/strangler/"
    "verify_sessionrunner_baseline.py"
)
SPEC = importlib.util.spec_from_file_location(
    "legacy_sessionrunner_strangler_verifier", VERIFIER_PATH
)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError("cannot load the SessionRunner strangler verifier")
VERIFIER = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(VERIFIER)


class SessionRunnerStranglerBaselineTests(unittest.TestCase):
    def git(self, root, *args):
        return subprocess.run(
            ["git", "-C", str(root), *args],
            check=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
        ).stdout.decode("ascii").strip()

    def write_json(self, path, value):
        path.write_text(
            json.dumps(value, ensure_ascii=False, indent=2) + "\n",
            encoding="utf-8",
        )

    def stage_repository(self):
        temporary = tempfile.TemporaryDirectory(prefix="dps-sessionrunner-")
        self.addCleanup(temporary.cleanup)
        base = Path(temporary.name).resolve()
        root = base / "repo"
        head = self.git(ROOT, "rev-parse", "HEAD^{commit}")
        subprocess.run(
            ["git", "clone", "--no-local", "--no-checkout", "--quiet", str(ROOT), str(root)],
            check=True,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
        )
        self.git(root, "checkout", "--detach", "--quiet", head)
        self.assertEqual(head, self.git(root, "rev-parse", "HEAD^{commit}"))
        self.assertEqual("false", self.git(root, "rev-parse", "--is-shallow-repository"))
        self.assertTrue((root / ".git").is_dir())
        self.assertFalse((root / ".git/objects/info/alternates").exists())

        provider = base / "synthetic-provider"
        provider.mkdir()
        anchor_path = provider / "legacy-baseline-anchor.json"

        def restore_permissions():
            if provider.exists():
                provider.chmod(0o755)
            if anchor_path.exists():
                anchor_path.chmod(0o644)

        self.addCleanup(restore_permissions)
        return root, anchor_path

    def freeze_synthetic_anchor(self, root, anchor_path):
        """Bind a test-only provider record to the real cloned Git history and bytes."""
        artifact = root / VERIFIER.ARTIFACT_RELATIVE
        manifest = json.loads((artifact / VERIFIER.LEGACY_BYTES_FILE).read_text(encoding="utf-8"))
        commit = manifest["baseline_commit"]
        inventory_errors = []
        inventory = VERIFIER._baseline_git_inventory(root, commit, inventory_errors)
        self.assertEqual([], inventory_errors)
        self.assertEqual(VERIFIER.EXPECTED_LEGACY_CSHARP_COUNT, len(inventory))

        repair_bindings = dict(VERIFIER.EXPECTED_APPROVAL_BINDINGS)
        repair_bindings.update(VERIFIER.EXPECTED_CONTAINMENT_BINDINGS)
        repair_bindings.update(VERIFIER.EXPECTED_ENTRYPOINT_CONTAINMENT_BINDINGS)
        self.assertEqual(21, len(repair_bindings))
        repair_records = {}
        for relative, (disposition, approval_ref) in repair_bindings.items():
            blob, baseline_bytes = inventory[relative]
            working = (root / relative).read_bytes()
            self.assertNotEqual(baseline_bytes, working, relative)
            repair_records[relative] = {
                "path": relative,
                "baseline_git_blob": blob,
                "baseline_sha256": hashlib.sha256(baseline_bytes).hexdigest(),
                "baseline_byte_length": len(baseline_bytes),
                "working_sha256": hashlib.sha256(working).hexdigest(),
                "working_byte_length": len(working),
                "disposition": disposition,
                "approval_ref": approval_ref,
            }

        parents = self.git(root, "rev-list", "--parents", "-n", "1", commit).split()[1:]
        protected_files = []
        for relative in sorted(VERIFIER.TRUSTED_PROTECTED_PATHS):
            content = (root / relative).read_bytes()
            protected_files.append({
                "path": relative,
                "sha256": hashlib.sha256(content).hexdigest(),
                "byte_length": len(content),
            })
        anchor = {
            "schema_version": VERIFIER.TRUSTED_ANCHOR_SCHEMA_VERSION,
            "anchor_id": "",
            "provider_id": VERIFIER.TRUSTED_PROVIDER_ID,
            "audience": VERIFIER.TRUSTED_AUDIENCE,
            "issued_at": "2026-07-15T00:00:00Z",
            "baseline_commit": commit,
            "baseline_tree": self.git(root, "rev-parse", commit + "^{tree}"),
            "baseline_parents": parents,
            "inventory_scope": "TRACKED_LEGACY_CSHARP_PLUS_APPROVED_REPAIRS",
            "inventory_count": VERIFIER.EXPECTED_LEGACY_CSHARP_COUNT,
            "inventory_digest_algorithm": "sha256-canonical-json-v1",
            "inventory_sha256": VERIFIER.legacy_inventory_sha256(inventory),
            "approved_repair_count": VERIFIER.EXPECTED_APPROVED_REPAIR_COUNT,
            "approved_repairs": [
                repair_records[path] for path in sorted(VERIFIER.EXPECTED_APPROVAL_BINDINGS)
            ],
            "containment_repair_count": VERIFIER.EXPECTED_CONTAINMENT_REPAIR_COUNT,
            "containment_repairs": [
                repair_records[path] for path in sorted(VERIFIER.EXPECTED_CONTAINMENT_BINDINGS)
            ],
            "entrypoint_containment_repair_count": VERIFIER.EXPECTED_ENTRYPOINT_CONTAINMENT_REPAIR_COUNT,
            "entrypoint_containment_repairs": [
                repair_records[path]
                for path in sorted(VERIFIER.EXPECTED_ENTRYPOINT_CONTAINMENT_BINDINGS)
            ],
            "protected_files": protected_files,
            "test_requirements": [
                {"path": path, "minimum_test_methods": minimum}
                for path, minimum in sorted(VERIFIER.EXPECTED_TEST_MINIMUMS.items())
            ],
            "issued_by": VERIFIER.TRUSTED_ISSUER,
            "limitations": ["Synthetic provider fixture only.", "No runtime or release evidence."],
        }
        anchor["anchor_id"] = VERIFIER.trusted_anchor_id(anchor)
        self.write_json(anchor_path, anchor)
        anchor_path.chmod(0o444)
        anchor_path.parent.chmod(0o555)

    def verify_with_synthetic_anchor(self, root, anchor_path):
        actual_uid = os.geteuid()
        with mock.patch.object(VERIFIER.os, "geteuid", return_value=actual_uid + 1):
            return VERIFIER.verify_repository(root, anchor_path)

    def assert_semantic_rejection(self, result, *expected_fragments):
        self.assertEqual("FAIL", result["status"])
        self.assertFalse(result["ok"])
        errors = result["errors"]
        for fragment in expected_fragments:
            self.assertTrue(any(fragment in error for error in errors), errors)
        for fragment in (
            "trusted anchor",
            "protected path",
            "legacy C# byte baseline",
            "legacy C# working",
            "legacy C# changed",
            "entrypoint inventory",
            "captured_from",
            "snapshot_id does not bind",
        ):
            self.assertFalse(any(fragment in error for error in errors), errors)

    def test_current_baseline_passes_with_test_only_anchor_without_runtime_claim(self):
        root, anchor_path = self.stage_repository()
        self.freeze_synthetic_anchor(root, anchor_path)
        result = self.verify_with_synthetic_anchor(root, anchor_path)
        self.assertEqual("PASS", result["status"], result["errors"])
        self.assertTrue(result["ok"])
        self.assertEqual([], result["errors"])
        self.assertEqual("STATIC_CHARACTERIZATION_ONLY", result["scope"])
        self.assertEqual(79, result["legacy_csharp_count"])
        self.assertEqual(21, result["approved_legacy_change_count"])
        self.assertEqual(39, result["legacy_entrypoint_count"])
        self.assertEqual(5, result["method_count"])
        self.assertEqual(2, result["synthetic_trace_count"])
        self.assertTrue(any("No Windows" in item for item in result["limitations"]))
        self.assertTrue(any("device evidence" in item for item in result["limitations"]))

    def test_signature_hash_tamper_fails_closed(self):
        root, anchor_path = self.stage_repository()
        snapshot_path = root / VERIFIER.ARTIFACT_RELATIVE / VERIFIER.SNAPSHOT_FILE
        snapshot = json.loads(snapshot_path.read_text(encoding="utf-8"))
        snapshot["methods"][0]["signature_sha256"] = "0" * 64
        self.write_json(snapshot_path, snapshot)
        self.freeze_synthetic_anchor(root, anchor_path)
        result = self.verify_with_synthetic_anchor(root, anchor_path)
        self.assert_semantic_rejection(result, "signature_sha256")

    def test_duplicate_declaration_fails_even_if_source_hash_is_rebound(self):
        root, anchor_path = self.stage_repository()
        source_path = root / VERIFIER.SOURCE_RELATIVE
        source_bytes = source_path.read_bytes() + (
            b"\npublic static string Run(object projectObj, object instanceObj)\n"
        )
        source_path.write_bytes(source_bytes)
        source_hash = hashlib.sha256(source_bytes).hexdigest()

        manifest_path = root / VERIFIER.ARTIFACT_RELATIVE / VERIFIER.LEGACY_BYTES_FILE
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
        source_entry = next(
            entry for entry in manifest["entries"]
            if entry["path"] == VERIFIER.SOURCE_RELATIVE.as_posix()
        )
        source_entry["working_sha256"] = source_hash
        source_entry["working_byte_length"] = len(source_bytes)
        self.write_json(manifest_path, manifest)

        snapshot_path = root / VERIFIER.ARTIFACT_RELATIVE / VERIFIER.SNAPSHOT_FILE
        snapshot = json.loads(snapshot_path.read_text(encoding="utf-8"))
        snapshot["snapshot_id"] = "legacy-sessionrunner:" + source_hash
        snapshot["captured_from"].update({
            "git_blob": VERIFIER.git_blob_sha1(source_bytes),
            "sha256": source_hash,
            "byte_length": len(source_bytes),
        })
        self.write_json(snapshot_path, snapshot)
        for trace_path in (root / VERIFIER.ARTIFACT_RELATIVE / "golden-traces").glob("*.json"):
            trace = json.loads(trace_path.read_text(encoding="utf-8"))
            trace["source_snapshot_id"] = snapshot["snapshot_id"]
            self.write_json(trace_path, trace)
        self.freeze_synthetic_anchor(root, anchor_path)
        result = self.verify_with_synthetic_anchor(root, anchor_path)
        self.assert_semantic_rejection(result, "Run declaration must occur exactly once")

    def test_synthetic_trace_cannot_be_relabelled_as_runtime_evidence(self):
        root, anchor_path = self.stage_repository()
        trace_path = root / VERIFIER.ARTIFACT_RELATIVE / (
            "golden-traces/device-unavailable.synthetic.v1.json"
        )
        trace = json.loads(trace_path.read_text(encoding="utf-8"))
        trace["runtime_observed"] = True
        trace["evidence_eligibility"] = ["DEVICE_VERIFIED"]
        self.write_json(trace_path, trace)
        self.freeze_synthetic_anchor(root, anchor_path)
        result = self.verify_with_synthetic_anchor(root, anchor_path)
        self.assert_semantic_rejection(
            result, "cannot claim runtime observation", "cannot satisfy a verification level"
        )

    def test_schema_weakening_fails_closed(self):
        root, anchor_path = self.stage_repository()
        schema_path = root / VERIFIER.ARTIFACT_RELATIVE / VERIFIER.TRACE_SCHEMA_FILE
        schema = json.loads(schema_path.read_text(encoding="utf-8"))
        schema["additionalProperties"] = True
        self.write_json(schema_path, schema)
        self.freeze_synthetic_anchor(root, anchor_path)
        result = self.verify_with_synthetic_anchor(root, anchor_path)
        self.assert_semantic_rejection(result, "reject unknown top-level fields")

    def test_protected_file_tamper_after_anchor_freeze_fails_closed(self):
        root, anchor_path = self.stage_repository()
        self.freeze_synthetic_anchor(root, anchor_path)
        snapshot_path = root / VERIFIER.ARTIFACT_RELATIVE / VERIFIER.SNAPSHOT_FILE
        snapshot_path.write_bytes(snapshot_path.read_bytes() + b"\n")
        result = self.verify_with_synthetic_anchor(root, anchor_path)
        self.assertEqual("FAIL", result["status"])
        self.assertTrue(any(
            "protected path hash differs from external anchor" in error
            for error in result["errors"]
        ), result["errors"])

    def test_real_cli_without_anchor_remains_waiting_external(self):
        environment = os.environ.copy()
        environment.pop(VERIFIER.TRUSTED_ANCHOR_ENV, None)
        process = subprocess.run(
            [sys.executable, "-I", str(VERIFIER_PATH), "--root", str(ROOT)],
            env=environment,
            check=False,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
        )
        self.assertEqual(3, process.returncode, process.stderr.decode("utf-8"))
        result = json.loads(process.stdout.decode("utf-8"))
        self.assertEqual("WAITING_EXTERNAL", result["status"])
        self.assertFalse(result["ok"])
        self.assertIsNone(result["trusted_anchor"])


if __name__ == "__main__":
    unittest.main()
