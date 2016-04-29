// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.DotNet.ProjectModel.FileSystemGlobbing.Internal;

namespace Microsoft.DotNet.ProjectModel.FileSystemGlobbing.Tests.PatternContexts
{
    internal class MockNonRecursivePathSegment : IPathSegment
    {
        private readonly StringComparison _comparisonType;

        public MockNonRecursivePathSegment(StringComparison comparisonType)
        {
            _comparisonType = comparisonType;
        }

        public MockNonRecursivePathSegment(string value)
        {
            Value = value;
        }

        public bool CanProduceStem { get { return false; } }

        public string Value { get; }

        public bool Match(string value)
        {
            return string.Compare(Value, value, _comparisonType) == 0;
        }
    }
}