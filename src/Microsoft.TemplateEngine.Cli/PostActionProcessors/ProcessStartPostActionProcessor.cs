using System;
using System.Diagnostics;
using System.IO;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Cli.PostActionProcessors
{
    public class ProcessStartPostActionProcessor : IPostActionProcessor
    {
        private static readonly Guid ActionProcessorId = new Guid("3A7C4B45-1F5D-4A30-959A-51B88E82B5D2");

        public Guid Id => ActionProcessorId;

        public bool Process(IEngineEnvironmentSettings settings, IPostAction actionConfig, ICreationResult templateCreationResult, string outputBasePath)
        {
            bool allSucceeded = true;

            settings.Host.LogMessage(string.Format(LocalizableStrings.RunningCommand, actionConfig.Args["executable"] + " " + actionConfig.Args["args"]));
            System.Diagnostics.Process commandResult = System.Diagnostics.Process.Start(new ProcessStartInfo
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = false,
                WorkingDirectory = outputBasePath,
                FileName = actionConfig.Args["executable"],
                Arguments = actionConfig.Args["args"]
            });

            commandResult.WaitForExit();

            if (commandResult.ExitCode != 0)
            {
                string error = commandResult.StandardError.ReadToEnd();
                settings.Host.LogMessage(LocalizableStrings.CommandFailed);
                settings.Host.LogMessage(string.Format(LocalizableStrings.CommandOutput, error));
                settings.Host.LogMessage(string.Empty);
                allSucceeded = false;
            }
            else
            {
                settings.Host.LogMessage(LocalizableStrings.CommandSucceeded);
            }

            return allSucceeded;
        }
    }
}
