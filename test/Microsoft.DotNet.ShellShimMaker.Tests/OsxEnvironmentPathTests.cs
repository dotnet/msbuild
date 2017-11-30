// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.Extensions.DependencyModel.Tests;
using Xunit;

namespace Microsoft.DotNet.ShellShimMaker.Tests
{
    public class OsxEnvironmentPathTests
    {
        [Fact]
        public void GivenEnvironmentAndReporterItCanPrintOutInstructionToAddPath()
        {
            var fakeReporter = new FakeReporter();
            var osxEnvironmentPath = new OSXEnvironmentPath(
                @"~/executable/path",
                @"/Users/name/executable/path",
                fakeReporter,
                new FakeEnvironmentProvider(
                    new Dictionary<string, string>
                    {
                        {"PATH", ""}
                    }),
                FakeFile.Empty);

            osxEnvironmentPath.PrintAddPathInstructionIfPathDoesNotExist();

            // similar to https://code.visualstudio.com/docs/setup/mac
            fakeReporter.Message.Should().Be(
                $"Cannot find the tools executable path. Please ensure /Users/name/executable/path is added to your PATH.{Environment.NewLine}" +
                $"If you are using bash, You can do this by running the following command:{Environment.NewLine}{Environment.NewLine}" +
                $"cat << EOF >> ~/.bash_profile{Environment.NewLine}" +
                $"# Add .NET Core SDK tools{Environment.NewLine}" +
                $"export PATH=\"$PATH:/Users/name/executable/path\"{Environment.NewLine}" +
                $"EOF");
        }

        [Theory]
        [InlineData("/Users/name/executable/path")]
        [InlineData("~/executable/path")]
        public void GivenEnvironmentAndReporterItPrintsNothingWhenenvironmentExists(string existingPath)
        {
            var fakeReporter = new FakeReporter();
            var osxEnvironmentPath = new OSXEnvironmentPath(
                @"~/executable/path",
                @"/Users/name/executable/path",
                fakeReporter,
                new FakeEnvironmentProvider(
                    new Dictionary<string, string>
                    {
                        {"PATH", existingPath}
                    }),
                FakeFile.Empty);

            osxEnvironmentPath.PrintAddPathInstructionIfPathDoesNotExist();

            fakeReporter.Message.Should().BeEmpty();
        }

        [Fact]
        public void GivenAddPackageExecutablePathToUserPathJustRunItPrintsInstructionToLogout()
        {
            var fakeReporter = new FakeReporter();
            var osxEnvironmentPath = new OSXEnvironmentPath(
                @"~/executable/path",
                @"/Users/name/executable/path",
                fakeReporter,
                new FakeEnvironmentProvider(
                    new Dictionary<string, string>
                    {
                        {"PATH", @""}
                    }),
                FakeFile.Empty);
            osxEnvironmentPath.AddPackageExecutablePathToUserPath();

            osxEnvironmentPath.PrintAddPathInstructionIfPathDoesNotExist();

            fakeReporter.Message.Should().Be(
                "Since you just installed the .NET Core SDK, you will need to reopen terminal before running the tool you installed.");
        }
    }
}
