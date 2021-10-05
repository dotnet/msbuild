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
Shared code needs access to assembly resources e.g. for loading error messages for exceptions. Each assembly that shares code, _must_ define a class called `AssemblyResources` in the shared namespace, with an `internal static readonly` member of type `ResourceManager` called `resources`. Each sharing assembly is required to do this because only it knows what the manifest resource name (a.k.a. logical name) of its resources is. Shared code can then statically reference the assembly’s resources. If the `AssemblyResources` class is not defined, it is a compile-time error.

The `AssemblyResources` class at a minimum must look like this:

```cs
using System.Resources;
using System.Reflection;

namespace Microsoft.Build.Shared
{
    internal static class AssemblyResources
    {
        internal static readonly ResourceManager resources =
            new ResourceManager(
                "<manifest resource name>",
                Assembly.GetExecutingAssembly());
    }
}
```

NOTE: the class is explicitly marked `static`, because it only contains static members and methods -- making the class static prevents it from being instantiated, and allows the compiler to flag the (accidental) addition of instance members.
___

## **Shared Resources**
Shared code sometimes needs to define its own resources. If this were not allowed, then each sharing assembly would have to redefine the same set of resources on behalf of the shared code. As with code, maintaining multiple copies of the same resources is not desirable.

Shared resources must be placed in the file `Strings.shared.resx` in the shared code directory. All resource names must be prefixed with “`Shared.`” to distinguish the shared resources from an assembly’s primary resources. Each sharing assembly must add an `internal static readonly` member of type `ResourceManager`, called `sharedResources`, to the `AssemblyResources` class. This is necessary because only the sharing assembly can assign the correct manifest resource name to the shared resources. Shared code can then statically reference the shared resources. The absence of either the `AssemblyResources` class, or the `sharedResources` member is a compile-time error.

For assemblies that share resources, the `AssemblyResources` class at a minimum must look like this:

```cs
using System.Resources;
using System.Reflection;

namespace Microsoft.Build.Shared
{
    internal static class AssemblyResources
    {
        internal static readonly ResourceManager resources =
            new ResourceManager(
                "<manifest resource name>",
                Assembly.GetExecutingAssembly());

        internal static readonly ResourceManager sharedResources =
            new ResourceManager(
                "<manifest resource name of shared resources>",
                Assembly.GetExecutingAssembly());
    }
}
```

To simplify the retrieval of resources, the `AssemblyResources` class can optionally define a method called `GetString()` that searches both the assembly’s primary resources as well as its shared resources for a given string. For example:

```cs
internal static string GetString(string name)
{
    string resource = resources.GetString(name, CultureInfo.CurrentUICulture);

    if (resource == null)
    {
        resource = sharedResources.GetString(name, CultureInfo.CurrentUICulture);
    }

    return resource;
}
```

NOTE: if the above method is added to the `AssemblyResources` class, it is advisable to make both `resources` and `sharedResources` private (instead of `internal`) to unify access to assembly resources.