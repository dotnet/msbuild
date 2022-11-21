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
using Microsoft.DotNet.ApiSymbolExtensions;

namespace Microsoft.DotNet.GenAPI
{
    /// <summary>
    /// Processes assemly symbols to build correspoding structures in C# language.
    /// </summary>
    public class CSharpFileBuilder : IAssemblySymbolWriter, IDisposable
    {
        private readonly TextWriter _textWriter;
        private readonly ISymbolFilter _symbolFilter;
        private readonly CSharpSyntaxRewriter _syntaxRewriter;

        private readonly AdhocWorkspace _adhocWorkspace;
        private readonly SyntaxGenerator _syntaxGenerator;

        private readonly IEnumerable<MetadataReference> _metadataReferences;

        public CSharpFileBuilder(
            ISymbolFilter symbolFilter,
            TextWriter textWriter,
            CSharpSyntaxRewriter syntaxRewriter,
            IEnumerable<MetadataReference> metadataReferences)
        {
            _textWriter = textWriter;
            _symbolFilter = symbolFilter;
            _syntaxRewriter = syntaxRewriter;

            _adhocWorkspace = new AdhocWorkspace();
            _syntaxGenerator = SyntaxGenerator.GetGenerator(_adhocWorkspace, LanguageNames.CSharp);

            _metadataReferences = metadataReferences;
        }

        /// <inheritdoc />
        public void WriteAssembly(IAssemblySymbol assembly) => Visit(assembly);

        private void Visit(IAssemblySymbol assembly)
        {
            Project project = _adhocWorkspace.AddProject(ProjectInfo.Create(
                ProjectId.CreateNewId(), VersionStamp.Create(), assembly.Name, assembly.Name, LanguageNames.CSharp));
            project = project.AddMetadataReferences(_metadataReferences);

            IEnumerable<INamespaceSymbol> namespaceSymbols = EnumerateNamespaces(assembly).Where(_symbolFilter.Include);
            List<SyntaxNode> namespaceSyntaxNodes = new();

            foreach (INamespaceSymbol namespaceSymbol in namespaceSymbols)
            {
                SyntaxNode? syntaxNode = Visit(namespaceSymbol);

                if (syntaxNode is not null)
                {
                    namespaceSyntaxNodes.Add(syntaxNode);
                }
            }

            SyntaxNode compilationUnit = _syntaxGenerator.CompilationUnit(namespaceSyntaxNodes)
                .WithAdditionalAnnotations(Formatter.Annotation, Simplifier.Annotation)
                .NormalizeWhitespace();

            compilationUnit = _syntaxRewriter.Visit(compilationUnit);

            Document document = project.AddDocument(assembly.Name, compilationUnit);
            document = Simplifier.ReduceAsync(document).Result;
            compilationUnit = document.GetSyntaxRootAsync().Result!;

            compilationUnit.WriteTo(_textWriter);
        }

        private SyntaxNode? Visit(INamespaceSymbol namespaceSymbol)
        {
            SyntaxNode namespaceNode = _syntaxGenerator.NamespaceDeclaration(namespaceSymbol.ToDisplayString());

            IEnumerable<INamedTypeSymbol> typeMembers = namespaceSymbol.GetTypeMembers().Where(_symbolFilter.Include);
            if (!typeMembers.Any())
            {
                return null;
            }

            foreach (INamedTypeSymbol typeMember in typeMembers)
            {
                SyntaxNode typeDeclaration = _syntaxGenerator.DeclarationExt(typeMember);

                foreach (AttributeData attribute in typeMember.GetAttributes()
                    .Where(a => a.AttributeClass != null && _symbolFilter.Include(a.AttributeClass)))
                {
                    typeDeclaration = _syntaxGenerator.AddAttributes(typeDeclaration, _syntaxGenerator.Attribute(attribute));
                }

                typeDeclaration = Visit(typeDeclaration, typeMember);

                namespaceNode = _syntaxGenerator.AddMembers(namespaceNode, typeDeclaration);
            }

            return namespaceNode;
        }

        private SyntaxNode Visit(SyntaxNode namedTypeNode, INamedTypeSymbol namedType)
        {
            namedTypeNode = VisitInnerNamedTypes(namedTypeNode, namedType);
            foreach (ISymbol member in namedType.GetMembers().Where(_symbolFilter.Include))
            {
                SyntaxNode memberDeclaration = _syntaxGenerator.DeclarationExt(member);

                foreach (AttributeData attribute in member.GetAttributes()
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
            IEnumerable<INamedTypeSymbol> innerNamedTypes = namedType.GetTypeMembers().Where(_symbolFilter.Include);

            foreach (INamedTypeSymbol innerNamedType in innerNamedTypes)
            {
                SyntaxNode typeDeclaration = _syntaxGenerator.DeclarationExt(innerNamedType);
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
