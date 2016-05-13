% DOTNET-PUBLISH(1)
% Microsoft Corporation dotnetclifeedback@microsoft.com
% April 2016


## NAME

`dotnet-publish` - packs the application and all of its dependencies into a folder getting it ready for publishing

## SYNOPSIS

dotnet-publish [--framework]  
    [--runtime] [--build-base-path] [--output]  
    [--version-suffix] [--configuration]  
    [< project >]  

## DESCRIPTION

`dotnet-publish` builds the application, reads through its dependencies specified in `project.json` and publishes the resulting set of files to a directory. 

Depending on the type of portable app, the directory contains the following:

1. **Portable application** - application's intermediate language (IL) code and all of application's managed dependencies.
    * **Portable application with native dependencies** - as above with a sub-directory for each native dependencies' 
    supported platform. 
2. **Self-contained application** - as above as well as the entire runtime for the targeted platform.

The above types are covered in more details in the [types of portable applications](app-types.md) document. 

## OPTIONS

`[project]` 
    
`dotnet-publish` needs access to `project.json` to work. If it is not specified on invocation via [project], 
`project.json` in the current directory will be the default.     
If no `project.json` can be found, `dotnet-publish` will error out. 

`-f`, `--framework` [FID]

Publish the application for a given framework identifier (FID). If not specified, FID is read from `project.json`. In case of no valid framework found, the command will error out. In case of multiple valid frameworks found, the command will publish for all valid frameworks. 


`-r`, `--runtime` [RID]

Publish the application for a given runtime. 

`-b`, `--build-base-path` [DIR]

Directory in which to place temporary outputs

`-o`, `--output`

Specify the path where to place the directory. If not specified, will default to _./bin/[configuration]/[framework]/_ 
for portable applications. For self-contained applications, will default to _./bin/[configuration]/[framework]/[runtime]_

--version-suffix [VERSION_SUFFIX]

Defines what `*` should be replaced with in the version field in project.json.

`-c`, `--configuration [Debug|Release]`

Configuration to use when publishing. If not specified, will default to "Debug".

## EXAMPLES

`dotnet publish`

Publish an application using the framework found in `project.json`. If `project.json` contains `runtimes` node, publish 
for the RID of the current platform. 

`dotnet publish ~/projects/app1/project.json`
    
Publish the application using the specified `project.json`.
	
`dotnet publish --framework netcoreapp1.0`
    
Publish the current application using the `netcoreapp1.0` framework.
	
`dotnet publish --framework netcoreapp1.0 --runtime osx.10.11-x64`
    
Publish the current application using the `netcoreapp1.0` framework and runtime for `OS X 10.10`. This RID has to 
exist in the `project.json` `runtimes` node. 

## SEE ALSO

dotnet-restore(1), dotnet-build(1)
