using System;
using System.Diagnostics;
using System.IO;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.PhysicalFileSystem;
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

            bool redirectStandardOutput = true;

            // By default, standard out is redirected.
            // Only redirect when the configuration says "redirectStandardOutput = false"
            if (actionConfig.Args.TryGetValue("redirectStandardOutput", out string redirectStandardOutputString)
                    && string.Equals(redirectStandardOutputString, "false", StringComparison.OrdinalIgnoreCase))
            {
                redirectStandardOutput = false;
            }

            settings.Host.LogMessage(string.Format(LocalizableStrings.RunningCommand, actionConfig.Args["executable"] + " " + args));

            string resolvedExecutablePath = ResolveExecutableFilePath(settings.Host.FileSystem, actionConfig.Args["executable"], outputBasePath);

            System.Diagnostics.Process commandResult = System.Diagnostics.Process.Start(new ProcessStartInfo
            {
                RedirectStandardError = true,
                RedirectStandardOutput = redirectStandardOutput,
                UseShellExecute = false,
                CreateNoWindow = false,
                WorkingDirectory = outputBasePath,
                FileName = resolvedExecutablePath,
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

        private static string ResolveExecutableFilePath(IPhysicalFileSystem fileSystem, string executableFileName, string outputBasePath)
        {
            if (!string.IsNullOrEmpty(outputBasePath) && fileSystem.DirectoryExists(outputBasePath))
            {
                string executableCombinedFileName = Path.Combine(Path.GetFullPath(outputBasePath), executableFileName);
                if (fileSystem.FileExists(executableCombinedFileName))
                {
                    return executableCombinedFileName;
                }
            }

            // The executable has not been found in the template folder, thus do not use the full path to the file.
            // The executable will be further searched in the directories from the PATH environment variable.
            return executableFileName;
        }
    }
}
