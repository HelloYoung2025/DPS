# Consumed contracts

- `operation.compiled/v1`, owned by `operation-compiler`.
- `command.receipt/v1`, self-owned protocol consumed from its real producer (`executor-gateway`) before exact relay to Audit Metrics.

- `execution.authorization/v1`, owned by `policy-approval`; Command constructs the wire payload and consumes the unchanged versioned signer port response without changing its canonical bytes.
- `command.receipt.signed/v1`, self-owned signed protocol consumed from `executor-gateway`.
