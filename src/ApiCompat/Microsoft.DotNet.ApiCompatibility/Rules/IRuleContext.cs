// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    /// <summary>
    /// Interface representing the context of the <see cref="IRuleRunner"/> used to register and run rule actions.
    /// </summary>
    public interface IRuleContext : IRuleRegistrationContext, IRuleRunnerContext
    {
    }

    /// <summary>
    /// Interface representing the context of the <see cref="IRuleRunner"/> used to register rule actions.
    /// This is provided to each rule when instantiating them to add events for the rules to be invoked.
    /// </summary>
    public interface IRuleRegistrationContext
    {
        /// <summary>
        /// Registers a callback to invoke when two <see cref="IAssemblySymbol"/> are compared.
        /// </summary>
        /// <param name="action">The action to invoke.</param>
        void RegisterOnAssemblySymbolAction(Action<IAssemblySymbol?, IAssemblySymbol?, MetadataInformation, MetadataInformation, bool, IList<CompatDifference>> action);

        /// <summary>
        /// Registers a callback to invoke when two <see cref="ITypeSymbol"/> are compared.
        /// </summary>
        /// <param name="action">The action to invoke.</param>
        void RegisterOnTypeSymbolAction(Action<ITypeSymbol?, ITypeSymbol?, MetadataInformation, MetadataInformation, IList<CompatDifference>> action);

        /// <summary>
        /// Registers a callback to invoke when two <see cref="ISymbol"/> members of a <see cref="ITypeSymbol"/> are compared.
        /// </summary>
        /// <param name="action">The action to invoke.</param>
        void RegisterOnMemberSymbolAction(Action<ISymbol?, ISymbol?, MetadataInformation, MetadataInformation, IList<CompatDifference>> action);

        /// <summary>
        /// Register a callback to invoke when two <see cref="ISymbol"/> members of a <see cref="ITypeSymbol"/>
        /// The action to register is invoked with the containing types for left and right. Sometimes this information
        /// is needed by some rules regardless of left or right being null.
        /// </summary>
        /// <param name="action">The action to invoke</param>
        void RegisterOnMemberSymbolAction(Action<ISymbol?, ISymbol?, ITypeSymbol, ITypeSymbol, MetadataInformation, MetadataInformation, IList<CompatDifference>> action);
    }

    /// <summary>
    /// Interface representing the context of the <see cref="IRuleRunner"/> used to run registered rule actions.
    /// </summary>
    public interface IRuleRunnerContext
    {
        void RunOnAssemblySymbolActions(IAssemblySymbol? left, IAssemblySymbol? right, MetadataInformation leftMetadata, MetadataInformation rightMetadata, bool isSingleAssembly, IList<CompatDifference> differences);

        void RunOnTypeSymbolActions(ITypeSymbol? left, ITypeSymbol? right, MetadataInformation leftMetadata, MetadataInformation rightMetadata, IList<CompatDifference> differences);

        void RunOnMemberSymbolActions(ISymbol? left, ISymbol? right, ITypeSymbol leftContainingType, ITypeSymbol rightContainingType, MetadataInformation leftMetadata, MetadataInformation rightMetadata, IList<CompatDifference> differences);
    }
}
