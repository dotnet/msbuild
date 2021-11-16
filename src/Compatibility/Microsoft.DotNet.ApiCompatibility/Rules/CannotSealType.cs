// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using Microsoft.DotNet.ApiCompatibility.Extensions;

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    public class CannotSealType : Rule
    {
        public override void Initialize(RuleRunnerContext context)
        {
            context.RegisterOnTypeSymbolAction(RunOnTypeSymbol);
        }

        private void RunOnTypeSymbol(ITypeSymbol left, ITypeSymbol right, string leftName, string rightName, IList<CompatDifference> differences)
        {
            if (left == null || right == null || left.TypeKind == TypeKind.Interface || right.TypeKind == TypeKind.Interface)
                return;

            bool isLeftSealed = left.IsEffectivelySealed(Settings.IncludeInternalSymbols);
            bool isRightSealed = right.IsEffectivelySealed(Settings.IncludeInternalSymbols);

            if (!isLeftSealed && isRightSealed)
            {
                differences.Add(CreateDifference(right, leftName, rightName));
            }
            else if (Settings.StrictMode && !isRightSealed && isLeftSealed)
            {
                differences.Add(CreateDifference(left, rightName, leftName));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private CompatDifference CreateDifference(ISymbol symbol, string leftName, string rightName) =>
            new(DiagnosticIds.CannotSealType,
                string.Format(symbol.IsSealed ? Resources.TypeIsActuallySealed : Resources.TypeIsEffectivelySealed, symbol.ToDisplayString(), rightName, leftName),
                DifferenceType.Changed,
                symbol);
    }
}
