// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiSymbolExtensions;

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    /// <summary>
    /// This rule validates that members aren't added to interfaces with strict mode comparison.
    /// </summary>
    public class CannotAddMemberToInterface : IRule
    {
        public CannotAddMemberToInterface(IRuleSettings settings, IRuleRegistrationContext context)
        {
            // StrictMode scenario are handled by the MembersMustExist rule.
            if (!settings.StrictMode)
            {
                context.RegisterOnMemberSymbolAction(RunOnMemberSymbol);
            }
        }

        private void RunOnMemberSymbol(ISymbol? left, ISymbol? right, MetadataInformation leftMetadata, MetadataInformation rightMetadata, IList<CompatDifference> differences)
        {
            if (left == null && right != null && right.ContainingType.TypeKind == TypeKind.Interface)
            {
                // Fields in interface can only be static which is not considered a break.
                if (right is IFieldSymbol)
                    return;

                // Event and property accessors are covered by finding the property or event implementation
                // for interface member on the containing type.
                if (right is IMethodSymbol ms && IsEventOrPropertyAccessor(ms))
                    return;

                // If there is a default implementation provided is not a breaking change to add an interface member.
                if (right.ContainingType.FindImplementationForInterfaceMember(right) == null)
                {
                    differences.Add(new CompatDifference(
                        leftMetadata,
                        rightMetadata,
                        DiagnosticIds.CannotAddMemberToInterface,
                        string.Format(Resources.CannotAddMemberToInterface, right.ToDisplayString(SymbolExtensions.DisplayFormat), rightMetadata, leftMetadata),
                        DifferenceType.Added,
                        right));
                }
            }
        }

        private static bool IsEventOrPropertyAccessor(IMethodSymbol symbol) =>
            symbol.MethodKind == MethodKind.PropertyGet ||
            symbol.MethodKind == MethodKind.PropertySet ||
            symbol.MethodKind == MethodKind.EventAdd ||
            symbol.MethodKind == MethodKind.EventRemove;
    }
}
