// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Construction;

namespace Microsoft.DotNet.ProjectJsonMigration
{
    internal class MigrationSettings
    {
        public string ProjectXProjFilePath { get; }
        public string ProjectDirectory { get; }
        public string OutputDirectory { get; }
        public string SdkPackageVersion { get; }
        public ProjectRootElement MSBuildProjectTemplate { get; }
        public string SdkDefaultsFilePath { get; }
        
        public MigrationSettings(
            string projectDirectory,
            string outputDirectory,
            string sdkPackageVersion,
            ProjectRootElement msBuildProjectTemplate,
            string projectXprojFilePath=null,
            string sdkDefaultsFilePath=null)
        {
            ProjectDirectory = projectDirectory;
            OutputDirectory = outputDirectory;
            SdkPackageVersion = sdkPackageVersion;
            MSBuildProjectTemplate = msBuildProjectTemplate != null ? msBuildProjectTemplate.DeepClone() : null;
            ProjectXProjFilePath = projectXprojFilePath;
            SdkDefaultsFilePath = sdkDefaultsFilePath;
        }
    }
}
