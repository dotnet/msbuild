// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiSymbolExtensions;

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    /// <summary>
    /// This class implements a rule to check that the 'virtual' keyword is not added to
    /// or removed from a member.
    /// </summary>
    public class CannotAddOrRemoveVirtualKeyword : IRule
    {
        private readonly IRuleSettings _settings;

        private static bool IsSealed(ISymbol sym) => sym.IsSealed || (!sym.IsVirtual && !sym.IsAbstract);

        public CannotAddOrRemoveVirtualKeyword(IRuleSettings settings, IRuleRegistrationContext context)
        {
            _settings = settings;
            context.RegisterOnMemberSymbolAction(RunOnMemberSymbol);
        }

        private void RunOnMemberSymbol(ISymbol? left, ISymbol? right, ITypeSymbol leftContainingType, ITypeSymbol rightContainingType, MetadataInformation leftMetadata, MetadataInformation rightMetadata, IList<CompatDifference> differences)
        {
            // Members must exist
            if (left is null || right is null)
            {
                return;
            }

            if (leftContainingType.TypeKind == TypeKind.Interface || rightContainingType.TypeKind == TypeKind.Interface)
            {
                if (!IsSealed(left) && IsSealed(right))
                {
                    // Introducing the sealed keyword to an interface method is a breaking change.
                    differences.Add(new CompatDifference(
                        leftMetadata,
                        rightMetadata,
                        DiagnosticIds.CannotAddSealedToInterfaceMember,
                        string.Format(Resources.CannotAddSealedToInterfaceMember, right),
                        DifferenceType.Added,
                        right));
                }

                return;
            }

            if (left.IsVirtual)
            {
                // Removing the virtual keyword from a member in a sealed type won't be a breaking change.
                if (leftContainingType.IsEffectivelySealed(_settings.IncludeInternalSymbols))
                {
                    return;
                }

                // If left is virtual and right is not, then emit a diagnostic
                // specifying that the virtual modifier cannot be removed.
                if (!right.IsVirtual)
                {
                    differences.Add(new CompatDifference(
                        leftMetadata,
                        rightMetadata,
                        DiagnosticIds.CannotRemoveVirtualFromMember,
                        string.Format(Resources.CannotRemoveVirtualFromMember, left),
                        DifferenceType.Removed,
                        right));
                }
            }
            // If the left member is not virtual, ensure that we're in strict mode.
            // TODO: This check can be expanded once compatibility rules for
            // adding a virtual keyword are clarified: https://github.com/dotnet/sdk/issues/26169.
            else if (_settings.StrictMode)
            {
                // If the right member is virtual, emit a diagnostic
                // that the virtual modifier cannot be added.
                if (right.IsVirtual)
                {
                    differences.Add(new CompatDifference(
                        leftMetadata,
                        rightMetadata,
                        DiagnosticIds.CannotAddVirtualToMember,
                        string.Format(Resources.CannotAddVirtualToMember, right),
                        DifferenceType.Added,
                        right));
                }
            }
        }
    }
}
