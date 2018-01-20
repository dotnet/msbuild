// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.ToolPackage
{
    internal class ToolConfigurationAndExecutablePath
    {
        public ToolConfigurationAndExecutablePath(
            ToolConfiguration toolConfiguration,
            FilePath executable)
        {
            Configuration = toolConfiguration;
            Executable = executable;
        }

        public ToolConfiguration Configuration { get; }

        public FilePath Executable { get; }
    }
}
