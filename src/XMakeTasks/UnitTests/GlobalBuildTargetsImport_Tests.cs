// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Tests that Microsoft.Common.targets successfully imports a global build project in the directory tree of the project being built.
    /// </summary>
    sealed public class GlobalBuildTargetsImport_Tests : GlobalBuildProjectImportTestBase
    {
        protected override string GlobalBuildProjectFile => "global.build.targets";

        protected override string CustomBuildProjectFile => "customBuild.targets";

        protected override string GlobalBuildProjectPathPropertyName => "GlobalBuildTargetsPath";

        protected override string ImportGlobalBuildProjectPropertyName => "ImportGlobalBuildTargets";

        protected override string GlobalBuildProjectFilePropertyName => "_GlobalBuildTargetsFile";

        protected override string GlobalBuildProjectBasePathPropertyName => "_GlobalBuildTargetsBasePath";
    }
}