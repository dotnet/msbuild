dotnet-compile
===========

**NAME** 
dotnet-compile-native -- Compiles IL binaries to native binaries.

**SYNOPSIS**
dotnet compile [options]

**DESCRIPTION**
The `compile-native` command compiles IL assemblies to native machine code. It is used by `dotnet-compile --native`.

The output is a native exe that conforms to the architecture of the underlying operating system (i.e. running on 64-bit OS will produce a native 64-bit exe). This can be overriden via the --arch switch and specifying the wanted architecture. The executable has a default extension of "" on Linux and OS X and ".exe" on Windows. The source must include a `static void Main(string[] args) entry point and specify compilerOptions.emitEntryPoint in the project.json. 

Output files are written to the child `bin` folder, which will be created if it doesn't exist. Files will be overwritten as needed.

**Options**

--appdepsdk <SDK_PATH>
Path to custom AppDepSDK

-c, --configuration [debug|release]
Build configuration. Defaults to `debug`.

--ilcargs <args>
Custom arguments for the IL Compiler.

--ilcpath <ILC_PATH>
Path to a custom ilc.exe

--linklib <LIB_PATH>
Path to static lib to link

--logpath <LOG_PATH>
Enables logging and writes native compilation logs to the given path.

-m, --mode [cpp|ryujit|custom]
Code generation mode. Defaults to ryujit.

-o, --out directoryname
Output directory for the native executable.

-r, --reference
Path to a managed dll reference for the app.

-t, --temp-out
Specifies temporary directory for intermediate files.

-v, --verbose
Prints verbose logging information, to follow the flow of execution of the command.
