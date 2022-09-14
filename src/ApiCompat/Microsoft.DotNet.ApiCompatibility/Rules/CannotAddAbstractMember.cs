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
    public class CannotAddAbstractMember : IRule
    {
        private readonly RuleSettings _settings;

        public CannotAddAbstractMember(RuleSettings settings, IRuleRegistrationContext context)
        {
            _settings = settings;
            // This rule should only run when not in strict mode. 
            // When running in strict mode this diff will be handled by MembersMustExist rule.
            if (!settings.StrictMode)
            {
                context.RegisterOnMemberSymbolAction(RunOnMemberMapperAction);
            }
        }

        private void RunOnMemberMapperAction(ISymbol? left, ISymbol? right, ITypeSymbol leftContainingType, ITypeSymbol rightContainingType, MetadataInformation leftMetadata, MetadataInformation rightMetadata, IList<CompatDifference> differences)
        {
            if (left == null && right != null && right.IsAbstract)
            {
                // We need to make sure left containing type is not sealed, as unsealing a type is not a breaking change.
                // So if in this version of left and right, right is unsealing the type, abstract members can be added.
                // checking for member additions on interfaces is checked on its own rule.
                if (leftContainingType.TypeKind != TypeKind.Interface && !leftContainingType.IsEffectivelySealed(_settings.IncludeInternalSymbols))
                {
                    differences.Add(new CompatDifference(
                        leftMetadata,
                        rightMetadata,
                        DiagnosticIds.CannotAddAbstractMember,
                        string.Format(Resources.CannotAddAbstractMember, right.ToDisplayString(), rightMetadata, leftMetadata),
                        DifferenceType.Added,
                        right));
                }
            }
        }
    }
}
