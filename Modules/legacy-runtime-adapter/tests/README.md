# legacy-runtime-adapter tests

This directory separates unit, contract, integration, resilience, Windows, and device evidence. The exact required suites and commands are declared in ../module.yaml.

A required check releases only on PASS. FAIL, SKIP, PARTIAL, NOT_RUN, INFRA_ERROR, NOT_APPLICABLE, an empty test set, or missing raw evidence is a failure. Mock evidence must be labelled and cannot satisfy integration, Windows, ZennoDroid, ADB, device, canary, or scale gates.

The current legacy runtime has not passed Windows or device gates. Static checks do not upgrade that status.

`test_legacy_byte_baseline.py` builds disposable Git repositories with exactly 79 legacy C# paths and 21 changes split into immutable sets of four, eight, and nine. Its 25 tests also cover real no-follow write-open authority, junction/reparse rejection, and unknown-entrypoint rejection. It executes no legacy C#.

`test_sessionrunner_fail_closed_p0.py` proves the source-level F5 P0 containment shape: all five production entrypoints gate on an explicitly unavailable bridge; writable DSL, missing/UNKNOWN results, Vision success overrides, direct device recovery, external process launch, and unverified memory writes remain blocked. It is static evidence only and executes no legacy C# or device operation.

`test_legacy_wrapper_and_orchestrator_p0.py` contains 10 static tests covering all fifteen production wrappers, the exact 39-file inventory, quarantine exclusion, disabled ModuleLoader, preserved line endings, full cleanup on the seven session wrappers, and SmartOrchestrator result truth. It executes no C#.

The external provider anchor protects the exact hashes of all three test files, their commands in `module.yaml`, and minimum method counts of 25, 9, and 10. Those commands resolve to an independent trusted-runner-created CPython 3.12.13 `.venv`, not a PATH-selected `python3`; formal evidence must also retain the interpreter SHA-256, exact argv, environment, raw output, and exit status. Deleting tests, reducing them to zero, replacing discovery with fixed success, or weakening assertions invalidates the provider record before the candidate verifier can pass.
