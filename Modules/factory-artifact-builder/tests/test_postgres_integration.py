from __future__ import annotations

import hashlib
import importlib.util
import os
import sys
import unittest
from pathlib import Path

try:
    import psycopg
except ImportError as exc:  # Required suite reports this as INFRA_ERROR below.
    psycopg = None
    PSYCOPG_IMPORT_ERROR = exc
else:
    PSYCOPG_IMPORT_ERROR = None


MODULE_ROOT = Path(__file__).resolve().parents[1]
SOURCE_ROOT = MODULE_ROOT / "src"
SOURCE_FILE = SOURCE_ROOT / "artifact_builder.py"
MIGRATION = MODULE_ROOT / "migrations" / "001_build_identity_claims.sql"
if SOURCE_ROOT.is_symlink() or SOURCE_FILE.is_symlink() or MIGRATION.is_symlink():
    raise ImportError("artifact-builder PostgreSQL test inputs must not use symbolic links")
RESOLVED_SOURCE = SOURCE_FILE.resolve(strict=True)
RESOLVED_MIGRATION = MIGRATION.resolve(strict=True)
for path in (RESOLVED_SOURCE, RESOLVED_MIGRATION):
    try:
        path.relative_to(MODULE_ROOT)
    except ValueError as exc:
        raise ImportError("artifact-builder PostgreSQL test input escaped its module") from exc

SUBJECT_NAME = "dps_factory_artifact_builder_postgres_subject"
if SUBJECT_NAME in sys.modules:
    raise ImportError("artifact-builder PostgreSQL test subject is already loaded")
SPEC = importlib.util.spec_from_file_location(SUBJECT_NAME, RESOLVED_SOURCE)
if SPEC is None or SPEC.loader is None:
    raise ImportError("artifact-builder PostgreSQL subject loader is unavailable")
SUBJECT = importlib.util.module_from_spec(SPEC)
sys.modules[SUBJECT_NAME] = SUBJECT
try:
    SPEC.loader.exec_module(SUBJECT)
except BaseException:
    sys.modules.pop(SUBJECT_NAME, None)
    raise

ArtifactBuildError = SUBJECT.ArtifactBuildError
PostgresBuildIdentityRegistry = SUBJECT.PostgresBuildIdentityRegistry


def claim(build_id="build-pg-001", artifact_sha256="3" * 64):
    return {
        "schema_version": "dps.artifact-build-identity-claim/v1",
        "build_id": build_id,
        "request_sha256": "1" * 64,
        "decision_sha256": "2" * 64,
        "artifact_sha256": artifact_sha256,
        "source_tree_sha256": "4" * 64,
        "module_id": "module-one",
        "module_version": "1.2.3",
        "integration_commit": "5" * 40,
    }


class PostgresBuildIdentityIntegrationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.admin_dsn = os.environ.get("DPS_ARTIFACT_BUILDER_ADMIN_DATABASE_URL")
        cls.runtime_dsn = os.environ.get("DPS_ARTIFACT_BUILDER_RUNTIME_DATABASE_URL")
        missing = []
        if not cls.admin_dsn:
            missing.append("DPS_ARTIFACT_BUILDER_ADMIN_DATABASE_URL")
        if not cls.runtime_dsn:
            missing.append("DPS_ARTIFACT_BUILDER_RUNTIME_DATABASE_URL")
        if psycopg is None:
            missing.append(f"locked psycopg driver ({PSYCOPG_IMPORT_ERROR})")
        if missing:
            raise RuntimeError(
                "INFRA_ERROR: required PostgreSQL build-identity inputs missing: "
                + ", ".join(missing)
            )
        if psycopg.__version__ != "3.3.4":
            raise RuntimeError(
                "INFRA_ERROR: hash-locked psycopg 3.3.4 required; got " + psycopg.__version__
            )

        identities = []
        databases = []
        for label, dsn in (("admin", cls.admin_dsn), ("runtime", cls.runtime_dsn)):
            try:
                with psycopg.connect(dsn) as connection:
                    version = str(connection.execute("SHOW server_version_num").fetchone()[0])
                    identity = connection.execute(
                        "SELECT current_user, session_user"
                    ).fetchone()
                    if identity[0] != identity[1]:
                        raise RuntimeError(
                            f"INFRA_ERROR: PostgreSQL {label} URL must directly authenticate its role"
                        )
                    identities.append(str(identity[0]))
                    databases.append(str(connection.execute("SELECT current_database()").fetchone()[0]))
                    if label == "runtime":
                        role_flags = connection.execute(
                            "SELECT rolcanlogin, rolinherit, rolsuper, rolcreaterole, rolcreatedb, "
                            "rolreplication, rolbypassrls FROM pg_roles WHERE rolname = current_user"
                        ).fetchone()
            except Exception as exc:
                raise RuntimeError(f"INFRA_ERROR: PostgreSQL {label} identity is unreachable") from exc
            if version != "180004":
                raise RuntimeError(
                    f"INFRA_ERROR: PostgreSQL 18.4 required for {label}; got {version}"
                )
        if identities[0] == identities[1]:
            raise RuntimeError("INFRA_ERROR: migration and runtime PostgreSQL roles must be distinct")
        if databases[0] != databases[1]:
            raise RuntimeError("INFRA_ERROR: migration and runtime roles must address one dedicated database")
        cls.admin_role, cls.runtime_role = identities
        if role_flags != (True, False, False, False, False, False, False):
            raise RuntimeError(
                "INFRA_ERROR: runtime PostgreSQL role must be a direct NOINHERIT login and unprivileged"
            )

        with psycopg.connect(cls.admin_dsn, autocommit=True) as connection:
            membership = connection.execute(
                "SELECT 1 FROM pg_auth_members membership "
                "JOIN pg_roles granted ON granted.oid = membership.roleid "
                "JOIN pg_roles member ON member.oid = membership.member "
                "WHERE (granted.rolname = %s AND member.rolname = %s) "
                "OR (granted.rolname = %s AND member.rolname = %s)",
                (cls.admin_role, cls.runtime_role, cls.runtime_role, cls.admin_role),
            ).fetchone()
            if membership is not None:
                raise RuntimeError(
                    "INFRA_ERROR: migration and runtime PostgreSQL roles must have no membership edge"
                )
            existing = connection.execute(
                "SELECT 1 FROM pg_namespace WHERE nspname = 'factory_artifact'"
            ).fetchone()
            if existing is not None:
                raise RuntimeError(
                    "INFRA_ERROR: dedicated artifact-builder database already contains factory_artifact"
                )
            database_identifier = psycopg.sql.Identifier(databases[0])
            runtime_identifier = psycopg.sql.Identifier(cls.runtime_role)
            connection.execute(
                psycopg.sql.SQL(
                    "REVOKE CREATE, TEMPORARY ON DATABASE {} FROM PUBLIC"
                ).format(database_identifier)
            )
            connection.execute(
                psycopg.sql.SQL(
                    "REVOKE CREATE, TEMPORARY ON DATABASE {} FROM {}"
                ).format(database_identifier, runtime_identifier)
            )
            connection.execute(RESOLVED_MIGRATION.read_text(encoding="utf-8"))
            connection.execute(
                "GRANT USAGE ON SCHEMA factory_artifact TO "
                + psycopg.sql.Identifier(cls.runtime_role).as_string(connection)
            )
            connection.execute(
                psycopg.sql.SQL(
                    "GRANT EXECUTE ON FUNCTION "
                    "factory_artifact.claim_build_identity(text,text,text,text,text,text,text,text,text) TO {}"
                ).format(psycopg.sql.Identifier(cls.runtime_role))
            )
            runtime_database_boundary = connection.execute(
                "SELECT database.datdba=(SELECT oid FROM pg_roles WHERE rolname=%s), "
                "has_database_privilege(%s, current_database(), 'CREATE'), "
                "has_database_privilege(%s, current_database(), 'TEMP'), "
                "EXISTS (SELECT 1 FROM pg_namespace namespace "
                " WHERE has_schema_privilege(%s, namespace.oid, 'CREATE')) "
                "FROM pg_database database WHERE database.datname=current_database()",
                (
                    cls.runtime_role, cls.runtime_role, cls.runtime_role,
                    cls.runtime_role,
                ),
            ).fetchone()
            if runtime_database_boundary != (False, False, False, False):
                raise RuntimeError(
                    "INFRA_ERROR: runtime role owns or can create database/schema objects"
                )

    @classmethod
    def tearDownClass(cls):
        if psycopg is not None and getattr(cls, "admin_dsn", None):
            try:
                with psycopg.connect(cls.admin_dsn, autocommit=True) as connection:
                    connection.execute("DROP SCHEMA IF EXISTS factory_artifact CASCADE")
            except Exception:
                pass

    def registry(self):
        return PostgresBuildIdentityRegistry(lambda: psycopg.connect(self.runtime_dsn))

    def test_exact_retry_survives_new_registry_and_connection(self):
        value = claim()
        self.registry().claim(value)
        self.registry().claim(value)
        with psycopg.connect(self.admin_dsn) as connection:
            row = connection.execute(
                "SELECT request_sha256, artifact_sha256 FROM factory_artifact.build_identity_claim "
                "WHERE build_id = %s",
                (value["build_id"],),
            ).fetchone()
        self.assertEqual((value["request_sha256"], value["artifact_sha256"]), row)

    def test_same_build_id_with_different_artifact_fails_closed(self):
        value = claim("build-pg-002")
        self.registry().claim(value)
        conflict = dict(value)
        conflict["artifact_sha256"] = "9" * 64
        with self.assertRaisesRegex(ArtifactBuildError, "already claimed"):
            self.registry().claim(conflict)

    def test_concurrent_conflicting_claims_have_one_durable_winner(self):
        first = claim("build-pg-003", "a" * 64)
        second = claim("build-pg-003", "b" * 64)
        outcomes = []

        import concurrent.futures

        def run(value):
            try:
                self.registry().claim(value)
                return "PASS"
            except ArtifactBuildError:
                return "CONFLICT"

        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as executor:
            outcomes = list(executor.map(run, (first, second)))
        self.assertEqual(["CONFLICT", "PASS"], sorted(outcomes))

    def test_runtime_cannot_read_write_or_mutate_claim_table(self):
        value = claim("build-pg-004")
        self.registry().claim(value)
        statements = (
            "SELECT * FROM factory_artifact.build_identity_claim",
            "INSERT INTO factory_artifact.build_identity_claim "
            "(build_id,claim_sha256,request_sha256,decision_sha256,artifact_sha256,"
            "source_tree_sha256,module_id,module_version,integration_commit) VALUES "
            "('build-raw-001','%s','%s','%s','%s','%s','module-one','1.2.3','%s')"
            % ("0" * 64, "1" * 64, "2" * 64, "3" * 64, "4" * 64, "5" * 40),
            "UPDATE factory_artifact.build_identity_claim SET artifact_sha256='%s'" % ("9" * 64),
            "DELETE FROM factory_artifact.build_identity_claim",
            "TRUNCATE factory_artifact.build_identity_claim",
            "ALTER TABLE factory_artifact.build_identity_claim ADD COLUMN forbidden integer",
            "CREATE TABLE factory_artifact.forbidden(value integer)",
        )
        for statement in statements:
            with self.subTest(statement=hashlib.sha256(statement.encode()).hexdigest()):
                with self.assertRaises(psycopg.Error):
                    with psycopg.connect(self.runtime_dsn) as connection:
                        connection.execute(statement)

    def test_catalog_proves_owner_security_definer_acl_and_append_only_guards(self):
        with psycopg.connect(self.admin_dsn) as connection:
            durability = connection.execute(
                "SELECT current_setting('server_version_num'), "
                "current_setting('fsync'), current_setting('full_page_writes')"
            ).fetchone()
            table_owner = connection.execute(
                "SELECT owner.rolname FROM pg_class relation "
                "JOIN pg_namespace namespace ON namespace.oid = relation.relnamespace "
                "JOIN pg_roles owner ON owner.oid = relation.relowner "
                "WHERE namespace.nspname='factory_artifact' "
                "AND relation.relname='build_identity_claim'"
            ).fetchone()
            function_row = connection.execute(
                "SELECT owner.rolname, procedure.prosecdef, procedure.proconfig, "
                "procedure.prosrc, obj_description(procedure.oid, 'pg_proc') "
                "FROM pg_proc procedure "
                "JOIN pg_namespace namespace ON namespace.oid = procedure.pronamespace "
                "JOIN pg_roles owner ON owner.oid = procedure.proowner "
                "WHERE namespace.nspname='factory_artifact' "
                "AND procedure.proname='claim_build_identity'"
            ).fetchone()
            mutation_row = connection.execute(
                "SELECT owner.rolname, procedure.prosecdef, procedure.proconfig, "
                "procedure.prosrc, obj_description(procedure.oid, 'pg_proc') "
                "FROM pg_proc procedure "
                "JOIN pg_namespace namespace ON namespace.oid = procedure.pronamespace "
                "JOIN pg_roles owner ON owner.oid = procedure.proowner "
                "WHERE namespace.nspname='factory_artifact' "
                "AND procedure.proname='reject_build_identity_mutation'"
            ).fetchone()
            trigger_rows = [
                tuple(row)
                for row in connection.execute(
                    "SELECT trigger.tgname, trigger.tgtype, trigger.tgenabled "
                    "FROM pg_trigger trigger "
                    "JOIN pg_class relation ON relation.oid = trigger.tgrelid "
                    "JOIN pg_namespace namespace ON namespace.oid = relation.relnamespace "
                    "WHERE namespace.nspname='factory_artifact' "
                    "AND relation.relname='build_identity_claim' AND NOT trigger.tgisinternal"
                    " ORDER BY trigger.tgname"
                ).fetchall()
            ]
            runtime_table_privilege = connection.execute(
                "SELECT bool_or(has_table_privilege(%s, "
                "'factory_artifact.build_identity_claim', privilege)) "
                "FROM unnest(ARRAY['SELECT','INSERT','UPDATE','DELETE','TRUNCATE',"
                "'REFERENCES','TRIGGER']) AS privileges(privilege)",
                (self.runtime_role,),
            ).fetchone()[0]
            explicit_column_acl = connection.execute(
                "SELECT EXISTS (SELECT 1 FROM pg_attribute attribute "
                "WHERE attribute.attrelid="
                "'factory_artifact.build_identity_claim'::regclass "
                "AND attribute.attnum>0 AND NOT attribute.attisdropped "
                "AND COALESCE(array_length(attribute.attacl, 1), 0)<>0)"
            ).fetchone()[0]
            constraint_rows = tuple(
                tuple(row)
                for row in connection.execute(
                    "SELECT constraint_row.conname, constraint_row.contype, "
                    "constraint_row.convalidated, constraint_row.condeferrable, "
                    "constraint_row.condeferred, constraint_row.conislocal, "
                    "constraint_row.coninhcount, constraint_row.conparentid=0, "
                    "array_to_string(constraint_row.conkey, ','), "
                    "pg_get_constraintdef(constraint_row.oid, false) "
                    "FROM pg_constraint constraint_row "
                    "WHERE constraint_row.conrelid="
                    "'factory_artifact.build_identity_claim'::regclass "
                    "ORDER BY constraint_row.conname"
                ).fetchall()
            )
            index_rows = tuple(
                tuple(row)
                for row in connection.execute(
                    "SELECT index_relation.relname, access_method.amname, "
                    "index_row.indisunique, index_row.indisprimary, "
                    "index_row.indisvalid, index_row.indisready, "
                    "index_row.indislive, index_row.indimmediate, "
                    "index_row.indisclustered, index_row.indisexclusion, "
                    "index_row.indnatts, index_row.indnkeyatts, "
                    "index_row.indkey::text, "
                    "pg_get_expr(index_row.indexprs, index_row.indrelid), "
                    "pg_get_expr(index_row.indpred, index_row.indrelid) "
                    "FROM pg_index index_row "
                    "JOIN pg_class index_relation "
                    "ON index_relation.oid=index_row.indexrelid "
                    "JOIN pg_am access_method "
                    "ON access_method.oid=index_relation.relam "
                    "WHERE index_row.indrelid="
                    "'factory_artifact.build_identity_claim'::regclass "
                    "ORDER BY index_relation.relname"
                ).fetchall()
            )
            public_function_privilege = connection.execute(
                "SELECT EXISTS ("
                "SELECT 1 FROM pg_proc procedure "
                "JOIN pg_namespace namespace ON namespace.oid=procedure.pronamespace, "
                "LATERAL aclexplode(COALESCE(procedure.proacl, "
                "acldefault('f', procedure.proowner))) acl "
                "WHERE namespace.nspname='factory_artifact' "
                "AND procedure.proname='claim_build_identity' "
                "AND acl.grantee=0 AND acl.privilege_type='EXECUTE')"
            ).fetchone()[0]
            runtime_function_privilege = connection.execute(
                "SELECT has_function_privilege(%s, "
                "'factory_artifact.claim_build_identity(text,text,text,text,text,text,text,text,text)', "
                "'EXECUTE')",
                (self.runtime_role,),
            ).fetchone()[0]
            object_comments = connection.execute(
                "SELECT obj_description(namespace.oid, 'pg_namespace'), "
                "obj_description(relation.oid, 'pg_class') "
                "FROM pg_namespace namespace JOIN pg_class relation "
                "ON relation.relnamespace=namespace.oid "
                "WHERE namespace.nspname='factory_artifact' "
                "AND relation.relname='build_identity_claim'"
            ).fetchone()

        self.assertEqual(("180004", "on", "on"), durability)
        self.assertEqual((self.admin_role,), table_owner)
        self.assertEqual(self.admin_role, function_row[0])
        self.assertIs(True, function_row[1])
        self.assertEqual(["search_path=pg_catalog"], function_row[2])
        self.assertEqual(SUBJECT._CLAIM_FUNCTION_PROSRC_SHA256, hashlib.sha256(
            function_row[3].strip().encode("utf-8")
        ).hexdigest())
        self.assertEqual(self.admin_role, mutation_row[0])
        self.assertIs(False, mutation_row[1])
        self.assertIsNone(mutation_row[2])
        self.assertEqual(SUBJECT._MUTATION_FUNCTION_PROSRC_SHA256, hashlib.sha256(
            mutation_row[3].strip().encode("utf-8")
        ).hexdigest())
        self.assertEqual((SUBJECT._SCHEMA_ATTESTATION,) * 2, object_comments)
        self.assertEqual(SUBJECT._SCHEMA_ATTESTATION, function_row[4])
        self.assertEqual(SUBJECT._SCHEMA_ATTESTATION, mutation_row[4])
        self.assertEqual(
            [
                ("reject_build_identity_truncate", 34, "O"),
                ("reject_build_identity_update_delete", 27, "O"),
            ],
            trigger_rows,
        )
        self.assertIs(False, runtime_table_privilege)
        self.assertIs(False, explicit_column_acl)
        self.assertEqual(SUBJECT._EXPECTED_BUILD_IDENTITY_CONSTRAINTS, constraint_rows)
        self.assertEqual(SUBJECT._EXPECTED_BUILD_IDENTITY_INDEXES, index_rows)
        self.assertIs(False, public_function_privilege)
        self.assertIs(True, runtime_function_privilege)

    def test_registry_rejects_column_acl_or_extra_index_catalog_drift(self):
        runtime_identifier = psycopg.sql.Identifier(self.runtime_role)
        with psycopg.connect(self.admin_dsn, autocommit=True) as connection:
            connection.execute(
                psycopg.sql.SQL(
                    "GRANT SELECT(build_id) ON "
                    "factory_artifact.build_identity_claim TO {}"
                ).format(runtime_identifier)
            )
        try:
            with self.assertRaisesRegex(ArtifactBuildError, "ACL"):
                self.registry().claim(claim("build-pg-column-acl"))
        finally:
            with psycopg.connect(self.admin_dsn, autocommit=True) as connection:
                connection.execute(
                    psycopg.sql.SQL(
                        "REVOKE SELECT(build_id) ON "
                        "factory_artifact.build_identity_claim FROM {}"
                    ).format(runtime_identifier)
                )

        with psycopg.connect(self.admin_dsn, autocommit=True) as connection:
            connection.execute(
                "CREATE UNIQUE INDEX forbidden_build_artifact_identity_idx ON "
                "factory_artifact.build_identity_claim (build_id, artifact_sha256)"
            )
        try:
            with self.assertRaisesRegex(ArtifactBuildError, "index set"):
                self.registry().claim(claim("build-pg-extra-index"))
        finally:
            with psycopg.connect(self.admin_dsn, autocommit=True) as connection:
                connection.execute(
                    "DROP INDEX IF EXISTS "
                    "factory_artifact.forbidden_build_artifact_identity_idx"
                )

    def test_registry_rejects_weakened_check_and_restores_exact_definition(self):
        constraint = "build_identity_claim_build_id_check"
        with psycopg.connect(self.admin_dsn, autocommit=True) as connection:
            connection.execute(
                "ALTER TABLE factory_artifact.build_identity_claim "
                f"DROP CONSTRAINT {constraint}"
            )
            connection.execute(
                "ALTER TABLE factory_artifact.build_identity_claim "
                f"ADD CONSTRAINT {constraint} CHECK (true)"
            )
        try:
            with self.assertRaisesRegex(ArtifactBuildError, "constraints"):
                self.registry().claim(claim("build-pg-weakened-check"))
        finally:
            with psycopg.connect(self.admin_dsn, autocommit=True) as connection:
                connection.execute(
                    "ALTER TABLE factory_artifact.build_identity_claim "
                    f"DROP CONSTRAINT {constraint}"
                )
                connection.execute(
                    "ALTER TABLE factory_artifact.build_identity_claim "
                    f"ADD CONSTRAINT {constraint} "
                    "CHECK (build_id ~ '^[a-z0-9][a-z0-9._:-]{7,127}$')"
                )
        self.registry().claim(claim("build-pg-check-restored"))

    def test_admin_cannot_bypass_append_only_triggers_without_ddl(self):
        value = claim("build-pg-005")
        self.registry().claim(value)
        for statement in (
            "UPDATE factory_artifact.build_identity_claim SET artifact_sha256='%s' "
            "WHERE build_id='%s'" % ("9" * 64, value["build_id"]),
            "DELETE FROM factory_artifact.build_identity_claim WHERE build_id='%s'" % value["build_id"],
            "TRUNCATE factory_artifact.build_identity_claim",
        ):
            with self.subTest(statement=hashlib.sha256(statement.encode()).hexdigest()):
                with self.assertRaises(psycopg.Error):
                    with psycopg.connect(self.admin_dsn) as connection:
                        connection.execute(statement)


if __name__ == "__main__":
    unittest.main()
