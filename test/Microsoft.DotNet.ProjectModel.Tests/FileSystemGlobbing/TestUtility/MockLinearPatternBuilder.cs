// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.ProjectModel.FileSystemGlobbing.Internal;

namespace Microsoft.DotNet.ProjectModel.FileSystemGlobbing.Tests.PatternContexts
{
    internal class MockLinearPatternBuilder
    {
        private List<IPathSegment> _segments;

        public static MockLinearPatternBuilder New()
        {
            return new MockLinearPatternBuilder();
        }

        private MockLinearPatternBuilder()
        {
            _segments = new List<IPathSegment>();
        }

        public MockLinearPatternBuilder Add(string value)
        {
            _segments.Add(new MockNonRecursivePathSegment(value));

            return this;
        }

        public MockLinearPatternBuilder Add(string[] values)
        {
            _segments.AddRange(values.Select(v => new MockNonRecursivePathSegment(v)));

            return this;
        }

        public ILinearPattern Build()
        {
            return new MockLinearPattern(_segments);
        }

        class MockLinearPattern : ILinearPattern
        {
            public MockLinearPattern(List<IPathSegment> segments)
            {
                Segments = segments;
            }

            public IList<IPathSegment> Segments { get; }

            public IPatternContext CreatePatternContextForExclude()
            {
                throw new NotImplementedException();
            }

            public IPatternContext CreatePatternContextForInclude()
            {
                throw new NotImplementedException();
            }
        }
    }
}