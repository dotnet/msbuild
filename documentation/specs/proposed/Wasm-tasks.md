# Wasm/WASI tasks in MSBuild
Exploration of using Wasm/WASI to create sandboxed [Tasks in MSBuild](https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-tasks) using non-dotnet Wasm/WASI compatible language.

## Stories 
Currently MSBuild tasks have unrestricted access to resources (filesystem, network, environment variables), Wasm/WASI runtimes provide a way to sandbox tasks (all access to resources has to be specified). Sandboxing is useful from a security perspective if someone wanted to run a task from an untrusted source without decompiling and analyzing it.

Today a MSBuild task = .NET class. We want to enable users to write a task in another language. This feature includes designing how tasks will communicate with MSBuild if they're running out of the .NET runtime. Ecosystems that support the Wasm/WASI development features we need are Rust and C/C++.

## Terminology and context
-  **WebAssembly (abbreviated Wasm)**
> is a binary instruction format for a stack-based virtual machine. Wasm is designed as a portable compilation target for programming languages, enabling deployment on the web for client and server applications. - [webassembly.org/](https://webassembly.org/)

- [**WASI**](https://wasi.dev/) : WebAssembly System Interface is a standard for APIs for software compiled to Wasm to use system resouces outside of browsers.
    - WASIp1 filesystem, environment variables, stdIO, programs are "Modules"
    - WASIp2 rich interface data types, networking, programs are "Components"
- [**Wasmtime**](https://wasmtime.dev) : Wasm runtime implementation for desktops supporting WASI
- **Wasm Module** a compiled Wasm program that exposes functions to the host and expects imports functions from the host

### Diagram of a Wasm execution from a host
```mermaid 
flowchart TD
    a[guest language] -->|compile with wasi-sdk| K[Wasm Module]

    A[Engine]  --> E[Linker]
    
    E -->|Define host functions & WASI| H[Instance]

    H <---> Mem[Shared Memory]
    
    K[Module] -->|Piece of functionality| H
    
    L[Store] -->|Config for runtime| H
    
    H -->|Invoke module functions| M[Execution]

    subgraph " "
    A
    K
    L
    end

    subgraph "Single execution"
    H
    E
    M
    Mem
    end

```

### Interacting with Wasm/WASI in MSBuild without Wasm/WASI Tasks
In a build, we can use the [`Exec` task](https://learn.microsoft.com/en-us/visualstudio/msbuild/exec-task) with Wasmtime and an executable .wasm file, but this execution would not have any MSBuild capabilities such as logging and passing of file parameters.

#### .NET example
1. install [wasi-sdk](https://github.com/WebAssembly/wasi-sdk), [wasmtime](https://wasmtime.dev)
1. `dotnet workload install wasi-experimental`
2. `dotnet new wasiconsole`
3. add `<WasmSingleFileBundle>true</WasmSingleFileBundle>` to .csproj,
 this example runs the compiled program after building: 
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <RsiuntimeIdentifier>wasi-wasm</RuntimeIdentifier>
    <OutputType>Exe</OutputType>
    <WasmSingleFileBundle>true</WasmSingleFileBundle>
  </PropertyGroup>

  <Target Name="RunWasmtime" AfterTargets="Build">
    <Exec Command="wasmtime run bin/$(Configuration)/$(TargetFramework)/wasi-wasm/AppBundle/$(AssemblyName).wasm --additional-parameters-for-wasmtime" />
</Target>
</Project>
```
5. `dotnet build`


#### Rust example:
1. install [wasi-sdk](https://github.com/WebAssembly/wasi-sdk), [wasmtime](https://wasmtime.dev), [cargo](https://doc.rust-lang.org/cargo/getting-started/installation.html)
3. write your Rust program
3. `project.csproj`
```xml
  <Target Name="CompileAndRun" BeforeTargets="Build">
    <Exec Command="cargo build --target wasm32-wasi --release">
    <Exec Command="wasmtime run path_to_compiled_rust_program.wasm" />
</Target>
```
4. `dotnet build`

This is quite cumbersome and does not provide a way to pass parameters to the "task" or get outputs from it.

## Goals for the Wasm tasks feature
1. specify how a Wasm/WASI task should communicate with MSBuild, and what it should contain to be recognized as a task
2. Write an `ITaskFactory` and a supporting `ITask` classes that when passed a `.wasm` file implementing required functions runs it as an MSBuild task
3. Demos/examples

### Prototype features
Prototypes are implemented in [https://github.com/JanProvaznik/MSBuildWasm](https://github.com/JanProvaznik/MSBuildWasm)
- [ ] WasmExec class taking a .wasm file as a parameter - just runs the file with Wasmtime 
    - nudges the user to parametrize access to resources
- [ ] WasmTask - creating tasks from .wasm files
    - [x] Specification for what should this .wasm file export and how it will be ran
    - [ ] ITaskFactory that let's msbuild use task parameters defined inside the .wasm module
- [ ] Rust example
#### User Experience
1. The user Writes a task in Rust based on the template.
2. The user adds the task to their .proj file and it runs and logs as if it were a C# task. 
```xml
<UsingTask TaskName="FancyWasmTask"
           AssemblyFile="path/MSBuildWasm.dll"
           TaskFactory="WasmTaskFactory">
  <Task>
    <WasmModule>compiled_task_implementation.wasm</WasmModule>
  </Task>
</UsingTask>

<Target Name="name">
<FancyWasmTask Param="..." Param2="asdf">
<Output .../>
</FancyWasiTask>
</Target>
```

### Advanced features
- [ ] ~~.NET example~~ (WASIp1 will not be supported in .NET)
- [ ] integrating pipeline for creating Wasm/WASI tasks from code in Rust 
    - [ ] investigate integrating tools compiling languages to Wasm/WASI
    - On task level
        - [ ] RustTaskFactory
        - exploring other languages (Go, C/C++, Zig)
- [x] investigate running an arbitrary .NET task distributed as a dll in the WASI sandbox (👀 Mono runtime)
    - Due to implementing WasmTasks with WASIp1, it will not compatible with .NET runtime effort to support WASIp2


## Design
### diagram

```mermaid
flowchart TD
    A[MSBuild] -->|Evaluation| B[WasmTaskFactory]
    A -->|Target execution| C[TaskExecutionHost]
    C -->|instantiate and\n set parameters from XML| D[WasmTask]
    H[Rust/C#/Go] -->|"compile using wasi-sdk"| G
    D -->|gather output \nfor use in other tasks| C 
    D -->|execute| E[wasmtime-dotnet]
    E <--> F[Wasmtime]

    B -->|Create Type for a specific WasmTask| D
    B -->|read what the task expects as parameters| E
    B -->|save path to task parameters| G[.wasm module]
    E -->|read output from task stdout| D
    %%B, C, D%%
    style B fill:#ffff00
    style C fill:#ffff00
    style D fill:#ffff00
```
C# classes are yellow.


### Wasm/WASI communication with MSBuild
Without WASIp2 WIT (not implemented in wasmtime-dotnet), the only data type that an be a Wasm function parameter and output is a number. Tasks have parameters which are of the following types: string, bool, [ITaskItem](https://github.com/dotnet/msbuild/blob/main/src/Framework/ITaskItem.cs) (basically a string dict), and arrays of these types.

The .wasm module has to import functions from "module" msbuild-log: LogError(int str_ptr,int str_len), LogWarning(int str_ptr,int str_len), LogMessage(int MessageImportance,int str_ptr,int str_len). 
The .wasm task file has to export functions void GetTaskInfo(), int Execute(). Where the return type is 0 for success and 1 for failure.

### Task parameters 
What parameters the task has is obtained by calling GetTaskInfo() in the Task wasm module. When initializing the task with the `WasmTaskFactory` we use reflection to create a corresponding C# type with corresponding properties.
Task parameter values are passed into the wasm module as a JSON string in stdin.

For future reference we describe the proposed interface [in the WASIp2 WIT format](./wasmtask.wit) once it is supported in wasmtime-dotnet as a model for rewrite to WASIp2. This would remove the need to use JSON strings for passing parameters and logs could be passed using strings rather than pointers.

Every resource available to the Wasm/WASI runtime has to be explicit, Wasmtime is a sandbox by default, and WASIp1 via wasmtime-dotnet enables: preopening directories, environment variables, stdIO, args (if ran as a standalone program), 
Parameters that specify execution environment for the task can be specified in the XML: 
- InheritEnv=default to false, 
- Directories="directories on host that can be accessed"
After the task is run, Output parameters as a JSON are read from stdout of the Wasm execution, and parsed back into C# class properties so the rest of MSBuild can use them.

### Json format for parameter spec
They mirror MSBuild Task parameters as they need to be reflected to a C# class.
```jsonc
{
    "Properties": {
        "Param1": {
            "type": "string", 
            "required": true, // required attribute in C#
            "output": false // output attribute in C#
        },
        "Param2": {
            "type": "bool",
            "required": false,
            "output": false
        },
        "Param3": {
            "type": "ITaskItem", 
            "required": false,
            "output": false
        },
        "Param4": {
            "type": "ITaskItem[]",
            "required": false,
            "output": true 
        }
    }
}

```
### Json format for parameter values
```jsonc
{
    "Properties" : {
        "Param1": "hello",
        "Param2": true,
        "Param3": {
            "ItemSpec": "C:\\real\\path\\file.txt",
            "WasmPath" : "file.txt", // guest runtime path
            "More dotnet metadata": "..."
            } 
    }
}
```

### Json format for task output
Only parameters with the output attribute set to true are recognized from the output in the MSBuild task.
```jsonc
{
        "Param4": [
            {
                "WasmPath" : "also/can/be/dir"
            },
            {
                "WasmPath" : "dir2"
            }
        ]
}
```


### Testing
#### Unit tests
- [ ] setting parameters in the task
- [ ] parsing outputs
- [ ] examples contain expected functions

#### E2E tests
- Using Wasm/WASI Tasks in a build
- [ ] Rust tasks
    - [ ] logging
    - [ ] accessing environment variables
    - [ ] passing parameters
    - [ ] accessing files


## Implementation details
### wasmtime-dotnet bindings and basic usage
```csharp
using var engine = new Engine();
using var module = Module.FromFile(engine, WasmFilePath);
using var linker = new Linker(engine);
linker.DefineWasi(); // linking WASI
linker.Define("namespace", "function", (Action)delegate { /* do something */ }); // Host function that can be called from Wasm
using var store = new Store(engine);
var wasiConfigBuilder = new WasiConfiguration(); // enable resources: InheritEnvironment, PreopenedDirectory, StdIO 
store.SetWasiConfiguration(wasiConfigBuilder);
Instance instance = linker.Instantiate(store, module);
Action fn = instance.GetAction("Execute");
fn.Invoke();
```


## Development remarks (in-progress)


### Architectural decision record
- **Inside MSBuild or as an external package?**
    - the feature seems largely independent
    - *-> separate repo https://github.com/JanProvaznik/MSBuild-Wasm, some features might need coordination - feature branch `dev/wasi-tasks`*
    - *-> actually the TaskExecutionHost is a very deep MSBuild thing and would need refactoring*

- **implementing WASI api on our own like [wasm in vscode](https://github.com/microsoft/vscode-wasm)?**
    - customizable👍
    - hard to maintain👎, wasi is changing
    - lot of work 👎
    - *-> resolved to use wasmtime*
    - Choosing Wasm/WASI runtime 
        - https://wasi.dev/ mentions several possible runtimes: Wasmtime, WAMR, WasmEdge, wazero, Wasmer, wasmi, and wasm3.
        - An important criterion is popularity/activity in development as the WASM standard is evolving and needs a lot of developers to implement it.
        - This leads to considering [Wasmtime](https://wasmtime.dev/) or [Wasmer](https://wasmer.io/).
        - Interaction with C# is especially important for us so we will use **Wasmtime** because the integration via a NuGet package is more up to date and there is more active development in tooling and other dotnet projects use it. [wasmtime-dotnet](https://github.com/bytecodealliance/wasmtime-dotnet) provides access to wasmtime API

- **bundling wasm runtime with MSBuild?**
    - compatibility👍
    - ease of use 👍
    - size👎
    - maintenance👎
    - *-> make a nuget package, no need to release under msbuild now, eventually could happen, lot of compat/licencing concerns. bytecodealliance is a consortium containing Microsoft*

- **Interacting with the tooling for creating .wasi files from other languages?**
    - hard, unstable
    - *-> in scope but ambitious, the package can check/download and install tooling (wasi-sdk, rust) in simple cases*

- **start with windows or UNIX?**
    - *-> most different is the investigation about how to bundle tooling for other languages*

- **renaming this feature from WASI-... to Wasm-...**
    - file extensions are called .wasm 👍
    - WASI is a standard building on Wasm 👍
    - the compilation target is called wasm-wasi 👍👎
    - *-> use Wasm/WASI, the repo is called [MSBuildWasm](https://github.com/JanProvaznik/MSBuildWasm) for brevity*

- **communication between host and a wasm module**
    - shared memory, both host and wasm can access it; callbacks where to read from it, environment vars, stdIO 
    - eventually with Wasm/WASI component model better data-structures  
    - component model would help us a lot with passing data it has support for complex types [WebAssembly interface type](https://github.com/WebAssembly/component-model/blob/main/design/mvp/WIT.md) 
        - but wasmtime-dotnet does not support it now and the implementation is nontrivial: https://github.com/bytecodealliance/wasmtime-dotnet/issues/324#issuecomment-2218889279
    - *-> use JSON strings with callbacks and stdIO for now, with parsing on both sides, WIT is not implemented in wasmtime-dotnet*
     
- **TaskExecutionHost?**
    - TaskExecutionHost is the class that usually runs instantiated tasks and uses reflection to give them property values, 
    - if we want this layer to handle setting up the environment for the task it has to be abstracted and the interface implemented by custom WasmTaskExecutionHost
    - Blocked by having to bring the feature to MSBuild repo and refactoring TaskBuilder and including wasmtime-dotnet
    - *-> keep it separate from MSBuild for now, it's OK that the base WasmTask class will handle setting up the Wasm/WASI environment*

### Related projects

[wasmtime](https://wasmtime.dev/) - Wasm runtime supporting the WASI standard written in Rust by *Bytecode Alliance* - a nonprofit, Microsoft is a member

[wasmtime-dotnet](https://github.com/bytecodealliance/wasmtime-dotnet) - Bindings for wasmtime API in C#

[componentize-dotnet](https://github.com/bytecodealliance/componentize-dotnet) NuGet package to easily make a Wasm/WASI component from a C#.NET project, released short time ago, created by people from Microsoft, right now we can't use it because components are a different system than modules and we can't switch because wasmtime-dotnet does not support it yet.

[dotnet-wasi-sdk](https://github.com/dotnet/dotnet-wasi-sdk) 
- compile dotnet to Wasm
- moved to sdk and runtime repos `dotnet workload install wasi-experimental`
    - Discussions: [1](https://github.com/dotnet/runtime/tree/main/src/mono/wasm) [2](https://github.com/dotnet/runtime/discussions/98538#discussioncomment-8499105) [3](https://github.com/dotnet/runtime/issues/65895#issuecomment-1511265657)
- developments that would enable using Wasm/WASI tasks written in .NET were added after the workload release but recently removed as .NET will focus only on WASIp2

MSBuild issues for making other environments for running tasks: [711](https://github.com/dotnet/msbuild/issues/711) [4834](https://github.com/dotnet/msbuild/issues/4834) [7257](https://github.com/dotnet/msbuild/issues/7257)

### Random

<!-- https://learn.microsoft.com/en-us/visualstudio/msbuild/configure-tasks?view=vs-2022 -->
<!-- - configuring tasks to run outside the env of the rest of the project, probably not relevant because wasi is too specific-->
- wasmtime-dotnet needs to be signed to have a StrongName and put in a private feed if we'd like to integrate it to MSBuild proper eventually https://github.com/bytecodealliance/wasmtime-dotnet/pull/320

- languages other than Rust are moving slowly and it's unclear if they'll implement WASIp1 at all or just like .NET will focus on WASIp2. Closest to support is Go but it's missing function exports. .NET 9 preview 6 possibly implements everything needed for Wasm/WASI tasks but the code was removed in preview 7 and won't come back.