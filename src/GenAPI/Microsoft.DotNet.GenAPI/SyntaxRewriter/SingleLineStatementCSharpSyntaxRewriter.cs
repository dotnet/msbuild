// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.DotNet.GenAPI.SyntaxRewriter
{
    /// <summary>
    /// Removes multiline formatting for one line statements:
    /// ```int Execute() {
    ///     throw null;
    /// }```
    /// =>
    /// ```int Execute() { throw null; }```
    /// </summary>
    public class SingleLineStatementCSharpSyntaxRewriter : CSharpSyntaxRewriter
    {
        /// <inheritdoc />
        public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            BlockSyntax? body = node.Body;
            if (body != null && body.Statements.Count == 1)
            {
                body = body
                    .WithOpenBraceToken(body.OpenBraceToken.WithTrailingTrivia(SyntaxFactory.Space))
                    .WithCloseBraceToken(body.CloseBraceToken.WithLeadingTrivia(SyntaxFactory.Space))
                    .WithStatements(new(body.Statements[0].WithLeadingTrivia().WithoutTrailingTrivia()));
            }

            return node.WithBody(body);
        }
    }
}
