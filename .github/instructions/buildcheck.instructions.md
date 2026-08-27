---
applyTo: "src/Build/BuildCheck/**,src/Framework/BuildCheck/**"
---

# BuildCheck Instructions

MSBuild's analyzer infrastructure enabling built-in and third-party build analyzers.

## Analyzer Authoring

* Analyzers must not throw — wrap logic in try/catch and report failures via infrastructure.
* Must be stateless between projects or explicitly manage state via context. Shared mutable state causes concurrency bugs in multi-node builds.
* Built-in analyzers serve as examples for third-party authors — keep them clean and idiomatic.

## Diagnostic Codes

* Every diagnostic must have a unique code — see [Codes](../../documentation/specs/BuildCheck/Codes.md).
* Codes are permanent — once assigned, cannot be reused or reassigned.
* Messages must be actionable: state what was detected and what to do.

## Severity Handling

* Escalation chain: `Suggestion` → `Warning` → `Error`.
* Users configure severity per-analyzer in `.editorconfig` or MSBuild properties.
* Default to `Suggestion` or `Warning` — `Error` blocks builds and requires high confidence.

## Acquisition & Extensibility

* Third-party analyzers load via NuGet packages — changes to the loading pipeline affect the ecosystem.
* Analyzer interfaces in `Framework/BuildCheck/` are public API — version contract interfaces, breaking changes require a new version.

## Performance Impact

* Analyzers must not measurably slow down builds.
* Avoid per-item/per-property callbacks for analyzers that only need project-level data.
* Cache results when the same check runs across projects with identical configuration.

## Architecture

* Engine logic in `src/Build/BuildCheck/`, contracts in `src/Framework/BuildCheck/`.
* Data flow: evaluation/execution → BuildCheck infrastructure → analyzer → diagnostic output.
* Cross-node remoting must be handled correctly — see [architecture doc](../../documentation/specs/BuildCheck/BuildCheck-Architecture.md).

## Related Documentation

* [BuildCheck Architecture](../../documentation/specs/BuildCheck/BuildCheck-Architecture.md)
* [BuildCheck Feature Spec](../../documentation/specs/BuildCheck/BuildCheck.md)
* [Custom BuildCheck Analyzers](../../documentation/specs/BuildCheck/CustomBuildCheck.md)
* [Diagnostic Codes](../../documentation/specs/BuildCheck/Codes.md)
