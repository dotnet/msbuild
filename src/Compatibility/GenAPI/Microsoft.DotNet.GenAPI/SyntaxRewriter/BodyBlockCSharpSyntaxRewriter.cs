// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
            // visit subtree first to normalize type names.
            if (base.VisitConstructorDeclaration(node) is not ConstructorDeclarationSyntax rs)
                return null;

            // if there is at least one reference parameter - generate non empty body.
            if (node.ParameterList.Parameters.Any(p => p.Modifiers.Any(m => m.IsKind(SyntaxKind.OutKeyword))))
            {
                rs = rs.WithBody(GetThrowNullBody());
            }
            else
            {
                rs = rs.WithBody(GetEmptyBody());
            }

            return rs;
        }

        /// <inheritdoc />
        public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            // visit subtree first to normalize type names.
            if (base.VisitMethodDeclaration(node) is not MethodDeclarationSyntax rs)
                return null;

            if (rs.Modifiers.Any(token => token.IsKind(SyntaxKind.AbstractKeyword)) || rs.Body is null)
            {
                return rs;
            }

            if (rs.ExpressionBody is not null)
            {
                rs = rs.WithExpressionBody(null);
            }

            if (!(rs.ReturnType is PredefinedTypeSyntax predefined && predefined.Keyword.IsKind(SyntaxKind.VoidKeyword)) ||
                // if there is at least one reference parameter - generate non empty body.
                node.ParameterList.Parameters.Any(p => p.Modifiers.Any(m => m.IsKind(SyntaxKind.OutKeyword))))
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
            return base.VisitOperatorDeclaration(node) is OperatorDeclarationSyntax rs ?
                rs.WithBody(GetThrowNullBody()).WithParameterList(rs.ParameterList.WithTrailingTrivia(SyntaxFactory.Space)) :
                null;
        }

        /// <inheritdoc />
        public override SyntaxNode? VisitConversionOperatorDeclaration(ConversionOperatorDeclarationSyntax node)
        {
            return base.VisitConversionOperatorDeclaration(node) is ConversionOperatorDeclarationSyntax rs ?
                rs.WithBody(GetThrowNullBody()).WithParameterList(rs.ParameterList.WithTrailingTrivia(SyntaxFactory.Space)) :
                null;
        }

        /// <inheritdoc />
        public override SyntaxNode? VisitAccessorDeclaration(AccessorDeclarationSyntax node)
        {
            switch (node.Kind())
            {
                case SyntaxKind.GetAccessorDeclaration:
                case SyntaxKind.InitAccessorDeclaration:
                case SyntaxKind.SetAccessorDeclaration:
                    {
                        var accessorListSyntax = (AccessorListSyntax?)node.Parent;
                        if (accessorListSyntax?.Parent == null) break;

                        if (accessorListSyntax?.Parent is IndexerDeclarationSyntax indexerDeclarationSyntax)
                        {
                            var typeDeclarationSyntax = (TypeDeclarationSyntax?)indexerDeclarationSyntax.Parent;

                            if (indexerDeclarationSyntax.Modifiers.Any(token => token.IsKind(SyntaxKind.AbstractKeyword)) ||
                                (typeDeclarationSyntax != null && typeDeclarationSyntax.Keyword.IsKind(SyntaxKind.InterfaceKeyword)))
                            {
                                return node.WithSemicolonToken(node.SemicolonToken);
                            }

                            return ProcessPropertyDeclarationSyntax(node);
                        }
                        else if (accessorListSyntax?.Parent is PropertyDeclarationSyntax propertyDeclarationSyntax)
                        {
                            var typeDeclarationSyntax = (TypeDeclarationSyntax?)propertyDeclarationSyntax.Parent;

                            if (propertyDeclarationSyntax.Modifiers.Any(token => token.IsKind(SyntaxKind.AbstractKeyword)) ||
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

        private static BlockSyntax GetEmptyBody()
        {
            BlockSyntax node = GetMethodBodyFromText(SyntaxFactory.Space.ToString());
            return node.WithOpenBraceToken(node.OpenBraceToken.WithTrailingTrivia(SyntaxFactory.Space));
        }

        private BlockSyntax GetThrowNullBody() =>
            _exceptionMessage is not null ?
                GetMethodBodyFromText($"throw new PlatformNotSupportedException(\"{_exceptionMessage}\");") :
                GetMethodBodyFromText("throw null;");

        private SyntaxNode? ProcessPropertyDeclarationSyntax(AccessorDeclarationSyntax node)
        {
            if (node.Kind() == SyntaxKind.GetAccessorDeclaration)
            {
                node = node.WithBody(GetThrowNullBody());
            }
            else if (node.Kind() is SyntaxKind.SetAccessorDeclaration or SyntaxKind.InitAccessorDeclaration)
            {
                node = node.WithBody(GetEmptyBody());
            }

            return node.WithSemicolonToken(default)
                .WithKeyword(node.Keyword.WithTrailingTrivia(SyntaxFactory.Space));
        }

        private static BlockSyntax GetMethodBodyFromText(string text) =>
            SyntaxFactory.Block(SyntaxFactory.ParseStatement(text))
                .WithTrailingTrivia(SyntaxFactory.Space);
    }
}
