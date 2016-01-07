% DOTNET-COMPILE(1)
% Zlatko Knezevic zlakne@microsoft.com
% January 2016

# NAME 
dotnet-compile -- Compiles source files for a single project to a binary format and saves to a target file.

# SYNOPSIS
dotnet compile [--native] [--output] 
    [--temp-output] [--framework] [--configuration] 
    [--output] [--arch] [--cpp] [-ilc-args] [--verbose]

# DESCRIPTION
The compile command compiles source files from a single project to a binary file, either intermmediate language (IL) byte code or native machine code, depending on the options provided. The default option is compilation to IL byte code, but may change in the future. Users who want to benefit from incremental builds and who want to compile both the project and its dependencies should use the dotnet-build(1) command.

The result of compilation is by default an executable file that can be ran. Output files, are written to the child `bin` folder, which will be created if it doesn't exist. Files will be overwritten as needed. The temporary files that are created during compilation are placed in the child `obj` folder. 
 
The executables also require a special configuration section in project.json:

```json
{ 
    "compilerOptions": {
      "emitEntryPoints": true
    }
}
```

The default native [--native] output is a native exe that conforms to the architecture of the underlying operating system (i.e. running on 64-bit OS will produce a native 64-bit exe). This can be overriden via the --arch switch and specifying the wanted architecture. The executable has a default extension of "" on Linux and OS X and ".exe" on Windows. The source must include a static void main entry point, or it is an error, unless otherwise specified in the project.json. The dynamic library [dylib] output option has the default extension of ".so" on Linux/UNIX, ".dynlib" on OS X and ".dll" on Windows. The static library [staticlib] option has the default extension of ".a" on Linux, UNIX and OS X and ".lib" on Windows.

This command relies on the following artifacts: source files, project.json project file and the "lock" file (project.lock.json). Prior to invoking dotnet-compile, dotnet-restore(1) should be run to restore any dependencies that are needed for the application.  

# OPTIONS 

`-n, --native`
    
    Compiles source to native machine code, for the local machine. The default is a native executable. The default executable extension is no extension and ".exe" on Windows.

`-t, --temp-output <PATH>`
    
    Path where to drop the temporary binaries that are produced during compile. By default, the temporary binaries are dropped in the `obj` directory in the directory where `project.json` files lives, that is, where the application lives.  

`-f, --framework <FID>`
    
    Compile the application for the specified framework. If the framework is not specified, one specified in `project.json` will be used. 

`-c, --configuration <CONFIGURATION>`
    
    Compile the application under the specified configuration. If not specified, the configuration will default to "Debug".  

`-o, --output filename`
    
    Specifies the filename to be used. By default, the resulting filename will be the same as the project name specified in `project.json`, if one exists, or the directory in which the source files reside. 

`--no-project-dependencies`
    
    Skips building cross-project references. The effect of this is that only the current project will be built. 

`-a, --arch`
    
    The architecture for which to compile. x64 only currently supported.

`--ilc-args <ARGS>`
    
    Specified parameters are passed through to ILC and are used by the engine when doing native compilation. 

`--cpp`
    
    Specify the C++ code generator to do native compilation of code instead of the default RyuJIT.       

`-v, --verbose`
    
    Prints verbose logging information, to follow the flow of execution of the command.

`-h, --help`
    
    Show short help. 

# ENVIRONMENT 

`DOTNET_HOME`

    Points to the base directory that contains the runtime and the binaries directories. The runtime will be used to run the executable file that is dropped after compiling. Not needed for native compilation.  
    
# SEE ALSO
dotnet-restore(1), dotnet-publish(1), dotnet(1)
