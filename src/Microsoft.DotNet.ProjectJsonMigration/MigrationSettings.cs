// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;

namespace Microsoft.DotNet.ProjectJsonMigration
{
    internal class MigrationSettings
    {
        private string _msBuildProjectTemplatePath;

        public string ProjectXProjFilePath { get; }
        public string ProjectDirectory { get; }
        public string OutputDirectory { get; }
        public ProjectRootElement MSBuildProjectTemplate { get; }
        public string SdkDefaultsFilePath { get; }
        
        public MigrationSettings(
            string projectDirectory,
            string outputDirectory,
            ProjectRootElement msBuildProjectTemplate,
            string projectXprojFilePath=null,
            string sdkDefaultsFilePath=null) : this(
                projectDirectory, outputDirectory, projectXprojFilePath, sdkDefaultsFilePath)
        {
            MSBuildProjectTemplate = msBuildProjectTemplate != null ? msBuildProjectTemplate.DeepClone() : null;
        }

        public MigrationSettings(
            string projectDirectory,
            string outputDirectory,
            string msBuildProjectTemplatePath,
            string projectXprojFilePath=null,
            string sdkDefaultsFilePath=null) : this(
                projectDirectory, outputDirectory, projectXprojFilePath, sdkDefaultsFilePath)
        {
            _msBuildProjectTemplatePath = msBuildProjectTemplatePath;
            MSBuildProjectTemplate = ProjectRootElement.Open(
                _msBuildProjectTemplatePath,
                new ProjectCollection(),
                preserveFormatting: true);
        }

        private MigrationSettings(
            string projectDirectory,
            string outputDirectory,
            string projectXprojFilePath=null,
            string sdkDefaultsFilePath=null)
        {
            ProjectDirectory = projectDirectory;
            OutputDirectory = outputDirectory;
            ProjectXProjFilePath = projectXprojFilePath;
            SdkDefaultsFilePath = sdkDefaultsFilePath;
        }

        public ProjectRootElement CloneMSBuildProjectTemplate()
        {
            ProjectRootElement msBuildProjectTemplateClone = null;
            if(!string.IsNullOrEmpty(_msBuildProjectTemplatePath))
            {
                msBuildProjectTemplateClone = ProjectRootElement.Open(
                    _msBuildProjectTemplatePath,
                    new ProjectCollection(),
                    preserveFormatting: true);
            }
            else if(MSBuildProjectTemplate != null)
            {
                msBuildProjectTemplateClone = MSBuildProjectTemplate.DeepClone();
            }

            return msBuildProjectTemplateClone;
        }
    }
}
