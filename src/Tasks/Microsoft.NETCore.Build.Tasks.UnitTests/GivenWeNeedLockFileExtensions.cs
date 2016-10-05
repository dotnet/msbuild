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

namespace Microsoft.NETCore.Build.Tasks.UnitTests
{
    public class GivenWeNeedLockFileExtensions
    {
        private static MethodInfo s_GetPrivateAssetsExclusionListMethod = typeof(GenerateDepsFile)
            .GetTypeInfo()
            .Assembly
            .GetType("Microsoft.NETCore.Build.Tasks.LockFileExtensions")
            .GetMethod("GetPrivateAssetsExclusionList");

        [Fact]
        public void ItComputesPrivateAssetsExclusionList()
        {
            LockFile lockFile = TestLockFiles.GetLockFile("dependencies.withgraphs");
            LockFileTarget lockFileTarget = lockFile.GetTarget(FrameworkConstants.CommonFrameworks.NetStandard16, null);
            IEnumerable<string> privateAssetPackageIds = new[] { "Microsoft.Extensions.Logging.Abstractions" };
            IDictionary<string, LockFileTargetLibrary> libraryLookup =
                lockFileTarget
                    .Libraries
                    .ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);

            HashSet<string> exclusionList = (HashSet<string>)
                s_GetPrivateAssetsExclusionListMethod.Invoke(
                    null,
                    new object[] { lockFile, lockFileTarget, privateAssetPackageIds, libraryLookup });
            
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
