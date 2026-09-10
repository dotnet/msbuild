# Project references and target contracts

## Reference negotiation is not outer-build dispatch

The common implementation is in [Microsoft.Common.CurrentVersion.targets](../../../../src/Tasks/Microsoft.Common.CurrentVersion.targets) and [Microsoft.Common.CrossTargeting.targets](../../../../src/Tasks/Microsoft.Common.CrossTargeting.targets).

For the consuming project's reference pipeline:

1. Configuration/platform and reference items are prepared.
2. `_GetProjectReferenceTargetFrameworkProperties` calls referenced projects' `GetTargetFrameworks` where needed and supported.
3. NuGet's framework-selection task chooses a compatible referenced framework; Common targets construct metadata such as `SetTargetFramework`.
4. `ResolveProjectReferences` invokes the appropriate referenced target and collects outputs.

`SetTargetFramework` is reference-item metadata containing a property assignment, not a universal outer-build dispatch property.

A cross-targeting outer build separately enumerates its own `TargetFrameworks` and dispatches to its own inner builds. `GetTargetFrameworksWithPlatformFromInnerBuilds`, `_ComputeTargetFrameworkItems`, and `DispatchToInnerBuilds` show that protocol; the inner-build items carry `AdditionalProperties`.

Do not assume an outer build has a single `TargetFramework`, or that a target runs only in inner builds without inspecting its import/dispatch conditions.

## Referenced target and returned data

| Branch | Expected behavior |
|---|---|
| IDE or `BuildProjectReferences` disabled | Common targets normally query `GetTargetPath`, without building the output |
| Normal CLI project-reference build | Invoke the reference's requested/default targets and collect their returned outputs |
| Framework negotiation | `GetTargetFrameworks` reports available choices and metadata; it is not the actual compile |
| Custom referenceable project | Implement the applicable contracts or rely only on explicitly skippable optional targets |

`GetTargetPath` should not itself compile the reference. The host/user contract determines whether its output already exists.

Inspect reference metadata including `BuildReference`, `ReferenceOutputAssembly`, `Targets`, `SetConfiguration`, `SetPlatform`, `SetTargetFramework`, and `GlobalPropertiesToRemove`. For example, not passing an output assembly to the compiler is different from not building the reference.

The [ProjectReference protocol document](../../../../documentation/ProjectReference-Protocol.md) provides broader context, including optional targets and additional metadata. Some historical names/examples can lag implementation; use the current target files to resolve exact names, schemas, and conditions.

## Extension ordering

Use `DependsOnTargets` when a target requires predecessors. Use documented `BeforeTargets`/`AfterTargets` hooks to extend another target without replacing its body. These mechanisms solve different problems.

The import position matters for dependency-list properties. Common targets assign `BuildDependsOn`; an append earlier in an implicitly imported SDK project can be overwritten. Inspect the final assignment or use the appropriate supported hook.

`BeforeTargets="Build"` does not mean "before everything Build depends on." [TargetBuilder](../../../../src/Build/BackEnd/Components/RequestBuilder/TargetBuilder.cs) schedules dependencies before the before-target hook and the target body. Select the actual consumer boundary for pre-compilation or generated-file work.

A hook does not simply inherit the target's condition. Give it the appropriate condition and, where incremental behavior is needed, accurate `Inputs`/`Outputs`. Check repeated/no-op builds only for the relevant changed outputs, not as a blanket requirement that every target be skipped.

For publish/pack/compile extensions, inspect the selected SDK/target owner and final dependency properties. Do not assume a name such as `PublishDependsOn` or `PackDependsOn` has identical placement/meaning across project types and SDK versions.

## Design-time and host behavior

Inspect requested targets and properties including `DesignTimeBuild`, `BuildingProject`, `BuildingInsideVisualStudio`, and `BuildProjectReferences`.

Common targets default `BuildProjectReferences` differently for design-time work and use `BuildingProject` in several failure/reporting decisions. That does not define a universal target list for every Visual Studio project system.

Keep expensive work and side effects out of paths that only need design-time information, but do not indiscriminately skip targets that supply required IDE data. Establish the actual target/input/output contract before adding a design-time condition.

## Cross-repository coordination

Determine which repository owns the producer, consumer, and deployment/consumption step. An additive engine change may land first; another change may need consumer tolerance or an opt-in before a producer changes. Choose sequencing from compatibility, not stack order alone.

Document the contract, expected version/feature availability, fallback behavior, and end-to-end evidence needed. Creating issues, posting comments, or opening PRs requires authority separate from diagnosing the boundary.

Stop after the actual integration question is answered or the scoped change is evidenced. Record untested project systems, frameworks, or consumption paths rather than claiming universal integration coverage.
