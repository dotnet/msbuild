// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Construction;
using Microsoft.DotNet.Tools.Test.Utilities;
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
        public void It_does_not_migrate_Summary()
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
        public void It_does_not_migrate_Owner()
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
        public void Migrating__empty_tags_does_not_populate_PackageTags()
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
        public void Migrating_tags_populates_PackageTags_semicolon_delimited()
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
        public void Migrating_ReleaseNotes_populates_PackageReleaseNotes()
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
        public void Migrating_IconUrl_populates_PackageIconUrl()
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
        public void Migrating_ProjectUrl_populates_PackageProjectUrl()
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
        public void Migrating_LicenseUrl_populates_PackageLicenseUrl()
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
        public void Migrating_RequireLicenseAcceptance_populates_PackageRequireLicenseAcceptance()
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
        public void Migrating_RequireLicenseAcceptance_populates_PackageRequireLicenseAcceptance_even_if_its_value_is_false()
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
        public void Migrating_Repository_Type_populates_RepositoryType()
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
        public void Migrating_Repository_Url_populates_RepositoryUrl()
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
        public void Migrating_Files_throws_an_exception_for_now()
        {
            Action action = () => RunPackOptionsRuleOnPj(@"
                {
                    ""packOptions"": {
                        ""files"": {
                            ""include"": [""somefile.cs""]
                        }
                    }
                }");

            action.ShouldThrow<Exception>()
                .Where(e => e.Message.Contains("Migrating projects with Files specified in PackOptions is not supported."));
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