// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
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
