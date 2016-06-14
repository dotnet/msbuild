% DOTNET-PACK(1)
% Microsoft Corporation dotnetclifeedback@microsoft.com
% June 2016

## NAME

`dotnet-pack` - Packs the code into a NuGet package

## SYNOPSIS

`dotnet pack [--output]  
    [--no-build] [--build-base-path]  
    [--configuration]  [--version-suffix]
    [<project>]`  

## DESCRIPTION

The `dotnet pack` command builds the project and creates NuGet packages. The result of this operation is two packages with the `nupkg` extension. One package contains the code and the other contains the debug symbols. 

NuGet dependencies of the project being packed are added to the nuspec file, so they are able to be resolved when the package is installed. 
Project-to-project references are not packaged inside the project by default. If you wish to do this, you need to reference the required project in your dependencies node with a `type` set to "build" like in the following example:

```json
{
    "version": "1.0.0-*",
    "dependencies": {
        "ProjectA": {
            "target": "project",
            "type": "build"
        }
    }
}
```

`dotnet pack` by default first builds the project. If you wish to avoid this, pass the `--no-build` option. This can be useful in Continuous Integration (CI) build scenarios in which you know the code was just previously built, for example. 

## OPTIONS

`[project]` 
    
The project to pack. It can be either a path to a `project.json` file or to a directory. If omitted, it will
default to the current directory. 

`-o`, `--output` [DIR]

Places the built packages in the directory specified. 

`--no-build`

Skips the building phase of the packing process. 

`--build-base-path`

Places the temporary build artifacts in the specified directory. By default, they go to the obj directory in the current directory. 

`-c`, `--configuration [Debug|Release]`

Configuration to use when building the project. If not specified, will default to "Debug".

## EXAMPLES

`dotnet pack`

Packs the current project.

`dotnet pack ~/projects/app1/project.json`
    
Packs the app1 project.
	
`dotnet pack --output nupkgs`
    
Packs the current application and place the resulting packages into the specified folder.

`dotnet pack --no-build --output nupkgs`

Packs the current project into the specified folder and skips the build step.