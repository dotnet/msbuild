// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    /// <summary>
    /// This class implements a rule to check that the visibility of symbols is not reduced.
    /// In strict mode, it also checks that the visibility isn't expanded.
    /// </summary>
    public class CannotChangeVisibility : IRule
    {
        private readonly IRuleSettings _settings;

        public CannotChangeVisibility(IRuleSettings settings, IRuleRegistrationContext context)
        {
            _settings = settings;
            context.RegisterOnMemberSymbolAction(RunOnMemberSymbol);
            context.RegisterOnTypeSymbolAction(RunOnTypeSymbol);
        }

        private static Accessibility NormalizeInternals(Accessibility a) => a switch
        {
            Accessibility.ProtectedOrInternal => Accessibility.Protected,
            Accessibility.ProtectedAndInternal or Accessibility.Internal => Accessibility.Private,
            _ => a,
        };

        private int CompareAccessibility(Accessibility a, Accessibility b)
        {
            if (!_settings.IncludeInternalSymbols)
            {
                a = NormalizeInternals(a);
                b = NormalizeInternals(b);
            }

            if (a == b)
            {
                return 0;
            }

            return (a, b) switch
            {
                (Accessibility.Public, _) => 1,
                (_, Accessibility.Public) => -1,
                (Accessibility.ProtectedOrInternal, _) => 1,
                (_, Accessibility.ProtectedOrInternal) => -1,
                (Accessibility.Protected or Accessibility.Internal, _) => 1,
                (_, Accessibility.Protected or Accessibility.Internal) => -1,
                (Accessibility.ProtectedAndInternal, _) => 1,
                (_, Accessibility.ProtectedAndInternal) => -1,
                _ => throw new NotImplementedException(),
            };
        }

        private void RunOnSymbol(ISymbol? left,
            ISymbol? right,
            MetadataInformation leftMetadata,
            MetadataInformation rightMetadata,
            IList<CompatDifference> differences)
        {
            // The MemberMustExist rule handles missing symbols and therefore this rule only runs when left and right is not null.
            if (left is null || right is null)
            {
                return;
            }

            Accessibility leftAccess = left.DeclaredAccessibility;
            Accessibility rightAccess = right.DeclaredAccessibility;
            int accessComparison = CompareAccessibility(leftAccess, rightAccess);

            if (accessComparison > 0)
            {
                differences.Add(new CompatDifference(leftMetadata,
                    rightMetadata,
                    DiagnosticIds.CannotReduceVisibility,
                    string.Format(Resources.CannotReduceVisibility, left, leftAccess, rightAccess),
                    DifferenceType.Changed,
                    left));
            }
            else if (_settings.StrictMode && accessComparison < 0)
            {
                differences.Add(new CompatDifference(leftMetadata,
                    rightMetadata,
                    DiagnosticIds.CannotExpandVisibility,
                    string.Format(Resources.CannotExpandVisibility, right, leftAccess, rightAccess),
                    DifferenceType.Changed,
                    right));
            }
        }

        private void RunOnTypeSymbol(ITypeSymbol? left,
            ITypeSymbol? right,
            MetadataInformation leftMetadata,
            MetadataInformation rightMetadata,
            IList<CompatDifference> differences) => RunOnSymbol(left, right, leftMetadata, rightMetadata, differences);

        private void RunOnMemberSymbol(ISymbol? left,
            ISymbol? right,
            ITypeSymbol leftContainingType,
            ITypeSymbol rightContainingType,
            MetadataInformation leftMetadata,
            MetadataInformation rightMetadata,
            IList<CompatDifference> differences) => RunOnSymbol(left, right, leftMetadata, rightMetadata, differences);
    }
}
