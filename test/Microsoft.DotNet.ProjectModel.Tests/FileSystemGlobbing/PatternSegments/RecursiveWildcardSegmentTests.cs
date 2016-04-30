// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.ProjectModel.FileSystemGlobbing.Internal.PathSegments;
using Xunit;

namespace Microsoft.DotNet.ProjectModel.FileSystemGlobbing.Tests.PatternSegments
{
    public class RecursiveWildcardSegmentTests
    {
        [Fact]
        public void Match()
        {
            var pathSegment = new RecursiveWildcardSegment();
            Assert.False(pathSegment.Match("Anything"));
        }
    }
}