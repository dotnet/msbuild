# Trim / AOT suppression tracker

**Status:** Living (suppression inventory).

Tracks every `[UnconditionalSuppressMessage(...)]` for a trim/AOT analyzer rule
(`IL2xxx`, `IL3xxx`) in `src/`, so we can drive the count down - ideally until only
provable false positives remain.

> The **strategy** for *how* to drive the count down — remove / gate / register / annotate, with the
> full decision framework and audit plan — is [aot-trimming-strategy.md](aot-trimming-strategy.md).
> This file is the live inventory that plan operates on.
>
> A line-by-line audit of every `Backlog` row - the exact call graph, what fix was tried, and where
> the annotation/feature-guard flow breaks — is the
> [Backlog deep analysis](#backlog-deep-analysis) appendix below, so this is the single combined audit.
>
> A companion map of *every* trim/AOT annotation in the engine (`[RequiresUnreferencedCode]`,
> `[DynamicallyAccessedMembers]`, `[FeatureGuard]`/`[FeatureSwitchDefinition]`) and the strategies to drive
> the remaining suppressions down is [aot-annotation-map.md](aot-annotation-map.md).

The `Triggering call` and `Justification` columns are condensed; the attribute in
source is authoritative. Line numbers drift as code changes — search for the
member name if a link is stale.

## Suppression validity (design criterion)

MSBuild's trim/AOT work follows one overriding rule - **fail observably, never silently**
(see [managing-trimming-and-aot.md](managing-trimming-and-aot.md#msbuilds-overriding-design-criterion-fail-observably-never-silently)).
An AOT host (the dotnet CLI) runs MSBuild in-process and must be able to *detect* a path it
cannot execute and fall back to a JIT MSBuild. That gives a strict test for every row here:

* A `[UnconditionalSuppressMessage]` is **valid only when the warning is inaccurate** - the
  code is provably trim/AOT-safe and the analyzer simply cannot see it (a *false positive*).
  These are the `Vetted` rows.
* Suppressing an **accurate** warning is not a clean resolution - the warning is the very
  signal the host relies on. Such a path must instead **propagate `[RequiresUnreferencedCode]`**
  to a boundary, be **gated behind a `[FeatureGuard]` whose disabled branch raises a reported
  error** (observable failure - see the BuildCheck acquisition guard), or be replaced with a
  closed-world registration/direct path. Until that feature work exists, the row is `Backlog`.
* A suppression may **never** mask a path that would, under trim/AOT, **silently misbehave or
  crash**. That is forbidden outright.

Consequently an accurate-warning suppression is never an accepted final state. It may remain only
as `Backlog`: explicit pending work that requires additional feature work. Observable failure is
the minimum safety bar while the work is pending, not a reason to declare the suppression done.

## Status legend

| Status | Meaning |
| --- | --- |
| **Vetted** | Reviewed; the warning is a **false positive** - the code is provably trim/AOT-safe and the analyzer cannot see it. Per the design criterion (above) this is the only strictly-valid suppression. |
| **Investigate** | Not yet classified. Audit the call graph and either remove/restructure the warning, prove it `Vetted`, or move it to `Backlog` with the required feature work named. |
| **Backlog** | Requires additional feature work. The warning is accurate, or the assembly/subsystem is not yet trim/AOT-ready; the row must name the missing work or backlog bucket. This is not a permanent accepted state. |

## Current state

**As of 2026-06-28.** In-scope product suppressions: **12**
(Microsoft.Build 8 + Microsoft.Build.Framework 4) = **8 Vetted + 4 Backlog**; plus **9**
`Microsoft.Build.Tasks` Backlog rows. There are no `Investigate` rows. The Backlog rows need additional
feature work; the current observable-failure behavior only prevents silent failure or crashes while that
work is pending. The AOT validation harness is not product code and is not counted. The `[RequiresUnreferencedCode]` and `[DynamicallyAccessedMembers]` attributes are verified
correct against current source - see the companion [aot-annotation-map.md](aot-annotation-map.md). The
per-row inventory and the [Backlog deep analysis](#backlog-deep-analysis) below reflect this state. The
step-by-step removal history is in the git log.

## Microsoft.Build.Framework (`src/Framework/`)

| File:Line | IL rule | Member | Triggering call | Justification (short) | Status |
| --- | --- | --- | --- | --- | --- |
| [TypeExtensions.cs:30](../../src/Framework/Utilities/TypeExtensions.cs#L30) | IL3000 | `Type.GetAssemblyPath()` | `type.Assembly.Location` | `Location` is empty under single-file/AOT; the empty result is handled (AOT hosts supply the path via `MSBUILD_EXE_PATH`) and `Path.GetFullPath` is skipped for it. | Vetted |
| [TypeExtensions.cs:47](../../src/Framework/Utilities/TypeExtensions.cs#L47) | IL2067 | `Type.CreateDefault()` | `Activator.CreateInstance(type)` | Only invoked for value types (guarded by `IsValueType`), which always have a public parameterless ctor. | Vetted |
| [TypeExtensions.cs:83](../../src/Framework/Utilities/TypeExtensions.cs#L83) | IL2070 | `InvokeMemberPublicOnly(...)` | `type.InvokeMember(...)` | Sole caller rejects `BindingFlags.NonPublic`; receiver public surface preserved via `[DynamicallyAccessedMembers]`. | Vetted |
| [TaskClassRegistration.cs:65](../../src/Framework/TaskClassRegistration.cs#L65) | IL2072 | `CreateLoadedTypeFromFactory()` | `_createInstance().GetType()` | Only on the `RegisterTask(string, Func<ITask>)` overload, whose task type is host-supplied and not statically known, so the `LoadedType` is built lazily from the first instance's type. The generic overload - which the built-in tasks and most hosts use - supplies the `LoadedType` eagerly and is fully trim-safe. Backlog work is to give the non-generic registration path an explicit trim-safe type/metadata contract instead of relying on host rooting (`RegisterTask<T>` or `TrimmerRootAssembly`). | Backlog |

## Microsoft.Build (`src/Build/`)

| File:Line | IL rule | Member | Triggering call | Justification (short) | Status |
| --- | --- | --- | --- | --- | --- |
| [Expander.FunctionBuilder.cs:55](../../src/Build/Evaluation/Expander.FunctionBuilder.cs#L55) | IL2069 | `FunctionBuilder.SetReceiverType(Type)` | store into DAM-annotated `ReceiverType` | Receiver bounded to preserved-member allowlists: static types to `AvailableStaticMethods` (members preserved by `Constants.PropertyFunctionMembers` `[DynamicDependency]`); instance receivers to `PropertyFunctionReceiver` (§10 `RestrictPropertyFunctionReceivers` substituted `true` under trim). The reflected members are preserved, so the dataflow warning is a false positive. | Vetted |
| [Expander.Function.cs:363](../../src/Build/Evaluation/Expander.Function.cs#L363) | IL2074 | `Function.Execute(...)` | `_receiverType` from runtime value | Same bounded-allowlist receiver as above; `Constants.PropertyFunctionMembers` preserves the reflected members under trim (the code comment there calls these suppressions "honest under trimming"). | Vetted |
| [Expander.Function.cs:365](../../src/Build/Evaluation/Expander.Function.cs#L365) | IL2080 | `Function.Execute(...)` | `_receiverType.GetMethods(_bindingFlags)` (out-param path) | `_bindingFlags` is masked to `AllowedBindingFlags` at construction so it never carries `BindingFlags.NonPublic`; the call binds only public methods of the bounded allowlist receiver, whose public members are preserved for trimming. | Vetted |
| [Expander.Function.cs:669](../../src/Build/Evaluation/Expander.Function.cs#L669) | IL2096 | `Function.GetTypeForStaticMethod(...)` | case-insensitive lookup vs `AvailableStaticMethods` | Only resolves to curated `AvailableStaticMethods` allowlist types, whose members are preserved for trimming. | Vetted |
| [Expander.Function.cs:1189](../../src/Build/Evaluation/Expander.Function.cs#L1189) | IL2080 | `Function.FindPublicMethodBySignature(...)` | `_receiverType.GetMethods(_bindingFlags)` | Same `_bindingFlags` no-`NonPublic` invariant; public-only bind over the bounded allowlist receiver (the GetMethods signature match added with #14191). | Vetted |
| [Expander.Function.cs:1240](../../src/Build/Evaluation/Expander.Function.cs#L1240) | IL3050 | `Function.LateBindExecute(...)` | `Enum.GetValues(Type)` (rooted) | `Enum.GetValues(Type)` is unreachable via property functions - no way to supply a `Type` arg (MSB4185/MSB4186) - and would fail observably if reached; proven under Native AOT by `PropertyFunctionAotTests`. | Vetted |
| [Expander.Function.cs:1242](../../src/Build/Evaluation/Expander.Function.cs#L1242) | IL2080 | `Function.LateBindExecute(...)` | `_receiverType.GetMethods(_bindingFlags)` | Same `_bindingFlags` no-`NonPublic` invariant; public-only bind over the bounded allowlist receiver. | Vetted |
| [Constants.cs:303](../../src/Build/Resources/Constants.cs#L303) | IL3050 | `Constants.InitializeAvailableMethods()` | `Enum.GetValues(Type)` (rooted for allowlist) | Same `Enum.GetValues(Type)` false positive: rooted by `typeof(Enum)` for the allowlist but unreachable via property functions; verified by `PropertyFunctionAotTests`. | Vetted |
| [OutOfProcNode.cs:631](../../src/Build/BackEnd/Node/OutOfProcNode.cs#L631) | IL2026 | `HandlePacket(INodePacket)` | `NodeConfiguration` arm -> `HandleNodeConfiguration` | The build-request arms now go through the **`EnableReflectiveTaskExecution` leaf gate** (fail observably with MSB4283). The residual RUC is `HandleNodeConfiguration`, which loads node **forwarding loggers** by reflection - a separate subsystem this task gate does not cover. Backlog work is to gate the node forwarding-logger leaf. | Backlog |
| [BuildManager.cs:1480](../../src/Build/BackEnd/BuildManager/BuildManager.cs#L1480) | IL2026 | `INodePacketHandler.PacketReceived(...)` | work-queue -> `IssueBuildRequestForBuildSubmission` | Reaches **solution-configuration evaluation / SDK resolution and logger/plugin** init - not task execution (now gated). Backlog work is to gate the remaining solution, logger, and project-cache plugin paths that this boundary can reach. | Backlog |
| [TaskRegistry.cs:1870](../../src/Build/Instance/TaskRegistry.cs#L1870) | IL2057 | `TranslatorForTaskParameterValue(...)` | `Type.GetType(propertyTypeName)` | Task parameter type reconstructed from a **serialized** assembly-qualified name during task-host marshalling. `IL2057` is dataflow-class (no `[FeatureGuard]` silences it), the input is a `string` (no DAM target), and the name is serialization-supplied. Backlog work is to reuse `TaskParameterTypeRegistry` for this serialization path where possible. See [Backlog deep analysis - Group B](#group-b--inline-task-by-name-type-resolution). | Backlog |

> **Status of the audited Backlog rows (covered in the
> [Backlog deep analysis](#backlog-deep-analysis) appendix below).** None masks a silent
> failure or a crash: every terminal reflective operation reports an observable build error
> (`TaskInstantiationFailureError` / `TaskLoadFailure` / `InvalidProjectFileException` / **MSB4283**).
> - **The task-execution boundary rows are gone.** The audit's Group A leaf gate is **implemented**:
>   `EnableReflectiveTaskExecution` (`[FeatureSwitchDefinition]`+`[FeatureGuard]`, substituted `false`
>   under trim) gates the three reflective task leaves (`FindTask`, `InitializeForBatch`,
>   `SetTaskParameters` in `TaskExecutionHost`), which fail observably with **MSB4283**. That freed the
>   whole build-execution chain (the nodes, `BuildRequestEngine`, `RequestBuilder`/`TargetBuilder`/
>   `TaskBuilder`, `TaskHost`, and the `IBuildEngine3`/`INodePacketHandler`/`IRequestBuilder`/
>   `ITaskBuilder` interfaces) of `[RequiresUnreferencedCode]`, so **5 of the 7** former boundary
>   suppressions were removed (`InProcNode`, `TaskHost.BuildProjectFilesInParallel`,
>   `MSBuild.ExecuteTargets`, the two `BuildRequestEngine` event handlers). See
>   [Backlog deep analysis - Group A](#group-a--the-il2026-build-execution-boundary).
> - **The 2 remaining `IL2026` rows reach a *different* subsystem** - node forwarding-logger loading
>   (`OutOfProcNode.HandleNodeConfiguration`) and solution-configuration/SDK/logger init
>   (`BuildManager` work queue) - not task execution. They are `Backlog` until those subsystems are
>   gated too.
> - **The remaining `TaskRegistry.TranslatorForTaskParameterValue` `IL2057` row** is `Backlog`: it
>   reconstructs a task parameter type from a **serialized** assembly-qualified name; `IL2057` is
>   dataflow-class (no `[FeatureGuard]` can silence it), the input is a `string` (no DAM target), and the
>   name is serialization-supplied (not removable). The other two `TaskRegistry` rows (the
>   `<ParameterGroup>` `ParameterType` resolution) are handled by the task parameter type registry,
>   not a suppression (see
>   [task-parameter-type-registration-api.md](../specs/task-parameter-type-registration-api.md)). The
>   analysis is in
>   [Backlog deep analysis - Group B](#group-b--inline-task-by-name-type-resolution).
>
> The former `Expander` property-function rows are now **Vetted**, not `Backlog`: §10's
> `RestrictPropertyFunctionReceivers` switch (substituted `true` under trim, paired with
> `EnableAllPropertyFunctions` substituted `false`) bounds every property-function receiver to a
> preserved-member allowlist, and `Constants.PropertyFunctionMembers` `[DynamicDependency]` keeps
> that surface, so the dataflow the analyzer cannot follow is provably trim-safe.


## Microsoft.Build.Tasks (`src/Tasks/`) - Backlog

`Microsoft.Build.Tasks` is not trim/AOT-ready. These rows are `Backlog` because they require
additional feature work before this assembly can be treated as trim/AOT-enabled. One bucket is XML
handling (`XmlSerializer`, `XslCompiledTransform`, `SignedXml`); the others are attribute reflection
and assembly metadata handling.

| File:Line | IL rule | Member | Triggering call | Status |
| --- | --- | --- | --- | --- |
| [BootstrapperBuilder.cs:133](../../src/Tasks/BootstrapperUtil/BootstrapperBuilder.cs#L133) | IL3050 | `Build(BuildSettings)` | `XslCompiledTransform` | Backlog |
| [WriteCodeFragment.cs:82](../../src/Tasks/WriteCodeFragment.cs#L82) | IL2026 | `Execute()` | attribute-type reflection | Backlog |
| [GenerateManifestBase.cs:280](../../src/Tasks/GenerateManifestBase.cs#L280) | IL2026 | `Execute()` | `XmlSerializer` | Backlog |
| [SignFile.cs:46](../../src/Tasks/SignFile.cs#L46) | IL2026 | `Execute()` | `XmlSerializer` | Backlog |
| [SignFile.cs:48](../../src/Tasks/SignFile.cs#L48) | IL3050 | `Execute()` | `XmlSerializer` / `XslCompiledTransform` / `SignedXml` | Backlog |
| [RoslynCodeTaskFactory.cs:95](../../src/Tasks/RoslynCodeTaskFactory/RoslynCodeTaskFactory.cs#L95) | IL3002 | `GetThisAssemblyDirectory()` | `Assembly.ManifestModule.FullyQualifiedName` | Backlog |
| [TrustInfo.cs:533](../../src/Tasks/ManifestUtil/TrustInfo.cs#L533) | IL3050 | `ToString()` | `XslCompiledTransform` | Backlog |
| [DeployManifest.cs:551](../../src/Tasks/ManifestUtil/DeployManifest.cs#L551) | IL2026 | `Validate()` | `XmlSerializer` | Backlog |
| [DeployManifest.cs:553](../../src/Tasks/ManifestUtil/DeployManifest.cs#L553) | IL3050 | `Validate()` | `XmlSerializer` / `XslCompiledTransform` | Backlog |

## AOT validation harness (`src/aot-validation/`)

Not product code — the Native AOT validation harness — ignore.

## Remaining follow-up work

There is no **Investigate** group: the current rows are either `Vetted` or `Backlog`. The remaining
work is tracked in one place: [follow-up-work.md](follow-up-work.md). The product Backlog buckets most
directly related to this tracker are host-supplied task type metadata, project-cache plugin loading,
the node forwarding-logger leaf, solution-metaproject generation, and the serialized task-parameter
type path. The `Microsoft.Build.Tasks` Backlog includes an XML handling bucket
(`XmlSerializer`, `XslCompiledTransform`, `SignedXml`) plus smaller attribute-reflection and assembly
metadata rows.

Every remaining in-scope suppression is therefore one of: **Vetted** (a provable false positive: the
`TypeExtensions` reflection helpers, the property-function receiver dataflow now bounded by §10, and
the two `Enum.GetValues(Type)` AOT rows proven unreachable via property functions); or **Backlog**
(feature work still required). None masks a silent failure or a crash while the Backlog work is pending.

## Backlog deep analysis

This appendix is the line-by-line audit of the `Backlog` rows - the exact call graph each reaches,
what fix was tried, and precisely what additional feature work is required. The strategy catalog
it references (S1-S8) is [aot-trimming-strategy.md](aot-trimming-strategy.md).

In summary:

- The `Backlog` rows are **accurate** warnings, not false positives - each path genuinely reflects or
  reaches a subsystem that is not trim/AOT-ready.
- The terminal reflective operation **already fails observably** - a reported build error (**MSB4283**,
  `TaskLoadFailure`, `TaskInstantiationFailureError`, or `InvalidProjectFileException`) - never a silent
  no-op and never an uncaught crash. That is a safety property while the row is pending, not a final
  resolution.
- The clean end-state is to **remove** the warning (gate or restructure), not silence it. Group A's leaf
  gate does this for most of its rows; two Group A rows, one Group B row, and the Group C task-metadata
  row remain.

### Group A — the IL2026 build-execution boundary

**Implemented (leaf gate).** [`FeatureSwitches.EnableReflectiveTaskExecution`](../../src/Framework/FeatureSwitches.cs)
(`[FeatureSwitchDefinition]` + `[FeatureGuard(typeof(RequiresUnreferencedCodeAttribute))]`, substituted
`false` under trim) gates the three reflective task leaves in
[`TaskExecutionHost`](../../src/Build/BackEnd/TaskExecutionHost/TaskExecutionHost.cs) - `FindTask`,
`InitializeForBatch`, `SetTaskParameters` - whose disabled branch raises an observable **MSB4283**
(`ReflectiveTaskExecutionNotSupported`) `InvalidProjectFileException`. Because the switch is `true` in
every JIT build, the existing reflective path is unchanged for real builds; the gated branch only runs
under a trimmed/AOT MSBuild, which cannot run reflective tasks anyway.

With the leaves guarded, the whole build-execution chain dropped its `[RequiresUnreferencedCode]`,
removing **5 of the 7** original boundary suppressions: `InProcNode.HandlePacket`,
`TaskHost.BuildProjectFilesInParallel`, `MSBuild.ExecuteTargets`, and the two `BuildRequestEngine` event
handlers.

The two rows that **survive** - `OutOfProcNode.HandlePacket` and `BuildManager.PacketReceived` - do so
because they *also* reach a **different** reflective subsystem (node forwarding-logger loading; solution-
configuration / SDK / project-cache-plugin init) that the task gate does not cover. Gating that subsystem
is tracked in [follow-up-work.md](follow-up-work.md).

Why forwarding the RUC out of these two boundaries (instead of gating) does not work:

| Boundary | Why RUC cannot be forwarded past it |
| --- | --- |
| `OutOfProcNode.HandlePacket` (private message pump) | A **dispatch root** driven by the node packet loop, not a synchronous caller. Its only upward boundary is the internal `INodePacketHandler.PacketReceived` contract, implemented by many handlers that process **non-reflective** packets and invoked generically by the packet router - marking it RUC is over-broad and amplifies rather than removes. |
| `BuildManager.PacketReceived` (`INodePacketHandler` impl) | The same internal interface, over-broad for the same reason; it fronts the work-queue that reaches evaluation / SDK resolution / logger init. |

The terminal already fails observably: task **instantiation** is wrapped in
`catch (...) → LogError("TaskInstantiationFailureError")` returning `null`; task **load** reports
`"TaskLoadFailure"` via `ProjectErrorUtilities.ThrowInvalidProject`. So under AOT an unloadable task is a
reported MSB error, never a silent skip or a crash.

### Group B — inline-task by-name type resolution

This is where the annotation / feature-guard flow is **genuinely unsolvable**, provably. The one surviving
row - [`TaskRegistry.TranslatorForTaskParameterValue`](../../src/Build/Instance/TaskRegistry.cs#L1870)
(`IL2057`) - reconstructs a task parameter type from a **serialized** assembly-qualified name
(`Type.GetType(propertyTypeName)`) during task-host marshalling.

| Strategy | Why it cannot apply |
| --- | --- |
| `[FeatureGuard]` (S3) | A feature guard silences only the `Requires*` trio (`IL2026`/`IL3050`/`IL30xx`). `IL2057` is a **dataflow** warning about an unanalyzable `Type.GetType(string)`; **no** guard or switch can suppress it. |
| `[DynamicallyAccessedMembers]` (S6) | DAM annotates a **`Type`**-typed target. Here the input is a **`string`** (a serialized assembly-qualified name) - there is no `Type` to annotate. |
| Remove the reflection (S1/S2) | The name is serialization-supplied; there is no compile-time-known type to substitute without deleting the feature. |
| Registration (S5) | The `<ParameterGroup>` sibling site **was** retired this way - [`TaskParameterTypeRegistry`](../../src/Framework/TaskParameterTypeRegistry.cs) resolves known types reflection-free, with the by-name fallback gated by `EnableReflectiveTaskParameterTypes`. The serialization path is a **candidate** to reuse the registry, but the value-type half is open-world (`IsValueType` admits any `struct`). |

If the name does not resolve, the path reports `InvalidProjectFileException` - observable. The
`[UnconditionalSuppressMessage]` is therefore the only mechanism for an accurate `IL2057` on an
unavoidable, serialization-supplied `Type.GetType(string)`. The allowed-type set and registry-feasibility
analysis are in [task-parameter-types.md](task-parameter-types.md).

### Group C - host-supplied task type metadata

[`TaskClassRegistration.CreateLoadedTypeFromFactory`](../../src/Framework/TaskClassRegistration.cs#L65)
(`IL2072`) exists only on the `RegisterTask(string, Func<ITask>)` overload. The generic registration
path roots the task type and builds `LoadedType` eagerly; the factory-only overload supplies only a
delegate, so MSBuild learns the concrete task type by calling the factory and reading `GetType()`.

Backlog work: give the factory registration path an explicit trim-safe type/metadata contract, or route
hosts to an API shape that carries the task type with DAM at registration time. Until then, hosts that use
the factory overload own rooting the task type's bindable public properties.
