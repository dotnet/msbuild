// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.DotNet.GenAPI.SyntaxRewriter
{
    /// <summary>
    /// Handles type forward assembly attributes and removes generic type arguments:
    ///     [assembly:TypeForwardedToAttribute(typeof(System.Collections.Generic.IAsyncEnumerable<A, B, C>))] ->
    ///     [assembly:TypeForwardedToAttribute(typeof(System.Collections.Generic.IAsyncEnumerable<,,>))] ->
    /// </summary>
    public class TypeForwardAttributeCSharpSyntaxRewriter : CSharpSyntaxRewriter
    {
        public override SyntaxNode? VisitGenericName(GenericNameSyntax node)
        {
            // skip if not of `typeof(Type<A,B,C>)` format
            if (node.Parent?.Parent is not TypeOfExpressionSyntax)
                return node;

            TypeArgumentListSyntax typeArgumentList = node.TypeArgumentList;
            SeparatedSyntaxList<TypeSyntax> newArguments = new();

            foreach (TypeSyntax argument in typeArgumentList.Arguments)
            {
                if (argument is IdentifierNameSyntax identifier)
                {
                    newArguments = newArguments.Add(identifier.WithIdentifier(SyntaxFactory.Identifier(string.Empty)));
                }
                else
                {
                    newArguments = newArguments.Add(argument);
                }
            }

            typeArgumentList = typeArgumentList.WithArguments(newArguments);
            return node.WithTypeArgumentList(typeArgumentList);
        }
    }
}
