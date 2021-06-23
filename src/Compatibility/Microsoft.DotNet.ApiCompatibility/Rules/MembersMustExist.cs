// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using Microsoft.DotNet.ApiCompatibility.Extensions;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    /// <summary>
    /// Rule that evaluates whether a member exists on Left and Right.
    /// If the member doesn't exist on Right but it does on Left, it adds a <see cref="CompatDifference"/> to the list of differences.
    /// </summary>
    public class MembersMustExist : Rule
    {
        /// <summary>
        /// Method that is called when the rules are created by the <see cref="IRuleRunner"/> in
        /// order to do the initial setup for the rule.
        /// </summary>
        /// <param name="context">The context that the <see cref="IRuleRunner"/> creates holding callbacks to get the differences.</param>
        public override void Initialize(RuleRunnerContext context)
        {
            context.RegisterOnTypeSymbolAction(RunOnTypeSymbol);
            context.RegisterOnMemberSymbolAction(RunOnMemberSymbol);
        }

        /// <summary>
        /// Evaluates whether a type exists on both sides of the <see cref="TypeMapper"/>.
        /// </summary>
        /// <param name="mapper">The <see cref="TypeMapper"/> to evaluate.</param>
        /// <param name="differences">The list of <see cref="CompatDifference"/> to add differences to.</param>
        private void RunOnTypeSymbol(ITypeSymbol left, ITypeSymbol right, IList<CompatDifference> differences)
        {
            if (left != null && right == null)
            {
                differences.Add(CreateDifference(left, DiagnosticIds.TypeMustExist, DifferenceType.Removed, Resources.TypeExistsOnLeft));
            }
            else if (Settings.StrictMode && left == null && right != null)
            {
                differences.Add(CreateDifference(right, DiagnosticIds.TypeMustExist, DifferenceType.Added, Resources.TypeExistsOnRight));
            }
        }

        /// <summary>
        /// Evaluates whether member (Field, Property, Method, Constructor, Event) exists on both sides of the <see cref="MemberMapper"/>.
        /// </summary>
        /// <param name="mapper">The <see cref="MemberMapper"/> to evaluate.</param>
        /// <param name="differences">The list of <see cref="CompatDifference"/> to add differences to.</param>
        private void RunOnMemberSymbol(ISymbol left, ISymbol right, IList<CompatDifference> differences)
        {
            if (left != null && right == null)
            {
                if (ShouldReportMissingMember(left))
                {
                    differences.Add(CreateDifference(left, DiagnosticIds.MemberMustExist, DifferenceType.Removed, Resources.MemberExistsOnLeft));
                }
            }
            else if (Settings.StrictMode && left == null && right != null)
            {
                if (ShouldReportMissingMember(right))
                {
                    differences.Add(CreateDifference(right, DiagnosticIds.MemberMustExist, DifferenceType.Added, Resources.MemberExistsOnRight));
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private CompatDifference CreateDifference(ISymbol symbol, string id, DifferenceType type, string format) =>
            new(id, string.Format(format, symbol.ToDisplayString()), type, symbol);

        private bool ShouldReportMissingMember(ISymbol symbol)
        {
            // Events and properties are handled via their accessors.
            if (symbol.Kind == SymbolKind.Property || symbol.Kind == SymbolKind.Event)
                return false;

            if (symbol is IMethodSymbol method)
            {
                // Will be handled by a different rule
                if (method.MethodKind == MethodKind.ExplicitInterfaceImplementation)
                    return false;

                // If method is an override or hides a base type definition removing it from the comparing side is compatible.
                if (method.IsOverride || FindMatchingOnBaseType(method))
                    return false;
            }

            return true;
        }

        private bool FindMatchingOnBaseType(IMethodSymbol method)
        {
            foreach (ITypeSymbol type in method.ContainingType.GetAllBaseTypes())
            {
                foreach (ISymbol symbol in type.GetMembers())
                {
                    if (symbol is IMethodSymbol candidate && IsMatchingMethod(method, candidate))
                        return true;
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
            method.ReturnType.ToComparisonDisplayString() == candidate.ReturnType.ToComparisonDisplayString();

        private bool ParametersMatch(IMethodSymbol method, IMethodSymbol candidate)
        {
            if (method.Parameters.Length != candidate.Parameters.Length)
                return false;

            for (int i = 0; i < method.Parameters.Length; i++)
            {
                if (method.Parameters[i].Type.ToComparisonDisplayString() != method.Parameters[i].Type.ToComparisonDisplayString())
                    return false;
            }

            return true;
        }
    }
}
