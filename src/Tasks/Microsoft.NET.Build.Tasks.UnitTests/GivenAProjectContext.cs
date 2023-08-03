// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using NuGet.Frameworks;
using NuGet.ProjectModel;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAProjectContext
    {
        [Fact]
        public void ItComputesExcludeFromPublishList()
        {
            LockFile lockFile = TestLockFiles.GetLockFile("dependencies.withgraphs");
            ProjectContext projectContext = lockFile.CreateProjectContext(
               FrameworkConstants.CommonFrameworks.NetStandard16.GetShortFolderName(),
               runtime: null,
               Constants.DefaultPlatformLibrary,
               runtimeFrameworks: null,
               isSelfContained: false);

            IEnumerable<string> excludeFromPublishPackageIds = new[] { "Microsoft.Extensions.Logging.Abstractions" };
            IDictionary<string, LockFileTargetLibrary> libraryLookup =
                projectContext
                    .LockFileTarget
                    .Libraries
                    .ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);

            HashSet<string> exclusionList =
                projectContext.GetExcludeFromPublishList(excludeFromPublishPackageIds, libraryLookup);
            
            HashSet<string> expectedExclusions = new HashSet<string>()
            {
                "Microsoft.Extensions.Logging.Abstractions",
                "System.Collections.Concurrent",
                "System.Diagnostics.Tracing",
            };

            exclusionList.Should().BeEquivalentTo(expectedExclusions);
        }
    }
}
