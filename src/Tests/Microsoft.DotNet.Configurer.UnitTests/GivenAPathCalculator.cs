// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
