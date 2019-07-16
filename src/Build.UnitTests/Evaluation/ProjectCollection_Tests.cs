using System;
using Microsoft.Build.Evaluation;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests.Evaluation
{
    public class ProjectCollection_Tests
    {
        [Fact]
        public void ProjectRootElementCache_IsDeterminedByEnvironmentVariable()
        {
            var collectionWithDefaultCache = new ProjectCollection();
            collectionWithDefaultCache.ProjectRootElementCache.ShouldBeOfType<ProjectRootElementCache>();

            const string envKey = "MsBuildUseSimpleProjectRootElementCacheConcurrency";
            string originalEnvVar = Environment.GetEnvironmentVariable(envKey);

            try
            {
                Environment.SetEnvironmentVariable(envKey, "true");

                var collectionWithSimpleCache = new ProjectCollection();
                collectionWithSimpleCache.ProjectRootElementCache.ShouldBeOfType<SimpleProjectRootElementCache>();
            }
            finally
            {
                Environment.SetEnvironmentVariable(envKey, originalEnvVar);
            }
        }
    }
}
