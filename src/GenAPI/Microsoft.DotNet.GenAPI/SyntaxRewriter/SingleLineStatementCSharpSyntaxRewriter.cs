// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
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
            return VisitBaseMethodDeclarationSyntax(node);
        }

        /// <inheritdoc />
        public override SyntaxNode? VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            return VisitBaseMethodDeclarationSyntax(node);
        }

        /// <inheritdoc />
        public override SyntaxNode? VisitOperatorDeclaration(OperatorDeclarationSyntax node)
        {
            return VisitBaseMethodDeclarationSyntax(node);
        }

        /// <inheritdoc />
        public override SyntaxNode? VisitConversionOperatorDeclaration(ConversionOperatorDeclarationSyntax node)
        {
            return VisitBaseMethodDeclarationSyntax(node);
        }

        /// <inheritdoc />
        public override SyntaxNode? VisitIndexerDeclaration(IndexerDeclarationSyntax node)
        {
            return VisitBasePropertyDeclarationSyntax(node);
        }

        /// <inheritdoc />
        public override SyntaxNode? VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            return VisitBasePropertyDeclarationSyntax(node);
        }

        private static SyntaxNode? VisitBaseMethodDeclarationSyntax(BaseMethodDeclarationSyntax node)
        {
            BlockSyntax? body = node.Body;
            if (body != null)
            {
                body = body
                    .WithOpenBraceToken(body.OpenBraceToken.WithTrailingTrivia())
                    .WithCloseBraceToken(body.CloseBraceToken.WithLeadingTrivia(SyntaxFactory.Space));

                if (body.Statements.Any() && !body.Statements[0].FullSpan.IsEmpty)
                {
                    body = body
                        .WithOpenBraceToken(body.OpenBraceToken.WithTrailingTrivia(SyntaxFactory.Space))
                        .WithStatements(new(body.Statements[0].WithoutLeadingTrivia().WithoutTrailingTrivia()));
                }
            }

            return node.WithBody(body);
        }

        private static SyntaxNode? VisitBasePropertyDeclarationSyntax(BasePropertyDeclarationSyntax node)
        {
            AccessorListSyntax? accessorList = node.AccessorList;

            if (accessorList != null)
            {
                SyntaxList<AccessorDeclarationSyntax> accessors = accessorList.Accessors;
                for (int i = 0; i < accessors.Count; ++i)
                {
                    AccessorDeclarationSyntax accessor = accessors.ElementAt(i);
                    BlockSyntax? body = accessor.Body;
                    if (body != null)
                    {
                        body = body
                        .WithOpenBraceToken(body.OpenBraceToken.WithTrailingTrivia())
                        .WithCloseBraceToken(body.CloseBraceToken.WithLeadingTrivia(SyntaxFactory.Space));

                        if (body.Statements.Any() && !body.Statements[0].FullSpan.IsEmpty)
                        {
                            body = body
                                .WithOpenBraceToken(body.OpenBraceToken.WithTrailingTrivia(SyntaxFactory.Space))
                                .WithStatements(new(body.Statements[0].WithoutLeadingTrivia().WithoutTrailingTrivia()));
                        }
                    }
                    AccessorDeclarationSyntax newAccessor = accessor
                        .WithBody(body)
                        .WithoutLeadingTrivia()
                        .WithTrailingTrivia(SyntaxFactory.Space);
                    if (i == 0)
                    {
                        newAccessor = newAccessor.WithLeadingTrivia(SyntaxFactory.Space);
                    }
                    accessors = accessors.Replace(accessor, newAccessor);
                }

                accessorList = accessorList
                    .WithOpenBraceToken(accessorList.OpenBraceToken.WithTrailingTrivia())
                    .WithCloseBraceToken(accessorList.CloseBraceToken.WithLeadingTrivia())
                    .WithAccessors(accessors);
            }

            return node.WithAccessorList(accessorList);
        }
    }
}
