using System;
using System.Diagnostics;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Cli.PostActionProcessors
{
    public class ProcessStartPostActionProcessor : IPostActionProcessor
    {
        public static readonly Guid ActionProcessorId = PostActionInfo.ProcessStartPostActionProcessorId;

        public Guid Id => ActionProcessorId;

        public bool Process(IEngineEnvironmentSettings settings, IPostAction actionConfig, ICreationResult templateCreationResult, string outputBasePath)
        {
            bool allSucceeded = true;
            actionConfig.Args.TryGetValue("args", out string args);

            settings.Host.LogMessage(string.Format(LocalizableStrings.RunningCommand, actionConfig.Args["executable"] + " " + args));
            System.Diagnostics.Process commandResult = System.Diagnostics.Process.Start(new ProcessStartInfo
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = false,
                WorkingDirectory = outputBasePath,
                FileName = actionConfig.Args["executable"],
                Arguments = args
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
