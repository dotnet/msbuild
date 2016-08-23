using Microsoft.Build.Construction;
using Microsoft.DotNet.ProjectJsonMigration;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.Tools.Test.Utilities;
using NuGet.Frameworks;
using System;
using System.IO;
using System.Linq;
using Xunit;
using FluentAssertions;
using Microsoft.DotNet.ProjectJsonMigration.Rules;
using Microsoft.DotNet.ProjectModel.Files;

namespace Microsoft.DotNet.ProjectJsonMigration.Tests
{
    public class GivenThatIWantToMigrateBuildOptions : TestBase
    {
        [Fact]
        public void Specified_default_properties_are_removed_when_they_exists_in_the_csproj_template()
        {
            // Setup project with default properties
            var defaultPropertiesExpectedToBeRemoved = new string[]
            {
                "OutputType",
                "TargetExt"
            };

            var defaultValue = "defaultValue";

            var templateProj = ProjectRootElement.Create();
            var defaultPropertyGroup = templateProj.AddPropertyGroup();

            foreach (var defaultPropertyName in defaultPropertiesExpectedToBeRemoved)
            {
                defaultPropertyGroup.AddProperty(defaultPropertyName, defaultValue);
            }

            // Setup projectcontext
            var testProjectDirectory = TestAssetsManager.CreateTestInstance("TestAppWithRuntimeOptions").Path;
            var projectContext = ProjectContext.Create(testProjectDirectory, FrameworkConstants.CommonFrameworks.NetCoreApp10);

            var testSettings = new MigrationSettings(testProjectDirectory, testProjectDirectory, "1.0.0", templateProj);
            var testInputs = new MigrationRuleInputs(new[] {projectContext}, templateProj, templateProj.AddItemGroup(),
                templateProj.AddPropertyGroup());
            new MigrateBuildOptionsRule().Apply(testSettings, testInputs);

            defaultPropertyGroup.Properties.Count.Should().Be(0);
        }

        [Fact]
        public void Migrating_empty_buildOptions_populates_only_AssemblyName_and_OutputType()
        {
            var mockProj = RunBuildOptionsRuleOnPj(@"
                {
                    ""buildOptions"": { }
                }");

            mockProj.Properties.Count().Should().Be(2);
            mockProj.Properties.Any(
                p =>
                    !(p.Name.Equals("AssemblyName", StringComparison.Ordinal) ||
                      p.Name.Equals("OutputType", StringComparison.Ordinal))).Should().BeFalse();

            mockProj.Items.Count().Should().Be(2);
            mockProj.Items.First(i => i.ItemType == "Compile").Include.Should().Be(@"**\*.cs");
            mockProj.Items.First(i => i.ItemType == "Compile").Exclude.Should().Be(@"bin\**;obj\**;**\*.xproj;packages\**");
            mockProj.Items.First(i => i.ItemType == "EmbeddedResource").Include.Should().Be(@"compiler\resources\**\*;**\*.resx");
            mockProj.Items.First(i => i.ItemType == "EmbeddedResource").Exclude.Should().Be(@"bin\**;obj\**;**\*.xproj;packages\**");
        }

        [Fact]
        public void Migrating_EmitEntryPoint_true_populates_OutputType_field()
        {
            var mockProj = RunBuildOptionsRuleOnPj(@"
                {
                    ""buildOptions"": {
                        ""emitEntryPoint"": ""true""
                    }
                }");

            mockProj.Properties.Count(p => p.Name == "OutputType").Should().Be(1);
            mockProj.Properties.First(p => p.Name == "OutputType").Value.Should().Be("Exe");
        }

        [Fact]
        public void Migrating_EmitEntryPoint_false_populates_OutputType_fields()
        {
            var mockProj = RunBuildOptionsRuleOnPj(@"
                {
                    ""buildOptions"": {
                        ""emitEntryPoint"": ""false""
                    }
                }");

            mockProj.Properties.Count(p => p.Name == "OutputType").Should().Be(1);
            mockProj.Properties.First(p => p.Name == "OutputType").Value.Should().Be("Library");
        }

        [Fact]
        public void Migrating_define_populates_DefineConstants()
        {
            var mockProj = RunBuildOptionsRuleOnPj(@"
                {
                    ""buildOptions"": {
                        ""define"": [ ""DEBUG"", ""TRACE"" ]
                    }
                }");

            mockProj.Properties.Count(p => p.Name == "DefineConstants").Should().Be(1);
            mockProj.Properties.First(p => p.Name == "DefineConstants").Value.Should().Be("DEBUG;TRACE");
        }

        [Fact]
        public void Migrating_nowarn_populates_NoWarn()
        {
            var mockProj = RunBuildOptionsRuleOnPj(@"
                {
                    ""buildOptions"": {
                        ""nowarn"": [ ""CS0168"", ""CS0219"" ]
                    }
                }");

            mockProj.Properties.Count(p => p.Name == "NoWarn").Should().Be(1);
            mockProj.Properties.First(p => p.Name == "NoWarn").Value.Should().Be("CS0168;CS0219");
        }

        [Fact]
        public void Migrating_warningsAsErrors_populates_WarningsAsErrors()
        {
            var mockProj = RunBuildOptionsRuleOnPj(@"
                {
                    ""buildOptions"": {
                        ""warningsAsErrors"": ""true""
                    }
                }");

            mockProj.Properties.Count(p => p.Name == "WarningsAsErrors").Should().Be(1);
            mockProj.Properties.First(p => p.Name == "WarningsAsErrors").Value.Should().Be("true");

            mockProj = RunBuildOptionsRuleOnPj(@"
                {
                    ""buildOptions"": {
                        ""warningsAsErrors"": ""false""
                    }
                }");

            mockProj.Properties.Count(p => p.Name == "WarningsAsErrors").Should().Be(0);
        }

        [Fact]
        public void Migrating_allowUnsafe_populates_AllowUnsafeBlocks()
        {
            var mockProj = RunBuildOptionsRuleOnPj(@"
                {
                    ""buildOptions"": {
                        ""allowUnsafe"": ""true""
                    }
                }");

            mockProj.Properties.Count(p => p.Name == "AllowUnsafeBlocks").Should().Be(1);
            mockProj.Properties.First(p => p.Name == "AllowUnsafeBlocks").Value.Should().Be("true");

            mockProj = RunBuildOptionsRuleOnPj(@"
                {
                    ""buildOptions"": {
                        ""allowUnsafe"": ""false""
                    }
                }");

            mockProj.Properties.Count(p => p.Name == "AllowUnsafeBlocks").Should().Be(0);
        }

        [Fact]
        public void Migrating_optimize_populates_Optimize()
        {
            var mockProj = RunBuildOptionsRuleOnPj(@"
                {
                    ""buildOptions"": {
                        ""optimize"": ""true""
                    }
                }");

            mockProj.Properties.Count(p => p.Name == "Optimize").Should().Be(1);
            mockProj.Properties.First(p => p.Name == "Optimize").Value.Should().Be("true");

            mockProj = RunBuildOptionsRuleOnPj(@"
                {
                    ""buildOptions"": {
                        ""optimize"": ""false""
                    }
                }");

            mockProj.Properties.Count(p => p.Name == "Optimize").Should().Be(0);
        }

        [Fact]
        public void Migrating_platform_populates_PlatformTarget()
        {
            var mockProj = RunBuildOptionsRuleOnPj(@"
                {
                    ""buildOptions"": {
                        ""platform"": ""x64""
                    }
                }");

            mockProj.Properties.Count(p => p.Name == "PlatformTarget").Should().Be(1);
            mockProj.Properties.First(p => p.Name == "PlatformTarget").Value.Should().Be("x64");

            mockProj = RunBuildOptionsRuleOnPj(@"
                {
                    ""buildOptions"": {
                        ""platform"": ""x86""
                    }
                }");

            mockProj.Properties.Count(p => p.Name == "PlatformTarget").Should().Be(1);
            mockProj.Properties.First(p => p.Name == "PlatformTarget").Value.Should().Be("x86");

            mockProj = RunBuildOptionsRuleOnPj(@"
                {
                    ""buildOptions"": {
                        ""platform"": ""foo""
                    }
                }");

            mockProj.Properties.Count(p => p.Name == "PlatformTarget").Should().Be(1);
            mockProj.Properties.First(p => p.Name == "PlatformTarget").Value.Should().Be("foo");
        }

        [Fact]
        public void Migrating_languageVersion_populates_LangVersion()
        {
            var mockProj = RunBuildOptionsRuleOnPj(@"
                {
                    ""buildOptions"": {
                        ""languageVersion"": ""5""
                    }
                }");

            mockProj.Properties.Count(p => p.Name == "LangVersion").Should().Be(1);
            mockProj.Properties.First(p => p.Name == "LangVersion").Value.Should().Be("5");
        }

        [Fact]
        public void Migrating_keyFile_populates_AssemblyOriginatorKeyFile_and_SignAssembly()
        {
            var mockProj = RunBuildOptionsRuleOnPj(@"
                {
                    ""buildOptions"": {
                        ""keyFile"": ""../keyfile.snk""
                    }
                }");

            mockProj.Properties.Count(p => p.Name == "AssemblyOriginatorKeyFile").Should().Be(1);
            mockProj.Properties.First(p => p.Name == "AssemblyOriginatorKeyFile").Value.Should().Be("../keyfile.snk");

            mockProj.Properties.Count(p => p.Name == "SignAssembly").Should().Be(1);
            mockProj.Properties.First(p => p.Name == "SignAssembly").Value.Should().Be("true");
        }

        [Fact]
        public void Migrating_delaySign_populates_DelaySign()
        {
            var mockProj = RunBuildOptionsRuleOnPj(@"
                {
                    ""buildOptions"": {
                        ""delaySign"": ""true""
                    }
                }");

            mockProj.Properties.Count(p => p.Name == "DelaySign").Should().Be(1);
            mockProj.Properties.First(p => p.Name == "DelaySign").Value.Should().Be("true");

            mockProj = RunBuildOptionsRuleOnPj(@"
                {
                    ""buildOptions"": {
                        ""delaySign"": ""false""
                    }
                }");

            mockProj.Properties.Count(p => p.Name == "DelaySign").Should().Be(0);
        }

        [Fact]
        public void Migrating_publicSign_populates_PublicSign()
        {
            var mockProj = RunBuildOptionsRuleOnPj(@"
                {
                    ""buildOptions"": {
                        ""publicSign"": ""true""
                    }
                }");

            mockProj.Properties.Count(p => p.Name == "PublicSign").Should().Be(1);
            mockProj.Properties.First(p => p.Name == "PublicSign").Value.Should().Be("true");

            mockProj = RunBuildOptionsRuleOnPj(@"
                {
                    ""buildOptions"": {
                        ""publicSign"": ""false""
                    }
                }");

            mockProj.Properties.Count(p => p.Name == "PublicSign").Should().Be(0);
        }

        [Fact]
        public void Migrating_debugType_populates_DebugType()
        {
            var mockProj = RunBuildOptionsRuleOnPj(@"
                {
                    ""buildOptions"": {
                        ""debugType"": ""full""
                    }
                }");

            mockProj.Properties.Count(p => p.Name == "DebugType").Should().Be(1);
            mockProj.Properties.First(p => p.Name == "DebugType").Value.Should().Be("full");

            mockProj = RunBuildOptionsRuleOnPj(@"
                {
                    ""buildOptions"": {
                        ""debugType"": ""foo""
                    }
                }");

            mockProj.Properties.Count(p => p.Name == "DebugType").Should().Be(1);
            mockProj.Properties.First(p => p.Name == "DebugType").Value.Should().Be("foo");
        }

        [Fact]
        public void Migrating_outputName_populates_AssemblyName()
        {
            var mockProj = RunBuildOptionsRuleOnPj(@"
                {
                    ""buildOptions"": {
                        ""outputName"": ""ARandomName""
                    }
                }");

            mockProj.Properties.Count(p => p.Name == "AssemblyName").Should().Be(1);
            mockProj.Properties.First(p => p.Name == "AssemblyName").Value.Should().Be("ARandomName");
        }

        [Fact]
        public void Migrating_xmlDoc_populates_GenerateDocumentationFile()
        {
            var mockProj = RunBuildOptionsRuleOnPj(@"
                {
                    ""buildOptions"": {
                        ""xmlDoc"": ""true""
                    }
                }");

            mockProj.Properties.Count(p => p.Name == "GenerateDocumentationFile").Should().Be(1);
            mockProj.Properties.First(p => p.Name == "GenerateDocumentationFile").Value.Should().Be("true");
        }

        [Theory]
        [InlineData("compile", "Compile")]
        [InlineData("embed", "EmbeddedResource")]
        [InlineData("copyToOutput", "Content")]
        private void Migrating_group_include_exclude_Populates_appropriate_ProjectItemElement(
            string group,
            string itemName)
        {
            var testDirectory = Temp.CreateDirectory().Path;

            Directory.CreateDirectory(Path.Combine(testDirectory, "root"));
            Directory.CreateDirectory(Path.Combine(testDirectory, "src"));
            File.WriteAllText(Path.Combine(testDirectory, "root", "file1.txt"), "content");
            File.WriteAllText(Path.Combine(testDirectory, "root", "file2.txt"), "content");
            File.WriteAllText(Path.Combine(testDirectory, "root", "file3.txt"), "content");
            File.WriteAllText(Path.Combine(testDirectory, "src", "file1.cs"), "content");
            File.WriteAllText(Path.Combine(testDirectory, "src", "file2.cs"), "content");
            File.WriteAllText(Path.Combine(testDirectory, "src", "file3.cs"), "content");
            File.WriteAllText(Path.Combine(testDirectory, "rootfile.cs"), "content");

            var pj = @"
                {
                    ""buildOptions"": {
                        ""<group>"": {
                            ""include"": [""root"", ""src"", ""rootfile.cs""],
                            ""exclude"": [""src"", ""rootfile.cs""],
                            ""includeFiles"": [""src/file1.cs"", ""src/file2.cs""],
                            ""excludeFiles"": [""src/file2.cs""]
                        }
                    }
                }".Replace("<group>", group);

            var mockProj = RunBuildOptionsRuleOnPj(pj,
                testDirectory: testDirectory);

            mockProj.Items.Count(i => i.ItemType.Equals(itemName, StringComparison.Ordinal)).Should().Be(2);

            var defaultIncludePatterns = group == "compile" ? ProjectFilesCollection.DefaultCompileBuiltInPatterns
                                       : group == "embed"   ? ProjectFilesCollection.DefaultResourcesBuiltInPatterns
                                       : Enumerable.Empty<string>();

            var defaultExcludePatterns = group == "copyToOutput" ? ProjectFilesCollection.DefaultPublishExcludePatterns
                                       : ProjectFilesCollection.DefaultBuiltInExcludePatterns;

            foreach (var item in mockProj.Items.Where(i => i.ItemType.Equals(itemName, StringComparison.Ordinal)))
            {
                if (item.ItemType == "Content")
                {
                    item.Metadata.Count(m => m.Name == "CopyToOutputDirectory").Should().Be(1);
                }

                if (item.Include.Contains(@"src\file1.cs"))
                {
                    item.Include.Should().Be(@"src\file1.cs;src\file2.cs");
                    item.Exclude.Should().Be(@"src\file2.cs");
                }
                else
                {
                    if (defaultIncludePatterns.Any())
                    {
                        item.Include.Should()
                            .Be(@"root\**\*;src\**\*;rootfile.cs;" + string.Join(";", defaultIncludePatterns).Replace("/", "\\"));
                    }
                    else
                    {
                        item.Include.Should()
                            .Be(@"root\**\*;src\**\*;rootfile.cs");
                    }

                    if (defaultExcludePatterns.Any())
                    {
                        item.Exclude.Should()
                            .Be(@"src\**\*;rootfile.cs;" + string.Join(";", defaultExcludePatterns).Replace("/", "\\") +
                                @";src\file2.cs");
                    }
                    else
                    {
                        item.Exclude.Should()
                            .Be(@"src\**\*;rootfile.cs;src\file2.cs");
                    }
                }
            }
        }

        private ProjectRootElement RunBuildOptionsRuleOnPj(string s, string testDirectory = null)
        {
            testDirectory = testDirectory ?? Temp.CreateDirectory().Path;
            return TemporaryProjectFileRuleRunner.RunRules(new IMigrationRule[]
            {
                new MigrateBuildOptionsRule()
            }, s, testDirectory);
        }
    }
}
