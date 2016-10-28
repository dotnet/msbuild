// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Exceptions;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Cli.Utils
{
    internal class ProjectFactory
    {
        private IEnvironmentProvider _environment;

        public ProjectFactory(IEnvironmentProvider environment)
        {
            _environment = environment;
        }

        public IProject GetProject(
            string projectDirectory,
            NuGetFramework framework,
            string configuration,
            string buildBasePath,
            string outputPath)
        {
            return GetMSBuildProj(projectDirectory, framework, configuration, outputPath);
        }

        private IProject GetMSBuildProj(string projectDirectory, NuGetFramework framework, string configuration, string outputPath)
        {
            var msBuildExePath = _environment.GetEnvironmentVariable(Constants.MSBUILD_EXE_PATH);

            msBuildExePath = string.IsNullOrEmpty(msBuildExePath) ?
                Path.Combine(AppContext.BaseDirectory, "MSBuild.dll") :
                msBuildExePath;

            Reporter.Verbose.WriteLine($"projetfactory: MSBUILD_EXE_PATH = {msBuildExePath}");

            string msBuildProjectPath = GetMSBuildProjPath(projectDirectory);

            Reporter.Verbose.WriteLine($"projetfactory: MSBuild project path = {msBuildProjectPath}");
            
            if(msBuildProjectPath == null)
            {
                return null;
            }

            try
            {
                return new MSBuildProject(msBuildProjectPath, framework, configuration, outputPath, msBuildExePath);
            }
            catch (InvalidProjectFileException ex)
            {
                Reporter.Verbose.WriteLine(ex.ToString().Red());
                
                return null;
            }
        }

        private string GetMSBuildProjPath(string projectDirectory)
        {
            IEnumerable<string> projectFiles = Directory
                .GetFiles(projectDirectory, "*.*proj")
                .Where(d => !d.EndsWith(".xproj"));

            if (projectFiles.Count() == 0)
            {
                return null;
            }
            else if (projectFiles.Count() > 1)
            {
                throw new InvalidOperationException(
                    $"Specify which project file to use because this '{projectDirectory}' contains more than one project file.");
            }

            return projectFiles.First();
        }
    }
}