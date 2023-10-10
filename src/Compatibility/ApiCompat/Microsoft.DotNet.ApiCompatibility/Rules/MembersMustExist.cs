// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Mapping;
using Microsoft.DotNet.ApiSymbolExtensions;

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    /// <summary>
    /// Rule that evaluates whether a member exists on Left and Right.
    /// If the member doesn't exist on Right but it does on Left, it adds a <see cref="CompatDifference"/> to the list of differences.
    /// </summary>
    public class MembersMustExist : IRule
    {
        private readonly IRuleSettings _settings;

        /// <summary>
        /// Instantiates the 'MemberMustExist' rule with <see cref="IRuleSettings"/> and an <see cref="IRuleRegistrationContext"/>.
        /// </summary>
        /// <param name="settings">The <see cref="IRuleSettings"/> that is used for comparison.</param>
        /// <param name="context">The <see cref="IRuleRegistrationContext"/> that provides callback registration for comparing elements.</param>
        public MembersMustExist(IRuleSettings settings, IRuleRegistrationContext context)
        {
            _settings = settings;
            context.RegisterOnTypeSymbolAction(RunOnTypeSymbol);
            context.RegisterOnMemberSymbolAction(RunOnMemberSymbol);
        }

        /// <summary>
        /// Evaluates whether a type exists on both sides of the <see cref="ITypeMapper"/>.
        /// </summary>
        private void RunOnTypeSymbol(ITypeSymbol? left, ITypeSymbol? right, MetadataInformation leftMetadata, MetadataInformation rightMetadata, IList<CompatDifference> differences)
        {
            if (left != null && right == null)
            {
                differences.Add(new CompatDifference(
                    leftMetadata,
                    rightMetadata,
                    DiagnosticIds.TypeMustExist,
                    string.Format(Resources.TypeMissingOnSide, left.ToDisplayString(SymbolExtensions.DisplayFormat), leftMetadata, rightMetadata),
                    DifferenceType.Removed,
                    left));
            }
            else if (_settings.StrictMode && left == null && right != null)
            {
                differences.Add(new CompatDifference(
                    leftMetadata,
                    rightMetadata,
                    DiagnosticIds.TypeMustExist,
                    string.Format(Resources.TypeMissingOnSide, right.ToDisplayString(SymbolExtensions.DisplayFormat), rightMetadata, leftMetadata),
                    DifferenceType.Added,
                    right));
            }
        }

        /// <summary>
        /// Evaluates whether member (Field, Property, Method, Constructor, Event) exists on both sides of the <see cref="IMemberMapper"/>.
        /// </summary>
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
                        string.Format(Resources.MemberExistsOnLeft, left.ToDisplayString(SymbolExtensions.DisplayFormat), leftMetadata, rightMetadata),
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
                        string.Format(Resources.MemberExistsOnRight, right.ToDisplayString(SymbolExtensions.DisplayFormat), leftMetadata, rightMetadata),
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
            _settings.SymbolEqualityComparer.Equals(method.ReturnType, candidate.ReturnType);

        private bool ParametersMatch(IMethodSymbol method, IMethodSymbol candidate)
        {
            if (method.Parameters.Length != candidate.Parameters.Length)
                return false;

            for (int i = 0; i < method.Parameters.Length; i++)
            {
                if (!_settings.SymbolEqualityComparer.Equals(method.Parameters[i].Type, method.Parameters[i].Type))
                    return false;
            }

            return true;
        }
    }
}
