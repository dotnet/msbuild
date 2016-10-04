// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.Build.Execution;
using NuGet.ProjectModel;

namespace Microsoft.DotNet.Cli.Utils
{
    internal class LockFilePathCalculator
    {
        public string GetLockFilePath(string projectDirectory)
        {
            return ResolveLockFilePathUsingCSProj(projectDirectory) ??
                ReturnProjectLockJson(projectDirectory);
        }

        private string ResolveLockFilePathUsingCSProj(string projectDirectory)
        {
            string csProjPath = GetCSProjPath(projectDirectory);
            if(csProjPath != null)
            {
                ProjectInstance projectInstance = new ProjectInstance(csProjPath, null, null);
            }

            return null;
        }

        private string ReturnProjectLockJson(string projectDirectory)
        {
            return Path.Combine(projectDirectory, LockFileFormat.LockFileName);
        }

        private string GetCSProjPath(string projectDirectory)
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