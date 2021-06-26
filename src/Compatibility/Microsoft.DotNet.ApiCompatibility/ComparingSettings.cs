// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using Microsoft.DotNet.ApiCompatibility.Rules;
using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.ApiCompatibility
{
    /// <summary>
    /// Class that contains all the settings used to filter metadata, compare symbols and run rules.
    /// </summary>
    public class ComparingSettings
    {
        /// <summary>
        /// The factory to get the <see cref="IRuleRunner"/>.
        /// </summary>
        public IRuleRunnerFactory RuleRunnerFactory { get; }

        /// <summary>
        /// The metadata filter to use when creating the <see cref="ElementMapper{T}"/>.
        /// </summary>
        public ISymbolFilter Filter { get; }

        /// <summary>
        /// The comparer to map metadata.
        /// </summary>
        public IEqualityComparer<ISymbol> EqualityComparer { get; }

        /// <summary>
        /// Instantiate an object with the desired settings.
        /// </summary>
        /// <param name="ruleRunnerFactory">The factory to create a <see cref="IRuleRunner"/></param>
        /// <param name="filter">The symbol filter.</param>
        /// <param name="equalityComparer">The comparer to map metadata.</param>
        public ComparingSettings(IRuleRunnerFactory ruleRunnerFactory = null, ISymbolFilter filter = null, IEqualityComparer<ISymbol> equalityComparer = null, bool strictMode = false, string leftName = null, string[] rightNames = null)
        {
            RuleRunnerFactory = ruleRunnerFactory ?? new RuleRunnerFactory(leftName, rightNames, strictMode: strictMode);
            Filter = filter ?? new SymbolAccessibilityBasedFilter(includeInternalSymbols: false);
            EqualityComparer = equalityComparer ?? new DefaultSymbolsEqualityComparer();
        }
    }
}
