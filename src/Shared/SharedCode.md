# Shared Code

## **Namespace**
All shared code _must_ live in `Microsoft.Build.Shared` namespace.
___

## **Internal Access Only**
Shared code gets compiled into every assembly as it is referenced by. However this does _not_ mean that the shared types can migrate across the assemblies they are in.

Even if two types in different assemblies have the same name and are in the same namespace, the CLR does _not_ recognize the types to be the same, because their assembly identities are different.

As a result all shared code _must_ have **internal** access only. There should be _no_ public types in shared code.
___

## **Resources**
Shared code needs access to assembly resources e.g. for loading error messages for exceptions. Each assembly that shares code, _must_ define a class called `AssemblyResources` in the shared namespace, exposing an `internal static` `ResourceManager` property called `PrimaryResources`. Each sharing assembly is required to do this because only it knows what the manifest resource name (a.k.a. logical name) of its resources is. Shared code can then statically reference the assembly’s resources. If the `AssemblyResources` class is not defined, it is a compile-time error.

The `AssemblyResources` class at a minimum must look like this:

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
Shared code sometimes needs to define its own resources. If this were not allowed, then each sharing assembly would have to redefine the same set of resources on behalf of the shared code. As with code, maintaining multiple copies of the same resources is not desirable.

Shared resources live in `Microsoft.Build.Framework`'s `SR.resx` (`src/Framework/Resources/SR.resx`). Because every assembly that shares code already references `Microsoft.Build.Framework`, they can all consume these resources through Framework's generated `SR.ResourceManager`. Each sharing assembly exposes them via an `internal static` `ResourceManager` property called `SharedResources` on its `AssemblyResources` class. Shared resource names do **not** use any special prefix — they are ordinary names in Framework's `SR.resx`.

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