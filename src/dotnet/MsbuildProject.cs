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
        public ProjectRootElement Project { get; private set; }
        public string ProjectPath { get; private set; }
        public string ProjectDirectory { get; private set; }

        private MsbuildProject(ProjectRootElement project, string projectPath, string projectDirectory)
        {
            Project = project;
            ProjectPath = projectPath;
            ProjectDirectory = PathUtility.EnsureTrailingSlash(projectDirectory);
        }

        private MsbuildProject(ProjectRootElement project, string projectPath)
        {
            Project = project;
            ProjectPath = projectPath;
            ProjectDirectory = PathUtility.EnsureTrailingSlash(new FileInfo(projectPath).DirectoryName);
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
                throw new GracefulException(LocalizableStrings.ProjectDoesNotExist, projectPath);
            }

            var project = TryOpenProject(projectPath);
            if (project == null)
            {
                throw new GracefulException(LocalizableStrings.ProjectIsInvalid, projectPath);
            }

            return new MsbuildProject(project, Path.GetFullPath(projectPath));
        }

        public static MsbuildProject FromDirectory(string projectDirectory)
        {
            DirectoryInfo dir;
            try
            {
                dir = new DirectoryInfo(projectDirectory);
                projectDirectory = dir.FullName;
            }
            catch (ArgumentException)
            {
                throw new GracefulException(LocalizableStrings.CouldNotFindProjectOrDirectory, projectDirectory);
            }

            if (!dir.Exists)
            {
                throw new GracefulException(LocalizableStrings.CouldNotFindProjectOrDirectory, projectDirectory);
            }

            FileInfo[] files = dir.GetFiles("*proj");
            if (files.Length == 0)
            {
                throw new GracefulException(LocalizableStrings.CouldNotFindAnyProjectInDirectory, projectDirectory);
            }

            if (files.Length > 1)
            {
                throw new GracefulException(LocalizableStrings.MoreThanOneProjectInDirectory, projectDirectory);
            }

            FileInfo projectFile = files.First();

            if (!projectFile.Exists)
            {
                throw new GracefulException(LocalizableStrings.CouldNotFindAnyProjectInDirectory, projectDirectory);
            }

            var project = TryOpenProject(projectFile.FullName);
            if (project == null)
            {
                throw new GracefulException(LocalizableStrings.FoundInvalidProject, projectFile.FullName);
            }

            return new MsbuildProject(project, projectFile.FullName, projectDirectory);
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
