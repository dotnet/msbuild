using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class GivenFrameworkReferences : SdkTest
    {
        public GivenFrameworkReferences(ITestOutputHelper log) : base(log)
        {
        }

        [WindowsOnlyFact]
        public void Multiple_frameworks_are_written_to_runtimeconfig_when_there_are_multiple_FrameworkReferences()
        {
            var testProject = new TestProject()
            {
                Name = "MultipleFrameworkReferenceTest",
                TargetFrameworks = "netcoreapp3.0",
                IsSdkProject = true,
                IsExe = true
            };

            testProject.SourceFiles.Add("Program.cs", @"
using System;

namespace FrameworkReferenceTest
{
    public class Program
    {
        public static void Main(string [] args)
        {
        }
    }
}");

            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;

                    var itemGroup = new XElement(ns + "ItemGroup");
                    project.Root.Add(itemGroup);

                    itemGroup.Add(new XElement(ns + "FrameworkReference",
                                               new XAttribute("Include", "Microsoft.AspNetCore.App")));
                    itemGroup.Add(new XElement(ns + "FrameworkReference",
                                               new XAttribute("Include", "Microsoft.WindowsDesktop.App")));


                })
                .Restore(Log, testProject.Name);

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(testProject.TargetFrameworks);

            string runtimeConfigFile = Path.Combine(outputDirectory.FullName, testProject.Name + ".runtimeconfig.json");
            string runtimeConfigContents = File.ReadAllText(runtimeConfigFile);
            JObject runtimeConfig = JObject.Parse(runtimeConfigContents);

            var runtimeFrameworksList = (JArray)runtimeConfig["runtimeOptions"]["frameworks"];
            var runtimeFrameworkNames = runtimeFrameworksList.Select(element => ((JValue)element["name"]).Value<string>());

            //  When we remove the workaround for https://github.com/dotnet/core-setup/issues/4947 in GenerateRuntimeConfigurationFiles,
            //  Microsoft.NETCore.App will need to be added to this list
            runtimeFrameworkNames.Should().BeEquivalentTo("Microsoft.AspNetCore.App", "Microsoft.WindowsDesktop.App");
        }

        [Fact]
        public void The_build_fails_when_there_is_an_unknown_FrameworkReference()
        {
            var testProject = new TestProject()
            {
                Name = "UnknownFrameworkReferenceTest",
                TargetFrameworks = "netcoreapp3.0",
                IsSdkProject = true,
                IsExe = true
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;

                    var itemGroup = new XElement(ns + "ItemGroup");
                    project.Root.Add(itemGroup);

                    itemGroup.Add(new XElement(ns + "FrameworkReference",
                                               new XAttribute("Include", "NotAKnownFramework")));
                    itemGroup.Add(new XElement(ns + "FrameworkReference",
                                               new XAttribute("Include", "AnotherUnknownFramework")));

                })
                .Restore(Log, testProject.Name);

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            buildCommand
                .Execute()
                .Should()
                .Fail()
                .And.HaveStdOutContaining("NETSDK1073")
                .And.HaveStdOutContaining("NotAKnownFramework")
                .And.HaveStdOutContaining("AnotherUnknownFramework")
                ;
        }

        [Theory]
        [InlineData("netcoreapp2.1", false)]
        [InlineData("netcoreapp3.0", true)]
        public void KnownFrameworkReferencesOnlyApplyToCorrectTargetFramework(string targetFramework, bool shouldPass)
        {
            var testProject = new TestProject()
            {
                Name = "FrameworkReferenceTest",
                TargetFrameworks = targetFramework,
                IsSdkProject = true,
                IsExe = true
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework)
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;

                    var itemGroup = new XElement(ns + "ItemGroup");
                    project.Root.Add(itemGroup);

                    itemGroup.Add(new XElement(ns + "FrameworkReference",
                                               new XAttribute("Include", "Microsoft.AspNetCore.App")));
                })
                .Restore(Log, testProject.Name);

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            var result = buildCommand.Execute();

            if (shouldPass)
            {
                result.Should().Pass();
            }
            else
            {
                result
                    .Should()
                    .Fail()
                    .And.HaveStdOutContaining("NETSDK1073")
                    .And.HaveStdOutContaining("Microsoft.AspNetCore.App");
            }

        }
    }
}
