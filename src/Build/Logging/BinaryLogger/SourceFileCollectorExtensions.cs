using Microsoft.Build.Framework;

namespace Microsoft.Build.Logging
{
    // This functionality is separate from SourceFileCollector to avoid having SourceFileCollector
    // depend on MSBuild. It seems like a more standalone and useful utility and it feels better to
    // have the population from BuildEventArgs in a separate file.
    internal static class SourceFileCollectorExtensions
    {
        public static void IncludeSourceFiles(this SourceFileCollector sourceFileCollector, BuildEventArgs e)
        {
            if (e is TaskStartedEventArgs)
            {
                var taskArgs = (TaskStartedEventArgs)e;
                sourceFileCollector.AddFile(taskArgs.TaskFile);
            }
            else if (e is TargetStartedEventArgs)
            {
                var targetArgs = (TargetStartedEventArgs)e;
                sourceFileCollector.AddFile(targetArgs.TargetFile);
            }
            else if (e is BuildErrorEventArgs)
            {
                var buildError = (BuildErrorEventArgs)e;
                sourceFileCollector.AddFile(buildError.File);
            }
            else if (e is BuildWarningEventArgs)
            {
                var buildWarning = (BuildWarningEventArgs)e;
                sourceFileCollector.AddFile(buildWarning.File);
            }
            else if (e is ProjectStartedEventArgs)
            {
                var projectStarted = (ProjectStartedEventArgs)e;
                sourceFileCollector.AddFile(projectStarted.ProjectFile);
            }
        }
    }
}
