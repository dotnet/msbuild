// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Runtime;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using Microsoft.DotNet.ApiCompatibility.Extensions;

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    /// <summary>
    /// This class implements a rule to check that the visibility of symbols is not reduced.
    /// In strict mode, it also checks that the visibility isn't expanded.
    /// </summary>
    public class CannotChangeVisibility : IRule
    {
        private readonly RuleSettings _settings;

        public CannotChangeVisibility(RuleSettings settings, IRuleRegistrationContext context)
        {
            _settings = settings;
            context.RegisterOnMemberSymbolAction(RunOnMemberSymbol);
            context.RegisterOnTypeSymbolAction(RunOnTypeSymbol);
        }

        private void RunOnSymbol(
            ISymbol? left,
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

            if (leftAccess > rightAccess)
            {
                differences.Add(new CompatDifference(leftMetadata,
                    rightMetadata,
                    DiagnosticIds.CannotReduceVisibility,
                    string.Format(Resources.CannotReduceVisibility, left, leftAccess, rightAccess),
                    DifferenceType.Changed,
                    left));
            }
            else if (_settings.StrictMode && rightAccess > leftAccess)
            {
                differences.Add(new CompatDifference(leftMetadata,
                    rightMetadata,
                    DiagnosticIds.CannotExpandVisibility,
                    string.Format(Resources.CannotExpandVisibility, right, leftAccess, rightAccess),
                    DifferenceType.Changed,
                    right));
            }
        }

        private void RunOnTypeSymbol(
            ITypeSymbol? left,
            ITypeSymbol? right,
            MetadataInformation leftMetadata,
            MetadataInformation rightMetadata,
            IList<CompatDifference> differences) => RunOnSymbol(left, right, leftMetadata, rightMetadata, differences);

        private void RunOnMemberSymbol(
            ISymbol? left,
            ISymbol? right,
            ITypeSymbol leftContainingType,
            ITypeSymbol rightContainingType,
            MetadataInformation leftMetadata,
            MetadataInformation rightMetadata,
            IList<CompatDifference> differences) => RunOnSymbol(left, right, leftMetadata, rightMetadata, differences);
    }
}
