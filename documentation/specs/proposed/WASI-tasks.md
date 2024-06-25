# Wasm/WASI tasks in MSBuild (WasmBuild)
We want to make it easier to work with the WebAssembly ecosystem in MSBuild.
MSBuild Tasks are the point where this makes sense. 
Also it brings sandboxing possibilities.


## Stories for requirements
Currently tasks have unrestricted access to resources, Wasm/WASI runtimes provide a way to sandbox tasks (by default executables don't have access to any resources). This can be acheived by specifying Inputs and Outputs of these tasks and other resources they can access.

 We want to be able to run tasks written in other languages than C# in MSBuild. Those tasks will need a definition for how to write them. Invoking a Wasm runtime can easily run pre-compiled tasks. 
 (Advanced) Integrating compiling other languages to WASI would enable an easy workflow. 

## Terminology and context
-  **WebAssembly (abbreviated Wasm)**
> is a binary instruction format for a stack-based virtual machine. Wasm is designed as a portable compilation target for programming languages, enabling deployment on the web for client and server applications. - [webassembly.org/](https://webassembly.org/)

- [**WASI**](https://wasi.dev/) : WebAssembly System Interface is a standard for APIs for software compiled to Wasm to use system resouces outside of browsers.
- [**Wasmtime**](https://wasmtime.dev) : Wasm runtime implementation for desktops supporting WASI

The WebAssembly standard defines an language for a Wasm runtime that can be implemented in a browser or as a standalone program. 
We can compile programs to this language and run them on any platform with virtual machine. 
- Note that .NET programs usually still run as the runtime bundled with CIL of the program.

### Current state
We can use the Exec task in this manner to run an executable .wasm file (.NET example):
- note that this execution does not get any resources so it can't manipulate files

1. install [wasi-sdk](https://github.com/WebAssembly/wasi-sdk), [wasmtime](https://wasmtime.dev)
1. `dotnet add workflow wasi-experimental`
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
    <Exec Command="wasmtime run bin/$(Configuration)/$(TargetFramework)/wasi-wasm/AppBundle/$(AssemblyName).wasm" />
</Target>
</Project>
```
5. dotnet build


Rust example:
1. install wasmtime
2. compile Rust to .wasm (won't elaborate here, GPT can explain without problems)
3. .proj
```xml
  <Target Name="RunWasmtime" AfterTargets="Build">
    <Exec Command="wasmtime run path_to_compiled_rust_program.wasm" />
</Target>
```
4. dotnet build
- In principle it's possible to compile to .wasm with a few Exec tasks too.

We can make this more user friendly.

### Utility for MSBuild
- resources for Wasm tasks have to be managed explicitly which provides sandboxing if desired
- Easier interoperability outside of .NET
    - Task authoring in non-.NET languages
- Wasm tasks can be packaged with no outside dependencies 

## Goals for the Wasm task feature
1. specify Wasm/WASI interface for writing tasks in other languages and returning MSBuild information
2. Write an `ITaskFactory` that takes a `.wasm` file implementing that interface and runs it as an MSBuild task  
3. Rust demo task

### Prototype features
- [ ] WasmExec class extending ToolTask taking a .wasm file as a parameter - just runs the file with wasmtime
    -  [ ] parametrizing access to resources (will apply to all subsequent parts)
- [ ] WasmTaskFactory - creating tasks from .wasm files
    - [ ] Specification for what should this .wasm file export and how it will be ran
    - [ ] Taskhost
    - example usage:
```xml
<UsingTask TaskName="FancyWasiTask"
           AssemblyFile="path/to/your/thing.dll"
           TaskFactory="WasiTaskFactory">
  <Task>
    <WasiModule>compiled_task_implementation.wasm</WasiModule>
  </Task>
</UsingTask>
```
- [ ] Rust example
- [ ] .NET example

### Advanced features
- [ ] integrating pipeline for creating Wasm/WASI tasks from code in other languages
    - [ ] investigate integrating tools compiling languages to Wasm/WASI
    - On task level
        - [ ] RustTaskFactory
        - exploring other languages (Go, C/C++, Zig)
    - On code in XML level (maybe out of scope)
        - [ ] RustCodeTaskFactory
        - exploring other languages
- [ ] Wasm code inside XML 
    - [ ] WasmCodeFactory 
- investigate running an arbitrary .NET task distributed as a dll in the WASI sandbox (👀 Mono runtime)


## Design
### diagram

![diagram](wasi-diagram.svg)
### Wasm/WASI interface
The .wasm task file has to export a function execute(), import extern function getMetadata from host, **TBD more details** 

### Task parameters
every resource has to be explicit, wasmtime is a sandbox by default
- *implicitly: Executable="path/to/executable.wasm" created by the factory*
- Inputs="list_of_input_files"
- Outputs="list_of_output_files"
- InheritEnv=default to false, 
- Environment="list_of_variables"
- StdIOE=default to true
- Directories="directories on host that can be accessed"
- Args="for the wasm program" 
- TmpDir="somethign like temporary working directory"
- HostFunctions="list of functions exported to wasm code"


### Testing
- **TBD**
- E2E tests - building projects in different languages most important

<!-- Integration tests for logging -->
<!-- mirror MSBuild\src\Build.UnitTests\BackEnd\TaskHost_Tests.cs --> 
### Other
The sandboxing of files will require changes to msbuild proper but the rest would preferrably be a NuGet package where community is more responsible for maintaining that the tools for using other languages are integrated well.

## User Experience
API should be clear and the Rust task provide an example of how to implement it.
Then the user adds the task to their .proj file and it runs and logs as if it were a C# task.

## Implementation details
### wasmtime-dotnet bindings and basic usage
```csharp
using var engine = new Engine();
using var module = Module.FromFile(engine, WasmFilePath);
using var linker = new Linker(engine);
linker.DefineWasi(); 
// add delegates to linker that the wasm file can use
using var store = new Store(engine);
var wasiConfigBuilder = new WasiConfiguration(); 
// enable resources: Environment, InheritEnvironment, PreopenedDirectory(ies), Standard(I/O/E), 
store.SetWasiConfiguration(wasiConfigBuilder);
Instance instance = linker.Instantiate(store, module);
Action fn = instance.GetAction("execute");
fn.Invoke();
```


## Development remarks (in-progress)

### TODO for this doc
- create in depth explanations for Wasm/WASI and how its concepts map to MSBuild concepts
- discuss with people who understand MSBuild internals, WASI and dotnet interaction, users of MSBuild
- elaborate how to give resources using wasmtime-dotnet


### Tentatively resolved considerations 
- **Inside MSBuild or as a NuGet package?
    - the feature seems largely independent
    - *-> separate repo https://github.com/JanProvaznik/MSBuild-Wasm, some features might need coordination - feature branch `dev/wasi-tasks`*

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
    - *-> in scope but ambitious, nuget package can have some install scripts doing that*

- **start with windows or UNIX?**
    - *-> most different is the investigation about how to bundle tooling for other languages*

- **renaming this feature from WASI-... to Wasm-...**
    - file extensions are called .wasm 👍
    - WASI is a standard building on Wasm 👍
    - the compilation target is called wasm-wasi 👍👎
    - *-> mostly use Wasm-tasks unless Wasm/WASI is more appropriate for that situation*

### Open questions
- **Is the strategy of defining an entrypoint function and invoking it correct?**
    - would using component model help us a lot? [WebAssembly interface type](https://github.com/WebAssembly/component-model/blob/main/design/mvp/WIT.md), does that require work on wasmtime-dotnet to get the bindings first?

- **What changes are needed in MSBuild repo?**
    - TaskHost is `internal` 
    - How does sandboxing in MSBuild work and how to interact with it?

- **Wasm/WASI Technical details**
    - how to pass strings/nontrivial objects C# -> Wasm, Wasm -> C#
    - how to use functions with return values?
    - calling imported functions (from C# host) in Wasm?
    - what happens when a host exports a function and the wasm does not expect it?
    - preventing users shooting themselves in the foot with Wasm errors

### Related projects

[wasmtime](https://wasmtime.dev/) - Wasm runtime supporting the WASI standard written in Rust by *Bytecode Alliance* - a nonprofit, Microsoft is a member

[wasmtime-dotnet](https://github.com/bytecodealliance/wasmtime-dotnet) - Bindings for wasmtime API in C#

[componentize-dotnet](https://github.com/bytecodealliance/componentize-dotnet) NuGet package to easily make Wasi bundle from a C#.NET project, released short time ago, created by Microsoft people

[dotnet-wasi-sdk](https://github.com/dotnet/dotnet-wasi-sdk) 
- compile dotnet to Wasm
- moved to sdk and runtime repos `dotnet workload install wasi-experimental`
    - Discussions: [1](https://github.com/dotnet/runtime/tree/main/src/mono/wasm) [2](https://github.com/dotnet/runtime/discussions/98538#discussioncomment-8499105) [3](https://github.com/dotnet/runtime/issues/65895#issuecomment-1511265657)
- copy their properties as those would be similar

MSBuild issues for making other environments for running tasks: [711](https://github.com/dotnet/msbuild/issues/711) [4834](https://github.com/dotnet/msbuild/issues/4834) [7257](https://github.com/dotnet/msbuild/issues/7257)

### Random

<!-- https://learn.microsoft.com/en-us/visualstudio/msbuild/configure-tasks?view=vs-2022 -->
<!-- - configuring tasks to run outside the env of the rest of the project, probably not relevant because wasi is too specific-->

- [documentation/wiki/Nodes-Orchestration.md](documentation/wiki/Nodes-Orchestration.md)

- wasmtime-dotnet needs to be signed to have a StrongName and put in a private feed if we'd like to integrate it to MSBuild proper eventually.