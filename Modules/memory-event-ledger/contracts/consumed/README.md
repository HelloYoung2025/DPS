# Consumed contracts

Schemas consumed from another owner are referenced through ../module.yaml. Do not fork or locally weaken an upstream schema.

The v2 adapter consumes `soul.resolved/v1` only as exact raw material returned by a fixed, current Soul authority; the DTO alone is never authorization. It also consumes exact canonical bytes for `command.receipt.signed/v1`, independently verifies its P-256 signature, and requires native/postcondition digests plus `OBSERVATION_VERIFIED`. Current upstream modules do not yet declare the reciprocal memory-specific authority path, so compatibility remains intentionally red and production composition fails closed.

## P05 test-only parity reference

Differential tests reference the Soul owner contract pack already reachable through the existing source ProjectReference. They compare with the actual internal Ledger production oracle using the existing friend test assembly. This adds no consumed wire registration, consumer communication or new project edge, and does not use the public Soul DTO as capability.
