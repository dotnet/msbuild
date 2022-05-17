// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Microsoft.AspNetCore.Razor.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.NET.TestFramework;
using Moq;
using Xunit;

namespace Microsoft.NET.Sdk.Razor.Tests
{
    public class MergeConfigurationPropertiesTest
    {
        [Fact]
        public void MergesProjectConfigurationWithProjectReferenceWhenMatchingReferenceFound()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var referenceProjectFile = Path.Combine("..", "reference", "myRcl.csproj");
            var task = new MergeConfigurationProperties
            {
                BuildEngine = buildEngine.Object,
                CandidateConfigurations = new[] { CreateCandidateProjectConfiguration(Path.GetFullPath(referenceProjectFile)) },
                ProjectReferences = new[] {
                    CreateProjectReference(
                        project: Path.Combine("..", "myRcl", "myRcl.csproj"),
                        msBuildSourceProjectFile: Path.GetFullPath(referenceProjectFile),
                        undefineProperties: Path.Combine(";TargetFramework;RuntimeIdentifier"))
                }
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true);
            task.ProjectConfigurations.Should().HaveCount(1);
            var config = task.ProjectConfigurations[0];
            config.GetMetadata("Source").Should().Be("myRcl");
            config.GetMetadata("GetBuildAssetsTargets").Should().Be("GetCurrentProjectBuildStaticWebAssetItems");
            config.GetMetadata("GetPublishAssetsTargets").Should()
                .Be("ComputeReferencedStaticWebAssetsPublishManifest;GetCurrentProjectPublishStaticWebAssetItems");
            config.GetMetadata("Version").Should().Be("2");
            config.GetMetadata("AdditionalBuildProperties").Should().Be("");
            config.GetMetadata("AdditionalBuildPropertiesToRemove").Should().Be("TargetFramework;RuntimeIdentifier");
            config.GetMetadata("AdditionalPublishProperties").Should().Be("");
            config.GetMetadata("AdditionalPublishPropertiesToRemove").Should().Be("TargetFramework;RuntimeIdentifier");
        }

        [Fact]
        public void MergesProjectConfigurationWithProjectReference_UsesOSCasingForMatching()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var referenceProjectFile = Path.Combine("..", "reference", "myRcl.csproj");
            var task = new MergeConfigurationProperties
            {
                BuildEngine = buildEngine.Object,
                CandidateConfigurations = new[] { CreateCandidateProjectConfiguration(Path.GetFullPath(referenceProjectFile)) },
                ProjectReferences = new[] 
                {
                    CreateProjectReference(
                        project: Path.Combine("..", "myRCL", "myRcl.csproj"),
                        msBuildSourceProjectFile: Path.GetFullPath(referenceProjectFile).ToUpperInvariant(),
                        undefineProperties: Path.Combine(";TargetFramework;RuntimeIdentifier"))
                }
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(OperatingSystem.IsWindows());
        }

        [Fact]
        public void FailswhenProjectReferenceNotFound()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var referenceProjectFile = Path.Combine("..", "reference", "myRcl.csproj");
            var task = new MergeConfigurationProperties
            {
                BuildEngine = buildEngine.Object,
                CandidateConfigurations = new[] { CreateCandidateProjectConfiguration(Path.GetFullPath(referenceProjectFile)) },
                ProjectReferences = Array.Empty<ITaskItem>()
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(false);
        }

        [Fact]
        public void MergesProjectConfigurationRespectsSetTargetFramework()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var referenceProjectFile = Path.Combine("..", "reference", "myRcl.csproj");
            var task = new MergeConfigurationProperties
            {
                BuildEngine = buildEngine.Object,
                CandidateConfigurations = new[] { CreateCandidateProjectConfiguration(Path.GetFullPath(referenceProjectFile)) },
                ProjectReferences = new[] {
                    CreateProjectReference(
                        project: Path.Combine("..", "myRcl", "myRcl.csproj"),
                        msBuildSourceProjectFile: Path.GetFullPath(referenceProjectFile),
                        setTargetFramework: $"TargetFramework={ToolsetInfo.CurrentTargetFramework}")
                }
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true);
            task.ProjectConfigurations.Should().HaveCount(1);
            var config = task.ProjectConfigurations[0];
            config.GetMetadata("Source").Should().Be("myRcl");
            config.GetMetadata("GetBuildAssetsTargets").Should().Be("GetCurrentProjectBuildStaticWebAssetItems");
            config.GetMetadata("GetPublishAssetsTargets").Should()
                .Be("ComputeReferencedStaticWebAssetsPublishManifest;GetCurrentProjectPublishStaticWebAssetItems");
            config.GetMetadata("Version").Should().Be("2");
            config.GetMetadata("AdditionalBuildProperties").Should().Be($"TargetFramework={ToolsetInfo.CurrentTargetFramework}");
            config.GetMetadata("AdditionalBuildPropertiesToRemove").Should().Be("");
            config.GetMetadata("AdditionalPublishProperties").Should().Be($"TargetFramework={ToolsetInfo.CurrentTargetFramework}");
            config.GetMetadata("AdditionalPublishPropertiesToRemove").Should().Be("");
        }

        [Fact]
        public void MergesProjectConfigurationRespectsSetPlatform()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var referenceProjectFile = Path.Combine("..", "reference", "myRcl.csproj");
            var task = new MergeConfigurationProperties
            {
                BuildEngine = buildEngine.Object,
                CandidateConfigurations = new[] { CreateCandidateProjectConfiguration(Path.GetFullPath(referenceProjectFile)) },
                ProjectReferences = new[] {
                    CreateProjectReference(
                        project: Path.Combine("..", "myRcl", "myRcl.csproj"),
                        msBuildSourceProjectFile: Path.GetFullPath(referenceProjectFile),
                        setPlatform: "RuntimeIdentifier=win-x64")
                }
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true);
            task.ProjectConfigurations.Should().HaveCount(1);
            var config = task.ProjectConfigurations[0];
            config.GetMetadata("Source").Should().Be("myRcl");
            config.GetMetadata("GetBuildAssetsTargets").Should().Be("GetCurrentProjectBuildStaticWebAssetItems");
            config.GetMetadata("GetPublishAssetsTargets").Should()
                .Be("ComputeReferencedStaticWebAssetsPublishManifest;GetCurrentProjectPublishStaticWebAssetItems");
            config.GetMetadata("Version").Should().Be("2");
            config.GetMetadata("AdditionalBuildProperties").Should().Be("RuntimeIdentifier=win-x64");
            config.GetMetadata("AdditionalBuildPropertiesToRemove").Should().Be("");
            config.GetMetadata("AdditionalPublishProperties").Should().Be("RuntimeIdentifier=win-x64");
            config.GetMetadata("AdditionalPublishPropertiesToRemove").Should().Be("");
        }

        [Fact]
        public void MergesProjectConfigurationRespectsSetConfiguration()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var referenceProjectFile = Path.Combine("..", "reference", "myRcl.csproj");
            var task = new MergeConfigurationProperties
            {
                BuildEngine = buildEngine.Object,
                CandidateConfigurations = new[] { CreateCandidateProjectConfiguration(Path.GetFullPath(referenceProjectFile)) },
                ProjectReferences = new[] {
                    CreateProjectReference(
                        project: Path.Combine("..", "myRcl", "myRcl.csproj"),
                        msBuildSourceProjectFile: Path.GetFullPath(referenceProjectFile),
                        setConfiguration: "Configuration=Release")
                }
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true);
            task.ProjectConfigurations.Should().HaveCount(1);
            var config = task.ProjectConfigurations[0];
            config.GetMetadata("Source").Should().Be("myRcl");
            config.GetMetadata("GetBuildAssetsTargets").Should().Be("GetCurrentProjectBuildStaticWebAssetItems");
            config.GetMetadata("GetPublishAssetsTargets").Should()
                .Be("ComputeReferencedStaticWebAssetsPublishManifest;GetCurrentProjectPublishStaticWebAssetItems");
            config.GetMetadata("Version").Should().Be("2");
            config.GetMetadata("AdditionalBuildProperties").Should().Be("Configuration=Release");
            config.GetMetadata("AdditionalBuildPropertiesToRemove").Should().Be("");
            config.GetMetadata("AdditionalPublishProperties").Should().Be("Configuration=Release");
            config.GetMetadata("AdditionalPublishPropertiesToRemove").Should().Be("");
        }

        [Fact]
        public void MergesProjectConfigurationRespectsGlobalPropertiesToRemove()
        {
            var errorMessages = new List<string>();
            var buildEngine = new Mock<IBuildEngine>();
            buildEngine.Setup(e => e.LogErrorEvent(It.IsAny<BuildErrorEventArgs>()))
                .Callback<BuildErrorEventArgs>(args => errorMessages.Add(args.Message));

            var referenceProjectFile = Path.Combine("..", "reference", "myRcl.csproj");
            var task = new MergeConfigurationProperties
            {
                BuildEngine = buildEngine.Object,
                CandidateConfigurations = new[] { CreateCandidateProjectConfiguration(Path.GetFullPath(referenceProjectFile)) },
                ProjectReferences = new[] {
                    CreateProjectReference(
                        project: Path.Combine("..", "myRcl", "myRcl.csproj"),
                        msBuildSourceProjectFile: Path.GetFullPath(referenceProjectFile),
                        undefineProperties: "TargetFramework",
                        globalPropertiesToRemove: "RuntimeIdentifier")
                }
            };

            // Act
            var result = task.Execute();

            // Assert
            result.Should().Be(true);
            task.ProjectConfigurations.Should().HaveCount(1);
            var config = task.ProjectConfigurations[0];
            config.GetMetadata("Source").Should().Be("myRcl");
            config.GetMetadata("GetBuildAssetsTargets").Should().Be("GetCurrentProjectBuildStaticWebAssetItems");
            config.GetMetadata("GetPublishAssetsTargets").Should()
                .Be("ComputeReferencedStaticWebAssetsPublishManifest;GetCurrentProjectPublishStaticWebAssetItems");
            config.GetMetadata("Version").Should().Be("2");
            config.GetMetadata("AdditionalBuildProperties").Should().Be("");
            config.GetMetadata("AdditionalBuildPropertiesToRemove").Should().Be("RuntimeIdentifier;TargetFramework");
            config.GetMetadata("AdditionalPublishProperties").Should().Be("");
            config.GetMetadata("AdditionalPublishPropertiesToRemove").Should().Be("RuntimeIdentifier;TargetFramework");
        }

        private ITaskItem CreateCandidateProjectConfiguration(string project)
        {
            return new TaskItem(Path.GetFullPath(project), new Dictionary<string, string>
            {
                ["AdditionalPublishProperties"] = "",
                ["GetBuildAssetsTargets"] = "GetCurrentProjectBuildStaticWebAssetItems",
                ["GetPublishAssetsTargets"] = "ComputeReferencedStaticWebAssetsPublishManifest;GetCurrentProjectPublishStaticWebAssetItems",
                ["Version"] = "2",
                ["AdditionalBuildProperties"] = "",
                ["Source"] = "myRcl",
                ["AdditionalPublishPropertiesToRemove"] = "",
                ["AdditionalBuildPropertiesToRemove"] = "",
            });
        }

        private ITaskItem CreateProjectReference(
            string project,
            string msBuildSourceProjectFile,
            string undefineProperties = "",
            string setConfiguration = "",
            string setPlatform = "",
            string setTargetFramework = "",
            string globalPropertiesToRemove = "")
        {
            return new TaskItem(project, new Dictionary<string, string>
            {
                ["MSBuildSourceProjectFile"] = msBuildSourceProjectFile,
                ["UndefineProperties"] = undefineProperties,
                ["SetConfiguration"] = setConfiguration,
                ["SetPlatform"] = setPlatform,
                ["SetTargetFramework"] = setTargetFramework,
                ["GlobalPropertiesToRemove"] = globalPropertiesToRemove,
            });
        }
    }
}
