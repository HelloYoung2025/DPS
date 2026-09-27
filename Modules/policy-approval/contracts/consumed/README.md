# Consumed contracts

- `action.proposal/v2`, owned by `planner`, active. Policy decodes it only with the Planner owner codec and commits to it only with `dps.planner.action-proposal-sha256/v2`.
- `action.proposal/v1`, owned by `planner`, quarantine-only. The entry gate returns a bounded hash-only proof, writes nothing, and never evaluates or promotes it.

`approval.evaluation.request/v1` and `action.execution.promotion/v1` are not consumed-owned contract entries: Policy Approval uniquely owns the API input schema and strict codec, while `producer_module` is fixed to `control-plane-host` and each communication edge remains inbound.

- `execution.authorization/v1`, self-owned at the same major/mode as provided and received from its fixed Command wire producer. The existing signer boundary is declared in both directions; the Policy return preserves that producer. This pure asset ownership does not establish a production signer.
