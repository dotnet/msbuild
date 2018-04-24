// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Evaluation;
using Microsoft.Build.UnitTests;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Build.Tasks.UnitTests
{
    public sealed class RegressionTests
    {
        private readonly ITestOutputHelper _output;

        public RegressionTests(ITestOutputHelper output)
        {
            _output = output;
        }

        /// <summary>
        /// Verifies that when a user overrides the BaseIntermediateOutputPath that the build still works.
        /// </summary>
        /// <remarks>This was written because of regression https://github.com/Microsoft/msbuild/issues/1509. </remarks>
        [Fact]
        public void OverrideBaseIntermediateOutputPathSucceeds()
        {
            Project project = ObjectModelHelpers.CreateInMemoryProject($@"
                <Project DefaultTargets=""Build"" xmlns=""msbuildnamespace"" ToolsVersion=""msbuilddefaulttoolsversion"">
                    <Import Project=""$(MSBuildToolsPath)\Microsoft.Common.props"" />

                    <PropertyGroup>
                        <BaseIntermediateOutputPath>obj\x86\Debug</BaseIntermediateOutputPath>
                    </PropertyGroup>

                    <Import Project=""$(MSBuildToolsPath)\Microsoft.CSharp.targets"" />

                    <Target Name=""Build"" />
                </Project>
                ");

            bool result = project.Build(new MockLogger(_output));

            Assert.True(result);
        }
    }
}
