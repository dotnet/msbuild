# Task isolation
## Problem definition
Tasks in MSBuild are dynamically loaded assemblies with potentially separate and colliding dependency trees. Currently MSBuild on .NET Core has no isolation between tasks and as such only one version of any given assembly can be loaded. Prime example of this is Newtonsoft.Json which has multiple versions, but all the tasks must agree on it to work.
This problem is also described in #1754.

## Solution
Use [`AssemblyLoadContext`](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.loader.assemblyloadcontext?view=netcore-2.2) (ALC) to provide binding isolation for task assemblies. Each task assembly would be loaded into its own ALC instance.
* The ALC would resolve all dependencies of the task assemblies (see dependency resolution below)
* ALC would fallback to the Default for dependencies which the assembly doesn't carry with itself (frameworks and so on)
* ALC would probably have to forcefully fallback for MSBuild assemblies since it's possible that tasks will carry these, but the system requires for the MSBuild assemblies to be shared.

We also want to load groups of tasks which belong together into the same ALC (for example based on their location on disk) to improve performance. This will need some care as there's no guarantee that two random tasks have compatible dependency trees. As implemented, each task assembly is loaded into its own ALC.

## Potential risks
* Has some small probability of causing breaks. Currently all assemblies from all tasks are loaded into the default context and thus are "visible" to everybody. Tasks with following properties might not work:
  * Task has a dependency on an assembly, but it doesn't declare this dependency in its .deps.json and this dependency gets loaded through some other task. This is mostly fixable by implementing probing similar to today's behavior.
  * Two tasks from different assemblies which somehow rely on sharing certain types. If the new system decides to load these in isolation they won't share types anymore and might not work.
* Performance - task isolation inherently (and by design) leads to loading certain assemblies multiple times. This increases memory pressure and causes additional JITing and other related work.

## Additional consideration
* None of these changes would have any effect on MSBuild on .NET Framework
* Task isolation alone could be achieved on existing MSBuild

# Task dependency resolution
## Problem definition
Tasks with complex and specifically platform specific dependencies don't work out of the box. For example if a task uses [`LibGit2Sharp`](https://www.nuget.org/packages/LibGit2Sharp) package it will not work as is. `LibGit2Sharp` has native dependencies which are platform specific. While the package carries all of them, there's no built in support for the task to load the right ones. For example [source link](https://github.com/dotnet/sourcelink/blob/master/src/Microsoft.Build.Tasks.Git/GitLoaderContext.cs) runs into this problem.

## Solution
.NET Core uses `.deps.json` files to describe dependencies of components. It would be natural to treat task assemblies as components and use associated .deps.json file to determine their dependencies. This would make the system work nicely end to end with the .NET Core CLI/SDK and VS integration.
In .NET Core 3 there's a new type [`AssemblyDependencyResolver`](https://github.com/dotnet/coreclr/blob/master/src/System.Private.CoreLib/src/System/Runtime/Loader/AssemblyDependencyResolver.cs) which implements parsing and processing of a `.deps.json` for a component (or assembly). The usage is to create an instance of the resolver pointing to the assembly (in MSBuild case the task assembly). The resolver parses the `.deps.json` and stores the information. It exposes two methods to resolve managed and native dependencies.
It was designed to be used as the underlying piece to implement custom ALC. So it would work nicely with task isolation above.

## Potential risks
* Small probability of breaking tasks which have `.deps.json` with them and those are not correct. With this change the file would suddenly be used and could cause either load failures or different versions of assemblies to get loaded.

## Additional consideration
* Task dependency resolution requires APIs which are only available in .NET Core 3.0 (no plan to backport), as such MSBuild will have to target netcoreapp3.0 to use these APIs.

We decided not to implement `AssemblyDependencyResolver` in the .NET Core 3.x timeframe because of the uncertain impact of the change. We should reconsider in the .NET 5 timeframe.
