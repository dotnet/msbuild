# How to investigate rebuilding when nothing has changed

There is a class of problems with build where when you build twice, it still rebuilds fully the second time even though nothing has changed. This is called build incrementality issues. They can happen in MSBuild or in Visual Studio (in which case the VS project system's up-to-date-check decides to rebuild the project).

There are multiple tools to investigate and fix broken incrementality. Start with the blog posts below.

 * [How to investigate Rebuilding in Visual Studio when nothing has changed](https://learn.microsoft.com/archive/blogs/kirillosenkov/how-to-investigate-rebuilding-in-visual-studio-when-nothing-has-changed)
 * [MSBuild: unnecessary rebuilds because of generated AssemblyAttributes.cs](https://learn.microsoft.com/archive/blogs/kirillosenkov/msbuild-unnecessary-rebuilds-because-of-generated-assemblyattributes-cs)
 * [When Visual Studio keeps rebuilding Projects that have not changed](https://web.archive.org/web/20120321204616/http://www.andreas-reiff.de/2012/02/when-visual-studio-keeps-rebuilding-projects-that-have-not-changed/)
 * [How to build incrementally](https://learn.microsoft.com/visualstudio/msbuild/how-to-build-incrementally)
 * [Incremental builds](https://learn.microsoft.com/visualstudio/msbuild/incremental-builds)

Strings to search for in the build logs:
 * `Building target "CoreCompile" completely`
 * `is newer than output`
 * `out-of-date`
 * `missing`

Consider using https://msbuildlog.com to help with searching through the build log.