% DOTNET-BUILD(1)
% Microsoft Corporation dotnetclifeedback@microsoft.com
% April 2016

## NAME 
dotnet-build -- builds a project and all of its' dependencies 

## SYNOPSIS

dotnet-build [--output]  
    [--build-base-path] [--framework]  
    [--configuration]  [--runtime] [--version-suffix]
    [--build-profile]  [--no-incremental] [--no-dependencies]
    [< project >]  

## DESCRIPTION

`dotnet-build` builds multiple source file from a source project and its dependencies into a binary. 
The binary will be in Intermmidiate Language (IL) by default and will have a DLL extension. 
`dotnet-build` will also drop a \*.deps file which outlines what the runner needs to run the application.  

Building requires an existence of a lock file which means that a `dotnet-restore` call needs to happen 
previous to building.

Before any compilation begins, the build verb analyzes the project and its dependencies for incremental safety checks. 
If all checks clear out, then build proceeds with incremental compilation of the project and its dependencies; 
otherwise it falls back to non-incremental compilation. Via a profile flag, users can choose to receive additional 
information on how they can improve their build times.

All the projects in the dependency graph that need compilation must pass the following safety checks in order for the 
compilation process to be incremental:
- not use pre / post compile scripts
- not load compilation tools from PATH (e.g., resgen, compilers)
- use only known compilers (csc, vbc, fsc)

In order to build an executable application (console application), you need a special configuration section in project.json:

```json
{ 
    "compilerOptions": {
      "emitEntryPoint": true
    }
}
```

Class libraries do not need this special piece of configuration. 

## OPTIONS

`-o`, `--output` [DIR]

Directory in which to place the built binaries. 

`-b`, `--build-base-path` [DIR]

Directory in which to place temporary outputs

`-f`, `--framework` [FRAMEWORK]

Compile for a specific framework. The framework needs to be defined in the project.json file.

`-c`, `--configuration` [CONFIGURATION]

Configuration under which to build. If omitted defaults to "Debug". Possible configuration options are:

    * Debug
    * Release 

`-r`, `--runtime` [RUNTIME_IDENTIFIER]

Target runtime to build for. 

--version-suffix [VERSION_SUFFIX]

Defines what `*` should be replaced with in the version field in project.json.

`--build-profile`

Prints out the incremental safety checks that users need to address in order for incremental compilation to be automatically turned on.

`--no-incremental`

Marks the build as unsafe for incremental build. This turns off incremental compilation and forces a clean rebuild of the project dependency graph.

`--no-dependencies`

Ignore project-to-project references and only build the root project specified to build.
