// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Test.Utilities;
using FluentAssertions;
using Microsoft.NET.TestFramework;

namespace Microsoft.DotNet.Configurer.UnitTests
{
    public class GivenAPathCalculator
    {
        [UnixOnlyFact]
        public void It_does_not_return_same_path_for_tools_package_and_tool_shim()
        {
            // shim name will conflict with the folder that is PackageId, if commandName and packageId are the same.
            CliFolderPathCalculator.ToolsPackagePath.Should().NotBe(CliFolderPathCalculator.ToolsShimPath);
            CliFolderPathCalculator.ToolsPackagePath.Should().NotBe(CliFolderPathCalculator.ToolsShimPathInUnix.Path);
        }
    }
}
