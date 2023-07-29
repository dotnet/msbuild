// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.DotNet.Cli.Utils;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.PhysicalFileSystem;
using Microsoft.TemplateEngine.Cli.PostActionProcessors;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.DotNet.Tools.New.PostActionProcessors
{
    internal class DotnetAddPostActionProcessor : PostActionProcessorBase
    {
        private readonly Func<string, string, string?, bool> _addPackageReferenceCallback;
        private readonly Func<string, string, bool> _addProjectReferenceCallback;

        public DotnetAddPostActionProcessor(
            Func<string, string, string?, bool>? addPackageReferenceCallback = null,
            Func<string, string, bool>? addProjectReferenceCallback = null)
        {
            _addPackageReferenceCallback = addPackageReferenceCallback ?? DotnetCommandCallbacks.AddPackageReference;
            _addProjectReferenceCallback = addProjectReferenceCallback ?? DotnetCommandCallbacks.AddProjectReference;
        }

        public override Guid Id => ActionProcessorId;

        internal static Guid ActionProcessorId { get; } = new("B17581D1-C5C9-4489-8F0A-004BE667B814");

        internal static IReadOnlyList<string> FindProjFileAtOrAbovePath(IPhysicalFileSystem fileSystem, string startPath, HashSet<string> extensionLimiters)
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

        protected override bool ProcessInternal(IEngineEnvironmentSettings environment, IPostAction action, ICreationEffects creationEffects, ICreationResult templateCreationResult, string outputBasePath)
        {
            IReadOnlyList<string>? projectsToProcess = GetConfiguredFiles(action.Args, creationEffects, "targetFiles", outputBasePath);

            if (projectsToProcess is null)
            {
                //If the author didn't opt in to the new behavior by specifying "targetFiles", search for project file in current output directory or above.
                HashSet<string> extensionLimiters = new(StringComparer.Ordinal);
                if (action.Args.TryGetValue("projectFileExtensions", out string? projectFileExtensions))
                {
                    if (projectFileExtensions.Contains('/') || projectFileExtensions.Contains('\\') || projectFileExtensions.Contains('*'))
                    {
                        // these must be literals
                        Reporter.Error.WriteLine(LocalizableStrings.PostAction_AddReference_Error_ActionMisconfigured);
                        return false;
                    }

                    extensionLimiters.UnionWith(projectFileExtensions.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));
                }
                projectsToProcess = FindProjFileAtOrAbovePath(environment.Host.FileSystem, outputBasePath, extensionLimiters);
                if (projectsToProcess.Count > 1)
                {
                    // multiple projects at the same level. Error.
                    Reporter.Error.WriteLine(LocalizableStrings.PostAction_AddReference_Error_UnresolvedProjFile);
                    Reporter.Error.WriteLine(LocalizableStrings.PostAction_AddReference_Error_ProjFileListHeader);
                    foreach (string projectFile in projectsToProcess)
                    {
                        Reporter.Error.WriteLine(string.Format("\t{0}", projectFile));
                    }
                    return false;
                }
            }
            if (projectsToProcess is null || !projectsToProcess.Any())
            {
                // no projects found. Error.
                Reporter.Error.WriteLine(LocalizableStrings.PostAction_AddReference_Error_UnresolvedProjFile);
                return false;
            }

            bool success = true;
            foreach (string projectFile in projectsToProcess)
            {
                success &= AddReference(action, projectFile, outputBasePath, creationEffects);

                if (!success)
                {
                    return false;
                }
            }
            return true;
        }

        private bool AddReference(IPostAction actionConfig, string projectFile, string outputBasePath, ICreationEffects creationEffects)
        {
            if (actionConfig.Args == null || !actionConfig.Args.TryGetValue("reference", out string? referenceToAdd))
            {
                Reporter.Error.WriteLine(LocalizableStrings.PostAction_AddReference_Error_ActionMisconfigured);
                return false;
            }

            if (!actionConfig.Args.TryGetValue("referenceType", out string? referenceType))
            {
                Reporter.Error.WriteLine(LocalizableStrings.PostAction_AddReference_Error_ActionMisconfigured);
                return false;
            }

            if (string.Equals(referenceType, "project", StringComparison.OrdinalIgnoreCase))
            {
                // replace the referenced project file's name in case it has been renamed
                string? referenceNameChange = GetTargetForSource((ICreationEffects2)creationEffects, referenceToAdd, outputBasePath).SingleOrDefault();
                string relativeProjectReference = referenceNameChange ?? referenceToAdd;

                referenceToAdd = Path.GetFullPath(relativeProjectReference, outputBasePath);
                return AddProjectReference(projectFile, referenceToAdd);
            }
            else if (string.Equals(referenceType, "package", StringComparison.OrdinalIgnoreCase))
            {
                actionConfig.Args.TryGetValue("version", out string? version);
                return AddPackageReference(projectFile, referenceToAdd, version);
            }
            else if (string.Equals(referenceType, "framework", StringComparison.OrdinalIgnoreCase))
            {
                Reporter.Error.WriteLine(string.Format(LocalizableStrings.PostAction_AddReference_Error_FrameworkNotSupported, referenceToAdd));
                return false;
            }
            else
            {
                Reporter.Error.WriteLine(string.Format(LocalizableStrings.PostAction_AddReference_Error_UnsupportedRefType, referenceType));
                return false;
            }
        }

        private bool AddPackageReference(string projectPath, string packageName, string? version)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(version))
                {
                    Reporter.Output.WriteLine(string.Format(LocalizableStrings.PostAction_AddReference_AddPackageReference, packageName, projectPath));
                }
                else
                {
                    Reporter.Output.WriteLine(string.Format(LocalizableStrings.PostAction_AddReference_AddPackageReference_WithVersion, packageName, version, projectPath));
                }
                bool succeeded = _addPackageReferenceCallback(projectPath, packageName, version);
                if (succeeded)
                {
                    Reporter.Output.WriteLine(LocalizableStrings.PostAction_AddReference_Succeeded);
                }
                else
                {
                    Reporter.Error.WriteLine(LocalizableStrings.PostAction_AddReference_Failed);
                }
                return succeeded;
            }
            catch (Exception e)
            {
                Reporter.Error.WriteLine(string.Format(LocalizableStrings.PostAction_AddReference_AddPackageReference_Failed, e.Message));
                return false;
            }
        }

        private bool AddProjectReference(string projectPath, string projectToAdd)
        {
            try
            {
                Reporter.Output.WriteLine(string.Format(LocalizableStrings.PostAction_AddReference_AddProjectReference, projectToAdd, projectPath));
                bool succeeded = _addProjectReferenceCallback(projectPath, projectToAdd);
                if (succeeded)
                {
                    Reporter.Output.WriteLine(LocalizableStrings.PostAction_AddReference_Succeeded);
                }
                else
                {
                    Reporter.Error.WriteLine(LocalizableStrings.PostAction_AddReference_Failed);
                }
                return succeeded;
            }
            catch (Exception e)
            {
                Reporter.Error.WriteLine(string.Format(LocalizableStrings.PostAction_AddReference_AddProjectReference_Failed, e.Message));
                return false;
            }
        }
    }
}
