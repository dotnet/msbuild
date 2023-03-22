// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Publish.Tests
{
    public class PublishWebApp : SdkTest
    {
        public PublishWebApp(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_publishes_and_runs_self_contained_web_app()
        {
            var testProject = new TestProject()
            {
                Name = "SelfContainedWebApp",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            testProject.RuntimeIdentifier = EnvironmentInfo.GetCompatibleRid(testProject.TargetFrameworks);

            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                            .WithProjectChanges(project =>
                            {
                                var ns = project.Root.Name.Namespace;

                                var itemGroup = new XElement(ns + "ItemGroup");
                                project.Root.Add(itemGroup);

                                itemGroup.Add(new XElement(ns + "FrameworkReference",
                                                           new XAttribute("Include", "Microsoft.AspNetCore.App")));

                            });

            var publishCommand = new PublishCommand(testAsset);

            publishCommand.Execute()
                .Should()
                .Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(
                targetFramework: testProject.TargetFrameworks,
                runtimeIdentifier: testProject.RuntimeIdentifier);

            var runAppCommand = new SdkCommandSpec()
            {
                FileName = Path.Combine(publishDirectory.FullName, testProject.Name + EnvironmentInfo.ExecutableExtension)
            };

            runAppCommand.Environment["DOTNET_ROOT"] = Path.GetDirectoryName(TestContext.Current.ToolsetUnderTest.DotNetHostPath);

            var result = runAppCommand.ToCommand()
                .CaptureStdErr()
                .CaptureStdOut()
                .Execute();

            result
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello World");
        }
    }
}
