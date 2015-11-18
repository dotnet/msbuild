dotnet-compile
===========

**NAME** 
dotnet-compile -- Compiles source files to a binary format and saves to a target file.

**SYNOPSIS**
dotnet compile [options]

**DESCRIPTION**
The compile command compiles source files to a binary file, either IL byte code or native machine code, depending on the options provided. The default option is compilation to IL byte code, but may change in the future.

The default IL [--il] output is a PE32 exe [exe], with the default extension of ".exe" on all OSes. The exe must include a public static void or public static int main entry point, or it is an error. The dll [dll] output option has the default extension of ".dll".

The IL exe output type needs a runtime host to execute. The IL exe output type also copies a host to the output directory. The host is renamed to the name of the exe. For example, if the file is intended to be "foo" (`-o foo`), then the host will be called foo, with the appropriate default native file extension fo the OS (see the native file extensions, below). The PE32 exe will be called "[filename]"-app.exe". In this case, it would be called "foo-app.exe". The executables also require a special configuration section in project.json:

```json
{ 
    "compilerOptions": {
      "emitEntryPoints": true
    }
}
```

The default native [--native] output is a native exe that conforms to the architecture of the underlying operating system (i.e. running on 64-bit OS will produce a native 64-bit exe). This can be overriden via the --arch switch and specifying the wanted architecture. The executable has a default extension of "" on Linux and OS X and ".exe" on Windows. The source must include a static void main entry point, or it is an error, unless otherwise specified in the project.json. The dynamic library [dylib] output option has the default extension of ".so" on Linux/UNIX, ".dynlib" on OS X and ".dll" on Windows. The static library [staticlib] option has the default extension of ".a" on Linux, UNIX and OS X and ".lib" on Windows.

This command relies on the following artifacts: source files, project.json project file, project.lock.json temporary file and restored NuGet dependencies. 

The project.json file represents and describes the project. It can contain several setting, which are described in the [Build](https://docs.asp.net/en/latest/dnx/projects.html#building). The most important information in the project.json file are the root (not transitive) NuGet dependencies and the files to be compiled. By default, this is a wildcard -- "*.cs". It supports both inclusion and exclusion semantics.

The project.lock.json file is expanded form of the project.json file. It includes the transitive closure of the project, per framework. It is produced by a NuGet client, typically by using the `dotnet restore` command. The project.lock.json file can be used by tools to safely determine the closure of dependencies, without having to manually calculate them. This file is only intended for tools, is temporary and should not be checked into source control. It should be present in .gitignore files.

It is important to know that a project.lock.json is invalid given that a project.json has been changed. The project.lock.json has enough information to determine this state given a project.json. The compile command validates this state and will error if the project.lock.json is invalid.

The compile command relies on NuGet dependencies for compilation, as references. These are expected to be found in the user-local NuGet cache (typically location here). It is an error state if a given NuGet package is not found.

Output files, are written to the child `bin` folder, which will be created if it doesn't exist. Files will be overwritten as needed. The temporary files that are created during compilation are placed in the child `obj` folder. 

**Options**

-n, --native [exe | dynlib | lib]
Compiles source to native machine code, for the local machine. The default is a native executable. The default exe extension is no extension and ".exe" on Windows. The default dynlib extension is ".a", ".dynlib" on OS X and ".dll" on Windows.

--il [exe | dll]
Compiles source to IL byte code, which is (typically) portable across machine types. The default output is a PE32 exe, with the default extension of ".exe" on all OSes. The exe must include a static main entry point, or it is an error. The DLL output option has the default extension of ".dll".

-o, --output filename
Specifies the filename to be used. It is an error not to specify an output filename. If no extension is provided, the default one is provided for the output type.

-v, --verbose
Prints verbose logging information, to follow the flow of execution of the command.
