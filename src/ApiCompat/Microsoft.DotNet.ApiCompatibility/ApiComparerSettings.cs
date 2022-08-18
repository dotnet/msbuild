// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Rules;

namespace Microsoft.DotNet.ApiCompatibility
{
    /// <summary>
    /// Settings for an ApiComparer instance.
    /// </summary>
    public readonly struct ApiComparerSettings
    {
        /// <summary>
        /// Flag indicating whether api comparison should be performed in strict mode.
        /// If true, the behavior of some rules will change and some other rules will be
        /// executed when getting the differences. This is useful when both sides's surface area
        /// which are compared, should not differ.
        /// </summary>
        public readonly bool StrictMode;

        /// <summary>
        /// Determines if internal members should be validated.
        /// </summary>
        public readonly bool IncludeInternalSymbols;

        /// <summary>
        /// If true, references are available. Necessary to know for following type forwards.
        /// </summary>
        public readonly bool WithReferences;

        public ApiComparerSettings(bool strictMode = false,
            bool includeInternalSymbols = false,
            bool withReferences = false,
            IEqualityComparer<ISymbol>? symbolComparer = null)
        {
            StrictMode = strictMode;
            IncludeInternalSymbols = includeInternalSymbols;
            WithReferences = withReferences;
        }

        /// <summary>
        /// Transforms the api comparer settings to rule settings.
        /// </summary>
        /// <returns>Returns the transformed settings as rule settings.</returns>
        public RuleSettings ToRuleSettings() =>
            new(StrictMode, IncludeInternalSymbols, WithReferences);

        /// <summary>
        /// Transforms the api comparer settings to mapper settings.
        /// </summary>
        /// <returns>Returns the transformed settings as mapper settings.</returns>
        public MapperSettings ToMapperSettings() =>
            new(warnOnMissingReferences: WithReferences, includeInternalSymbols: IncludeInternalSymbols);
    }
}
