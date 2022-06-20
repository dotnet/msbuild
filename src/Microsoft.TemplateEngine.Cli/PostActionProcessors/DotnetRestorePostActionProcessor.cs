// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Cli.PostActionProcessors
{
    internal class DotnetRestorePostActionProcessor : PostActionProcessor2Base, IPostActionProcessor
    {
        private static readonly Guid ActionProcessorId = new Guid("210D431B-A78B-4D2F-B762-4ED3E3EA9025");

        public Guid Id => ActionProcessorId;

        public bool Process(IEngineEnvironmentSettings environment, IPostAction actionConfig, ICreationEffects creationEffects, ICreationResult templateCreationResult, string outputBasePath)
        {
            if (string.IsNullOrWhiteSpace(outputBasePath))
            {
                throw new ArgumentException($"'{nameof(outputBasePath)}' cannot be null or whitespace.", nameof(outputBasePath));
            }
            outputBasePath = Path.GetFullPath(outputBasePath);

            bool allSucceeded = true;
            IEnumerable<string>? targetFiles = null;

            if (actionConfig.Args.TryGetValue("files", out string? specificFilesString) && creationEffects is ICreationEffects2 creationEffects2)
            {
                JToken config = JToken.Parse(specificFilesString);

                if (config.Type == JTokenType.String)
                {
                    targetFiles = specificFilesString.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).SelectMany(x => GetTargetForSource(creationEffects2, x, outputBasePath));
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

                        allFiles.AddRange(GetTargetForSource(creationEffects2, child.ToString(), outputBasePath));
                    }

                    if (allFiles.Count > 0)
                    {
                        targetFiles = allFiles;
                    }
                }
            }
            else
            {
                if (templateCreationResult.PrimaryOutputs.Count == 0)
                {
                    Reporter.Output.WriteLine(LocalizableStrings.NoPrimaryOutputsToRestore);
                    return true;
                }
                targetFiles = templateCreationResult.PrimaryOutputs.Select(output => NormalizePath(outputBasePath, output.Path));
            }

            if (targetFiles is null || !targetFiles.Any())
            {
                Reporter.Error.WriteLine(string.Format(LocalizableStrings.CouldntDetermineFilesToRestore));
                return false;
            }

            foreach (string pathToRestore in targetFiles)
            {
                //do not check for file existance. The restore will fail in case file doesn't exist.
                Reporter.Output.WriteLine(string.Format(LocalizableStrings.RunningDotnetRestoreOn, pathToRestore));

                // Prefer to restore the project in-proc vs. creating a new process.
                bool succeeded = false;
                if (Callbacks?.RestoreProject != null)
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
                    Reporter.Error.WriteLine(LocalizableStrings.RestoreFailed);
                    allSucceeded = false;
                }
                else
                {
                    Reporter.Output.WriteLine(LocalizableStrings.RestoreSucceeded);
                }
            }
            return allSucceeded;
        }
    }
}
