// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.PhysicalFileSystem;
using Microsoft.TemplateEngine.Utils;
using Newtonsoft.Json.Linq;

namespace Microsoft.TemplateEngine.Cli.PostActionProcessors
{
    internal class AddReferencePostActionProcessor : PostActionProcessor2Base
    {
        internal static readonly Guid ActionProcessorId = new Guid("B17581D1-C5C9-4489-8F0A-004BE667B814");

        public override Guid Id => ActionProcessorId;

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

        protected override bool ProcessInternal(IEngineEnvironmentSettings environment, IPostAction action, ICreationEffects creationEffects, ICreationResult templateCreationResult, string outputBasePath)
        {
            IReadOnlyList<string>? projectsToProcess = GetConfiguredFiles(action.Args, creationEffects, "targetFiles", outputBasePath);

            if (projectsToProcess is null)
            {
                //If the author didn't opt in to the new behavior by specifying "targetFiles", search for project file in current output directory or above.
                HashSet<string> extensionLimiters = new HashSet<string>(StringComparer.Ordinal);
                if (action.Args.TryGetValue("projectFileExtensions", out string? projectFileExtensions))
                {
                    if (projectFileExtensions.Contains("/") || projectFileExtensions.Contains("\\") || projectFileExtensions.Contains("*"))
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
                success &= AddReference(environment, action, projectFile, outputBasePath, creationEffects);

                if (!success)
                {
                    return false;
                }
            }
            return true;
        }

        private bool AddReference(IEngineEnvironmentSettings environment, IPostAction actionConfig, string projectFile, string outputBasePath, ICreationEffects creationEffects)
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

            bool succeeded = false;

            if (string.Equals(referenceType, "project", StringComparison.OrdinalIgnoreCase))
            {
                if ((Callbacks?.AddProjectReference) == null)
                {
                    Reporter.Error.WriteLine(LocalizableStrings.PostAction_AddReference_Failed);
                    Reporter.Error.WriteLine(LocalizableStrings.Generic_NoCallbackError);
                    return false;
                }

                // replace the referenced project file's name in case it has been renamed
                string? referenceNameChange = GetTargetForSource((ICreationEffects2)creationEffects, referenceToAdd, outputBasePath).SingleOrDefault();
                string relativeProjectReference = referenceNameChange ?? referenceToAdd;

                referenceToAdd = Path.GetFullPath(relativeProjectReference, outputBasePath);

                Reporter.Output.WriteLine(string.Format(LocalizableStrings.PostAction_AddReference_AddProjectReference, referenceToAdd, projectFile));
                succeeded = Callbacks.AddProjectReference(projectFile, new[] { referenceToAdd });
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
            else if (string.Equals(referenceType, "package", StringComparison.OrdinalIgnoreCase))
            {
                actionConfig.Args.TryGetValue("version", out string? version);
                if ((Callbacks?.AddPackageReference) == null)
                {
                    Reporter.Error.WriteLine(LocalizableStrings.PostAction_AddReference_Failed);
                    Reporter.Error.WriteLine(LocalizableStrings.Generic_NoCallbackError);
                    return false;
                }
                if (string.IsNullOrWhiteSpace(version))
                {
                    Reporter.Output.WriteLine(string.Format(LocalizableStrings.PostAction_AddReference_AddPackageReference, referenceToAdd, projectFile));
                }
                else
                {
                    Reporter.Output.WriteLine(string.Format(LocalizableStrings.PostAction_AddReference_AddPackageReference_WithVersion, referenceToAdd, version, projectFile));
                }
                succeeded = Callbacks.AddPackageReference(projectFile, referenceToAdd, version);
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
    }
}
