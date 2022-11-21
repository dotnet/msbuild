// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.DotNet.GenAPI
{
    /// <summary>
    /// Represents a <see cref="CSharpSyntaxVisitor{TResult}"/> which descends an entire <see cref="CSharpSyntaxNode"/> graph and
    /// modify visited constructor, class, interface, struct, method declarations SyntaxNodes in depth-first order.
    /// </summary>
    public class CSharpSyntaxWriter : CSharpSyntaxRewriter
    {
        private readonly string? _exceptionMessage;

        public CSharpSyntaxWriter(string? exceptionMessage)
        {
            _exceptionMessage = exceptionMessage;
        }

        /// <inheritdoc />
        public override SyntaxNode? VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            node = node.WithBody(GetEmptyBody(true))
                       .WithParameterList(node.ParameterList.WithTrailingTrivia(SyntaxFactory.Space));
            return base.VisitConstructorDeclaration(node);
        }

        /// <inheritdoc />
        public override SyntaxNode? VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
        {
            InterfaceDeclarationSyntax? rs = (InterfaceDeclarationSyntax?)base.VisitInterfaceDeclaration(node);
            return rs is null ? rs : VisitCommonTypeDeclaration(rs);
        }

        /// <inheritdoc />
        public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            ClassDeclarationSyntax? rs = (ClassDeclarationSyntax?)base.VisitClassDeclaration(node);
            return rs is null ? rs : VisitCommonTypeDeclaration(rs);
        }

        /// <inheritdoc />
        public override SyntaxNode? VisitStructDeclaration(StructDeclarationSyntax node)
        {
            StructDeclarationSyntax? rs = (StructDeclarationSyntax?)base.VisitStructDeclaration(node);
            return rs is null ? rs : VisitCommonTypeDeclaration(node);
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

            if (rs.ReturnType.ToString() != "System.Void")
            {
                rs = rs.WithBody(GetThrowNullBody(true));
            }
            else
            {
                rs = rs.WithBody(GetEmptyBody(true));
            }

            return rs.WithParameterList(rs.ParameterList.WithTrailingTrivia(SyntaxFactory.Space));
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
                        var propertyDeclarationSyntax = (PropertyDeclarationSyntax?)accessorListSyntax?.Parent;

                        if (propertyDeclarationSyntax is null) return null;

                        if (propertyDeclarationSyntax.Modifiers.Where(token => token.IsKind(SyntaxKind.AbstractKeyword)).Any())
                        {
                            node = node.WithSemicolonToken(node.SemicolonToken);
                        }
                        else
                        {
                            if (node.Kind() == SyntaxKind.GetAccessorDeclaration)
                            {
                                node = node.WithBody(GetThrowNullBody(newLine: false));
                            }
                            else if (node.Kind() == SyntaxKind.SetAccessorDeclaration)
                            {
                                node = node.WithBody(GetEmptyBody(newLine: false));
                            }
                            node = node.WithSemicolonToken(default).WithKeyword(node.Keyword.WithTrailingTrivia(SyntaxFactory.Space));
                        }
                        return node;
                    }
            }
            return base.VisitAccessorDeclaration(node);
        }

        /// Removes the specified base type from a Class/struct/interface node.
        private static TypeDeclarationSyntax RemoveBaseType(TypeDeclarationSyntax node, string typeName)
        {
            var baseType = node.BaseList?.Types.FirstOrDefault(x => string.Equals(x.ToString(), typeName, StringComparison.OrdinalIgnoreCase));
            if (baseType == null)
            {
                // Base type not found
                return node;
            }

            var baseTypes = node.BaseList!.Types.Remove(baseType);
            if (baseTypes.Count == 0)
            {
                // No more base implementations, remove the base list entirely
                // Make sure we update the identifier though to include the baselist trailing trivia (typically '\r\n')
                // so the trailing opening brace gets put onto a new line.
                return node
                    .WithBaseList(null)
                    .WithIdentifier(node.Identifier.WithTrailingTrivia(node.BaseList.GetTrailingTrivia()));
            }
            else
            {
                // Remove the type but retain all remaining types and trivia
                return node.WithBaseList(node.BaseList!.WithTypes(baseTypes));
            }
        }

        private SyntaxNode? VisitCommonTypeDeclaration<T>(T node) where T : TypeDeclarationSyntax
        {
            node = (T)RemoveBaseType(node, "global::System.Object");
            return AddPartialModifier(node);
        }

        private SyntaxNode AddPartialModifier<T>(T node) where T: TypeDeclarationSyntax
        {
            if (!node.Modifiers.Any(m => m.RawKind == (int)SyntaxKind.PartialKeyword))
            {
                return node.AddModifiers(SyntaxFactory.Token(SyntaxKind.PartialKeyword).WithTrailingTrivia(SyntaxFactory.Space));
            }
            return node;
        }

        private BlockSyntax GetEmptyBody(bool newLine = false)
        {
            BlockSyntax node = GetMethodBodyFromText(SyntaxFactory.Space.ToString(), newLine);
            return node.WithOpenBraceToken(node.OpenBraceToken.WithTrailingTrivia(SyntaxFactory.Space));
        }

        private BlockSyntax GetThrowNullBody(bool newLine = false)
        {
            if (_exceptionMessage is not null)
            {
                return GetMethodBodyFromText($" throw new PlatformNotSupportedException(\"{_exceptionMessage}\"); ", newLine);
            }
            return GetMethodBodyFromText(" throw null; ", newLine);
        }

        private BlockSyntax GetMethodBodyFromText(string text, bool newLine = false) =>
            SyntaxFactory.Block(SyntaxFactory.ParseStatement(text))
                         .WithTrailingTrivia(newLine ? SyntaxFactory.CarriageReturnLineFeed : SyntaxFactory.Space);
    }
}
