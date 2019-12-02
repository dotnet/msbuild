Canonical scenarios
===================

# Contents

* [Overview](#overview)
* [Acquisition](#acquisition)
* Scenarios
  * [Starting a new console application](#starting-a-new-console-application)
  * [Starting a new class library](#starting-a-new-class-library)
  * [Adding 3rd party dependencies to the projects](#adding-3rd-party-dependencies-to-the-projects)
  * [Running unit tests](#running-unit-tests)
  * [Publishing a shared runtime console application](#publishing-a-shared-runtime-console-application)
  * [Publishing a self-contained console application for all platforms](#publishing-a-self-contained-console-application-for-all-platforms)
  * [Packaging a class library](#packaging-a-class-library)
  * [Installing `dotnet` extensions as tools](#installing-dotnet-extensions-as-tools)

# Overview

This document outlines the End-to-End canonical scenarios for the CLI tooling. The scenarios outline the planned steps that the developer needs to do to work with her applications. 

Each scenario is organized around a narrative, which provides an explanation on what the developers are trying to do, steps that are needed for the user to achieve the needed narrative. Steps are organized as commands that the developer would need to execute on the command line to achieve the result.

These scenarios are focused on console applications and libraries.

# Acquisition
All of the scenarios below assume that the CLI tools have been acquired in some way. The acquisition of the CLI tools is explained in detail in a [separate specification](cli-install-experience.md). This document only contains a very short summary of that document.

There are two main ways to acquire the CLI toolset:
1. Using targeted platform's native installers - this approach is used by developers who want to get stable bits on their development machines and don't mind the system-wide installation and need for elevated privileges. 
2. Using a local install (a zip/tarball) - this approach is used by developers who want to enable their build servers to use CLI toolset or who want to have multiple, side-by-side installs.

The bits that are gotten are same modulo potential differences in stability of the bits, however, the smoothness of the experience is not. With native installers the installers themselves do as much as possible to set up a working environment (installing dependencies where possible, setting needed environment variables etc.). Local installs require all of the work to be done by developers after dropping bits on the machine.

The below scenarios must work regardless of the way used to acquire the tools. 



# Starting a new console application

## Narrative
The developer would like to kick the tires on .NET Core by writing a console application. She would like to use the new .NET Core CLI tooling to help her get started, manage dependencies and quickly test out the console application by running it from source. She would then like to try building the code and running it using the shared host that is installed with the CLI toolset. 

## Steps
1. Create a C# console application via `dotnet new` command

```
/myapp> dotnet new myapp 

```
2. Edit  the C# code 

```
  namespace myapp
  {
      public static class Program
      {
          public static void Main(string[] args)
          {
              Console.WriteLine("Hello, World!");
          }
      }
  }
```

3. Restore packages
  ```
  /myapp> dotnet restore
  
  [messages about restore progress]
  
  Writing lock file /myapp/project.lock.json
  
  /myapp> 
  ```

4. Run from source for a quick test

```
/myapp> dotnet run

Hello World!

/myapp>
```

5. Build a binary that can be executed by the shared host

```
/myapp> dotnet build

[information about the build]

  Creating build output:
    /myapp/bin/Debug/netstandardapp1.5/myapp.dll
    /myapp/bin/Debug/netstandardapp1.5/myapp.deps
    /myapp/bin/Debug/netstandardapp1.5/[All dependencies' IL assemblies].dll

/myapp>
```

6. Run the built version using the shared host in the installed toolset

```
/myapp> dotnet run /myapp/bin/Debug/netstandardapp1.5/myapp.dll
Hello World!
```

 
# Starting a new class library

## Narrative 
Once started, the developer wants to also include a class library in order to have a place to share common code. She wants to use the CLI toolset to bootstrap this effort as well. 

## Steps

1. Create a new class library using `dotnet new`

```
/> dotnet new mylib --type lib

Creating a "mylib" class library in "mylib"

/mylib> 
```

2. Restore the dependencies

  ```
  /mylib> dotnet restore
  
  [messages about restore progress]
  
  Writing lock file /mylib/project.lock.json
  
  /mylib> 
  ```


3. Edit the `MyLib.cs` file

```
  namespace mylib
  {
      public class mylib
      {
          public void Method1()
          {
          }
      }
  }
```

4. Build the code

```
/mylib> dotnet build

[information about the build]

  Creating build output:
    /mylib/bin/Debug/netstandardapp1.5/mylib.dll
    /mylib/bin/Debug/netstandardapp1.5/mylib.deps
    /mylib/bin/Debug/netstandardapp1.5/[All dependencies' IL assemblies].dll

/mylib>

```

# Adding 3rd party dependencies to the projects

## Narrative
Working towards a complete application, the developer realizes she needs to add good JSON parsing support. Searching across the internet, she finds JSON.NET to be the most recommended choice. She now uses the CLI tooling to install a dependency off of NuGet. 

>**NOTE:** the shape of the commands used in this scenario is still being discussed. 

## Steps

1. Install the package

```
/myapp> dotnet pkg install Newtonsoft.Json --version 8.0.2

[lots of messages about getting JSON.NET]

Writing lock file /tests/project.lock.json

/myapp>
```

2. Change the code to use the new dependency

```
   using Newtonsoft.Json;
   namespace myapp
  {
      public static class Program
      {
          public static void Main(string[] args)
          {
              var thing = JsonConvert.DeserializeObject("{ 'item': 1 }");
              Console.WriteLine("Hello, World!");
              Console.WriteLine(thing.item);
          }
      }
  }
```

3. Run code from source 

```
/myapp> dotnet run
Hello, World!
1
/myapp>
```

# Running unit tests

## Narrative
Writing tests is important, and our developer knows that. She is now writing out the shared logic in her class library and she wants to make sure that she has test coverage. Investigating the manuals, she realizes that the CLI toolset comes with support for xUnit tests including the test runner.  

## Steps

1. Create a new xunit test project using `dotnet new`

```
/> dotnet new tests --type xunit
Created "tests" xunit test project in "tests".

/tests>
```
2. Restore the runner and dependencies

  ```
  /tests> dotnet restore
  
  [messages about restore progress]
  
  Writing lock file /tests/project.lock.json
  
  [messages about tool dependencies restore]
  
  /tests> 
  ```

3. Add a test to the test class
```
using System;
using Xunit;

namespace tests
{
    public class Tests
    {
        [Fact]
        public void AssertTrue() {
            Assert.True(true);
        }        
    }
}
```

3. Run tests using `dotnet test`

```
/tests> dotnet test

[information about discovery of tests]

=== TEST EXECUTION SUMMARY ===
   test  Total: 1, Errors: 0, Failed: 0, Skipped: 0, Time: 0.323s
 
/tests>
```

# Publishing a shared runtime console application

## Narrative
Coding away on the application has proven worthwhile and our developer wants to share her progress with another developer on her team. She wants to give just the application and its dependencies. Luckily, another developer can easily install the .NET Core SDK and get a shared host, which would be enough to run the application. The CLI toolset allows our developer to publish just the application's code (in IL) and dependencies. 

## Steps

1. Publish the application
```
  /myapp> dotnet publish --output /pubapp
  
  [Lots of messages about publishing stuff]
  
  Creating publish output:
    /pubapp/myapp/myapp.dll
    /pubapp/myapp/myapp.deps
    /pubapp/myapp/[All dependencies' IL assemblies].dll
  
  /myapp> 
  ```

2. Run the project publish output:
  ```
  /myapp> cd /pubapp/myapp
  /pubapp/myapp> dotnet ./myapp.dll
  Hello, World!
  
  /published/myapp> 
  ```
3. The published application can be transferred over to a machine that has the .NET Core shared host installed and it is possible for it to be ran. 

# Publishing a self-contained console application for all platforms

## Narrative
After getting feedback from her colleague developer, our developer decides to test on another machine. However, this machine doesn't have the shared host installed and she cannot get it installed. Luckily, she realizes that .NET Core has support for self-contained applications 

**NOTE**: some of the behaviours in this scenario are still being discussed with the relevant stakeholders. 

## Steps

1. Modify the project file to enable it to be published as a standalone, platform-specific application (one that doesn't require `dotnet` on the target machine to run) for the desired platforms by adding the `"runtimes"` section:
  ```
  {
    "imports": {
      "Microsoft.ProjectType.ConsoleApplication": "1.0.0"
    },
    "runtimes": {
      "linux-x64": { },
      "win7-x64": { }
    }
  }
  ```

2. Restore the project's dependencies again to ensure the platform-specific dependencies for the specified runtimes are acquired:
  ```
  /myapp> dotnet restore
  
  [lots of messages about restoring stuff]
  
  Writing lock file /myapp/project.lock.json
  
  /myapp> 
  ```



3. Publish the project again. In this case, the publish will publish for each runtime in the `project.json` file
  ```
  /myapp> dotnet publish --output /published/myapp
  
  [Lots of messages about publishing stuff]
  
  Creating publish output for (linux-x64):
    /published/myapp-linux-x64/myapp
    /published/myapp-linux-x64/myapp.dll
    /published/myapp-linux-x64/myapp.deps
    /published/myapp-linux-x64/[All dependencies' IL & platform-specific assemblies, inc. stdlib]

  Creating publish output for (win7-x64):
    /published/myapp-win7-x64/myapp
    /published/myapp-win7-x64/myapp.dll
    /published/myapp-win7-x64/myapp.deps
    /published/myapp-win7-x64/[All dependencies' IL & platform-specific assemblies, inc. stdlib]
  
  /myapp> 
 
  ```
  
4. Any of the outputs above can be xcopied to the platform in question and it will work without having to have the shared host installed. 
  
5. Publish the project for a specific platform (win7-x64):

```
  /myapp> dotnet publish --output /win7/myapp --runtime win7-x64
  
  [Lots of messages about publishing stuff]
  
  Creating publish output for (win7-x64):
    /published/myapp-win7-x64/myapp
    /published/myapp-win7-x64/myapp.dll
    /published/myapp-win7-x64/myapp.deps
    /published/myapp-win7-x64/[All dependencies' IL & platform-specific assemblies, inc. stdlib]
  
  /myapp> 
  ```

# Packaging a class library 

## Narrative 
The developer wants to take the library she built and package it up as a NuGet package in order to share it with the rest of the ecosystem. Again, she would like to use the CLI toolset to achieve this. Since she wants to be sure that all her code is in a pristine condition, she will also build it one more time, run tests and then package it. 

## Steps 
1. Build the code to make sure no build errors have crept in

```
/mylib> dotnet build

[information about the build]

  Creating build output:
    /myapp/bin/Debug/netstandardapp1.5/myapp.dll
    /myapp/bin/Debug/netstandardapp1.5/myapp.deps
    /myapp/bin/Debug/netstandardapp1.5/[All dependencies' IL assemblies].dll

/mylib>

```

2. Switch to the test project and run unit tests

```console
[switch to the directory containing unit tests]
/mytests> dotnet test

[info about tests flies around]

=== TEST EXECUTION SUMMARY ===
   test  Total: 50, Errors: 0, Failed: 0, Skipped: 0, Time: 5.323s

/mytests>

``` 

3. Package the library 

```console
[switch to the library directory]

/mylib> dotnet pack

[information about build is shown]

Producing nuget package "mylib.1.0.0" for mylib
mylib -> /mylib/bin/Debug/mylib.1.0.0.nupkg
Producing nuget package "mylib.1.0.0.symbols" for mylib
mylib -> /mylib/bin/Debug/mylib.1.0.0.symbols.nupkg

/mylib> 
```

# Installing `dotnet` extensions as tools 

## Narrative
As our developer is going further with her usage of the CLI tools, she figures out that there is an easy way to extend the CLI tools on her machine by adding project-level tools to her `project.json`. She uses the CLI to work with the tools and she is able to extend the default toolset to further fit her needs. 

## Steps 
>**TODO:** at this point, this needs more work to figure out how it will surface; it is listed here so it is not forgotten.


