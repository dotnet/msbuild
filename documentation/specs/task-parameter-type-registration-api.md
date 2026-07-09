# API Proposal: Host registration of task parameter types

**Status:** Implemented as `Microsoft.Build.Utilities.TaskItem.RegisterTaskParameterValueType<T>` /
`RegisterTaskParameterItemType<T>` ([TaskItem.cs](../../src/Utilities/TaskItem.cs)), backed by the
reflection-free [`TaskParameterTypeRegistry`](../../src/Framework/TaskParameterTypeRegistry.cs). The
proposal below is the original design rationale.

## Background and motivation

A `<UsingTask>` may declare its parameters in a `<ParameterGroup>`, each with a `ParameterType` naming a
.NET type:

```xml
<UsingTask TaskName="MyTask" AssemblyName="My.Tasks" TaskFactory="RoslynCodeTaskFactory">
  <ParameterGroup>
    <Sources ParameterType="Microsoft.Build.Framework.ITaskItem[]" />
    <MaxCount ParameterType="System.Int32" />
    <Result ParameterType="System.String" Output="true" />
  </ParameterGroup>
  <Task>...</Task>
</UsingTask>
```

MSBuild restricts these types to a small, well-defined set (see
[task-parameter-types.md](../aot/task-parameter-types.md)): any value type, `string`, and the
`Microsoft.Build.Framework.ITaskItem` family, each also allowed as an array. To turn the declared
*name* into a `System.Type`,
[`TaskRegistry.ParseUsingTaskParameterGroupElement`](../../src/Build/Instance/TaskRegistry.cs#L1764)
historically called `System.Type.GetType(string)`.

`Type.GetType(string)` is incompatible with trimming and Native AOT: the type is named at run time by
the project author, so the trimmer cannot know which type to preserve. The call carries `IL2057` /
`IL2096` trim warnings, and in a trimmed image the named type's metadata may have been removed, so
resolution fails. This is the only thing standing between a stock `<ParameterGroup>` and a clean AOT
evaluation - the *set* of legal types is almost entirely statically known (the intrinsics and the
`ITaskItem` types), even though the *resolution* was reflective.

This proposal adds a small registry of task parameter types, pre-populated with the product-known set
and rooted for trimming, plus a host-registration surface for any additional types a host uses. The
by-name `Type.GetType` becomes a gated fallback that a trimmed/AOT image removes.

## API Proposal

Two static methods on the public `Microsoft.Build.Utilities.TaskItem`, mirroring the host-registration
shape of [`SdkResolver.Register`](sdk-resolver-host-registration-api.md):

```diff
 namespace Microsoft.Build.Utilities;

 public sealed class TaskItem : ITaskItem2, IMetadataContainer
 {
     // ... existing members ...

+    /// <summary>
+    /// Registers a value type so it can be used as a task parameter type (the ParameterType of a
+    /// <UsingTask> <ParameterGroup> parameter) in a trimmed or Native AOT host.
+    /// </summary>
+    public static void RegisterTaskParameterValueType<
+        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>() where T : struct;
+
+    /// <summary>
+    /// Registers an ITaskItem type so it can be used as a task parameter type (the ParameterType of a
+    /// <UsingTask> <ParameterGroup> parameter) in a trimmed or Native AOT host.
+    /// </summary>
+    public static void RegisterTaskParameterItemType<T>() where T : ITaskItem;
 }
```

The two constraints (`struct` and `ITaskItem`) correspond exactly to the two open-ended families of the
allowed set; `string` and its array are intrinsic and never need registration. `[DynamicallyAccessedMembers(All)]`
on the **value-type** method roots the registered struct/enum, so a trimmed image can still convert a
parameter from its string form (which may use a `TypeConverter`/`Enum.Parse`). The **item-type** method
carries no DAM: an item-typed parameter is validated by assignability and is never member-reflected through
the registry, so only the type reference is needed - which keeps registering even large concrete item
classes essentially free under trimming.

### Supporting internals

- A new internal store,
  [`Microsoft.Build.Framework.TaskParameterTypeRegistry`](../../src/Framework/TaskParameterTypeRegistry.cs),
  maps a type's `FullName` (for example `System.Int32`, `System.Int32[]`,
  `Microsoft.Build.Framework.ITaskItem`) to the `Type`. It lives in Framework - the lowest assembly -
  so both the engine (`Microsoft.Build`) and the public surface (`Microsoft.Build.Utilities`) reach it.
  The public `TaskItem` methods forward to it.
- It is **pre-registered** in its static constructor with the product-known set:
  - `string` and `string[]`.
  - The intrinsic value types and their arrays (`bool`, `byte`, `sbyte`, `char`, `short`, `ushort`,
    `int`, `uint`, `long`, `ulong`, `float`, `double`, `decimal`, `DateTime`), each member-rooted via the
    `[DynamicallyAccessedMembers(All)]` on `RegisterValueType`.
  - The Framework `ITaskItem` types and their arrays (`ITaskItem`, `ITaskItem2`, `TaskItemData`), with no
    member rooting (item types are validated by assignability only).
  The **concrete** item types in higher assemblies are registered by their owning assembly, since Framework
  cannot reference them: the engine's internal `ProjectItemInstance.TaskItem` from a static constructor in
  `Microsoft.Build` (next to the `<ParameterGroup>` parser), and the public
  `Microsoft.Build.Utilities.TaskItem` by a host through the public API (a higher-layer type the engine
  does not reference, and one a multi-targeted library cannot cleanly module-initialize - Utilities targets
  `netstandard2.0`, where `[ModuleInitializer]` does not exist and would also trip `CA2255`). The
  out-of-proc task host's private `TaskParameterTaskItem` is never declared as a parameter type and is not
  registered.
- A new feature switch
  [`FeatureSwitches.EnableReflectiveTaskParameterTypes`](../../src/Framework/FeatureSwitches.cs)
  (default `true` under the JIT, substituted `false` when trimming) gates the by-name fallback.

### Resolution logic

[`ParseUsingTaskParameterGroupElement`](../../src/Build/Instance/TaskRegistry.cs#L1764) becomes:

```csharp
// Always consult the reflection-free registry first.
Type paramType = TaskParameterTypeRegistry.TryGetType(expandedType);
if (paramType == null)
{
    if (FeatureSwitches.EnableReflectiveTaskParameterTypes)
    {
        paramType = ResolveParameterTypeByName(expandedType); // the old Type.GetType path, now a RUC helper
    }
}

ProjectErrorUtilities.VerifyThrowInvalidProject(paramType != null, /* InvalidEvaluatedAttributeValue */ ...);
```

The reflective fallback is isolated in a `[RequiresUnreferencedCode]` helper
([`ResolveParameterTypeByName`](../../src/Build/Instance/TaskRegistry.cs#L1743)). Because the switch
is a `[FeatureGuard]`, calling that helper inside `if (EnableReflectiveTaskParameterTypes)` needs no
suppression; because the switch is a `[FeatureSwitchDefinition]`, the trimmer substitutes it `false` and
removes the helper call entirely. The two `[UnconditionalSuppressMessage]` rows on
`ParseUsingTaskParameterGroupElement` (`IL2057`, `IL2096`) are deleted - the warnings are gone, not
suppressed.

## Semantics

- **Registry first, always.** Every resolution consults the registry first, on both the JIT and trimmed
  paths, with no reflection. The switch only gates the fallback for names the registry does not know.
- **Observable failure.** With the switch off, an unregistered name leaves `paramType` null and the
  existing `VerifyThrowInvalidProject(... "InvalidEvaluatedAttributeValue" ...)` reports an
  `InvalidProjectFileException` - the same error an unresolvable type already produced, now reached
  without reflection.
- **Process scope and timing.** The registry is process-global and consulted fresh on every lookup, so
  (unlike SDK resolver registration) there is no first-use snapshot; a registration takes effect for any
  later evaluation. Intended to be called once per type at host startup.
- **Thread-safety / idempotence.** Backed by a `ConcurrentDictionary`; registering the same type twice
  is harmless.
- **Validation is unchanged.** The registry only replaces name -> `Type`. `TaskParameterTypeVerifier`
  still enforces the input/output rules afterward, so registering a type does not widen what is legal.

## API Usage

```csharp
using Microsoft.Build.Utilities;

// During host startup, before the first evaluation. The type is referenced statically (no reflection).
// The value-type method roots the struct/enum for the trimmer (string-to-value conversion may reflect);
// the item-type method does not need to (item types are validated by assignability).
TaskItem.RegisterTaskParameterValueType<MyEnum>();
TaskItem.RegisterTaskParameterValueType<MyOptionsStruct>();
TaskItem.RegisterTaskParameterItemType<MyCustomTaskItem>();
```

After this, a `<ParameterGroup>` may declare `ParameterType="MyNamespace.MyEnum"` (etc.) and it resolves
from the registry under Native AOT with no `Type.GetType`.

### Effect on the validation harness

[`TaskParameterTypeRegistryAotTests`](../../src/aot-validation/TaskParameterTypeRegistryAotTests.cs)
bakes `EnableReflectiveTaskParameterTypes=false` and proves, under Native AOT: pre-registered types
(`string`, the intrinsics, `ITaskItem`) resolve and a project evaluates; a host-registered custom
`struct` and the public concrete `Microsoft.Build.Utilities.TaskItem` both resolve through the public
seam; and an unregistered type (`System.Guid`) fails observably with `InvalidProjectFileException` rather
than crashing in reflection.

## What this does and does not buy

- It resolves the **common, product-known** parameter types under AOT with no reflection, and gives a
  host a supported way to add its own. That covers the overwhelming majority of real `<ParameterGroup>`
  declarations (`string`, an intrinsic, `ITaskItem`/`ITaskItem[]`).
- It **cannot fully close the set.** The legal set includes *any* value type (`IsValueType`), which is
  not enumerable, so a name the host did not register still falls back to `Type.GetType` (under the JIT)
  or fails observably (under AOT). This is inherent and is documented in
  [task-parameter-types.md](../aot/task-parameter-types.md).
- **The .NET SDK itself declares no typed parameter groups.** A scan of every build file in a `10.0.300`
  SDK (`*.targets`/`*.props`/`*.tasks`) found **zero** `ParameterType="..."` declarations - the SDK uses
  compiled tasks, not typed inline tasks. So this primarily serves non-SDK / host scenarios and is
  infrastructure for the compiled-task path; the SDK's own evaluation needs none of it.
- The `<ParameterGroup>` path it most directly affects is **inline-task only and evaluation-time**, and
  inline tasks are already AOT-incompatible at execution (they compile source at run time). So the
  immediate payoff is removing two trim suppressions and making the parse trim-clean; the same registry
  is the reusable primitive for the higher-value **compiled-task** parameter-binding path
  (`LoadedType` / `TaskExecutionHost.SetTaskItemParameter`).

## Alternative designs

1. **A dedicated public `TaskParameterTypeRegistry` type** instead of methods on `TaskItem`. Cleaner
   separation, but a second new public type for a two-method feature; hanging the methods off the
   existing public `TaskItem` (the type a parameter author already knows) keeps the surface minimal, the
   same trade-off `SdkResolver.Register` made.
2. **A single `Register<T>()`** without the `struct` / `ITaskItem` split. Rejected: the two constraints
   map exactly to the two legal families and stop a caller from registering a reference type that the
   verifier would reject anyway.
3. **Tunable value-type rooting (`DynamicallyAccessedMemberTypes`).** Value types use `All` (conservative,
   to keep string-to-value conversion working); item types use none. A narrower value-type set (for example
   `PublicParameterlessConstructor | PublicProperties`) would shrink the image further if a scenario needs
   it; the level is an implementation detail behind the same API.
4. **Where the concrete `ITaskItem` implementations are registered.** Framework cannot reference them, so
   each is registered by its owning assembly: the engine's `ProjectItemInstance.TaskItem` from a static
   constructor in `Microsoft.Build` (free, since item types are not member-rooted), and the public
   `Microsoft.Build.Utilities.TaskItem` through the public API by a host that declares it. A library module
   initializer was rejected for the latter (`netstandard2.0` has no `[ModuleInitializer]`, and it trips
   `CA2255`).

## Risks

- **Process-global mutable static.** Justified as for `SdkResolver.Register`: a host's parameter-type set
  is fixed and established once at startup. The surface is two `Register` methods with no removal.
- **Rooting size cost.** Only the **value** types carry `[DynamicallyAccessedMembers(All)]` (item types
  are validated by assignability, so they are registered without member rooting). Measured on the AOT
  validation harness (`win-x64`, Release):

  | Native image | Bytes | Delta vs control |
  | --- | --- | --- |
  | No member rooting (control) | 19,319,296 | - |
  | As implemented (value types member-rooted; item + concrete item types registered without member rooting) | 19,969,536 | **+650,240 (~635 KB, +3.4%)** |
  | For reference: also member-rooting every item/concrete type with `All` | 20,478,464 | +1,159,168 (~1.1 MB) |

  The cost is entirely value-type rooting and is a one-time, fixed cost (it does not grow with project
  count). Registering the concrete item types (including the engine's `ProjectItemInstance.TaskItem`) is
  **free** because item types are never member-reflected - the third row shows what member-rooting them
  would have cost, which is why the implementation does not.
- **Evaluation-time failure shift.** Turning the unknown-type case into an evaluation error (under AOT)
  is reached when a project is *evaluated*, which is broader than task *execution*. This is acceptable
  because an inline task that cannot resolve its parameter types cannot run under AOT regardless, and the
  failure is observable rather than a crash.
- **Layering.** The store lives in `Microsoft.Build.Framework`; the engine and Utilities reach it through
  the existing `InternalsVisibleTo`. MSBuild gains no new dependency.

## Implementation status

Implemented in this change:

- **API.** `RegisterTaskParameterValueType<T>()` and `RegisterTaskParameterItemType<T>()` on
  [`Microsoft.Build.Utilities.TaskItem`](../../src/Utilities/TaskItem.cs), forwarding to the internal
  [`TaskParameterTypeRegistry`](../../src/Framework/TaskParameterTypeRegistry.cs).
- **Engine wiring.** [`ParseUsingTaskParameterGroupElement`](../../src/Build/Instance/TaskRegistry.cs#L1764)
  consults the registry first and gates the by-name fallback
  ([`ResolveParameterTypeByName`](../../src/Build/Instance/TaskRegistry.cs#L1743)) behind
  [`FeatureSwitches.EnableReflectiveTaskParameterTypes`](../../src/Framework/FeatureSwitches.cs); the
  `IL2057`/`IL2096` suppressions are removed.
- **Trimmed default.** A matching `RuntimeHostConfigurationOption` in Microsoft.Build.Framework.csproj,
  plus the Framework package's buildTransitive targets for package consumers. The AOT harness re-declares
  the switch because project-level host-configuration options do not flow across project references.
- **Concrete item types.** The engine's `ProjectItemInstance.TaskItem` is registered from a static
  constructor on the `<ParameterGroup>` parser; the public `Microsoft.Build.Utilities.TaskItem` is
  registered by a host through the public API (it is above Framework, and a multi-targeted library cannot
  cleanly module-initialize it). Item types carry no member rooting, so registering them is free.
- **Tests.** Four Native-AOT tests in
  [`TaskParameterTypeRegistryAotTests.cs`](../../src/aot-validation/TaskParameterTypeRegistryAotTests.cs);
  the harness publishes warning-clean and passes. Both TFMs of `Microsoft.Build` and all
  three TFMs of `Microsoft.Build.Utilities` (including `netstandard2.0`) build warning-clean, with 102
  `TaskRegistry_Tests` as a JIT regression guard.

## Related

- [task-parameter-types.md](../aot/task-parameter-types.md) - the precise allowed-type set and the `ITaskItem` landscape.
- [sdk-resolver-host-registration-api.md](sdk-resolver-host-registration-api.md) - the host-registration shape this mirrors.
- [task-class-registration-api.md](task-class-registration-api.md) - the companion proposal for registering task *classes*.
- [task-factory-aot.md](../aot/task-factory-aot.md) - the static task-registration / source-generator direction this complements.
