// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using Microsoft.DotNet.ApiCompatibility.Extensions;

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    /// <summary>
    /// Rule that validates that an abstract member is not added to right when left containing type is unsealed and has a visible constructor.
    /// </summary>
    public class CannotAddAbstractMember : Rule
    {
        /// <summary>
        /// Method that is called when the rules are created by the <see cref="IRuleRunner"/> in
        /// order to do the initial setup for the rule.
        /// </summary>
        /// <param name="context">The context that the <see cref="IRuleRunner"/> creates holding callbacks to get the differences.</param>
        public override void Initialize(RuleRunnerContext context)
        {
            // This rule should only run when not in strict mode. 
            // When running in strict mode this diff will be handled by MembersMustExist rule.
            if (!Settings.StrictMode)
            {
                context.RegisterOnMemberSymbolAction(RunOnMemberMapperAction);
            }
        }

        private void RunOnMemberMapperAction(ISymbol left, ISymbol right, ITypeSymbol leftContainingType, ITypeSymbol rightContainingType, string leftName, string rightName, IList<CompatDifference> differences)
        {
            if (left == null && right != null && right.IsAbstract)
            {
                // We need to make sure left containing type is not sealed, as unsealing a type is not a breaking change.
                // So if in this version of left and right, right is unsealing the type, abstract members can be added.
                // checking for member additions on interfaces is checked on its own rule.
                if (leftContainingType.TypeKind != TypeKind.Interface && !leftContainingType.IsSealed && HasVisibleConstructor(leftContainingType as INamedTypeSymbol))
                {
                    differences.Add(new CompatDifference(DiagnosticIds.CannotAddAbstractMember, string.Format(Resources.CannotAddAbstractMember, right.ToDisplayString(), rightName, leftName), DifferenceType.Added, right));
                }
            }
        }

        private bool HasVisibleConstructor(INamedTypeSymbol type)
        {
            foreach (IMethodSymbol constructor in type.Constructors)
            {
                if (!constructor.IsStatic && constructor.IsVisibleOutsideOfAssembly())
                    return true;
            }

            return false;
        }
    }
}
