// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.DotNet.GenAPI.SyntaxRewriter
{
    /// <summary>
    /// Removes static keyword if field has both static and const modifiers.
    /// TODO: remove when https://github.com/dotnet/arcade/issues/11934 issue is fixed.
    /// </summary>
    public class FieldDeclarationCSharpSyntaxRewriter : CSharpSyntaxRewriter
    {
        /// <inheritdoc />
        public override SyntaxNode? VisitFieldDeclaration(FieldDeclarationSyntax node)
        {
            SyntaxTokenList modifiers = node.Modifiers;

            if (modifiers.Any(m => m.RawKind == (int)SyntaxKind.StaticKeyword) &&
                modifiers.Any(m => m.RawKind == (int)SyntaxKind.ConstKeyword))
            {
                return node.WithModifiers(modifiers.RemoveAt(modifiers.IndexOf(SyntaxKind.StaticKeyword)));
            }
            return node;
        }
    }
}
