// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Tests that Microsoft.Common.targets successfully imports a directory build project in the directory tree of the project being built.
    /// </summary>
    public sealed class DirectoryBuildTargetsImportTests : DirectoryBuildProjectImportTestBase
    {
        protected override string DirectoryBuildProjectFile => "Directory.Build.targets";

        protected override string CustomBuildProjectFile => "customBuild.targets";

        protected override string DirectoryBuildProjectPathPropertyName => "DirectoryBuildTargetsPath";

        protected override string ImportDirectoryBuildProjectPropertyName => "ImportDirectoryBuildTargets";

        protected override string DirectoryBuildProjectFilePropertyName => "_DirectoryBuildTargetsFile";

        protected override string DirectoryBuildProjectBasePathPropertyName => "_DirectoryBuildTargetsBasePath";
    }
}
