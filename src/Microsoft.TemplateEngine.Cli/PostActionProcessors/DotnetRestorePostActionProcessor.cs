using System;
using System.IO;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli.PostActionProcessors
{
    public class DotnetRestorePostActionProcessor : IPostActionProcessor
    {
        private static readonly Guid ActionProcessorId = new Guid("210D431B-A78B-4D2F-B762-4ED3E3EA9025");

        public Guid Id => ActionProcessorId;

        public DotnetRestorePostActionProcessor()
        {
        }

        public bool Process(IEngineEnvironmentSettings environment, IPostAction actionConfig, ICreationResult templateCreationResult, string outputBasePath)
        {
            if (templateCreationResult.PrimaryOutputs.Count == 0)
            {
                environment.Host.LogMessage(LocalizableStrings.NoPrimaryOutputsToRestore);
                return true;
            }

            bool allSucceeded = true;

            foreach (ICreationPath output in templateCreationResult.PrimaryOutputs)
            {
                string pathToRestore = !string.IsNullOrEmpty(outputBasePath) ? Path.Combine(outputBasePath, output.Path) : output.Path;

                if (string.IsNullOrEmpty(pathToRestore) || (!Directory.Exists(pathToRestore) && !Path.GetExtension(pathToRestore).EndsWith("proj", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                environment.Host.LogMessage(string.Format(LocalizableStrings.RunningDotnetRestoreOn, pathToRestore));
                Dotnet restoreCommand = Dotnet.Restore(pathToRestore).ForwardStdErr().ForwardStdOut();
                Dotnet.Result commandResult = restoreCommand.Execute();

                if (commandResult.ExitCode != 0)
                {
                    environment.Host.LogMessage(LocalizableStrings.RestoreFailed);
                    allSucceeded = false;
                }
                else
                {
                    environment.Host.LogMessage(LocalizableStrings.RestoreSucceeded);
                }
            }

            return allSucceeded;
        }
    }
}
