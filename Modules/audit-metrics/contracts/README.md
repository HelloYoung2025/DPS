# Audit Metrics contracts

Consumes `command.receipt/v1` and uniquely owns `audit.event/v1`. The public event contains scoped opaque IDs and evidence digests, never raw content, raw aliases, secrets, prompts, OCR/UI data, or device screenshots.

## Proposed Audit relay canonical profile v1

**Proposed, not activated.** [The machine profile and complete reference vectors](provided/audit.relay.v1.canonical.json) describe the two Audit-local preimages at source commit `23d92b84ceff1ab40e7e751fa23da5dfea6bd608`: the receipt digest used by Audit and the relay signature payload. They do not register a new active wire contract or change `command.receipt/v1`, its Schema, DTO, producer, or canonicalizer. The profile is a version-bound design choice for later independent review and implementation work, not proof that historical golden constants, deployment, or interoperability were correct. The current `CanonicalBindingsMatchGoldenVectors` failure and both original expectations remain unchanged.

The compatibility choice preserves current Audit bytes for accepted inputs. Changing Audit to Command's canonicalizer would change domains, field-name tokens, nullable representation and digests; that would require a separately reviewed migration of signers, receivers and stored digest bindings. Preserving bytes avoids making that change merely to repair a test. It still requires independent agreement on this Proposed profile. A real production relay signer and actual end-to-end compatibility have not been established.

### Ownership and dependency

| Role | Module / responsibility |
|---|---|
| Receipt contract owner | Command Orchestrator, including `CommandReceiptV1` and `CommandReceiptProtocolV1` |
| Receipt wire producer | Executor Gateway; the relayed receipt retains `producer_module=executor-gateway` |
| Relay transport sender | Command Orchestrator; it must not rewrite the receipt producer |
| Relay receiver | Audit Metrics, consuming the Command public DTO/Schema |
| Proposed local profile | Audit Metrics; receiver-local receipt digest and relay signature preimage, not a second receipt protocol owner |

The dependency remains Audit → Command. Executor also depends on the Command public protocol; there is no reverse dependency on Audit internals. Command's signed receipt uses domain `dps.command-orchestrator.command-receipt/v1` for its receipt digest and `dps.executor-gateway.command-receipt/v1` for the Executor signature. Its encoding includes field-name tokens and explicit nullable presence/value tokens. **An Audit-local `ReceiptSha256` must never be substituted for Command's `ReceiptSha256` or evidence digest**, despite the shared property name. `receipt.EvidenceDigest` is opaque data in the Audit preimage; Audit does not recompute the Command evidence-summary digest here.

### Exact frame and value rendering

For each ordered token, emit its UTF-8 byte length as exactly four unsigned big-endian bytes, then those bytes. Concatenate tokens without delimiter, field names, terminator, count, BOM, JSON quoting, padding or trailing newline. Length is bytes, not Unicode characters or UTF-16 code units; a token over `4294967295` bytes cannot be represented. Domain is the first length-prefixed token, not a raw prefix. SHA-256 is computed over the entire resulting byte array and expressed as 64 lowercase hex digits.

The JSON profile lists every receipt and relay token in order. Receipt covers the domain and all 21 `CommandReceiptV1` fields exactly once. Relay covers the domain, CallerModule, AuthScope, ReceiptId, the **Audit-local** receipt digest, ExpiresAt and ReleaseBomSha256. SignatureBase64 is excluded; no signature, public key, current time, trust-state revision or key ID is added to this preimage. Receipt contents are bound indirectly by its recomputed Audit digest and matching ReceiptId. BOM/key currentness and scope authorization remain separate verifier/ingestion obligations.

| Type | Exact rendering before length prefix |
|---|---|
| String | Preserve content and case; no trimming, normalization, escaping or version rewriting |
| Version | Preserve the accepted original string, e.g. `1`, `1.0`, `1.0.0`, `1.00.0` are distinct byte values; no coercion to `1.0.0` |
| GUID | 32 lowercase hexadecimal digits, no hyphens or braces (`N` representation); never .NET mixed-endian binary GUID bytes |
| Nullable GUID | null is a zero-length token (`00000000`); non-null is the GUID rendering. There is no presence token. `Guid.Empty` when non-null is 32 zero digits, distinct from null |
| Integer | ASCII invariant decimal, no plus sign or leading zeros; receipt Attempt is 1–3 |
| Boolean | ASCII `true` or `false`; no numeric or uppercase alternative |
| Timestamp | Typed DateTimeOffset round-trip representation: `yyyy-MM-ddTHH:mm:ss.fffffff±HH:mm`, exactly seven fractional digits (100 ns ticks). Accepted ingestion values have offset zero and therefore end `+00:00`, not `Z`. Valid UTC wire `Z` or `+00:00` may parse to this same typed value; missing fractional digits become zero digits, not rounding |

UTC validation happens before accepted receipt/relay use. Nonzero offsets are rejected; there is **no automatic offset-to-UTC conversion in this profile**. The low-level relay encoder alone preserves a typed nonzero offset if called directly; it does not validate or normalize it. A proposed caller must not treat that output as an authorized relay. Wire parsing/precision beyond the typed 100 ns range is not standardized by this helper, and this design does not add a parser or silently round such inputs.

For valid Unicode scalar strings the encoding is ordinary UTF-8. Current Audit uses .NET's replacement fallback for malformed UTF-16: a valid surrogate pair becomes its scalar; an isolated high or low surrogate becomes U+FFFD (UTF-8 `efbfbd`). Distinct malformed inputs can therefore collapse to identical bytes. Command's canonicalizer instead uses strict UTF-8 and throws for isolated surrogates. This difference is an explicit safety finding, not a recommendation to accept malformed input or proof of injectivity. The accepted Audit ingestion domain below restricts the freely supplied result label to ASCII, while low-level helpers remain callable with inputs outside that domain. A later strict-encoding repair would need explicit scope and rejection tests; this proposal neither changes that behavior nor labels invalid-input helper output as accepted evidence.

### Validation is not the same as encoding

`AuditRelayAuthorizationBinding.CanonicalBytes(receipt)` calls `CommandReceiptV1.Validate`. At this base that requires: non-null receipt; schema version of at most 32 UTF-16 code units matching `1(?:\.[0-9]+){0,2}` at the absolute end; exact `command.receipt/v1` and `executor-gateway`; nonempty ReceiptId/CommandId/LeaseId; integer Attempt 1–3; exact lowercase opaque Soul/device/account/trace/idempotency patterns; zero-offset OccurredAt; privacy `internal`; lowercase 64-hex EvidenceDigest; non-whitespace ResultCode of at most 128 UTF-16 code units; and the following outcome rules. The opaque patterns use absolute ends: `soul_[a-f0-9]{64}`, `db_[a-f0-9]{32}`, `pa_[a-f0-9]{32}`, `trace_[a-f0-9]{32}`, `idem_[a-f0-9]{64}`. No leading/trailing character is permitted. These are receipt validation constraints, not extra tokens.

- Outcome is SUCCESS, FAILED or UNKNOWN_OUTCOME.
- SUCCESS requires NativeResultVerified and PostconditionVerified true, and non-null NativeResultId.
- A retryable FAILED requires NativeResultVerified true and non-null NativeResultId.
- UNKNOWN_OUTCOME forbids RetryAllowed. Other boolean combinations are not inferred or upgraded.
- The existing receipt validator does not reject a non-null empty NativeResultId; the frame preserves that value and the proposal does not silently tighten it.

Successful Audit ingestion additionally validates the derived `AuditEventV1`. The current ResultCode guard uses .NET regex `^[A-Za-z0-9._:-]+$`, whose `$` can match before one final LF; it is not an absolute-end check. Thus an otherwise valid value such as `VERIFIED\n` (one actual terminal U+000A) can pass this guard and the remaining label checks, subject to the original length limit. The canonical frame does not trim it: the LF contributes byte `0a` and increases the UTF-8 length prefix by one. This documents existing acceptance, not a proposed tightening or normalization. ResultCode must also pass the existing PII/secret checks: reject the phone-like pattern `\+?[0-9][0-9 -]{7,}[0-9]`, `@`, and case-insensitive substrings `secret`, `token`, `password`, `email`, `phone`, `prompt`, `content`, `cookie`, `authorization` in the label key/value combination. This is a stricter domain than receipt hashing alone. Receipt hashing returning bytes does not prove that the event can be appended. Unknown outcome/version/scope/label inputs still fail closed; no invalid tuple from the delimiter test is an accepted reference vector.

`EcdsaAuditRelayAuthorizationVerifier.CanonicalBytes(envelope)` only checks the envelope itself for null; it does not validate its members, claimed role, digests or expiry. Non-null string fields are required by the writer. The real `Verify` path requires UTC now/expiry, lowercase digest shapes, matching receipt ID, expiry **at or after** now, and a recomputed matching Audit receipt digest. It verifies SHA-256/ECDSA with a NIST P-256 SPKI key and IEEE P1363 `r || s` signature bytes. The signature is Base64-decoded; current Audit has no separate canonical-Base64 round-trip check (whitespace/noncanonical spelling accepted by the decoder is not newly prohibited here). A valid P-256 P1363 signature is 64 bytes. The signature signs SHA-256 of the full relay frame once through ECDSA/SHA-256, not a hex digest string or a twice-hashed digest. No valid signature/private key is supplied by these digest vectors.

The processor separately requires verified CallerModule `command-orchestrator`, AuthScope `audit:command-receipt`, matching receipt ID/digest and current validity. Directly calling the concrete verifier is not equivalent to the processor's role check. Production adds the independently trusted root-signed active BOM/key state and rotation/currentness controls; neither a hash nor a locally generated test signature supplies that authority.

### Reference vectors and remaining work

The machine file contains the full original golden fixture inputs, ordered rendered tokens, each byte length, complete receipt/relay preimages in hex, total lengths and independently derived SHA-256. The receipt fixture uses the existing typed UTC instant; the envelope factory's `fake-signature` is explicitly a test placeholder and the golden method replaces it with an empty string before hashing. Both spellings are excluded from the relay preimage. The relay's ReceiptSha256 is derived from this profile's receipt preimage; the old failed expected receipt hash is not injected into the relay vector. Small field examples cover null/empty GUID, false, UTF-8 byte lengths and the documented malformed-UTF-16 fallback; they are encoding examples, not additional accepted receipt fixtures.

A standard-library reference calculation from these written rules and typed fixture inputs supplies the bytes, rather than invoking/importing the product canonicalizer or copying its output into expectations. Independent QA must derive both complete frames from the frozen profile and compare all tokens, lengths, bytes and digests; a digest-only comparison or re-signing the product canonicalizer's output is insufficient.

Command's outbound Manifest still says `module:audit-metrics`, while Audit inbound and the processor require `audit:command-receipt`. The actual relay signer, its public contract/composition, exact trust distribution and this authScope mapping remain unresolved integration work. The profile selects no runtime mapping and declares no end-to-end closure. It also does not specify Audit event-integrity, persisted-record, scope-idempotency or root-trust-state canonical protocols; those distinct existing domains remain unchanged.

Migration order: (1) independently review and freeze this Proposed Audit-local profile; (2) separately bind any golden repair to the agreed profile, retain the historical failing evidence and run all applicable build/Unit/Contract/real-PG/gate checks; (3) separately bind Command's real relay signer and both authenticated communication edges, verify complete producer-to-receiver bytes and current BOM/key behavior; (4) obtain applicable deployment/rollout evidence. This document does not approve itself or execute any step beyond design. If rejected, do not integrate the Proposed profile; retain the frozen evidence. A later byte-changing decision needs its own version/compatibility and immutable-history plan, never a rewrite of stored receipt hashes. Existing kill switch and rollback continue to stop ingestion/restore prior routing without changing history. P04 and the global gates remain unfinished; no formal verification level is issued here.
