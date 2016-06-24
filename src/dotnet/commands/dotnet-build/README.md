% DOTNET-BUILD(1)
% Microsoft Corporation dotnetclifeedback@microsoft.com
% April 2016

## NAME 
dotnet-build -- Builds a project and all of its dependencies 

## SYNOPSIS

`dotnet build [--output]  
    [--build-base-path] [--framework]  
    [--configuration]  [--runtime] [--version-suffix]
    [--build-profile]  [--no-incremental] [--no-dependencies]
    [<project>]`  

## DESCRIPTION

The `dotnet build` command builds multiple source file from a source project and its dependencies into a binary. 
The binary will be in Intermediate Language (IL) by default and will have a DLL extension. 
`dotnet build` will also drop a `\*.deps` file which outlines what the host needs to run the application.  

Building requires the existence of a lock file, which means that you have to run [`dotnet restore`](../dotnet-restore/README.md) prior to building your code.

Before any compilation begins, the build verb analyzes the project and its dependencies for incremental safety checks.
If all checks pass, then build proceeds with incremental compilation of the project and its dependencies; 
otherwise, it falls back to non-incremental compilation. Via a profile flag, users can choose to receive additional 
information on how they can improve their build times.

All projects in the dependency graph that need compilation must pass the following safety checks in order for the 
compilation process to be incremental:
- not use pre/post compile scripts
- not load compilation tools from PATH (for example, resgen, compilers)
- use only known compilers (csc, vbc, fsc)

In order to build an executable application, you need a special configuration section in your project.json file:

```json
{ 
    "compilerOptions": {
      "emitEntryPoint": true
    }
}
```

## OPTIONS

`-o`, `--output` [DIR]

Directory in which to place the built binaries. 

`-b`, `--build-base-path` [DIR]

Directory in which to place temporary outputs.

`-f`, `--framework` [FRAMEWORK]

Compiles for a specific framework. The framework needs to be defined in the project.json file.

`-c`, `--configuration` [Debug|Release]

Defines a configuration under which to build.  If omitted, it defaults to Debug.

`-r`, `--runtime` [RUNTIME_IDENTIFIER]

Target runtime to build for. 

--version-suffix [VERSION_SUFFIX]

Defines what `*` should be replaced with in the version field in the project.json file. The format follows NuGet's version guidelines. 

`--build-profile`

Prints out the incremental safety checks that users need to address in order for incremental compilation to be automatically turned on.

`--no-incremental`

Marks the build as unsafe for incremental build. This turns off incremental compilation and forces a clean rebuild of the project dependency graph.

`--no-dependencies`

Ignores project-to-project references and only builds the root project specified to build.
