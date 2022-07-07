// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;

#nullable enable

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    /// <summary>
    /// This class implements a rule to check that the 'virtual' keyword is not added to
    /// or removed from a member.
    /// </summary>
    public class CannotAddOrRemoveVirtualKeyword : Rule
    {
        public override void Initialize(RuleRunnerContext context) => context.RegisterOnMemberSymbolAction(RunOnMemberSymbol);

        private void RunOnMemberSymbol(ISymbol left, ISymbol right, ITypeSymbol leftContainingType, ITypeSymbol rightContainingType, string leftName, string rightName, IList<CompatDifference> differences)
        {
            // Members must exist
            if (left is null || right is null)
            {
                return;
            }

            // TODO: Skip interfaces for now, until the compatibility rules for interface
            // members are clarified: https://github.com/dotnet/sdk/issues/26169.
            if (leftContainingType.TypeKind == TypeKind.Interface || rightContainingType.TypeKind == TypeKind.Interface)
            {
                return;
            }

            if (left.IsVirtual)
            {
                // If left is virtual and right is not, then emit a diagnostic
                // specifying that the virtual modifier cannot be removed.
                if (!right.IsVirtual)
                {
                    differences.Add(new CompatDifference(
                    DiagnosticIds.CannotRemoveVirtualFromMember, string.Format(
                        Resources.CannotRemoveVirtualFromMember, left), DifferenceType.Removed, right));
                }
            }
            // If the left member is not virtual, ensure that we're in strict mode.
            // TODO: This check can be expanded once compatibility rules for
            // adding a virtual keyword are clarified: https://github.com/dotnet/sdk/issues/26169.
            else if (Settings.StrictMode)
            {
                // If the right member is virtual, emit a diagnostic
                // that the virtual modifier cannot be added.
                if (right.IsVirtual)
                {
                    differences.Add(new CompatDifference(
                    DiagnosticIds.CannotAddVirtualToMember, string.Format(
                        Resources.CannotAddVirtualToMember, right), DifferenceType.Added, right));
                }
            }
        }
    }
}
