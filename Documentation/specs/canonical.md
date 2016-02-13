Canonical scenarios
===================

# Overview

The scenarios are all grouped around the major activity that the developer is expected to do. Each scenario has the following components: 

* A description of the scenario, from the perspective of the developer
* Set of commands that are used to get results
* Description of the result(s) that should happen

Unless otherwise specified, the commands and scenario below imply a basic console application as the main code. Unless otherwise specified, all of the samples have one project.json and no global.json. The project.json file is the following:

```json
{
    "version": "1.0.0-*",
    "compilationOptions": {
        "emitEntryPoint": true
    },

    "dependencies": {
    },

    "frameworks": {
        "netstandard10": { }
    }
}
```

# Acquisition
The idea of acquisition is simple. We need to get bits to the developer machines. Acquisition in general has the following principles applied to it:

* Each platform uses its own native package 
* Post acquisition the user is able to start using the toolchain and .NET Core immediately 

Also, most of the scenarios here mention a *well-known location*, which here means the following URLs for the CLI toolchain:

* For Windows: https://dotnetcli.blob.core.windows.net/dotnet/dev/Installers/Latest/dotnet-win-x64.latest.msi
* For OS X: https://dotnetcli.blob.core.windows.net/dotnet/dev/Installers/Latest/dotnet-osx-x64.latest.pkg

## Windows interactive install

### Description
The users download the MSI off of a well-known location. The user double clicks on the MSI to start the installation. 

### Commands
N/A - GUI interaction

### Results
The package is installed with no errors. Running `dotnet` produces the proper output. 

## Ubuntu install

### Description
The user adds an apt-get feed to their machine and update their package index. After that, they can install the `dotnet` package using the proper commands. 

### Commands
`sudo sh -c 'echo "deb [arch=amd64] http://apt-mo.trafficmanager.net/repos/dotnet/ trusty main" > /etc/apt/sources.list.d/dotnetdev.list'`
 
`sudo apt-key adv --keyserver apt-mo.trafficmanager.net --recv-keys 417A0893`

`sudo apt-get update`

`sudo apt-get install dotnet`

### Results
The package is installed with no errors. Running `dotnet` produces the proper output. 

## OS X interactive install 

### Description
The user downloads the PKG file from a well-known location. The user double-clicks the PKG file to start the installation. 

### Commands
N/A - GUI interaction 

### Results
The package is installed with no errors. Running `dotnet` produces the proper output. 


# Initializing and working with projects
We group several things here that all help developers get started with their projects and code. Operations that fall into this 
category cover basic working with packages (restoring and installing) as well as getting started with the "blessed" project files and basic code samples.

## Initializing the project & code

### Description
The developer has acquired the .NET Command Line Interface (CLI) toolchain on her operating system. She wants to have a quick sample of how the code and the project file look like. She also wants to udnerstand the structure of the project file, and have a "blessed" version of the project file that she can modify. 
### Commands

`mkdir testapp`

`cd testapp` 

`dotnet init`

### Results
Directory is populated with 2 files:

1. Code file (Program.cs)
2. Project file (project.json)

## Installing packages

### Description
Once the initial code is dropped using the `dotnet init` command, the developer decides to add a package to her application. She scours the depths of [Nuget.org](https://www.nuget.org) and finds the package she needs. She now wants to install the package to the local machine as well as add it to her `project.json` file. 

### Commands

`dotnet install-package Json.net`

### Results
JSON.net is installed in the local cache. The `project.json` has a new line in it that outlines the new dependency that was added. 

## Restoring packages

### Description
Our developer has commited her initial work into source control and cloned/restored that work to another machine. She wants to continue developing on the new machine, and for that, she needs to get all of the dependencies her application has. 

### Commands

`dotnet restore`

`dotnet run`

### Results
All packages are restored to the local cache. The last command runs the application and no missing dependencies errors are encountered. 

# Building code 
Building code scenarios cover all of the actions that produce an executable artifact on disk, be it a single file or a "package" of some sorts. I called this set of scenarios "building" because of that, not because they imply any orchestration or similar things. 

## Compiling code to IL executable

### Description
The developer wants to check if the code has compile errors (that is, does it compile). Developer also wants 
to try to run the application being developed to test whether it works, so the expectation is that the product of compile is always runnable. 

### Commands

`mkdir testapp`

`cd testapp`

`dotnet init`

`dotnet compile`

`./bin/Debug/netstandard10/testapp`

### Results
Outputs "Hello World" to the console. No errors are encountered. 

## Compiling code to IL library

### Description
When creating an application, the developer wants to move some of the code into a separate library. She wants to use the toolchain to produce a managed, IL assembly without producing an executable. 

In order to do that, the following `project.json` is used:
```json
{
    "version": "1.0.0-*",

    "dependencies": {
    },

    "frameworks": {
        "netstandard10": { }
    }
}
```

### Commands

`mkdir testapp`

`cd testapp`

`dotnet compile`

`ls ./bin/Debug/netstandard10/`


### Results
The final command shows just two files, an libname.dll and a libname.pdb files. No executable is dropped.  

## Compiling code to native

### Description
The developer wants to have a small microservice that is easily deployed to a Docker container or similar barebones machine. She writes her application and then compiles it down to a single native executable binary that can be moved easily eslwewhere.  

### Commands

`mkdir testapp`

`cd testapp`

`dotnet init`

`dotnet compile --native`

`./bin/Debug/netstandard10/native/testapp`

### Results
Outputs "Hello World" to the console. No errors are encountered. 

## Publishing a self-contained application with default runtime and framework 

### Description
Developer wants to have a runtime coupled and packaged with the application being built. The final package contains the 
application code and the runtime which makes it cleanly deployable to another machine, *regardless* of presence of .NET 
Core and/or the CLI toolchain on it. 

The developer has a single framework defined in her `project.json` file. The developer doesn't want to specify the runtime version or the framework, instead expecting sensible defaults to be inferred.  

### Commands
`dotnet publish`

`ls ./bin/Debug/netstandard10/[runtime-id]`

`./bin/Debug/netstandard10/[runtime-id]/appname`

### Results 
The entire path exists. Command #2 shows all of the DLLs that the application requires as well as the needed native files that comprise the runtime for a given platform. The final command is runnable and there are no errors when running it.  

## Publishing a self-contained application with default runtime and multiple frameworks

### Description
Similar to the previous one, however, the developer has added several frameworks in `project.json`. The file looks like this now:
```json
{
    "version": "1.0.0-*",
    "compilationOptions": {
        "emitEntryPoint": true
    },

    "dependencies": {
    },

    "frameworks": {
        "netstandard10": { },
        "netstandard20": { }
    }
}
```

The developer wants 

### Commands
`dotnet publish`

`ls ./bin/Debug/publish/[runtime-id]`

`./bin/Debug/publish/[runtime-id]/netstandard10/appname`

`./bin/Debug/publish/[runtime-id]/netstandard20/appname`

### Results 
The paths are created wthout errors. The ls command shows the following two directories exist:

1. `netstandard10`
2. `netstandard20`

The last two commands are runnable and the applications produce the expected results. 

## Publishing a self-contained application with specific runtime

**[TODO]**

## Publishing a self-contained application with shared runtime w/ framework

### Description
Developer wants to create a publishing package that contains her application code and her dependencies, but relies on the shared runtime on the deployment target. This is being done because the deployment and servicing of the .NET Core runtime(s) is done by central IT. 

The developer creates the following `project.json` file with a single dependency that is extrenal to the standard library:

```json
{
    "version": "1.0.0-*",
    "compilationOptions": {
        "emitEntryPoint": true
    },

    "dependencies": {
        "Newtonsoft.JSON": "7.0.1"
    },

    "frameworks": {
        "netstandard10": { }
    }
}
```

### Commands

`dotnet publish --runtime shared`

`ls ./bin/Debug/netstandard10/[runtime-id]/libcore*`

`ls ./bin/Debig/netstandard10/[runtime-id]/`

`./bin/Debug/netstandard10/[runtime-id]/appname`

### Results 
The path in `/bin/Debug/netstandard10/` is created correctly with the correct runtime id directory. The second command should not find any files, which confirms that the runtime was not deployed. Third command should show the executable files as well as . The final command should still run. 


# Running code
In this set of scenarios we are focused on quick testing and turnaround, as well as application types that have different running scenarios than console applications. 

## Running from source

### Description
The developer is working on a web application using ASP.NET 5 (MVC6). She wants to see if her application will work, and wishes to run it directly without bothering with compile. 

**[TODO]: add Startup.cs with adequate code**

### Commands

`dotnet run`

### Results
The application is ran. Since this is a web application, the developer can navigate to a defined URL to test out the application. 

# Testing code

### Description
Developer is close to finishing her application. However, as she is completing the scenarios, she also wants to add a certain amount of unit tests written using a popular test runner. She writes the unit tests in a separate directory that is a sibling to the code directory. 

The test runner is defined in `project.json` file, and it is pointed to the source files that contain the tests. 

### Commands

`dotnet test`

### Results 
The unit tests are compiled, assemblies dropped in the location specified by `dotnet compile`. The tests are then executed using the specified test runner. 
