// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    public class RuleSettings
    {
        public RuleSettings(bool strictMode, IEqualityComparer<ISymbol> symbolComparer, bool includeInternalSymbols)
        {
            StrictMode = strictMode;
            SymbolComparer = symbolComparer;
            IncludeInternalSymbols = includeInternalSymbols;
        }

        public bool StrictMode { get; }
        public IEqualityComparer<ISymbol> SymbolComparer { get; }
        public bool IncludeInternalSymbols { get; }
    }
}
