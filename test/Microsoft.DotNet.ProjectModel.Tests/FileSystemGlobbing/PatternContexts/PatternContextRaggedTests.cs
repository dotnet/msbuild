// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.DotNet.ProjectModel.FileSystemGlobbing.Internal;
using Microsoft.DotNet.ProjectModel.FileSystemGlobbing.Internal.PathSegments;
using Microsoft.DotNet.ProjectModel.FileSystemGlobbing.Internal.PatternContexts;
using Microsoft.DotNet.ProjectModel.FileSystemGlobbing.Internal.Patterns;
using Microsoft.DotNet.ProjectModel.FileSystemGlobbing.Tests.TestUtility;
using Xunit;

namespace Microsoft.DotNet.ProjectModel.FileSystemGlobbing.Tests.PatternContexts
{
    public class PatternContextRaggedIncludeTests
    {
        [Fact]
        public void PredictBeforeEnterDirectoryShouldThrow()
        {
            var builder = new PatternBuilder();
            var pattern = builder.Build("**") as IRaggedPattern;
            var context = new PatternContextRaggedInclude(pattern);

            Assert.Throws<InvalidOperationException>(() =>
            {
                context.Declare((segment, last) =>
                {
                    Assert.False(true, "No segment should be declared.");
                });
            });
        }

        [Theory]
        [InlineData("/a/b/**/c/d", new string[] { "root" }, "a", false)]
        [InlineData("/a/b/**/c/d", new string[] { "root", "a" }, "b", false)]
        [InlineData("/a/b/**/c/d", new string[] { "root", "a", "b" }, null, false)]
        [InlineData("/a/b/**/c/d", new string[] { "root", "a", "b", "whatever" }, null, false)]
        [InlineData("/a/b/**/c/d", new string[] { "root", "a", "b", "whatever", "anything" }, null, false)]
        public void PredictReturnsCorrectResult(string patternString, string[] pushDirectory, string expectSegment, bool expectWildcard)
        {
            var builder = new PatternBuilder();
            var pattern = builder.Build(patternString) as IRaggedPattern;
            Assert.NotNull(pattern);

            var context = new PatternContextRaggedInclude(pattern);
            PatternContextHelper.PushDirectory(context, pushDirectory);

            context.Declare((segment, last) =>
            {
                if (expectSegment != null)
                {
                    var mockSegment = segment as LiteralPathSegment;

                    Assert.NotNull(mockSegment);
                    Assert.Equal(false, last);
                    Assert.Equal(expectSegment, mockSegment.Value);
                }
                else
                {
                    Assert.Equal(Microsoft.DotNet.ProjectModel.FileSystemGlobbing.Internal.PathSegments.WildcardPathSegment.MatchAll, segment);
                }
            });
        }

        [Theory]
        [InlineData("/a/b/**/c/d", new string[] { "root", "b" })]
        [InlineData("/a/b/**/c/d", new string[] { "root", "a", "c" })]
        public void PredictNotCallBackWhenEnterUnmatchDirectory(string patternString, string[] pushDirectory)
        {
            var builder = new PatternBuilder();
            var pattern = builder.Build(patternString) as IRaggedPattern;
            var context = new PatternContextRaggedInclude(pattern);
            PatternContextHelper.PushDirectory(context, pushDirectory);

            context.Declare((segment, last) =>
            {
                Assert.False(true, "No segment should be declared.");
            });
        }
    }
}