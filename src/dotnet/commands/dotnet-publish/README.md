% DOTNET-PUBLISH(1)
% Microsoft Corporation dotnetclifeedback@microsoft.com
% June 2016

## NAME

`dotnet-publish` - Packs the application and all of its dependencies into a folder getting it ready for publishing

## SYNOPSIS

`dotnet publish [--framework]  
    [--runtime] [--build-base-path] [--output]  
    [--version-suffix] [--configuration]  
    [<project>]`  

## DESCRIPTION

`dotnet publish` compiles the application, reads through its dependencies specified in the `project.json` file and publishes the resulting set of files to a directory. 

Depending on the type of portable app, the resulting directory will contain the following:

1. **Portable application** - application's intermediate language (IL) code and all of application's managed dependencies.
    * **Portable application with native dependencies** - same as above with a sub-directory for the supported platform of each native
    dependency. 
2. **Self-contained application** - same as above plus the entire runtime for the targeted platform.

The above types are covered in more details in the [types of portable applications](../../app-types.md) topic. 

## OPTIONS

`[project]` 
    
`dotnet publish` needs access to the `project.json` file to work. If it is not specified on invocation via [project], `project.json` in the current directory will be the default.     
If no `project.json` can be found, `dotnet publish` will throw an error. 

`-f`, `--framework` [FID]

Publishes the application for a given framework identifier (FID). If not specified, FID is read from `project.json`. In no valid framework is found, the command will throw an error. If multiple valid frameworks are found, the command will publish for all valid frameworks. 


`-r`, `--runtime` [RID]

Publishes the application for a given runtime. 

`-b`, `--build-base-path` [DIR]

Directory in which to place temporary outputs.

`-o`, `--output`

Specify the path where to place the directory. If not specified, it will default to _./bin/[configuration]/[framework]/_ 
for portable applications or _./bin/[configuration]/[framework]/[runtime]_ for self-contained applications.

--version-suffix [VERSION_SUFFIX]

Defines what `*` should be replaced with in the version field in the project.json file.

`-c`, `--configuration [Debug|Release]`

Configuration to use when publishing. The default value is Debug.

## EXAMPLES

`dotnet publish`

Publishes an application using the framework found in `project.json`. If `project.json` contains `runtimes` node, publish for the RID of the current platform.

`dotnet publish ~/projects/app1/project.json`
    
Publishes the application using the specified `project.json`.
	
`dotnet publish --framework netcoreapp1.0`
    
Publishes the current application using the `netcoreapp1.0` framework.
	
`dotnet publish --framework netcoreapp1.0 --runtime osx.10.11-x64`
    
Publishes the current application using the `netcoreapp1.0` framework and runtime for `OS X 10.10`. This RID has to 
exist in the `project.json` `runtimes` node.