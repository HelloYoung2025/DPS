# soul-registry tests

This directory separates unit, contract, integration, resilience, Windows, and device evidence. The exact required suites and commands are declared in ../module.yaml.

A required check releases only on PASS. FAIL, SKIP, PARTIAL, NOT_RUN, INFRA_ERROR, NOT_APPLICABLE, an empty test set, or missing raw evidence is a failure. Mock evidence must be labelled and cannot satisfy integration, Windows, ZennoDroid, ADB, device, canary, or scale gates.

The first implementation slice is side-effect free. PostgreSQL integration must use a real PostgreSQL 18.4 instance; an in-memory replacement cannot satisfy the required integration suite.

Receipt rotation regressions cover register, resolve and revoke against the real database, including unchanged stored facts, all registration hash fields, missing or replaced recorded keys, explicit historical key sets and immutable metadata. `LegacyReceiptMigrationTests.cs` also imports frozen synthetic old-runtime rows into the original schema before the additive migration, then calls the normal replay APIs. Importing this historical state tests migration compatibility; it does not provide identity or release authority. The old positive Soul-to-Ledger test is retained and remains required while its v2 consumer migration is pending.

The three multi-key cases replay old receipts under a new three-key lookup ring. No historical set and an incomplete set containing only `key-v1` must both fail; the complete old `[key-v1, key-v2]` set must return the original projection and preserve every stored row. The result still names `key-v1`, proving that the result alias key alone is not the legacy request-hash basis.

The five `fixtures/legacy-receipts/*.json` files are exact bytes exported by the production Soul implementation at commit `1831a40f65bba2b629b267489220d6b0f896d04d`, with synthetic fixture key `key-v1` containing 32 bytes of `0x21`. The requests use the existing `Register`, `Resolve` and `Revoke` test helpers with alias `rotation.replay@example.test`, suffixes `rotation-register`, `rotation-resolve` and `rotation-revoke`, and revoke reason `verified correction`. The exporter invoked the old APIs and only read their stored rows and returned contracts; it did not disable triggers or issue authority. These frozen rows contain only synthetic pseudonymous identifiers and digests. Do not regenerate them using the new implementation.

| Frozen fixture | SHA-256 |
| --- | --- |
| register.resolved-receipt.json | `823401b2357206583f584aad2f000ae22252725b1f03c7ef372441582cd919dd` |
| register.stored-rows.json | `dfe02a979826e4e93f28b5e6538b4cae055ef26989848c889fba32caedd6bd1f` |
| resolve.resolved-receipt.json | `f5bb4381b3f47122f42994787df1d9b39132d57bfa0a3a4553c687f35c0cb81a` |
| resolve.stored-rows.json | `7fb71211c64ecfe40eaa0bd016df15c6eb99181a18b64401445213fe918c9a6b` |
| revoke.stored-rows.json | `a3a605bb9aeac32a4734871c255fe6228beee3992bcd964503304bd260524850` |

The five `multi-` fixtures were separately exported by the same frozen old production commit, using the same requests, current key `key-v1`, and keyring `[key-v1, key-v2]` with 32-byte fill patterns `0x21` and `0x22`. Independent reconstruction of the original request-hash projection matched all three stored two-key hashes and rejected each result-key-only hash. The original five files remain unchanged.

| Frozen multi-key fixture | SHA-256 |
| --- | --- |
| multi-register.resolved-receipt.json | `cbd4c7d0bc31c63c8760cd49cd88e95f9ce3b77745398ad7386d3d00dbc78956` |
| multi-register.stored-rows.json | `217be69ea67e3d9facfc6c83aff5fac370f2ea65892f1d7add418f896c393fd0` |
| multi-resolve.resolved-receipt.json | `21c4992cac7960b8631ebc78be1eab4dd9e2f313f4cbaedc5b9d3ae3e3765607` |
| multi-resolve.stored-rows.json | `38cd1cffaeaa807ee1aa9b79e95162aaced2acaa5da3b41eaf18a6a2ec44d614` |
| multi-revoke.stored-rows.json | `4ff9a0b8aa2a2c1728415c936c22e533b1de186dc912614f383ae8d95a02df3b` |

On 2026-09-14, local locked restore/build passed with zero warnings or errors. Complete compiled-MTP Category runs passed Unit 14/14 and Contract 7/7; real PostgreSQL Integration ran 21 cases with 20 passing, one retained P11 `memory.event/v1` quarantine-only failure, and no skips. The original Manifest Unit frontend produced no output and timed out after 15 seconds under the restricted runner, recorded as INFRA_ERROR. This is unsigned local diagnostic evidence, not successful original Manifest frontend execution, central-gate completion, or a P05 package PASS. Final independent QA remains required.

## P05 identity contract asset coverage

`SoulIdentityContractTests` embeds the owner corpus and executes all 411 stable cases in the existing Contract category. Six additional facts cover inventory, property order and committed scalar mutation, exact request/query matching, historical structure versus expiry, synthetic P-256/P1363 mathematics and explicit frame/nullable encoding. Zero signatures are invalid cryptographic fixtures and never authority. No original Unit/Contract/Integration method or fixture is removed.

The required `soul-registry.schema-corpus` suite uses exactly six unittest methods with a floor of 6. Four family methods consume all schema-applicable subcases with stable IDs; inventory and schema-definition methods check completeness and active real-calendar format validation. Subtests are not counted as discovered methods. Codec-only raw wire attacks remain in the same corpus and are checked by .NET. The corpus and all added tests are implementation assets awaiting the separately authorized Stage II runs and independent QA, not evidence of an executed pass.

Real current provider, signatures rooted in protected configuration, locks, cancellation/commit-unknown lifecycle, migration and full PG/central required gates remain separate required work. This first slice cannot supply P06/P11 with a real identity capability.

## P05-R1 Soul-only local authority checks

`SoulIdentityAuthorityTests` checks the closed construction boundary. `PostgresSoulIdentityAuthorityIntegrationTests` uses the actual 003 migration and a dedicated PostgreSQL 18.4 database, with a test-owned signed evidence issuer and P-256 signer. It covers exact proof replay, keyring collision/rotation, legacy 001/002 upgrade without authority backfill, nonce/idempotency conflicts, fixed trust metadata, signed reference-set tampering, revocation versus a real session advisory lock, backend termination, pre-commit rollback/retry and a test-owned loopback bridge that drops the real COMMIT acknowledgement after the server responds. The bridge does not substitute an after-commit exception for a lost wire response.

The evidence fixture signs the complete request and alias material, then the production decision core validates the result and persists its own signed proof. It does not insert a pre-authorized database row or skip a production branch. These tests prove only Soul-owner local behavior under synthetic trust. They do not prove Binding current, a cross-module two-lock interval, a protected production issuer, Ledger commit safety or any formal evidence level. The complete required Integration suite still includes the retained positive Soul-to-Ledger v1 case, which is expected to report its actual quarantine failure until the separately owned P11 path is implemented; do not filter or lower it to obtain a green result.

## P05-R2 replay binding regressions

The real PostgreSQL tests first create verification rows through `VerifyAliasAsync`, then deliberately change only the stored test row. They check that normal replay rejects another tenant's valid signed proof placed in that row, a changed row `proof_id`, and an altered signed alias-reference set. A separate test uses the test-owned loopback proxy to forward a real COMMIT, alter the resulting row through a second database connection after the server responds, and drop its acknowledgement; reconciliation must return `COMMIT_UNKNOWN`. This is a controlled post-commit row-corruption fault, not evidence that production storage was corrupted. The existing real acknowledgement-loss positive case and consumed same-request replay remain required. These tests do not add a public authority route or replace the complete Integration suite.
