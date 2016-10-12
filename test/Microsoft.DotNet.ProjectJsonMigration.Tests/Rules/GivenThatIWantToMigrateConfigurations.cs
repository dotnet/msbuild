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
        public void Frameworks_buildOptions_produce_expected_properties_in_a_group_with_a_condition()
        {
            var mockProj = RunConfigurationsRuleOnPj(@"
                {
                    ""frameworks"": {
                        ""netcoreapp1.0"": {
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
                .Contain("'$(TargetFramework)' == 'netcoreapp1.0'");
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
        public void Configuration_buildOptions_includes_and_Remove_are_written_when_they_differ_from_base_buildOptions()
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
                                    ""exclude"": [""src"", ""root/rootfile.cs""],
                                    ""includeFiles"": [""src/file1.cs"", ""src/file2.cs""],
                                    ""excludeFiles"": [""src/file2.cs""]
                                }
                            }
                        }
                    }
                }");

            var contentItems = mockProj.Items.Where(item => item.ItemType == "Content");

            contentItems.Count().Should().Be(4);

            // 2 for Base Build options
            contentItems.Where(i => i.ConditionChain().Count() == 0).Should().HaveCount(2);

            // 2 for Configuration BuildOptions (1 Remove, 1 Include)
            contentItems.Where(i => i.ConditionChain().Count() == 1).Should().HaveCount(2);
            
            var configIncludeContentItem = contentItems.First(
                item => item.ConditionChain().Count() > 0 && !string.IsNullOrEmpty(item.Include));
            var configRemoveContentItem = contentItems.First(
                item => item.ConditionChain().Count() > 0 && !string.IsNullOrEmpty(item.Remove));

            // Directories are not converted to globs in the result because we did not write the directory
            configRemoveContentItem.Remove.Should().Be(@"root;src;rootfile.cs");
            configIncludeContentItem.Include.Should().Be(@"root;src;rootfile.cs");
            configIncludeContentItem.Exclude.Should().Be(@"src;root\rootfile.cs;src\file2.cs");
        }

        [Fact]
        public void Configuration_buildOptions_which_have_different_excludes_than_buildOptions_overwrites()
        {
            var mockProj = RunConfigurationsAndBuildOptionsRuleOnPj(@"
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
                                    ""exclude"": [""rootfile.cs"", ""someotherfile.cs""],
                                    ""includeFiles"": [""src/file1.cs"", ""src/file2.cs""],
                                    ""excludeFiles"": [""src/file2.cs""]
                                }
                            }
                        }
                    }
                }");

            var contentItems = mockProj.Items.Where(item => item.ItemType == "Content");

            contentItems.Count().Should().Be(5);

            // 2 for Base Build options
            contentItems.Where(i => i.ConditionChain().Count() == 0).Should().HaveCount(2);

            // 3 for Configuration BuildOptions (1 Remove, 2 Include)
            contentItems.Where(i => i.ConditionChain().Count() == 1).Should().HaveCount(3);

            var configIncludeContentItem = contentItems.First(
                item => item.ConditionChain().Count() > 0 
                    && item.Include.Contains("root"));

            var configIncludeContentItem2 = contentItems.First(
                item => item.ConditionChain().Count() > 0 
                    && item.Include.Contains(@"src\file1.cs"));

            var configRemoveContentItem = contentItems.First(
                item => item.ConditionChain().Count() > 0 && !string.IsNullOrEmpty(item.Remove));

            // Directories are not converted to globs in the result because we did not write the directory
            configRemoveContentItem.Removes()
                .Should().BeEquivalentTo("root", "src", "rootfile.cs", @"src\file1.cs", @"src\file2.cs");

            configIncludeContentItem.Includes().Should().BeEquivalentTo("root", "src", "rootfile.cs");
            configIncludeContentItem.Excludes()
                .Should().BeEquivalentTo("rootfile.cs", "someotherfile.cs", @"src\file2.cs");

            configIncludeContentItem2.Includes().Should().BeEquivalentTo(@"src\file1.cs", @"src\file2.cs");
            configIncludeContentItem2.Excludes().Should().BeEquivalentTo(@"src\file2.cs");
        }

        [Fact]
        public void Configuration_buildOptions_which_have_mappings_to_directory_add_link_metadata_with_item_metadata()
        {
            var mockProj = RunConfigurationsAndBuildOptionsRuleOnPj(@"
                {
                    ""configurations"": {
                        ""testconfig"": {
                            ""buildOptions"": {
                                ""copyToOutput"": {
                                    ""mappings"": {
                                        ""/some/dir/"" : {
                                            ""include"": [""src"", ""root""],
                                            ""exclude"": [""src"", ""rootfile.cs""],
                                            ""includeFiles"": [""src/file1.cs"", ""src/file2.cs""],
                                            ""excludeFiles"": [""src/file2.cs""]
                                        }
                                    }
                                }
                            }
                        }
                    }
                }");

            var contentItems = mockProj.Items.Where(item => item.ItemType == "Content");

            contentItems.Count().Should().Be(2);
            contentItems.Where(i => i.ConditionChain().Count() == 1).Should().HaveCount(2);

            var configIncludeContentItem = contentItems.First(
                item => item.ConditionChain().Count() > 0
                    && item.Include.Contains("root"));

            var configIncludeContentItem2 = contentItems.First(
                item => item.ConditionChain().Count() > 0
                    && item.Include.Contains(@"src\file1.cs"));

            configIncludeContentItem.Includes().Should().BeEquivalentTo("root", "src");
            configIncludeContentItem.Excludes()
                .Should().BeEquivalentTo("rootfile.cs", "src", @"src\file2.cs");

            configIncludeContentItem.GetMetadataWithName("Link").Should().NotBeNull();
            configIncludeContentItem.GetMetadataWithName("Link").Value.Should().Be("/some/dir/%(FileName)%(Extension)");

            configIncludeContentItem.GetMetadataWithName("CopyToOutputDirectory").Should().NotBeNull();
            configIncludeContentItem.GetMetadataWithName("CopyToOutputDirectory").Value.Should().Be("PreserveNewest");

            configIncludeContentItem2.Includes().Should().BeEquivalentTo(@"src\file1.cs", @"src\file2.cs");
            configIncludeContentItem2.Excludes().Should().BeEquivalentTo(@"src\file2.cs");

            configIncludeContentItem2.GetMetadataWithName("Link").Should().NotBeNull();
            configIncludeContentItem2.GetMetadataWithName("Link").Value.Should().Be("/some/dir/%(FileName)%(Extension)");

            configIncludeContentItem2.GetMetadataWithName("CopyToOutputDirectory").Should().NotBeNull();
            configIncludeContentItem2.GetMetadataWithName("CopyToOutputDirectory").Value.Should().Be("PreserveNewest");
        }

        [Fact]
        public void Configuration_buildOptions_which_have_mappings_overlapping_with_includes_in_same_configuration_merged_items_have_Link_metadata()
        {
            var mockProj = RunConfigurationsAndBuildOptionsRuleOnPj(@"
                {
                    ""configurations"": {
                        ""testconfig"": {
                            ""buildOptions"": {
                                ""copyToOutput"": {
                                    ""include"": [""src"", ""root""],
                                    ""exclude"": [""src"", ""rootfile.cs""],
                                    ""includeFiles"": [""src/file1.cs""],
                                    ""excludeFiles"": [""src/file2.cs""],
                                    ""mappings"": {
                                        ""/some/dir/"" : {
                                            ""include"": [""src""],
                                            ""exclude"": [""src"", ""src/rootfile.cs""]
                                        }
                                    }
                                }
                            }
                        }
                    }
                }");

            var contentItems = mockProj.Items.Where(item => item.ItemType == "Content");

            contentItems.Count().Should().Be(3);
            contentItems.Where(i => i.ConditionChain().Count() == 1).Should().HaveCount(3);

            var configIncludeContentItem = contentItems.First(
                item => item.ConditionChain().Count() > 0
                    && item.Include == "root");
            
            var configIncludeContentItem2 = contentItems.First(
                item => item.ConditionChain().Count() > 0
                    && item.Include == "src");

            var configIncludeContentItem3 = contentItems.First(
                item => item.ConditionChain().Count() > 0
                    && item.Include.Contains(@"src\file1.cs"));

            // Directories are not converted to globs in the result because we did not write the directory

            configIncludeContentItem.Includes().Should().BeEquivalentTo("root");
            configIncludeContentItem.Excludes()
                .Should().BeEquivalentTo("rootfile.cs", "src", @"src\file2.cs");

            configIncludeContentItem.GetMetadataWithName("Link").Should().BeNull();
            configIncludeContentItem.GetMetadataWithName("CopyToOutputDirectory").Should().NotBeNull();
            configIncludeContentItem.GetMetadataWithName("CopyToOutputDirectory").Value.Should().Be("PreserveNewest");

            configIncludeContentItem2.Include.Should().Be("src");
            configIncludeContentItem2.Excludes().Should().BeEquivalentTo("src", "rootfile.cs", @"src\rootfile.cs", @"src\file2.cs");

            configIncludeContentItem2.GetMetadataWithName("Link").Should().NotBeNull();
            configIncludeContentItem2.GetMetadataWithName("Link").Value.Should().Be("/some/dir/%(FileName)%(Extension)");
            configIncludeContentItem2.GetMetadataWithName("CopyToOutputDirectory").Should().NotBeNull();
            configIncludeContentItem2.GetMetadataWithName("CopyToOutputDirectory").Value.Should().Be("PreserveNewest");

            configIncludeContentItem3.Includes().Should().BeEquivalentTo(@"src\file1.cs");
            configIncludeContentItem3.Exclude.Should().Be(@"src\file2.cs");

            configIncludeContentItem3.GetMetadataWithName("Link").Should().BeNull();
            configIncludeContentItem3.GetMetadataWithName("CopyToOutputDirectory").Should().NotBeNull();
            configIncludeContentItem3.GetMetadataWithName("CopyToOutputDirectory").Value.Should().Be("PreserveNewest");
        }
        
        [Fact]
        public void Configuration_buildOptions_which_have_mappings_overlapping_with_includes_in_root_buildoptions_has_remove()
        {
            var mockProj = RunConfigurationsAndBuildOptionsRuleOnPj(@"
                {
                    ""buildOptions"" : {
                        ""copyToOutput"": {
                            ""include"": [""src"", ""root""],
                            ""exclude"": [""src"", ""rootfile.cs""],
                            ""includeFiles"": [""src/file1.cs"", ""src/file2.cs""],
                            ""excludeFiles"": [""src/file2.cs""]
                        }
                    },
                    ""configurations"": {
                        ""testconfig"": {
                            ""buildOptions"": {
                                ""copyToOutput"": {
                                    ""mappings"": {
                                        ""/some/dir/"" : {
                                            ""include"": [""src""],
                                            ""exclude"": [""src"", ""rootfile.cs""]
                                        }
                                    }
                                }
                            }
                        }
                    }
                }");

            var contentItems = mockProj.Items.Where(item => item.ItemType == "Content");

            contentItems.Count().Should().Be(4);
            
            var rootBuildOptionsContentItems = contentItems.Where(i => i.ConditionChain().Count() == 0).ToList();
            rootBuildOptionsContentItems.Count().Should().Be(2);
            foreach (var buildOptionContentItem in rootBuildOptionsContentItems)
            {
                buildOptionContentItem.GetMetadataWithName("Link").Should().BeNull();
                buildOptionContentItem.GetMetadataWithName("CopyToOutputDirectory").Value.Should().Be("PreserveNewest");
            }

            var configItems = contentItems.Where(i => i.ConditionChain().Count() == 1);
            configItems.Should().HaveCount(2);

            var configIncludeContentItem = contentItems.First(
                item => item.ConditionChain().Count() > 0
                    && item.Include.Contains("src"));

            var configRemoveContentItem = contentItems.First(
                item => item.ConditionChain().Count() > 0
                    && !string.IsNullOrEmpty(item.Remove));

            configIncludeContentItem.Include.Should().Be("src");

            configIncludeContentItem.GetMetadataWithName("Link").Should().NotBeNull();
            configIncludeContentItem.GetMetadataWithName("Link").Value.Should().Be("/some/dir/%(FileName)%(Extension)");

            configIncludeContentItem.GetMetadataWithName("CopyToOutputDirectory").Should().NotBeNull();
            configIncludeContentItem.GetMetadataWithName("CopyToOutputDirectory").Value.Should().Be("PreserveNewest");

            configRemoveContentItem.Remove.Should().Be("src");
        }

        [Fact]
        public void Configuration_buildOptions_which_have_mappings_overlapping_with_includes_in_same_configuration_and_root_buildOptions_have_removes_and_Link_metadata_and_encompassed_items_are_merged()
        {
            var mockProj = RunConfigurationsAndBuildOptionsRuleOnPj(@"
                {
                    ""buildOptions"" : {
                        ""copyToOutput"": {
                            ""include"": [""src"", ""root""],
                            ""exclude"": [""src"", ""rootfile.cs""],
                            ""includeFiles"": [""src/file1.cs""],
                            ""excludeFiles"": [""src/file2.cs""]
                        }
                    },
                    ""configurations"": {
                        ""testconfig"": {
                            ""buildOptions"": {
                                ""copyToOutput"": {
                                    ""include"": [""src"", ""root""],
                                    ""exclude"": [""src"", ""rootfile.cs""],
                                    ""includeFiles"": [""src/file3.cs""],
                                    ""excludeFiles"": [""src/file2.cs""],
                                    ""mappings"": {
                                        ""/some/dir/"" : {
                                            ""include"": [""src""],
                                            ""exclude"": [""src"", ""src/rootfile.cs""]
                                        }
                                    }
                                }
                            }
                        }
                    }
                }");

            var contentItems = mockProj.Items.Where(item => item.ItemType == "Content");

            contentItems.Count().Should().Be(5);
            contentItems.Where(i => i.ConditionChain().Count() == 1).Should().HaveCount(3);

            var rootBuildOptionsContentItems = contentItems.Where(i => i.ConditionChain().Count() == 0).ToList();
            rootBuildOptionsContentItems.Count().Should().Be(2);
            foreach (var buildOptionContentItem in rootBuildOptionsContentItems)
            {
                buildOptionContentItem.GetMetadataWithName("Link").Should().BeNull();
                buildOptionContentItem.GetMetadataWithName("CopyToOutputDirectory").Value.Should().Be("PreserveNewest");
            }

            var configIncludeEncompassedItem = contentItems.FirstOrDefault(
                item => item.ConditionChain().Count() > 0
                    && item.Include == "root");
            configIncludeEncompassedItem.Should().BeNull();
            
            var configIncludeContentItem = contentItems.First(
                item => item.ConditionChain().Count() > 0
                    && item.Include == "src");

            var configIncludeContentItem2 = contentItems.First(
                item => item.ConditionChain().Count() > 0
                    && item.Include.Contains(@"src\file3.cs"));

            var configRemoveContentItem = contentItems.First(
                item => item.ConditionChain().Count() > 0
                    && !string.IsNullOrEmpty(item.Remove));

            configIncludeContentItem.Include.Should().Be("src");
            configIncludeContentItem.Excludes().Should().BeEquivalentTo("src", "rootfile.cs", @"src\rootfile.cs", @"src\file2.cs");

            configIncludeContentItem.GetMetadataWithName("Link").Should().NotBeNull();
            configIncludeContentItem.GetMetadataWithName("Link").Value.Should().Be("/some/dir/%(FileName)%(Extension)");
            configIncludeContentItem.GetMetadataWithName("CopyToOutputDirectory").Should().NotBeNull();
            configIncludeContentItem.GetMetadataWithName("CopyToOutputDirectory").Value.Should().Be("PreserveNewest");

            configIncludeContentItem2.Includes().Should().BeEquivalentTo(@"src\file3.cs");
            configIncludeContentItem2.Exclude.Should().Be(@"src\file2.cs");

            configIncludeContentItem2.GetMetadataWithName("Link").Should().BeNull();
            configIncludeContentItem2.GetMetadataWithName("CopyToOutputDirectory").Should().NotBeNull();
            configIncludeContentItem2.GetMetadataWithName("CopyToOutputDirectory").Value.Should().Be("PreserveNewest");

            configRemoveContentItem.Removes().Should().BeEquivalentTo("src");
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
