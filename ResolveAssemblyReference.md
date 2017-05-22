One of the most important tasks in the MSBuild toolset is `ResolveAssemblyReference` (RAR). Its purpose is to take all the references specified in .csproj files (or elsewhere) via the `<Reference>` item and map them to paths to assembly files on disk. The compiler only can accept a .dll path on disk as a reference, so `ResolveAssemblyReference` converts strings like `mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089` to paths like `C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.1\mscorlib.dll` which are then passed to the compiler via the /r switch.

Additionally RAR determines a closure of all .dll/exe references recursively, and for each of them determines whether it should be copied to the build output directory or not. It doesn't do the actual copying (that is handled later, after the actual compile step), but it prepares an item list of files to copy.

RAR is invoked from the `ResolveAssemblyReferences` target:
![image](https://cloud.githubusercontent.com/assets/679326/21276536/ca861968-c386-11e6-9a06-a3d74532ed15.png)

If you notice the ordering, ResolveAssemblyReferences is happening before Compile, and CopyFilesToOutputDirectory happens after Compile (obviously).

## Source Code
You can browse Microsoft's MSBuild targets online at:
http://source.roslyn.io/#MSBuildFiles/C/ProgramFiles(x86)/MSBuild/14.0/bin_/amd64/Microsoft.Common.CurrentVersion.targets,1820
This is where the RAR task is invoked in the targets file.

The source code for RAR is at:
https://github.com/Microsoft/msbuild/blob/master/src/Tasks/AssemblyDependency/ResolveAssemblyReference.cs

## Inputs
RAR is very detailed about logging its inputs:
![image](https://cloud.githubusercontent.com/assets/679326/21276697/76ea6830-c387-11e6-94f7-cd523c19064e.png)
The Parameters node is standard for all tasks, but additionally RAR logs its own set of information under Inputs (which is basically the same as under Parameters but structured differently). RAR logs this information in a method called LogInputs():
https://github.com/Microsoft/msbuild/blob/xplat/src/XMakeTasks/AssemblyDependency/ResolveAssemblyReference.cs#L1249

The most important inputs are Assemblies and AssemblyFiles:

```
    <ResolveAssemblyReference
        Assemblies="@(Reference)"
        AssemblyFiles="@(_ResolvedProjectReferencePaths);@(_ExplicitReference)"
```

http://source.roslyn.io/#MSBuildFiles/C/ProgramFiles(x86)/MSBuild/14.0/bin_/amd64/Microsoft.Common.CurrentVersion.targets,1820

`Assemblies` is just using the contents of the `Reference` MSBuild item at the moment when RAR is invoked for the project. All the metadata/assembly references, including your NuGet references, go here. Each reference has a rich set of metadata attached to it:
![image](https://cloud.githubusercontent.com/assets/679326/21276904/5ef66a3e-c388-11e6-8c6f-500169d9b79d.png)

`AssemblyFiles` comes from `ResolveProjectReference` target's output item called `_ResolvedProjectReferencePaths`. `ResolveProjectReference` runs before RAR and it converts `<ProjectReference>` items to paths of built assemblies on disk. So the `AssemblyFiles` will contain the assemblies built by all referenced projects of the current project:
![image](https://cloud.githubusercontent.com/assets/679326/21276978/e67ee99a-c388-11e6-9796-33e75caa2dc6.png)

Another useful input is the boolean `FindDependencies` parameter which takes its value from the `_FindDependencies` property:
```
FindDependencies="$(_FindDependencies)"
```

You can set this property to false in your build to turn off analyzing transitive dependency assemblies.

## Execution

The source code of the main `Execute()` method can be found in MSBuild source code on GitHub:
https://github.com/Microsoft/msbuild/blob/xplat/src/XMakeTasks/AssemblyDependency/ResolveAssemblyReference.cs#L1877

The algorithm simplified is:
```
...
Line 1923: LogInputs();
...
// useful environment variable to set to crank up detailed search result logging
Line 1930: _logVerboseSearchResults = Environment.GetEnvironmentVariable("MSBUILDLOGVERBOSERARSEARCHRESULTS") != null;
...
Line 2087: ReferenceTable dependencyTable = new ReferenceTable(...) // main data structure
...
Line 2052: ReadStateFile(); // read the cache file from the `obj` directory if present
...
Line 2182: dependencyTable.ComputeClosure(allRemappedAssemblies, _assemblyFiles, _assemblyNames, generalResolutionExceptions);
...
Line 2213: // Build the output tables.
           dependencyTable.GetReferenceItems
           (
               out _resolvedFiles,
               out _resolvedDependencyFiles,
               out _relatedFiles,
               out _satelliteFiles,
               out _serializationAssemblyFiles,
               out _scatterFiles,
               out _copyLocalFiles
           );
...
Line 2274: WriteStateFile(); // write the cache file to the `obj` directory
...
Line 2284: LogResults();
...
```

Very simplified, the way it works is it takes the input list of assemblies (both from metadata and project references), retrieves the list of references for each assembly it processes (by reading metadata) and builds a transitive closure of all referenced assemblies, and resolves them from various locations (including the GAC, AssemblyFoldersEx, etc.).

It builds a ReferenceTable:
https://github.com/Microsoft/msbuild/blob/xplat/src/XMakeTasks/AssemblyDependency/ReferenceTable.cs

Referenced assemblies are added to the closure iteratively until no more new references are added. Then the algorithm stops.

Direct references that we started with are called Primary references. Indirect assemblies that were added to closure because of a transitive reference are called Dependency. Each indirect assembly remembers all the primary ("root") items that led to its inclusion and their corresponding metadata.

## Results

RAR is just as rich at logging results as it is for inputs:
![image](https://cloud.githubusercontent.com/assets/679326/21277329/ab52e9aa-c38a-11e6-9037-c98efbee0055.png)

Resolved assemblies are divided into two categories: Primary references and Dependencies. Primary references were specified explicitly as references of the project being built. Dependencies were inferred from references of references transitively.

**Important note:** RAR reads assembly metadata to determine the references of a given assembly. When the C# compiler emits an assembly it only adds references to assemblies that are actually needed. So it may happen that when compiling a certain project the project may specify a unneeded reference that won't be baked into the assembly. It is OK to add references to project that are not needed; they are just ignored.

## CopyLocal item metadata

References can also have the `CopyLocal` metadata or not. If the reference has `CopyLocal = true`, it will later be copied to the output directory by the `CopyFilesToOutputDirectory` target. In this example, DataFlow is CopyLocal while Immutable is not:
![image](https://cloud.githubusercontent.com/assets/679326/21277638/3dde6d16-c38c-11e6-8997-547ab51152aa.png)

If the CopyLocal metadata is missing entirely, it is assumed to be true by default. So RAR by default tries to copy dependencies to output unless it finds a reason not to. RAR is quite detailed about the reasons why it chose a particular reference to be CopyLocal or not.

All possible reasons for CopyLocal decision are enumerated here:
https://github.com/Microsoft/msbuild/blob/master/src/Tasks/AssemblyDependency/CopyLocalState.cs
It is useful to know these strings to be able to search for them in build logs.

## Private item metadata
An important part of determining CopyLocal is the Private metadata on all primary references. Each reference (primary or dependency) has a list of all primary references (source items) that have contributed to that reference being added to the closure.

 1. If none of the source items specify `Private` metadata, `CopyLocal` is set to `True` (or not set, which defaults to `True`)
 2. If any of the source items specify `Private=true`, `CopyLocal` is set to `True`
 3. If none of the source assemblies specify `Private=true` and at least one specifies `Private=false`, `CopyLocal` is set to `False`

Here's the source code:
https://github.com/Microsoft/msbuild/blob/master/src/Tasks/AssemblyDependency/Reference.cs#L1243

## Which reference set Private to false?

The last point is an often used reason for CopyLocal being set to false:
`This reference is not "CopyLocal" because at least one source item had "Private" set to "false" and no source items had "Private" set to "true".`

Unfortunately MSBuild doesn't tell us _which_ reference has set Private to false. I've filed an issue on MSBuild to improve logging: https://github.com/Microsoft/msbuild/issues/1485

For now, MSBuildStructuredLog offers an enhancement. It adds Private metadata to the items that had it specified above:
![image](https://cloud.githubusercontent.com/assets/679326/21278154/733b5f80-c38e-11e6-9eda-ef213b0e233c.png)

This greatly simplifies investigations and tells you exactly which reference caused the dependency in question to be CopyLocal=false.

Here's a special analyzer that was added to MSBuild Structured Log Viewer to add this information:
https://github.com/KirillOsenkov/MSBuildStructuredLog/blob/65f57afb858280effd4b56c59ef8d78de861241d/src/StructuredLogViewer/Analyzers/ResolveAssemblyReferenceAnalyzer/CopyLocalAnalyzer.cs

## GAC
The Global Assembly Cache plays an important role in determining whether to copy references to output. This is unfortunate because the GAC contents is machine specific and this results in problems for reproducible builds (where the behavior differs on different machine dependent on machine state, such as the GAC).

There were recent fixes made to RAR to alleviate the situation. You can control the behavior by these two new inputs to RAR:

```
    CopyLocalDependenciesWhenParentReferenceInGac="$(CopyLocalDependenciesWhenParentReferenceInGac)"
    DoNotCopyLocalIfInGac="$(DoNotCopyLocalIfInGac)"
```

## There was a conflict

A common situation is MSBuild gives a warning about different versions of the same assembly being used by different references. The solution often involves adding a binding redirect to the app.config file. 

A useful way to investigate these conflicts is to search in MSBuild Structured Log Viewer for "There was a conflict". It will show you detailed information about which references needed which versions of the assembly in question.