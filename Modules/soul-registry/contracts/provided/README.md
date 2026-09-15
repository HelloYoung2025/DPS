# Provided contracts

Only this module may change schemas owned here. Additive changes stay within a major; breaking changes create a new major and follow the compatibility rollout sequence.

## Proposed identity schemas

The owner pack now contains `soul.alias.verify.request/v1`, `soul.alias.verification/v1`, `soul.current.query/v1` and `soul.current.resolution/v1`, registered as proposed/quarantine-only. This registration is not an implemented quarantine handler or production provider. Request producers remain Host or the four explicit current callers, not the schema owner by default. No consumer is newly registered.

The shared owner corpus covers every required field deletion, unknown/type/format/numeric/null boundary, caller-purpose and nested resolution case plus raw codec-only attacks. Preserve all stable IDs. The original `soul.resolved/v1` schema and ten legacy receipt fixtures remain byte-frozen. No raw alias, proof or key material belongs in these public assets.
