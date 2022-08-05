// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.TemplateEngine.Abstractions;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Cli.PostActionProcessors
{
    internal class DotnetRestorePostActionProcessor : PostActionProcessor2Base
    {
        private static readonly Guid ActionProcessorId = new Guid("210D431B-A78B-4D2F-B762-4ED3E3EA9025");

        public override Guid Id => ActionProcessorId;

        protected override bool ProcessInternal(IEngineEnvironmentSettings environment, IPostAction actionConfig, ICreationEffects creationEffects, ICreationResult templateCreationResult, string outputBasePath)
        {
            bool allSucceeded = true;
            IEnumerable<string>? targetFiles = GetConfiguredFiles(actionConfig.Args, creationEffects, "files", outputBasePath);

            if (targetFiles is null || !targetFiles.Any())
            {
                //If the author didn't opt in to the new behavior by specifying "projectFiles", use the old behavior - primary outputs
                if (templateCreationResult.PrimaryOutputs.Count == 0)
                {
                    Reporter.Output.WriteLine(LocalizableStrings.PostAction_Restore_Error_NoProjectsToRestore);
                    return true;
                }
                targetFiles = templateCreationResult.PrimaryOutputs.Select(output => Path.GetFullPath(output.Path, outputBasePath));
            }

            if (targetFiles is null || !targetFiles.Any())
            {
                Reporter.Error.WriteLine(string.Format(LocalizableStrings.PostAction_Restore_Error_FailedToDetermineProjectToRestore));
                return false;
            }

            foreach (string pathToRestore in targetFiles)
            {
                //do not check for file existance. The restore will fail in case file doesn't exist.
                Reporter.Output.WriteLine(string.Format(LocalizableStrings.PostAction_Restore_Running, pathToRestore));

                // Prefer to restore the project in-proc vs. creating a new process.
                bool succeeded = false;
                if (Callbacks?.RestoreProject != null)
                {
                    succeeded = Callbacks.RestoreProject(pathToRestore);
                }

                if (!succeeded)
                {
                    Reporter.Error.WriteLine(LocalizableStrings.PostAction_Restore_Failed);
                    if (Callbacks?.RestoreProject == null)
                    {
                        Reporter.Error.WriteLine(LocalizableStrings.Generic_NoCallbackError);
                    }
                    allSucceeded = false;
                }
                else
                {
                    Reporter.Output.WriteLine(LocalizableStrings.PostAction_Restore_Succeeded);
                }
            }
            return allSucceeded;
        }
    }
}
