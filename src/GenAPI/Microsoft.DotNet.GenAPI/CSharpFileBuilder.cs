// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Formatting;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Simplification;
using Microsoft.DotNet.ApiSymbolExtensions;
using Microsoft.DotNet.GenAPI.SyntaxRewriter;

namespace Microsoft.DotNet.GenAPI
{
    /// <summary>
    /// Processes assembly symbols to build corresponding structures in C# language.
    /// </summary>
    public class CSharpFileBuilder : IAssemblySymbolWriter, IDisposable
    {
        private readonly TextWriter _textWriter;
        private readonly ISymbolFilter _symbolFilter;
        private readonly string? _exceptionMessage;

        private readonly AdhocWorkspace _adhocWorkspace;
        private readonly SyntaxGenerator _syntaxGenerator;

        private readonly IEnumerable<MetadataReference> _metadataReferences;

        public CSharpFileBuilder(
            ISymbolFilter symbolFilter,
            TextWriter textWriter,
            string? exceptionMessage,
            IEnumerable<MetadataReference> metadataReferences)
        {
            _textWriter = textWriter;
            _symbolFilter = symbolFilter;
            _exceptionMessage = exceptionMessage;

            _adhocWorkspace = new AdhocWorkspace();
            _syntaxGenerator = SyntaxGenerator.GetGenerator(_adhocWorkspace, LanguageNames.CSharp);

            _metadataReferences = metadataReferences;
        }

        /// <inheritdoc />
        public void WriteAssembly(IAssemblySymbol assembly) => Visit(assembly);

        private void Visit(IAssemblySymbol assembly)
        {
            CSharpCompilationOptions compilationOptions = new(OutputKind.DynamicallyLinkedLibrary,
                    nullableContextOptions: NullableContextOptions.Enable);
            Project project = _adhocWorkspace.AddProject(ProjectInfo.Create(
                ProjectId.CreateNewId(), VersionStamp.Create(), assembly.Name, assembly.Name, LanguageNames.CSharp,
                compilationOptions: compilationOptions));
            project = project.AddMetadataReferences(_metadataReferences);

            IEnumerable<INamespaceSymbol> namespaceSymbols = EnumerateNamespaces(assembly).Where(_symbolFilter.Include);
            List<SyntaxNode> namespaceSyntaxNodes = new();

            foreach (INamespaceSymbol namespaceSymbol in namespaceSymbols.Order())
            {
                SyntaxNode? syntaxNode = Visit(namespaceSymbol);

                if (syntaxNode is not null)
                {
                    namespaceSyntaxNodes.Add(syntaxNode);
                }
            }

            SyntaxNode compilationUnit = _syntaxGenerator.CompilationUnit(namespaceSyntaxNodes)
                .WithAdditionalAnnotations(Formatter.Annotation, Simplifier.Annotation)
                .Rewrite(new TypeDeclarationCSharpSyntaxRewriter())
                .Rewrite(new BodyBlockCSharpSyntaxRewriter(_exceptionMessage))
                .Rewrite(new FieldDeclarationCSharpSyntaxRewriter())
                .NormalizeWhitespace();

            Document document = project.AddDocument(assembly.Name, compilationUnit);

            document = Simplifier.ReduceAsync(document).Result;
            document = Formatter.FormatAsync(document, DefineFormattingOptions()).Result;

            document.GetSyntaxRootAsync().Result!
                .Rewrite(new SingleLineStatementCSharpSyntaxRewriter())
                .WriteTo(_textWriter);
        }

        private SyntaxNode? Visit(INamespaceSymbol namespaceSymbol)
        {
            SyntaxNode namespaceNode = _syntaxGenerator.NamespaceDeclaration(namespaceSymbol.ToDisplayString());

            IEnumerable<INamedTypeSymbol> typeMembers = namespaceSymbol.GetTypeMembers().Where(_symbolFilter.Include);
            if (!typeMembers.Any())
            {
                return null;
            }

            foreach (INamedTypeSymbol typeMember in typeMembers.Order())
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
            IEnumerable<ISymbol> members = namedType.GetMembers().Where(_symbolFilter.Include);

            foreach (ISymbol member in members.Order())
            {
                SyntaxNode memberDeclaration = _syntaxGenerator.DeclarationExt(member);

                foreach (AttributeData attribute in member.GetAttributes()
                    .Where(a => a.AttributeClass != null && _symbolFilter.Include(a.AttributeClass)))
                {
                    memberDeclaration = _syntaxGenerator.AddAttributes(memberDeclaration, _syntaxGenerator.Attribute(attribute));
                }

                if (member is INamedTypeSymbol nestedTypeSymbol)
                {
                    memberDeclaration = Visit(memberDeclaration, nestedTypeSymbol);
                }

                namedTypeNode = _syntaxGenerator.AddMembers(namedTypeNode, memberDeclaration);
            }

            return namedTypeNode;
        }

        private IEnumerable<INamespaceSymbol> EnumerateNamespaces(IAssemblySymbol assemblySymbol)
        {
            Stack<INamespaceSymbol> stack = new();
            stack.Push(assemblySymbol.GlobalNamespace);

            while (stack.Count > 0)
            {
                INamespaceSymbol current = stack.Pop();

                yield return current;

                foreach (INamespaceSymbol subNamespace in current.GetNamespaceMembers())
                {
                    stack.Push(subNamespace);
                }
            }
        }

        private OptionSet DefineFormattingOptions()
        {
            /// TODO: consider to move configuration into file.
            return _adhocWorkspace.Options
                .WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInTypes, true)
                .WithChangedOption(CSharpFormattingOptions.WrappingKeepStatementsOnSingleLine, true)
                .WithChangedOption(CSharpFormattingOptions.WrappingPreserveSingleLine, true)
                .WithChangedOption(CSharpFormattingOptions.IndentBlock, false)
                .WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInMethods, false)
                .WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInProperties, false)
                .WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInAccessors, false)
                .WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInAnonymousMethods, false)
                .WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInControlBlocks, false)
                .WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInAnonymousTypes, false)
                .WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInObjectCollectionArrayInitializers, false)
                .WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInLambdaExpressionBody, false)
                .WithChangedOption(CSharpFormattingOptions.NewLineForMembersInObjectInit, false)
                .WithChangedOption(CSharpFormattingOptions.NewLineForMembersInAnonymousTypes, false)
                .WithChangedOption(CSharpFormattingOptions.NewLineForClausesInQuery, false);
        }

        /// <inheritdoc />
        public void Dispose() => _textWriter.Dispose();
    }
}
