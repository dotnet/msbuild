// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using System.Linq;
using System.Xml.Linq;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Cli.Build.Tests
{
    public class GivenThatWeWantToBeBackwardsCompatibleWith1xProjects : SdkTest
    {
        public GivenThatWeWantToBeBackwardsCompatibleWith1xProjects(ITestOutputHelper log) : base(log)
        {
        }

        [RequiresSpecificFrameworkTheory("netcoreapp1.1")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void ItRestoresBuildsAndRuns(string target)
        {

            var testAppName = "TestAppSimple";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName, identifier: target.Replace('.', '_'))
                .WithSource();

            //   Replace the 'TargetFramework'
            ChangeProjectTargetFramework(Path.Combine(testInstance.Path, $"{testAppName}.csproj"), target);

            var buildCommand = new BuildCommand(testInstance);

            buildCommand
                .Execute()
                .Should().Pass();

            var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";

            var outputDll = Path.Combine(buildCommand.GetOutputDirectory(target, configuration).FullName, $"{testAppName}.dll");

            new DotnetCommand(Log)
                .Execute(outputDll)
                .Should().Pass()
                .And.HaveStdOutContaining("Hello World");
        }

        [Theory]
        [InlineData("netstandard1.3")]
        [InlineData("netstandard1.6")]
        public void ItRestoresBuildsAndPacks(string target)
        {

            var testAppName = "TestAppSimple";
            var testInstance = _testAssetsManager.CopyTestAsset(testAppName, identifier: target.Replace('.', '_'))
                .WithSource();

            //   Replace the 'TargetFramework'
            ChangeProjectTargetFramework(Path.Combine(testInstance.Path, $"{testAppName}.csproj"), target);

            new BuildCommand(testInstance)
                .Execute()
                .Should().Pass();

            new PackCommand(Log, testInstance.Path)
                .Execute()
                .Should().Pass();
        }

        [RequiresSpecificFrameworkFact("netcoreapp1.0")] // https://github.com/dotnet/cli/issues/6087
        public void ItRunsABackwardsVersionedTool()
        {
            var testInstance = _testAssetsManager
                .CopyTestAsset("11TestAppWith10CLIToolReferences")
                .WithSource();

            NuGetConfigWriter.Write(testInstance.Path, TestContext.Current.TestPackages);

            new RestoreCommand(testInstance)
                .Execute()
                .Should()
                .Pass();

            new DotnetCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute("outputsframeworkversion-netcoreapp1.0")
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("netcoreapp1.0");
        }

        void ChangeProjectTargetFramework(string projectFile, string target)
        {
            var projectXml = XDocument.Load(projectFile);
            var ns = projectXml.Root.Name.Namespace;
            var propertyGroup = projectXml.Root.Elements(ns + "PropertyGroup").First();
            var rootNamespaceElement = propertyGroup.Element(ns + "TargetFramework");
            rootNamespaceElement.SetValue(target);
            projectXml.Save(projectFile.ToString());
        }

    }
}
