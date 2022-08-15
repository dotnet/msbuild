// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    /// <summary>
    /// Class representing the context of the <see cref="IRuleRunner"/> used to register and run rule actions.
    /// </summary>
    public class RuleContext : IRuleContext
    {
        private readonly List<Action<IAssemblySymbol?, IAssemblySymbol?, string, string, IList<CompatDifference>>> _onAssemblySymbolActions = new();
        private readonly List<Action<ITypeSymbol?, ITypeSymbol?, string, string, IList<CompatDifference>>> _onTypeSymbolActions = new();
        private readonly List<Action<ISymbol?, ISymbol?, string, string, IList<CompatDifference>>> _onMemberSymbolActions = new();
        private readonly List<Action<ISymbol?, ISymbol?, ITypeSymbol, ITypeSymbol, string, string, IList<CompatDifference>>> _onMemberSymbolWithContainingTypeActions = new();

        /// <inheritdoc> />
        public void RegisterOnAssemblySymbolAction(Action<IAssemblySymbol?, IAssemblySymbol?, string, string, IList<CompatDifference>> action)
        {
            _onAssemblySymbolActions.Add(action);
        }

        /// <inheritdoc> />
        public void RegisterOnTypeSymbolAction(Action<ITypeSymbol?, ITypeSymbol?, string, string, IList<CompatDifference>> action)
        {
            _onTypeSymbolActions.Add(action);
        }

        /// <inheritdoc> />
        public void RegisterOnMemberSymbolAction(Action<ISymbol?, ISymbol?, string, string, IList<CompatDifference>> action)
        {
            _onMemberSymbolActions.Add(action);
        }

        /// <inheritdoc> />
        public void RegisterOnMemberSymbolAction(Action<ISymbol?, ISymbol?, ITypeSymbol, ITypeSymbol, string, string, IList<CompatDifference>> action)
        {
            _onMemberSymbolWithContainingTypeActions.Add(action);
        }

        /// <inheritdoc> />
        public void RunOnAssemblySymbolActions(IAssemblySymbol? left, IAssemblySymbol? right, string leftName, string rightName, IList<CompatDifference> differences)
        {
            foreach (Action<IAssemblySymbol?, IAssemblySymbol?, string, string, IList<CompatDifference>> action in _onAssemblySymbolActions)
            {
                action(left, right, leftName, rightName, differences);
            }
        }

        /// <inheritdoc> />
        public void RunOnTypeSymbolActions(ITypeSymbol? left, ITypeSymbol? right, string leftName, string rightName, IList<CompatDifference> differences)
        {
            foreach (Action<ITypeSymbol?, ITypeSymbol?, string, string, IList<CompatDifference>> action in _onTypeSymbolActions)
            {
                action(left, right, leftName, rightName, differences);
            }
        }

        /// <inheritdoc> />
        public void RunOnMemberSymbolActions(ISymbol? left, ISymbol? right, ITypeSymbol leftContainingType, ITypeSymbol rightContainingType, string leftName, string rightName, IList<CompatDifference> differences)
        {
            foreach (Action<ISymbol?, ISymbol?, string, string, IList<CompatDifference>> action in _onMemberSymbolActions)
            {
                action(left, right, leftName, rightName, differences);
            }

            foreach (Action<ISymbol?, ISymbol?, ITypeSymbol, ITypeSymbol, string, string, IList<CompatDifference>> action in _onMemberSymbolWithContainingTypeActions)
            {
                action(left, right, leftContainingType, rightContainingType, leftName, rightName, differences);
            }
        }
    }
}
