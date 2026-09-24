# soul-registry migrations

Use expand, migrate, contract across separate releases. The current and previous compatible versions must both run after the expand step. Destructive contraction requires a later signed BOM, backup evidence, and an explicit rollback or forward-fix decision.

`003_add_identity_authority_state.sql` adds empty owner-only verification/nonce, authenticated alias, current state, immutable result receipt and audit tables. It never promotes rows written by the old caller-supplied `Verified` flag. `PostgresSoulRegistry.InitializeAsync` embeds and applies 001, 002 and 003 in that order; both its resource list and the source project file must name 003. The signed proof and the complete retained-key reference set are checked before Apply changes any Soul row. No raw alias or signing key is stored.

Run the migration on an isolated PostgreSQL 18.4 database before a new binary processes identity requests. Keep old receipts and aliases intact. Once new authority rows exist, an older binary without the authority-alias collision guard is not a safe writer: stop it before new writes and use a forward fix or an independently proven compatible rollback binary. The current internal core has no production composition, so this migration does not activate a listener, current-resolution issuer, Binding tuple or P06/P11 handoff.
