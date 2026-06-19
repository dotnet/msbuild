// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Evaluation;
using Shouldly;
using Xunit;

#nullable disable

namespace Microsoft.Build.UnitTests.Evaluation
{
    public class ProjectCollection_Tests
    {
        [Fact]
        public void ProjectRootElementCache_IsDeterminedByEnvironmentVariable()
        {
            using var collectionWithDefaultCache = new ProjectCollection();
            collectionWithDefaultCache.ProjectRootElementCache.ShouldBeOfType<ProjectRootElementCache>();

            const string envKey = "MsBuildUseSimpleProjectRootElementCacheConcurrency";

            using (TestEnvironment env = TestEnvironment.Create())
            {
                env.SetEnvironmentVariable(envKey, "true");
                using var collectionWithSimpleCache = new ProjectCollection();
                collectionWithSimpleCache.ProjectRootElementCache.ShouldBeOfType<SimpleProjectRootElementCache>();
            }
        }

        [Theory]
        // VMR build: the informational version carries the VMR commit while RepoOriginalSourceRevisionId carries
        // the original MSBuild commit, which must win and be truncated to 9 characters.
        [InlineData("18.8.0-dev-26316-01+vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "18.8.0-dev-26316-01+aaaaaaaaa")]
        // Non-VMR build: no RepoOriginalSourceRevisionId, so the informational version's own SHA is used (truncated to 9).
        [InlineData("18.8.0-dev-26316-01+vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv", "", "18.8.0-dev-26316-01+vvvvvvvvv")]
        [InlineData("18.8.0-dev-26316-01+vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv", null, "18.8.0-dev-26316-01+vvvvvvvvv")]
        // VMR build whose informational version has no '+' SHA suffix still gets the MSBuild commit appended.
        [InlineData("18.8.0-dev-26316-01", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "18.8.0-dev-26316-01+aaaaaaaaa")]
        // Non-VMR build with no SHA suffix is returned unchanged.
        [InlineData("18.8.0-dev-26316-01", "", "18.8.0-dev-26316-01")]
        // A RepoOriginalSourceRevisionId shorter than 9 characters is used as-is.
        [InlineData("18.8.0-dev-26316-01+vvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvvv", "abc123", "18.8.0-dev-26316-01+abc123")]
        public void GetDisplayVersion_PrefersOriginalSourceRevisionId(string fullInformationalVersion, string sourceRevisionId, string expected)
        {
            ProjectCollection.GetDisplayVersion(fullInformationalVersion, sourceRevisionId).ShouldBe(expected);
        }
    }
}
