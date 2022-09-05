// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;
using Microsoft.DotNet.ApiCompatibility.Extensions;

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    public class CannotRemoveBaseTypeOrInterface : IRule
    {
        private readonly RuleSettings _settings;

        public CannotRemoveBaseTypeOrInterface(RuleSettings settings, IRuleRegistrationContext context)
        {
            _settings = settings;
            context.RegisterOnTypeSymbolAction(RunOnTypeSymbol);
        }

        private void RunOnTypeSymbol(ITypeSymbol? left, ITypeSymbol? right, string leftName, string rightName, IList<CompatDifference> differences)
        {
            if (left == null || right == null)
                return;

            if (left.TypeKind != TypeKind.Interface && right.TypeKind != TypeKind.Interface)
            {
                // if left and right are not interfaces check base types
                ValidateBaseTypeNotRemoved(left, right, leftName, rightName, differences);

                if (_settings.StrictMode)
                    ValidateBaseTypeNotRemoved(right, left, rightName, leftName, differences);
            }

            ValidateInterfaceNotRemoved(left, right, leftName, rightName, differences);

            if (_settings.StrictMode)
                ValidateInterfaceNotRemoved(right, left, rightName, leftName, differences);
        }

        private void ValidateBaseTypeNotRemoved(ITypeSymbol left, ITypeSymbol right, string leftName, string rightName, IList<CompatDifference> differences)
        {
            ITypeSymbol? leftBaseType = left.BaseType;
            ITypeSymbol? rightBaseType = right.BaseType;

            if (leftBaseType == null)
                return;

            if (leftBaseType.TypeKind == TypeKind.Error && _settings.WithReferences)
            {
                AddAssemblyLoadError(differences, leftBaseType);
            }

            while (rightBaseType != null)
            {
                // If we found the immediate left base type on right we can assume
                // that any removal of a base type up on the hierarchy will be handled
                // when validating the type which it's base type was actually removed.
                if (_settings.SymbolComparer.Equals(leftBaseType, rightBaseType))
                    return;

                if (rightBaseType.TypeKind == TypeKind.Error && _settings.WithReferences)
                {
                    AddAssemblyLoadError(differences, rightBaseType);
                }

                rightBaseType = rightBaseType.BaseType;
            }
            
            differences.Add(new CompatDifference(
                DiagnosticIds.CannotRemoveBaseType,
                string.Format(Resources.CannotRemoveBaseType, left.ToDisplayString(), leftBaseType.ToDisplayString(), rightName, leftName),
                DifferenceType.Changed,
                right));
        }

        private void ValidateInterfaceNotRemoved(ITypeSymbol left, ITypeSymbol right, string leftName, string rightName, IList<CompatDifference> differences)
        {
            HashSet<ITypeSymbol> rightInterfaces = new(right.GetAllBaseInterfaces(), _settings.SymbolComparer);

            foreach (ITypeSymbol leftInterface in left.GetAllBaseInterfaces())
            {
                if (leftInterface.TypeKind == TypeKind.Error && _settings.WithReferences)
                {
                    AddAssemblyLoadError(differences, leftInterface);
                }

                // Ignore non visible interfaces based on the run Settings
                // If TypeKind == Error it means the Roslyn couldn't resolve it,
                // so we are running with a missing assembly reference to where that typeref is defined.
                // However we still want to consider it as Roslyn does resolve it's name correctly.
                if (!leftInterface.IsVisibleOutsideOfAssembly(_settings.IncludeInternalSymbols) && leftInterface.TypeKind != TypeKind.Error)
                    return;

                if (!rightInterfaces.Contains(leftInterface))
                {
                    differences.Add(new CompatDifference(
                        DiagnosticIds.CannotRemoveBaseInterface,
                        string.Format(Resources.CannotRemoveBaseInterface, left.ToDisplayString(), leftInterface.ToDisplayString(), rightName, leftName),
                        DifferenceType.Changed,
                        right));
                    return;
                }
            }

            foreach (ITypeSymbol rightInterface in rightInterfaces)
            {
                if (rightInterface.TypeKind == TypeKind.Error && _settings.WithReferences)
                {
                    AddAssemblyLoadError(differences, rightInterface);
                }
            }
        }

        private static void AddAssemblyLoadError(IList<CompatDifference> differences, ITypeSymbol type)
        {
            differences.Add(new CompatDifference(
                DiagnosticIds.AssemblyReferenceNotFound,
                string.Format(Resources.MatchingAssemblyNotFound, $"{type.ContainingAssembly.Name}.dll"),
                DifferenceType.Changed,
                string.Empty));
        }
    }
}
