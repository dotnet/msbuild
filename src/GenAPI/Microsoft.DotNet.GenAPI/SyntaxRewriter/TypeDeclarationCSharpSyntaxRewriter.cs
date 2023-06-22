// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.DotNet.GenAPI.SyntaxRewriter
{
    /// <summary>
    /// Represents a <see cref="CSharpSyntaxVisitor{TResult}"/> which descends an entire <see cref="CSharpSyntaxNode"/> graph and
    /// modify visited type declarations SyntaxNodes in depth-first order.
    /// Rewrites interface, struct, class type declaration:
    /// - adds partial keyword
    /// - remove Object from a list of base types.
    /// </summary>
    public class TypeDeclarationCSharpSyntaxRewriter : CSharpSyntaxRewriter
    {
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
            return rs is null ? rs : VisitCommonTypeDeclaration(rs);
        }

        /// Removes the specified base type from a Class/struct/interface node.
        private static TypeDeclarationSyntax RemoveBaseType(TypeDeclarationSyntax node, string typeName)
        {
            BaseTypeSyntax? baseType = node.BaseList?.Types.FirstOrDefault(x => string.Equals(x.ToString(), typeName, StringComparison.OrdinalIgnoreCase));
            if (baseType == null)
            {
                // Base type not found
                return node;
            }

            SeparatedSyntaxList<BaseTypeSyntax> baseTypes = node.BaseList!.Types.Remove(baseType);
            if (baseTypes.Count == 0)
            {
                // No more base implementations, remove the base list entirely
                // Make sure we update the identifier though to include the baselist trailing trivia (typically '\r\n')
                // so the trailing opening brace gets put onto a new line.
                return node
                    .WithBaseList(null)
                    .WithTrailingTrivia(node.BaseList.GetTrailingTrivia());
            }
            else
            {
                // Remove the type but retain all remaining types and trivia
                return node.WithBaseList(node.BaseList!.WithTypes(baseTypes));
            }
        }

        private static T VisitCommonTypeDeclaration<T>(T node) where T : TypeDeclarationSyntax
        {
            node = (T)RemoveBaseType(node, "global::System.Object");
            return AddPartialModifier(node);
        }

        private static T AddPartialModifier<T>(T node) where T : TypeDeclarationSyntax =>
            !node.Modifiers.Any(m => m.RawKind == (int)SyntaxKind.PartialKeyword) ?
                (T)node.AddModifiers(SyntaxFactory.Token(SyntaxKind.PartialKeyword).WithTrailingTrivia(SyntaxFactory.Space)) :
                node;
    }
}
