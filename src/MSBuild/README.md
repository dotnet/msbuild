# Microsoft.Build.Runtime

This package delivers a complete executable copy of MSBuild. Reference this
package only if your application needs to load projects or execute in-process
builds without requiring installation of MSBuild. Successfully evaluating
projects using this package requires aggregating additional components (like the
compilers) into an application directory.

🗒️ NOTE: if you are building an application that wants to use MSBuild to
evaluate or build projects, you will generally not need this package. Instead,
use [MSBuildLocator](https://aka.ms/msbuild/locator) to use a complete toolset
provided by the .NET SDK or Visual Studio.
