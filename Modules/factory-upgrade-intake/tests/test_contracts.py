import copy
import datetime as dt
import hashlib
import importlib.util
import json
import pathlib
import sys
import unittest

from jsonschema import Draft202012Validator, FormatChecker


MODULE_ROOT = pathlib.Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location(
    "factory_upgrade_intake_contract_subject",
    MODULE_ROOT / "src" / "upgrade_intake.py",
)
SUBJECT = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
sys.modules[SPEC.name] = SUBJECT
SPEC.loader.exec_module(SUBJECT)

BASELINE = "a" * 40
OWNERSHIP = {
    "factory-upgrade-intake": ["Modules/factory-upgrade-intake/**"]
}
FROZEN_HASH_DOMAINS = {
    "component": "dps.upgrade-intent/v2/public-contract-changes",
    "quarantine_evidence": "dps.upgrade-intent/v2/quarantine-import-evidence",
    "manifest_ownership": "dps.manifest-ownership/v1",
    "requester_auth_context": "dps.requester-auth-context/v1",
    "approval_subject": "dps.upgrade-intent/v2/approval-subject",
    "full_intent": "dps.upgrade-intent/v2/full-intent",
}
CANONICAL_VECTOR_NAMES = ["non_ascii", "nested_key_order", "integers", "literals"]


class AuthPort(SUBJECT.AuthVerificationPort):
    def verify(self, record):
        return record.get("verification_material") == {"signature": "contract-fixture"}


class ManifestPort(SUBJECT.ManifestOwnershipVerificationPort):
    def verify(self, baseline_commit, snapshot_sha256, receipt_id):
        return (
            baseline_commit == BASELINE
            and len(snapshot_sha256) == 64
            and receipt_id == "manifest:contract0001"
        )


class UpgradeIntentContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.schema_path = (
            MODULE_ROOT / "contracts" / "provided" / "upgrade.intent.v2.schema.json"
        )
        cls.v1_path = (
            MODULE_ROOT / "contracts" / "provided" / "upgrade.intent.v1.schema.json"
        )
        cls.corpus_path = (
            MODULE_ROOT / "contracts" / "provided" / "upgrade.intent.v2.corpus.json"
        )
        cls.schema = json.loads(cls.schema_path.read_text(encoding="utf-8"))
        Draft202012Validator.check_schema(cls.schema)
        cls.validator = Draft202012Validator(
            cls.schema, format_checker=FormatChecker()
        )
        cls.corpus = json.loads(cls.corpus_path.read_text(encoding="utf-8"))

    def setUp(self):
        self.now = dt.datetime(2026, 7, 15, 0, 5, tzinfo=dt.timezone.utc)
        self.auth_authority = SUBJECT.ProcessBoundAuthAuthority(
            AuthPort(), clock=lambda: self.now
        )
        self.manifest_authority = SUBJECT.ProcessBoundManifestAuthority(
            ManifestPort()
        )
        self.ownership = self.manifest_authority.verify(
            BASELINE, OWNERSHIP, "manifest:contract0001"
        )
        self.auth = self.auth_authority.verify({
            "context_id": "authctx:contract0001",
            "subject": "module-implementer-1",
            "role": "module-implementer",
            "audience": "dps.factory-upgrade-intake",
            "issued_at": "2026-07-15T00:00:00Z",
            "expires_at": "2026-07-15T00:10:00Z",
            "nonce": "nonce_" + "4" * 32,
            "receipt_id": "auth:contract0001",
            "approvals": [],
            "verification_material": {"signature": "contract-fixture"},
        })

    def changes_from_refs(self, refs):
        return [copy.deepcopy(self.corpus["templates"][name]) for name in refs]

    def apply_mutations(self, changes, mutations):
        for mutation in mutations:
            target = changes[mutation["index"]]
            if mutation["op"] == "remove":
                del target[mutation["field"]]
            elif mutation["op"] == "replace":
                target[mutation["field"]] = mutation["value"]
            else:
                self.fail("unknown corpus mutation operation")
        return changes

    def base_intent(self, changes, *, digest=None, omit_contract_sources=False):
        try:
            changes = sorted(changes, key=SUBJECT._contract_change_sort_key)
        except (KeyError, TypeError):
            changes = list(changes)
        paths = ["Modules/factory-upgrade-intake/src/upgrade_intake.py"]
        if not omit_contract_sources:
            paths.extend(
                item["expected_source"]
                for item in changes
                if isinstance(item, dict) and isinstance(item.get("expected_source"), str)
            )
        value = {
            "schema_version": "dps.upgrade-intent/v2",
            "contract_id": "upgrade.intent/v2",
            "producer_module": "factory-upgrade-intake",
            "soul_id": None,
            "device_binding_id": None,
            "platform_account_id": None,
            "trace_id": "trace_" + "5" * 32,
            "idempotency_key": "idem_" + "6" * 64,
            "occurred_at": "2026-07-15T00:04:00Z",
            "privacy_class": "internal",
            "intent_id": "intent:contract0001",
            "auth_context_id": self.auth.context_id,
            "requester_auth_context_sha256": self.auth.requester_context_sha256,
            "requester_auth_receipt_id": self.auth.receipt_id,
            "requester_auth_nonce": self.auth.nonce,
            "baseline_commit": BASELINE,
            "manifest_ownership_sha256": self.ownership.snapshot_sha256,
            "manifest_ownership_receipt_id": self.ownership.receipt_id,
            "target_modules": ["factory-upgrade-intake"],
            "requested_paths": sorted(set(paths)),
            "public_contract_changes": changes,
            "public_contract_changes_sha256": digest or "0" * 64,
            "contract_change_claims_status": "UNVERIFIED_EXPECTATIONS",
            "baseline_verification_required": True,
            "approval_subject_sha256": "0" * 64,
            "upgrade_intent_sha256": "0" * 64,
            "requested_risk_tier": "R1",
            "requested_stage": "development",
            "requester": {
                "identity": self.auth.subject,
                "role": self.auth.role,
            },
            "authorization": {
                "status": "not-required",
                "approved_by": None,
                "approver_role": "not-applicable",
                "approval_scope": [],
                "approval_receipt_id": None,
                "approval_nonce": None,
                "approved_at": None,
                "approval_expires_at": None,
            },
        }
        value["approval_subject_sha256"] = SUBJECT.approval_subject_sha256(value)
        value["upgrade_intent_sha256"] = SUBJECT.upgrade_intent_sha256(value)
        return value

    def valid_intent(self, changes=None):
        changes = list(changes or [])
        digest = SUBJECT.public_contract_changes_sha256(
            changes,
            ["factory-upgrade-intake"],
            self.ownership,
            BASELINE,
            self.manifest_authority,
        )
        return self.base_intent(changes, digest=digest)

    def production_validate(self, value):
        return SUBJECT.validate_upgrade_intent(
            value,
            self.auth,
            self.ownership,
            self.auth_authority,
            self.manifest_authority,
        )

    def rebind_full_digests(self, value):
        value["approval_subject_sha256"] = SUBJECT.approval_subject_sha256(value)
        value["upgrade_intent_sha256"] = SUBJECT.upgrade_intent_sha256(value)
        return value

    def test_production_normalized_output_and_wire_roundtrip_match_schema(self):
        changes = self.changes_from_refs(["v2_add_major", "v1_quarantine_import"])
        value = self.valid_intent(changes)
        normalized = self.production_validate(value)
        self.validator.validate(normalized)
        encoded = SUBJECT.encode_upgrade_intent_v2(
            value, self.auth, self.ownership,
            self.auth_authority, self.manifest_authority,
        )
        decoded = SUBJECT.decode_upgrade_intent_v2(
            encoded, self.auth, self.ownership,
            self.auth_authority, self.manifest_authority,
        )
        self.assertEqual(normalized, decoded)

    def test_owned_positive_corpus_has_exact_schema_source_and_hash_domains(self):
        self.assertEqual(BASELINE, self.corpus["baseline_commit"])
        self.assertEqual(
            self.ownership.snapshot_sha256,
            self.corpus["manifest_ownership_sha256"],
        )
        self.assertEqual(
            hashlib.sha256(self.schema_path.read_bytes()).hexdigest(),
            self.corpus["templates"]["v2_add_major"]["expected_source_sha256"],
        )
        self.assertEqual(
            "01d57cbd2c2aef67c216c7375950cf324159e0c823a1e52290aae22efd81862a",
            self.corpus["templates"]["v1_quarantine_import"]["expected_source_sha256"],
        )
        for case in self.corpus["valid"]:
            with self.subTest(case=case["name"]):
                changes = self.changes_from_refs(case["template_refs"])
                actual_digest = SUBJECT.public_contract_changes_sha256(
                    changes, ["factory-upgrade-intake"], self.ownership,
                    BASELINE, self.manifest_authority,
                )
                self.assertEqual(case["public_contract_changes_sha256"], actual_digest)
                value = self.base_intent(changes, digest=actual_digest)
                self.validator.validate(value)
                self.production_validate(value)

    def test_negative_corpus_asserts_schema_and_production_independently(self):
        for case in self.corpus["invalid"]:
            with self.subTest(case=case["name"]):
                changes = self.changes_from_refs(case["template_refs"])
                self.apply_mutations(changes, case["mutations"])
                value = self.base_intent(
                    changes,
                    digest="0" * 64,
                    omit_contract_sources=case.get(
                        "omit_contract_sources_from_requested_paths", False
                    ),
                )
                schema_failed = bool(list(self.validator.iter_errors(value)))
                self.assertEqual(
                    case["schema_rejects"], schema_failed,
                    "schema expectation drifted for %s" % case["name"],
                )
                with self.assertRaises(
                    SUBJECT.IntentValidationError,
                    msg="production accepted invalid case %s" % case["name"],
                ):
                    self.production_validate(value)

    def test_top_level_schema_and_runtime_attacks_both_fail(self):
        attacks = []
        value = self.valid_intent()
        value["occurred_at"] = "2026-07-15T08:04:00+08:00"
        attacks.append(value)
        value = self.valid_intent()
        value["requested_paths"] = ["Modules/factory-upgrade-intake/*.py"]
        attacks.append(value)
        value = self.valid_intent()
        value["requested_risk_tier"] = "R4"
        attacks.append(value)
        value = self.valid_intent()
        value["authorization"]["status"] = "pending"
        attacks.append(value)
        value = self.valid_intent()
        value["model_shell"] = "forbidden"
        attacks.append(value)
        value = self.valid_intent()
        del value["manifest_ownership_sha256"]
        attacks.append(value)
        for attacked in attacks:
            self.rebind_full_digests(attacked)
            with self.subTest(keys=sorted(attacked)):
                self.assertTrue(list(self.validator.iter_errors(attacked)))
                with self.assertRaises(SUBJECT.IntentValidationError):
                    self.production_validate(attacked)

    def test_component_hash_uses_domain_separation_and_snapshot_binding(self):
        changes = self.changes_from_refs(["v2_add_major"])
        canonical = SUBJECT.canonical_public_contract_changes(
            changes, ["factory-upgrade-intake"], self.ownership,
            BASELINE, self.manifest_authority,
        )
        expected = hashlib.sha256(
            b"DPS\x00dps.upgrade-intent/v2/public-contract-changes\x00" + canonical
        ).hexdigest()
        self.assertEqual(
            expected,
            SUBJECT.public_contract_changes_sha256(
                changes, ["factory-upgrade-intake"], self.ownership,
                BASELINE, self.manifest_authority,
            ),
        )
        self.assertIn(self.ownership.snapshot_sha256.encode("ascii"), canonical)

    def test_v1_bytes_are_frozen_and_only_quarantine_routing_is_available(self):
        self.assertEqual(
            "01d57cbd2c2aef67c216c7375950cf324159e0c823a1e52290aae22efd81862a",
            hashlib.sha256(self.v1_path.read_bytes()).hexdigest(),
        )
        legacy = json.dumps({
            "schema_version": "dps.upgrade-intent/v1",
            "contract_id": "upgrade.intent/v1",
            "producer_module": "factory-upgrade-intake",
            "unknown": {"model_command": "never execute"},
        }, separators=(",", ":"), sort_keys=True).encode("utf-8")
        quarantined = SUBJECT.quarantine_upgrade_intent_v1(legacy)
        self.assertEqual("quarantine-only", quarantined.contract_mode)
        self.assertTrue(list(self.validator.iter_errors(json.loads(legacy))))
        self.assertFalse(hasattr(SUBJECT, "encode_upgrade_intent_v1"))

    def test_freeze_hash_domains_are_complete_and_bound_to_production(self):
        self.assertEqual(FROZEN_HASH_DOMAINS, self.corpus["hash_domains"])
        domains = self.corpus["hash_domains"]

        def framed(domain, material):
            return hashlib.sha256(
                b"DPS\x00" + domain.encode("ascii") + b"\x00"
                + SUBJECT._canonical_json_bytes(material)
            ).hexdigest()

        changes = self.changes_from_refs(["v2_add_major", "v1_quarantine_import"])
        canonical = SUBJECT.canonical_public_contract_changes(
            changes, ["factory-upgrade-intake"], self.ownership,
            BASELINE, self.manifest_authority,
        )
        self.assertEqual(
            hashlib.sha256(
                b"DPS\x00" + domains["component"].encode("ascii") + b"\x00" + canonical
            ).hexdigest(),
            SUBJECT.public_contract_changes_sha256(
                changes, ["factory-upgrade-intake"], self.ownership,
                BASELINE, self.manifest_authority,
            ),
        )
        quarantine = self.corpus["templates"]["v1_quarantine_import"]
        self.assertEqual(
            quarantine["quarantine_evidence_sha256"],
            framed(domains["quarantine_evidence"], {
                key: quarantine[key] for key in (
                    "baseline_commit", "contract_id", "major", "expected_source",
                    "expected_source_sha256", "quarantine_reason",
                )
            }),
        )
        self.assertEqual(
            self.corpus["manifest_ownership_sha256"],
            framed(domains["manifest_ownership"], {
                "baseline_commit": BASELINE,
                "ownership": SUBJECT._normalize_ownership(OWNERSHIP, set()),
            }),
        )
        vector = self.corpus["intent_vectors"][0]
        record = vector["requester_auth_record"]
        self.assertEqual(
            vector["requester_auth_context_sha256"],
            framed(domains["requester_auth_context"], {
                key: record[key] for key in (
                    "context_id", "subject", "role", "audience",
                    "issued_at", "expires_at", "nonce", "receipt_id",
                )
            }),
        )
        intent = vector["intent"]
        self.assertEqual(
            vector["approval_subject_sha256"],
            framed(domains["approval_subject"], {
                key: value for key, value in intent.items()
                if key not in {"authorization", "approval_subject_sha256", "upgrade_intent_sha256"}
            }),
        )
        self.assertEqual(
            vector["upgrade_intent_sha256"],
            framed(domains["full_intent"], {
                key: value for key, value in intent.items()
                if key != "upgrade_intent_sha256"
            }),
        )

    def test_freeze_intent_digest_vectors_replay_production(self):
        self.assertIn("intent_vectors", self.corpus)
        valid = {case["name"]: case for case in self.corpus["valid"]}
        self.assertEqual(
            sorted(valid),
            sorted(vector["valid_ref"] for vector in self.corpus["intent_vectors"]),
        )
        for vector in self.corpus["intent_vectors"]:
            with self.subTest(case=vector["valid_ref"]):
                auth = self.auth_authority.verify(
                    copy.deepcopy(vector["requester_auth_record"])
                )
                self.assertEqual(
                    vector["requester_auth_context_sha256"],
                    auth.requester_context_sha256,
                )
                intent = vector["intent"]
                self.assertEqual(
                    valid[vector["valid_ref"]]["public_contract_changes_sha256"],
                    intent["public_contract_changes_sha256"],
                )
                for field, function in (
                    ("approval_subject_sha256", SUBJECT.approval_subject_sha256),
                    ("upgrade_intent_sha256", SUBJECT.upgrade_intent_sha256),
                ):
                    self.assertEqual(vector[field], function(intent))
                    self.assertEqual(vector[field], intent[field])
                self.assertEqual(intent, SUBJECT.validate_upgrade_intent(
                    intent, auth, self.ownership,
                    self.auth_authority, self.manifest_authority,
                ))
                wire = SUBJECT.encode_upgrade_intent_v2(
                    intent, auth, self.ownership,
                    self.auth_authority, self.manifest_authority,
                )
                self.assertEqual(vector["wire_sha256"], hashlib.sha256(wire).hexdigest())

    def test_freeze_canonicalizer_vectors(self):
        self.assertIn("canonical_vectors", self.corpus)
        vectors = self.corpus["canonical_vectors"]
        self.assertEqual(CANONICAL_VECTOR_NAMES, [vector["name"] for vector in vectors])
        for vector in vectors:
            with self.subTest(vector=vector["name"]):
                actual = SUBJECT._canonical_json_bytes(vector["value"])
                self.assertEqual(vector["canonical"].encode("utf-8"), actual)
                self.assertEqual(vector["sha256"], hashlib.sha256(actual).hexdigest())

    def test_freeze_canonicalizer_rejects_non_finite_numbers(self):
        for value in (float("nan"), float("inf"), {"a": float("-inf")}):
            with self.assertRaises(SUBJECT.IntentValidationError):
                SUBJECT._canonical_json_bytes(value)


if __name__ == "__main__":
    unittest.main()
