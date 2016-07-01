// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Tests that Microsoft.Common.props successfully imports a global build project in the directory tree of the project being built.
    /// </summary>
    sealed public class GlobalBuildPropsImport_Tests : GlobalBuildProjectImportTestBase
    {
        protected override string CustomBuildProjectFile => "customBuild.props";

        protected override string GlobalBuildProjectBasePathPropertyName => "_GlobalBuildPropsBasePath";

        protected override string GlobalBuildProjectFile => "global.build.props";

        protected override string GlobalBuildProjectFilePropertyName => "_GlobalBuildPropsFile";

        protected override string GlobalBuildProjectPathPropertyName => "GlobalBuildPropsPath";

        protected override string ImportGlobalBuildProjectPropertyName => "ImportGlobalBuildProps";
    }
}