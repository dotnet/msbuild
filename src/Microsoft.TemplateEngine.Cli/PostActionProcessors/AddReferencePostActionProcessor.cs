using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.PhysicalFileSystem;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Cli.PostActionProcessors
{
    public class AddReferencePostActionProcessor : IPostActionProcessor
    {
        public static readonly Guid ActionProcessorId = new Guid("B17581D1-C5C9-4489-8F0A-004BE667B814");

        public Guid Id => ActionProcessorId;

        public bool Process(IEngineEnvironmentSettings environment, IPostAction actionConfig, ICreationResult templateCreationResult, string outputBasePath)
        {
            if (actionConfig.Args == null || !actionConfig.Args.TryGetValue("reference", out string referenceToAdd))
            {
                environment.Host.LogMessage(LocalizableStrings.AddRefPostActionMisconfigured);
                return false;
            }

            if (!actionConfig.Args.TryGetValue("referenceType", out string referenceType))
            {
                environment.Host.LogMessage(LocalizableStrings.AddRefPostActionMisconfigured);
                return false;
            }

            if (string.IsNullOrEmpty(outputBasePath))
            {
                environment.Host.LogMessage(LocalizableStrings.AddRefPostActionUnresolvedProjFile);
            }

            HashSet<string> extensionLimiters = new HashSet<string>(StringComparer.Ordinal);
            if (actionConfig.Args.TryGetValue("projectFileExtensions", out string projectFileExtensions))
            {
                if (projectFileExtensions.Contains("/") || projectFileExtensions.Contains("\\") || projectFileExtensions.Contains("*"))
                {   // these must be literals
                    environment.Host.LogMessage(LocalizableStrings.AddRefPostActionMisconfigured);
                    return false;
                }

                extensionLimiters.UnionWith(projectFileExtensions.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));
            }

            IReadOnlyList<string> nearestProjectFilesFound = FindProjFileAtOrAbovePath(environment.Host.FileSystem, outputBasePath, extensionLimiters);

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
                    environment.Host.LogMessage(string.Format(LocalizableStrings.AddRefPostActionAddProjectRef, projectFile, referenceToAdd));
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
                        environment.Host.LogMessage(string.Format(LocalizableStrings.AddRefPostActionAddPackageRef, projectFile, referenceToAdd));
                    }
                    else
                    {
                        environment.Host.LogMessage(string.Format(LocalizableStrings.AddRefPostActionAddPackageRefWithVersion, projectFile, referenceToAdd, version));
                    }
                    commandResult = addReferenceCommand.Execute();
                }
                else if (string.Equals(referenceType, "framework", StringComparison.OrdinalIgnoreCase))
                {
                    environment.Host.LogMessage(string.Format(LocalizableStrings.AddRefPostActionFrameworkNotSupported, referenceToAdd));
                    return false;
                }
                else
                {
                    environment.Host.LogMessage(string.Format(LocalizableStrings.AddRefPostActionUnsupportedRefType, referenceType));
                    return false;
                }

                if (commandResult.ExitCode != 0)
                {
                    environment.Host.LogMessage(string.Format(LocalizableStrings.AddRefPostActionFailed, referenceToAdd, projectFile));
                    environment.Host.LogMessage(string.Format(LocalizableStrings.CommandOutput, commandResult.StdOut + Environment.NewLine + Environment.NewLine + commandResult.StdErr));
                    environment.Host.LogMessage(string.Empty);
                    return false;
                }
                else
                {
                    environment.Host.LogMessage(string.Format(LocalizableStrings.AddRefPostActionSucceeded, referenceToAdd, projectFile));
                    return true;
                }
            }
            else if (nearestProjectFilesFound.Count == 0)
            {
                // no projects found. Error.
                environment.Host.LogMessage(LocalizableStrings.AddRefPostActionUnresolvedProjFile);
                return false;
            }
            else
            {
                // multiple projects at the same level. Error.
                environment.Host.LogMessage(LocalizableStrings.AddRefPostActionUnresolvedProjFile);
                environment.Host.LogMessage(LocalizableStrings.AddRefPostActionProjFileListHeader);
                foreach (string projectFile in nearestProjectFilesFound)
                {
                    environment.Host.LogMessage(string.Format("\t{0}", projectFile));
                }

                return false;
            }
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
    }
}
