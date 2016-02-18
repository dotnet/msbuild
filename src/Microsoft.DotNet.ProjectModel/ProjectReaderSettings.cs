using System;

namespace Microsoft.DotNet.ProjectModel
{
    public class ProjectReaderSettings
    {
        public string VersionSuffix { get; set; }
        public string AssemblyFileVersion { get; set; }

        public static ProjectReaderSettings ReadFromEnvironment()
        {
            var settings = new ProjectReaderSettings
            {
                VersionSuffix = Environment.GetEnvironmentVariable("DOTNET_BUILD_VERSION"),
                AssemblyFileVersion = Environment.GetEnvironmentVariable("DOTNET_ASSEMBLY_FILE_VERSION")
            };

            return settings;
        }
    }
}
