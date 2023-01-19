# General Resources
 * [MSBuild Concepts](https://learn.microsoft.com/visualstudio/msbuild/msbuild-concepts)
 * [MSBuild Reserved and Well-Known Properties](https://learn.microsoft.com/visualstudio/msbuild/msbuild-reserved-and-well-known-properties)
 * [MSBuild Tips & Tricks](MSBuild-Tips-&-Tricks.md)
 * [Target Maps](Target-Maps.md)

# MSBuild Source Code
 * [https://github.com/dotnet/msbuild](https://github.com/dotnet/msbuild)
 * [https://source.dot.net](https://source.dot.net)
 * Use [referencesource.microsoft.com](https://referencesource.microsoft.com) or [sourceroslyn.io/](https://sourceroslyn.io/) to browse Microsoft MSBuild targets. Examples:
   * search for "[_FindDependencies MSBuildProperty](https://referencesource.microsoft.com/#q=_FindDependencies%20MSBuildProperty)"
   * find targets [referencesource.microsoft.com/#MSBuildTarget=ResolveAssemblyReferences](https://referencesource.microsoft.com/#MSBuildTarget=ResolveAssemblyReferences)

# Tools
**Note:** These are third party tools
 * [MSBuildStructuredLog](https://msbuildlog.com/)
   * A log viewer that displays a structured representation of executed targets, tasks, property and item values.
 * [MSBuildExtensionPack](https://github.com/mikefourie-zz/MSBuildExtensionPack) (also via [NuGet](https://www.nuget.org/packages/MSBuild.Extension.Pack))
   * Provides a large collection of MSBuild Tasks, MSBuild Loggers and MSBuild TaskFactories.
 * [MSBuilder](https://github.com/MobileEssentials/MSBuilder)
   * Reusable blocks of MSBuild helpers; MSBuilder's goal is to provide fine-grained nuget packages that can be installed when only a certain MSBuild extension (task, property, target) is needed.
 * [MSBuildExplorer](https://github.com/mikefourie/MSBuildExplorer)
   * Use MSBuild Explorer to help you find your way around the make-up of your build file(s).
 * [MSBuild Sidekick](http://attrice.info/msbuild)
   * MSBuild Sidekick allows you to view, edit, build and debug Visual Studio projects and solution files as well as custom MSBuild projects.
 * [MSBuildDumper](https://github.com/KirillOsenkov/MSBuildTools)
     * Very quick tool to dump properties and items of a project without building it.
     * Install from Chocolatey `cinst MSBuildDumper`.
 * [MSBuild Profiler](https://msbuildprofiler.codeplex.com/)
   * A performance measurement tool for MSBuild scripts. MSBuild Profiler shows a graphical performance output for all your MSBuild scripts.
 * [MsBuildPipeLogger](https://msbuildpipelogger.netlify.com/) ([GitHub](https://github.com/daveaglick/MsBuildPipeLogger))
   * A logger for MSBuild that sends event data over anonymous or named pipes.
 * [MSBuild Shell Extension](https://msbuildshellex.codeplex.com/) Note: Not supported on Windows 10.
   * Lets you build Visual Studio solutions and projects as well as any MSBuild file through a context menu without opening Visual Studio.

# Books
 * [Inside the Microsoft Build Engine: Using MSBuild and Team Foundation Build (2nd Edition) by Sayed Hashimi, William Bartholomew](https://www.amazon.com/Inside-Microsoft-Build-Engine-Foundation/dp/0735645248)
 * [MSBuild Trickery: 99 Ways to Bend the Build Engine to Your Will, by Brian Kretzler](https://www.amazon.com/MSBuild-Trickery-Ways-Build-Engine/dp/061550907X)

# Blogs
 * [MSBuild Team Blog](https://learn.microsoft.com/archive/blogs/msbuild/) (archive)
 * [Sayed Hashimi's blog at sedodream.com](http://sedodream.com)
 * [Mike Fourie's blog https://mikefourie.wordpress.com](https://mikefourie.wordpress.com)

# MSBuild Assemblies
![MSBuild Assemblies](https://raw.githubusercontent.com/KirillOsenkov/MSBuildStructuredLog/main/docs/MSBuildAssemblies.png)
