// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using System;
using System.IO;
using FluentAssertions;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.VSTest.Tests
{
    public class VSTestTests : TestBase
    {
        [Fact]
        public void TestsFromAGivenContainerShouldRunWithExpectedOutput()
        {
            var testAppName = "VSTestDotNetCore";
            var testRoot = TestAssets.Get(testAppName)
                .CreateInstance()
                .WithRestoreFiles()
                .WithBuildFiles()
                .Root;

            var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";

            var outputDll = testRoot
                .GetDirectory("bin", configuration, "netcoreapp1.0")
                .GetFile($"{testAppName}.dll");

            var argsForVstest = $"\"{outputDll.FullName}\"";

            // Call vstest
            new VSTestCommand()
                .ExecuteWithCapturedOutput(argsForVstest)
                .StdOut
                .Should().Contain("Total tests: 2. Passed: 1. Failed: 1. Skipped: 0.")
                     .And.Contain("Passed   TestNamespace.VSTestTests.VSTestPassTest")
                     .And.Contain("Failed   TestNamespace.VSTestTests.VSTestFailTest");
        }
    }
}
