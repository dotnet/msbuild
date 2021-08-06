// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.ApiCompatibility.Abstractions;
using Xunit;

namespace Microsoft.DotNet.PackageValidation.Tests
{
    public class ApiCompatRunnerTests
    {
        [Fact]
        public void NoDuplicateRightsForSpecificLeft()
        {
            ApiCompatRunner acp = new(null, null, false, null);
            MetadataInformation left = new(@"A.dll", "netstandard2.0", @"lib\netstandard2.0\A.dll");
            MetadataInformation right = new(@"A.dll", "net461", @"lib\net461\A.dll");

            acp.QueueApiCompat(left, right, string.Empty);
            acp.QueueApiCompat(left, right, string.Empty);
            Assert.Single(acp._dict[left]);
        }
    }
}
