// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.s

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.DotNet.ApiCompatibility.Abstractions;

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    /// <summary>
    /// Rule runner that exposes functionality to initialize rules and run element mapper objects.
    /// </summary>
    public class RuleRunner : IRuleRunner
    {
        private readonly IRuleFactory _ruleFactory;
        private readonly IRuleContext _context;

        public RuleRunner(IRuleFactory ruleFactory, IRuleContext context)
        {
            _ruleFactory = ruleFactory;
            _context = context;
        }

        /// <inheritdoc />
        public void InitializeRules(RuleSettings settings)
        {
            // Instantiate the rules but don't invoke anything on them as they register themselves on "events" inside their constructor.
            _ = _ruleFactory.CreateRules(settings, _context);
        }

        /// <inheritdoc />
        public IEnumerable<CompatDifference> Run<T>(ElementMapper<T> mapper)
        {
            List<CompatDifference> differences = new();

            int rightLength = mapper.Right.Length;
            for (int rightIndex = 0; rightIndex < rightLength; rightIndex++)
            {
                if (mapper is AssemblyMapper am)
                {
                    /* Some assembly symbol actions need to know if the passed in assembly is the only one being visited.
                       This is true if the assembly set only contains a single assembly or if there is no assembly set and
                       the assembly mapper is directly visited. */
                    bool containsSingleAssembly = am.ContainingAssemblySet == null || am.ContainingAssemblySet.AssemblyCount < 2;

                    _context.RunOnAssemblySymbolActions(am.Left?.Element,
                        am.Right[rightIndex]?.Element,
                        am.Left?.MetadataInformation ?? MetadataInformation.DefaultLeft,
                        am.Right[rightIndex]?.MetadataInformation ?? MetadataInformation.DefaultRight,
                        containsSingleAssembly,
                        differences);
                }
                else if (mapper is TypeMapper tm)
                {
                    if (tm.ShouldDiffElement(rightIndex))
                    {
                        _context.RunOnTypeSymbolActions(tm.Left,
                            tm.Right[rightIndex],
                            tm.ContainingNamespace.ContainingAssembly.Left?.MetadataInformation ?? MetadataInformation.DefaultLeft,
                            tm.ContainingNamespace.ContainingAssembly.Right[rightIndex]?.MetadataInformation ?? MetadataInformation.DefaultRight,
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
                            mm.ContainingType.ContainingNamespace.ContainingAssembly.Left?.MetadataInformation ?? MetadataInformation.DefaultLeft,
                            mm.ContainingType.ContainingNamespace.ContainingAssembly.Right[rightIndex]?.MetadataInformation ?? MetadataInformation.DefaultRight,
                            differences);
                    }
                }
                else
                {
                    throw new ArgumentOutOfRangeException(nameof(mapper));
                }
            }

            return differences;
        }
    }
}
