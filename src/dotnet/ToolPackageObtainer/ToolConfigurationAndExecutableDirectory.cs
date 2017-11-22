// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.ToolPackageObtainer
{
    internal class ToolConfigurationAndExecutableDirectory
    {
        public ToolConfigurationAndExecutableDirectory(
            ToolConfiguration toolConfiguration,
            DirectoryPath executableDirectory)
        {
            Configuration = toolConfiguration;
            ExecutableDirectory = executableDirectory;
        }

        public ToolConfiguration Configuration { get; }
        public DirectoryPath ExecutableDirectory { get; }
    }
}
