dotnet-build
===========

**NAME** 
dotnet-build -- Orchestrates the compilation of a project and all its dependencies.

**SYNOPSIS**
dotnet build[options]

**DESCRIPTION**

The build verb orchestrates the compilation of a project: it gathers the dependencies of a project and decides which to compile. 

Users should invoke the Build verb when they want the entire dependency graph compiled, and Compile when they want only a specific project compiled.

Before any compilation begins, the build verb analyzes the project and its dependencies for incremental safety checks. If all checks clear out, then build proceeds with incremental compilation of the project and its dependencies. Otherwise it falls back to non-incremental compilation. Via a profile flag users can choose to receive additional information on how they can improve their build times.

All the projects in the dependency graph that need compilation must pass the following safety checks in order for the compilation process to be incremental:
- not use pre / post compile scripts
- not load compilation tools from PATH (e.g., resgen, compilers)
- use only known compilers (csc, vbc, fsc)

Please read the [documentation](https://github.com/dotnet/cli/blob/master/src/Microsoft.DotNet.Tools.Compiler/README.md) on Compile for details on compilation and project structure: 

**Options**

Build inherits all the [Compile command line parameters](https://github.com/dotnet/cli/blob/master/src/Microsoft.DotNet.Tools.Compiler/README.md).

In addition Compile's parameters, Build adds the following flag:

--build-profile
Prints out the incremental safety checks that users need to address in order for incremental compilation to be automatically turned on.

--no-incremental
Marks the build as unsafe for incrementality. This turns off incremental compilation and forces a clean rebuild of the project dependency graph.

--no-dependencies
Ignore project to project references and only build the root project specified to build.
