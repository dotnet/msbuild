// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.DotNet.ApiSymbolExtensions;
using Microsoft.DotNet.ApiSymbolExtensions.Filtering;

namespace Microsoft.DotNet.GenAPI
{
    internal static class SyntaxNodeExtensions
    {
        public static SyntaxNode Rewrite(this SyntaxNode node, CSharpSyntaxRewriter rewriter) => rewriter.Visit(node);

        public static SyntaxNode AddMemberAttributes(this SyntaxNode node,
            SyntaxGenerator syntaxGenerator,
            ISymbolFilter symbolFilter,
            ISymbol member)
        {
            foreach (AttributeData attribute in member.GetAttributes().ExcludeNonVisibleOutsideOfAssembly(symbolFilter))
            {
                // The C# compiler emits the DefaultMemberAttribute on any type containing an indexer.
                // In C# it is an error to manually attribute a type with the DefaultMemberAttribute if the type also declares an indexer.
                if (member is INamedTypeSymbol typeMember && typeMember.HasIndexer() && attribute.IsDefaultMemberAttribute())
                {
                    continue;
                }
                node = syntaxGenerator.AddAttributes(node, syntaxGenerator.Attribute(attribute));

            }
            return node;
        }
    }
}
