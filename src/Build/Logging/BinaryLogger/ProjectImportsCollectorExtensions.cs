using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Logging
{
    internal static class ProjectImportCollectorExtensions
    {
        private static string importingProject = ResourceUtilities.GetResourceString("ImportingProject");
        private static int importingProjectFilePathStart = importingProject.IndexOf('"') + 1;
        private static string importingProjectPrefix = importingProject.Substring(0, importingProjectFilePathStart);

        public static void IncludeSourceFiles(this ProjectImportsCollector projectImportsCollector, BuildEventArgs e)
        {
            if (e is ProjectStartedEventArgs)
            {
                var projectStarted = (ProjectStartedEventArgs)e;
                projectImportsCollector.AddFile(projectStarted.ProjectFile);
            }
            else if (e is BuildMessageEventArgs)
            {
                var messageArgs = (BuildMessageEventArgs)e;
                var message = messageArgs.Message;
                if (message.Length > importingProjectFilePathStart && message.StartsWith(importingProjectPrefix, StringComparison.Ordinal))
                {
                    var secondQuote = message.IndexOf('"', importingProjectFilePathStart);
                    if (secondQuote > importingProjectFilePathStart)
                    {
                        var filePath = message.Substring(importingProjectFilePathStart, secondQuote - importingProjectFilePathStart);
                        projectImportsCollector.AddFile(filePath);
                    }
                }
            }
        }
    }
}
