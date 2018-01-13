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
    public class LinuxEnvironmentPathTests
    {
        [Fact]
        public void GivenEnvironmentAndReporterItCanPrintOutInstructionToAddPath()
        {
            var fakeReporter = new FakeReporter();
            var linuxEnvironmentPath = new LinuxEnvironmentPath(
                new BashPathUnderHomeDirectory("/myhome", "executable/path"),
                fakeReporter,
                new FakeEnvironmentProvider(
                    new Dictionary<string, string>
                    {
                        {"PATH", ""}
                    }),
                FakeFile.Empty);

            linuxEnvironmentPath.PrintAddPathInstructionIfPathDoesNotExist();

            // similar to https://code.visualstudio.com/docs/setup/mac
            fakeReporter.Message.Should().Be(
                string.Format(
                    CommonLocalizableStrings.EnvironmentPathLinuxManualInstruction,
                    "/myhome/executable/path", "/myhome/executable/path"));
        }

        [Fact]
        public void GivenEnvironmentAndReporterItPrintsNothingWhenenvironmentExists()
        {
            var fakeReporter = new FakeReporter();
            var linuxEnvironmentPath = new LinuxEnvironmentPath(
                new BashPathUnderHomeDirectory("/myhome", "executable/path"),
                fakeReporter,
                new FakeEnvironmentProvider(
                    new Dictionary<string, string>
                    {
                        {"PATH", @"/myhome/executable/path"}
                    }),
                FakeFile.Empty);

            linuxEnvironmentPath.PrintAddPathInstructionIfPathDoesNotExist();

            fakeReporter.Message.Should().BeEmpty();
        }

        [Fact]
        public void GivenAddPackageExecutablePathToUserPathJustRunItPrintsInstructionToLogout()
        {
            var fakeReporter = new FakeReporter();
            var linuxEnvironmentPath = new LinuxEnvironmentPath(
                new BashPathUnderHomeDirectory("/myhome", "executable/path"),
                fakeReporter,
                new FakeEnvironmentProvider(
                    new Dictionary<string, string>
                    {
                        {"PATH", @""}
                    }),
                FakeFile.Empty);
            linuxEnvironmentPath.AddPackageExecutablePathToUserPath();

            linuxEnvironmentPath.PrintAddPathInstructionIfPathDoesNotExist();

            fakeReporter.Message.Should()
                .Be(CommonLocalizableStrings.EnvironmentPathLinuxNeedLogout);
        }
    }
}
