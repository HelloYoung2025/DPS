"""All schema-applicable cases in the owner corpus; no provider or signature authority."""
import base64
import datetime
import json
import re
import unittest
from pathlib import Path

from jsonschema import Draft202012Validator, FormatChecker

PROVIDED = Path(__file__).resolve().parents[1] / "contracts" / "provided"
FAMILIES = (
    "soul.alias.verify.request/v1",
    "soul.alias.verification/v1",
    "soul.current.query/v1",
    "soul.current.resolution/v1",
)
CHECKER = FormatChecker()


@CHECKER.checks("date-time")
def real_calendar(value):
    if not isinstance(value, str):
        return True
    if not re.fullmatch(r"[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(?:\.[0-9]{1,7})?Z", value):
        return False
    try:
        datetime.datetime.fromisoformat(value)
    except ValueError:
        return False
    return True


def load(path):
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeError, ValueError) as error:
        raise AssertionError("Cannot read owner contract asset: " + str(path)) from error


class SoulIdentitySchemaTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.corpus = load(PROVIDED / "soul.identity.v1.corpus.json")
        cls.schemas = {family: load(PROVIDED / family.replace("/v1", ".v1.schema.json")) for family in FAMILIES}

    def test_schema_definitions(self):
        for family, schema in self.schemas.items():
            with self.subTest(family=family):
                Draft202012Validator.check_schema(schema)
                self.assertEqual(schema["title"], family)
                self.assertFalse(schema["additionalProperties"])
                self.assertEqual(set(schema["required"]), set(schema["properties"]))
                validator = Draft202012Validator(schema, format_checker=CHECKER)
                positive = next(c["payload"] for c in self.corpus["cases"] if c["id"] == family.replace("/v1", "") + ".valid")
                self.assertTrue(validator.is_valid(positive))
                invalid = dict(positive, occurred_at="2026-02-30T00:00:00.0000000Z")
                self.assertFalse(validator.is_valid(invalid))
        self.assertTrue(CHECKER.conforms("0001-01-01T00:00:00.0000000Z", "date-time"))
        self.assertTrue(CHECKER.conforms("2000-02-29T00:00:00.0000000Z", "date-time"))
        self.assertFalse(CHECKER.conforms("1900-02-29T00:00:00.0000000Z", "date-time"))
        self.assertFalse(CHECKER.conforms("2026-09-15T00:00:60.0000000Z", "date-time"))

    def test_corpus_inventory(self):
        cases = self.corpus["cases"]
        ids = [case["id"] for case in cases]
        self.assertEqual(ids, self.corpus["inventory"])
        self.assertEqual(len(ids), len(set(ids)))
        self.assertEqual(set(FAMILIES), {case["contract_id"] for case in cases})
        for family in FAMILIES:
            subset = [case for case in cases if case["contract_id"] == family]
            self.assertTrue(any(c["schema_expected"] is True for c in subset))
            self.assertTrue(any(c["schema_expected"] is False for c in subset))
            for field in self.schemas[family]["required"]:
                self.assertIn(family.replace("/v1", "") + ".missing." + field, ids)
            for case in subset:
                self.assertIs(type(case["codec_expected"]), bool)
                if case["schema_expected"] is None:
                    self.assertIn("raw_base64", case)
                    base64.b64decode(case["raw_base64"], validate=True)
                else:
                    self.assertIs(type(case["schema_expected"]), bool)
                    self.assertIn("payload", case)

    def check_family(self, family):
        validator = Draft202012Validator(self.schemas[family], format_checker=CHECKER)
        applicable = [case for case in self.corpus["cases"] if case["contract_id"] == family and case["schema_expected"] is not None]
        self.assertGreater(len(applicable), 0)
        for case in applicable:
            with self.subTest(case_id=case["id"]):
                errors = list(validator.iter_errors(case["payload"]))
                self.assertEqual(not errors, case["schema_expected"], case["id"] + ": " + "; ".join(e.message for e in errors))

    def test_alias_verify_request_schema_corpus(self):
        self.check_family("soul.alias.verify.request/v1")

    def test_alias_verification_schema_corpus(self):
        self.check_family("soul.alias.verification/v1")

    def test_current_query_schema_corpus(self):
        self.check_family("soul.current.query/v1")

    def test_current_resolution_schema_corpus(self):
        self.check_family("soul.current.resolution/v1")


if __name__ == "__main__":
    unittest.main()
