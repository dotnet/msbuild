# How to investigate rebuilding when nothing has changed

There is a class of problems with build where when you build twice, it still rebuilds fully the second time even though nothing has changed. This is called build incrementality issues. They can happen in MSBuild or in Visual Studio (in which case the VS project system's up-to-date-check decides to rebuild the project).

There are multiple tools to investigate and fix broken incrementality. Start with the blog posts below.

 * [https://blogs.msdn.microsoft.com/kirillosenkov/2014/08/04/how-to-investigate-rebuilding-in-visual-studio-when-nothing-has-changed/](https://blogs.msdn.microsoft.com/kirillosenkov/2014/08/04/how-to-investigate-rebuilding-in-visual-studio-when-nothing-has-changed/)
 * [http://www.andreas-reiff.de/2012/02/when-visual-studio-keeps-rebuilding-projects-that-have-not-changed/](http://www.andreas-reiff.de/2012/02/when-visual-studio-keeps-rebuilding-projects-that-have-not-changed/)
