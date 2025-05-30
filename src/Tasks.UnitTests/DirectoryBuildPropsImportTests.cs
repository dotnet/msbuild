// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Tests that Microsoft.Common.props successfully imports a directory build project in the directory tree of the project being built.
    /// </summary>
    public sealed class DirectoryBuildPropsImportTests : DirectoryBuildProjectImportTestBase
    {
        protected override string CustomBuildProjectFile => "customBuild.props";

        protected override string DirectoryBuildProjectBasePathPropertyName => "_DirectoryBuildPropsBasePath";

        protected override string DirectoryBuildProjectFile => "Directory.Build.props";

        protected override string DirectoryBuildProjectFilePropertyName => "_DirectoryBuildPropsFile";

        protected override string DirectoryBuildProjectPathPropertyName => "DirectoryBuildPropsPath";

        protected override string ImportDirectoryBuildProjectPropertyName => "ImportDirectoryBuildProps";
    }
}
