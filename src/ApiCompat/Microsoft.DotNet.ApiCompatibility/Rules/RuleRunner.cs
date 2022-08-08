// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.s

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    public class RuleRunner : IRuleRunner
    {
        private readonly RuleRunnerContext _context;
        private readonly RuleSettings _settings;
        private readonly string _leftName;
        private readonly string[] _rightNames;
        internal const string DEFAULT_LEFT_NAME = "left";
        internal const string DEFAULT_RIGHT_NAME = "right";

        internal RuleRunner(string leftName, string[] rightNames, bool strictMode, IEqualityComparer<ISymbol> symbolComparer, bool includeInternalSymbols, bool withReferences)
        {
            _context = new RuleRunnerContext();
            _settings = new RuleSettings(strictMode, symbolComparer, includeInternalSymbols, withReferences);
            _leftName = leftName;
            _rightNames = rightNames;

            // Initialize registered rules but don't invoke anything on them as they register themselves on "events" inside their constructor.
            new RuleLocator(_context, _settings).GetService<IEnumerable<IRule>>();
        }

        public IReadOnlyList<IEnumerable<CompatDifference>> Run<T>(ElementMapper<T> mapper)
        {
            int rightLength = mapper.Right.Length;
            List<CompatDifference>[] result = new List<CompatDifference>[rightLength];
            
            for (int rightIndex = 0; rightIndex < rightLength; rightIndex++)
            {
                string leftName = _leftName;
                string rightName = rightIndex < _rightNames.Length ? _rightNames[rightIndex] : DEFAULT_RIGHT_NAME;
                List<CompatDifference> differences = new();

                if (mapper is AssemblyMapper am)
                {
                    _context.RunOnAssemblySymbolActions(am.Left, am.Right[rightIndex], leftName, rightName, differences);
                }
                else if (mapper is TypeMapper tm)
                {
                    if (tm.ShouldDiffElement(rightIndex))
                        _context.RunOnTypeSymbolActions(tm.Left, tm.Right[rightIndex], leftName, rightName, differences);
                }
                else if (mapper is MemberMapper mm)
                {
                    if (mm.ShouldDiffElement(rightIndex))
                    {
                        // ContainingType Left and Right cannot be null, as otherwise, the above condition would be false.
                        Debug.Assert(mm.ContainingType.Left != null);
                        Debug.Assert(mm.ContainingType.Right[rightIndex] != null);

                        _context.RunOnMemberSymbolActions(
                            mm.Left,
                            mm.Right[rightIndex],
                            mm.ContainingType.Left!,
                            mm.ContainingType.Right[rightIndex]!,
                            leftName,
                            rightName,
                            differences);
                    }
                }

                result[rightIndex] = differences;
            }

            return result;
        }
    }
}
