### Microsoft.Build.Framework
It you have looked carefully, you might notice some odd behavior around this assembly (Microsoft.Build.Framework). We released the source here, but in some cases if you use our `BuildAndCopy.cmd` script, you will reference the one one your machine instead of the one you just built! Here's why.

Microsoft.Build.Framework contains the types and interfaces for extensibility in MSBuild. If you're ever written a custom Task, you might recognize them as ITask, ITaskItem, etc. After you build your Task, let's say targeting `Microsoft.Build.Framework, Version=12.0.0.0, PublicKeyToken=b03f5f7f11d50a3a` (Visual Studio 2013), anyone with MSBuild 12.0 or later can use your Task. In later versions of MSBuild, say version 14.0, we will use a [binding redirect](https://msdn.microsoft.com/en-us/library/eftw1fys(v=vs.110).aspx) to point you to the newer version of Microsoft.Build.Framework. Assuming we did our jobs right with compatibility, your Task should run without ever knowing the difference. The crucial point of detail here is that the public key token for the Framework assembly **did not change** between version. If it does, binding redirection is not allowed.

##Option 1 - Project Reference
By default this is enabled. This means that all MSBuild code will reference Microsoft.Build.Framework as a project reference and therefor will not have the same public key token as the retail version.

| Pros  | Cons  |
|:-:|:-:|
| You can customize/change Microsoft.Build.Framework as much as you want. Change the types, base implementation, interfaces, it's up to you. | You can not build anything that uses a custom task **unless** that custom task references your Microsoft.Build.Framework (or more precisely, one with the same public key token) |

##Option 2 - Retail Assembly Reference
If you set the `TargetRetailBuildFramework` property to `true`, this behavior will occur. You are now referencing the public retail assembly version 14.0.0.0 (Visual Studio 2015).

| Pros  | Cons  |
|:-:|:-:|
| You can build projects that use custom tasks. | You cannot make any changes to the Framework project. If you do, they won't be used. |

To make this a little bit easier, use: 
```
BuildAndCopy.cmd <path> true
```
This will set the property for you and create a drop of MSBuild and dependencies needed to build other project.
