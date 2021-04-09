// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using System.Collections.Generic;

namespace Microsoft.DotNet.ApiCompatibility
{
    public class ApiDiffer
    {
        private readonly DiffingSettings _settings;

        public ApiDiffer() : this(new DiffingSettings()) { }

        public ApiDiffer(DiffingSettings settings)
        {
            _settings = settings;
        }

        public ApiDiffer(bool includeInternalSymbols)
        {
            _settings = new DiffingSettings(filter: new AccessibilityFilter(includeInternalSymbols));
        }

        public string NoWarn { get; set; } = string.Empty;
        public (string diagnosticId, string memberId)[] IgnoredDifferences { get; set; }

        public IEnumerable<CompatDifference> GetDifferences(IEnumerable<IAssemblySymbol> left, IEnumerable<IAssemblySymbol> right)
        {
            AssemblySetMapper mapper = new(_settings);
            mapper.AddElement(left, 0);
            mapper.AddElement(right, 1);

            DiferenceVisitor visitor = new(noWarn: NoWarn, ignoredDifferences: IgnoredDifferences);
            visitor.Visit(mapper);
            return visitor.Differences;
        }

        public IEnumerable<CompatDifference> GetDifferences(IAssemblySymbol left, IAssemblySymbol right)
        {
            AssemblyMapper mapper = new(_settings);
            mapper.AddElement(left, 0);
            mapper.AddElement(right, 1);

            DiferenceVisitor visitor = new(noWarn: NoWarn, ignoredDifferences: IgnoredDifferences);
            visitor.Visit(mapper);
            return visitor.Differences;
        }
    }
}
