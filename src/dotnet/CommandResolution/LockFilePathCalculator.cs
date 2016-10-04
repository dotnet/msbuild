// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Evaluation;
using NuGet.ProjectModel;

namespace Microsoft.DotNet.Cli.CommandResolution
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
            if(csProjPath == null)
            {
                return null;
            }

            var globalProperties = new Dictionary<string, string>()
            {
                { "MSBuildExtensionsPath", AppContext.BaseDirectory }
            };

            Project project = new Project(csProjPath, globalProperties, null);
            // TODO: This is temporary. We should use ProjectLockFile property, but for some reason, it is coming up as project.lock.json
            // instead of the path to project.assets.json.
            var lockFilePath = project.AllEvaluatedProperties.FirstOrDefault(p => p.Name.Equals("BaseIntermediateOutputPath")).EvaluatedValue;
            return Path.Combine(lockFilePath, "project.assets.json");
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