# MSBuild Precompute Mode

> **Note:** Precompute mode is currently in development and experimental. It may be shipped/implemented in future releases or it may be scrapped.

Precompute mode separates the creation of a build graph, called the precompute phase, from the execution of that build graph, called the compute phase, and uses that foundation to improve the reliability and performance of the build.

This document is meant to be an introduction to precompute mode while the full specification and detailed design are still under development and will be provided in future updates.

## Overview
To create the build graph, precompute mode evaluates project files and visits targets just like MSBuild would do before precompute mode, but new syntax makes the target contents either a) execute immediately or b) skip execution and be encoded into the build graph.
By default each target (specifically each batch of a target) becomes a separate node in the build graph with input/output files defined by the target.
Targets can also be merged together into a single node to optimize the build graph.
Additionally, there are also more explicit ways to declare inputs and outputs beyond the default file-based attributes on targets.
All inputs and outputs must be completely declared.
If a target attempts to read or write undeclared data during execution it fails.
The creation of the graph cannot depend on the execution of any part of the build graph and nodes in the build graph must be acyclic and correctly describe dependencies.

## Benefits
With this design precompute mode can improve reliability and performance.

The precompute phase:
 - can be re-used from the prior build if none of its inputs changed avoiding the costs around evaluation and various targets that are part of the precompute phase
   - if anything that was used to do precompute changes then it executes (e.g. project files, SDKs, etc)
   - if a code file changes content then it does not execute since it does not affect the precompute phase
 - is more deterministic:
   - it cannot be affected by the state of the outputs of the build since it is prevented from depending on them
   - targets are not skipped or partially executed based on what needs rebuilding which can cause subtle different behavior

The compute phase can be improved to:
 - perform content-based hashing of all inputs/outputs (note: inputs include the task assembly and its dependencies)
 - hash the definition of the node to safely re-use or re-execute based on if parameters/what tasks/etc have changed
 - store outputs into a cache based on the hash of the inputs and node definition
 - safely distribute the build across many machines to speed up large builds
 - sandbox targets to enforce the inputs/outputs are correct
 - delete prior outputs to ensure a clean execution state
 - clean extraneous content that is no longer covered by the graph
 - sandbox targets to determine which inputs were used and which were not to ignore input hashes that were irrelevant
 - allow targets (including batches of a target) to execute concurrently if there is no file or compute-phase value dependency between them

## Simple Example
The following is a self-contained example that should demonstrate the basic concept of precompute mode.

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
            <Output Parameter="GeneratorMetadata" ItemName="CompilerMetadata" />
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
First this project would be evaluated largely unchanged from what MSBuild does today.
Note there are some exceptions which result in changed behavior but are not present in this example.
Next the default target for this project is `Build` which depends on `CompileTarget` which depends on `GenerateTarget` which depends on `GeneratePrepTarget`.

The first target to execute is `GeneratePrepTarget` which executes unchanged from non-precompute and manipulates items/properties immediately.
Notably this sets `GeneratorFlags` to include `/someflag` because `SomePrepareSetting` was true.

Next `GenerateTarget` executes and it processes its first `PropertyGroup`, setting the `GeneratorFlags` property.
Then when it gets to the `GenerateTask`, which has been `PrecomputeMode="Graph"` it will not execute during precompute and will be stored into the graph.
Outputs and any other items/properties that cannot be determined during the precompute phase will be processed and captured during the compute phase and any subsequent readers will automatically take a dependency on this node and be able to consume the resulting value (including from cache during cache hits).
Any parameters and conditions that can be evaluated during the precompute phase will be expanded and the resulting data will be stored into the graph.
Finally it will process the `ItemGroup` that adds `@(GeneratorOutputs)` to `@(Compile)`.
The entire target becomes a node in the graph with inputs/outputs coming from the `Inputs` and `Outputs` attributes of the target and the steps being the task.
This results in something akin to the following being stored into the graph as a node (JSON is used just for illustration):

```json
{
	"Name": "GenerateTarget",
    "Inputs": ["schema.def"],
    "Outputs": ["bin\\schema.src"],
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
Again, the following is a rough representation of the resulting node:

```json
{
	"Name": "CompileTarget",
    "Inputs": ["code.src", "bin\\schema.src"],
    "Outputs": ["bin\\MyOutput.dll"],
	"Steps": [
        { 
            "Type": "ExecuteTask",
            "TaskActivationData": { "Task": "CompileTask", "AssemblyFile": "path\\to\\CompileAssembly.dll" },
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

Finally the `Build` target runs which doesn't do anything so it doesn't create a node in the resulting graph.
This concludes the precompute phase.

### Compute Phase
The build graph constructed is executed based on the inputs/outputs and compute-phase item/property dependencies between nodes (i.e. `DependsOnTargets` does not play a role).
This example has just two nodes: `GenerateTarget` and `CompileTarget` as shown above and thus `CompileTarget` depends on `GenerateTarget`.

#### Impacts of Changes
Let's say the above project was built unmodified.
Here are some example changes that they could make after that and what would then happen during the next build using precompute mode.

**No-op project-file change**: if a project file was edited in a way ends up emitting the same build graph (e.g. a whitespace change) then the precompute phase would need to execute but the nodes would not execute provided their definitions and inputs are unchanged and the outputs are still present and valid.  
**Changing `code.src`**: the precompute phase would not need to execute since it didn't depend on this, the `GenerateTarget` would have the same definition and inputs so it would not execute, while the `CompileTarget` would have a new input hash and would execute (possibly producing new output depending on the change).  
**Changing `SomeCompilerSetting`**: the precompute phase would execute to process the impact of this, the `GenerateTarget` would end up being the same and would not execute, while the `CompileTarget` would have a different definition hash and thus would execute.  
**Changing `schema.def`**: the precompute phase would not need to execute since it didn't depend on this, the `GenerateTarget` would have a different input hash and thus would execute, the `CompileTarget` would execute depending on whether or not the generated `bin\\schema.src` or the value of `@(CompilerMetadata)` changed.  

