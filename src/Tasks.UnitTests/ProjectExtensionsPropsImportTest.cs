﻿// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Tests that Microsoft.Common.props successfully imports project extensions written by package management systems.
    /// </summary>
    public sealed class ProjectExtensionsPropsImportTest : ProjectExtensionsImportTestBase
    {
        protected override string CustomImportProjectPath => Path.Combine(ObjectModelHelpers.TempProjectDir, "obj", $"{Path.GetFileName(_projectRelativePath)}.custom.props");

        protected override string ImportProjectPath => Path.Combine(Path.GetDirectoryName(_projectRelativePath), "obj", $"{Path.GetFileName(_projectRelativePath)}.custom.props");

        protected override string PropertyNameToEnableImport => "ImportProjectExtensionProps";

        protected override string PropertyNameToSignalImportSucceeded => "WasProjectExtensionPropsImported";
    }
}