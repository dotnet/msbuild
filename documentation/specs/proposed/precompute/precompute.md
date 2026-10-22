# MSBuild Precompute Mode

> **Note:** Precompute mode is currently in development and experimental. It may be shipped/implemented in future releases or it may be scrapped.

Precompute mode separates the creation of a build graph, called the precompute phase, from the execution of that build graph, called the compute phase, and uses that foundation to improve the reliability and performance of the build.

This is a fundamentally different approach to graph construction than [static graph](../../static-graph.md) (i.e. `/graph`).

This document is meant to be an introduction to precompute mode, not a full specification or detailed design.

Precompute mode mainly affects SDK authors (i.e. Targets authors).
Many users just consume an SDK and provide items / properties to them and so they will likely not need to change or understand precompute mode in detail.
Basic `.csproj/.vcxproj` project files, such as those that come from `dotnet new`, will need no changes to get the benefits of precompute mode.

## Motivation
MSBuild's lack of enforcement around inputs/outputs, timestamp-based incrementality, little support for handling target changes correctly, and other issues cause incremental builds to be widely considered untrustworthy for production outputs.
As a result, MSBuild users are forced to build dramatically more than would be optimal, usually from a clean state each time.
Precompute mode's goal is to systematically address and handle these issues to significantly improve reliability and performance.

## Overview
To create the build graph, precompute mode evaluates project files and visits targets just like MSBuild would do before precompute mode, but new syntax makes the target contents either a) execute immediately or b) skip execution and be encoded into the build graph.
Parameters, conditions, item and property groups that can be evaluated during precompute are expanded immediately by default.
Operations that depend on values produced during compute must be encoded into the graph.
Tasks must be configured to determine if they should be done during the precompute phase or be encoded into the graph.
For work encoded in the build graph that affects items or properties (e.g. a task's `<Output />` elements) the engine will note these items and properties.
Subsequent users of those items or properties must put those uses in the build graph and the engine will ensure the values are correctly propagated during the compute phase.
The value propagation is done the same whether the producer is executed, up-to-date, or replayed from cache.

By default each target becomes a separate node in the build graph with input/output files defined by the target.
Targets may optionally be merged together into a single node to optimize the build graph.
Precompute introduces more ways to declare inputs and outputs beyond the default file-based attributes on targets.
All possible inputs and outputs of a target must be determined during graph construction.
If a target attempts to read or write undeclared inputs or outputs during execution, it is treated as a failure.
The creation of the graph cannot depend on the execution of any part of the build graph and nodes in the build graph must be acyclic and correctly describe dependencies.

The build graph is executed based on the inputs/outputs between nodes, including compute-phase item/property dependencies.
Target-level ordering logic like `DependsOnTargets`, `BeforeTargets`, `AfterTargets` is no longer relevant.
The engine will hash the definition and inputs together and look into the cache to see if that state is known.
If a cache entry is found then the outputs are checked to see if they match the result, and if not then they can be replayed from a cache.
If the desired state cannot be found or replayed, then the node will be executed and a cache entry created.
The execution and outputs include expanding/propagating items and properties that are in the build graph if applicable.


## Benefits
With this design precompute mode can improve reliability and performance.

The precompute phase:
 - can be re-used from the prior build if none of its inputs changed avoiding the costs around evaluation and various targets that are part of the precompute phase
   - if anything that was used to do precompute changes then it executes (e.g. project files, SDKs, etc)
   - if a code file's content changes then the graph can be reused since implementation code does not affect the precompute phase
 - is more deterministic:
   - cannot be affected by the state of the outputs of the build since it is prevented from depending on them
   - targets are not skipped or partially executed based on what needs rebuilding which can cause subtle different behavior in today’s MSBuild incremental builds

With a graph, the compute phase can be improved to:
 - perform content-based hashing of all inputs/outputs (with higher accuracy than manual target inputs/outputs)
 - hash the definition of the node to safely re-use or re-execute based on if parameters/what tasks/etc have changed
   - this is often the result of expanding items and properties but also includes other changes that affect the final semantics of target(s)
 - store outputs into a cache based on the hash of the inputs and node definition
 - safely distribute the build across many machines to speed up large builds
 - sandbox targets to:
   - enforce the inputs/outputs are complete
   - determine which inputs were used and which were not and use that to skip hashing inputs that were unused
 - delete prior outputs to ensure a clean execution state
 - clean extraneous content that is no longer covered by the graph
 - allow nodes (including batches of a target) to execute concurrently if there is no file or compute-phase value dependency between them, including within a single project

## Simple Example
The following is a self-contained example that should demonstrate the basic concepts of precompute mode.
For simplicity, this example doesn't use any SDKs but hopefully it still illustrates the key concepts.

```xml
<Project>
    <!-- the main project file would contain some declarative properties/items to configure the build -->
    <PropertyGroup>
        <CompileOutputName>MyOutput</CompileOutputName>
        <SomePrepareSetting>True</SomePrepareSetting>
        <SomeCompilerSetting>True</SomeCompilerSetting>
    </PropertyGroup>
    <ItemGroup>
        <Generate Include="schema.def" />
        <Compile Include="code.src" />
    </ItemGroup>

    <!-- The MSBuild default SDK would typically define the default entry target and output locations, though that may be custom/overridden by others -->
    <Target Name="Build" DependsOnTargets="$(BuildDependsOn)" />
    <PropertyGroup>
        <OutputDirectory>bin</OutputDirectory>
    </PropertyGroup>

    <!-- The targets would typically be in SDKs somewhere else -->

    <!-- The first set of targets are from one SDK generating some code that is consumed by a different SDK -->
    <UsingTask TaskName="GenerateTask" AssemblyFile="path\to\GeneratorAssembly.dll" PrecomputeMode="Graph" />

    <Target Name="GeneratePrepTarget" Condition="@(Generate->Count()) > 0">
        <PropertyGroup Condition="'$(SomePrepareSetting)' == 'True'">
            <GeneratorFlags>/someflag</GeneratorFlags>
        </PropertyGroup>
        <ItemGroup>
            <GeneratorInputs Include="@(Generate)" />
            <GeneratorOutputs Include="@(Generate->'$(OutputDirectory)\%(Filename).src')" />
        </ItemGroup>
    </Target>

    <Target Name="GenerateTarget" DependsOnTargets="GeneratePrepTarget" Inputs="@(GeneratorInputs)" Outputs="@(GeneratorOutputs)">
        <PropertyGroup>
            <GeneratorFlags>$(GeneratorFlags) /version:1</GeneratorFlags>
        </PropertyGroup>
        <GenerateTask 
            Sources="@(GeneratorInputs)"
            OutputDirectory="$(OutputDirectory)"
            Flags="$(GeneratorFlags)"
        >
            <Output TaskParameter="GeneratorMetadata" ItemName="CompilerMetadata" />
        </GenerateTask>

        <ItemGroup>
            <Compile Include="@(GeneratorOutputs)" />
        </ItemGroup>
    </Target>
    
    <PropertyGroup>
        <CompileDependsOn>$(CompileDependsOn);GenerateTarget</CompileDependsOn>
    </PropertyGroup>

    <!-- This is a second SDK -->
    <UsingTask TaskName="CompileTask" AssemblyFile="path\to\CompilerAssembly.dll" PrecomputeMode="Graph" />

    <Target Name="CompileTarget" DependsOnTargets="$(CompileDependsOn)" Inputs="@(Compile)" Outputs="$(OutputDirectory)\$(CompileOutputName).dll">
        <CompileTask 
            Sources="@(Compile)" 
            SomeCompilerSetting="$(SomeCompilerSetting)"
            Metadata="@(CompilerMetadata)"
            Output="$(OutputDirectory)\$(CompileOutputName).dll" />
    </Target>
    
    <PropertyGroup>
        <BuildDependsOn>$(BuildDependsOn);CompileTarget</BuildDependsOn>
    </PropertyGroup>
</Project>
```

### Precompute Phase
First this project would be evaluated unchanged from what MSBuild does today.
Executing the targets in precompute is next.
As in today’s MSBuild, the default target for this project is `Build` which depends on `CompileTarget` which depends on `GenerateTarget` which depends on `GeneratePrepTarget`.

The first target to execute is `GeneratePrepTarget` which executes unchanged from non-precompute and manipulates items/properties immediately.
Notably this sets `GeneratorFlags` to include `/someflag` because `SomePrepareSetting` was true.
This is all done during precompute and nothing is deferred to the execution phase.

Next `GenerateTarget` executes and it processes its first `PropertyGroup`, setting the `GeneratorFlags` property.
When execution gets to the `GenerateTask`, the task's annotation `PrecomputeMode="Graph"` means it will not be invoked during precompute and will instead be stored into the graph as a step in a node.
The node also notes that the `GeneratorMetadata` output parameter of the `GenerateTask` will be added to `@(CompilerMetadata)`.
The engine notes that the `CompilerMetadata` item is now only known during the compute phase and cannot be expanded during the precompute phase.
It adds a special output that represents these resulting items so that other nodes can depend on that if they need the values of items or properties produced by this target.
Finally it will process the `ItemGroup` that adds `@(GeneratorOutputs)` to `@(Compile)`.
Because `@(GeneratorOutputs)` is knowable without running `GenerateTask`, this is done immediately and not put into the build graph.
The target's work not performed in the precompute phase becomes a node in the graph with inputs/outputs coming from the `Inputs` and `Outputs` attributes of the target and the steps being the task.
This results in something akin to the following being stored into the graph as a node (JSON is used for illustration):

```json
{
	"Name": "GenerateTarget",
    "Inputs": ["schema.def"],
    "Outputs": ["bin\\schema.src", "<GenerateTarget:ResultingItemsProperties>"],
	"Steps": [
        { 
            "Type": "ExecuteTask",
            "TaskActivationData": { "Task": "GenerateTask", "AssemblyFile": "path\\to\\GeneratorAssembly.dll" },
            "Parameters": { 
                "Sources": ["schema.def"],
                "OutputDirectory": "bin",
                "Flags": "/someflag /version:1"
            },
            "Outputs": [ { "Item": "CompilerMetadata", "Parameter": "GeneratorMetadata" } ]
        }
    ]
}
```

Next the `CompileTarget` executes.
Its only task is also `PrecomputeMode="Graph"` so it follows a similar pattern.
The one notable difference is that the `CompilerMetadata` value is produced by a prior target that is in the graph.
This means it cannot be expanded now and instead the build graph will capture/consume the value during the compute phase.
This results in an input being added that contains the values of the items or properties coming from the prior target and instructions on the appropriate steps to expand that data.
Again, the following is a rough representation of the resulting node:

```json
{
	"Name": "CompileTarget",
    "Inputs": ["code.src", "bin\\schema.src", "<GenerateTarget:ResultingItemsProperties>"],
    "Outputs": ["bin\\MyOutput.dll"],
	"Steps": [
        { 
            "Type": "ExecuteTask",
            "TaskActivationData": { "Task": "CompileTask", "AssemblyFile": "path\\to\\CompilerAssembly.dll" },
            "Parameters": { 
                "Sources": ["code.src", "bin\\schema.src"],
                "SomeCompilerSetting": "True",
                "Metadata": { "Source": "GenerateTarget", "Item": "CompilerMetadata" },
                "Output": "bin\\MyOutput.dll"
            }
        }
    ]
}
```

Finally, the `Build` target runs, which doesn't do anything so it doesn't create a node in the resulting graph.
This concludes the precompute phase.

### Compute Phase
This example has just two nodes: `CompileTarget` which depends on `GenerateTarget`.
For the first build the cache will have no data so both will get a cache miss and execute.
`GenerateTarget` is executed first and it produces its output files and also the value of `CompilerMetadata`.
A cache entry is added that uses the hash of the node's definition and inputs as the key and stores the outputs state, including the value of `CompilerMetadata`, as the result.
`CompileTarget` follows an almost identical process, but the value of `CompilerMetadata` is hashed as part of the cache key.

#### Impacts of Changes
Let's say the above project was built, populating a cache.
Here are some example changes that they could make after that and what would then happen during the next build using precompute mode.

**No-op project-file change**: if a project file was edited in a way that ends up emitting the same build graph (e.g. an XML comment change) then the precompute phase would need to execute but the compute phase would be up to date because its nodes and their inputs are unchanged and the outputs are still present and valid.  
**Changing `code.src`**: the precompute phase would not need to execute since it didn't depend on this, the `GenerateTarget` node would have the same definition and inputs so it would not execute, while the `CompileTarget` node would have a new input hash and would execute (possibly producing new output depending on the change).  
**Changing `SomeCompilerSetting`**: the precompute phase would execute because one of its inputs changed, the `GenerateTarget` would end up being the same and would not execute, while the `CompileTarget` would have a different definition hash and thus would execute even though its _file_ inputs are unchanged.  
**Changing `schema.def`**: the precompute phase would not need to execute since it didn't depend on this, the `GenerateTarget` would have a different input hash and thus would execute, the `CompileTarget` would execute if the outputs of `GenerateTarget` (`bin\\schema.src` or the value of `@(CompilerMetadata)`) changed.  

