// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.Tools
{
    internal static class P2PHelpers
    {
        public static void EnsureAllReferencesExist(List<string> references)
        {
            var notExisting = new List<string>();
            foreach (var r in references)
            {
                if (!File.Exists(r))
                {
                    notExisting.Add(r);
                }
            }

            if (notExisting.Count > 0)
            {
                throw new GracefulException(
                    string.Join(
                        Environment.NewLine,
                        notExisting.Select((r) => string.Format(LocalizableStrings.ReferenceDoesNotExist, r))));
            }
        }

        public static void ConvertPathsToRelative(string root, ref List<string> references)
        {
            root = PathUtility.EnsureTrailingSlash(Path.GetFullPath(root));
            references = references.Select((r) => PathUtility.GetRelativePath(root, Path.GetFullPath(r))).ToList();
        }

        // There is ProjectRootElement.TryOpen but it does not work as expected
        // I.e. it returns null for some valid projects
        public static ProjectRootElement TryOpenProject(string filename)
        {
            try
            {
                return ProjectRootElement.Open(filename, new ProjectCollection(), preserveFormatting: true);
            }
            catch (Microsoft.Build.Exceptions.InvalidProjectFileException)
            {
                return null;
            }
        }

        public static ProjectRootElement GetProjectFromFileOrThrow(string filename)
        {
            if (!File.Exists(filename))
            {
                throw new GracefulException(LocalizableStrings.ProjectDoesNotExist, filename);
            }

            var project = TryOpenProject(filename);
            if (project == null)
            {
                throw new GracefulException(LocalizableStrings.ProjectIsInvalid, filename);
            }

            return project;
        }

        public static ProjectRootElement GetProjectFromDirectoryOrThrow(string directory)
        {
            DirectoryInfo dir;
            try
            {
                dir = new DirectoryInfo(directory);
            }
            catch (ArgumentException)
            {
                throw new GracefulException(LocalizableStrings.CouldNotFindProjectOrDirectory, directory);
            }

            if (!dir.Exists)
            {
                throw new GracefulException(LocalizableStrings.CouldNotFindProjectOrDirectory, directory);
            }

            FileInfo[] files = dir.GetFiles("*proj");
            if (files.Length == 0)
            {
                throw new GracefulException(LocalizableStrings.CouldNotFindAnyProjectInDirectory, directory);
            }

            if (files.Length > 1)
            {
                throw new GracefulException(LocalizableStrings.MoreThanOneProjectInDirectory, directory);
            }

            FileInfo projectFile = files.First();

            if (!projectFile.Exists)
            {
                throw new GracefulException(LocalizableStrings.CouldNotFindAnyProjectInDirectory, directory);
            }

            var ret = TryOpenProject(projectFile.FullName);
            if (ret == null)
            {
                throw new GracefulException(LocalizableStrings.FoundInvalidProject, projectFile.FullName);
            }

            return ret;
        }

        public static string NormalizeSlashesForMsbuild(string path)
        {
            return path.Replace('/', '\\');
        }

        public static int AddProjectToProjectReference(ProjectRootElement root, string framework, IEnumerable<string> refs)
        {
            int numberOfAddedReferences = 0;
            const string ProjectItemElementType = "ProjectReference";

            ProjectItemGroupElement itemGroup = root.FindUniformOrCreateItemGroupWithCondition(ProjectItemElementType, framework);
            foreach (var @ref in refs.Select((r) => NormalizeSlashesForMsbuild(r)))
            {
                if (root.HasExistingItemWithCondition(framework, @ref))
                {
                    Reporter.Output.WriteLine(string.Format(LocalizableStrings.ProjectAlreadyHasAreference, @ref));
                    continue;
                }

                numberOfAddedReferences++;
                itemGroup.AppendChild(root.CreateItemElement(ProjectItemElementType, @ref));

                Reporter.Output.WriteLine(string.Format(LocalizableStrings.ReferenceAddedToTheProject, @ref));
            }

            return numberOfAddedReferences;
        }

        public static int RemoveProjectToProjectReference(ProjectRootElement root, string framework, IEnumerable<string> refs)
        {
            throw new NotImplementedException();
        }
    }
}
