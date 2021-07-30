// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    /// <summary>
    /// Class representing the context of the <see cref="IRuleRunner"/> used to run the rules.
    /// This is provided to each rule when initializing them to add events for the rules to be invoked.
    /// </summary>
    public class RuleRunnerContext
    {
        private readonly List<Action<IAssemblySymbol, IAssemblySymbol, string, string, IList<CompatDifference>>> _onAssemblySymbolActions = new();
        private readonly List<Action<ITypeSymbol, ITypeSymbol, string, string, IList<CompatDifference>>> _onTypeSymbolActions = new();
        private readonly List<Action<ISymbol, ISymbol, string, string, IList<CompatDifference>>> _onMemberSymbolActions = new();
        private readonly List<Action<ISymbol, ISymbol, ITypeSymbol, ITypeSymbol, string, string, IList<CompatDifference>>> _onMemberSymbolWithContainingTypeActions = new();

        /// <summary>
        /// Registers a callback to invoke when two <see cref="IAssemblySymbol"/> are compared.
        /// </summary>
        /// <param name="action">The action to invoke.</param>
        public void RegisterOnAssemblySymbolAction(Action<IAssemblySymbol, IAssemblySymbol, string, string, IList<CompatDifference>> action)
        {
            _onAssemblySymbolActions.Add(action);
        }

        /// <summary>
        /// Registers a callback to invoke when two <see cref="ITypeSymbol"/> are compared.
        /// </summary>
        /// <param name="action">The action to invoke.</param>
        public void RegisterOnTypeSymbolAction(Action<ITypeSymbol, ITypeSymbol, string, string, IList<CompatDifference>> action)
        {
            _onTypeSymbolActions.Add(action);
        }

        /// <summary>
        /// Registers a callback to invoke when two <see cref="ISymbol"/> members of a <see cref="ITypeSymbol"/> are compared.
        /// </summary>
        /// <param name="action">The action to invoke.</param>
        public void RegisterOnMemberSymbolAction(Action<ISymbol, ISymbol, string, string, IList<CompatDifference>> action)
        {
            _onMemberSymbolActions.Add(action);
        }
        /// <summary>
        /// Register a callback to invoke when two <see cref="ISymbol"/> members of a <see cref="ITypeSymbol"/>
        /// The action to register is invoked with the containing types for left and right. Sometimes this information
        /// is needed by some rules regardless of left or right being null.
        /// </summary>
        /// <param name="action">The action to invoke</param>
        public void RegisterOnMemberSymbolAction(Action<ISymbol, ISymbol, ITypeSymbol, ITypeSymbol, string, string, IList<CompatDifference>> action)
        {
            _onMemberSymbolWithContainingTypeActions.Add(action);
        }

        internal void RunOnAssemblySymbolActions(IAssemblySymbol left, IAssemblySymbol right, string leftName, string rightName, List<CompatDifference> differences)
        {
            foreach (Action<IAssemblySymbol, IAssemblySymbol, string, string, IList<CompatDifference>> action in _onAssemblySymbolActions)
            {
                action(left, right, leftName, rightName, differences);
            }
        }

        internal void RunOnTypeSymbolActions(ITypeSymbol left, ITypeSymbol right, string leftName, string rightName, List<CompatDifference> differences)
        {
            foreach (Action<ITypeSymbol, ITypeSymbol, string, string, IList<CompatDifference>> action in _onTypeSymbolActions)
            {
                action(left, right, leftName, rightName, differences);
            }
        }

        internal void RunOnMemberSymbolActions(ISymbol left, ISymbol right, ITypeSymbol leftContainingType, ITypeSymbol rightContainingType, string leftName, string rightName, List<CompatDifference> differences)
        {
            foreach (Action<ISymbol, ISymbol, string, string, IList<CompatDifference>> action in _onMemberSymbolActions)
            {
                action(left, right, leftName, rightName, differences);
            }

            foreach (Action<ISymbol, ISymbol, ITypeSymbol, ITypeSymbol, string, string, IList<CompatDifference>> action in _onMemberSymbolWithContainingTypeActions)
            {
                action(left, right, leftContainingType, rightContainingType, leftName, rightName, differences);
            }
        }
    }
}
