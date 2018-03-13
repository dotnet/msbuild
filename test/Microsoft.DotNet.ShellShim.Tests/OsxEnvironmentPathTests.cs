// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using FluentAssertions;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.Extensions.DependencyModel.Tests;
using Xunit;

namespace Microsoft.DotNet.ShellShim.Tests
{
    public class OsxEnvironmentPathTests
    {
        [Fact]
        public void GivenEnvironmentAndReporterItCanPrintOutInstructionToAddPath()
        {
            var fakeReporter = new FakeReporter();
            var osxEnvironmentPath = new OSXEnvironmentPath(
                new BashPathUnderHomeDirectory("/myhome", "executable/path"),
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
                string.Format(
                    CommonLocalizableStrings.EnvironmentPathOSXManualInstruction,
                    "/myhome/executable/path", "/myhome/executable/path"));
        }

        [Theory]
        [InlineData("/myhome/executable/path")]
        [InlineData("~/executable/path")]
        public void GivenEnvironmentAndReporterItPrintsNothingWhenenvironmentExists(string existingPath)
        {
            var fakeReporter = new FakeReporter();
            var osxEnvironmentPath = new OSXEnvironmentPath(
                new BashPathUnderHomeDirectory("/myhome", "executable/path"),
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
                new BashPathUnderHomeDirectory("/myhome", "executable/path"),
                fakeReporter,
                new FakeEnvironmentProvider(
                    new Dictionary<string, string>
                    {
                        {"PATH", @""}
                    }),
                FakeFile.Empty);
            osxEnvironmentPath.AddPackageExecutablePathToUserPath();

            osxEnvironmentPath.PrintAddPathInstructionIfPathDoesNotExist();

            fakeReporter.Message.Should().Be(CommonLocalizableStrings.EnvironmentPathOSXNeedReopen);
        }
    }
}
