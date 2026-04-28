---
applyTo: "src/Tasks/**/*.targets,src/Tasks/**/*.props"
---

# Targets & Props Authoring Instructions

`.targets` and `.props` files define MSBuild's default build logic. Changes here affect every .NET project.

## Target Ordering

* Use `DependsOnTargets` for required predecessors — primary ordering mechanism.
* Use `BeforeTargets`/`AfterTargets` sparingly — harder to reason about and debug.
* Test execution order with project references and multi-targeting scenarios.
* New targets must hook into the correct extensibility point (e.g., `BuildDependsOn`, `CompileDependsOn`).

## Condition Patterns

* Use `Condition="'$(PropertyName)' == ''"` to set defaults without overriding user values.
* `.props` files set defaults; `.targets` files should not override user-set properties.
* Conditions are evaluated in file order — cannot reference properties set later.
* Always use single-quotes for MSBuild string comparisons in conditions.

## Backwards Compatibility

* Changing property defaults breaks projects that relied on the previous default.
* Removing or renaming a target is a breaking change — may be referenced by `DependsOnTargets` in user projects.
* Adding items to existing item types can change build output unexpectedly.
* Gate behavioral changes behind a [ChangeWave](../../documentation/wiki/ChangeWaves.md).

## Incremental Build Correctness

* `Inputs`/`Outputs` declarations must be precise:
  - `Inputs`: all files that affect the target's output.
  - `Outputs`: all files the target produces.
  - Missing inputs → stale builds. Missing outputs → unnecessary rebuilds.
* See [Rebuilding when nothing changed](../../documentation/wiki/Rebuilding-when-nothing-changed.md).

## SDK Target Interaction

* SDK `.props` import before user project content; SDK `.targets` import after.
* Property defaults in SDK props must use `Condition="'$(Prop)' == ''"` so user values win.
* Do not assume execution order between SDK targets and user-authored targets.

## Related Documentation

* [ChangeWaves](../../documentation/wiki/ChangeWaves.md)
* [Target Maps](../../documentation/wiki/Target-Maps.md)
