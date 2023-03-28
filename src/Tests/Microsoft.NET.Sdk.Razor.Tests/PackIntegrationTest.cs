// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.Utilities;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Sdk.Razor.Tests
{
    public class PackIntegrationTest : AspNetSdkTest
    {

        public PackIntegrationTest(ITestOutputHelper log) : base(log) {}

        [Fact]
        public void Pack_NoBuild_Works_IncludesAssembly()
        {
            var testAsset = "RazorClassLibrary";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

            var build = new BuildCommand(projectDirectory);
            build.Execute().Should().Pass();

            var pack = new MSBuildCommand(Log, "Pack", projectDirectory.Path);
            var result = pack.Execute("/p:NoBuild=true");

            result.Should().Pass();

            var outputPath = build.GetOutputDirectory(DefaultTfm, "Debug").ToString();

            new FileInfo(Path.Combine(outputPath, "ClassLibrary.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "ClassLibrary.Views.dll")).Should().NotExist();

            result.Should().NuSpecContain(
                Path.Combine(projectDirectory.Path, "obj", "Debug", "ClassLibrary.1.0.0.nuspec"),
                $"<file src=\"{Path.Combine(projectDirectory.Path, "bin", "Debug", DefaultTfm, "ClassLibrary.dll")}\" " +
                $"target=\"{Path.Combine("lib", DefaultTfm, "ClassLibrary.dll")}\" />");

            result.Should().NuSpecDoesNotContain(
                Path.Combine(projectDirectory.Path, "obj", "Debug", "ClassLibrary.1.0.0.nuspec"),
                $"<file src=\"{Path.Combine(projectDirectory.Path, "bin", "Debug", DefaultTfm, "ClassLibrary.Views.dll")}\" " +
                $"target=\"{Path.Combine("lib", DefaultTfm, "ClassLibrary.Views.dll")}\" />");

            result.Should().NuSpecDoesNotContain(
                Path.Combine(projectDirectory.Path, "obj", "Debug", "ClassLibrary.1.0.0.nuspec"),
                $"<file src=\"{Path.Combine(projectDirectory.Path, "bin", "Debug", DefaultTfm, "ClassLibrary.Views.pdb")}\" " +
                $"target=\"{Path.Combine("lib", DefaultTfm, "ClassLibrary.Views.pdb")}\" />");

            result.Should().NuSpecDoesNotContain(
                Path.Combine(projectDirectory.Path, "obj", "Debug", "ClassLibrary.1.0.0.nuspec"),
                $@"<files include=""any/{DefaultTfm}/Views/Shared/_Layout.cshtml"" buildAction=""Content"" />");

            result.Should().NuPkgContain(
                Path.Combine(projectDirectory.Path, "bin", "Debug", "ClassLibrary.1.0.0.nupkg"),
                Path.Combine("lib", DefaultTfm, "ClassLibrary.dll"));
        }
    }
}
