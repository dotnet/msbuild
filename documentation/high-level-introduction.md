# A gentle introduction to MSBuild

## Introduction

This document aims to be a gentle introduction to MSBuild, useful for those with very little background in how it works.

### What is MSBuild?

MSBuild is a build system commonly used to build .NET projects. 

It consists of the MSBuild toolchain itself and additional files from the SDK that provide platform-specific processing instructions.

MSBuild is based upon a few simple ideas:

- Run a sequence of defined steps (Tasks) based upon input / output files and dependency ordering
- Store single and groups of values in a global table
- Load and execute C# library tasks
- Include other files in the file we are processing 

Built on top of those bare bones are a huge number of conventions:

- Separating your data and step definitions in different files (see file structure below)
- Creating common locations to import “SDK” files from
- Standard variables to add source files / references / resources
- Prefixing “private” tasks with underscore
- Splitting language specific items from a Common file to language specific (CSharp, FSharp, VB.net, etc) versions of the same file

None of these are enforced by MSBuild itself. The files commonly included by user projects just assume you will follow these conventions.


## File Structure Convention

Before we get into the specifics of how things work, let’s talk about the general file structure, which is built upon many standard conventions. We’ll cover the how [data is represented](#data) and the [nodes available](#XML-Structure) in each shortly. 

### Text Files

Instead of including every single detail of a build in the user projects, this information tends to be split into multiple places. Some of these are provided by SDKs are stored in standard global locations, for example: `/Library/Frameworks/Mono.framework/External/xbuild/Xamarin/` 


- Project files (csproj)
  - These are user provided files describing the files, references, and settings for the build in question 
  - These tend to contain many Reference items and additions to the Compile and None item group
  - These tend to include one single project specific “target” file that include the rest of the files needed to define the entire build

Examples:

`<Import Project="$(MSBuildBinPath)\Microsoft.CSharp.targets" />`
`<Import Project="$(MSBuildExtensionsPath)\Xamarin\Mac\Xamarin.Mac.CSharp.targets" />`

- Solution files (.sln) combine one or more projects for editing and building. They contain some build dependency and configuration information, but project files are the main level of build knowledge
- Target Files (.targets)
  - These files are generally provided by the SDKs
  - These files generally describe a set of steps (targets) which need to be executed to process the build
  - These tend to contain many UsingTask items to reference task assembly targets and Target to define the targets and dependencies
- Property file (.props)
  - These files are generally provided by the SDKs
  - These files generally set a number of properties used by the targets
  - These tend to contain many PropertyGroup and ItemGroup items defining values needed in the builds.

All of these are are [XML Structured](#XML-Structure) text files.

As I noted above, there is nothing truly special with the names and suffixes, they are just standard conventions you should follow.

### Libraries 

Many parts of the build involve invoking outside tools or operations that would be burdensome to write in XML. Task assemblies allow extension of the MSBuild by defining types in a C# library that can be called by MSBuild targets.


- Task Assemblies (.dll)
  - These files are generally provided by the SDKs
  - These are C# libraries which reference various MSBuild libraries
  - These define [tasks](#MSBuild-Tasks) which can be invoked inside defined targets to carry out parts of the builds
  - They are loaded when other MSBuild files use `UsingTask` nodes



## Data


- There is only one data type in MSBuild for values. Everything is a string
- MSBuild distinguished between two types of data, properties and items:
  - Properties are single named elements 
    - Think of accessing into a Dictionary<string, string> with the name type being the case insensitive key
    - They are defined in PropertyGroups

            <PropertyGroup>
              <DebugSymbols>true</DebugSymbols>
            </PropertyGroup>

       - This defines a property "DebugSymbols” with the value “true”
    - They are referenced later via $(NAME)
  - Items are a list of properties containing one or more elements
    - Think of accessing into a Dictionary<string, List<string>> with the name type being the key
    - They are defined in ItemGroups

            <ItemGroup>
              <Reference Include="System" />
            </ItemGroup>

      - This includes the value “System” in the Item Reference, which may already have zero or more items
    - There are also Exclude, Remove, and other actions you can do on an Item
    - They are referenced later via @(LIST_NAME) or [list transforms](#List-Transforms)
    - The may be represented by a semicolon separated string of items
- **Variables will return empty string if not defined!**
  - If you reference an property when you meant an item, you will get the undefined empty string


## Node Types

There are many, but the most common include:


- `<PropertyGroup>`
  - As seen above, children of this are set as properties in the global context
- `<ItemGroup>`
  - As seen above, children of this are processed in as Items (lists) of the name of the node
- `<Target>`
  - Defines a target which will be considered when we get to execution set
  - Before/After and BuildDependsOn will determine ordering
  - Inputs/Ouputs will determine if run at all
  - Can contain one or more children that are executed in sequence when the target is executed
- `<UsingTask>`
  - Loads a task type from the given C# library assembly for execution later by a `<Target>`
- `<Import>`
  - “Include” another file into the current process
-  `<Project>`
  - The “top level” parent of all content in a given file


## XML Structure
  - The top level of each project/target/prop file is a `<Project>` node which contains many children
  - As noted above, each specific file type tends to contain certain items. However **these are all conventions**! 
  - If the XML is invalid (unclosed tag, etc) your build may fail in strange ways
    - macios has an xml check as pat of `make MSBuild`

**Logic Constructs**

  - Many nodes can have Conditions which when false will prevent them and their children from being used in a given build
    - Conditions can even prevent entire files from being included 
    - Almost everything can have one and they are very powerful
  - There are XML constructs that mirror switch statements, recursion, and many other programming elements
  - As everything is strings, comparisions involves a number of quotes:

            <SwiftVerbose Condition="'$(SwiftVerbose)' == ''">false</SwiftVerbose>

    - Outer `"` for the contents of the condition
    - `'` around the variable itself
    - `''` for empty string
  - Remember, unset variables are valid to access and provide empty string

**List Transforms**

  - If your contents contain a `%` then you will be run like a “for each” for each element in the relevant itemgroup
  - See the [XA doc](https://github.com/xamarin/xamarin-android/blob/master/Documentation/guides/MSBuildBestPractices.md#item-group-transforms) and [MSBuild docs](https://docs.microsoft.com/en-us/visualstudio/msbuild/msbuild-transforms?view=vs-2017) for more details. It’s powerful but complex

**Property Functions**

  - It is possible to invoke C# properties and functions from MSBuild 
  - This is useful for a number of use cases, including string and path manipulation and math operations
  - See the [documentation](https://docs.microsoft.com/en-us/visualstudio/msbuild/property-functions?view=vs-2019) for more details

## MSBuild Tasks
- These are C# types defined in a library 
- The specifics of creating a new task library is outside the scope of this guide
- There are two common base classes for Tasks:
  - `Task` - A generic “I want to do some work” base class with and `Execute ()` method to override
  - `ToolTask` - A base class that makes invoking a single external tool significantly easier
- Parameters are passed via public properties defined on the task

```c#
public abstract class MySpecialTask : Task 
{
    public string MyOptionalArg { get; set; }
    
    [Required]
    public string MyRequiredArg { get; set; }
}
```

  - Properties can be marked [Required] which will fail the build if given an empty string value
  - Strings are the common type, but you can use types like bool and MSBuild will convert for you
  - ITaskItem[] are need to pass Items (lists) to / from tasks when the task may need to modify them
- They are later invoked with:

```xml
     <UsingTask TaskName="MyTasks.MySpecialTask" AssemblyFile="MyTasks.dll" />
     
    <Target Name="_MySpecialTask">
                    <MySpecialTask
                            MyOptionalArg="$(MyOptionalArg)"
                            MyRequiredArg="$(MyRequiredArg)"
                            >
                    </MySpecialTask>
    </Target>
```
- You can attach a C# debugger to the task, but that can be tricky on mono since there is no attach to process
- `SessionId` is needed on most C# tasks to enable “build remoting” for Visual Studio Windows

## Execution Model

A very inaccurate but useful way of thinking about how MSBuild processes is to split it into three parts.

- Loading
  - Starting with the csproj load each file included into a giant buffer
    - Think C preprocessor gloming files together
  - Some default values will be set in the global property dictionary
- Property Eval
  - Starting from the top, evaluate every node
    - Property and Item Group will set items in the global context
    - UsingTask will load task assemblies and prepare to invoke them
    - Targets will define then and their ordering/inputs/output
      - **Their contents will not be executed yet**
  - This will respect conditions, and be done top to bottom based upon include order
- Target Eval
  - Starting with the defined “DefaultTargets", which for our purposes is always “Build” determine a list of target to invoke and their ordering
  - Invoke one at a time, comparing their Input/Output against file timestamps like make


## "SDK style” project and nuget injection
  - SDK style projects and nugets are beyond the scope of this document 
  - One important thing to note is the “magic” injection of files
    - When referenced the props file will be “auto injected” to the top of your project and the targets injected at the very bottom. 
    - Thus you can include files for these project types you don’t explicitly see referenced in the csproj


## Debugging
  - Easily the most important thing is to get a full diagnostic build. This will list all variables set and the ordering/execution of each target
  - You can add debug “Console.WriteLine”s by using the built in MessageTask:


            <Message Text="Project File Name = $(MSBuildProjectFile)" />

    - **Do note these must be inside targets**. They are only executed during Target Eval time
    - If you put them inside ItemGroups or PropertyGroups they will be ignored
  - There is a new “binary log” hotness which acts as a full diagnostics build but is faster and contains even more information. You can enable it by passing `/bl`  to MSBuild. Read more about it [here](https://github.com/Microsoft/msbuild/blob/master/documentation/wiki/Binary-Log.md) 
    - The support for loading .binlog files in Visual Studio for Mac is still somewhat new
  - If you are developing on Windows you can add a `System.Diagnostics.Debugger.Launch()` invocation and attach Visual Studio to the process


## Gotchas
  - Visual Studio for Mac will cache your task assemblies but not your target/prop files, usually
    - This means if you make changes to a task assembly, your changes may not be reflected until you restart your IDE
    - This is why I always test using MSBuild from the command line OR write an nunit test that invokes MSBuild directly
  - Ordering matters, dearly
    - If file A defines a variable and B uses it, then A must be included before B in all cases
    - Otherwise, B will see the empty string instead of the true value of the variable
    - This is why people generally use props and targets files to keep the order right
    - You cannot safely refactor MSBuild without serious testing. 
  - Everything is a string
    - Typos will ruin your day, since you’ll get empty strings in in unexpected places
    - Make sure your quotes are correct, or you will be very confused


## Further Reading 
- [How to stop worrying and love MSBuild - Daniel Plaisted (video)](https://www.youtube.com/watch?v=CiXlVrbBepM)
- [Android best practices guide](https://github.com/xamarin/xamarin-android/blob/master/Documentation/guides/MSBuildBestPractices.md)



