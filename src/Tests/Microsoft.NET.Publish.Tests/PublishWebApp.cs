using System.IO;
using FluentAssertions;
using System.Runtime.InteropServices;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.PlatformAbstractions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using System.Xml.Linq;
using System.Runtime.CompilerServices;
using System;
using Microsoft.Extensions.DependencyModel;
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
                TargetFrameworks = "netcoreapp3.0",
                IsSdkProject = true,
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

                            })
                            .Restore(Log, testProject.Name);

            var publishCommand = new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

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
