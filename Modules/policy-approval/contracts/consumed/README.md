# Consumed contracts

- `action.proposal/v1`, owned by `planner`.

`action.execution.promotion/v1` is not a consumed-owned contract entry: Policy Approval uniquely owns the API input schema and strict codec, while `producer_module` is fixed to `control-plane-host` and the communication edge remains inbound.

- `execution.authorization/v1`, self-owned at the same major/mode as provided and received from its fixed Command wire producer. The existing signer boundary is declared in both directions; the Policy return preserves that producer. This pure asset ownership does not establish a production signer.
