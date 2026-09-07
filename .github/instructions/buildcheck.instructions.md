---
applyTo: "src/Build/BuildCheck/**,src/Framework/BuildCheck/**"
---

# BuildCheck

## Contracts and failure handling

- Public analyzer APIs live in [Build/BuildCheck/API](../../src/Build/BuildCheck/API); Framework also carries internal BuildCheck contracts. Determine visibility and consumers from source, not folder names.
- Analyzer state must have an explicit project/build lifetime and concurrency model.
- Keep failure isolation at the infrastructure boundary. [BuildCheckCentralContext](../../src/Build/BuildCheck/Infrastructure/BuildCheckCentralContext.cs) already catches callback failures and removes affected checks; do not add broad catch-and-ignore blocks to every analyzer.
- Loading/configuration failures and analyzer-result diagnostics have different paths. Preserve their reporting and scope.

## Diagnostics and performance

- Use the [code registry](../../documentation/specs/BuildCheck/Codes.md) and current configuration API; do not reuse assigned codes.
- [CheckResultSeverity](../../src/Build/BuildCheck/API/CheckResultSeverity.cs) includes `Default` and `None` as well as `Suggestion`, `Warning`, and `Error`. Preserve configuration/disable semantics, not just severity escalation.
- Match default severity to the opted-in feature contract and compatibility policy. New warnings in existing behavior can break warning-as-error builds.
- Select callbacks and cache lifetimes for the data actually needed. Measure meaningful overhead rather than promise that an analyzer has no measurable cost.
- Test configuration precedence, disabled checks, callback failure isolation, and cross-node results when the change touches those boundaries.

Read only the relevant [architecture](../../documentation/specs/BuildCheck/BuildCheck-Architecture.md), [feature](../../documentation/specs/BuildCheck/BuildCheck.md), or [custom analyzer](../../documentation/specs/BuildCheck/CustomBuildCheck.md) section; proposals are not proof of implemented behavior.
