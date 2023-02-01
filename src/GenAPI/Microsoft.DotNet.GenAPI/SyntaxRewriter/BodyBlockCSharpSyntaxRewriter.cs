// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.DotNet.GenAPI.SyntaxRewriter
{
    /// <summary>
    /// Represents a <see cref="CSharpSyntaxVisitor{TResult}"/> which descends an entire <see cref="CSharpSyntaxNode"/> graph and
    /// modify visited constructor, method declarations SyntaxNodes in depth-first order.
    /// Rewrites body with default implementation details.
    /// </summary>
    public class BodyBlockCSharpSyntaxRewriter : CSharpSyntaxRewriter
    {
        private readonly string? _exceptionMessage;

        public BodyBlockCSharpSyntaxRewriter(string? exceptionMessage)
        {
            _exceptionMessage = exceptionMessage;
        }

        /// <inheritdoc />
        public override SyntaxNode? VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            node = node.WithBody(GetEmptyBody())
                       .WithParameterList(node.ParameterList.WithTrailingTrivia(SyntaxFactory.Space));
            return base.VisitConstructorDeclaration(node);
        }

        /// <inheritdoc />
        public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            // visit subtree first to normalize type names.
            MethodDeclarationSyntax? rs = (MethodDeclarationSyntax?)base.VisitMethodDeclaration(node);
            if (rs is null) return rs;

            if (rs.Modifiers.Where(token => token.IsKind(SyntaxKind.AbstractKeyword)).Any() || rs.Body is null)
            {
                return rs;
            }

            if (rs.ExpressionBody != null)
            {
                rs = rs.WithExpressionBody(null);
            }

            string returnType = rs.ReturnType.ToString();
            if (returnType != "void" && returnType != "System.Void")
            {
                rs = rs.WithBody(GetThrowNullBody());
            }
            else
            {
                rs = rs.WithBody(GetEmptyBody());
            }

            return rs.WithParameterList(rs.ParameterList.WithTrailingTrivia(SyntaxFactory.Space));
        }

        /// <inheritdoc />
        public override SyntaxNode? VisitOperatorDeclaration(OperatorDeclarationSyntax node)
        {
            // visit subtree first to normalize type names.
            OperatorDeclarationSyntax? rs = base.VisitOperatorDeclaration(node) as OperatorDeclarationSyntax;
            return rs?
                .WithBody(GetThrowNullBody())
                .WithParameterList(rs.ParameterList.WithTrailingTrivia(SyntaxFactory.Space));
        }

        /// <inheritdoc />
        public override SyntaxNode? VisitConversionOperatorDeclaration(ConversionOperatorDeclarationSyntax node)
        {
            ConversionOperatorDeclarationSyntax? rs = base.VisitConversionOperatorDeclaration(node) as ConversionOperatorDeclarationSyntax;
            return rs?
                .WithBody(GetThrowNullBody())
                .WithParameterList(rs.ParameterList.WithTrailingTrivia(SyntaxFactory.Space));
        }

        /// <inheritdoc />
        public override SyntaxNode? VisitAccessorDeclaration(AccessorDeclarationSyntax node)
        {
            switch (node.Kind())
            {
                case SyntaxKind.GetAccessorDeclaration:
                case SyntaxKind.SetAccessorDeclaration:
                    {
                        var accessorListSyntax = (AccessorListSyntax?)node.Parent;
                        if (accessorListSyntax?.Parent == null) break;

                        if (accessorListSyntax?.Parent is IndexerDeclarationSyntax indexerDeclarationSyntax)
                        {
                            var typeDeclarationSyntax = (TypeDeclarationSyntax?)indexerDeclarationSyntax.Parent;

                            if (indexerDeclarationSyntax.Modifiers.Where(token => token.IsKind(SyntaxKind.AbstractKeyword)).Any() ||
                                (typeDeclarationSyntax != null && typeDeclarationSyntax.Keyword.IsKind(SyntaxKind.InterfaceKeyword)))
                            {
                                return node.WithSemicolonToken(node.SemicolonToken);
                            }

                            return ProcessPropertyDeclarationSyntax(node);
                        }
                        else if (accessorListSyntax?.Parent is PropertyDeclarationSyntax propertyDeclarationSyntax)
                        {
                            var typeDeclarationSyntax = (TypeDeclarationSyntax?)propertyDeclarationSyntax.Parent;

                            if (propertyDeclarationSyntax.Modifiers.Where(token => token.IsKind(SyntaxKind.AbstractKeyword)).Any() ||
                                (typeDeclarationSyntax != null && typeDeclarationSyntax.Keyword.IsKind(SyntaxKind.InterfaceKeyword)))
                            {
                                return node.WithSemicolonToken(node.SemicolonToken);
                            }

                            return ProcessPropertyDeclarationSyntax(node);
                        }
                    }
                    break;
            }
            return base.VisitAccessorDeclaration(node);
        }

        private BlockSyntax GetEmptyBody()
        {
            BlockSyntax node = GetMethodBodyFromText(SyntaxFactory.Space.ToString());
            return node.WithOpenBraceToken(node.OpenBraceToken.WithTrailingTrivia(SyntaxFactory.Space));
        }

        private BlockSyntax GetThrowNullBody()
        {
            if (_exceptionMessage is not null)
            {
                return GetMethodBodyFromText($"throw new PlatformNotSupportedException(\"{_exceptionMessage}\");");
            }
            return GetMethodBodyFromText("throw null;");
        }

        private SyntaxNode? ProcessPropertyDeclarationSyntax(AccessorDeclarationSyntax node)
        {
            if (node.Kind() == SyntaxKind.GetAccessorDeclaration)
            {
                node = node.WithBody(GetThrowNullBody());
            }
            else if (node.Kind() == SyntaxKind.SetAccessorDeclaration)
            {
                node = node.WithBody(GetEmptyBody());
            }
            return node.WithSemicolonToken(default).WithKeyword(node.Keyword.WithTrailingTrivia(SyntaxFactory.Space));
        }

        private BlockSyntax GetMethodBodyFromText(string text) =>
            SyntaxFactory.Block(SyntaxFactory.ParseStatement(text))
                .WithTrailingTrivia(SyntaxFactory.Space);
    }
}
