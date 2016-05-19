// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

namespace Microsoft.Dotnet.Tools.Test.Tests
{
    public class GivenThatWeWantToRunTests : TestBase
    {
        [Fact]
        public void It_fails_correctly_with_an_unrestored_project()
        {
            // NOTE: we don't say "WithLockFiles", so the project is "unrestored"
            var instance = TestAssetsManager.CreateTestInstance(Path.Combine("ProjectsWithTests", "NetCoreAppOnlyProject"));

            new DotnetTestCommand()
                .ExecuteWithCapturedOutput(instance.TestRoot)
                .Should()
                .Fail()
                .And
                .HaveStdErrContaining("NU1009")
                .And
                .HaveStdErrContaining("dotnet restore");
        }
    }
}
