// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Diagnostics;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.PhysicalFileSystem;

namespace Microsoft.TemplateEngine.Cli.PostActionProcessors
{
    internal class ProcessStartPostActionProcessor : IPostActionProcessor
    {
        internal static readonly Guid ActionProcessorId = new Guid("3A7C4B45-1F5D-4A30-959A-51B88E82B5D2");

        public Guid Id => ActionProcessorId;

        public bool Process(IEngineEnvironmentSettings environment, IPostAction actionConfig, ICreationEffects creationEffects, ICreationResult templateCreationResult, string outputBasePath)
        {
            if (!actionConfig.Args.TryGetValue("executable", out string? executable))
            {
                Reporter.Error.WriteLine(LocalizableStrings.PostAction_ProcessStartProcessor_Error_ConfigMissingExecutable);
                return false;
            }
            actionConfig.Args.TryGetValue("args", out string? args);

            bool redirectStandardOutput = true;
            // By default, standard out is redirected.
            // Only redirect when the configuration says "redirectStandardOutput = false"
            if (actionConfig.Args.TryGetValue("redirectStandardOutput", out string? redirectStandardOutputString)
                    && string.Equals(redirectStandardOutputString, "false", StringComparison.OrdinalIgnoreCase))
            {
                redirectStandardOutput = false;
            }

            try
            {
                Reporter.Output.WriteLine(string.Format(LocalizableStrings.RunningCommand, executable + " " + args));
                string resolvedExecutablePath = ResolveExecutableFilePath(environment.Host.FileSystem, executable, outputBasePath);

                Process? commandResult = System.Diagnostics.Process.Start(new ProcessStartInfo
                {
                    RedirectStandardError = true,
                    RedirectStandardOutput = redirectStandardOutput,
                    UseShellExecute = false,
                    CreateNoWindow = false,
                    WorkingDirectory = outputBasePath,
                    FileName = resolvedExecutablePath,
                    Arguments = args
                });

                if (commandResult == null)
                {
                    Reporter.Error.WriteLine(LocalizableStrings.CommandFailed);
                    Reporter.Verbose.WriteLine("Unable to start sub-process.");
                    return false;
                }

                commandResult.WaitForExit();

                if (commandResult.ExitCode != 0)
                {
                    string error = commandResult.StandardError.ReadToEnd();
                    Reporter.Error.WriteLine(LocalizableStrings.CommandFailed);
                    Reporter.Error.WriteLine(string.Format(LocalizableStrings.CommandOutput, error));
                    Reporter.Error.WriteLine(string.Empty);
                    return false;
                }
                else
                {
                    Reporter.Output.WriteLine(LocalizableStrings.CommandSucceeded);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Reporter.Error.WriteLine(LocalizableStrings.CommandFailed);
                Reporter.Error.WriteLine(string.Format(LocalizableStrings.CommandOutput, ex.Message));
                Reporter.Error.WriteLine(string.Empty);
                Reporter.Verbose.WriteLine(string.Format(LocalizableStrings.Generic_Details, ex.ToString()));
                return false;
            }
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
