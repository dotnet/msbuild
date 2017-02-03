using System;
using System.IO;
using Microsoft.DotNet.Cli.Utils;
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

        public bool Process(IPostAction actionConfig, ICreationResult templateCreationResult, string outputBasePath)
        {
            if (templateCreationResult.PrimaryOutputs.Count == 0)
            {
                Reporter.Output.WriteLine("No Primary Outputs to restore");
                return true;
            }

            bool allSucceeded = true;

            foreach (ICreationPath output in templateCreationResult.PrimaryOutputs)
            {
                string pathToRestore = Path.Combine(outputBasePath, output.Path);
                Command restoreCommand = Command.CreateDotNet("restore", new[] { pathToRestore });
                restoreCommand.CaptureStdOut();
                restoreCommand.CaptureStdErr();

                Reporter.Output.WriteLine($"Running 'dotnet restore' on {pathToRestore}");
                var commandResult = restoreCommand.Execute();
                Reporter.Output.WriteLine(commandResult.StdOut);

                if (commandResult.ExitCode != 0)
                {
                    Reporter.Output.WriteLine("restore failed:");
                    Reporter.Output.WriteLine($"StdErr: {commandResult.StdErr}");
                    Reporter.Output.WriteLine();
                    allSucceeded = false;
                }
            }

            return allSucceeded;
        }
    }
}
