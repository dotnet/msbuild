// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Construction;
using Microsoft.DotNet.Tools.Test.Utilities;
using System.IO;
using System.Linq;
using Xunit;
using FluentAssertions;
using Microsoft.DotNet.ProjectJsonMigration.Rules;
using System;

namespace Microsoft.DotNet.ProjectJsonMigration.Tests
{
    public class GivenThatIWantToMigratePackOptions : TestBase
    {
        [Fact]
        public void ItDoesNotMigrateSummary()
        {
            var mockProj = RunPackOptionsRuleOnPj(@"
                {
                    ""packOptions"": {
                        ""summary"": ""Some not important summary""
                    }                    
                }");
            
            EmitsOnlyAlwaysEmittedPackOptionsProperties(mockProj);            
        }

        [Fact]
        public void ItDoesNotMigrateOwner()
        {
            var mockProj = RunPackOptionsRuleOnPj(@"
                {
                    ""packOptions"": {
                        ""owner"": ""Some not important owner""
                    }                    
                }");

            EmitsOnlyAlwaysEmittedPackOptionsProperties(mockProj);
        }

        [Fact]
        public void MigratingEmptyTagsDoesNotPopulatePackageTags()
        {
            var mockProj = RunPackOptionsRuleOnPj(@"
                {
                    ""packOptions"": {
                        ""tags"": []
                    }                    
                }");

            mockProj.Properties.Count(p => p.Name == "PackageTags").Should().Be(0);
        }

        [Fact]
        public void MigratingTagsPopulatesPackageTagsSemicolonDelimited()
        {
            var mockProj = RunPackOptionsRuleOnPj(@"
                {
                    ""packOptions"": {
                        ""tags"": [""hyperscale"", ""cats""]
                    }                    
                }");

            mockProj.Properties.Count(p => p.Name == "PackageTags").Should().Be(1);
            mockProj.Properties.First(p => p.Name == "PackageTags").Value.Should().Be("hyperscale;cats");
        }

        [Fact]
        public void MigratingReleaseNotesPopulatesPackageReleaseNotes()
        {
            var mockProj = RunPackOptionsRuleOnPj(@"
                {
                    ""packOptions"": {
                        ""releaseNotes"": ""Some release notes value.""
                    }                    
                }");

            mockProj.Properties.Count(p => p.Name == "PackageReleaseNotes").Should().Be(1);
            mockProj.Properties.First(p => p.Name == "PackageReleaseNotes").Value.Should()
                .Be("Some release notes value.");
        }

        [Fact]
        public void MigratingIconUrlPopulatesPackageIconUrl()
        {
            var mockProj = RunPackOptionsRuleOnPj(@"
                {
                    ""packOptions"": {
                        ""iconUrl"": ""http://www.mylibrary.gov/favicon.ico""
                    }                    
                }");

            mockProj.Properties.Count(p => p.Name == "PackageIconUrl").Should().Be(1);
            mockProj.Properties.First(p => p.Name == "PackageIconUrl").Value.Should()
                .Be("http://www.mylibrary.gov/favicon.ico");
        }

        [Fact]
        public void MigratingProjectUrlPopulatesPackageProjectUrl()
        {
            var mockProj = RunPackOptionsRuleOnPj(@"
                {
                    ""packOptions"": {
                        ""projectUrl"": ""http://www.url.to.library.com""
                    }                    
                }");

            mockProj.Properties.Count(p => p.Name == "PackageProjectUrl").Should().Be(1);
            mockProj.Properties.First(p => p.Name == "PackageProjectUrl").Value.Should()
                .Be("http://www.url.to.library.com");
        }

        [Fact]
        public void MigratingLicenseUrlPopulatesPackageLicenseUrl()
        {
            var mockProj = RunPackOptionsRuleOnPj(@"
                {
                    ""packOptions"": {
                        ""licenseUrl"": ""http://www.url.to.library.com/licence""
                    }                    
                }");

            mockProj.Properties.Count(p => p.Name == "PackageLicenseUrl").Should().Be(1);
            mockProj.Properties.First(p => p.Name == "PackageLicenseUrl").Value.Should()
                .Be("http://www.url.to.library.com/licence");
        }

        [Fact]
        public void MigratingRequireLicenseAcceptancePopulatesPackageRequireLicenseAcceptance()
        {
            var mockProj = RunPackOptionsRuleOnPj(@"
                {
                    ""packOptions"": {
                        ""requireLicenseAcceptance"": ""true""
                    }                    
                }");

            mockProj.Properties.Count(p => p.Name == "PackageRequireLicenseAcceptance").Should().Be(1);
            mockProj.Properties.First(p => p.Name == "PackageRequireLicenseAcceptance").Value.Should().Be("true");
        }

        [Fact]
        public void MigratingRequireLicenseAcceptancePopulatesPackageRequireLicenseAcceptanceEvenIfItsValueIsFalse()
        {
            var mockProj = RunPackOptionsRuleOnPj(@"
                {
                    ""packOptions"": {
                        ""requireLicenseAcceptance"": ""false""
                    }                    
                }");

            mockProj.Properties.Count(p => p.Name == "PackageRequireLicenseAcceptance").Should().Be(1);
            mockProj.Properties.First(p => p.Name == "PackageRequireLicenseAcceptance").Value.Should().Be("false");
        }

        [Fact]
        public void MigratingRepositoryTypePopulatesRepositoryType()
        {
            var mockProj = RunPackOptionsRuleOnPj(@"
                {
                    ""packOptions"": {
                        ""repository"": {
                            ""type"": ""git""
                        }
                    }                    
                }");

            mockProj.Properties.Count(p => p.Name == "RepositoryType").Should().Be(1);
            mockProj.Properties.First(p => p.Name == "RepositoryType").Value.Should().Be("git");
        }

        [Fact]
        public void MigratingRepositoryUrlPopulatesRepositoryUrl()
        {
            var mockProj = RunPackOptionsRuleOnPj(@"
                {
                    ""packOptions"": {
                        ""repository"": {
                            ""url"": ""http://github.com/dotnet/cli""
                        }
                    }
                }");

            mockProj.Properties.Count(p => p.Name == "RepositoryUrl").Should().Be(1);
            mockProj.Properties.First(p => p.Name == "RepositoryUrl").Value.Should().Be("http://github.com/dotnet/cli");
        }

        [Fact]
        public void MigratingFilesWithoutMappingsPopulatesContentWithSamePathAsIncludeAndPackTrue()
        {
            var mockProj = RunPackOptionsRuleOnPj(@"
                {
                    ""packOptions"": {
                        ""files"": {
                            ""include"": [""path/to/some/file.cs"", ""path/to/some/other/file.cs""]
                        }
                    }
                }");

            var contentItems = mockProj.Items
                .Where(item => item.ItemType.Equals("None", StringComparison.Ordinal))
                .Where(item => item.GetMetadataWithName("Pack").Value == "true");

            contentItems.Count().Should().Be(1);
            contentItems.First().Update.Should().Be(@"path\to\some\file.cs;path\to\some\other\file.cs");
        }

        [Fact]
        public void MigratingFilesWithExcludePopulatesNoneWithPackFalseForTheExcludedFiles()
        {
            var mockProj = RunPackOptionsRuleOnPj(@"
                {
                    ""packOptions"": {
                        ""files"": {
                            ""include"": [""path/to/some/file.cs"", ""path/to/some/other/file.cs""],
                            ""exclude"": [""path/to/file/to/exclude.cs""]
                        }
                    }
                }");

            foreach (var item in mockProj.Items.Where(i => i.ItemType.Equals("None", StringComparison.Ordinal)))
            {
                Console.WriteLine($"Update: {item.Update}, Include: {item.Include}, Remove: {item.Remove}");
                foreach(var meta in item.Metadata)
                {
                    Console.WriteLine($"\tMetadata: Name: {meta.Name}, Value: {meta.Value}");
                }

                foreach(var condition in item.ConditionChain())
                {
                    Console.WriteLine($"\tCondition: {condition}");
                }
            }

            var contentItemsToInclude = mockProj.Items
                .Where(item => item.ItemType.Equals("None", StringComparison.Ordinal))
                .Where(item => item.GetMetadataWithName("Pack").Value == "true");

            contentItemsToInclude.Count().Should().Be(1);
            contentItemsToInclude.First().Update.Should().Be(@"path\to\some\file.cs;path\to\some\other\file.cs");

            var contentItemsToExclude = mockProj.Items
                .Where(item => item.ItemType.Equals("None", StringComparison.Ordinal))
                .Where(item => item.GetMetadataWithName("Pack").Value == "false");

            contentItemsToExclude.Count().Should().Be(1);
            contentItemsToExclude.First().Update.Should().Be(@"path\to\file\to\exclude.cs");
        }

        [Fact]
        public void MigratingFilesWithMappingsPopulatesContentPackagePathMetadata()
        {
            var mockProj = RunPackOptionsRuleOnPj(@"
                {
                    ""packOptions"": {
                        ""files"": {
                            ""include"": [""path/to/some/file.cs""],
                            ""mappings"": {
                                ""some/other/path/file.cs"": ""path/to/some/file.cs""
                            }
                        }
                    }
                }");

            var contentItems = mockProj.Items
                .Where(item => item.ItemType.Equals("None", StringComparison.Ordinal))
                .Where(item =>
                    item.GetMetadataWithName("Pack").Value == "true" &&
                    item.GetMetadataWithName("PackagePath") != null);

            contentItems.Count().Should().Be(1);
            contentItems.First().Update.Should().Be(@"path\to\some\file.cs");
            contentItems.First().GetMetadataWithName("PackagePath").Value.Should().Be(
                Path.Combine("some", "other", "path"));
        }

        [Fact]
        public void MigratingFilesWithMappingsToRootPopulatesContentPackagePathMetadataButLeavesItEmpty()
        {
            var mockProj = RunPackOptionsRuleOnPj(@"
                {
                    ""packOptions"": {
                        ""files"": {
                            ""include"": [""path/to/some/file.cs""],
                            ""mappings"": {
                                "".file.cs"": ""path/to/some/file.cs""
                            }
                        }
                    }
                }");

            var contentItems = mockProj.Items
                .Where(item => item.ItemType.Equals("None", StringComparison.Ordinal))
                .Where(item =>
                    item.GetMetadataWithName("Pack").Value == "true" &&
                    item.GetMetadataWithName("PackagePath") != null);

            contentItems.Count().Should().Be(1);
            contentItems.First().Update.Should().Be(@"path\to\some\file.cs");
            contentItems.First().GetMetadataWithName("PackagePath").Value.Should().BeEmpty();
        }

        [Fact]
        public void MigratingSameFileWithMultipleMappingsStringJoinsTheMappingsInPackagePath()
        {
            var mockProj = RunPackOptionsRuleOnPj(@"
                {
                    ""packOptions"": {
                        ""files"": {
                            ""include"": [""path/to/some/file.cs""],
                            ""mappings"": {
                                ""other/path/file.cs"": ""path/to/some/file.cs"",
                                ""different/path/file1.cs"": ""path/to/some/file.cs""
                            }
                        }
                    }
                }");

            var expectedPackagePath = string.Join(
                ";", 
                new [] {                    
                    Path.Combine("different", "path"),
                    Path.Combine("other", "path")
                });

            var contentItems = mockProj.Items
                .Where(item => item.ItemType.Equals("None", StringComparison.Ordinal))
                .Where(item =>
                    item.GetMetadataWithName("Pack").Value == "true" &&
                    item.GetMetadataWithName("PackagePath") != null);

            contentItems.Count().Should().Be(1);
            contentItems.First().Update.Should().Be(@"path\to\some\file.cs");
            contentItems.First().GetMetadataWithName("PackagePath").Value.Should().Be(expectedPackagePath);
        }

        private ProjectRootElement RunPackOptionsRuleOnPj(string packOptions, string testDirectory = null)
        {
            testDirectory = testDirectory ?? Temp.CreateDirectory().Path;
            return TemporaryProjectFileRuleRunner.RunRules(new IMigrationRule[]
            {
                new MigratePackOptionsRule()
            }, packOptions, testDirectory);
        }

        private void EmitsOnlyAlwaysEmittedPackOptionsProperties(ProjectRootElement project)
        {
            project.Properties.Count().Should().Be(1);
            project.Properties.All(p => p.Name == "PackageRequireLicenseAcceptance").Should().BeTrue();
        }   
    }
}