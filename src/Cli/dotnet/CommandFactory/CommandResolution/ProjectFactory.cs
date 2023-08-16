// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Exceptions;
using Microsoft.DotNet.Cli.Utils;
using NuGet.Frameworks;

namespace Microsoft.DotNet.CommandFactory
{
    internal class ProjectFactory
    {
        private const string ProjectFactoryName = "projectfactory";

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

            Reporter.Verbose.WriteLine(string.Format(
                LocalizableStrings.MSBuildExePath,
                ProjectFactoryName,
                msBuildExePath));

            string msBuildProjectPath = GetMSBuildProjPath(projectDirectory);

            Reporter.Verbose.WriteLine(string.Format(
                LocalizableStrings.MSBuildProjectPath,
                ProjectFactoryName,
                msBuildProjectPath));

            if (msBuildProjectPath == null)
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
                throw new GracefulException(string.Format(
                    LocalizableStrings.MultipleProjectFilesFound,
                    projectDirectory));
            }

            return projectFiles.First();
        }
    }
}
