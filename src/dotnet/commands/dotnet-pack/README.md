% DOTNET-PACK(1)
% Microsoft Corporation dotnetclifeedback@microsoft.com
% April 2016

## NAME

dotnet-pack - packs the code into a NuGet package

## SYNOPSIS

dotnet-pack [--output]  
    [--no-build] [--build-base-path]  
    [--configuration]  [--version-suffix]
    [< project >]  

## DESCRIPTION

`dotnet-pack` will build the project and package it up as a NuGet file. The result of this operation are two packages 
with the extension of `nupkg`. One package contains the code and another contains the debug symbols. 

NuGet dependencies of the project being packed are added to the nuspec file so they are able to be resolved when the 
package is installed. Project-to-project references are not packaged inside the project by default. If you wish to do 
this, you need to reference the required project in your dependencies node with a `type` set to "build":

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

`dotnet-pack` will by default build the project. If you wish to avoid this pass the `--no-build` option. This would be 
useful in CI build scenarios in which you know the code was just previously built. 

## OPTIONS

`[project]` 
    
The project to pack. It can be either a path to a `project.json` file or a path to a directory. If omitted, will
default to the current directory. 

`-o`, `--output` [DIR]

Place the built packages in the directory specified. 


`--no-build`

Skip the building phase of the packing process. 

`--build-base-path`

Place the temporary build artifacts in the specified directory. By default, they go to obj directory in the current directory. 

`-c`, `--configuration [Debug|Release]`

Configuration to use when building the project. If not specified, will default to "Debug".

## EXAMPLES

### Pack the current project
`dotnet-pack`

### Pack the specific project
`dotnet-pack ~/projects/app1/project.json`

### Pack the current application and place the resulting packages into the specified folder	
`dotnet-pack --output nupkgs`

### Pack the current project into the specified folder and skip the build step
`dotnet-pack --no-build --output nupkgs`

### Add files to a project
Add following section in the project.json
```json
{
    "packInclude": {
        "dir/in/the/package/": "path_relative_to_project.json",
        "other/dir/in/the/package/": "absolute_path_to_a.file",
        "another/dir/in/the/package/": ["file1.txt", "file2.txt", "file3.txt"],
        "runtimes/ubuntu.14.04-x64/native/": "rid_specific_native_file.so"
    }
}
```

## SEE ALSO

