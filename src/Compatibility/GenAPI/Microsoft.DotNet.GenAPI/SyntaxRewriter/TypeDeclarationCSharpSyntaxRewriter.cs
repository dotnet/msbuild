// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;
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
            return VisitCommonTypeDeclaration(rs);
        }

        /// <inheritdoc />
        public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            ClassDeclarationSyntax? rs = (ClassDeclarationSyntax?)base.VisitClassDeclaration(node);
            return VisitCommonTypeDeclaration(rs);
        }

        /// <inheritdoc />
        public override SyntaxNode? VisitStructDeclaration(StructDeclarationSyntax node)
        {
            StructDeclarationSyntax? rs = (StructDeclarationSyntax?)base.VisitStructDeclaration(node);
            return VisitCommonTypeDeclaration(rs);
        }

        /// <inheritdoc />
        public override SyntaxNode? VisitRecordDeclaration(RecordDeclarationSyntax node)
        {
            RecordDeclarationSyntax? rs = (RecordDeclarationSyntax?)base.VisitRecordDeclaration(node);

            rs = RemoveBaseType(rs, x => Regex.IsMatch(x.ToString(), $"global::System.IEquatable<.*{rs!.Identifier}>"));

            return VisitCommonTypeDeclaration(rs);
        }

        /// Removes the specified base type from a Class/struct/interface node.
        private static T? RemoveBaseType<T>(T? node, string typeName) where T : TypeDeclarationSyntax =>
            RemoveBaseType(node, x => string.Equals(x.ToString(), typeName, StringComparison.OrdinalIgnoreCase));

        private static T? RemoveBaseType<T>(T? node, Func<BaseTypeSyntax, bool> selector) where T : TypeDeclarationSyntax
        {
            if (node == null)
            {
                return null;
            }

            BaseTypeSyntax? baseType = node.BaseList?.Types.FirstOrDefault(selector);
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
                return (T)node
                    .WithBaseList(null)
                    .WithTrailingTrivia(node.BaseList.GetTrailingTrivia());
            }
            else
            {
                // Remove the type but retain all remaining types and trivia
                return (T)node.WithBaseList(node.BaseList!.WithTypes(baseTypes));
            }
        }

        private static T? VisitCommonTypeDeclaration<T>(T? node) where T : TypeDeclarationSyntax
        {
            if (node == null)
            {
                return null;
            }

            node = RemoveBaseType(node, "global::System.Object");
            return AddPartialModifier(node);
        }

        private static T? AddPartialModifier<T>(T? node) where T : TypeDeclarationSyntax =>
            node is not null && !node.Modifiers.Any(m => m.RawKind == (int)SyntaxKind.PartialKeyword) ?
                (T)node.AddModifiers(SyntaxFactory.Token(SyntaxKind.PartialKeyword).WithTrailingTrivia(SyntaxFactory.Space)) :
                node;
    }
}
