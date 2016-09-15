using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Build.Construction;
using Microsoft.DotNet.ProjectJsonMigration;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.Tools.Test.Utilities;
using NuGet.Frameworks;
using Xunit;
using FluentAssertions;
using Microsoft.DotNet.ProjectJsonMigration.Rules;
using Microsoft.DotNet.ProjectJsonMigration.Tests;

namespace Microsoft.DotNet.ProjectJsonMigration.Tests
{
    public class GivenThatIWantToMigrateConfigurations : TestBase
    {
        [Fact]
        public void Configuration_buildOptions_produce_expected_properties_in_a_group_with_a_condition()
        {
            var mockProj = RunConfigurationsRuleOnPj(@"
                {
                    ""configurations"": {
                        ""testconfig"": {
                            ""buildOptions"": {
                                ""emitEntryPoint"": ""true"",
                                ""debugType"": ""full""
                            }
                        }
                    }
                }");

            mockProj.Properties.Count(
                prop => prop.Name == "OutputType" || prop.Name == "DebugType").Should().Be(2);

            mockProj.Properties.First(p => p.Name == "OutputType")
                .Parent.Condition.Should()
                .Contain("'$(Configuration)' == 'testconfig'");
        }

        [Fact]
        public void Configuration_buildOptions_properties_are_not_written_when_they_overlap_with_buildOptions()
        {
            var mockProj = RunConfigurationsAndBuildOptionsRuleOnPj(@"
                {
                    ""buildOptions"": {
                        ""emitEntryPoint"": ""true"",
                        ""debugType"": ""full""
                    },
                    ""configurations"": {
                        ""testconfig"": {
                            ""buildOptions"": {
                                ""emitEntryPoint"": ""true"",
                                ""debugType"": ""full""
                            }
                        }
                    }
                }");

            mockProj.Properties.Count(property =>
                    property.Name == "OutputType" || property.Name == "DebugType")
                .Should().Be(2);

            foreach (var property in mockProj.Properties.Where(property =>
                    property.Name == "OutputType" || property.Name == "DebugType"))
            {
                property.Parent.Condition.Should().Be(string.Empty);
            }

        }

        [Fact]
        public void Configuration_buildOptions_includes_are_not_written_when_they_overlap_with_buildOptions()
        {
            var mockProj = RunConfigurationsAndBuildOptionsRuleOnPj(@"
                {
                    ""buildOptions"": {
                        ""copyToOutput"": {
                            ""include"": [""src""],
                            ""exclude"": [""src"", ""rootfile.cs""],
                            ""includeFiles"": [""src/file1.cs"", ""src/file2.cs""],
                            ""excludeFiles"": [""src/file2.cs""]
                        }
                    },
                    ""configurations"": {
                        ""testconfig"": {
                            ""buildOptions"": {
                                ""copyToOutput"": {
                                    ""include"": [""root"", ""src"", ""rootfile.cs""],
                                    ""exclude"": [""src"", ""rootfile.cs""],
                                    ""includeFiles"": [""src/file1.cs"", ""src/file2.cs""],
                                    ""excludeFiles"": [""src/file2.cs""]
                                }
                            }
                        }
                    }
                }");

            mockProj.Items.Count(item => item.ItemType == "Content").Should().Be(3);

            mockProj.Items.Where(item => item.ItemType == "Content")
                .Count(item => !string.IsNullOrEmpty(item.Parent.Condition))
                .Should()
                .Be(1);

            var configContent = mockProj.Items
                .Where(item => item.ItemType == "Content").First(item => !string.IsNullOrEmpty(item.Parent.Condition));

            // Directories are not converted to globs in the result because we did not write the directory
            configContent.Include.Should().Be(@"root;rootfile.cs");
            configContent.Exclude.Should().Be(@"src;rootfile.cs;src\file2.cs");
        }

        [Fact]
        public void Configuration_buildOptions_includes_which_have_different_excludes_than_buildOptions_throws()
        {
            Action action = () => RunConfigurationsRuleOnPj(@"
                {
                    ""buildOptions"": {
                        ""copyToOutput"": {
                            ""include"": [""src""],
                            ""exclude"": [""rootfile.cs""],
                            ""includeFiles"": [""src/file1.cs"", ""src/file2.cs""],
                            ""excludeFiles"": [""src/file3.cs""]
                        }
                    },
                    ""configurations"": {
                        ""testconfig"": {
                            ""buildOptions"": {
                                ""copyToOutput"": {
                                    ""include"": [""root"", ""src"", ""rootfile.cs""],
                                    ""exclude"": [""src"", ""rootfile.cs""],
                                    ""includeFiles"": [""src/file1.cs"", ""src/file2.cs""],
                                    ""excludeFiles"": [""src/file2.cs""]
                                }
                            }
                        }
                    }
                }");

            action.ShouldThrow<Exception>()
                .WithMessage(
                    "MIGRATE20012::Configuration Exclude: Unable to migrate projects with excluded files in configurations.");
        }
        private ProjectRootElement RunConfigurationsRuleOnPj(string s, string testDirectory = null)
        {
            testDirectory = testDirectory ?? Temp.CreateDirectory().Path;
            return TemporaryProjectFileRuleRunner.RunRules(new[] {new MigrateConfigurationsRule()}, s, testDirectory);
        }

        private ProjectRootElement RunConfigurationsAndBuildOptionsRuleOnPj(string s, string testDirectory = null)
        {
            testDirectory = testDirectory ?? Temp.CreateDirectory().Path;
            return TemporaryProjectFileRuleRunner.RunRules(new IMigrationRule[]
                {
                    new MigrateBuildOptionsRule(),
                    new MigrateConfigurationsRule()
                }, s, testDirectory);
        }
    }
}
