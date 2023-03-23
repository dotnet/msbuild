using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tests
{
    public class GenerateResourceTests : SdkTest
    {

        public GenerateResourceTests(ITestOutputHelper log) : base(log)
        {
        }

        [Theory(Skip="https://github.com/microsoft/msbuild/issues/4488")]
        [InlineData(ToolsetInfo.CurrentTargetFramework, true)]
        public void DependentUponTest(string targetFramework, bool isExe)
        {
            var testProject = new TestProject
            {
                Name = "HelloWorld",
                TargetFrameworks = targetFramework,
                IsExe = isExe,
                SourceFiles =
                {
                    ["Program.cs"] = @"
                        using System;

                        namespace SomeNamespace
                        {
                            public static class SomeClass
                            {
                                public static void Main(string[] args)
                                {
                                     var resourceManager = new global::System.Resources.ResourceManager(""SomeNamespace.SomeClass"", typeof(SomeClass).Assembly);
                                     Console.WriteLine(resourceManager.GetString(""SomeString""));
                                }
                            }
                        }
                        ",
                },
                EmbeddedResources =
                {
                    ["Program.resx"] = @"
                        <root>                          
                            <data name=""SomeString"" xml:space=""preserve"">
                                <value>Hello world from a resource!</value>
                            </data>
                        </root>
                        ",
                }
            };

            var testAsset = _testAssetsManager
                .CreateTestProject(testProject, identifier: targetFramework + isExe);

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework);

            var runCommand = new RunExeCommand(Log, Path.Combine(outputDirectory.FullName, "HelloWorld.exe"));
            runCommand
                .Execute()
                .Should()
                .Pass()
                .And.HaveStdOutContaining("Hello world from a resource!");
        }
    }
}
