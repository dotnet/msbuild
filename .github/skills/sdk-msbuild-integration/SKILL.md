---
name: integrating-sdk-and-msbuild
description: 'Guides work on the SDK-MSBuild integration boundary. Consult when authoring or modifying SDK targets, working on dotnet CLI to MSBuild invocation, handling project-reference protocol, coordinating cross-repo changes with dotnet/sdk, debugging property resolution or import ordering, working on restore/build/publish/pack target chains, or dealing with Directory.Build.props/targets interaction.'
argument-hint: 'Describe the SDK integration scenario or cross-repo coordination need.'
---

# SDK-MSBuild Integration Patterns

MSBuild operates as a component within the .NET SDK. This boundary is the most complex integration point in the .NET build stack, spanning MSBuild (engine), SDK (target implementations), NuGet (restore), and Roslyn (compilation).

## The Evaluation Boundary

Understanding MSBuild's evaluation order is critical for SDK target authoring:

```
1. Environment variables
2. Global properties (from CLI: -p:Foo=Bar)
3. Project-level properties (file order, with imports):
   ┌─ Sdk.props (SDK defaults)
   ├─ Directory.Build.props (user overrides BEFORE project)
   ├─ <Project> properties (the .csproj itself)
   ├─ Directory.Build.targets (user overrides AFTER project)
   └─ Sdk.targets (SDK target definitions)
4. Item definitions
5. Items (including SDK default globs)
```

### Key Import Order Rules

- **SDK props import BEFORE user project** — SDK defaults can be overridden by the user
- **SDK targets import AFTER user project** — SDK targets see user-specified properties
- **`Directory.Build.props` is imported from `Microsoft.Common.props` as an early user extension point after core defaults are computed** — use it for solution-wide customization
- **Property defaults set in SDK must not override user-specified values** — always use `Condition="'$(Prop)' == ''"`

```xml
<!-- CORRECT: SDK default that respects user override -->
<OutputType Condition="'$(OutputType)' == ''">Library</OutputType>

<!-- WRONG: Unconditional set clobbers user's .csproj -->
<OutputType>Library</OutputType>
```

## Restore and Build Separation

**Restore and Build must never run in the same evaluation.** The restore phase generates `.g.props` and `.g.targets` files that must be imported during evaluation — but they don't exist until restore completes.

- `dotnet build` implicitly runs restore then build as **separate invocations**
- `dotnet build --no-restore` skips restore, assuming it already happened
- Running both targets in one invocation (`/t:Restore;Build`) is a known anti-pattern that causes intermittent failures

## Project Reference Protocol

The project-reference protocol spans MSBuild, SDK, and NuGet. It is the most complex integration boundary.

### How It Works

1. Outer build dispatches to `_GetProjectReferenceTargetFrameworkProperties` to determine inner build parameters
2. Inner build runs with the resolved `TargetFramework` (singular) for each referenced project
3. `GetTargetPath` returns the output assembly for the referencing project to consume

### Rules

- Protocol changes must be coordinated across MSBuild, SDK, and NuGet teams
- Multi-targeting projects (`<TargetFrameworks>`) dispatch multiple inner builds
- The outer build must not assume a single target framework
- `SetTargetFramework` is how the outer build communicates the chosen framework to inner builds

## Target Authoring in SDK Context

### Extension Points

SDK provides well-known extension points for targets:

| Extension Point | Use For |
|----------------|---------|
| `$(BuildDependsOn)` | Adding to the Build chain |
| `$(CompileDependsOn)` | Pre-compilation steps |
| `$(PublishDependsOn)` | Publish pipeline additions |
| `$(PackDependsOn)` | NuGet pack pipeline additions |
| `BeforeTargets="Build"` | Use sparingly; prefer `DependsOnTargets` |

### Ordering Rules

1. **Use `DependsOnTargets` for required predecessors** — it's explicit and predictable
2. **`BeforeTargets`/`AfterTargets` should be used sparingly** — they create implicit ordering that's hard to debug
3. **Incremental build targets need precise `Inputs` and `Outputs`** — incorrect declarations cause either rebuild-every-time or stale-output bugs
4. **Test with multi-targeting** — target chains execute once per `TargetFramework` in the inner build

## Cross-Repo Coordination

Changes that touch the MSBuild-SDK boundary often require coordinated PRs:

1. **MSBuild engine change** → may need SDK target updates
2. **SDK target change** → may need MSBuild API additions
3. **NuGet restore change** → affects both MSBuild evaluation and SDK targets

### Coordination Protocol

- File an issue in both repos describing the cross-cutting change
- Land the MSBuild change first (lower in the stack)
- Update SDK to consume the new MSBuild via dependency flow
- Test end-to-end with the SDK's MSBuild integration tests

## Design-Time Builds

Visual Studio uses design-time builds with different target contracts:

- Design-time builds call `ResolveProjectReferences` but not `Build`
- They set `$(DesignTimeBuild)=true` and `$(BuildingProject)=false`
- Targets that should not run during design-time must check these properties
- Design-time builds must be fast — avoid expensive I/O or compilation

## Common Integration Bugs

| Symptom | Likely Cause |
|---------|-------------|
| Property has wrong value | Import ordering — check if SDK prop overrides user setting |
| Target runs in wrong order | Missing `DependsOnTargets` declaration |
| Build works, restore fails | Evaluation-time dependency on restore-generated files |
| Works single-target, fails multi-target | Target assumes single `$(TargetFramework)` |
| CLI build works, VS build fails | Design-time build target contract violation |
