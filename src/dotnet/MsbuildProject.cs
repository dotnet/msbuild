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
    internal class MsbuildProject
    {
        const string ProjectItemElementType = "ProjectReference";

        public ProjectRootElement Project { get; private set; }
        public string ProjectDirectory { get; private set; }

        private MsbuildProject(ProjectRootElement project)
        {
            Project = project;
            ProjectDirectory = PathUtility.EnsureTrailingSlash(Project.DirectoryPath);
        }

        public static MsbuildProject FromFileOrDirectory(string fileOrDirectory)
        {
            if (File.Exists(fileOrDirectory))
            {
                return FromFile(fileOrDirectory);
            }
            else
            {
                return FromDirectory(fileOrDirectory);
            }
        }

        public static MsbuildProject FromFile(string projectPath)
        {
            if (!File.Exists(projectPath))
            {
                throw new GracefulException(CommonLocalizableStrings.ProjectDoesNotExist, projectPath);
            }

            var project = TryOpenProject(projectPath);
            if (project == null)
            {
                throw new GracefulException(CommonLocalizableStrings.ProjectIsInvalid, projectPath);
            }

            return new MsbuildProject(project);
        }

        public static MsbuildProject FromDirectory(string projectDirectory)
        {
            DirectoryInfo dir;
            try
            {
                dir = new DirectoryInfo(projectDirectory);
            }
            catch (ArgumentException)
            {
                throw new GracefulException(CommonLocalizableStrings.CouldNotFindProjectOrDirectory, projectDirectory);
            }

            if (!dir.Exists)
            {
                throw new GracefulException(CommonLocalizableStrings.CouldNotFindProjectOrDirectory, projectDirectory);
            }

            FileInfo[] files = dir.GetFiles("*proj");
            if (files.Length == 0)
            {
                throw new GracefulException(CommonLocalizableStrings.CouldNotFindAnyProjectInDirectory, projectDirectory);
            }

            if (files.Length > 1)
            {
                throw new GracefulException(CommonLocalizableStrings.MoreThanOneProjectInDirectory, projectDirectory);
            }

            FileInfo projectFile = files.First();

            if (!projectFile.Exists)
            {
                throw new GracefulException(CommonLocalizableStrings.CouldNotFindAnyProjectInDirectory, projectDirectory);
            }

            var project = TryOpenProject(projectFile.FullName);
            if (project == null)
            {
                throw new GracefulException(CommonLocalizableStrings.FoundInvalidProject, projectFile.FullName);
            }

            return new MsbuildProject(project);
        }

        public int AddProjectToProjectReferences(string framework, IEnumerable<string> refs)
        {
            int numberOfAddedReferences = 0;

            ProjectItemGroupElement itemGroup = Project.FindUniformOrCreateItemGroupWithCondition(ProjectItemElementType, framework);
            foreach (var @ref in refs.Select((r) => NormalizeSlashes(r)))
            {
                if (Project.HasExistingItemWithCondition(framework, @ref))
                {
                    Reporter.Output.WriteLine(string.Format(CommonLocalizableStrings.ProjectAlreadyHasAreference, @ref));
                    continue;
                }

                numberOfAddedReferences++;
                itemGroup.AppendChild(Project.CreateItemElement(ProjectItemElementType, @ref));

                Reporter.Output.WriteLine(string.Format(CommonLocalizableStrings.ReferenceAddedToTheProject, @ref));
            }

            return numberOfAddedReferences;
        }

        public int RemoveProjectToProjectReferences(string framework, IEnumerable<string> refs)
        {
            int totalNumberOfRemovedReferences = 0;

            foreach (var @ref in refs)
            {
                totalNumberOfRemovedReferences += RemoveProjectToProjectReferenceAlternatives(framework, @ref);
            }

            return totalNumberOfRemovedReferences;
        }

        public void ConvertPathsToRelative(ref List<string> references)
        {
            references = references.Select((r) => PathUtility.GetRelativePath(ProjectDirectory, Path.GetFullPath(r))).ToList();
        }

        public static string NormalizeSlashes(string path)
        {
            return path.Replace('/', '\\');
        }

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
                        notExisting.Select((r) => string.Format(CommonLocalizableStrings.ReferenceDoesNotExist, r))));
            }
        }

        private int RemoveProjectToProjectReferenceAlternatives(string framework, string reference)
        {
            int numberOfRemovedRefs = 0;
            foreach (var r in GetIncludeAlternativesForRemoval(reference))
            {
                foreach (var existingItem in Project.FindExistingItemsWithCondition(framework, r))
                {
                    ProjectElementContainer itemGroup = existingItem.Parent;
                    itemGroup.RemoveChild(existingItem);
                    if (itemGroup.Children.Count == 0)
                    {
                        itemGroup.Parent.RemoveChild(itemGroup);
                    }

                    numberOfRemovedRefs++;
                    Reporter.Output.WriteLine(string.Format(CommonLocalizableStrings.ProjectReferenceRemoved, r));
                }
            }

            if (numberOfRemovedRefs == 0)
            {
                Reporter.Output.WriteLine(string.Format(CommonLocalizableStrings.ProjectReferenceCouldNotBeFound, reference));
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
        // path references to consider for removal are following:
        // - full path to ref.proj [/some/path/a/d/ref.proj]
        // - string which is passed as reference is relative to project [../d/ref.proj]
        // - string which is passed as reference is relative to current dir [../../d/ref.proj]
        private IEnumerable<string> GetIncludeAlternativesForRemoval(string reference)
        {
            // We do not care about duplicates in case when i.e. reference is already full path
            var ret = new List<string>();
            ret.Add(reference);

            string fullPath = Path.GetFullPath(reference);
            ret.Add(fullPath);
            ret.Add(PathUtility.GetRelativePath(ProjectDirectory, fullPath));

            return ret;
        }

        // There is ProjectRootElement.TryOpen but it does not work as expected
        // I.e. it returns null for some valid projects
        private static ProjectRootElement TryOpenProject(string filename)
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
    }
}
