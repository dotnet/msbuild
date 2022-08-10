// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.s

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    public class RuleRunner : IRuleRunner
    {
        private readonly RuleRunnerContext _context;
        private readonly RuleSettings _settings;
        private const string DEFAULT_LEFT_NAME = "left";
        private const string DEFAULT_RIGHT_NAME = "right";

        internal RuleRunner(bool strictMode, IEqualityComparer<ISymbol> symbolComparer, bool includeInternalSymbols, bool withReferences)
        {
            _context = new RuleRunnerContext();
            _settings = new RuleSettings(strictMode, symbolComparer, includeInternalSymbols, withReferences);

            // Initialize registered rules but don't invoke anything on them as they register themselves on "events" inside their constructor.
            new RuleLocator(_context, _settings).GetService<IEnumerable<IRule>>();
        }

        public IReadOnlyList<IEnumerable<CompatDifference>> Run<T>(ElementMapper<T> mapper)
        {
            int rightLength = mapper.Right.Length;
            List<CompatDifference>[] result = new List<CompatDifference>[rightLength];
            
            for (int rightIndex = 0; rightIndex < rightLength; rightIndex++)
            {
                List<CompatDifference> differences = new();

                if (mapper is AssemblyMapper am)
                {
                    _context.RunOnAssemblySymbolActions(am.Left?.Element,
                        am.Right[rightIndex]?.Element,
                        GetAssemblyName(am.Left, ElementSide.Left),
                        GetAssemblyName(am.Right[rightIndex], ElementSide.Right),
                        differences);
                }
                else if (mapper is TypeMapper tm)
                {
                    if (tm.ShouldDiffElement(rightIndex))
                    {
                        _context.RunOnTypeSymbolActions(tm.Left,
                            tm.Right[rightIndex],
                            GetAssemblyName(tm.ContainingNamespace.ContainingAssembly.Left, ElementSide.Left),
                            GetAssemblyName(tm.ContainingNamespace.ContainingAssembly.Right[rightIndex], ElementSide.Right),
                            differences);
                    }
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
                            GetAssemblyName(mm.ContainingType.ContainingNamespace.ContainingAssembly.Left, ElementSide.Left),
                            GetAssemblyName(mm.ContainingType.ContainingNamespace.ContainingAssembly.Right[rightIndex], ElementSide.Right),
                            differences);
                    }
                }

                result[rightIndex] = differences;
            }

            return result;
        }

        private static string GetAssemblyName(ElementContainer<IAssemblySymbol>? assemblyContainer, ElementSide side) =>
            side switch
            {
                ElementSide.Left => string.IsNullOrEmpty(assemblyContainer?.MetadataInformation.DisplayString) ?
                    DEFAULT_LEFT_NAME :
                    assemblyContainer!.MetadataInformation.DisplayString,
                ElementSide.Right => string.IsNullOrEmpty(assemblyContainer?.MetadataInformation.DisplayString) ?
                    DEFAULT_RIGHT_NAME :
                    assemblyContainer!.MetadataInformation.DisplayString,
                _ => throw new ArgumentOutOfRangeException(nameof(side)),
            };
    }
}
