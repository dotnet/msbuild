// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiSymbolExtensions;

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    /// <summary>
    /// This rule validates that types aren't marked as sealed on the right when they weren't on the left.
    /// In strict mode, it also validates that types aren't unsealed when they were sealed before.
    /// </summary>
    public class CannotSealType : IRule
    {
        private readonly IRuleSettings _settings;

        public CannotSealType(IRuleSettings settings, IRuleRegistrationContext context)
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
                    string.Format(GetResourceStringForTypeState(right), right.ToDisplayString(SymbolExtensions.DisplayFormat), rightMetadata, leftMetadata),
                    DifferenceType.Changed,
                    right));
            }
            else if (_settings.StrictMode && !isRightSealed && isLeftSealed)
            {
                differences.Add(new CompatDifference(
                    leftMetadata,
                    rightMetadata,
                    DiagnosticIds.CannotSealType,
                    string.Format(GetResourceStringForTypeState(left), left.ToDisplayString(SymbolExtensions.DisplayFormat), leftMetadata, rightMetadata),
                    DifferenceType.Changed,
                    left));
            }
        }

        private static string GetResourceStringForTypeState(ISymbol symbol) =>
            symbol.IsSealed ? Resources.TypeIsActuallySealed : Resources.TypeIsEffectivelySealed;
    }
}
