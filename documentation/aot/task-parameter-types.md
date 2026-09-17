# Task parameter types: the allowed set, resolution, and an `ITaskItem` registry

**Status:** Background analysis.

**Bottom line:** the legal task-parameter type set is small and fixed, but inline-task
`<ParameterType>` resolves arbitrary type names through `Type.GetType`, so a closed-world `ITaskItem`
registry can cover the common path yet cannot fully replace by-name resolution without a breaking change.

This documents **precisely** which .NET types are legal for an MSBuild task parameter, how the
inline-task `<ParameterGroup>` resolves a declared `ParameterType` string to a `System.Type`, what
`ITaskItem` implementations exist in the product, and whether a public, trim-safe `ITaskItem` type
registry is feasible.

It exists because the parameter-type resolution in
[`TaskRegistry.ParseUsingTaskParameterGroupElement`](../../src/Build/Instance/TaskRegistry.cs#L1764) carries
two trim/AOT suppressions (`IL2057`/`IL2096`) - the "Group B" rows in
[aot-trim-suppressions.md](aot-trim-suppressions.md#group-b--inline-task-by-name-type-resolution) -
and understanding the *exact* constraint set is a prerequisite for deciding whether a closed-world
registry (strategy **S5** in [aot-trimming-strategy.md](aot-trimming-strategy.md)) could ever replace
the by-name `Type.GetType` there or in the broader task parameter-binding path.

---

## 1. The allowed parameter type set (precise)

The single source of truth is [`TaskParameterTypeVerifier`](../../src/Shared/TaskParameterTypeVerifier.cs).
A declared parameter type is validated by `IsValidInputParameter` or `IsValidOutputParameter`
depending on whether the parameter is `Output="true"`. The predicates are:

```csharp
// INPUT = scalar OR vector
IsValidScalarInputParameter(t) = t.IsValueType || t == typeof(string) || t == typeof(ITaskItem);
IsValidVectorInputParameter(t) = (t.IsArray && t.GetElementType().IsValueType)
                               || t == typeof(string[])
                               || t == typeof(ITaskItem[]);

// OUTPUT = value-type-output OR assignable-to-ITaskItem
IsValueTypeOutputParameter(t) = (t.IsArray && t.GetElementType().IsValueType)
                              || t == typeof(string[])
                              || t.IsValueType
                              || t == typeof(string);
IsAssignableToITask(t)        = typeof(ITaskItem[]).IsAssignableFrom(t)
                              || typeof(ITaskItem).IsAssignableFrom(t);
```

Effective table (✔ = allowed, ✘ = rejected):

| Declared parameter type | As **input** | As **output** |
| --- | :---: | :---: |
| `System.String` | ✔ | ✔ |
| `System.String[]` | ✔ | ✔ |
| any value type (`bool`, `int`, `DateTime`, **a user `struct`**, …) | ✔ | ✔ |
| array of a value type (`int[]`, `MyStruct[]`, …) | ✔ | ✔ |
| `Microsoft.Build.Framework.ITaskItem` (the interface itself) | ✔ | ✔ |
| `Microsoft.Build.Framework.ITaskItem[]` (the interface array) | ✔ | ✔ |
| a **custom** `T : ITaskItem` (scalar), e.g. `ITaskItem2` or a user class | ✘ | ✔ |
| a **custom** `T[]` where `T : ITaskItem` (array) | ✘ | ✔ |
| any other reference type (`object`, `System.IO.FileInfo`, a non-item class) | ✘ | ✘ |

### The three things to notice

1. **Input vs output is asymmetric for items.** Inputs are checked with **reference equality**
   (`t == typeof(ITaskItem)` / `t == typeof(ITaskItem[])`), so **only the exact `ITaskItem` /
   `ITaskItem[]` interface types are legal inputs** - `ITaskItem2`, `Microsoft.Build.Utilities.TaskItem`,
   or any user item class are **rejected as inputs**. Outputs are checked with
   `IsAssignableFrom`, so **any** `ITaskItem` implementer (and, via array covariance, any array of one)
   is a legal output.
2. **Any value type is legal in both directions** - not just BCL primitives. `t.IsValueType` admits an
   arbitrary user `struct`. This is the part of the set that is *not* a finite, statically-known list.
3. **Reference types that are neither `string` nor an `ITaskItem` are never legal** (neither input nor
   output). You cannot declare a parameter of type `object`, `FileInfo`, `Stream`, etc.

> The same predicates gate parameters of **compiled** tasks too: when a task assembly is loaded,
> [`LoadedType`](../../src/Framework/Loader/LoadedType.cs#L152) records `isAssignableToITask` per
> property (`iTaskItemType.IsAssignableFrom(pt)` on the element type), which
> [`TaskExecutionHost.SetTaskItemParameter`](../../src/Build/BackEnd/TaskExecutionHost/TaskExecutionHost.cs#L916)
> uses to bind item-typed inputs/outputs. `TaskParameterTypeVerifier` is the **declaration-time**
> gate; `LoadedType` is the **load-time** classification.

---

## 2. Where the constraint is enforced, and the suppressed resolution

`TaskParameterTypeVerifier` is consulted in exactly one declaration path:
[`ParseUsingTaskParameterGroupElement`](../../src/Build/Instance/TaskRegistry.cs#L1764), which parses the
`<ParameterGroup>` of a `<UsingTask>`. The parse - including the type resolution below - runs at
**evaluation/registration time for *any* `<UsingTask>` that carries a `<ParameterGroup>`**
([call site](../../src/Build/Instance/TaskRegistry.cs#L416): `if (projectUsingTaskXml.Count > 0)`),
**before and independent of any task factory**. A `<ParameterGroup>` is *consumed* only by the inline
factories (`CodeTaskFactory`, `RoslynCodeTaskFactory`, `XamlTaskFactory`) - the
`AssemblyTaskFactory`/`TaskHostFactory` path reflects parameters off the compiled task type instead - but
that consumption happens later, at execution; the name resolution here is factory-agnostic.

As of [task-parameter-type-registration-api.md](../specs/task-parameter-type-registration-api.md) this
resolution is **registry-first**: the reflection-free
[`TaskParameterTypeRegistry`](../../src/Framework/TaskParameterTypeRegistry.cs) (the intrinsic value
types, `string`, and the MSBuild `ITaskItem` types, plus any a host registers) is consulted first, and
only a name it does not know falls back to `Type.GetType` - and only when the
`EnableReflectiveTaskParameterTypes` switch is on. That gate (a `[FeatureGuard]`
`[RequiresUnreferencedCode]` helper,
[`ResolveParameterTypeByName`](../../src/Build/Instance/TaskRegistry.cs#L1743)) **retired the
`IL2057`/`IL2096` suppressions** this path used to carry.

One `Type.GetType(string)` site here still carries a trim suppression:

* [`TranslatorForTaskParameterValue`](../../src/Build/Instance/TaskRegistry.cs#L1872) (`IL2057`) - reconstructs
  the (already-validated) type from its serialized `AssemblyQualifiedName` when a `TaskPropertyInfo`
  crosses the task-host boundary. (This serialization path is a candidate to reuse the same registry; it
  is separate from the parameter-type registry change.)

---

## 3. How a `ParameterType` string is resolved to a `Type`

The author writes a .NET type name (the C# keyword forms like `bool` do **not** resolve;
`Type.GetType` needs `System.Boolean`, `System.String`, `Microsoft.Build.Framework.ITaskItem`, …).
The reflection-free registry is consulted first; the **by-name fallback** below (now the gated
[`ResolveParameterTypeByName`](../../src/Build/Instance/TaskRegistry.cs#L1743) helper) runs only for a
name the registry does not know, and only when `EnableReflectiveTaskParameterTypes` is on. After
property/item expansion the string is `expandedType`, and the fallback resolution is:

```csharp
if (expandedType.StartsWith("Microsoft.Build.Framework.", OrdinalIgnoreCase) && !expandedType.Contains(","))
{
    // (A) Framework-prefixed, unqualified name: try the *loaded* Framework assembly FIRST.
    paramType = Type.GetType(expandedType + "," + typeof(ITaskItem).Assembly.FullName, throwOnError: false, ignoreCase: true)
                ?? Type.GetType(expandedType);
}
else
{
    // (B) everything else: try the bare name first, then fall back to the Framework assembly.
    paramType = Type.GetType(expandedType)
                ?? Type.GetType(expandedType + "," + typeof(ITaskItem).Assembly.FullName, throwOnError: false, ignoreCase: true);
}
```

Precisely what this does (correcting the "Framework-only branch" intuition - it is **not** a compile
-time `#if`, it is a *runtime name-prefix* special-case plus a *universal fallback*):

* **The assembly-name fallback is universal.** `!expandedType.Contains(",")` means "the name is **not**
  assembly-qualified." For any unqualified name, MSBuild will, as a fallback, append the **currently
  loaded** `Microsoft.Build.Framework` identity (`typeof(ITaskItem).Assembly.FullName` -
  `typeof(ITaskItem).Assembly` *is* `Microsoft.Build.Framework`) and retry. This is why
  `Microsoft.Build.Framework.ITaskItem` resolves even though it is written without an assembly
  qualifier, and why authors can write `ITaskItem` parameter types without spelling out the assembly.
* **Branch (A) only reorders the attempts** for names that start with `Microsoft.Build.Framework.`: it
  resolves against the loaded Framework assembly **first**, then falls back to a bare `Type.GetType`.
  This is a deliberate workaround (internal bug 1448821): Visual Studio can have **more than one
  version** of `Microsoft.Build.Framework.dll` loaded, and a bare `Type.GetType("Microsoft.Build.Framework.ITaskItem")`
  could otherwise bind to the *wrong* version, yielding a `Type` that is reference-unequal to the
  engine's `typeof(ITaskItem)` and then failing the `== typeof(ITaskItem)` input check above with a
  spurious `UnsupportedTaskParameterTypeError`. Forcing the loaded-Framework identity first pins the
  resolution to the engine's own `ITaskItem`.
* **Assembly-qualified names** (containing a comma) take branch (B) and resolve through the bare
  `Type.GetType(expandedType)` using the supplied assembly identity.

If resolution yields `null`, the author gets a reported `InvalidProjectFileException`
(`InvalidEvaluatedAttributeValue`); if it resolves but fails `TaskParameterTypeVerifier`, they get
`UnsupportedTaskParameterTypeError`. Both are observable build errors.

---

## 4. The `ITaskItem` implementation landscape

`ITaskItem` and `ITaskItem2` are **public** interfaces in `Microsoft.Build.Framework`
([ITaskItem2.cs](../../src/Framework/ITaskItem2.cs)). The concrete implementations the product ships:

| Type | Assembly | Visibility | Role |
| --- | --- | --- | --- |
| [`Microsoft.Build.Utilities.TaskItem`](../../src/Utilities/TaskItem.cs#L37) | Microsoft.Build.Utilities.Core | **public**, `sealed` | The canonical item a task author constructs (`new TaskItem(spec)`). Implements `ITaskItem2, IMetadataContainer`. |
| [`Microsoft.Build.Execution.ProjectItemInstance.TaskItem`](../../src/Build/Instance/ProjectItemInstance.cs#L781) | Microsoft.Build | internal | The **engine's** runtime item - what the engine actually hands to and reads back from tasks. Implements `ITaskItem2` (+ engine interfaces). |
| [`Microsoft.Build.Framework.TaskItemData`](../../src/Framework/TaskItemData.cs#L17) | Microsoft.Build.Framework | internal | Lightweight immutable item (e.g. binary-log replay). Implements `ITaskItem, IMetadataContainer`. |
| `Microsoft.Build.BackEnd.TaskParameter.TaskParameterTaskItem` | MSBuildTaskHost | internal (nested) | Item marshalled across the out-of-proc task-host boundary. |

As of [task-parameter-type-registration-api.md](../specs/task-parameter-type-registration-api.md), the
registry pre-registers the Framework-visible item types (`ITaskItem`, `ITaskItem2`, `TaskItemData`) and
the engine's `ProjectItemInstance.TaskItem` (from `Microsoft.Build`); the public
`Microsoft.Build.Utilities.TaskItem` is registered by a host through the public API (it is above
Framework). The private `TaskParameterTaskItem` is never declared as a parameter type and is not
registered. Item types are registered **without** member rooting (they are validated by assignability,
never member-reflected), so this is free under trimming.

### Can users create any implementation?

**Yes - the interfaces are public and the engine consumes items purely through them.** A task can
expose an **output** property typed as a custom `ITaskItem`/`ITaskItem2` and the engine will read it
through the interface. Two caveats that follow directly from §1 and from the type design:

* For an **inline-task `<ParameterGroup>` declaration**, a custom item type is legal **only as an
  output** (`IsAssignableFrom`); as an **input** only the exact `ITaskItem`/`ITaskItem[]` are legal.
* `Microsoft.Build.Utilities.TaskItem` is `sealed` **on purpose** - its class comment notes the engine
  "instantiates its own copy of this type," so subclassing it would not get engine behavior. Authors
  who want a custom item implement the interface directly rather than deriving from the public class.

In practice the overwhelming majority of real task parameters are `string`, a BCL value type, or
`ITaskItem`/`ITaskItem[]`; custom `ITaskItem` *parameter types* are rare, and custom value-type
parameters rarer still.

---

## 5. A public, trim-safe task parameter type registry - implemented

This is **implemented** in [task-parameter-type-registration-api.md](../specs/task-parameter-type-registration-api.md):
a reflection-free [`TaskParameterTypeRegistry`](../../src/Framework/TaskParameterTypeRegistry.cs) keyed by
type name, pre-registered with the intrinsics, `string`, and the `ITaskItem` types and rooted for
trimming, with two public registration methods on `Microsoft.Build.Utilities.TaskItem`
(`RegisterTaskParameterValueType<T>` for value types, `RegisterTaskParameterItemType<T>` for `ITaskItem`
types). It is the closed-world registration recipe (strategy **S5** /
[aot-trimming-strategy.md §6](aot-trimming-strategy.md#6-aot-safe-reflection-over-registered-types--the-annotation-recipe)):

```csharp
// The DAM on T roots T's entire member surface, so the trimmer preserves a registered item type
// in full and any later reflection over it (construct, set metadata, copy) stays trim-safe.
public static void RegisterTaskItemType<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>()
    where T : ITaskItem
{
    s_itemTypesByName[typeof(T).FullName] = typeof(T);   // name -> Type, no Type.GetType at use time
}
```

A host (the SDK, a custom app) would register its item types at startup; the engine would resolve a
declared/serialized item parameter type via the dictionary instead of `Type.GetType`, and the
trimmer would preserve those types because each `Register<Concrete>()` call site roots `Concrete`.
This is the same shape as the shipped `SdkResolver.Register`
([sdk-resolver-host-registration-api.md](../specs/sdk-resolver-host-registration-api.md)) and the
proposed static task generator ([task-factory-aot.md §7](task-factory-aot.md)).

**But weigh what it actually buys, against the two sites in §2:**

* It **cannot fully close the set.** §1 shows the legal set includes *any* value type (`IsValueType`)
  and *any* `ITaskItem` implementer. A registry bounds the item-type half (a host can enumerate its
  item classes) but not arbitrary user `struct`s. So a registry resolves the **registered + well-known**
  subset without reflection and leaves a remainder that must still fall back to `Type.GetType`
  (narrowed honest RUC) or be declared unsupported under AOT (fail observably).
* For [`ParseUsingTaskParameterGroupElement`](../../src/Build/Instance/TaskRegistry.cs#L1764) this is now
  **done**: the registry resolves the known types reflection-free and the by-name fallback is gated, which
  **retired the `IL2057`/`IL2096` suppressions** there. The surrounding inline-task *execution* stays
  AOT-incompatible (it compiles source at run time) and gated by `EnableReflectiveTaskExecution`, so the
  win is a trim-clean *parse*, not a runnable inline task under AOT.
* The registry's **real value is the compiled-task parameter-binding path** - the `LoadedType` /
  `TaskExecutionHost.SetTaskItemParameter` reflection over item-typed properties, which is the genuine
  AOT-hard tier (now behind `EnableReflectiveTaskExecution`). A member-rooted, closed `ITaskItem` type
  set is what would eventually let that path reflect trim-safely over a known world, complementing the
  task source-generator. If an `ITaskItem` registry is built, build it for **that**, and let the
  inline-task declaration site reuse it opportunistically.

**Bottom line.** The registry is implemented and wired into the `<ParameterGroup>` resolution, retiring
its two trim suppressions while keeping observable failure for unknown types under AOT. It does not fully
close the set (arbitrary value types remain open-ended), and the higher-value target remains the
**compiled-task** parameter-binding path (`LoadedType` / `TaskExecutionHost.SetTaskItemParameter`), for
which the same registry is the reusable primitive - see
[task-class-registration-api.md](../specs/task-class-registration-api.md).

---

## Related

* [task-parameter-type-registration-api.md](../specs/task-parameter-type-registration-api.md) - the implemented registry this section describes.
* [task-class-registration-api.md](../specs/task-class-registration-api.md) - the companion proposal for registering task *classes*.
* [aot-trim-suppressions.md - Backlog deep analysis (Group B)](aot-trim-suppressions.md#group-b--inline-task-by-name-type-resolution) - why the serialized `Type.GetType` row remains Backlog and why the `<ParameterGroup>` site was retired.
* [aot-trimming-strategy.md](aot-trimming-strategy.md) - S5 (registration) and the `Register<[DAM] T>()` recipe (§6).
* [task-factory-aot.md](task-factory-aot.md) - the proposed static task registration / source generator this registry would complement.
