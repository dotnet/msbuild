// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.DotNet.Internal.ProjectModel
{
    internal class ProjectReaderSettings
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
