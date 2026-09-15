# soul-registry contracts

Provided contract schemas have one owner: this module. Consumed schemas are referenced from their owner and are not copied or modified here. Every cross-module payload rejects unknown major versions and includes the identity, trace, idempotency, time, and privacy fields required by the root policy where applicable.

## Identity owner assets, P05 first slice

`SoulAliasVerificationV1.cs` owns the alias request/proof, their strict codecs, commitments and the alias client declaration. `SoulCurrentResolutionV1.cs` owns the current query/snapshot and current client/lease declarations. Use each DTO's `FromUtf8Json`, `ToUtf8Json`, `CanonicalBytes` and `ComputeSha256`; general-purpose DTO JSON serialization is not this wire protocol. Public construction, parsing, `RequireMatches` and `RequireValidWindow` only validate data. They do not authenticate, verify a production signature or grant a lease.

The four exact Draft 2020-12 schemas and `soul.identity.v1.corpus.json` share required/closed fields, UTC7, nonzero lowercase UUIDs, Int64 and nullable rules. Raw duplicate keys, BOM/UTF8/length/depth and lexical integer checks belong to the codec. JSON Schema cannot recover duplicate keys or distinguish integer lexical spellings after parsing; the corpus explicitly separates those cases. Lifetime, nested digest equality and exact query/request matching are codec checks, not claims about schema or current authority.

Frames use strict UTF8 length prefixes, explicit domains, nullable presence and UUID N format. Signature bytes and the nested resolution object are excluded as specified; the signed resolution digest binds the actual owner legacy audit bytes. The shared corpus includes format-only zero signatures. They are not cryptographic positive evidence.

`SoulResolvedCanonicalV1` keeps legal Zulu `SerializeWire` separate from the byte-frozen `SerializeLedgerAudit` algorithm identified by `soul.resolved.ledger-audit/v1`. Legacy audit uses the original default .NET JSON encoder, ordered 13 fields, O-format UTC string and raw SHA256, without a domain prefix. No legacy receipt, schema or old canonicalizer has been changed. Ledger tests call its unchanged internal production oracle through the existing friend test assembly.

Production verifier, signer, current provider, trust-root composition, nonce consumption, database transactions and lease lifecycle are still unimplemented. A future provider must authenticate the caller, use pinned protected roots, check current state and hold the owner lock until transaction outcome is known. Timeout, cancellation, expiry or Dispose must not release a lock while commit is unresolved. These declarations add no runnable routing, consumer edge or product capability.
