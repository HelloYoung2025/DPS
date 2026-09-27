import copy
import datetime as dt
import hashlib
import importlib.util
import json
import pathlib
import sys
import unittest

from jsonschema import Draft202012Validator, FormatChecker
from jsonschema.exceptions import ValidationError


MODULE_ROOT = pathlib.Path(__file__).resolve().parents[1]


def load_module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    assert spec.loader is not None
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


PIPELINE = load_module(
    "impact_contract_pipeline_fixture", MODULE_ROOT / "tests/test_impact_analyzer.py"
)
IMPACT = PIPELINE.IMPACT
REPOSITORY_ROOT = MODULE_ROOT.parents[1]
PLAN_V2_SCHEMA = (
    "Modules/factory-impact-analyzer/contracts/provided/module.change.plan.v2.schema.json"
)
PLAN_CORPUS_PATH = MODULE_ROOT / "contracts/provided/module.change.plan.v2.corpus.json"
RECEIPT_CORPUS_PATH = (
    MODULE_ROOT.parent
    / "factory-instruction-resolver/contracts/provided/instruction.receipt.v2.corpus.json"
)
CANONICAL_VECTOR_NAMES = ["non_ascii", "nested_key_order", "integers", "literals"]


def apply_corpus_mutations(value, mutations):
    value = copy.deepcopy(value)
    for mutation in mutations:
        target = value
        for key in mutation["path"][:-1]:
            target = target[key]
        if mutation["op"] == "remove":
            del target[mutation["path"][-1]]
        elif mutation["op"] == "replace":
            target[mutation["path"][-1]] = mutation["value"]
        else:
            raise AssertionError("unknown corpus mutation operation")
    return value


def owner_plan_digests(plan):
    """Recompute every undomained plan digest with the owner's production primitives."""
    without_hash = dict(plan)
    without_hash.pop("plan_sha256")
    without_id = dict(without_hash)
    without_id.pop("plan_id")
    return {
        "plan_sha256": IMPACT._sha256(IMPACT._canonical_bytes(without_hash)),
        "plan_id": "change:" + IMPACT._sha256(IMPACT._canonical_bytes(without_id))[:32],
        "instruction_scope_sha256": IMPACT._sha256(
            IMPACT._canonical_bytes(plan["instruction_scope"])
        ),
        "authorized_write_paths_sha256": IMPACT._sha256(
            IMPACT._canonical_bytes(plan["authorized_write_paths"])
        ),
        "write_scope_sha256": IMPACT._sha256(IMPACT._canonical_bytes({
            "write_modules": plan["write_modules"],
            "authorized_write_paths": plan["authorized_write_paths"],
        })),
    }


def canonical_bytes(value):
    return json.dumps(
        value, sort_keys=True, separators=(",", ":"), ensure_ascii=False
    ).encode("utf-8")


def strict_format_checker():
    checker = FormatChecker()

    @checker.checks("date-time")
    def is_datetime(value):
        try:
            parsed = dt.datetime.fromisoformat(value.replace("Z", "+00:00"))
        except (AttributeError, ValueError):
            return False
        return parsed.tzinfo is not None

    return checker


class ModuleChangePlanV2ContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.schema_path = (
            MODULE_ROOT / "contracts/provided/module.change.plan.v2.schema.json"
        )
        cls.schema = json.loads(cls.schema_path.read_text(encoding="utf-8"))
        Draft202012Validator.check_schema(cls.schema)
        cls.validator = Draft202012Validator(
            cls.schema, format_checker=strict_format_checker()
        )
        fixture = PIPELINE.PipelineFixture("additive-schema")
        try:
            cls.base_plan = fixture.analyze()
        finally:
            fixture.close()

    def production_plan(self, kind="additive-schema"):
        if kind == "additive-schema":
            return copy.deepcopy(self.base_plan)
        fixture = PIPELINE.PipelineFixture(kind)
        try:
            return fixture.analyze()
        finally:
            fixture.close()

    def test_real_analyzer_v2_output_validates_and_hashes_are_recomputable(self):
        plan = self.production_plan()
        self.validator.validate(plan)
        without_hash = dict(plan)
        supplied_hash = without_hash.pop("plan_sha256")
        self.assertEqual(
            hashlib.sha256(canonical_bytes(without_hash)).hexdigest(), supplied_hash
        )
        without_id = dict(without_hash)
        supplied_id = without_id.pop("plan_id")
        self.assertEqual(
            "change:" + hashlib.sha256(canonical_bytes(without_id)).hexdigest()[:32],
            supplied_id,
        )
        self.assertEqual(
            hashlib.sha256(canonical_bytes(plan["instruction_scope"])).hexdigest(),
            plan["instruction_scope_sha256"],
        )
        self.assertEqual(
            hashlib.sha256(
                canonical_bytes(plan["authorized_write_paths"])
            ).hexdigest(),
            plan["authorized_write_paths_sha256"],
        )
        self.assertEqual(
            hashlib.sha256(canonical_bytes({
                "write_modules": plan["write_modules"],
                "authorized_write_paths": plan["authorized_write_paths"],
            })).hexdigest(),
            plan["write_scope_sha256"],
        )

    def test_all_four_change_kind_outputs_validate(self):
        for kind in (
            "add-major", "additive-schema", "mode-transition",
            "introduce-quarantined-major",
        ):
            with self.subTest(kind=kind):
                self.validator.validate(self.production_plan(kind))

    def test_unknown_major_extra_field_and_missing_full_digest_fail(self):
        invalid = self.production_plan()
        invalid["contract_id"] = "module.change.plan/v3"
        with self.assertRaises(ValidationError):
            self.validator.validate(invalid)

        invalid = self.production_plan()
        invalid["shell_command"] = "not-a-contract-field"
        with self.assertRaises(ValidationError):
            self.validator.validate(invalid)

        invalid = self.production_plan()
        del invalid["instruction_receipt_sha256"]
        with self.assertRaises(ValidationError):
            self.validator.validate(invalid)

    def test_release_side_effect_and_expectation_escalation_fail_schema(self):
        for field, value in (
            ("release_eligible", True),
            ("side_effects_authorized", True),
            ("portable_trust_status", "PORTABLE_VERIFIED"),
            ("shadow_side_effect_count", 1),
            ("source_contract_change_claims_status", "VERIFIED"),
            ("changeset_contract_verification_required", False),
        ):
            with self.subTest(field=field):
                invalid = self.production_plan()
                invalid[field] = value
                with self.assertRaises(ValidationError):
                    self.validator.validate(invalid)

        invalid = self.production_plan("mode-transition")
        expectation = invalid["bound_contract_change_expectations"][0]
        expectation["expected_mode"] = "active"
        expectation["expected_status"] = "active"
        with self.assertRaises(ValidationError):
            self.validator.validate(invalid)

    def test_invalid_identity_path_digest_and_role_fail_closed(self):
        invalid = self.production_plan()
        invalid["device_binding_id"] = "device-without-prefix"
        with self.assertRaises(ValidationError):
            self.validator.validate(invalid)

        invalid = self.production_plan()
        invalid["authorized_write_paths"][0] = "../escape.py"
        with self.assertRaises(ValidationError):
            self.validator.validate(invalid)

        invalid = self.production_plan()
        invalid["plan_sha256"] += "\n"
        with self.assertRaises(ValidationError):
            self.validator.validate(invalid)

        invalid = self.production_plan()
        invalid["role_assignments"]["release_approver"] = []
        with self.assertRaises(ValidationError):
            self.validator.validate(invalid)

    def test_v1_contract_is_byte_frozen_and_v2_is_distinct(self):
        v1 = (
            MODULE_ROOT / "contracts/provided/module.change.plan.v1.schema.json"
        ).read_bytes()
        self.assertEqual(
            "dd504c558c87aa06ee429cc5c62029c48afc537286b838926120436a302bc0e6",
            hashlib.sha256(v1).hexdigest(),
        )
        self.assertNotEqual(
            hashlib.sha256(v1).hexdigest(),
            hashlib.sha256(self.schema_path.read_bytes()).hexdigest(),
        )

    def load_corpus(self, path):
        self.assertTrue(path.is_file(), "owned contract corpus is missing: %s" % path.name)
        return json.loads(path.read_text(encoding="utf-8"))

    def test_freeze_schema_bytes_are_pinned_by_owned_corpus(self):
        template = self.load_corpus(PLAN_CORPUS_PATH)["templates"]["v2_schema"]
        self.assertEqual(
            ("module.change.plan", 2, PLAN_V2_SCHEMA),
            (template["contract_id"], template["major"], template["expected_source"]),
        )
        self.assertEqual(
            template["expected_source_sha256"],
            hashlib.sha256((REPOSITORY_ROOT / PLAN_V2_SCHEMA).read_bytes()).hexdigest(),
        )

    def test_freeze_positive_plans_recompute_owner_digests(self):
        corpus = self.load_corpus(PLAN_CORPUS_PATH)
        receipts = {
            case["name"]: case
            for case in self.load_corpus(RECEIPT_CORPUS_PATH)["valid"]
        }
        self.assertTrue(corpus["valid"])
        for case in corpus["valid"]:
            with self.subTest(case=case["name"]):
                plan = case["plan"]
                self.validator.validate(plan)
                IMPACT._schema_validate(
                    copy.deepcopy(plan), IMPACT._PLAN_VALIDATOR, "corpus plan"
                )
                self.assertEqual(corpus["baseline_commit"], plan["baseline_commit"])
                for field, digest in owner_plan_digests(plan).items():
                    self.assertEqual(case[field], digest, field)
                    self.assertEqual(digest, plan[field], field)
                receipt = receipts[case["name"]]
                self.assertEqual(
                    receipt["receipt_sha256"], plan["instruction_receipt_sha256"]
                )
                self.assertEqual(receipt["receipt_id"], plan["instruction_receipt_id"])
                self.assertEqual(
                    receipt["source_contract_change_claims_sha256"],
                    case["source_contract_change_claims_sha256"],
                )
                self.assertEqual(
                    case["source_contract_change_claims_sha256"],
                    plan["source_contract_change_claims_sha256"],
                )

    def test_freeze_negative_plans_fail_schema_or_owner_recompute(self):
        corpus = self.load_corpus(PLAN_CORPUS_PATH)
        valid = {case["name"]: case["plan"] for case in corpus["valid"]}
        self.assertTrue(corpus["invalid"])
        for case in corpus["invalid"]:
            with self.subTest(case=case["name"]):
                value = apply_corpus_mutations(valid[case["base"]], case["mutations"])
                self.assertEqual(
                    case["schema_rejects"],
                    bool(list(self.validator.iter_errors(value))),
                )
                self.assertIn(case["rejected_by"], {"schema", "owner-recompute"})
                if case["rejected_by"] == "schema":
                    self.assertTrue(case["schema_rejects"])
                    with self.assertRaises(IMPACT.ImpactError):
                        IMPACT._schema_validate(
                            value, IMPACT._PLAN_VALIDATOR, "corpus negative"
                        )
                else:
                    self.assertFalse(case["schema_rejects"])
                    mismatched = [
                        field for field, digest in owner_plan_digests(value).items()
                        if digest != value[field]
                    ]
                    self.assertTrue(mismatched)

    def test_freeze_canonicalizer_vectors(self):
        vectors = self.load_corpus(PLAN_CORPUS_PATH)["canonical_vectors"]
        self.assertEqual(CANONICAL_VECTOR_NAMES, [vector["name"] for vector in vectors])
        for vector in vectors:
            with self.subTest(vector=vector["name"]):
                actual = IMPACT._canonical_bytes(vector["value"])
                self.assertEqual(vector["canonical"].encode("utf-8"), actual)
                self.assertEqual(vector["sha256"], hashlib.sha256(actual).hexdigest())

    def test_freeze_canonicalizer_rejects_non_finite_numbers(self):
        for value in (float("nan"), float("inf"), {"a": float("-inf")}):
            with self.assertRaises(IMPACT.ImpactError):
                IMPACT._canonical_bytes(value)


if __name__ == "__main__":
    unittest.main()
