// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Xml.Linq;

using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;

using FluentAssertions;
using Xunit;

using Xunit.Abstractions;
using System.Text.RegularExpressions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildADesktopExeWithFSharp : SdkTest
    {
        public GivenThatWeWantToBuildADesktopExeWithFSharp(ITestOutputHelper log) : base(log)
        {
        }

        [WindowsOnlyFact]
        public void It_builds_a_simple_desktop_app()
        {
            var targetFramework = "net45";
            var testAsset = _testAssetsManager
                .CopyTestAsset("HelloWorldFS")
                .WithSource()
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    var propertyGroup = project.Root.Elements(ns + "PropertyGroup").First();
                    propertyGroup.Element(ns + "TargetFramework").SetValue(targetFramework);
                })
                .Restore(Log);

            var buildCommand = new BuildCommand(Log, testAsset.TestRoot);
            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework);

            outputDirectory.Should().OnlyHaveFiles(new[] {
                "TestApp.exe",
                "TestApp.pdb",
                "FSharp.Core.dll",
                "System.ValueTuple.dll",
                "cs/FSharp.Core.resources.dll",
                "de/FSharp.Core.resources.dll",
                "en/FSharp.Core.resources.dll",
                "es/FSharp.Core.resources.dll",
                "fr/FSharp.Core.resources.dll",
                "it/FSharp.Core.resources.dll",
                "ja/FSharp.Core.resources.dll",
                "ko/FSharp.Core.resources.dll",
                "pl/FSharp.Core.resources.dll",
                "pt-BR/FSharp.Core.resources.dll",
                "ru/FSharp.Core.resources.dll",
                "tr/FSharp.Core.resources.dll",
                "zh-Hans/FSharp.Core.resources.dll",
                "zh-Hant/FSharp.Core.resources.dll"
            });
        }
    }
}
