// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.ProjectModel.FileSystemGlobbing.Abstractions;
using Microsoft.DotNet.ProjectModel.FileSystemGlobbing.Internal;
using Microsoft.DotNet.ProjectModel.FileSystemGlobbing.Internal.Patterns;

namespace Microsoft.DotNet.ProjectModel.FileSystemGlobbing
{
    public class Matcher
    {
        private IList<IPattern> _includePatterns = new List<IPattern>();
        private IList<IPattern> _excludePatterns = new List<IPattern>();
        private readonly PatternBuilder _builder;
        private readonly StringComparison _comparison;

        public Matcher()
            : this(StringComparison.OrdinalIgnoreCase)
        {
        }

        public Matcher(StringComparison comparisonType)
        {
            _comparison = comparisonType;
            _builder = new PatternBuilder(comparisonType);
        }

        public virtual Matcher AddInclude(string pattern)
        {
            _includePatterns.Add(_builder.Build(pattern));
            return this;
        }

        public virtual Matcher AddExclude(string pattern)
        {
            _excludePatterns.Add(_builder.Build(pattern));
            return this;
        }

        public virtual PatternMatchingResult Execute(DirectoryInfoBase directoryInfo)
        {
            var context = new MatcherContext(_includePatterns, _excludePatterns, directoryInfo, _comparison);
            return context.Execute();
        }
    }
}