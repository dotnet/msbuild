// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using Microsoft.DotNet.ApiCompatibility.Extensions;

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    public class CannotSealType : IRule
    {
        private readonly RuleSettings _settings;

        public CannotSealType(RuleSettings settings, IRuleRegistrationContext context)
        {
            _settings = settings;
            context.RegisterOnTypeSymbolAction(RunOnTypeSymbol);
        }

        private void RunOnTypeSymbol(ITypeSymbol? left, ITypeSymbol? right, MetadataInformation leftMetadata, MetadataInformation rightMetadata, IList<CompatDifference> differences)
        {
            if (left == null || right == null || left.TypeKind == TypeKind.Interface || right.TypeKind == TypeKind.Interface)
                return;

            bool isLeftSealed = left.IsEffectivelySealed(_settings.IncludeInternalSymbols);
            bool isRightSealed = right.IsEffectivelySealed(_settings.IncludeInternalSymbols);

            if (!isLeftSealed && isRightSealed)
            {
                differences.Add(new CompatDifference(
                    leftMetadata,
                    rightMetadata,
                    DiagnosticIds.CannotSealType,
                    string.Format(GetResourceStringForTypeState(right), right.ToDisplayString(), rightMetadata, leftMetadata),
                    DifferenceType.Changed,
                    right));
            }
            else if (_settings.StrictMode && !isRightSealed && isLeftSealed)
            {
                differences.Add(new CompatDifference(
                    leftMetadata,
                    rightMetadata,
                    DiagnosticIds.CannotSealType,
                    string.Format(GetResourceStringForTypeState(left), left.ToDisplayString(), leftMetadata, rightMetadata),
                    DifferenceType.Changed,
                    left));
            }
        }

        private static string GetResourceStringForTypeState(ISymbol symbol) =>
            symbol.IsSealed ? Resources.TypeIsActuallySealed : Resources.TypeIsEffectivelySealed;
    }
}
