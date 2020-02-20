% DOTNET-RESTORE(1)
% Microsoft Corporation dotnetclifeedback@microsoft.com
% June 2016

## NAME

`dotnet-restore` - Restores the dependencies and tools of a project

## SYNOPSIS

`dotnet restore [--source]  
    [--packages] [--disable-parallel]  
    [--fallbacksource] [--configfile] [--verbosity]
    [<root>]`  

## DESCRIPTION

The `dotnet restore` command uses NuGet to restore dependencies as well as project-specific tools that are specified in the project.json file. 
By default, the restoration of dependencies and tools are done in parallel.

In order to restore the dependencies, NuGet needs the feeds where the packages are located. 
Feeds are usually provided via the NuGet.config configuration file; a default one is present when the CLI tools are installed. 
You can specify more feeds by creating your own NuGet.config file in the project directory. 
Feeds can also be specified per invocation on the command line. 

For dependencies, you can specify where the restored packages are placed during the restore operation using the 
`--packages` argument. 
If not specified, the default NuGet package cache is used. 
It is found in the `.nuget/packages` directory in the user's home directory on all operating systems (for example, `/home/user1` on Linux or `C:\Users\user1` on Windows).

For project-specific tooling, `dotnet restore` first restores the package in which the tool is packed, and then
proceeds to restore the tool's dependencies as specified in its project.json. 

## OPTIONS

`[root]` 
    
 A list of projects or project folders to restore. The list can contain either a path to a `project.json` file, or a path to `global.json` file or folder. The restore operation runs recursively for all subdirectories and restores for each given project.json file it finds.

`-s`, `--source` [SOURCE]

Specifies a source to use during the restore operation. This overrides all of the sources specified in the NuGet.config file(s). 

`--packages` [DIR]

Specifies the directory to place the restored packages in. 

`--disable-parallel`

Disables restoring multiple projects in parallel. 

`-f`, `--fallbacksource` [FEED]

Specifies a fallback source that will be used in the restore operation if all other sources fail. All valid feed formats are allowed. 

`--configfile` [FILE]

Configuration file (NuGet.config) to use for the restore operation. 

`--verbosity` [LEVEL]

The verbosity of logging to use. Allowed values: Debug, Verbose, Information, Minimal, Warning, or Error.

## EXAMPLES

`dotnet restore`

Restores dependencies and tools for the project in the current directory. 

`dotnet restore ~/projects/app1/project.json`
    
Restores dependencies and tools for the `app1` project found in the given path.
	
`dotnet restore --f c:\packages\mypackages`
    
Restores the dependencies and tools for the project in the current directory using the file path provided as the fallback source. 
	
`dotnet restore --verbosity Error`
    
Restores dependencies and tools for the project in the current directory and shows only errors in the output.
