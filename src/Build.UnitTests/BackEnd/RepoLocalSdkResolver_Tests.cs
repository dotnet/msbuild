// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.BackEnd.SdkResolution;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.UnitTests;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.Engine.UnitTests.BackEnd
{
    public class RepoLocalSdkResolver_Tests : IDisposable
    {
        private readonly string _testRoot;

        public RepoLocalSdkResolver_Tests()
        {
            _testRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testRoot);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testRoot))
            {
                FileUtilities.DeleteDirectoryNoThrow(_testRoot, recursive: true);
            }
        }

        [Theory]
        [InlineData(".git")]
        [InlineData(".hg")]
        [InlineData(".svn")]
        public void ResolveSdkFromRepoLocalDirectory(string repoMarker)
        {
            // Arrange
            string repoRoot = Path.Combine(_testRoot, "repo");
            Directory.CreateDirectory(repoRoot);
            Directory.CreateDirectory(Path.Combine(repoRoot, repoMarker));

            string sdkName = "My.Test.Sdk";
            string sdkPath = Path.Combine(repoRoot, ".msbuild", "Sdk", sdkName, "Sdk");
            Directory.CreateDirectory(sdkPath);

            // Create Sdk.props and Sdk.targets to make it a valid SDK
            File.WriteAllText(Path.Combine(sdkPath, "Sdk.props"), "<Project />");
            File.WriteAllText(Path.Combine(sdkPath, "Sdk.targets"), "<Project />");

            string projectPath = Path.Combine(repoRoot, "src", "Project.csproj");
            Directory.CreateDirectory(Path.GetDirectoryName(projectPath));
            File.WriteAllText(projectPath, "<Project />");

            var resolver = new RepoLocalSdkResolver();
            var context = new MockSdkResolverContext(projectPath);
            var factory = new SdkResultFactory(new SdkReference(sdkName, null, null));

            // Act
            var result = resolver.Resolve(new SdkReference(sdkName, null, null), context, factory);

            // Assert
            result.Success.ShouldBeTrue();
            result.Path.ShouldBe(sdkPath);
        }

        [Fact]
        public void DoesNotResolveSdkWhenNoRepoMarkerFound()
        {
            // Arrange
            string projectDir = Path.Combine(_testRoot, "project");
            Directory.CreateDirectory(projectDir);

            string projectPath = Path.Combine(projectDir, "Project.csproj");
            File.WriteAllText(projectPath, "<Project />");

            string sdkName = "My.Test.Sdk";
            var resolver = new RepoLocalSdkResolver();
            var context = new MockSdkResolverContext(projectPath);
            var factory = new SdkResultFactory(new SdkReference(sdkName, null, null));

            // Act
            var result = resolver.Resolve(new SdkReference(sdkName, null, null), context, factory);

            // Assert
            result.Success.ShouldBeFalse();
        }

        [Fact]
        public void DoesNotResolveSdkWhenSdkDirectoryDoesNotExist()
        {
            // Arrange
            string repoRoot = Path.Combine(_testRoot, "repo");
            Directory.CreateDirectory(repoRoot);
            Directory.CreateDirectory(Path.Combine(repoRoot, ".git"));

            string projectPath = Path.Combine(repoRoot, "src", "Project.csproj");
            Directory.CreateDirectory(Path.GetDirectoryName(projectPath));
            File.WriteAllText(projectPath, "<Project />");

            string sdkName = "My.Test.Sdk";
            var resolver = new RepoLocalSdkResolver();
            var context = new MockSdkResolverContext(projectPath);
            var factory = new SdkResultFactory(new SdkReference(sdkName, null, null));

            // Act
            var result = resolver.Resolve(new SdkReference(sdkName, null, null), context, factory);

            // Assert
            result.Success.ShouldBeFalse();
        }

        [Fact]
        public void FailsWhenSdkDirectoryExistsButNoPropsOrTargets()
        {
            // Arrange
            string repoRoot = Path.Combine(_testRoot, "repo");
            Directory.CreateDirectory(repoRoot);
            Directory.CreateDirectory(Path.Combine(repoRoot, ".git"));

            string sdkName = "My.Test.Sdk";
            string sdkPath = Path.Combine(repoRoot, ".msbuild", "Sdk", sdkName, "Sdk");
            Directory.CreateDirectory(sdkPath);
            // Don't create Sdk.props or Sdk.targets

            string projectPath = Path.Combine(repoRoot, "src", "Project.csproj");
            Directory.CreateDirectory(Path.GetDirectoryName(projectPath));
            File.WriteAllText(projectPath, "<Project />");

            var resolver = new RepoLocalSdkResolver();
            var context = new MockSdkResolverContext(projectPath);
            var factory = new SdkResultFactory(new SdkReference(sdkName, null, null));

            // Act
            var result = resolver.Resolve(new SdkReference(sdkName, null, null), context, factory);

            // Assert
            result.Success.ShouldBeFalse();
            result.Errors.ShouldNotBeEmpty();
            result.Errors.ShouldContain(e => e.Contains("does not contain Sdk.props or Sdk.targets"));
        }

        [Fact]
        public void SucceedsWithOnlyPropsFile()
        {
            // Arrange
            string repoRoot = Path.Combine(_testRoot, "repo");
            Directory.CreateDirectory(repoRoot);
            Directory.CreateDirectory(Path.Combine(repoRoot, ".git"));

            string sdkName = "My.Test.Sdk";
            string sdkPath = Path.Combine(repoRoot, ".msbuild", "Sdk", sdkName, "Sdk");
            Directory.CreateDirectory(sdkPath);

            // Create only Sdk.props
            File.WriteAllText(Path.Combine(sdkPath, "Sdk.props"), "<Project />");

            string projectPath = Path.Combine(repoRoot, "src", "Project.csproj");
            Directory.CreateDirectory(Path.GetDirectoryName(projectPath));
            File.WriteAllText(projectPath, "<Project />");

            var resolver = new RepoLocalSdkResolver();
            var context = new MockSdkResolverContext(projectPath);
            var factory = new SdkResultFactory(new SdkReference(sdkName, null, null));

            // Act
            var result = resolver.Resolve(new SdkReference(sdkName, null, null), context, factory);

            // Assert
            result.Success.ShouldBeTrue();
            result.Path.ShouldBe(sdkPath);
        }

        [Fact]
        public void SucceedsWithOnlyTargetsFile()
        {
            // Arrange
            string repoRoot = Path.Combine(_testRoot, "repo");
            Directory.CreateDirectory(repoRoot);
            Directory.CreateDirectory(Path.Combine(repoRoot, ".git"));

            string sdkName = "My.Test.Sdk";
            string sdkPath = Path.Combine(repoRoot, ".msbuild", "Sdk", sdkName, "Sdk");
            Directory.CreateDirectory(sdkPath);

            // Create only Sdk.targets
            File.WriteAllText(Path.Combine(sdkPath, "Sdk.targets"), "<Project />");

            string projectPath = Path.Combine(repoRoot, "src", "Project.csproj");
            Directory.CreateDirectory(Path.GetDirectoryName(projectPath));
            File.WriteAllText(projectPath, "<Project />");

            var resolver = new RepoLocalSdkResolver();
            var context = new MockSdkResolverContext(projectPath);
            var factory = new SdkResultFactory(new SdkReference(sdkName, null, null));

            // Act
            var result = resolver.Resolve(new SdkReference(sdkName, null, null), context, factory);

            // Assert
            result.Success.ShouldBeTrue();
            result.Path.ShouldBe(sdkPath);
        }

        [Fact]
        public void FindsRepoRootMultipleLevelsUp()
        {
            // Arrange
            string repoRoot = Path.Combine(_testRoot, "repo");
            Directory.CreateDirectory(repoRoot);
            Directory.CreateDirectory(Path.Combine(repoRoot, ".git"));

            string sdkName = "My.Test.Sdk";
            string sdkPath = Path.Combine(repoRoot, ".msbuild", "Sdk", sdkName, "Sdk");
            Directory.CreateDirectory(sdkPath);

            File.WriteAllText(Path.Combine(sdkPath, "Sdk.props"), "<Project />");
            File.WriteAllText(Path.Combine(sdkPath, "Sdk.targets"), "<Project />");

            // Create project several levels deep
            string projectPath = Path.Combine(repoRoot, "src", "subfolder", "nested", "Project.csproj");
            Directory.CreateDirectory(Path.GetDirectoryName(projectPath));
            File.WriteAllText(projectPath, "<Project />");

            var resolver = new RepoLocalSdkResolver();
            var context = new MockSdkResolverContext(projectPath);
            var factory = new SdkResultFactory(new SdkReference(sdkName, null, null));

            // Act
            var result = resolver.Resolve(new SdkReference(sdkName, null, null), context, factory);

            // Assert
            result.Success.ShouldBeTrue();
            result.Path.ShouldBe(sdkPath);
        }

        [Fact]
        public void UsesSolutionPathWhenProjectPathIsNull()
        {
            // Arrange
            string repoRoot = Path.Combine(_testRoot, "repo");
            Directory.CreateDirectory(repoRoot);
            Directory.CreateDirectory(Path.Combine(repoRoot, ".git"));

            string sdkName = "My.Test.Sdk";
            string sdkPath = Path.Combine(repoRoot, ".msbuild", "Sdk", sdkName, "Sdk");
            Directory.CreateDirectory(sdkPath);

            File.WriteAllText(Path.Combine(sdkPath, "Sdk.props"), "<Project />");
            File.WriteAllText(Path.Combine(sdkPath, "Sdk.targets"), "<Project />");

            string solutionPath = Path.Combine(repoRoot, "MySolution.sln");
            File.WriteAllText(solutionPath, "");

            var resolver = new RepoLocalSdkResolver();
            var context = new MockSdkResolverContext(null, solutionPath);
            var factory = new SdkResultFactory(new SdkReference(sdkName, null, null));

            // Act
            var result = resolver.Resolve(new SdkReference(sdkName, null, null), context, factory);

            // Assert
            result.Success.ShouldBeTrue();
            result.Path.ShouldBe(sdkPath);
        }

        [Fact]
        public void ResolverHasHigherPriorityThanDefault()
        {
            // This test verifies that RepoLocalSdkResolver has a higher priority (lower number)
            // than DefaultSdkResolver so that repo-local SDKs take precedence
            var repoLocalResolver = new RepoLocalSdkResolver();
            var defaultResolver = new DefaultSdkResolver();

            repoLocalResolver.Priority.ShouldBeLessThan(defaultResolver.Priority);
        }

        private class MockSdkResolverContext : SdkResolverContext
        {
            public MockSdkResolverContext(string projectFilePath, string solutionFilePath = null)
            {
                ProjectFilePath = projectFilePath;
                SolutionFilePath = solutionFilePath;
            }
        }
    }
}
