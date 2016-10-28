// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.DotNet.Internal.ProjectModel.FileSystemGlobbing.Util;

namespace Microsoft.DotNet.Internal.ProjectModel.FileSystemGlobbing.Internal.PathSegments
{
    internal class LiteralPathSegment : IPathSegment
    {
        private readonly StringComparison _comparisonType;

        public bool CanProduceStem { get { return false; } }

        public LiteralPathSegment(string value, StringComparison comparisonType)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            Value = value;

            _comparisonType = comparisonType;
        }

        public string Value { get; }

        public bool Match(string value)
        {
            return string.Equals(Value, value, _comparisonType);
        }

        public override bool Equals(object obj)
        {
            var other = obj as LiteralPathSegment;

            return other != null &&
                _comparisonType == other._comparisonType &&
                string.Equals(other.Value, Value, _comparisonType);
        }

        public override int GetHashCode()
        {
            return StringComparisonHelper.GetStringComparer(_comparisonType).GetHashCode(Value);
        }
    }
}