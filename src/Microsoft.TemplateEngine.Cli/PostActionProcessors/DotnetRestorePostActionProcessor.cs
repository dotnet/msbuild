using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Cli.PostActionProcessors
{
    internal class DotnetRestorePostActionProcessor : PostActionProcessor2Base, IPostActionProcessor, IPostActionProcessor2
    {
        private static readonly Guid ActionProcessorId = new Guid("210D431B-A78B-4D2F-B762-4ED3E3EA9025");

        public Guid Id => ActionProcessorId;

        internal DotnetRestorePostActionProcessor()
        {
        }

        public bool Process(IEngineEnvironmentSettings environment, IPostAction actionConfig, ICreationEffects2 creationEffects, ICreationResult templateCreationResult, string outputBasePath)
        {
            bool allSucceeded = true;
            IEnumerable<string> targetFiles = null;

            if (actionConfig.Args.TryGetValue("files", out string specificFilesString))
            {
                JToken config = JToken.Parse(specificFilesString);

                if (config.Type == JTokenType.String)
                {
                    targetFiles = specificFilesString.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).SelectMany(x => GetTargetForSource(creationEffects, x));
                }
                else if (config is JArray arr)
                {
                    List<string> allFiles = new List<string>();
                    foreach (JToken child in arr)
                    {
                        if (child.Type != JTokenType.String)
                        {
                            continue;
                        }

                        allFiles.AddRange(GetTargetForSource(creationEffects, child.ToString()));
                    }

                    if (allFiles.Count > 0)
                    {
                        targetFiles = allFiles;
                    }
                }
            }
            else
            {
                //If the author didn't opt in to the new behavior by using "files", do things the old way
                return Process(environment, actionConfig, templateCreationResult, outputBasePath);
            }

            if (targetFiles is null)
            {
                environment.Host.LogMessage(string.Format(LocalizableStrings.CouldntDetermineFilesToRestore));
                return false;
            }

            foreach (string targetRelativePath in targetFiles)
            {
                string pathToRestore = !string.IsNullOrEmpty(outputBasePath) ? Path.GetFullPath(Path.Combine(outputBasePath, targetRelativePath)) : targetRelativePath;

                if (string.IsNullOrEmpty(pathToRestore) ||
                    (!Directory.Exists(pathToRestore)
                        && !Path.GetExtension(pathToRestore).EndsWith("proj", StringComparison.OrdinalIgnoreCase)
                        && !Path.GetExtension(pathToRestore).Equals(".sln", StringComparison.OrdinalIgnoreCase)
                    ))
                {
                    continue;
                }

                environment.Host.LogMessage(string.Format(LocalizableStrings.RunningDotnetRestoreOn, pathToRestore));

                // Prefer to restore the project in-proc vs. creating a new process.
                bool succeeded = false;
                if (Callbacks.RestoreProject != null)
                {
                    succeeded = Callbacks.RestoreProject(pathToRestore);
                }
                else
                {
                    Dotnet restoreCommand = Dotnet.Restore(pathToRestore).ForwardStdErr().ForwardStdOut();
                    Dotnet.Result commandResult = restoreCommand.Execute();
                    succeeded = commandResult.ExitCode == 0;
                }

                if (!succeeded)
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

                if (string.IsNullOrEmpty(pathToRestore) ||
                    (!Directory.Exists(pathToRestore)
                        && !Path.GetExtension(pathToRestore).EndsWith("proj", StringComparison.OrdinalIgnoreCase)
                        && !Path.GetExtension(pathToRestore).Equals(".sln", StringComparison.OrdinalIgnoreCase)
                    ))
                {
                    continue;
                }

                environment.Host.LogMessage(string.Format(LocalizableStrings.RunningDotnetRestoreOn, pathToRestore));

                // Prefer to restore the project in-proc vs. creating a new process.
                bool succeeded = false;
                if (Callbacks.RestoreProject != null)
                {
                    succeeded = Callbacks.RestoreProject(pathToRestore);
                }
                else
                {
                    Dotnet restoreCommand = Dotnet.Restore(pathToRestore).ForwardStdErr().ForwardStdOut();
                    Dotnet.Result commandResult = restoreCommand.Execute();
                    succeeded = commandResult.ExitCode == 0;
                }

                if (!succeeded)
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
