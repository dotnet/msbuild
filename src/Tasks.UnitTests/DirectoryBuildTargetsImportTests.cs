// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Tests that Microsoft.Common.targets successfully imports a directory build project in the directory tree of the project being built.
    /// </summary>
    sealed public class DirectoryBuildTargetsImportTests : DirectoryBuildProjectImportTestBase
    {
        protected override string DirectoryBuildProjectFile => "Directory.Build.targets";

        protected override string CustomBuildProjectFile => "customBuild.targets";

        protected override string DirectoryBuildProjectPathPropertyName => "DirectoryBuildTargetsPath";

        protected override string ImportDirectoryBuildProjectPropertyName => "ImportDirectoryBuildTargets";

        protected override string DirectoryBuildProjectFilePropertyName => "_DirectoryBuildTargetsFile";

        protected override string DirectoryBuildProjectBasePathPropertyName => "_DirectoryBuildTargetsBasePath";
    }
}