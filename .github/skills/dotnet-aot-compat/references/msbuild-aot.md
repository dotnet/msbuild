# MSBuild trim/AOT workflow

## Establish the requested contract

Trimming reachability, dynamic-code generation, and single-file assumptions are related but different concerns. A library with clean build-time analysis can still have a consumer or runtime behavior problem.

Read the affected project's current analysis settings and TFM conditions. `IsAotCompatible` is a compatibility declaration as well as an analysis opt-in; do not treat it as a harmless switch to add to every project. Preserve the existing conditions in the [Framework](../../../../src/Framework/Microsoft.Build.Framework.csproj), [Engine](../../../../src/Build/Microsoft.Build.csproj), [Tasks](../../../../src/Tasks/Microsoft.Build.Tasks.csproj), and [Utilities](../../../../src/Utilities/Microsoft.Build.Utilities.csproj) projects.

For a read-only question, inspect supplied diagnostics and source. When execution is authorized and necessary, use the SDK selected by [global.json](../../../../global.json) and the existing build setup. A local `.dotnet` directory is not proof it contains the selected SDK.

Preserve the build's exit status and relevant errors when collecting diagnostics. Filtering output to only `ILxxxx` lines can hide compilation, restore, or SDK failures. Do not infer a clean build from an empty filtered list.

## Follow the value and its consumers

Warnings identify a useful starting point, not the complete edit scope. Inspect the warned operation, the incoming type/value, its callers, and shared-source consumers before changing a signature or annotation.

`Type` is a reference type. Passing it through `object`, `object[]`, or an untyped collection is not boxing, but it can lose the dataflow information the analyzer needs. Prefer an explicitly typed/annotated path where it preserves the API and runtime contract.

For example, a C# call-site helper reflecting over public methods can express that requirement:

```csharp
using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

static MethodInfo? FindFoo(
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type type)
{
    return type.GetMethod("Foo");
}
```

Trace callers outward. A known `typeof(T)` or a sufficiently annotated value can satisfy the requirement; it is not necessary to add an identical attribute mechanically to every caller. Annotate the appropriate parameter, generic parameter, field, property, or return value according to the actual flow.

The source requirement must cover the members the consumer needs. A generic-argument mismatch and an ordinary parameter mismatch can produce different IL diagnostics; do not assume they all produce IL2091.

An attribute on `object` or a collection does not magically describe the runtime type of every stored value. Consider a typed field/parameter or a supported registration boundary rather than moving the same ambiguity elsewhere. Check interface/override annotation contracts when changing a member.

Use the minimum required member set. `DynamicallyAccessedMemberTypes.All` can increase retained code and expose further warnings; use it only where the actual contract requires that surface.

## Select the appropriate approach

| Situation | Approach |
|---|---|
| Known types/members, analyzable flow | Narrow `DynamicallyAccessedMembers` requirements or an equivalent static path |
| Existing reflection-free host registration | Reuse that registration and its rooting contract |
| Optional runtime feature unsuitable for trimming | Inspect the existing feature-switch/guard design and consumer defaults |
| Fundamentally dynamic supported API | Document the relevant `RequiresUnreferencedCode`, `RequiresDynamicCode`, or `RequiresAssemblyFiles` contract |
| Safe/reachability-constrained code the analyzer cannot prove | A small justified suppression with preservation/reachability evidence |
| A subsystem redesign or third-party limitation | Report the decision/dependency boundary; do not manufacture compatibility through annotations |

Marking an API with `RequiresUnreferencedCode` moves an incompatibility warning to callers. It does not implement an AOT-safe substitute, and it is not an automatic next step after a fixed number of attempts.

Public annotation changes can affect consumers' warnings-as-errors builds. Use [compatibility assessment](../../assessing-breaking-changes/SKILL.md) for that boundary.

## Existing MSBuild mechanisms

[FeatureSwitches.cs](../../../../src/Framework/FeatureSwitches.cs) is the central registry for trim-related AppContext switches. `FeatureSwitchDefinition` lets trimming substitute a value; applicable `FeatureGuard` annotations describe the capability guarded by a branch.

The defaults and their delivery matter:

- [Framework's project](../../../../src/Framework/Microsoft.Build.Framework.csproj) declares library-level `RuntimeHostConfigurationOption` items.
- [Framework's buildTransitive targets](../../../../src/Framework/buildTransitive/Microsoft.Build.Framework.targets) supply defaults to package consumers publishing trimmed or Native AOT.
- The [native harness](../../../../src/aot-validation/Microsoft.Build.AotValidation.csproj) explicitly declares the host configuration for its project-reference scenario.

Do not assume a library's items automatically reach every application's native compiler. Trace the package or project-reference consumption path, and preserve the distinction between normal untrimmed behavior and trim-time defaults.

Existing reflection-free paths include [TaskClassRegistry](../../../../src/Framework/TaskClassRegistry.cs), [TaskParameterTypeRegistry](../../../../src/Framework/TaskParameterTypeRegistry.cs), and task registration in [Utilities Task](../../../../src/Utilities/Task.cs). The [registered-resolver harness scenarios](../../../../src/aot-validation/RegisteredSdkResolverAotTests.cs) show another host-registration boundary.

For property-function work, inspect [Expander.FunctionBuilder](../../../../src/Build/Evaluation/Expander.FunctionBuilder.cs) and [TypeExtensions](../../../../src/Framework/Utilities/TypeExtensions.cs). Their type-flow and reachability guarantees cannot be reconstructed solely from a warning code.

## Suppressions require a proof, not a ban or quota

Official [trim-warning guidance](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/fixing-warnings) permits `UnconditionalSuppressMessage` when code is safe but the trimmer cannot prove it. Keep the scope small and document the specific preservation, reachability, or fallback guarantee.

[TypeExtensions](../../../../src/Framework/Utilities/TypeExtensions.cs) demonstrates several different justifications: handling empty single-file assembly paths, a guarded value-type operation, and a private reflection call reachable only through a public-member-preserving boundary.

Compiler-only `#pragma`/`SuppressMessage` does not generally suppress warnings emitted later by ILLink. Separately, [FeatureSwitches](../../../../src/Framework/FeatureSwitches.cs) has narrowly explained IL4000 analyzer suppressions because the analyzer cannot see the trim-time substitutions. Do not generalize that case into permission to hide reachable linker warnings.

Do not lower warning severity, add broad exclusions, or replace all warnings with `Requires*` annotations just to reach a numerical target.

## Diagnostic orientation

These are starting points; inspect the precise diagnostic text and API requirement.

| Code | Common boundary |
|---|---|
| IL2070 / IL2075 | Reflection receiver lacking the required member information |
| IL2067 | Argument does not satisfy a parameter's member requirement |
| IL2072 | A returned/extracted value does not satisfy a member requirement |
| IL2057 | Runtime type-name resolution that cannot be established statically |
| IL2091 | Generic argument does not meet the generic member requirement |
| IL2026 | A `RequiresUnreferencedCode` call |
| IL2050 | COM marshaling at an interop boundary |
| IL3000 | Assembly-location assumptions under single-file deployment |
| IL3050 | A `RequiresDynamicCode` call |

Use [COM/PInvoke guidance](../../cswin32-com/SKILL.md) for an actual interop boundary, or [serialization guidance](serialization.md) for serialization. JSON warnings are not presumed to dominate MSBuild work.

## Evidence levels and the native harness

[src/aot-validation](../../../../src/aot-validation/README.md) is an explicit standalone **MSTest/Microsoft.Testing.Platform** harness. Its local props/targets isolate it from the normal Arcade/xUnit test configuration, and it is not part of the ordinary solution build.

For a claim requiring native execution:

1. Select a harness scenario matching the capability and inspect its host assumptions in the project and [HarnessEnvironment](../../../../src/aot-validation/HarnessEnvironment.cs).
2. Use the selected SDK and required native toolchain; report missing prerequisites instead of changing SDK/feed configuration for a documentation task.
3. When appropriate, compare the JIT behavior with the native-published behavior under the same inputs.
4. Publish the harness for its supported configuration/RID and run the **published native executable**, not a previously built managed output.
5. Record the exercised feature switches, registration paths, results, and untested surfaces.

Use the harness README/current executable help for commands and supported selection options. Do not copy an old TFM-specific output path or impose the ordinary xUnit command on this host.

The harness covers defined object-model, resolver, and registered-task scenarios; it does not establish arbitrary dynamic task/plugin loading under AOT. Native publication, native execution, and analyzer-clean library compilation are distinct evidence.

For shared-source or public-annotation edits, include affected consumers/TFMs as needed. Stop at the requested contract, with explicit remaining gaps rather than a claim that every scenario is covered.
