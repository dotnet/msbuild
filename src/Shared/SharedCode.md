# Shared Code

Code that can be consumed from its owning assembly should be compiled there rather than linked into multiple assemblies. `src/Shared` retains files whose assembly-local behavior or use as test assets requires source or content sharing.

## **Retained Files**

| Files | Consumers and reason for sharing |
| ----- | ------------------------------- |
| `ErrorUtilities.cs`, `ResourceUtilities.cs` | Build, Tasks, Utilities, and MSBuild compile these helpers against their own `AssemblyResources` implementation so diagnostics use the calling assembly's resources. |
| `UnitTests/TestAssemblyInfo.cs` | `src/Directory.Build.targets` includes this source in eligible test projects to configure their xUnit assembly attributes, test framework, and startup. These must be compiled into each test assembly, not just a referenced test library. |
| `UnitTests/App.config`, `UnitTests/xunit.runner.json` | Test projects share configuration files; `src/Directory.Build.targets` also selects the runner settings to prevent Arcade defaults from replacing them. |
| `EmptyDirectoryBuildFiles/*` | Empty props, targets, response, and package files isolate test builds from repository-wide settings. `src/Directory.Build.targets` copies them into executable output directories. |

These test files are not candidates for production assembly ownership.
___

## **Namespace**
Source-shared production helpers live in the `Microsoft.Build.Shared` namespace. Test setup uses the `Microsoft.Build.UnitTests` namespace.
___

## **Internal Access Only**
Source-shared helpers are compiled separately into each consuming assembly. They must remain **internal** and must not be used as cross-assembly contracts.

Even if two types in different assemblies have the same name and are in the same namespace, the CLR does _not_ recognize the types to be the same, because their assembly identities are different.

Define cross-assembly contracts once in their owning assembly rather than sharing their source.
___

## **Resources**
`ResourceUtilities` loads diagnostic strings through `AssemblyResources.GetString()`, and `ErrorUtilities` uses `ResourceUtilities` to format exceptions. Each assembly compiling these helpers must define its own internal `AssemblyResources` class in `Microsoft.Build.Shared`. This preserves assembly-local resource lookup; moving the helpers into a common library without an equivalent resource-ownership design would change which resources they use.

Each assembly initializes `PrimaryResources` with its own manifest resource name:

```cs
using System.Resources;

namespace Microsoft.Build.Shared;

internal static class AssemblyResources
{
    internal static ResourceManager PrimaryResources { get; } =
        new ResourceManager("<manifest resource name>", typeof(AssemblyResources).Assembly);
}
```

NOTE: the class is explicitly marked `static`, because it only contains static members and methods -- making the class static prevents it from being instantiated, and allows the compiler to flag the (accidental) addition of instance members.
___

## **Shared Resources**
Diagnostic strings common to the consuming assemblies are defined once in Framework rather than duplicated in each assembly.

Shared resources live in `Microsoft.Build.Framework`'s `SR.resx` (`src/Framework/Resources/SR.resx`). Assemblies compiling the helpers already reference `Microsoft.Build.Framework`, so they can consume these resources through Framework's generated `SR.ResourceManager`. Each consuming assembly exposes them via an `internal static` `ResourceManager` property called `SharedResources` on its `AssemblyResources` class. Shared resource names do **not** use any special prefix — they are ordinary names in Framework's `SR.resx`.

For assemblies that share resources, the `AssemblyResources` class looks like this:

```cs
using System.Resources;

namespace Microsoft.Build.Shared;

internal static class AssemblyResources
{
    internal static ResourceManager PrimaryResources { get; } =
        new ResourceManager("<manifest resource name>", typeof(AssemblyResources).Assembly);

    internal static ResourceManager SharedResources => Framework.Resources.SR.ResourceManager;
}
```

To simplify the retrieval of resources, the `AssemblyResources` class defines a `GetString()` method that searches the assembly’s primary resources first, then falls back to its shared resources:

```cs
internal static string GetString(string name, CultureInfo? culture = null)
{
    culture ??= CultureInfo.CurrentUICulture;
    string? resource = PrimaryResources.GetString(name, culture) ??
                       SharedResources.GetString(name, culture);

    Assumed.NotNull(resource, $"Missing resource '{name}'");

    return resource;
}
```