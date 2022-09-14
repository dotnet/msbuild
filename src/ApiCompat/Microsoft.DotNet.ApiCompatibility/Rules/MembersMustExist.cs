// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using Microsoft.DotNet.ApiCompatibility.Extensions;

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    /// <summary>
    /// Rule that evaluates whether a member exists on Left and Right.
    /// If the member doesn't exist on Right but it does on Left, it adds a <see cref="CompatDifference"/> to the list of differences.
    /// </summary>
    public class MembersMustExist : IRule
    {
        private readonly RuleSettings _settings;

        public MembersMustExist(RuleSettings settings, IRuleRegistrationContext context)
        {
            _settings = settings;
            context.RegisterOnTypeSymbolAction(RunOnTypeSymbol);
            context.RegisterOnMemberSymbolAction(RunOnMemberSymbol);
        }

        /// <summary>
        /// Evaluates whether a type exists on both sides of the <see cref="TypeMapper"/>.
        /// </summary>
        /// <param name="mapper">The <see cref="TypeMapper"/> to evaluate.</param>
        /// <param name="differences">The list of <see cref="CompatDifference"/> to add differences to.</param>
        private void RunOnTypeSymbol(ITypeSymbol? left, ITypeSymbol? right, MetadataInformation leftMetadata, MetadataInformation rightMetadata, IList<CompatDifference> differences)
        {
            if (left != null && right == null)
            {
                differences.Add(new CompatDifference(
                    leftMetadata,
                    rightMetadata,
                    DiagnosticIds.TypeMustExist,
                    string.Format(Resources.TypeMissingOnSide, left.ToDisplayString(), leftMetadata, rightMetadata),
                    DifferenceType.Removed,
                    left));
            }
            else if (_settings.StrictMode && left == null && right != null)
            {
                differences.Add(new CompatDifference(
                    leftMetadata,
                    rightMetadata,
                    DiagnosticIds.TypeMustExist,
                    string.Format(Resources.TypeMissingOnSide, right.ToDisplayString(), rightMetadata, leftMetadata),
                    DifferenceType.Added,
                    right));
            }
        }

        /// <summary>
        /// Evaluates whether member (Field, Property, Method, Constructor, Event) exists on both sides of the <see cref="MemberMapper"/>.
        /// </summary>
        /// <param name="mapper">The <see cref="MemberMapper"/> to evaluate.</param>
        /// <param name="differences">The list of <see cref="CompatDifference"/> to add differences to.</param>
        private void RunOnMemberSymbol(ISymbol? left, ISymbol? right, ITypeSymbol leftContainingType, ITypeSymbol rightContainingType, MetadataInformation leftMetadata, MetadataInformation rightMetadata, IList<CompatDifference> differences)
        {
            if (left != null && right == null)
            {
                if (ShouldReportMissingMember(left, rightContainingType))
                {
                    differences.Add(new CompatDifference(
                        leftMetadata,
                        rightMetadata,
                        DiagnosticIds.MemberMustExist,
                        string.Format(Resources.MemberExistsOnLeft, left.ToDisplayString(), leftMetadata, rightMetadata),
                        DifferenceType.Removed,
                        left));
                }
            }
            else if (_settings.StrictMode && left == null && right != null)
            {
                if (ShouldReportMissingMember(right, leftContainingType))
                {
                    differences.Add(new CompatDifference(
                        leftMetadata,
                        rightMetadata,
                        DiagnosticIds.MemberMustExist,
                        string.Format(Resources.MemberExistsOnRight, right.ToDisplayString(), leftMetadata, rightMetadata),
                        DifferenceType.Added,
                        right));
                }
            }
        }

        private bool ShouldReportMissingMember(ISymbol symbol, ITypeSymbol containingType)
        {
            // Events and properties are handled via their accessors.
            if (symbol.Kind == SymbolKind.Property || symbol.Kind == SymbolKind.Event)
                return false;

            if (symbol is IMethodSymbol method)
            {
                // Will be handled by a different rule
                if (method.MethodKind == MethodKind.ExplicitInterfaceImplementation)
                    return false;

                // If method is an override or is promoted to the base type should not be reported.
                if (method.IsOverride || FindMatchingOnBaseType(method, containingType))
                    return false;
            }

            return true;
        }

        private bool FindMatchingOnBaseType(IMethodSymbol method, ITypeSymbol containingType)
        {
            // Constructors cannot be promoted
            if (method.MethodKind == MethodKind.Constructor)
                return false;

            if (containingType != null)
            {
                foreach (ITypeSymbol type in containingType.GetAllBaseTypes())
                {
                    foreach (ISymbol symbol in type.GetMembers())
                    {
                        if (symbol is IMethodSymbol candidate && IsMatchingMethod(method, candidate))
                            return true;
                    }
                }
            }

            return false;
        }

        private bool IsMatchingMethod(IMethodSymbol method, IMethodSymbol candidate)
        {
            if (method.Name == candidate.Name)
            {
                if (ParametersMatch(method, candidate) && ReturnTypesMatch(method, candidate))
                {
                    if (method.TypeParameters.Length == candidate.TypeParameters.Length)
                        return true;
                }
            }

            return false;
        }

        private bool ReturnTypesMatch(IMethodSymbol method, IMethodSymbol candidate) =>
            _settings.SymbolComparer.Equals(method.ReturnType, candidate.ReturnType);

        private bool ParametersMatch(IMethodSymbol method, IMethodSymbol candidate)
        {
            if (method.Parameters.Length != candidate.Parameters.Length)
                return false;

            for (int i = 0; i < method.Parameters.Length; i++)
            {
                if (!_settings.SymbolComparer.Equals(method.Parameters[i].Type, method.Parameters[i].Type))
                    return false;
            }

            return true;
        }
    }
}
