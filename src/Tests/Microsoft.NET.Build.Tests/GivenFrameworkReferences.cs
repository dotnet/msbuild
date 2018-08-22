using System;
using System.Collections.Generic;
using System.IO;
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

        [Fact]
        public void A_FrameworkReference_resolves_to_a_known_framework_reference()
        {
            var testProject = new TestProject()
            {
                Name = "FrameworkReferenceTest",
                TargetFrameworks = "netcoreapp2.0",
                IsSdkProject = true,
                IsExe = true
            };

            testProject.SourceFiles.Add("Program.cs", @"
using System;
using Humanizer;

namespace FrameworkReferenceTest
{
    public class Program
    {
        public static void Main(string [] args)
        {
            Console.WriteLine(""HelloWorld"".Humanize());
        }
    }
}");

            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;

                    var itemGroup = new XElement(ns + "ItemGroup");
                    project.Root.Add(itemGroup);

                    //  In order to test the SDK functionality without the KnownFrameworkReferences which will be set
                    //  by the core-sdk repo in the bundled versions .props file, add a KnownFrameworkReference directly
                    //  to the project for test purposes
                    itemGroup.Add(new XElement(ns + "KnownFrameworkReference",
                           new XAttribute("Include", "HumanizerFramework"),
                           new XAttribute("RuntimeFrameworkName", "Humanizer.App"),
                           new XAttribute("DefaultRuntimeFrameworkVersion", "2.0.0"),
                           new XAttribute("LatestRuntimeFrameworkVersion", "3.0.0"),
                           new XAttribute("TargetingPackName", "Humanizer"),
                           new XAttribute("TargetingPackVersion", "2.4.2")));

                    itemGroup.Add(new XElement(ns + "FrameworkReference",
                                               new XAttribute("Include", "HumanizerFramework")));


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

            var runtimeFrameworkSection = runtimeConfig["runtimeOptions"]["framework"];

            string runtimeFrameworkName = ((JValue)runtimeFrameworkSection["name"]).Value<string>();
            runtimeFrameworkName.Should().Be("Humanizer.App");

            string runtimeFrameworkVersion = ((JValue)runtimeFrameworkSection["version"]).Value<string>();
            runtimeFrameworkVersion.Should().Be("2.0.0");
        }

        [Fact]
        public void Multiple_frameworks_are_written_to_runtimeconfig_when_there_are_multiple_FrameworkReferences()
        {
            var testProject = new TestProject()
            {
                Name = "MultipleFrameworkReferenceTest",
                TargetFrameworks = "netcoreapp2.0",
                IsSdkProject = true,
                IsExe = true
            };

            testProject.SourceFiles.Add("Program.cs", @"
using System;
using Humanizer;

namespace FrameworkReferenceTest
{
    public class Program
    {
        public static void Main(string [] args)
        {
            Console.WriteLine(""HelloWorld"".Humanize());
        }
    }
}");

            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;

                    var itemGroup = new XElement(ns + "ItemGroup");
                    project.Root.Add(itemGroup);

                    //  In order to test the SDK functionality without the KnownFrameworkReferences which will be set
                    //  by the core-sdk repo in the bundled versions .props file, add a KnownFrameworkReference directly
                    //  to the project for test purposes
                    itemGroup.Add(new XElement(ns + "KnownFrameworkReference",
                           new XAttribute("Include", "HumanizerFramework"),
                           new XAttribute("RuntimeFrameworkName", "Humanizer.App"),
                           new XAttribute("DefaultRuntimeFrameworkVersion", "2.0.0"),
                           new XAttribute("LatestRuntimeFrameworkVersion", "3.0.0"),
                           new XAttribute("TargetingPackName", "Humanizer"),
                           new XAttribute("TargetingPackVersion", "2.4.2")));


                    itemGroup.Add(new XElement(ns + "KnownFrameworkReference",
                           new XAttribute("Include", "NodaTimeFramework"),
                           new XAttribute("RuntimeFrameworkName", "NodaTime.App"),
                           new XAttribute("DefaultRuntimeFrameworkVersion", "1.0.0"),
                           new XAttribute("LatestRuntimeFrameworkVersion", "1.5.0"),
                           new XAttribute("TargetingPackName", "NodaTime"),
                           new XAttribute("TargetingPackVersion", "2.4.0")));

                    itemGroup.Add(new XElement(ns + "FrameworkReference",
                                               new XAttribute("Include", "HumanizerFramework")));
                    itemGroup.Add(new XElement(ns + "FrameworkReference",
                                               new XAttribute("Include", "NodaTimeFramework")));


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
            runtimeFrameworksList.Count.Should().Be(2);

            ((JValue)runtimeFrameworksList[0]["name"]).Value<string>().Should().Be("Humanizer.App");
            ((JValue)runtimeFrameworksList[0]["version"]).Value<string>().Should().Be("2.0.0");

            ((JValue)runtimeFrameworksList[1]["name"]).Value<string>().Should().Be("NodaTime.App");
            ((JValue)runtimeFrameworksList[1]["version"]).Value<string>().Should().Be("1.0.0");
        }

        [Fact]
        public void The_build_fails_when_there_is_an_unknown_FrameworkReference()
        {
            var testProject = new TestProject()
            {
                Name = "UnknownFrameworkReferenceTest",
                TargetFrameworks = "netcoreapp2.0",
                IsSdkProject = true,
                IsExe = true
            };

            testProject.SourceFiles.Add("Program.cs", @"
using System;
using Humanizer;

namespace FrameworkReferenceTest
{
    public class Program
    {
        public static void Main(string [] args)
        {
            Console.WriteLine(""HelloWorld"".Humanize());
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
    }
}
