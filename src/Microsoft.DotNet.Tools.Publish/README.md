% DOTNET-PUBLISH(1)
% Zlatko Knezevic zlakne@microsoft.com
% January 2016

# NAME

`dotnet-publish` - packs the application and all of its dependencies into a folder getting it ready for publishing

# SYNOPSIS

dotnet-publish [--framework]  
    [--runtime] [--output]  
    [--configuration]  
    [< project >]  

# DESCRIPTION

`dotnet-publish` will compile the application, read through its dependencies specified in `project.json` and publish the resulting set of files to a directory. 
This directory will contain the assemblies, the runtime as well as the executable version of the application. This directory can then be moved to a different machine and the application will be able to run regardless of existence of any other runtime. 

The native dependencies of the runtime are not packaged with the application. This means that the target machine needs to have the native dependencies installed in order for the application to run.  

# OPTIONS

`[project]` 
    
    `dotnet-publish` needs access to `project.json` to work. If it is not specified on invocation via [project], `project.json` in the current directory will be the default.     If no `project.json` can be found, `dotnet-publish` will error out. `dotnet-publish` command also requires certain dependencies in the `project.json` to work. Namely the `Microsoft.NETCore.Runtime` package must be referenced as a dependency in order for the command to copy the runtime files as well as the application's files to the published location.  

`-f`, `--framework` [FID]

    Publish the application for a given framework identifier (FID). If not specified, FID is read from `project.json`. In case of no valid framework found, the command will error out. In case of multiple valid frameworks found, the command will publish for all valid frameworks. 


`-r`, `--runtime` [RID]

    Publish the application for a given runtime. If the option is not specified, the command will default to the runtime for the current operationg system. Supported values for the option at this time are:

        * ubuntu.14.04-x64
        * win7-x64
        * osx.10.10-x64

`-o`, `--output`

    Specify the path where to place the directory. If not specified, will default to _./bin/[configuration]/[framework]/[runtime]/_

`-c`, `--configuration [Debug|Release]`

    Configuration to use when publishing. If not specified, will default to "Debug".

# EXAMPLES

`dotnet-publish`

    Publish the current application using the `project.json` framework and runtime for the current operating system. 

`dotnet-publish ~/projects/app1/project.json`
    
    Publish the application using the specified `project.json`; also use framework specified withing and runtime for the current operating system. 
	
`dotnet-publish --framework dnxcore50`
    
    Publish the current application using the `dnxcore50` framework and runtime for the current operating system. 
	
`dotnet-publish --framework dnxcore50 --runtime osx.10.10-x64`
    
    Publish the current application using the `dnxcore50` framework and runtime for `OS X 10.10`

# SEE ALSO

dotnet-restore(1), dotnet-compile(1)
