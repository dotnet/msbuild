// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Diagnostics.CodeAnalysis;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.PhysicalFileSystem;
using Microsoft.TemplateEngine.Utils;
using Microsoft.TemplateEngine.Cli.PostActionProcessors;

namespace Microsoft.DotNet.Tools.New.PostActionProcessors
{
    internal class DotnetSlnPostActionProcessor : PostActionProcessorBase
    {
        private readonly Func<string, IReadOnlyList<string>, string?, bool?, bool> _addProjToSolutionCallback;

        public DotnetSlnPostActionProcessor(Func<string, IReadOnlyList<string>, string?, bool?, bool>? addProjToSolutionCallback = null)
        {
            _addProjToSolutionCallback = addProjToSolutionCallback ?? DotnetCommandCallbacks.AddProjectsToSolution;
        }

        public override Guid Id => ActionProcessorId;

        internal static Guid ActionProcessorId { get; } = new Guid("D396686C-DE0E-4DE6-906D-291CD29FC5DE");

        internal static IReadOnlyList<string> FindSolutionFilesAtOrAbovePath(IPhysicalFileSystem fileSystem, string outputBasePath)
        {
            return FileFindHelpers.FindFilesAtOrAbovePath(fileSystem, outputBasePath, "*.sln");
        }

        // The project files to add are a subset of the primary outputs, specifically the primary outputs indicated by the primaryOutputIndexes post action argument (semicolon separated)
        // If any indexes are out of range or non-numeric, this method returns false and projectFiles is set to null.
        internal static bool TryGetProjectFilesToAdd(IPostAction actionConfig, ICreationResult templateCreationResult, string outputBasePath, [NotNullWhen(true)] out IReadOnlyList<string>? projectFiles)
        {
            List<string> filesToAdd = new();

            if ((actionConfig.Args != null) && actionConfig.Args.TryGetValue("primaryOutputIndexes", out string? projectIndexes))
            {
                foreach (string indexString in projectIndexes.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (int.TryParse(indexString.Trim(), out int index))
                    {
                        if (templateCreationResult.PrimaryOutputs.Count <= index || index < 0)
                        {
                            projectFiles = null;
                            return false;
                        }

                        filesToAdd.Add(Path.GetFullPath(templateCreationResult.PrimaryOutputs[index].Path, outputBasePath));
                    }
                    else
                    {
                        projectFiles = null;
                        return false;
                    }
                }

                projectFiles = filesToAdd;
                return true;
            }
            else
            {
                foreach (string pathString in templateCreationResult.PrimaryOutputs.Select(x => x.Path))
                {
                    filesToAdd.Add(Path.GetFullPath(pathString, outputBasePath));
                }

                projectFiles = filesToAdd;
                return true;
            }
        }

        protected override bool ProcessInternal(IEngineEnvironmentSettings environment, IPostAction action, ICreationEffects creationEffects, ICreationResult templateCreationResult, string outputBasePath)
        {
            IReadOnlyList<string> nearestSlnFilesFound = FindSolutionFilesAtOrAbovePath(environment.Host.FileSystem, outputBasePath);
            if (nearestSlnFilesFound.Count != 1)
            {
                Reporter.Error.WriteLine(LocalizableStrings.PostAction_AddProjToSln_Error_NoSolutionFile);
                return false;
            }

            IReadOnlyList<string>? projectFiles = GetConfiguredFiles(action.Args, creationEffects, "projectFiles", outputBasePath, (path) => Path.GetExtension(path).EndsWith("proj", StringComparison.OrdinalIgnoreCase));
            if (projectFiles is null)
            {
                //If the author didn't opt in to the new behavior by specifying "projectFiles", use the old behavior
                if (!TryGetProjectFilesToAdd(action, templateCreationResult, outputBasePath, out projectFiles))
                {
                    Reporter.Error.WriteLine(LocalizableStrings.PostAction_AddProjToSln_Error_NoProjectsToAdd);
                    return false;
                }
            }
            if (projectFiles.Count == 0)
            {
                Reporter.Error.WriteLine(LocalizableStrings.PostAction_AddProjToSln_Error_NoProjectsToAdd);
                return false;
            }

            string? solutionFolder = GetSolutionFolder(action);
            bool? inRoot = GetInRoot(action);

            if (!string.IsNullOrWhiteSpace(solutionFolder) && inRoot is true)
            {
                Reporter.Error.WriteLine(LocalizableStrings.PostAction_AddProjToSln_Error_BothInRootAndSolutionFolderSpecified);
                return false;
            }

            if (inRoot is true)
            {
                Reporter.Output.WriteLine(string.Format(LocalizableStrings.PostAction_AddProjToSln_InRoot_Running, string.Join(" ", projectFiles), nearestSlnFilesFound[0]));
            }
            else
            {
                Reporter.Output.WriteLine(string.Format(LocalizableStrings.PostAction_AddProjToSln_Running, string.Join(" ", projectFiles), nearestSlnFilesFound[0], solutionFolder));
            }
            return AddProjectsToSolution(nearestSlnFilesFound[0], projectFiles, solutionFolder, inRoot);
        }

        private bool AddProjectsToSolution(string solutionPath, IReadOnlyList<string> projectsToAdd, string? solutionFolder, bool? inRoot)
        {
            try
            {
                bool succeeded = _addProjToSolutionCallback(solutionPath, projectsToAdd, solutionFolder, inRoot);
                if (!succeeded)
                {
                    Reporter.Error.WriteLine(LocalizableStrings.PostAction_AddProjToSln_Failed_NoReason);
                }
                else
                {
                    Reporter.Output.WriteLine(LocalizableStrings.PostAction_AddProjToSln_Succeeded);
                }
                return succeeded;

            }
            catch (Exception e)
            {
                Reporter.Error.WriteLine(string.Format(LocalizableStrings.PostAction_AddProjToSln_Failed, e.Message));
                return false;
            }
        }

        private static string? GetSolutionFolder(IPostAction actionConfig)
        {
            if (actionConfig.Args != null && actionConfig.Args.TryGetValue("solutionFolder", out string? solutionFolder))
            {
                return solutionFolder;
            }
            return null;
        }

        private static bool? GetInRoot(IPostAction actionConfig)
        {
            if (actionConfig.Args != null && actionConfig.Args.TryGetValue("inRoot", out string? inRoot))
            {
                if (bool.TryParse(inRoot, out bool result))
                {
                    return result;
                }
            }
            return null;
        }
    }
}
