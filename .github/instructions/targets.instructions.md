---
applyTo: "src/Tasks/**/*.targets,src/Tasks/**/*.props"
---

# Targets & Props Authoring Instructions

`.targets` and `.props` files define MSBuild's default build logic. Changes here affect every .NET project.

## Target Ordering

* Use `DependsOnTargets` for required predecessors — this is the primary ordering mechanism.
* Use `BeforeTargets`/`AfterTargets` sparingly; they are harder to reason about and debug.
* Target execution order must be tested with project references and multi-targeting scenarios.
* When adding a new target, ensure it hooks into the correct extensibility point (e.g., `BuildDependsOn`, `CompileDependsOn`).

## Condition Patterns

* Use `Condition="'$(PropertyName)' == ''"` to set defaults without overriding user values.
* Properties in `.props` files set defaults; `.targets` files should not override user-set properties.
* Conditions are evaluated in file order — a condition on line 10 cannot reference a property set on line 20.
* Always use single-quotes for MSBuild string comparisons in conditions.

## Backwards Compatibility (Critical)

* **New warnings are breaking changes** for builds using `/WarnAsError` or `<TreatWarningsAsErrors>`. Gate behind ChangeWave or use `Message` importance instead.
* Changing property defaults breaks existing project files that relied on the previous default.
* Removing or renaming a target is a breaking change — targets may be referenced by `DependsOnTargets` in user projects.
* Adding items to existing item types can change build output unexpectedly.

## Incremental Build Correctness

* Targets with `Inputs` and `Outputs` enable incremental builds. These declarations must be precise:
  - `Inputs` must list all files that affect the target's output.
  - `Outputs` must list all files the target produces.
  - Missing inputs → stale builds. Missing outputs → unnecessary rebuilds.
* See [Rebuilding when nothing changed](../../documentation/wiki/Rebuilding-when-nothing-changed.md).

## SDK Target Interaction

* SDK `.props` import before user project content; SDK `.targets` import after.
* Property defaults set in SDK props must use `Condition="'$(Prop)' == ''"` so user values win.
* Do not assume execution order between SDK targets and user-authored targets.

## Property & Item Group Organization

* Place `PropertyGroup` and `ItemGroup` elements at the correct evaluation point.
* Global properties override all file-level properties — do not rely on file-level overrides of global properties.

## Related Documentation

* [ChangeWaves](../../documentation/wiki/ChangeWaves.md)
* [Target Maps](../../documentation/wiki/Target-Maps.md)
