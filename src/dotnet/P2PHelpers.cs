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
        const string ProjectItemElementType = "ProjectReference";

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

        public static string NormalizeSlashesForMsbuild(string path)
        {
            return path.Replace('/', '\\');
        }

        public static int AddProjectToProjectReferences(ProjectRootElement root, string framework, IEnumerable<string> refs)
        {
            int numberOfAddedReferences = 0;

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

        public static int RemoveProjectToProjectReferences(MsbuildProject msbuildProject, string framework, IEnumerable<string> refs)
        {
            int totalNumberOfRemovedReferences = 0;

            foreach (var @ref in refs)
            {
                totalNumberOfRemovedReferences += RemoveProjectToProjectReferenceAlternatives(msbuildProject, framework, @ref);
            }

            return totalNumberOfRemovedReferences;
        }

        private static int RemoveProjectToProjectReferenceAlternatives(MsbuildProject msbuildProject, string framework, string reference)
        {
            int numberOfRemovedRefs = 0;
            foreach (var r in GetIncludeAlternativesForRemoval(msbuildProject, reference))
            {
                foreach (var existingItem in msbuildProject.Project.FindExistingItemsWithCondition(framework, r))
                {
                    ProjectElementContainer itemGroup = existingItem.Parent;
                    itemGroup.RemoveChild(existingItem);
                    if (itemGroup.Children.Count == 0)
                    {
                        itemGroup.Parent.RemoveChild(itemGroup);
                    }

                    numberOfRemovedRefs++;
                    Reporter.Output.WriteLine(string.Format(LocalizableStrings.ProjectReferenceRemoved, r));
                }
            }

            if (numberOfRemovedRefs == 0)
            {
                Reporter.Output.WriteLine(string.Format(LocalizableStrings.ProjectReferenceCouldNotBeFound, reference));
            }

            return numberOfRemovedRefs;
        }

        // Easiest way to explain rationale for this function is on the example. Let's consider following directory structure:
        // .../a/b/p.proj <project>
        // .../a/d/ref.proj <reference>
        // .../a/e/f/ <current working directory>
        // Project = /some/path/a/b/p.proj
        //
        // We do not know the format of passed reference so
        // directories to consider for removal are following:
        // - full path to ref.proj [/some/path/a/d/ref.proj]
        // - string which is passed as reference is relative to project [../d/ref.proj]
        // - string which is passed as reference is relative to current dir [../../d/ref.proj]
        private static IEnumerable<string> GetIncludeAlternativesForRemoval(MsbuildProject msbuildProject, string reference)
        {
            // We do not care about duplicates in case when i.e. reference is already full path
            var ret = new List<string>();
            ret.Add(reference);

            string fullPath = Path.GetFullPath(reference);
            ret.Add(fullPath);
            ret.Add(PathUtility.GetRelativePath(msbuildProject.ProjectDirectory, fullPath));

            return ret;
        }
    }
}
