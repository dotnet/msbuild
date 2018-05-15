// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using System;
using System.IO;
using FluentAssertions;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;

namespace Microsoft.DotNet.Cli.InsertionTests
{
    public class GivenRestoreDotnetTools : TestBase
    {
        // Assets are placed during build of this project
        private static string GetDotnetToolPath() =>
            Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "TestDotnetToolsLayoutDirectory");
        private IEnumerable<DirectoryInfo> GetDotnetToolDirectory() =>
            new DirectoryInfo(GetDotnetToolPath()).GetDirectories().Where(d => d.Name.StartsWith("dotnet-"));

        [Fact]
        public void Then_there_is_DotnetTools()
        {
            new DirectoryInfo(GetDotnetToolPath()).GetDirectories().Should().Contain(d => d.Name.StartsWith("dotnet-"));
        }

        [Fact]
        public void Then_there_is_only_1_version()
        {
            foreach (var packageFolder in GetDotnetToolDirectory())
            {
                packageFolder.GetDirectories().Should().HaveCount(1);
            }
        }

        [Fact]
        public void Then_there_is_only_1_tfm()
        {
            foreach (var packageFolder in GetDotnetToolDirectory())
            {
                packageFolder.GetDirectories()[0]
                    .GetDirectories("tools")[0]
                    .GetDirectories().Should().HaveCount(1);
            }
        }

        [Fact]
        public void Then_there_is_only_1_rid()
        {
            foreach (var packageFolder in GetDotnetToolDirectory())
            {
                packageFolder.GetDirectories()[0]
                    .GetDirectories("tools")[0]
                    .GetDirectories()[0]
                    .GetDirectories().Should().HaveCount(1);
            }
        }

        [Fact]
        public void Then_packageName_is_the_same_as_dll()
        {
            foreach (var packageFolder in GetDotnetToolDirectory())
            {
                var packageId = packageFolder.Name;
                packageFolder.GetDirectories()[0].GetDirectories("tools")[0].GetDirectories()[0].GetDirectories()[0]
                    .GetFiles()
                    .Should().Contain(f => string.Equals(f.Name, $"{packageId}.dll", StringComparison.Ordinal));
            }
        }
    }
}
