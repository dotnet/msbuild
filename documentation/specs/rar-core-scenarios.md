# Resolve Assembly Reference core scenarios

This document aims to capture the core functionality provided by the ResolveAssemblyReference task when building .NET (_Core_ - pun intended) projects.
The goal is to rationalize and optimize the task, ultimately achieving substantially better performance and crossing out RAR from the list of notoriously
slow build tasks.

## Overview

RAR is the Swiss army knife of assembly resolution. Very extensible and universal, exposing over 50 documented parameters and supporting 10 different
locations where it searches for assemblies. Please see the [official documentation](https://learn.microsoft.com/visualstudio/msbuild/resolveassemblyreference-task) and
the [ResolveAssemblyReference page](https://github.com/dotnet/msbuild/blob/main/documentation/wiki/ResolveAssemblyReference.md) for a detailed description
of its features.

While all of RAR's functionality has to be supported for backward compatibility, some parts are more relevant for modern builds than others. For example,
if we focus only on building .NET Core / .NET 5+ projects, resolving assemblies in the Global Assembly Cache (GAC) is not supported. In fact, most of
the "resolvers", internal classes implementing various resolution strategies, are not used in modern scenarios.

## Requirements

Looking at the RAR contract at a high-level, it is effectively transforming one string array to another. It is passed an array of strings specifying the
assemblies required for the build, and returns an array of strings specifying full paths to assembly files on disk. Not necessarily a 1:1 mapping because
assemblies are transitively probed for dependencies, thus the output array may be larger than input. Additionally, if an input assembly cannot be resolved,
RAR issues a warning and otherwise ignores the assembly. This may lead to the output array being smaller than input.

### Inputs

In a typical build targeting modern .NET (*not* .NET Framework), RAR inputs come from three sources.

1. SDK reference assemblies. These are full paths to assemblies distributed with the SDK. The SDK may get the list of assemblies for example by parsing the
corresponding `FrameworkList.xml`. Reference assemblies are passed to RAR with the `ExternallyResolved` metadatum set, which means that they are
transitively closed with respect to their dependencies. In other words, all dependencies, including transitive dependencies, of these assemblies are
guaranteed to be passed in.

1. NuGet references. These are again full paths to assemblies pre-resolved by the NuGet system. The `ExternallyResolved` metadatum is set for these as well,
signalling to RAR that it doesn't have to open the assembly files to read their AssemblyRef tables.

1. Project references. When a project depends on another project, the output file of the dependency is passed to RAR. Alternatively, a project may directly
reference a random file o disk, resulting in the same code path. Unlike SDK and NuGet, these references are not pre-resolved and RAR must open the assembly
files and use a .NET metadata reader to enumerate the AssemblyRef table to get the list of dependent assembly names. The dependent assembly names are
resolved to assembly files and newly discovered assembly files are again scanned for AssemblyRef's. This process repeats itself until a closure is
established.

The above sums up the functionality required from RAR in a nutshell. For extra clarity, note that RAR is invoked only once during build, and is passed the
combined SDK, NuGet, and project references in one input array.

## Design

To meet the requirements, RAR must internally be able to do the following.

- For each input reference passed as a file path, it must verify that the file path exists. If the file does not exist, RAR issues a warning and ignores
the reference.

- For each input reference passed as a file path, it must know what its assembly name is. For example, for a reference given as
`C:\_nugetpackages\microsoft.netcore.app.ref\7.0.2\ref\net7.0\Microsoft.VisualBasic.Core.dll`, RAR must figure out the assembly name to be
`Microsoft.VisualBasic.Core, Version=12.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a` so it can map it back to the reference when it sees
such an AssemblyRef in another assembly.

- For project references (reference given as a path to an assembly file that is not guaranteed to have its dependencies resolved), RAR must be able to look
up dependencies. If a dependency is not found in the assembly name -> file path map, RAR first searches the directory where the primary reference is located.
Failing that, it then uses pre-defined strategies, four of which are enabled by default when building modern projects: `CandidateAssemblyFiles`, `HintPathFromItem`,
`TargetFrameworkDirectory`, and `RawFileName`. Of these, only `CandidateAssemblyFiles` tends to actually be of potential use. `CandidateAssemblyFiles` is
configured to use all `Content` and `None` items defined in the build. Given an AssemblyRef found in a project reference, for example `MyLibrary, Version=1.0.0.0`,
if `Content` or `None` contains `MyLibrary.dll`, RAR will get its assembly name and see if there is a match.

## Scale

The typical total number of references processed by RAR when building one project is in the order of hundreds. Even if the project referenced everything
that comes with the .NET SDK, consumed a large number of NuGet packages, and was part of a large solution, it would likely reach only low thousands of references.
In the OrchardCore solution, for example, the median number of references passed to and from RAR is 317 and the maximum is 629.

While CPU optimizations can definitely make a difference, at this scale I/O is far more concerning. Building upon the design notes above, here is where RAR
has to touch the disk.

- **File existence checks**. RAR tends to invoke its resolvers sequentially with the first-one-wins semantics. It returns the first suitable file that actually
exists. As a special case, if RAR is given a full path, it checks if the file exists before returning it.
- Assembly name extraction. Given a file on disk, RAR needs to know its assembly name, i.e. version, culture, public key token, ... This requires opening the
file with a .NET metadata reader.
- **AssemblyRef extraction**. For references that are not marked with `ExternallyResolved`, RAR needs enumerate their dependencies. This, again, requires opening
the file with a .NET metadata reader.

## Optimizations

RAR optimizations tend to revolve around caching of information that is expensive to calculate.

### Existing caching

Over the years RAR has implemented several layers of caches, both in-memory and on-disk. An inventory follows.

#### **Per invocation in-memory cache**

Not surprisingly, RAR caches the result of I/O operations in a cache in memory. The lifetime of this cache is one RAR invocation because generally we cannot
assume that files on disk don't change between builds. It is implemented inside `SystemState` as several dictionaries keyed off of the absolute file path.
One issue with this cache is that the key is not normalized so a file specified as `C:\dir\file` will use a different cache entry than the same file specified as
`C:/dir/file`.

#### **Per process in-memory cache**

This comes in multiple forms. `SystemState` has its own process-wide cache which maps file paths to data we need - assembly name, dependencies (AssemblyRef's), last
modification time (time stamp). It uses the time stamp to filter out invalid entries, i.e if the last modification time stamp changes, the cache contents is no longer
considered valid.

Another form of such a process-wide cache is a low-level cache of timestamps of immutable files, as implemented in `NativeMethods.GetLastWriteFileUtcTime`
and `FileClassifier`. The idea is that some files are not expected to be updated or deleted during inner loop development. For instance, a reference assembly that
comes with the SDK should never change and may get deleted only by uninstalling the SDK. The problem with this cache is that the file path-based classification is
more or less a heuristic and doesn't seem to work in all cases. Currently it is failing to recognize SDK reference assemblies under paths like
`C:\_nugetpackages\microsoft.aspnetcore.app.ref\7.0.2\ref\net7.0`, for example.

#### **Per project disk cache**

To help in cold build scenarios where RAR has not seen the project yet and the in-memory caches are empty or not relevant, RAR supports an on-disk cache using the
`StateFile` parameter. If specified, RAR will attempt to populate `SystemState` by deserializing the file before it starts. If `SystemState` has been modified
during RAR execution, its new contents will be serialized back to the file after RAR is done. This is somewhat non-deterministic because the cache being written
back is a union of what was read from the disk and what's in the memory, the latter depending on what other projects have been built by the current MSBuild process.
Building the exact same project with the exact same disk state will sometimes write the cache, sometimes it will not.

From performance point of view, while helping when RAR is cold, reading the cache unnecessarily slows down the execution when RAR is hot, because the cache contents
already is in memory so there is nothing to gain from reading it again. Of note here is the fact that as of _On disk cache serialization (#6094)_, RAR uses a custom
hand-optimized serializer for the cache file. It has better peformance than the previously used `BinaryFormatter`, not to mention being considered more secure.

#### **SDK disk pre-cache**

The observation that if there is no per project disk cache and RAR is cold, it has to read information about many SDK assemblies, led to the advent of the global
pre-cache. The idea is that the pre-cache is created as part of building the SDK and distributed with it. I.e. it is the SDK vendor's responsibility to create the
file, make it available on developer machines, and pass it to RAR in the `AssemblyInformationCachePaths` parameter when building relevant projects.

The pre-cache functionality is generic and available to any SDK vendor. The .NET SDK currently builds and distributes a file named `SDKPrecomputedAssemblyReferences.cache`
but it is not passed to RAR by default. Only a couple of projects in the dotnet organization are explicitly opted into consuming the pre-cache at the moment.

The downside of the current pre-cache design is that the full pre-cache ends up being written to each per project cache file upon completing the first RAR invocation.
For the .NET SDK the pre-cache contains more than 3000 assemblies. All of them stay in memory in the per process cache and all of them become part of the
per-project cache file, meaning that they will be read back from disk on each subsequent hot invocation. Not only does it hurt build performance, but it is also
wasteful to duplicate >2 MB worth of serialized assembly information in each project's intermediate directory.

## Proposed design

Completely rewriting RAR doesn't appear to be worthwhile. The requirements described above are for a typical build, not necessarily for all builds. RAR is highly
configurable and customizable, thus the bar for backward compatibility is very high. There are definitely opportunities for micro-optimizations without any functional
effect. Be it eliminating allocations, simplifying tight loops, reordering cases in hot switches, ..., there is a lot of low-hanging fruit. This by itself won't help
address the elephant in the room: the file I/O resulting from scanning of assemblies, checking their timestamps, and reading/writing on-disk caches.

For regular project references the system works as about as efficient as possible.

- In a cold scenario, where there is no state in memory or on disk, the referenced assembly file has to be scanned for its name and dependencies.
- In a warm scenario, where there is no state in memory but a disk cache exists, the assembly name and dependencies are read from the cache, together with the
corresponding timestamp which is compared to the current timestamp of the assembly file. If they match the cached data is used.
- In a hot scenario, where there is state in memory, the only I/O on the happy path is the timestamp check to verify that the file hasn't changed since last time.

There is a chance that the timestamp check can be replaced with something faster, although historically we haven't been able to come up with anything solid.
File watchers, for example, while tempting to use because the validity check in the happy case would cost literally nothing, suffer from an inherent race
condition. When a watched file is modified, the file watcher routine is not guaranteed to run by the time we need to reliably know whether the file is unchanged.
The exact time the routine is executed depends on the latency of the asynchronous OS callback, on thread pool availability, CPU scheduling, and more.

The focus of the following paragraphs is instead on SDK and NuGet references, because there are typically one to two orders of magnitude more of them than project
references, so optimizing them has the best bang for the buck.

### Obtain assembly names from the SDK

The SDK is currently already passing relevant metadata such as `AssemblyVersion` and `PublicKeyToken`, so there is no need for RAR to open the file and parse its
.NET metadata tables to get this information. This, together with the fact that SDK references are marked with `ExternallyResolved` so they cannot have dependencies
outside of the primary set, means that there is no need to cache anything about these assemblies. Everything RAR needs comes (or can come if it's not there already)
from the `Assemblies` parameter, explicitly provided on each invocation. Note, it may make sense to keep a cache in memory but it definitely doesn't make sense
to save it to disk.

If we do this, then in the warm and hot scenarios where the per project disk cache exists, we use it only to cache data about NuGet references and project references,
significantly reducing its size. By eliminating per-reference I/O for most references, RAR would see a significant performance boost.

This is assuming we trust the SDK that it passes correct data and we trust the user that they don't delete or overwrite their SDK files. If this assumption is not
valid, the mitigation would be to store and check the timestamp of each individual file. We would still benefit from smaller on disk caches, being able to store only
the timestamp and not assembly name for intact SDK references, but the hot scenario wouldn't get any faster than today.

### Treat NuGet references as immutable [shelved]

NuGet references live in the NuGet cache which is conceptually immutable. If RAR takes advantage of this, it can eliminate timestamp checks for NuGet references as
well. The risk is higher than for SDK references because overwriting files in the NuGet cache is commonly used as a cut-the-corner workaround. The benefit is smaller
because the number of NuGet references is typically lower. The proposal is to shelve this opportunity for now due to the unfavorable risk-benefit ratio.

### Don't load the per project disk cache when not needed

As described above, the on disk cache is not adding any value in the hot scenario because its contents already lives in the in-memory cache. The proposal is to
load it lazily only when (and if) RAR runs into an assembly that does not have a record in the in-memory cache. In developer inner loop, when the same solution is
built over and over again, the cache would typically not be loaded at all, unless the developer makes a change that actually changes the dependency graph.

### Save only relevant data to the per project disk cache

As for saving the per-project cache, we would guarantee that after RAR is done, the cache contains exactly the data needed for this specific project. This would
be done by keeping track of the items used during RAR execution, and writing those and only those to the cache. Having a cache that's guaranteed to have certain
well-defined content after each build is a very good property to have. For instance, in dev box scenarios it would otherwise be hard to reliably "prime" a repo
enlistment - the system may prime by building the full solution and then the developer uses the box to build a specific project that happens to have an incomplete
cache and get sub-optimal first-time build performance.

Saving of the per-project disk cache may be further optimized by

- Keeping the timestamp of the cache file in memory and skipping the save if the relevant cache items haven't become dirty (i.e. the dependencies have not changed)
*and* the timestamp of the cache file hasn't changed since the last save. In hot inner loop scenarios this would reduce the save to a timestamp check.
- Saving the file asynchronously, i.e. not blocking the build on completing the save operation.

### Don't use the SDK disk pre-cache

The idea of pre-generated on-disk cache is sound. For the `ExternallyResolved` SDK assemblies specifically, though, it effectively duplicates the information already
present in `FrameworkList.xml`. That is, it maps assembly paths to assembly names. If the need arises we may want to re-design the pre-cache to remove the major
drawback that it duplicates itself into all per-project caches. Cold RAR would load both caches and combine their contents (currently it's either or). Until then,
it should be OK to leave it unchanged and unused.
