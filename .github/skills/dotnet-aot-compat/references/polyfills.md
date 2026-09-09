# Trimming annotations on older target frameworks

## Reuse the repository owner

Before adding an attribute definition, read [AotTrimmingPolyfills.cs](../../../../src/Framework/Polyfills/AotTrimmingPolyfills.cs) and the affected project's references/guards. MSBuild already supplies trimming/AOT annotation types for its older targets through Framework.

The file is guarded by `!NET`; on the modern runtime the platform supplies the types. Its header explains why source generation alone was insufficient: some dependencies contain inaccessible internal copies of attributes, causing a polyfill generator to skip them even though this assembly still needs accessible definitions.

Do not paste another copy into Framework or assume each consuming project needs its own definitions. Check the actual compiler visibility and assembly/reference relationship first.

## Semantics worth preserving

The trimmer recognizes annotation types by their full names. Keep namespace, attribute usage, constructor/property signatures, enum values, and conditional compilation aligned with the owner and supported targets.

For `DynamicallyAccessedMemberTypes`, the existing values include:

| Member set | Value |
|---|---|
| None | `0` |
| PublicParameterlessConstructor | `0x0001` |
| PublicConstructors | `0x0002` plus `PublicParameterlessConstructor` |
| NonPublicConstructors | `0x0004` |
| PublicMethods / NonPublicMethods | `0x0008` / `0x0010` |
| PublicFields / NonPublicFields | `0x0020` / `0x0040` |
| PublicNestedTypes / NonPublicNestedTypes | `0x0080` / `0x0100` |
| PublicProperties / NonPublicProperties | `0x0200` / `0x0400` |
| PublicEvents / NonPublicEvents | `0x0800` / `0x1000` |
| Interfaces | `0x2000` |
| All | `~None` |

In particular, public constructors include the parameterless-constructor bit. Do not substitute a simplified enum that changes that contract.

The owner also defines capability/suppression/dependency/feature-guard attributes. Its `DynamicallyAccessedMembersAttribute` includes `AttributeTargets.Method`; an abbreviated older example omitted that target. Inspect the current source rather than duplicating a partial attribute list here.

`All` is not a default recommendation for rooting: choose the actual member surface needed by the operation. An annotation compiling on an older TFM does not mean that runtime supports Native AOT analysis/execution.

## Changing or using definitions

For an annotation change, cover the affected legacy and modern compilation paths and shared consumers when execution is part of the task. For a definition change, check that it does not collide with platform/generated types on another target.

Use the [MSBuild AOT reference](msbuild-aot.md) for analyzer/runtime evidence and the [Framework instructions](../../../instructions/framework.instructions.md) for source invariants. Documentation-only work does not require building these targets.
