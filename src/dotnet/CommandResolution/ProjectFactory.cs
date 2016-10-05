// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;

namespace Microsoft.DotNet.Cli.CommandResolution
{
    internal class ProjectFactory
    {
        public IProject GetProject(string projectDirectory)
        {
            return GetMSBuildProj(projectDirectory) ??
                GetProjectJsonProject(projectDirectory);
        }

        private IProject GetMSBuildProj(string projectDirectory)
        {
            string msBuildProjectPath = GetMSBuildProjPath(projectDirectory);
            if(msBuildProjectPath == null)
            {
                return null;
            }

            return new MSBuildProject(msBuildProjectPath);
        }

        private IProject GetProjectJsonProject(string projectDirectory)
        {
            return new ProjectJsonProject(projectDirectory);
        }

        private string GetMSBuildProjPath(string projectDirectory)
        {
            string[] projectFiles = Directory.GetFiles(projectDirectory, "*.*proj");

            if (projectFiles.Length == 0)
            {
                return null;
            }
            else if (projectFiles.Length > 1)
            {
                throw new InvalidOperationException(
                    $"Specify which project file to use because this '{projectDirectory}' contains more than one project file.");
            }

            return projectFiles[0];
        }
    }
}