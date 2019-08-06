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

            using (TestEnvironment env = TestEnvironment.Create())
            {
                env.SetEnvironmentVariable(envKey, "true");
                var collectionWithSimpleCache = new ProjectCollection();
                collectionWithSimpleCache.ProjectRootElementCache.ShouldBeOfType<SimpleProjectRootElementCache>();
            }
        }
    }
}
