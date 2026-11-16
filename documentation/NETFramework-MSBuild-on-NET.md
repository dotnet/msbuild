# The .NET Framework flavor of MSBuild in a .NET process

A tool or IDE that runs on .NET can load the .NET Framework flavor of MSBuild (the assemblies next to
MSBuild.exe in Visual Studio) into its own process, for example with
[MSBuildLocator](https://github.com/microsoft/MSBuildLocator), to evaluate and build projects with the
Visual Studio toolset. The .NET Framework assemblies run on the .NET runtime in that case. This page
lists what works in that configuration and the rule that code changes have to follow to keep it working.

## What runs in the .NET process

These parts are supported and are run in the host process:

- Evaluation, including SDK resolution (`Microsoft.DotNet.MSBuildSdkResolver` is loaded with
  `Assembly.LoadFrom`, because .NET ignores `AssemblyName.CodeBase`).
- `BuildManager` with out-of-proc nodes (`BuildParameters.DisableInProcNode = true`). The nodes are
  MSBuild.exe processes on .NET Framework, so every task runs on .NET Framework. Node pipes are opened
  with `PipeOptions.CurrentUserOnly`, which makes `Connect` check the owner of the pipe.
- Loggers, including the binary logger.
- The clients of the MSBuild server, the build coordinator and the RAR node.

The in-proc node also runs, with these limits:

- A task assembly that depends on the binding redirects in `MSBuild.exe.config` cannot be loaded. For
  example, `NuGet.Build.Tasks.dll` references `Microsoft.Build.Utilities.v4.0`, so restore and the NuGet
  targets of SDK projects fail. Use out-of-proc nodes for such builds.
- A `[LoadInSeparateAppDomain]` task (`AppDomainIsolatedTask`) runs in the current AppDomain, as in the
  .NET flavor of MSBuild. `GenerateResource` does not create an AppDomain either.
- COM tasks (`ResolveComReference`, `RegisterAssembly`, `UnregisterAssembly`) and the ClickOnce signing
  tasks use .NET Framework-only APIs and fail.
- `GenerateResource` fails for a `.resx` file with resources of types other than strings and primitives
  (for example `System.Drawing.Point` or a bitmap): the resources are written with
  `System.Resources.ResourceWriter`, which throws `PlatformNotSupportedException` for such values on
  .NET. Out-of-proc nodes run the task on .NET Framework and are not affected.

The host has to include the Windows Desktop runtime (for example with `<UseWPF>true</UseWPF>` or a
`FrameworkReference` to `Microsoft.WindowsDesktop.App`): `Microsoft.Build.Framework` has an assembly
attribute from `System.Xaml`, which is read when its resources are loaded.

## The rule for code changes

On .NET, a method that references a type or member which the runtime does not have throws
`TypeLoadException` or `MissingMethodException` when the method is compiled. This happens before any of
its code runs, also when the reference is in a branch that does not run and also inside a `try` block.
A field whose type does not exist has the same effect for every method that reads or writes it. The
APIs that the .NET Framework flavor of MSBuild uses and .NET does not have include:

| API | Present on .NET |
|---|---|
| `System.Runtime.Remoting.*` (`RemotingServices`, `ILease`, `ClientSponsor`) | no |
| `AppDomain.CreateDomain(string, Evidence, AppDomainSetup)` | no (`CreateDomain(string)` is present and throws) |
| `new AppDomainSetup()`, `AppDomainSetup.GetConfigurationBytes`, `SetConfigurationBytes` | no |
| `PipeStream.GetAccessControl()` (instance method) | no (.NET has it as an extension method) |
| `NamedPipeServerStream` constructors with `PipeSecurity` | no |
| `AssemblyName.KeyPair` | present, throws `PlatformNotSupportedException` |

Code that can run in the .NET process follows this rule:

1. The reference is in its own method marked `[MethodImpl(MethodImplOptions.NoInlining)]`.
2. The method is called only when `RuntimeCapabilities` (in `Microsoft.Build.Framework`) reports the API
   as available: `SupportsRemoting`, `SupportsAppDomains`, `SupportsPipeOptionsCurrentUserOnly`,
   `AssemblyLoadUsesCodeBase`, `SupportsAssemblyNameKeyPair`. Each check tests for the API, not for the
   runtime version.
3. For the common cases, `NetFrameworkApis` already has such methods: `IsTransparentProxy`, `Disconnect`,
   `GetConfigurationBytes` and `CreateAppDomainSetup`.

`NetFrameworkApiIsolation_Tests.NetFrameworkOnlyApisAreReferencedOnlyFromIsolatedMethods` reads the IL of
every method in `Microsoft.Build`, `Microsoft.Build.Framework`, `Microsoft.Build.Tasks.Core` and
`Microsoft.Build.Utilities.Core` and fails when one of the APIs above that .NET does not have is
referenced from a method that is not marked `NoInlining` and is not listed as running only on .NET
Framework. APIs that are present and throw, such as `AssemblyName.KeyPair`, are not detected by the test. When the test reports a new
method, move the reference into a `NoInlining` method behind a `RuntimeCapabilities` check, or, if the
method runs only in a .NET Framework process (an MSBuild.exe node, a task host, or a remoting callback),
add it to `s_methodsThatRunOnlyOnNetFramework` with the reason.

To check a build by hand, load the .NET Framework assemblies into a .NET process and call
`RuntimeHelpers.PrepareMethod` for every method: the methods that throw are the ones .NET cannot
compile.
