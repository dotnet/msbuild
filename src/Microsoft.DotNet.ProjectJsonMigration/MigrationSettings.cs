// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.DotNet.Cli.Sln.Internal;

namespace Microsoft.DotNet.ProjectJsonMigration
{
    internal class MigrationSettings
    {
        public string ProjectXProjFilePath { get; }
        public string ProjectDirectory { get; }
        public string OutputDirectory { get; }
        public ProjectRootElement MSBuildProjectTemplate { get; }
        public string MSBuildProjectTemplatePath { get; }
        public string SdkDefaultsFilePath { get; }
        public SlnFile SolutionFile { get; }

        public MigrationSettings(
            string projectDirectory,
            string outputDirectory,
            string msBuildProjectTemplatePath,
            string projectXprojFilePath=null,
            string sdkDefaultsFilePath=null,
            SlnFile solutionFile=null) : this(
                projectDirectory, outputDirectory, projectXprojFilePath, sdkDefaultsFilePath, solutionFile)
        {
            MSBuildProjectTemplatePath = msBuildProjectTemplatePath;
            MSBuildProjectTemplate = ProjectRootElement.Open(
                MSBuildProjectTemplatePath,
                new ProjectCollection(),
                preserveFormatting: true);
        }

        private MigrationSettings(
            string projectDirectory,
            string outputDirectory,
            ProjectRootElement msBuildProjectTemplate,
            string projectXprojFilePath = null,
            string sdkDefaultsFilePath = null) : this(
                projectDirectory, outputDirectory, projectXprojFilePath, sdkDefaultsFilePath, null)
        {
            MSBuildProjectTemplate = msBuildProjectTemplate != null ? msBuildProjectTemplate.DeepClone() : null;
        }

        private MigrationSettings(
            string projectDirectory,
            string outputDirectory,
            string projectXprojFilePath,
            string sdkDefaultsFilePath,
            SlnFile solutionFile)
        {
            ProjectDirectory = projectDirectory;
            OutputDirectory = outputDirectory;
            ProjectXProjFilePath = projectXprojFilePath;
            SdkDefaultsFilePath = sdkDefaultsFilePath;
            SolutionFile = solutionFile;
        }

        public static MigrationSettings CreateMigrationSettingsTestHook(
            string projectDirectory,
            string outputDirectory,
            ProjectRootElement msBuildProjectTemplate,
            string projectXprojFilePath = null,
            string sdkDefaultsFilePath = null)
        {
            // Product code should not call this private constructor because we don't want to call DeepClone.
            // Doing so means we lose formatting.
            return new MigrationSettings(
                projectDirectory,
                outputDirectory,
                msBuildProjectTemplate,
                projectXprojFilePath,
                sdkDefaultsFilePath);
        }

    }
}
