// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.PhysicalFileSystem;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Cli.PostActionProcessors
{
    internal class AddReferencePostActionProcessor : PostActionProcessor2Base, IPostActionProcessor, IPostActionProcessor2
    {
        internal static readonly Guid ActionProcessorId = new Guid("B17581D1-C5C9-4489-8F0A-004BE667B814");

        public Guid Id => ActionProcessorId;

        public bool Process(IEngineEnvironmentSettings environment, IPostAction action, ICreationEffects2 creationEffects, ICreationResult templateCreationResult, string outputBasePath)
        {
            IReadOnlyList<string> allTargets = null;
            if (action.Args.TryGetValue("targetFiles", out string singleTarget) && singleTarget != null)
            {
                JToken config = JToken.Parse(singleTarget);

                if (config.Type == JTokenType.String)
                {
                    allTargets = singleTarget.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                }
                else if (config is JArray arr)
                {
                    List<string> parts = new List<string>();

                    foreach (JToken token in arr)
                    {
                        if (token.Type != JTokenType.String)
                        {
                            continue;
                        }

                        parts.Add(token.ToString());
                    }

                    if (parts.Count > 0)
                    {
                        allTargets = parts;
                    }
                }
            }
            else
            {
                //If the author didn't opt in to the new behavior by using "targetFiles", do things the old way
                return Process(environment, action, templateCreationResult, outputBasePath);
            }

            if (allTargets is null)
            {
                return Process(environment, action, creationEffects.CreationResult, outputBasePath);
            }

            bool success = true;
            foreach (string target in allTargets)
            {
                success &= AddReference(environment, action, GetTargetForSource(creationEffects, target));

                if (!success)
                {
                    return false;
                }
            }

            return true;
        }

        public bool Process(IEngineEnvironmentSettings environment, IPostAction actionConfig, ICreationResult templateCreationResult, string outputBasePath)
        {
            if (string.IsNullOrEmpty(outputBasePath))
            {
                Reporter.Error.WriteLine(LocalizableStrings.AddRefPostActionUnresolvedProjFile);
            }

            HashSet<string> extensionLimiters = new HashSet<string>(StringComparer.Ordinal);
            if (actionConfig.Args.TryGetValue("projectFileExtensions", out string projectFileExtensions))
            {
                if (projectFileExtensions.Contains("/") || projectFileExtensions.Contains("\\") || projectFileExtensions.Contains("*"))
                {
                    // these must be literals
                    Reporter.Error.WriteLine(LocalizableStrings.AddRefPostActionMisconfigured);
                    return false;
                }

                extensionLimiters.UnionWith(projectFileExtensions.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));
            }

            IReadOnlyList<string> nearestProjectFilesFound = FindProjFileAtOrAbovePath(environment.Host.FileSystem, outputBasePath, extensionLimiters);
            return AddReference(environment, actionConfig, nearestProjectFilesFound);
        }

        internal IReadOnlyList<string> FindProjFileAtOrAbovePath(IPhysicalFileSystem fileSystem, string startPath, HashSet<string> extensionLimiters)
        {
            if (extensionLimiters.Count == 0)
            {
                return FileFindHelpers.FindFilesAtOrAbovePath(fileSystem, startPath, "*.*proj");
            }
            else
            {
                return FileFindHelpers.FindFilesAtOrAbovePath(fileSystem, startPath, "*.*proj", (filename) => extensionLimiters.Contains(Path.GetExtension(filename)));
            }
        }

        private bool AddReference(IEngineEnvironmentSettings environment, IPostAction actionConfig, IReadOnlyList<string> nearestProjectFilesFound)
        {
            if (actionConfig.Args == null || !actionConfig.Args.TryGetValue("reference", out string referenceToAdd))
            {
                Reporter.Error.WriteLine(LocalizableStrings.AddRefPostActionMisconfigured);
                return false;
            }

            if (!actionConfig.Args.TryGetValue("referenceType", out string referenceType))
            {
                Reporter.Error.WriteLine(LocalizableStrings.AddRefPostActionMisconfigured);
                return false;
            }

            if (nearestProjectFilesFound.Count == 1)
            {
                string projectFile = nearestProjectFilesFound[0];
                Dotnet.Result commandResult;

                if (string.Equals(referenceType, "project", StringComparison.OrdinalIgnoreCase))
                {
                    // actually do the add ref
                    Dotnet addReferenceCommand = Dotnet.AddProjectToProjectReference(projectFile, referenceToAdd);
                    addReferenceCommand.CaptureStdOut();
                    addReferenceCommand.CaptureStdErr();
                    Reporter.Output.WriteLine(string.Format(LocalizableStrings.AddRefPostActionAddProjectRef, projectFile, referenceToAdd));
                    commandResult = addReferenceCommand.Execute();
                }
                else if (string.Equals(referenceType, "package", StringComparison.OrdinalIgnoreCase))
                {
                    actionConfig.Args.TryGetValue("version", out string version);

                    Dotnet addReferenceCommand = Dotnet.AddPackageReference(projectFile, referenceToAdd, version);
                    addReferenceCommand.CaptureStdOut();
                    addReferenceCommand.CaptureStdErr();
                    if (string.IsNullOrEmpty(version))
                    {
                        Reporter.Output.WriteLine(string.Format(LocalizableStrings.AddRefPostActionAddPackageRef, projectFile, referenceToAdd));
                    }
                    else
                    {
                        Reporter.Output.WriteLine(string.Format(LocalizableStrings.AddRefPostActionAddPackageRefWithVersion, projectFile, referenceToAdd, version));
                    }
                    commandResult = addReferenceCommand.Execute();
                }
                else if (string.Equals(referenceType, "framework", StringComparison.OrdinalIgnoreCase))
                {
                    Reporter.Error.WriteLine(string.Format(LocalizableStrings.AddRefPostActionFrameworkNotSupported, referenceToAdd));
                    return false;
                }
                else
                {
                    Reporter.Error.WriteLine(string.Format(LocalizableStrings.AddRefPostActionUnsupportedRefType, referenceType));
                    return false;
                }

                if (commandResult.ExitCode != 0)
                {
                    Reporter.Error.WriteLine(string.Format(LocalizableStrings.AddRefPostActionFailed, referenceToAdd, projectFile));
                    Reporter.Error.WriteLine(string.Format(LocalizableStrings.CommandOutput, commandResult.StdOut + Environment.NewLine + Environment.NewLine + commandResult.StdErr));
                    Reporter.Error.WriteLine(string.Empty);
                    return false;
                }
                else
                {
                    Reporter.Output.WriteLine(string.Format(LocalizableStrings.AddRefPostActionSucceeded, referenceToAdd, projectFile));
                    return true;
                }
            }
            else if (nearestProjectFilesFound.Count == 0)
            {
                // no projects found. Error.
                Reporter.Error.WriteLine(LocalizableStrings.AddRefPostActionUnresolvedProjFile);
                return false;
            }
            else
            {
                // multiple projects at the same level. Error.
                Reporter.Error.WriteLine(LocalizableStrings.AddRefPostActionUnresolvedProjFile);
                Reporter.Error.WriteLine(LocalizableStrings.AddRefPostActionProjFileListHeader);
                foreach (string projectFile in nearestProjectFilesFound)
                {
                    Reporter.Error.WriteLine(string.Format("\t{0}", projectFile));
                }

                return false;
            }
        }
    }
}
