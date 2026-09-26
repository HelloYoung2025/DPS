# soul-registry contracts

Provided contract schemas have one owner: this module. Consumed schemas are referenced from their owner and are not copied or modified here. Every cross-module payload rejects unknown major versions and includes the identity, trace, idempotency, time, and privacy fields required by the root policy where applicable.

## Identity owner assets, P05 first slice

`SoulAliasVerificationV1.cs` owns the alias request/proof, their strict codecs, commitments and the alias client declaration. `SoulCurrentResolutionV1.cs` owns the current query/snapshot and current client/lease declarations. Use each DTO's `FromUtf8Json`, `ToUtf8Json`, `CanonicalBytes` and `ComputeSha256`; general-purpose DTO JSON serialization is not this wire protocol. Public construction, parsing, `RequireMatches` and `RequireValidWindow` only validate data. They do not authenticate, verify a production signature or grant a lease.

The four exact Draft 2020-12 schemas and `soul.identity.v1.corpus.json` share required/closed fields, UTC7, nonzero lowercase UUIDs, Int64 and nullable rules. Raw duplicate keys, BOM/UTF8/length/depth and lexical integer checks belong to the codec. JSON Schema cannot recover duplicate keys or distinguish integer lexical spellings after parsing; the corpus explicitly separates those cases. Lifetime, nested digest equality and exact query/request matching are codec checks, not claims about schema or current authority.

Frames use strict UTF8 length prefixes, explicit domains, nullable presence and UUID N format. Signature bytes and the nested resolution object are excluded as specified; the signed resolution digest binds the actual owner legacy audit bytes. The shared corpus includes format-only zero signatures. They are not cryptographic positive evidence.

`SoulResolvedCanonicalV1` keeps legal Zulu `SerializeWire` separate from the byte-frozen `SerializeLedgerAudit` algorithm identified by `soul.resolved.ledger-audit/v1`. Legacy audit uses the original default .NET JSON encoder, ordered 13 fields, O-format UTC string and raw SHA256, without a domain prefix. No legacy receipt, schema or old canonicalizer has been changed. Ledger tests call its unchanged internal production oracle through the existing friend test assembly.

The internal Soul-owner core now exercises authenticated-evidence input, signed alias proof verification, durable nonce/receipt decisions and a Soul-only PostgreSQL owner lease under test-owned synthetic keys. Its constructor is internal and no production evidence verifier, protected signer, pinned release root, listener or public current-resolution issuer is composed. The four new contracts stay quarantine-only with no consumer edge. A future provider must authenticate the caller with real protected roots and compose Binding truth separately; a Soul-only lease cannot prove a downstream Ledger commit outcome. These declarations and local tests add no production capability.

## Quarantined four-role Soul-current session

Host references only `Dps.SoulRegistry.Contracts`. The previous in-process runtime factory is removed; there is no D3 exception. `ControlPlaneSoulCurrentComposition.Open()`, provider Open and both signer Open methods reject even when bootstrap is enabled. Only fixed internal fixture entry assemblies can construct the synthetic flow. No public caller-selected provider, path, key or synthetic switch exists. Both modules remain proposed, quarantine-only and release-ineligible.

Host owns the five-second entry budget and verifies owner signatures locally. The provider holds a real nonpooled PostgreSQL owner lease and compares the independent current signer's signed facts with its locked snapshot. Host obtains possession directly from the host-auth signer, generates the final status challenge, and checks the returned signature and exact request/nonce. After explicit release ACK, Host rechecks the original deadline, caller cancellation and both signed windows before `SOUL_ACTIVE_AS_OF`. Device/account echoes are not Binding authority or a future CURRENT guarantee.

Installation loads `configuration.v2.json` only. `SoulCurrentConfigurationV2` keeps every v1 field and guard, changes schema/contract to `2.0.0` / `soul.current.configuration/v2`, and adds `provider_uid`, `provider_gid` after `host_gid`. V1 remains a frozen historical DTO/schema/corpus; query, resolution, signing/status canonical bytes and public client/lease interfaces are unchanged.

New installation assets are owner implementation formats, not activated cross-module routes. Every field below is required, ordered for canonical framing as shown, and rejects unknown/duplicate fields. JSON is bounded to 16384 bytes/depth 4 and strict UTF-8; integers use canonical Int64 lexical spelling, UUIDs are nonzero lowercase D on wire/N in frames, times are UTC7, hashes lowercase SHA256, signatures canonical P1363/P-256. JSON Schema cannot enforce raw duplicate keys, lexical integers, canonical DER/P-256 or cryptographic validity; the owner codecs and shared corpus also check these constraints.

| Format | Exact ordered fields |
| --- | --- |
| `SoulCurrentBootstrapV1` | `schema_version`, `contract_id`, `installation_id`, `enabled`, `root_key_id`, `root_spki_base64`, `active_configuration_sha256`, `active_release_bom_sha256`, `active_release_generation` |
| `SoulCurrentConfigurationV1` | `schema_version`, `contract_id`, `configuration_id`, `tenant_ref`, `soul_id`, `principal_id`, `consumer_module`, `permission`, `host_uid`, `host_gid`, `host_auth_uid`, `host_auth_gid`, `host_auth_key_id`, `host_auth_spki_base64`, `current_service_uid`, `current_service_gid`, `current_key_id`, `current_spki_base64`, `status_key_id`, `status_spki_base64`, `database_host`, `database_port`, `database_name`, `database_schema`, `database_runtime_role`, `database_secret_id`, `signer_database_runtime_role`, `signer_database_secret_id`, `deadline_ms` |
| `SoulCurrentCompositionV1` | `schema_version`, `contract_id`, `configuration_sha256`, `host_artifact_sha256`, `soul_artifact_sha256`, `contracts_artifact_sha256`, `release_bom_sha256`, `release_generation`, `issuer`, `key_role`, `key_id`, `issued_at`, `expires_at`, `signature_algorithm`, `signature_base64` |
| `SoulCurrentAuthorityStateV1` | `schema_version`, `contract_id`, `configuration_sha256`, `tenant_ref`, `soul_id`, `principal_id`, `permission`, `status`, `authorization_revision`, `trust_epoch`, `key_revocation_epoch`, `release_bom_sha256`, `release_generation`, `host_auth_key_id`, `current_key_id`, `status_key_id` |
| `SoulCurrentStatusV1` | `schema_version`, `contract_id`, `request_id`, `nonce`, `query_sha256`, `instance_id`, `session_id`, `configuration_sha256`, `tenant_ref`, `soul_id`, `principal_id`, `permission`, `status`, `authorization_revision`, `trust_epoch`, `key_revocation_epoch`, `release_bom_sha256`, `release_generation`, `host_auth_key_id`, `current_key_id`, `status_key_id`, `issued_at`, `expires_at`, `signature_algorithm`, `signature_base64` |
| `SoulCurrentSigningProtocolV1` | `schema_version`, `contract_id`, `operation`, `message_type`, `request_id`, `instance_id`, `session_id`, `query_base64`, `query_sha256`, `configuration_sha256`, `release_bom_sha256`, `release_generation`, `expires_at`, `challenge_nonce`, `possession_base64`, `status_base64`, `resolution_base64`, `error_code`, `signature_base64` |

Canonical frames concatenate F(domain), then F(value) in the table order, omitting only `signature_base64`; F is uint32 big-endian UTF-8 byte length followed by those bytes. Domains are `dps.soul-registry.current.composition/v1`, `dps.soul-registry.current.status/v1` and `dps.soul-registry.host-possession/v1`. Scalars use invariant integer, lowercase boolean, UUID N or exact validated string encodings. Existing query/resolution commitments retain their original domains and bytes. The new 132-case corpus includes independently fixed composition/status frame hashes and format-only zero signatures, never authority.

The frozen internal signing format has exactly nine operation/message pairs: host-possession challenge-request/challenge/answer/proof, read-current-status request/response, sign-current request/response, error response. Each bounded frame has uint32BE byte length and strict owner JSON. Embedded query/proof/status/resolution values are canonical Base64 and reparse with their owner codecs. Unused payload fields must be empty. The host proof binds query canonical hash, server one-use nonce, instance/session, configuration/BOM/generation and expiry. A sign-current request cannot carry a caller-owned resolution; the actual product handler reads and constructs it. Errors are correlated closed codes and carry no exception, secret or SQL text. One monotonic five-second budget covers the complete flow, without retry or per-hop renewal; status signatures expire within two seconds and that budget.

R2 configuration parsing and schema reject the reserved database name `dps_gbrain_company` independently of the existing port-55434 prohibition. The shared corpus includes this rejection on otherwise valid port 55000. Provider and signer admission reject effective column INSERT/UPDATE/REFERENCES and table MAINTAIN, including privileges inherited from PUBLIC. R3 removes the runtime import; all unrelated quarantine, P11 and global gate failures remain separately reportable.

### Public envelope and phase matrix

Manifest `contractId=soul.current.lease.session`, major `1`; wire `contract_id=soul.current.lease.session/v1`. Soul is the sole schema/codec owner. Reciprocal `read-only-query` edges use AF_UNIX: Host↔provider and Host↔host-auth. Provider↔current signer retains the internal signing protocol. Actual OS peer routing is Host at provider/host-auth and provider at current signer, never a combined allowlist.

The exact top-level order is `schema_version`, `contract_id`, `producer_module`, `soul_id`, `device_binding_id`, `platform_account_id`, `trace_id`, `idempotency_key`, `occurred_at`, `privacy_class`, `phase`, `request_id`, `nonce`, `instance_id`, `session_id`, `sequence`, `tenant_ref`, `query_base64`, `query_sha256`, `configuration_sha256`, `release_bom_sha256`, `release_generation`, `expires_at`, `signing_base64`, `resolution_base64`, `status_base64`, `error_code`. All fields are required. The seven common business fields equal the decoded query exactly, including `privacy_class=sensitive`; no null placeholders. Strict 16384-byte length-prefixed canonical JSON, nonzero lowercase UUIDs, UTC7, canonical integers/Base64 and typed nested codecs reject unknown/duplicate/extra fields and alternate encodings. Schema shape checks do not replace decoded cross-field equality, canonical-byte or signature validation.

| Phase | Producer | Required payload; all other payloads empty |
| --- | --- | --- |
| acquire/begin | Host | query; empty session, sequence 1 |
| acquire/challenge | Soul/provider | nonzero connection-bound session, sequence 2 |
| acquire/possession-request | Host | signing challenge-request; separate auth connection sequence 1 |
| acquire/possession-challenge | Soul/host-auth | signing challenge; auth sequence 2 |
| acquire/possession-answer | Host | signing answer; auth sequence 3 |
| acquire/possession-proof | Soul/host-auth | signed proof; auth sequence 4 |
| acquire/complete | Host | signed proof; original provider connection sequence 3 |
| acquire/acquired | Soul/provider | signed resolution and initial signed status; sequence 4 |
| revalidate/request | Host | new request/nonce, next odd sequence |
| revalidate/response | Soul/provider | fresh signed status matching Host request/nonce, next even sequence |
| dispose/request | Host | new request/nonce, next odd sequence |
| dispose/ack | Soul/provider | no payload; only after confirmed release/close |
| error/response | Soul | closed error code; no SQL/exception/secret payload |

Responses echo request/nonce and increment sequence; all frames bind instance, exact query/hash, tenant, session, configuration/BOM/generation and immutable expiry. The two auth request/response pairs retain their own request ID/nonce. One provider connection owns one session and one in-flight operation. Duplicate requests/nonces, out-of-order frames, pipelining, cross-connection transfer and deadline renewal fail closed. Rejection can close the transport without an error frame; EOF is never success. There is no retry, recovery or session database.

Normal release drains the actual owner connection, requires `pg_advisory_unlock` true, confirms absence of the exact single-bigint advisory lock in that backend, then closes its nonpooled connection before ACK. ACK is a peer/session-correlated message, not independent issuance or proof about an uncontrolled server. A false/remaining/uncertain release, missing ACK or expired final window cannot succeed. Failure cleanup has a separate maximum 5000ms wait, finite Npgsql cancellation wait and irreversible terminal state; late work is observed and resources disposed, never turned into business success. This does not promise an uncooperative remote task has stopped.
