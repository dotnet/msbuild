// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    /// <summary>
    /// This class implements a rule to check that the constant values for an enum's fields don't change.
    /// </summary>
    public class EnumsMustMatch : IRule
    {
        private readonly IRuleSettings _settings;

        public EnumsMustMatch(IRuleSettings settings, IRuleRegistrationContext context)
        {
            _settings = settings;
            context.RegisterOnTypeSymbolAction(RunOnTypeSymbol);
        }

        private void RunOnTypeSymbol(ITypeSymbol? left, ITypeSymbol? right, MetadataInformation leftMetadata, MetadataInformation rightMetadata, IList<CompatDifference> differences)
        {
            // Ensure that this rule only runs on enums.
            if (!IsEnum(left) || !IsEnum(right))
            {
                return;
            }

            // Enum must be a named type to access its underlying type.
            if (left is not INamedTypeSymbol l || right is not INamedTypeSymbol r)
            {
                return;
            }

            // Get enum's underlying type.
            if (l.EnumUnderlyingType is not INamedTypeSymbol leftType || r.EnumUnderlyingType is not INamedTypeSymbol rightType)
            {
                return;
            }

            // Check that the underlying types are equal and if not, emit a diagnostic.
            if (!_settings.SymbolEqualityComparer.Equals(leftType, rightType))
            {
                differences.Add(new CompatDifference(
                    leftMetadata,
                    rightMetadata,
                    DiagnosticIds.EnumTypesMustMatch,
                    string.Format(Resources.EnumTypesMustMatch, left.Name, leftType, rightType),
                    DifferenceType.Changed,
                    right));
                return;
            }

            // If so, compare their fields.
            // Build a map of the enum's fields, keyed by the field names.
            Dictionary<string, IFieldSymbol> leftMembers = left.GetMembers()
                .Where(a => a.Kind == SymbolKind.Field)
                .Select(a => (IFieldSymbol)a)
                .ToDictionary(a => a.Name);
            Dictionary<string, IFieldSymbol> rightMembers = right.GetMembers()
                .Where(a => a.Kind == SymbolKind.Field)
                .Select(a => (IFieldSymbol)a)
                .ToDictionary(a => a.Name);

            // For each field that is present in the left and right, check that their constant values match.
            // Otherwise, emit a diagnostic.
            foreach (KeyValuePair<string, IFieldSymbol> lEntry in leftMembers)
            {
                if (!rightMembers.TryGetValue(lEntry.Key, out IFieldSymbol? rField))
                {
                    continue;
                }

                if (lEntry.Value.ConstantValue is not object lval || rField.ConstantValue is not object rval || !lval.Equals(rval))
                {
                    differences.Add(new CompatDifference(
                        leftMetadata,
                        rightMetadata,
                        DiagnosticIds.EnumValuesMustMatch,
                        string.Format(Resources.EnumValuesMustMatch, left.Name, lEntry.Key, lEntry.Value.ConstantValue, rField.ConstantValue),
                        DifferenceType.Changed,
                        rField));
                }
            }
        }

        private static bool IsEnum(ITypeSymbol? sym) => sym is not null && sym.TypeKind == TypeKind.Enum;
    }
}
