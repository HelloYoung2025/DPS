# gbrain-projector source

Production source belongs below this directory. Other modules may depend only on versioned provided contracts, never internal implementation types.

`GBrainProjectorPostgresMigrator` is the only DDL entrypoint. It receives separate migration and runtime connection strings, proves both are distinct direct login identities, refuses elevated roles or role membership in either direction, creates only an absent schema, and otherwise adopts an existing schema only after exact catalog, owner, relation/type, constraint expression/FK target, index, trigger, rewrite-rule/publication, and ACL attestation.

`GBrainProjectorPostgresRuntime` receives only the runtime connection string. `InitializeAsync` performs attestation and never executes migration SQL; resolution and append fail closed until initialization succeeds. The runtime fixes the Source derivation, microsecond-normalized system clock, PostgreSQL store, Source binding authority, and renderer into one process boundary. Every INSERT is read back inside the transaction before success is returned. Every read cross-checks relational proof columns, canonical text, JSONB, checksum, and referenced binding before issuing a capability or accepting a revision; a sub-microsecond persistent timestamp or two revisions at the highest event count is rejected.

Migration preflight and every new runtime connection require exact PostgreSQL `server_version_num=180004` before identity, catalog, DDL, or module data work. Another major, minor, patch, malformed value, or missing version truth fails closed.

`GBrainProjectionRenderer` accepts only `VerifiedGBrainSourceBinding`; callers cannot provide a raw Source identifier or a custom binding store through the public API. Rendering remains a DTO-only operation with no GBrain client or credentials.
