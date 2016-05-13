// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

namespace Microsoft.DotNet.Tools.Builder.Tests
{
    public class BuildWarningsTests : TestBase
    {
        [Fact]
        public void HavingDeprecatedProjectFileProducesWarning()
        {
            var testInstance = TestAssetsManager.CreateTestInstance("TestLibraryWithDeprecatedProjectFile").WithLockFiles();

            new BuildCommand(testInstance.TestRoot)
                .ExecuteWithCapturedOutput()
                .Should()
                .Pass()
                .And
                .HaveStdErrContaining("DOTNET1015: The 'compilationOptions' option is deprecated. Use 'buildOptions' instead.")
                .And
                .HaveStdErrContaining("DOTNET1015: The 'packInclude' option is deprecated. Use 'files' in 'packOptions' instead.");
        }
    }
}
