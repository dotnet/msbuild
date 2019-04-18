// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
               FrameworkConstants.CommonFrameworks.NetStandard16,
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
