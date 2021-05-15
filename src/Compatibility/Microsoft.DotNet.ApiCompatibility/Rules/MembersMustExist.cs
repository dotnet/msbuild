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
                AddDifference(left, DifferenceType.Removed, Resources.TypeExistsOnLeft);
            }
            else if (Settings.StrictMode && left == null && right != null)
            {
                AddDifference(right, DifferenceType.Added, Resources.TypeExistsOnRight);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            void AddDifference(ITypeSymbol symbol, DifferenceType type, string format)
            {
                differences.Add(new CompatDifference(DiagnosticIds.TypeMustExist, string.Format(format, symbol.ToDisplayString()), type, symbol));
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
                // Events and properties are handled via their accessors.
                if (left.Kind == SymbolKind.Property || left.Kind == SymbolKind.Event)
                    return;

                if (left is IMethodSymbol method)
                {
                    // Will be handled by a different rule
                    if (method.MethodKind == MethodKind.ExplicitInterfaceImplementation)
                        return;

                    // If method is an override or hides a base type definition removing it from right is compatible.
                    if (method.IsOverride || FindMatchingOnBaseType(method))
                        return;
                }

                differences.Add(new CompatDifference(DiagnosticIds.MemberMustExist, string.Format(Resources.MemberExistsOnLeft, left.ToDisplayString()), DifferenceType.Removed, left));
            }
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
