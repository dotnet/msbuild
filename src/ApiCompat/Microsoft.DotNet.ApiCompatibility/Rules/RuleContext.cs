// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    /// <summary>
    /// Class representing the context of the <see cref="IRuleRunner"/> used to register and run rule actions.
    /// </summary>
    public class RuleContext : IRuleContext
    {
        private readonly List<Action<IAssemblySymbol?, IAssemblySymbol?, MetadataInformation, MetadataInformation, bool, IList<CompatDifference>>> _onAssemblySymbolActions = new();
        private readonly List<Action<ITypeSymbol?, ITypeSymbol?, MetadataInformation, MetadataInformation, IList<CompatDifference>>> _onTypeSymbolActions = new();
        private readonly List<Action<ISymbol?, ISymbol?, MetadataInformation, MetadataInformation, IList<CompatDifference>>> _onMemberSymbolActions = new();
        private readonly List<Action<ISymbol?, ISymbol?, ITypeSymbol, ITypeSymbol, MetadataInformation, MetadataInformation, IList<CompatDifference>>> _onMemberSymbolWithContainingTypeActions = new();

        /// <inheritdoc />
        public void RegisterOnAssemblySymbolAction(Action<IAssemblySymbol?, IAssemblySymbol?, MetadataInformation, MetadataInformation, bool, IList<CompatDifference>> action)
        {
            _onAssemblySymbolActions.Add(action);
        }

        /// <inheritdoc />
        public void RegisterOnTypeSymbolAction(Action<ITypeSymbol?, ITypeSymbol?, MetadataInformation, MetadataInformation, IList<CompatDifference>> action)
        {
            _onTypeSymbolActions.Add(action);
        }

        /// <inheritdoc />
        public void RegisterOnMemberSymbolAction(Action<ISymbol?, ISymbol?, MetadataInformation, MetadataInformation, IList<CompatDifference>> action)
        {
            _onMemberSymbolActions.Add(action);
        }

        /// <inheritdoc />
        public void RegisterOnMemberSymbolAction(Action<ISymbol?, ISymbol?, ITypeSymbol, ITypeSymbol, MetadataInformation, MetadataInformation, IList<CompatDifference>> action)
        {
            _onMemberSymbolWithContainingTypeActions.Add(action);
        }

        /// <inheritdoc />
        public void RunOnAssemblySymbolActions(IAssemblySymbol? left, IAssemblySymbol? right, MetadataInformation leftMetadata, MetadataInformation rightMetadata, bool isSingleAssembly, IList<CompatDifference> differences)
        {
            foreach (Action<IAssemblySymbol?, IAssemblySymbol?, MetadataInformation, MetadataInformation, bool, IList<CompatDifference>> action in _onAssemblySymbolActions)
            {
                action(left, right, leftMetadata, rightMetadata, isSingleAssembly, differences);
            }
        }

        /// <inheritdoc />
        public void RunOnTypeSymbolActions(ITypeSymbol? left, ITypeSymbol? right, MetadataInformation leftMetadata, MetadataInformation rightMetadata, IList<CompatDifference> differences)
        {
            foreach (Action<ITypeSymbol?, ITypeSymbol?, MetadataInformation, MetadataInformation, IList<CompatDifference>> action in _onTypeSymbolActions)
            {
                action(left, right, leftMetadata, rightMetadata, differences);
            }
        }

        /// <inheritdoc />
        public void RunOnMemberSymbolActions(ISymbol? left, ISymbol? right, ITypeSymbol leftContainingType, ITypeSymbol rightContainingType, MetadataInformation leftMetadata, MetadataInformation rightMetadata, IList<CompatDifference> differences)
        {
            foreach (Action<ISymbol?, ISymbol?, MetadataInformation, MetadataInformation, IList<CompatDifference>> action in _onMemberSymbolActions)
            {
                action(left, right, leftMetadata, rightMetadata, differences);
            }

            foreach (Action<ISymbol?, ISymbol?, ITypeSymbol, ITypeSymbol, MetadataInformation, MetadataInformation, IList<CompatDifference>> action in _onMemberSymbolWithContainingTypeActions)
            {
                action(left, right, leftContainingType, rightContainingType, leftMetadata, rightMetadata, differences);
            }
        }
    }
}
