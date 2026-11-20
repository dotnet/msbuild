// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Tests that Microsoft.Common.props successfully imports project extensions written by package management systems.
    /// </summary>
    public sealed class ProjectExtensionsPropsImportTest : ProjectExtensionsImportTestBase
    {
        public ProjectExtensionsPropsImportTest(ITestOutputHelper output)
            : base(output)
        {
        }

        protected override string CustomImportProjectRelativePath => Path.Combine("obj", $"{Path.GetFileName(_projectRelativePath)}.custom.props");

        protected override string ImportProjectRelativePath => Path.Combine(Path.GetDirectoryName(_projectRelativePath), "obj", $"{Path.GetFileName(_projectRelativePath)}.custom.props");

        protected override string PropertyNameToEnableImport => "ImportProjectExtensionProps";

        protected override string PropertyNameToSignalImportSucceeded => "WasProjectExtensionPropsImported";
    }
}
