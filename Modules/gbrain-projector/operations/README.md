# gbrain-projector operations

The module is proposed and not production release eligible. Runtime routing may change only through an exact signed Release BOM.

Rollout order is synthetic test, shadow, bounded canary, rolling waves, and soak. Shadow is forbidden from producing real-world side effects. The feature flag, kill switch, canary rule, rollback target, minimum evidence, and human approval boundary are machine truth in ../module.yaml.

Rollback restores routing and the previous compatible signed BOM. It must not pretend that external side effects or destructive data changes were reversed; those require stop, reconcile, compensate, or restore procedures.

The v2 rollout flag is `gbrain_projector_v2`; the kill switch is `kill_gbrain_projector`. Canary prerequisites include real PostgreSQL migration evidence, exact v2 consumer compatibility, Source binding collision/concurrency/restart tests, and append-only revision evidence. Frozen v1 is quarantine-only and is not a rollback runtime target.

Database rollout uses two independently managed direct-login credentials to the same database. Set `DPS_TEST_POSTGRES_ADMIN_URI` only for migration/integration infrastructure and `DPS_TEST_POSTGRES_RUNTIME_URI` only for the application runtime. Never copy the migration connection into runtime configuration, use `SET ROLE`, or log either value. The migrator proves the shared database lock namespace before DDL. Bind its exact `MigrationRole` into the signed runtime configuration as `ExpectedMigrationRole`; a runtime cannot infer or substitute the owner. Every connection pins `search_path` to `pg_catalog, pg_temp`, requires effective `session_replication_role=origin`, and the runtime role must have neither `SET` nor `ALTER SYSTEM` privilege for that parameter. Run the migrator first, retain its created-versus-verified-existing result, then start the runtime and require its attestation before routing traffic.

Every migration and runtime connection must first read `pg_catalog.current_setting('server_version_num')` and match exact `180004`; `18.x`, another 18.4 patch, and compatibility claims are rejected before identity inspection, DDL, or module data access. The required negative control uses test-only `DPS_TEST_POSTGRES_WRONG_VERSION_URI` and never belongs in runtime configuration or a Release BOM.

An existing schema is accepted only when every expected relation/type, table, column/default, PK, UNIQUE, quote-aware CHECK expression, FK target/action/internal trigger, index, application trigger/function, exact signed owner, rewrite rule/publication state, and effective ACL matches. An unexpected view, standalone type, rule, policy, publication, role-membership edge, disabled RI trigger, weak object, or partly created schema is an operator-visible stop condition, not something the migrator repairs. Schema or ACL drift after initialization requires immediate kill-switch activation, routing stop, forensic export, and restoration from the last signed database/BOM procedure.

Nonce exhaustion, missing INSERT read-back, binding checksum drift, cross-Soul Source reuse, sub-microsecond persistence input, or persisted revision ambiguity stops allocation/rendering and records quarantine where possible. Operators must not bypass the unique constraint or rewrite binding/revision rows.

The provider currently advertises v2 while repository consumers still retain v1 declarations. This reciprocal compatibility failure remains a release blocker; do not turn the matrix green by weakening the provider or silently editing consumers.
