// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.NET.Build.Tasks.ConflictResolution;
using Microsoft.NET.Build.Tasks.UnitTests.Mocks;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class GivenAPackageOverrideResolver
    {
        [Fact]
        public void ItMergesPackageOverridesUsingHighestVersion()
        {
            ITaskItem[] packageOverrides = new[]
            {
                new MockTaskItem("Platform", new Dictionary<string, string>
                {
                    { MetadataKeys.OverriddenPackages, "System.Ben|4.2.0;System.Immo|4.2.0;System.Livar|4.3.0;System.Dave|4.2.0" }
                }),
                new MockTaskItem("Platform", new Dictionary<string, string>
                {
                    { MetadataKeys.OverriddenPackages, "System.Ben|4.2.0;System.Immo|4.3.0;System.Livar|4.2.0;System.Nick|4.2.0" }
                })
            };

            var resolver = new PackageOverrideResolver<MockConflictItem>(packageOverrides);

            Assert.Single(resolver.PackageOverrides);

            PackageOverride packageOverride = resolver.PackageOverrides["Platform"];
            Assert.Equal(5, packageOverride.OverriddenPackages.Count);
            Assert.Equal(new Version(4, 2, 0), packageOverride.OverriddenPackages["System.Ben"]);
            Assert.Equal(new Version(4, 3, 0), packageOverride.OverriddenPackages["System.Immo"]);
            Assert.Equal(new Version(4, 3, 0), packageOverride.OverriddenPackages["System.Livar"]);
            Assert.Equal(new Version(4, 2, 0), packageOverride.OverriddenPackages["System.Dave"]);
            Assert.Equal(new Version(4, 2, 0), packageOverride.OverriddenPackages["System.Nick"]);
        }

        [Fact]
        public void ItHandlesNullITaskItemArray()
        {
            var resolver = new PackageOverrideResolver<MockConflictItem>(null);

            Assert.Null(resolver.PackageOverrides);
            Assert.Null(resolver.Resolve(new MockConflictItem(), new MockConflictItem()));
        }
    }
}
