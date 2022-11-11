// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;
using Microsoft.CodeAnalysis.Text;
using Microsoft.DotNet.ApiSymbolExtensions;

namespace Microsoft.DotNet.GenAPI
{
    /// <summary>
    /// Processes assemly symbols to build correspoding structures in C# language.
    /// </summary>
    internal class CSharpFileBuilder : IAssemblySymbolWriter, IDisposable
    {
        private readonly TextWriter _textWriter;
        private readonly ISymbolFilter _symbolFilter;
        private readonly CSharpSyntaxRewriter _syntaxRewriter;

        private readonly AdhocWorkspace _adhocWorkspace;
        private readonly SyntaxGenerator _syntaxGenerator;

        public CSharpFileBuilder(ISymbolFilter symbolFilter, TextWriter textWriter, CSharpSyntaxRewriter syntaxRewriter)
        {
            _textWriter = textWriter;
            _symbolFilter = symbolFilter;
            _syntaxRewriter = syntaxRewriter;

            _adhocWorkspace = new AdhocWorkspace();
            _syntaxGenerator = SyntaxGenerator.GetGenerator(_adhocWorkspace, LanguageNames.CSharp);
        }

        /// <inheritdoc />
        public void WriteAssembly(IAssemblySymbol assembly) => Visit(assembly);

        private void Visit(IAssemblySymbol assembly)
        {
            var namespaceSymbols = EnumerateNamespaces(assembly).Where(_symbolFilter.Include);

            foreach (var namespaceSymbol in namespaceSymbols)
            {
                SyntaxNode? syntaxNode = Visit(namespaceSymbol);

                if (syntaxNode is not null)
                {
                    _syntaxRewriter.Visit(syntaxNode).WriteTo(_textWriter);
                    _textWriter.WriteLine();
                }
            }
        }

        private SyntaxNode? Visit(INamespaceSymbol namespaceSymbol)
        {
            var namespaceNode = _syntaxGenerator.NamespaceDeclaration(namespaceSymbol.ToDisplayString());

            var typeMembers = namespaceSymbol.GetTypeMembers().Where(_symbolFilter.Include);
            if (!typeMembers.Any())
            {
                return null;
            }

            foreach (var typeMember in typeMembers)
            {
                var typeDeclaration = _syntaxGenerator.DeclarationExt(typeMember);

                foreach (var attribute in typeMember.GetAttributes()
                    .Where(a => a.AttributeClass != null && _symbolFilter.Include(a.AttributeClass)))
                {
                    typeDeclaration = _syntaxGenerator.AddAttributes(typeDeclaration, _syntaxGenerator.Attribute(attribute));
                }

                typeDeclaration = Visit(typeDeclaration, typeMember);

                namespaceNode = _syntaxGenerator.AddMembers(namespaceNode, typeDeclaration);
            }

            return namespaceNode
                .WithAdditionalAnnotations(Formatter.Annotation, Simplifier.Annotation)
                .NormalizeWhitespace();
        }

        private SyntaxNode Visit(SyntaxNode namedTypeNode, INamedTypeSymbol namedType)
        {
            namedTypeNode = VisitInnerNamedTypes(namedTypeNode, namedType);

            foreach (var member in namedType.GetMembers().Where(_symbolFilter.Include))
            {
                var memberDeclaration = _syntaxGenerator.DeclarationExt(member);

                foreach (var attribute in member.GetAttributes()
                    .Where(a => a.AttributeClass != null && _symbolFilter.Include(a.AttributeClass)))
                {
                    memberDeclaration = _syntaxGenerator.AddAttributes(memberDeclaration, _syntaxGenerator.Attribute(attribute));
                }

                namedTypeNode = _syntaxGenerator.AddMembers(namedTypeNode, memberDeclaration);
            }

            return namedTypeNode;
        }

        private SyntaxNode VisitInnerNamedTypes(SyntaxNode namedTypeNode, INamedTypeSymbol namedType)
        {
            var innerNamedTypes = namedType.GetTypeMembers().Where(_symbolFilter.Include);

            foreach (var innerNamedType in innerNamedTypes)
            {
                var typeDeclaration = _syntaxGenerator.DeclarationExt(innerNamedType);
                typeDeclaration = Visit(typeDeclaration, innerNamedType);

                namedTypeNode = _syntaxGenerator.AddMembers(namedTypeNode, typeDeclaration);
            }

            return namedTypeNode;
        }

        private IEnumerable<INamespaceSymbol> EnumerateNamespaces(IAssemblySymbol assemblySymbol)
        {
            Stack<INamespaceSymbol> stack = new();
            stack.Push(assemblySymbol.GlobalNamespace);

            while (stack.TryPop(out var current))
            {
                yield return current;

                foreach (var subNamespace in current.GetNamespaceMembers())
                {
                    stack.Push(subNamespace);
                }
            }
        }

        /// <inheritdoc />
        public void Dispose() => _textWriter.Dispose();
    }
}
