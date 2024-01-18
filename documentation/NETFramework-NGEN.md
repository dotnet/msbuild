# .NET Framework NGEN Considerations

NGEN is the name of the legacy native AOT technology used in .NET Framework. Compared to its modern .NET counter-part,
NGEN has the following key characteristics:
- Native code is always stored in separate images located in a machine-wide cache.
- Native images are generated on user machines, typically during app installation, by an elevated process.
- Native images are specific for a given IL image (its identity, *not* its location) and its exact dependencies as they are bound to at run-time.

Check the [Ngen.exe (Native Image Generator)](https://learn.microsoft.com/en-us/dotnet/framework/tools/ngen-exe-native-image-generator) Microsoft Learn article for an overview of how NGEN works.

## NGEN in Visual Studio

Visual Studio use NGEN for almost everything that ships in the box. The sheer amount of code which needs to be
compiled makes it impractical for native image generation to occur synchronously during installation. Instead,
VS installer queues up assemblies for deferred compilation by the NGEN service, which typically happens when the
machine is idle. To force native images to be generated, one can execute the following command in an elevated
terminal window:

```
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\ngen eqi
```

The .NET Framework build of MSBuild is inserted into VS and it registers itself for NGEN by including `vs.file.ngenApplications`
in the relevant [files.swr](https://github.com/dotnet/msbuild/blob/main/src/Package/MSBuild.VSSetup/files.swr) entries. MSBuild
is hosted in several processes, most notably the stand-alone command line tool `MSBuild.exe` and the main IDE process `devenv.exe`.
Because each process runs with slightly different dependencies - `MSBuild.exe` loads most of them from `[VS install dir]\MSBuild\Current\Bin`
or `[VS install dir]\MSBuild\Current\Bin\[arch]` while `devenv.exe` has its own set loaded from other parts of the VS installation -
we NGEN our code twice. This is encoded by multiple `vs.file.ngenApplications` entries for a single file in `files.swr`.
The special `[installDir]\Common7\IDE\vsn.exe` entry represents devenv.

The bad thing about this is that the system is fragile and adding a dependency often results in having to tweak `files.swr` or
`devenv.exe.config`, the latter of which is generated from the file named `devenv.urt.config.tt` in the VS source tree. The good
thing is that regressions, be it a failure to compile an NGEN image or a failure to use an NGEN image, are reliably detected
by the VS PR gates so they are fixed before MSBuild is inserted into the product.

## NGEN image loading rules

The Common Language Runtime can be finicky about allowing a native image to load. We usually speak of "NGEN rejections" where a native
image has successfully been created but it cannot be used at run-time. When it happens, the CLR falls back to loading the IL assembly
and JITting code on demand, leading to sub-optimal performance.

One major reason why a native image is rejected is loading into the LoadFrom context. The rules are excruciatingly complex, but suffice
it to say that when an assembly is loaded by `Assembly.LoadFrom`, it is automatically disqualified from having its native image used.
This is bad news for any app with an add-in system where extension assemblies are loaded by path.

## SDK resolvers

One class of assemblies loaded by MSBuild by path are SDK resolvers. MSBuild scans the `SdkResolvers` subdirectory to discover
the set of resolvers to use when evaluating projects. Extensible in theory, though in reality only a couple of resolvers actually
exist. Because resolvers ship as part of VS, it is not difficult to make sure their assemblies are properly NGENed. The hard part is
loading them with the regular `Assembly.Load` so the native images can be used. MSBuild cannot simply extend its probing path to
include the relevant subdirectories of `SdkResolvers` because 1) It is outside of the app directory cone for amd64 and arm64 versions
of `MSBuild.exe` and 2) Not all resolvers actually live under this directory; the system allows them to be placed anywhere.
It is unfortunately also not straightforward to add a binding redirect with a [`codeBase`](https://learn.microsoft.com/en-us/dotnet/framework/configure-apps/file-schema/runtime/codebase-element)
entry pointing to the right assemblies, because this requires knowing the exact assembly versions. 

### Microsoft.DotNet.MSBuildSdkResolver

This is the most-commonly-used resolver, capable of resolving "in-box" SDKs that ship with the .NET SDK and .NET SDK workloads. Since the resolver assembly
is located at a known path relative to MSBuild and has very few dependencies, none of which are used anywhere else, we have decided to
freeze the version of the resolver plus dependencies, so that their full names can be specified in `MSBuild.exe.config`, e.g.

```xml
    <dependentAssembly>
      <assemblyIdentity name="Microsoft.DotNet.MSBuildSdkResolver" culture="neutral" publicKeyToken="adb9793829ddae60" />
      <codeBase version="8.0.100.0" href=".\SdkResolvers\Microsoft.DotNet.MSBuildSdkResolver\Microsoft.DotNet.MSBuildSdkResolver.dll" />
    </dependentAssembly>
```

Additionally, `MSBuild.exe.config` has the following entry, which enables us to refer to the resolver by simple name.

```xml
<qualifyAssembly partialName="Microsoft.DotNet.MSBuildSdkResolver" fullName="Microsoft.DotNet.MSBuildSdkResolver, Version=8.0.100.0, Culture=neutral, PublicKeyToken=adb9793829ddae60" />
```

This has a small advantage compared to hardcoding `Microsoft.DotNet.MSBuildSdkResolver, Version=8.0.100.0, Culture=neutral, PublicKeyToken=adb9793829ddae60`
directly in the code, as it can be modified to work in non-standard environments just by editing the app config appropriately.

The resolver loading logic in MSBuild [has been updated](https://github.com/dotnet/msbuild/pull/9439) to call `Assembly.Load(AssemblyName)` where the `AssemblyName` specifies the
simple name of the assembly, e.g. `Microsoft.DotNet.MSBuildSdkResolver`, as well as its `CodeBase` (file path). This way the CLR assembly
loader will try to load the assembly into the default context first - a necessary condition for the native image to be used - and fall back
to LoadFrom if the simple name wasn't resolved.

### Microsoft.Build.NuGetSdkResolver

The NuGet resolver has many dependencies and its version is frequently changing, so the technique used for `Microsoft.DotNet.MSBuildSdkResolver`
does not apply in its current state. However, the NuGet team is [looking to address this](https://github.com/NuGet/Home/issues/11441) by:

1) ILMerge'ing the resolver with its dependencies into a single assembly.
2) Freezing the version of the assembly.

When this happens, the cost of JITting `Microsoft.Build.NuGetSdkResolver` will be eliminated as well.

## NuGet.Frameworks

When evaluating certain property functions, MSBuild requires functionality from `NuGet.Frameworks.dll`, which is not part of MSBuild proper.
The assembly is loaded lazily from a path calculated based on the environment where MSBuild is running and the functionality is invoked
via reflection. Similar to the NuGet resolver, the version is changing and it is not easy to know it statically at MSBuild's build time.
But, since there are only a handful of APIs used by MSBuild and they take simple types such as strings and versions, this has been
addressed by loading the assembly into a separate AppDomain. The AppDomain's config file is created in memory on the fly to contain the
right binding redirects, allowing MSBuild to use `Assembly.Load` and get the native image loaded if it exists.

This approach has some small startup cost (building the config, creating AppDomain & a `MarshalByRefObject`) and a small run-time overhead
of cross-domain calls. The former is orders of magnitude smaller that the startup hit of JITting and the latter is negligible as long as
the types moved across the AppDomain boundary do not require expensive marshaling.

## Task assemblies

This is the proverbial elephant in the room. MSBuild learns about tasks dynamically as it parses project files. The `UsingTask`
element tends to specify the `AssemblyFile` attribute, pointing to the task assembly by path. Consequently MSBuild uses
`Assembly.LoadFrom` and no native images are loaded. Even task assemblies located in the SDK are problematic because MSBuild is
paired with an SDK on users machine at run-time. Unlike SDK resolvers and NuGet.Frameworks, which are part of the same installation
unit, this is a true dynamic inter-product dependency. Additionally, the task API is complex and involves a lot of functionality
provided to tasks via callbacks (e.g. logging) so the overhead of cross-domain calls may be significant. And that's assuming that
suitable native images exist in the first place, something that both VS and SDK installers would need to handle (task assemblies
in each installed SDK would need to be NGENed against each installed version of VS).

Hosting task assemblies in separate AppDomains looks like a major piece of work with uncertain outcome. We haven't tried it yet
and most task code is JITted.
