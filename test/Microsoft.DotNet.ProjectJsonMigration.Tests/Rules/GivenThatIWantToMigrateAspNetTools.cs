// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Tools.Test.Utilities;
using System.Linq;
using Xunit;
using FluentAssertions;
using System;

namespace Microsoft.DotNet.ProjectJsonMigration.Tests
{
    public class GivenThatIWantToMigrateAspNetTools : PackageDependenciesTestBase
    {
        [Fact]
        public void It_migrates_MicrosoftEntityFrameworkCoreTools_to_AspNetToolsVersion()
        {
            var mockProj = RunPackageDependenciesRuleOnPj(@"
                {
                    ""dependencies"": {
                        ""Microsoft.EntityFrameworkCore.Tools"" : {
                            ""version"": ""1.0.0-preview2-final"",
                            ""type"": ""build""
                        }
                    }
                }");

            var packageRef = mockProj.Items.First(i => i.Include == "Microsoft.EntityFrameworkCore.Tools" && i.ItemType == "PackageReference");

            packageRef.GetMetadataWithName("Version").Value.Should().Be(ConstantPackageVersions.AspNetToolsVersion);

            var privateAssetsMetadata = packageRef.GetMetadataWithName("PrivateAssets");
            privateAssetsMetadata.Value.Should().NotBeNull();
            privateAssetsMetadata.Value.Should().Be("All");
        }

        [Fact]
        public void It_migrates_MicrosoftEntityFrameworkCoreToolsDotNet_tool_to_AspNetToolsVersion()
        {
            var mockProj = RunPackageDependenciesRuleOnPj(@"
                {
                    ""tools"": {
                        ""Microsoft.EntityFrameworkCore.Tools"": ""1.0.0-preview2-final""
                    }
                }");

            EmitsToolReferences(mockProj, Tuple.Create("Microsoft.EntityFrameworkCore.Tools.DotNet", ConstantPackageVersions.AspNetToolsVersion));
        }

        [Fact]
        public void It_migrates_MicrosoftAspNetCoreRazorTools_to_AspNetToolsVersion()
        {
            var mockProj = RunPackageDependenciesRuleOnPj(@"
                {
                    ""dependencies"": {
                        ""Microsoft.AspNetCore.Razor.Tools"" : {
                            ""version"": ""1.0.0-preview2-final"",
                            ""type"": ""build""
                        }
                    }
                }");

            var packageRef = mockProj.Items.First(i => i.Include == "Microsoft.AspNetCore.Razor.Design" && i.ItemType == "PackageReference");

            packageRef.GetMetadataWithName("Version").Value.Should().Be(ConstantPackageVersions.AspNetToolsVersion);

            var privateAssetsMetadata = packageRef.GetMetadataWithName("PrivateAssets");
            privateAssetsMetadata.Value.Should().NotBeNull();
            privateAssetsMetadata.Value.Should().Be("All");
        }

        [Fact]
        public void It_migrates_MicrosoftAspNetCoreRazorDesign_to_AspNetToolsVersion()
        {
            var mockProj = RunPackageDependenciesRuleOnPj(@"
                {
                    ""dependencies"": {
                        ""Microsoft.AspNetCore.Razor.Design"" : {
                            ""version"": ""1.0.0-preview2-final"",
                            ""type"": ""build""
                        }
                    }
                }");

            var packageRef = mockProj.Items.First(i => i.Include == "Microsoft.AspNetCore.Razor.Design" && i.ItemType == "PackageReference");

            packageRef.GetMetadataWithName("Version").Value.Should().Be(ConstantPackageVersions.AspNetToolsVersion);

            var privateAssetsMetadata = packageRef.GetMetadataWithName("PrivateAssets");
            privateAssetsMetadata.Value.Should().NotBeNull();
            privateAssetsMetadata.Value.Should().Be("All");
        }

        [Fact]
        public void It_migrates_MicrosoftAspNetCoreRazorTools_tool_to_AspNetToolsVersion()
        {
            var mockProj = RunPackageDependenciesRuleOnPj(@"
                {
                    ""tools"": {
                        ""Microsoft.AspNetCore.Razor.Tools"": ""1.0.0-preview2-final""
                    }
                }");

            EmitsToolReferences(mockProj, Tuple.Create("Microsoft.AspNetCore.Razor.Tools", ConstantPackageVersions.AspNetToolsVersion));
        }

        [Fact]
        public void It_migrates_MicrosoftVisualStudioWebCodeGeneratorsMvc_to_AspNetToolsVersion()
        {
            var mockProj = RunPackageDependenciesRuleOnPj(@"
                {
                    ""dependencies"": {
                        ""Microsoft.VisualStudio.Web.CodeGenerators.Mvc"" : {
                            ""version"": ""1.0.0-preview2-final"",
                            ""type"": ""build""
                        }
                    }
                }");

            var packageRef = mockProj.Items.First(i => i.Include == "Microsoft.VisualStudio.Web.CodGeneration.Design" && i.ItemType == "PackageReference");

            packageRef.GetMetadataWithName("Version").Value.Should().Be(ConstantPackageVersions.AspNetToolsVersion);

            var privateAssetsMetadata = packageRef.GetMetadataWithName("PrivateAssets");
            privateAssetsMetadata.Value.Should().NotBeNull();
            privateAssetsMetadata.Value.Should().Be("All");
        }

        [Fact]
        public void It_does_not_migrate_MicrosoftVisualStudioWebCodeGenerationTools()
        {
            var mockProj = RunPackageDependenciesRuleOnPj(@"
                {
                    ""dependencies"": {
                        ""Microsoft.VisualStudio.Web.CodeGeneration.Tools"" : {
                            ""version"": ""1.0.0-preview2-final"",
                            ""type"": ""build""
                        }
                    }
                }");

            var packageRef = mockProj.Items.Where(i => i.Include != "Microsoft.NET.Sdk" && i.ItemType == "PackageReference").Should().BeEmpty();
        }

        [Fact]
        public void It_migrates_MicrosoftVisualStudioWebCodeGenerationTools_tool_to_AspNetToolsVersion()
        {
            var mockProj = RunPackageDependenciesRuleOnPj(@"
                {
                    ""tools"": {
                        ""Microsoft.VisualStudio.Web.CodeGeneration.Tools"": ""1.0.0-preview2-final""
                    }
                }");

            EmitsToolReferences(mockProj, Tuple.Create("Microsoft.VisualStudio.Web.CodGeneration.Tools", ConstantPackageVersions.AspNetToolsVersion));
        }

        [Fact]
        public void It_migrates_MicrosoftDotNetWatcherTools_tool_to_AspNetToolsVersion()
        {
            var mockProj = RunPackageDependenciesRuleOnPj(@"
                {
                    ""tools"": {
                        ""Microsoft.DotNet.Watcher.Tools"": ""1.0.0-preview2-final""
                    }
                }");

            EmitsToolReferences(mockProj, Tuple.Create("Microsoft.DotNet.Watcher.Tools", ConstantPackageVersions.AspNetToolsVersion));
        }

        [Fact]
        public void It_migrates_MicrosoftExtensionsSecretManagerTools_tool_to_AspNetToolsVersion()
        {
            var mockProj = RunPackageDependenciesRuleOnPj(@"
                {
                    ""tools"": {
                        ""Microsoft.Extensions.SecretManager.Tools"": ""1.0.0-preview2-final""
                    }
                }");

            EmitsToolReferences(mockProj, Tuple.Create("Microsoft.Extensions.SecretManager.Tools", ConstantPackageVersions.AspNetToolsVersion));
        }

        [Fact]
        public void It_does_not_migrate_MicrosoftAspNetCoreServerIISIntegrationTools()
        {
            var mockProj = RunPackageDependenciesRuleOnPj(@"
                {
                    ""tools"": {
                        ""Microsoft.AspNetCore.Server.IISIntegration.Tools"": ""1.0.0-preview2-final""
                    }
                }");

            var packageRef = mockProj.Items.Where(i => i.ItemType == "DotNetCliToolReference").Should().BeEmpty();
        }
    }
}