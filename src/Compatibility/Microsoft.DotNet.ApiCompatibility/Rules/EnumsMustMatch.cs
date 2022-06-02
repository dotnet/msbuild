// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    /// <summary>
    /// This class implements a rule to check that the constant values for an enum's fields don't change.
    /// </summary>
    public class EnumsMustMatch : Rule
    {
        public override void Initialize(RuleRunnerContext context) => context.RegisterOnTypeSymbolAction(RunOnTypeSymbol);

        private bool IsEnum(ITypeSymbol sym) => sym is not null && sym.TypeKind == TypeKind.Enum;

        private void RunOnTypeSymbol(ITypeSymbol left, ITypeSymbol right, string leftName, string rightName, IList<CompatDifference> differences)
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

            // Check that the underlying types are equal.
            if (Settings.SymbolComparer.Equals(leftType, rightType))
            {
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
                    if (!rightMembers.TryGetValue(lEntry.Key, out IFieldSymbol rField))
                    {
                        continue;
                    }
                    if (lEntry.Value.ConstantValue is not object lval || rField.ConstantValue is not object rval || !lval.Equals(rval))
                    {
                        string msg = string.Format(Resources.EnumValuesMustMatch, left.Name, lEntry.Key, lEntry.Value.ConstantValue, rField.ConstantValue);
                        differences.Add(new CompatDifference(DiagnosticIds.EnumValuesMustMatch, msg, DifferenceType.Changed, rField));
                    }
                }
            }
            else
            {
                // Otherwise, emit a diagnostic.
                string msg = string.Format(Resources.EnumTypesMustMatch, left.Name, leftType, rightType);
                differences.Add(new CompatDifference(DiagnosticIds.EnumTypesMustMatch, msg, DifferenceType.Changed, right));
            }
        }
    }
}
