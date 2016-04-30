// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.ProjectModel.FileSystemGlobbing.Internal.PathSegments;
using Xunit;

namespace Microsoft.DotNet.ProjectModel.FileSystemGlobbing.Tests.PatternSegments
{
    public class ParentPathSegmentTests
    {
        [Theory]
        [InlineData(".", false)]
        [InlineData("..", true)]
        [InlineData("...", false)]
        public void Match(string testSample, bool expectation)
        {
            var pathSegment = new ParentPathSegment();
            Assert.Equal(expectation, pathSegment.Match(testSample));
        }
    }
}