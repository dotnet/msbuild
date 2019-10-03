// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using FluentAssertions;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;
using System.Linq;
using System.Xml.Linq;

namespace Microsoft.DotNet.Cli.Build.Tests
{
    public class GivenThatWeWantToBeBackwardsCompatibleWith1xProjects : TestBase
    {
        [RequiresSpecificFrameworkTheory("netcoreapp1.1")]
        [InlineData("netcoreapp1.1")]
        public void ItRestoresBuildsAndRuns(string target)
        {

            var testAppName = "TestAppSimple";
            var testInstance = TestAssets.Get(testAppName)
                .CreateInstance(testAppName + "_" + target.Replace('.', '_'))
                .WithSourceFiles();

            //   Replace the 'TargetFramework'
            ChangeProjectTargetFramework(testInstance.Root.GetFile($"{testAppName}.csproj"), target);

            new RestoreCommand()
                .WithWorkingDirectory(testInstance.Root)
                .Execute()
                .Should().Pass();

            new BuildCommand()
                .WithWorkingDirectory(testInstance.Root)
                .Execute()
                .Should().Pass();

            var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";

            var outputDll = testInstance.Root.GetDirectory("bin", configuration, target)
                .GetFile($"{testAppName}.dll");

            new DotnetCommand()
                .ExecuteWithCapturedOutput(outputDll.FullName)
                .Should().Pass()
                .And.HaveStdOutContaining("Hello World");
        }

        [Theory]
        [InlineData("netstandard1.3")]
        [InlineData("netstandard1.6")]
        public void ItRestoresBuildsAndPacks(string target)
        {

            var testAppName = "TestAppSimple";
            var testInstance = TestAssets.Get(testAppName)
                .CreateInstance(testAppName + "_" + target.Replace('.', '_'))
                .WithSourceFiles();

            //   Replace the 'TargetFramework'
            ChangeProjectTargetFramework(testInstance.Root.GetFile($"{testAppName}.csproj"), target);

            new RestoreCommand()
                .WithWorkingDirectory(testInstance.Root)
                .Execute()
                .Should().Pass();

            new BuildCommand()
                .WithWorkingDirectory(testInstance.Root)
                .Execute()
                .Should().Pass();

            new PackCommand()
                .WithWorkingDirectory(testInstance.Root)
                .Execute()
                .Should().Pass();
        }

        [RequiresSpecificFrameworkFact("netcoreapp1.0")] // https://github.com/dotnet/cli/issues/6087
        public void ItRunsABackwardsVersionedTool()
        {
            var testInstance = TestAssets.Get("11TestAppWith10CLIToolReferences")
                                         .CreateInstance()
                                         .WithSourceFiles()
                                         .WithRestoreFiles();

            var testProjectDirectory = testInstance.Root;

            new DotnetCommand()
                .WithWorkingDirectory(testInstance.Root)
                .ExecuteWithCapturedOutput("outputsframeworkversion-netcoreapp1.0")
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("netcoreapp1.0");
        }

        void ChangeProjectTargetFramework(FileInfo projectFile, string target)
        {
            var projectXml = XDocument.Load(projectFile.ToString());
            var ns = projectXml.Root.Name.Namespace;
            var propertyGroup = projectXml.Root.Elements(ns + "PropertyGroup").First();
            var rootNamespaceElement = propertyGroup.Element(ns + "TargetFramework");
            rootNamespaceElement.SetValue(target);
            projectXml.Save(projectFile.ToString());
        }

    }
}
