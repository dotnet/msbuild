---
name: dotnet-aot-compat
description: "Resolve scoped trim/AOT analyzer warnings or establish an MSBuild trim/AOT compatibility contract. Not automatic serializer replacement, warning suppression, broad code migration, or ordinary test execution."
license: MIT
---

# Improve trim and Native AOT compatibility

**Use for:** a specific IL diagnostic, annotation-flow change, feature guard, or explicit AOT-compatibility request.

**Do not use for:** exclusively Framework-targeted analyzer work, unrelated .NET changes, automatic JSON migration, or choosing a new subsystem architecture from warning counts.

## Source preflight

Establish the requested surface and read/write authority before enabling anything. Read [global.json](../../../global.json), the affected project, and its nearest props/targets. MSBuild's core projects already condition `IsAotCompatible`; inspect [Framework's project](../../../src/Framework/Microsoft.Build.Framework.csproj) rather than adding a duplicate property.

For MSBuild behavior, inspect [FeatureSwitches](../../../src/Framework/FeatureSwitches.cs), existing annotations, and the [standalone native harness](../../../src/aot-validation/Microsoft.Build.AotValidation.csproj). Read-only analysis does not require a build or SDK installation.

## Workflow

1. Start with the actual diagnostic or requested capability. Trace the relevant member, type/value flow, caller contract, and affected shared-source consumers.
2. Select the appropriate approach from [MSBuild AOT guidance](references/msbuild-aot.md): analyzable flow, an existing registration/feature boundary, an explicit incompatibility contract, or a narrowly justified suppression.
3. Change a coherent scoped group, preserving untrimmed behavior and public annotation contracts. A warning count or fixed iteration limit is not grounds for broad edits, delegation, or `RequiresUnreferencedCode`.
4. Use [serialization guidance](references/serialization.md) only for an actual serialization issue, and [polyfill guidance](references/polyfills.md) only for older-target annotation availability.
5. Obtain evidence at the required level: compiler/analyzer, affected TFMs, or a published native consumer. Use the existing harness where relevant; zero build warnings alone do not establish native runtime correctness.

## Evidence and stop condition

Stop when the scoped warning or capability is resolved with the necessary evidence, or report the specific architectural/dependency/toolchain gap. Do not weaken analyzer settings to declare success, chase unrelated warnings, or imply every MSBuild execution scenario works under AOT.
