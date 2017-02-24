using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Build.Construction;
using Microsoft.DotNet.ProjectJsonMigration;
using Microsoft.DotNet.Internal.ProjectModel;
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
        public void ConfigurationBuildOptionsProduceExpectedPropertiesInAGroupWithACondition()
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
        public void FrameworksBuildOptionsProduceExpectedPropertiesInAGroupWithACondition()
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
        public void ItDoesNotAddADefineForTheFramework()
        {
            var mockProj = RunConfigurationsRuleOnPj(@"
                {
                    ""frameworks"": {
                        ""netcoreapp1.0"": {
                        }
                    }
                }");

            mockProj.Properties.Count(
                prop => prop.Name == "DefineConstants" && prop.Value.Contains("NETCOREAPP10")).Should().Be(0);
        }

        [Fact]
        public void ItDoesNotAddADefineForReleaseConfiguration()
        {
            var mockProj = RunRulesOnPj(@"
                {
                    ""frameworks"": {
                        ""netcoreapp1.0"": {
                        }
                    }
                }",
                new IMigrationRule[]
                {
                    new AddDefaultsToProjectRule(),
                    new MigrateConfigurationsRule(),
                    new RemoveDefaultsFromProjectRule()
                });

            mockProj.Properties.Count(
                prop => prop.Name == "DefineConstants" && prop.Value.Contains("Release")).Should().Be(0);
        }

        [Fact]
        public void ItDoesNotAddADefineForDebugConfiguration()
        {
            var mockProj = RunRulesOnPj(@"
                {
                    ""frameworks"": {
                        ""netcoreapp1.0"": {
                        }
                    }
                }",
                new IMigrationRule[]
                {
                    new AddDefaultsToProjectRule(),
                    new MigrateConfigurationsRule(),
                    new RemoveDefaultsFromProjectRule()
                });

            mockProj.Properties.Count(
                prop => prop.Name == "DefineConstants" && prop.Value.Contains("Debug")).Should().Be(0);
        }

        [Fact]
        public void ConfigurationBuildOptionsPropertiesAreNotWrittenWhenTheyOverlapWithBuildOptions()
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
        public void ConfigurationBuildOptionsIncludesAndRemoveAreWrittenWhenTheyDifferFromBaseBuildOptions()
        {
            var mockProj = RunConfigurationsAndBuildOptionsRuleOnPj(@"
                {
                    ""buildOptions"": {
                        ""copyToOutput"": {
                            ""include"": [""src""],
                            ""exclude"": [""anothersource"", ""rootfile.cs""],
                            ""includeFiles"": [""src/file1.cs"", ""src/file2.cs""],
                            ""excludeFiles"": [""anothersource/file2.cs""]
                        }
                    },
                    ""configurations"": {
                        ""testconfig"": {
                            ""buildOptions"": {
                                ""copyToOutput"": {
                                    ""include"": [""root"", ""src"", ""rootfile.cs""],
                                    ""exclude"": [""anothersource"", ""root/rootfile.cs""],
                                    ""includeFiles"": [""src/file1.cs"", ""src/file2.cs""],
                                    ""excludeFiles"": [""src/file3.cs""]
                                }
                            }
                        }
                    }
                }");

            var contentItems = mockProj.Items.Where(item => item.ItemType == "None");

            contentItems.Count().Should().Be(8);

            contentItems.Where(i => i.ConditionChain().Count() == 0).Should().HaveCount(4);

            contentItems.Where(i =>
                i.ConditionChain().Count() == 0 &&
                i.Metadata.Any(m => m.Value == "PreserveNewest")).Should().HaveCount(2);
            contentItems.Where(i =>
                i.ConditionChain().Count() == 0 &&
                i.Metadata.Any(m => m.Value == "Never")).Should().HaveCount(2);

            contentItems.Where(i => i.ConditionChain().Count() == 1).Should().HaveCount(4);
            contentItems.Where(i =>
                i.ConditionChain().Count() == 1 &&
                i.Metadata.Any(m => m.Value == "PreserveNewest")).Should().HaveCount(1);
            contentItems.Where(i =>
                i.ConditionChain().Count() == 1 &&
                i.Metadata.Any(m => m.Value == "Never")).Should().HaveCount(2);

            contentItems.First(i =>
                i.ConditionChain().Count() == 1 &&
                i.Metadata.Any(m => m.Value == "PreserveNewest") &&
                !string.IsNullOrEmpty(i.Update)).Update.Should().Be(@"root;rootfile.cs");

            contentItems.First(i =>
                i.ConditionChain().Count() == 1 &&
                i.Metadata.Any(m => m.Value == "Never") &&
                !string.IsNullOrEmpty(i.Update) &&
                i.Update.Contains(@"src\file3.cs")).Update.Should().Be(@"src\file3.cs");

            contentItems.First(i =>
                i.ConditionChain().Count() == 1 &&
                i.Metadata.Any(m => m.Value == "Never") &&
                !string.IsNullOrEmpty(i.Update) &&
                i.Update.Contains(@"root\rootfile.cs")).Update.Should().Be(@"root\rootfile.cs");

            contentItems.First(i =>
                i.ConditionChain().Count() == 0 &&
                i.Metadata.Any(m => m.Value == "PreserveNewest") &&
                !string.IsNullOrEmpty(i.Update) &&
                i.Update.Contains(@"src\file1.cs")).Update.Should().Be(@"src\file1.cs;src\file2.cs");

            contentItems.First(i =>
                i.ConditionChain().Count() == 0 &&
                i.Metadata.Any(m => m.Value == "PreserveNewest") &&
                !string.IsNullOrEmpty(i.Update) &&
                i.Update.Equals(@"src")).Update.Should().Be(@"src");

            contentItems.First(i =>
                i.ConditionChain().Count() == 0 &&
                i.Metadata.Any(m => m.Value == "Never") &&
                !string.IsNullOrEmpty(i.Update) &&
                i.Update.Equals(@"anothersource\file2.cs")).Update.Should().Be(@"anothersource\file2.cs");

            contentItems.First(i =>
                i.ConditionChain().Count() == 0 &&
                i.Metadata.Any(m => m.Value == "Never") &&
                !string.IsNullOrEmpty(i.Update) &&
                i.Update.Equals(@"anothersource;rootfile.cs")).Update.Should().Be(@"anothersource;rootfile.cs");
        }

        [Fact]
        public void ConfigurationBuildOptionsWhichHaveDifferentExcludesThanBuildOptionsOverwrites()
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
                                    ""include"": [""root"", ""anothersource"", ""rootfile.cs""],
                                    ""exclude"": [""rootfile.cs"", ""someotherfile.cs""],
                                    ""includeFiles"": [""src/file1.cs"", ""src/file2.cs""],
                                    ""excludeFiles"": [""src/file2.cs""]
                                }
                            }
                        }
                    }
                }");

            var contentItems = mockProj.Items.Where(item => item.ItemType == "None");

            contentItems.Count().Should().Be(9);
            contentItems.Where(i => i.ConditionChain().Count() == 0).Should().HaveCount(4);
            contentItems.Where(i => i.ConditionChain().Count() == 1).Should().HaveCount(5);

            contentItems.Where(i =>
                i.ConditionChain().Count() == 0 &&
                i.Metadata.Any(m => m.Value == "PreserveNewest")).Should().HaveCount(2);
            contentItems.Where(i =>
                i.ConditionChain().Count() == 0 &&
                i.Metadata.Any(m => m.Value == "Never")).Should().HaveCount(2);

            contentItems.Where(i =>
                i.ConditionChain().Count() == 1 &&
                i.Metadata.Any(m => m.Value == "PreserveNewest")).Should().HaveCount(1);
            contentItems.Where(i =>
                i.ConditionChain().Count() == 1 &&
                i.Metadata.Any(m => m.Value == "Never")).Should().HaveCount(3);

            contentItems.First(i =>
                i.ConditionChain().Count() == 0 &&
                i.Metadata.Any(m => m.Value == "PreserveNewest") &&
                !string.IsNullOrEmpty(i.Update) &&
                i.Update.Contains(@"src\file1.cs")).Update.Should().Be(@"src\file1.cs;src\file2.cs");

            contentItems.First(i =>
                i.ConditionChain().Count() == 0 &&
                i.Metadata.Any(m => m.Value == "PreserveNewest") &&
                !string.IsNullOrEmpty(i.Update) &&
                i.Update.Equals(@"src")).Update.Should().Be(@"src");

            contentItems.First(i =>
                i.ConditionChain().Count() == 0 &&
                i.Metadata.Any(m => m.Value == "Never") &&
                !string.IsNullOrEmpty(i.Update) &&
                i.Update.Equals(@"src\file3.cs")).Update.Should().Be(@"src\file3.cs");

            contentItems.First(i =>
                i.ConditionChain().Count() == 0 &&
                i.Metadata.Any(m => m.Value == "Never") &&
                !string.IsNullOrEmpty(i.Update) &&
                i.Update.Equals(@"rootfile.cs")).Update.Should().Be(@"rootfile.cs");

            contentItems.First(i =>
                i.ConditionChain().Count() == 1 &&
                i.Metadata.Any(m => m.Value == "PreserveNewest") &&
                !string.IsNullOrEmpty(i.Update) &&
                i.Update.Contains(@"root;anothersource")).Update.Should().Be(@"root;anothersource");

            contentItems.First(i =>
                i.ConditionChain().Count() == 1 &&
                i.Metadata.Any(m => m.Value == "Never") &&
                !string.IsNullOrEmpty(i.Update) &&
                i.Update.Contains(@"src\file2.cs")).Update.Should().Be(@"src\file2.cs");

            contentItems.First(i =>
                i.ConditionChain().Count() == 1 &&
                i.Metadata.Any(m => m.Value == "Never") &&
                !string.IsNullOrEmpty(i.Update) &&
                i.Update.Contains(@"rootfile.cs")).Update.Should().Be(@"rootfile.cs");

            contentItems.First(i =>
                i.ConditionChain().Count() == 1 &&
                i.Metadata.Any(m => m.Value == "Never") &&
                !string.IsNullOrEmpty(i.Update) &&
                i.Update.Contains(@"someotherfile.cs")).Update.Should().Be(@"someotherfile.cs");
        }

        [Fact]
        public void ConfigurationBuildOptionsWhichHaveMappingsToDirectoryAddLinkMetadataWithItemMetadata()
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

            var contentItems = mockProj.Items.Where(item => item.ItemType == "None");

            contentItems.Count().Should().Be(5);
            contentItems.Where(i => i.ConditionChain().Count() == 1).Should().HaveCount(5);

            var configIncludeContentItem = contentItems.First(item => item.Update.Contains("root"));
            var configIncludeContentItem2 = contentItems.First(item => item.Update.Contains(@"src\file1.cs"));
            var configIncludeContentItem3 = contentItems.First(item => item.Update.Contains(@"src\file2.cs"));
            var configIncludeContentItem4 = contentItems.First(item => item.Update.Equals(@"src"));
            var configIncludeContentItem5 = contentItems.First(item => item.Update.Contains(@"rootfile.cs"));

            configIncludeContentItem.Updates().Should().BeEquivalentTo("root");
            configIncludeContentItem.GetMetadataWithName("Link").Should().NotBeNull();
            configIncludeContentItem.GetMetadataWithName("Link").Value.Should().Be("/some/dir/%(FileName)%(Extension)");
            configIncludeContentItem.GetMetadataWithName("CopyToOutputDirectory").Should().NotBeNull();
            configIncludeContentItem.GetMetadataWithName("CopyToOutputDirectory").Value.Should().Be("PreserveNewest");

            configIncludeContentItem2.Updates().Should().BeEquivalentTo(@"src\file1.cs");
            configIncludeContentItem2.GetMetadataWithName("Link").Should().NotBeNull();
            configIncludeContentItem2.GetMetadataWithName("Link").Value.Should().Be("/some/dir/%(FileName)%(Extension)");
            configIncludeContentItem2.GetMetadataWithName("CopyToOutputDirectory").Should().NotBeNull();
            configIncludeContentItem2.GetMetadataWithName("CopyToOutputDirectory").Value.Should().Be("PreserveNewest");

            configIncludeContentItem3.Updates().Should().BeEquivalentTo(@"src\file2.cs");
            configIncludeContentItem3.GetMetadataWithName("Link").Should().NotBeNull();
            configIncludeContentItem3.GetMetadataWithName("Link").Value.Should().Be("/some/dir/%(FileName)%(Extension)");
            configIncludeContentItem3.GetMetadataWithName("CopyToOutputDirectory").Should().NotBeNull();
            configIncludeContentItem3.GetMetadataWithName("CopyToOutputDirectory").Value.Should().Be("Never");

            configIncludeContentItem4.Updates().Should().BeEquivalentTo(@"src");
            configIncludeContentItem4.GetMetadataWithName("Link").Should().NotBeNull();
            configIncludeContentItem4.GetMetadataWithName("Link").Value.Should().Be("/some/dir/%(FileName)%(Extension)");
            configIncludeContentItem4.GetMetadataWithName("CopyToOutputDirectory").Should().NotBeNull();
            configIncludeContentItem4.GetMetadataWithName("CopyToOutputDirectory").Value.Should().Be("Never");

            configIncludeContentItem5.Updates().Should().BeEquivalentTo(@"rootfile.cs");
            configIncludeContentItem5.GetMetadataWithName("Link").Should().NotBeNull();
            configIncludeContentItem5.GetMetadataWithName("Link").Value.Should().Be("/some/dir/%(FileName)%(Extension)");
            configIncludeContentItem5.GetMetadataWithName("CopyToOutputDirectory").Should().NotBeNull();
            configIncludeContentItem5.GetMetadataWithName("CopyToOutputDirectory").Value.Should().Be("Never");
        }

        [Fact]
        public void ConfigurationBuildOptionsWhichHaveMappingsOverlappingWithIncludesInSameConfigurationMergedItemsHaveLinkMetadata()
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

            var contentItems = mockProj.Items.Where(item => item.ItemType == "None");

            contentItems.Count().Should().Be(6);
            contentItems.Where(i => i.ConditionChain().Count() == 1).Should().HaveCount(6);

            var configIncludeContentItem = contentItems.First(
                item => item.ConditionChain().Count() > 0
                    && item.Update == "root");
            
            var configIncludeContentItem2 = contentItems.First(
                item => item.ConditionChain().Count() > 0
                    && item.Update == "src");

            var configIncludeContentItem3 = contentItems.First(
                item => item.ConditionChain().Count() > 0
                    && item.Update.Contains(@"src\file1.cs"));

            var configIncludeContentItem4 = contentItems.First(
                item => item.ConditionChain().Count() > 0
                    && item.Update.Contains(@"src\file2.cs"));

            var configIncludeContentItem5 = contentItems.First(
                item => item.ConditionChain().Count() > 0
                    && item.Update.Equals(@"rootfile.cs"));

            var configIncludeContentItem6 = contentItems.First(
                item => item.ConditionChain().Count() > 0
                    && item.Update.Equals(@"src\rootfile.cs"));

            configIncludeContentItem.Updates().Should().BeEquivalentTo("root");
            configIncludeContentItem.GetMetadataWithName("Link").Should().BeNull();
            configIncludeContentItem.GetMetadataWithName("CopyToOutputDirectory").Should().NotBeNull();
            configIncludeContentItem.GetMetadataWithName("CopyToOutputDirectory").Value.Should().Be("PreserveNewest");

            configIncludeContentItem2.Update.Should().Be("src");
            configIncludeContentItem2.GetMetadataWithName("Link").Should().NotBeNull();
            configIncludeContentItem2.GetMetadataWithName("Link").Value.Should().Be("/some/dir/%(FileName)%(Extension)");
            configIncludeContentItem2.GetMetadataWithName("CopyToOutputDirectory").Should().NotBeNull();
            configIncludeContentItem2.GetMetadataWithName("CopyToOutputDirectory").Value.Should().Be("Never");

            configIncludeContentItem3.Updates().Should().BeEquivalentTo(@"src\file1.cs");
            configIncludeContentItem3.GetMetadataWithName("Link").Should().BeNull();
            configIncludeContentItem3.GetMetadataWithName("CopyToOutputDirectory").Should().NotBeNull();
            configIncludeContentItem3.GetMetadataWithName("CopyToOutputDirectory").Value.Should().Be("PreserveNewest");

            configIncludeContentItem4.Updates().Should().BeEquivalentTo(@"src\file2.cs");
            configIncludeContentItem4.GetMetadataWithName("Link").Should().BeNull();
            configIncludeContentItem4.GetMetadataWithName("CopyToOutputDirectory").Should().NotBeNull();
            configIncludeContentItem4.GetMetadataWithName("CopyToOutputDirectory").Value.Should().Be("Never");

            configIncludeContentItem5.Updates().Should().BeEquivalentTo(@"rootfile.cs");
            configIncludeContentItem5.GetMetadataWithName("Link").Should().BeNull();
            configIncludeContentItem5.GetMetadataWithName("CopyToOutputDirectory").Should().NotBeNull();
            configIncludeContentItem5.GetMetadataWithName("CopyToOutputDirectory").Value.Should().Be("Never");

            configIncludeContentItem6.Updates().Should().BeEquivalentTo(@"src\rootfile.cs");
            configIncludeContentItem6.GetMetadataWithName("Link").Value.Should().Be("/some/dir/%(FileName)%(Extension)");
            configIncludeContentItem6.GetMetadataWithName("CopyToOutputDirectory").Should().NotBeNull();
            configIncludeContentItem6.GetMetadataWithName("CopyToOutputDirectory").Value.Should().Be("Never");
        }
        
        [Fact]
        public void ConfigurationBuildOptionsWhichHaveMappingsOverlappingWithIncludesInRootBuildoptionsHasRemove()
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

            var contentItems = mockProj.Items.Where(item => item.ItemType == "None");

            contentItems.Count().Should().Be(8);
            
            var rootBuildOptionsContentItems = contentItems.Where(i => i.ConditionChain().Count() == 0).ToList();
            rootBuildOptionsContentItems.Count().Should().Be(5);
            foreach (var buildOptionContentItem in rootBuildOptionsContentItems)
            {
                buildOptionContentItem.GetMetadataWithName("Link").Should().BeNull();
            }

            contentItems.First(i =>
                i.ConditionChain().Count() == 0 &&
                i.Metadata.Any(m => m.Value == "PreserveNewest") &&
                !string.IsNullOrEmpty(i.Update) &&
                i.Update.Equals(@"src\file1.cs")).Update.Should().Be(@"src\file1.cs");

            contentItems.First(i =>
                i.ConditionChain().Count() == 0 &&
                i.Metadata.Any(m => m.Value == "PreserveNewest") &&
                !string.IsNullOrEmpty(i.Update) &&
                i.Update.Equals(@"root")).Update.Should().Be(@"root");

            contentItems.First(i =>
                i.ConditionChain().Count() == 0 &&
                i.Metadata.Any(m => m.Value == "Never") &&
                !string.IsNullOrEmpty(i.Update) &&
                i.Update.Equals(@"src\file2.cs")).Update.Should().Be(@"src\file2.cs");

            contentItems.First(i =>
                i.ConditionChain().Count() == 0 &&
                i.Metadata.Any(m => m.Value == "Never") &&
                !string.IsNullOrEmpty(i.Update) &&
                i.Update.Equals(@"src")).Update.Should().Be(@"src");

            contentItems.First(i =>
                i.ConditionChain().Count() == 0 &&
                i.Metadata.Any(m => m.Value == "Never") &&
                !string.IsNullOrEmpty(i.Update) &&
                i.Update.Equals(@"rootfile.cs")).Update.Should().Be(@"rootfile.cs");

            var configItems = contentItems.Where(i => i.ConditionChain().Count() == 1);
            configItems.Should().HaveCount(3);

            var configIncludeContentItem = contentItems.First(
                item => item.ConditionChain().Count() > 0
                    && item.Update.Contains("src"));

            var configRemoveContentItem = contentItems.First(
                item => item.ConditionChain().Count() > 0
                    && !string.IsNullOrEmpty(item.Remove));

            configIncludeContentItem.Update.Should().Be("src");

            configIncludeContentItem.GetMetadataWithName("Link").Should().NotBeNull();
            configIncludeContentItem.GetMetadataWithName("Link").Value.Should().Be("/some/dir/%(FileName)%(Extension)");

            configIncludeContentItem.GetMetadataWithName("CopyToOutputDirectory").Should().NotBeNull();
            configIncludeContentItem.GetMetadataWithName("CopyToOutputDirectory").Value.Should().Be("Never");

            configRemoveContentItem.Remove.Should().Be("src;rootfile.cs");
        }

        [Fact]
        public void ConfigurationBuildOptionsWhichHaveMappingsOverlappingWithIncludesInSameConfigurationAndRootBuildOptionsHaveRemovesAndLinkMetadataAndEncompassedItemsAreMerged()
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

            var contentItems = mockProj.Items.Where(item => item.ItemType == "None");

            contentItems.Count().Should().Be(9);
            contentItems.Where(i => i.ConditionChain().Count() == 1).Should().HaveCount(4);

            var rootBuildOptionsContentItems = contentItems.Where(i => i.ConditionChain().Count() == 0).ToList();
            rootBuildOptionsContentItems.Count().Should().Be(5);
            foreach (var buildOptionContentItem in rootBuildOptionsContentItems)
            {
                buildOptionContentItem.GetMetadataWithName("Link").Should().BeNull();
            }

            var configIncludeEncompassedItem = contentItems.FirstOrDefault(
                item => item.ConditionChain().Count() > 0
                    && item.Update == "root");
            configIncludeEncompassedItem.Should().BeNull();
            
            var configIncludeContentItem = contentItems.First(
                item => item.ConditionChain().Count() > 0
                    && item.Update == "src");

            var configIncludeContentItem2 = contentItems.First(
                item => item.ConditionChain().Count() > 0
                    && item.Update.Contains(@"src\file3.cs"));

            var configRemoveContentItem = contentItems.First(
                item => item.ConditionChain().Count() > 0
                    && !string.IsNullOrEmpty(item.Remove));

            configIncludeContentItem.Update.Should().Be("src");
            configIncludeContentItem.GetMetadataWithName("Link").Should().NotBeNull();
            configIncludeContentItem.GetMetadataWithName("Link").Value.Should().Be("/some/dir/%(FileName)%(Extension)");
            configIncludeContentItem.GetMetadataWithName("CopyToOutputDirectory").Should().NotBeNull();
            configIncludeContentItem.GetMetadataWithName("CopyToOutputDirectory").Value.Should().Be("Never");

            configIncludeContentItem2.Updates().Should().BeEquivalentTo(@"src\file3.cs");
            configIncludeContentItem2.GetMetadataWithName("Link").Should().BeNull();
            configIncludeContentItem2.GetMetadataWithName("CopyToOutputDirectory").Should().NotBeNull();
            configIncludeContentItem2.GetMetadataWithName("CopyToOutputDirectory").Value.Should().Be("PreserveNewest");

            configRemoveContentItem.Removes().Should().BeEquivalentTo("src");
        }

        private ProjectRootElement RunConfigurationsRuleOnPj(string s, string testDirectory = null)
        {
            testDirectory = testDirectory ?? Temp.CreateDirectory().Path;
            return RunRulesOnPj(s, new[] { new MigrateConfigurationsRule() }, testDirectory);
        }

        private ProjectRootElement RunConfigurationsAndBuildOptionsRuleOnPj(string s, string testDirectory = null)
        {
            return RunRulesOnPj(
                s, 
                new IMigrationRule[]
                {
                    new MigrateBuildOptionsRule(),
                    new MigrateConfigurationsRule()
                },
                testDirectory);
        }

        private ProjectRootElement RunRulesOnPj(string s, IMigrationRule[] migrationRules, string testDirectory = null)
        {
            testDirectory = testDirectory ?? Temp.CreateDirectory().Path;
            return TemporaryProjectFileRuleRunner.RunRules(migrationRules, s, testDirectory);
        }
    }
}
