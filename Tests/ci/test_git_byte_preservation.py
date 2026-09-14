#!/usr/bin/env python3
"""Real Git round trips and whitespace checks for preserved source bytes."""

from __future__ import annotations

import subprocess
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
PRESERVED_PATHS = (
    "Config/Apps.json",
    "Config/BehaviorConfig.json",
    "Config/EvolutionRules.json",
    "Config/ExtensionsConfig.json",
    "Config/MaintenanceConfig.json",
    "Config/StageConfig.json",
    "Config/ValidationRules.json",
    "Core/README.md",
    "Modules/Decision/README.md",
    "Modules/Persona/README.md",
    "Modules/README.md",
    "Modules/Report/README.md",
    "ZDProjects/README.md",
    "Modules/zenno-bridge/tests/Invoke-WindowsZennoGate.ps1",
    "Modules/SessionRunner.cs",
    "Modules/Core/SmartOrchestrator.cs",
    "ZDProjects/Initializer_OwnCode.cs",
)


class GitBytePreservationTests(unittest.TestCase):
    def git(self, root: Path, *args: str, check: bool = True):
        return subprocess.run(
            ["git", "-C", str(root), *args],
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            check=check,
        )

    def initialize(self, root: Path):
        root.mkdir()
        self.git(root, "init", "-q")
        (root / ".gitattributes").write_bytes((ROOT / ".gitattributes").read_bytes())
        self.git(root, "add", "--", ".gitattributes")

    def test_normal_commit_and_checkout_preserve_existing_bytes(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary) / "source"
            self.initialize(root)
            expected = {path: (ROOT / path).read_bytes() for path in PRESERVED_PATHS}
            for relative, payload in expected.items():
                target = root / relative
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes(payload)
            self.git(root, "add", "--", *expected)
            self.git(
                root, "-c", "user.name=DPS Test", "-c",
                "user.email=dps-test@example.invalid", "commit", "-qm", "fixture",
            )
            restored = Path(temporary) / "restored"
            self.git(root, "clone", "--no-local", "--no-hardlinks", str(root), str(restored))
            for relative, payload in expected.items():
                with self.subTest(path=relative):
                    self.assertEqual(payload, (restored / relative).read_bytes())
            self.assertEqual(b"", self.git(restored, "status", "--porcelain").stdout)

    def test_crlf_exception_keeps_whitespace_errors_detectable(self):
        cases = (
            ("ZDProjects/Example.cs", b"class Example {}\r\n", True),
            ("ZDProjects/Example.cs", b"class Example {} \r\n", False),
            ("ZDProjects/Example.cs", b"class Example {}\r\n\r\n", False),
            ("Modules/planner/src/Example.cs", b"class Example {} \n", False),
            ("Config/new.json", b"{} \n", False),
        )
        for relative, payload, should_pass in cases:
            with self.subTest(path=relative, payload=payload):
                with tempfile.TemporaryDirectory() as temporary:
                    root = Path(temporary) / "repo"
                    self.initialize(root)
                    target = root / relative
                    target.parent.mkdir(parents=True, exist_ok=True)
                    target.write_bytes(payload)
                    self.git(root, "add", "--", relative)
                    result = self.git(root, "diff", "--cached", "--check", check=False)
                    self.assertEqual(should_pass, result.returncode == 0, result.stdout)

    def test_new_modern_files_keep_declared_normalization(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary) / "repo"
            self.initialize(root)
            for relative in ("Modules/planner/src/New.cs", "Config/new.json"):
                target = root / relative
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes(b"first\r\nsecond\r\n")
                self.git(root, "add", "--", relative)
                self.assertEqual(b"first\nsecond\n", self.git(root, "show", ":" + relative).stdout)


if __name__ == "__main__":
    unittest.main()
