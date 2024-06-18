# WASI tasks in MSBuild (WasiBuild)
Abstract

## Terminology and context
-  **WebAssembly (abbreviated Wasm)**
> is a binary instruction format for a stack-based virtual machine. Wasm is designed as a portable compilation target for programming languages, enabling deployment on the web for client and server applications. - [webassembly.org/](https://webassembly.org/)

- [**WASI**](https://wasi.dev/) : WebAssembly System Interface is a standard for APIs for software compiled to Wasm.
- [**Wasmtime**](https://wasmtime.dev) : WASI runtime implementation 

The WebAssembly standard defines an intermediate language for a WASM runtime that can be implemented in a browser or as a standalone program. 
We can compile programs to this intermediate language and run them on any platform with this interpreter. 
- Note that .NET usually still runs as the runtime which JITs the Common Intermediate Language (CIL) as usual.

### Current state
We can use the Exec task in this manner (.NET example):

1. install [wasi-sdk](https://github.com/WebAssembly/wasi-sdk), [wasmtime](https://wasmtime.dev)
1. `dotnet add workflow wasi-experimental`
2. `dotnet new wasi-console`
3. add `<WasmSingleFileBundle>true</WasmSingleFileBundle>` to .csproj,
 for example: 
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <RuntimeIdentifier>wasi-wasm</RuntimeIdentifier>
    <OutputType>Exe</OutputType>
    <WasmSingleFileBundle>true</WasmSingleFileBundle>
  </PropertyGroup>

  <Target Name="RunWasmtime" AfterTargets="Build">
    <Exec Command="wasmtime bin/$(Configuration)/$(TargetFramework)/wasi-wasm/AppBundle/$(AssemblyName).wasm" />
</Target>
</Project>
```
5. dotnet build


Rust example:
1. install wasmtime
2. compile Rust to .wasm (won't elaborate here, GPT can explain without problems)
3. .csproj
```xml
  <Target Name="RunWasmtime" AfterTargets="Build">
    <Exec Command="wasmtime path_to_compiled_rust_program.wasm" />
</Target>
```
4. dotnet build
- In principle it's possible to compile to .wasm with a few Exec tasks too.

### Utility
- WASI tasks are sandboxed
- Easier interoperability outside of C#
- WASI executables are packaged with no outside dependencies 

## Goals
TBD


### Steps
1. figure out the best options for hosting a WASI sandbox from .NET (probably p/invoke into some Rust thing)
2. write a TaskHost that uses it
3. write a WASM interface spec to map to the task model
4. do the invocation stuff
5. demos

### Prototype features
TBD

[ ] WASM task 

### Advanced features
TBD

[ ] Wasm code inside XML 
<!-- [] Wasm-supported language code inside XML -->

## Design
### Choosing WASI runtime
https://wasi.dev/ mentions several possible runtimes: Wasmtime, WAMR, WasmEdge, wazero, Wasmer, wasmi, and wasm3.
An important criterion is popularity/activity in development as the WASM standard is new and needs a lot of developers to implement it.
This leads to considering [Wasmtime](https://wasmtime.dev/) or [Wasmer](https://wasmer.io/).
Interaction with C# is especially important for us so we will use **Wasmtime** because the integration via a NuGet package is more up to date and there is more active development in tooling. 

### Testing
E2E tests - building projects in different languages most important
Integration tests for logging
mirror MSBuild\src\Build.UnitTests\BackEnd\TaskHost_Tests.cs

## User Experience
TBD

## Development remarks (in-progress)

#### Related issues making other environments for running tasks:
https://github.com/dotnet/msbuild/issues/711
https://github.com/dotnet/msbuild/issues/4834
https://github.com/dotnet/msbuild/issues/7257

### Requirements gathering
What are the needs that WASI tasks would address?
WASI task host?


### Related projects

[wasmtime](https://wasmtime.dev/) - Wasm runtime

[wasm in vscode](https://github.com/microsoft/vscode-wasm)

[wasmtime-dotnet](https://github.com/bytecodealliance/wasmtime-dotnet)

[dotnet-wasi-sdk](https://github.com/dotnet/dotnet-wasi-sdk) - compile dotnet to Wasm - likely stale


[wasm in dotnet runtime](https://github.com/dotnet/runtime/tree/main/src/mono/wasm)
https://github.com/dotnet/runtime/discussions/98538#discussioncomment-8499105

[componentize-dotnet](https://github.com/bytecodealliance/componentize-dotnet) nuget package to make Wasi bundle from c#, active recently

### Random
What to put in scope? 
supporting WASM-compatible-lang -> WASM seems super hard - every workflow is changing constantly
- maybe do it somehow extensibly so they can be plug in libraries?

UsingTask runtime: CLR2, CLR4, CurrentRuntime, \*, **WASI** 🤔

System.Runtime.InteropServices

windows/linux/mac all seem like they will have some specifics

threadsafety?

https://learn.microsoft.com/en-us/visualstudio/msbuild/configure-tasks?view=vs-2022
- configuring tasks to run outside the env of the rest of the project


would implementing this mean that msbuild is bundled with a wasi runtime (wasmtime)?

[documentation/wiki/Nodes-Orchestration.md](documentation/wiki/Nodes-Orchestration.md)

https://github.com/dotnet/msbuild/blob/main/documentation/specs/task-isolation-and-dependencies.md
- Wasi tasks might help?



named Conditions in xml to test if wasmtime is present



## TODO

make a diagram

create explanations how Wasm/WASI concepts map to MSBuild concepts