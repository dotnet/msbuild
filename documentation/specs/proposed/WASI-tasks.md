# WASI tasks in MSBuild (WasiBuild)
We want to make it easier to work with the WebAssembly ecosystem in MSBuild.
MSBuild Tasks are the point where this makes sense. 

*...*

## Terminology and context
-  **WebAssembly (abbreviated Wasm)**
> is a binary instruction format for a stack-based virtual machine. Wasm is designed as a portable compilation target for programming languages, enabling deployment on the web for client and server applications. - [webassembly.org/](https://webassembly.org/)

- [**WASI**](https://wasi.dev/) : WebAssembly System Interface is a standard for APIs for software compiled to Wasm.
- [**Wasmtime**](https://wasmtime.dev) : WASI runtime implementation 

The WebAssembly standard defines an intermediate language for a WASM runtime that can be implemented in a browser or as a standalone program. 
We can compile programs to this intermediate language and run them on any platform with this interpreter. 
- Note that .NET programs usually still run as the runtime bundled with CIL of the program.

### Current state
We can use the Exec task in this manner to run an executable .wasm file (.NET example):

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
3. .proj
```xml
  <Target Name="RunWasmtime" AfterTargets="Build">
    <Exec Command="wasmtime path_to_compiled_rust_program.wasm" />
</Target>
```
4. dotnet build
- In principle it's possible to compile to .wasm with a few Exec tasks too.

We can make this more user friendly.

### Utility for MSBuild
- WASI tasks are sandboxed
- Easier interoperability outside of C#
- WASI executables are packaged with no outside dependencies 
- Users from other languages

## Goals for the WASI task feature
1. specify interface for writing tasks in other languages
2. running Wasm 
3. Rust demo task


### Steps (???)
1. figure out the best options for hosting a WASI sandbox from .NET (probably p/invoke into some Rust thing)
2. write a TaskHost that uses it
3. write a WASM interface spec to map to the task model
4. do the invocation stuff
5. demos

### Prototype features
- [ ] WasmExec class extending ToolTask taking a .wasm file as a parameter - just runs the file with wasmtime
- [ ] WasmTask class extending ToolTask taking a .wasm file as a parameter
    - [ ] Specification for what should this .wasm file export and how it will be ran
    - [ ] Rust example
    - [ ] Possibly needs a TaskHost TBD

### Advanced features
- [ ] RunWasmCode task which takes a .wasm file which exports a function and parameterized to which function to run
- [ ] Wasm code inside XML 
    - [ ] WasmCodeFactory 
- [ ] C# code using wasm exported functions inside xml
- [ ] Wasm-supported language code inside XML (possibly too ambitious/flaky) [open question](#open-questions-and-considerations)
    - would need to include the tooling for getting wasm out of the languages which is under heavy development 



## Design
### diagram

![diagram](wasi-diagram.svg)

### Choosing WASI runtime [(open questions)](#open-questions-and-considerations)
https://wasi.dev/ mentions several possible runtimes: Wasmtime, WAMR, WasmEdge, wazero, Wasmer, wasmi, and wasm3.
An important criterion is popularity/activity in development as the WASM standard is evolving and needs a lot of developers to implement it.
This leads to considering [Wasmtime](https://wasmtime.dev/) or [Wasmer](https://wasmer.io/).
Interaction with C# is especially important for us so we will use **Wasmtime** because the integration via a NuGet package is more up to date and there is more active development in tooling and other dotnet projects use it. 

### Testing
- **TBD**
- E2E tests - building projects in different languages most important

<!-- Integration tests for logging -->
<!-- mirror MSBuild\src\Build.UnitTests\BackEnd\TaskHost_Tests.cs --> 

## User Experience
API should be clear and the Rust task provide an example of how to implement it.
Then the user adds the task to their .proj file and it runs and logs as if it were a C# task.

## Development remarks (in-progress)

### TODO for this doc
- create in depth explanations for Wasm/WASI and how its concepts map to MSBuild concepts
- discuss with people who understand MSBuild internals, WASI and dotnet interaction, users of MSBuild

### Open questions and considerations 
- **implementing WASI api on our own like [wasm in vscode](https://github.com/microsoft/vscode-wasm)?**
    - security👍 we don't have control over external runtime
    - customizable👍
    - hard to maintain👎, wasi is constantly changing
    - lot of work 👎


- **Interacting with the tooling for creating .wasi files**
    - hard, unstable
    - separate project?
    - out of scope?
    - if yes - inclusion in msbuild? how to handle missing?

- **bundling wasm runtime with MSBuild?**
    - compatibility👍
    - ease of use 👍
    - size👎
    - maintenance👎
    - if no how to handle missing?

#### Related issues making other environments for running tasks:
https://github.com/dotnet/msbuild/issues/711
https://github.com/dotnet/msbuild/issues/4834
https://github.com/dotnet/msbuild/issues/7257

### Requirements gathering
What are the needs that WASI tasks would address?
WASI task host? security?


### Related projects

[wasmtime](https://wasmtime.dev/) - Wasm runtime

[wasm in vscode](https://github.com/microsoft/vscode-wasm)

[wasmtime-dotnet](https://github.com/bytecodealliance/wasmtime-dotnet)

[dotnet-wasi-sdk](https://github.com/dotnet/dotnet-wasi-sdk) - compile dotnet to Wasm - likely stale
- copy their properties as those would be similar

[wasm in dotnet runtime](https://github.com/dotnet/runtime/tree/main/src/mono/wasm)
https://github.com/dotnet/runtime/discussions/98538#discussioncomment-8499105
https://github.com/dotnet/runtime/issues/65895#issuecomment-1511265657

[componentize-dotnet](https://github.com/bytecodealliance/componentize-dotnet) nuget package to make Wasi bundle from c#, released a week ago

### Random
System.Runtime.InteropServices

windows/linux/mac all seem like they will have some specifics

threadsafety?

<!-- https://learn.microsoft.com/en-us/visualstudio/msbuild/configure-tasks?view=vs-2022 -->
<!-- - configuring tasks to run outside the env of the rest of the project, probably not relevant because wasi is too specific-->


named Conditions in xml to test if wasmtime is present?

[documentation/wiki/Nodes-Orchestration.md](documentation/wiki/Nodes-Orchestration.md)
