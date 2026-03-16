---
applyTo: "src/Build/BuildCheck/**,src/Framework/BuildCheck/**"
---

# BuildCheck Instructions

BuildCheck is MSBuild's analyzer infrastructure (172 review comments in 2024-2026 — a top focus area). It enables both built-in and third-party build analyzers.

## Analyzer Authoring

* Analyzers must not throw exceptions — wrap analysis logic in try/catch and report failures via the infrastructure, not by crashing the build.
* Analyzers must be stateless between projects or explicitly manage state via the provided context. Shared mutable state causes concurrency bugs in multi-node builds.
* Built-in analyzers serve as examples for third-party authors — keep them clean, well-documented, and idiomatic.

## Diagnostic Codes

* Every BuildCheck diagnostic must have a unique code following the established format — see [Codes](../../documentation/specs/BuildCheck/Codes.md).
* Codes are permanent — once assigned, they cannot be reused or reassigned.
* Diagnostic messages must be actionable: state what was detected and what the user should do.

## Severity Handling

* Respect the severity escalation chain: `Suggestion` → `Warning` → `Error`.
* Users can configure severity per-analyzer in `.editorconfig` or MSBuild properties. Ensure the configuration pipeline works correctly.
* Default severity should be conservative (`Suggestion` or `Warning`) — `Error` blocks builds and requires high confidence.

## Acquisition & Extensibility

* Third-party analyzer loading follows the NuGet package acquisition pattern. Changes to the loading pipeline affect the ecosystem.
* Analyzer interfaces defined in `Framework/BuildCheck/` are public API — treat changes with API surface discipline (see framework.instructions.md).
* Version the analyzer contract interfaces. Breaking changes require a new interface version.

## Performance Impact

* Analyzers run during the build — they must not measurably slow down builds.
* Avoid per-item or per-property callbacks for analyzers that only need project-level data.
* Cache analysis results when the same check runs across multiple projects with identical configuration.
* Profile analyzer impact and document expected overhead in the analyzer's description.

## Architecture

* BuildCheck spans two assemblies: engine logic in `src/Build/BuildCheck/` and contracts in `src/Framework/BuildCheck/`.
* Data flows from evaluation/execution → BuildCheck infrastructure → analyzer → diagnostic output.
* Cross-node remoting of BuildCheck data must be handled correctly — see the architecture doc.

## Related Documentation

* [BuildCheck Architecture](../../documentation/specs/BuildCheck/BuildCheck-Architecture.md)
* [BuildCheck Feature Spec](../../documentation/specs/BuildCheck/BuildCheck.md)
* [Custom BuildCheck Analyzers](../../documentation/specs/BuildCheck/CustomBuildCheck.md)
* [Diagnostic Codes](../../documentation/specs/BuildCheck/Codes.md)
