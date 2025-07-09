// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.Build.Utilities.Analyzer
{
    /// <summary>
    /// Analyzer that bans APIs when used within implementations of IThreadSafeTask.
    /// Thread-safe tasks are run in parallel and must not use APIs that depend on process-global state
    /// such as current working directory, environment variables, or process-wide culture settings.
    /// </summary>
    public abstract class IThreadSafeTaskBannedAnalyzer<TSyntaxKind> : DiagnosticAnalyzer
        where TSyntaxKind : struct
    {
        private const string IThreadSafeTaskInterfaceName = "Microsoft.Build.Framework.IThreadSafeTask";
        private const string ConfigFileName = "IThreadSafeTask_BannedApis.txt";

        /// <summary>
        /// Diagnostic rule for detecting banned API usage in IThreadSafeTask implementations.
        /// </summary>
        public static readonly DiagnosticDescriptor ThreadSafeTaskSymbolIsBannedRule = new DiagnosticDescriptor(
            id: "MSB4260",
            title: "Symbol is banned in IThreadSafeTask implementations",
            messageFormat: "Symbol '{0}' is banned in IThreadSafeTask implementations{1}",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "This symbol is banned when used within types that implement IThreadSafeTask due to threading concerns. Thread-safe tasks should not use APIs that depend on process-global state such as current working directory, environment variables, or process-wide culture settings.");

        public sealed override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
            ImmutableArray.Create(ThreadSafeTaskSymbolIsBannedRule);

        protected abstract SymbolDisplayFormat SymbolDisplayFormat { get; }
        protected abstract bool IsTypeDeclaration(SyntaxNode node);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private void OnCompilationStart(CompilationStartAnalysisContext compilationContext)
        {
            var bannedApis = ReadBannedApis(compilationContext);
            if (bannedApis == null || bannedApis.Count == 0)
            {
                return;
            }

            // Register operation analysis
            compilationContext.RegisterOperationAction(
                context => AnalyzeOperationInContext(context, bannedApis),
                OperationKind.ObjectCreation,
                OperationKind.Invocation,
                OperationKind.EventReference,
                OperationKind.FieldReference,
                OperationKind.MethodReference,
                OperationKind.PropertyReference);
        }

        private Dictionary<string, BanFileEntry>? ReadBannedApis(CompilationStartAnalysisContext compilationContext)
        {
            var compilation = compilationContext.Compilation;
            var result = new Dictionary<string, BanFileEntry>();

            var query =
                from additionalFile in compilationContext.Options.AdditionalFiles
                let fileName = Path.GetFileName(additionalFile.Path)
                where fileName != null && fileName.Equals(ConfigFileName, StringComparison.OrdinalIgnoreCase)
                let sourceText = additionalFile.GetText(compilationContext.CancellationToken)
                where sourceText != null
                from line in sourceText.Lines
                let text = line.ToString()
                let commentIndex = text.IndexOf("//", StringComparison.Ordinal)
                let textWithoutComment = commentIndex == -1 ? text : text.Substring(0, commentIndex)
                where !string.IsNullOrWhiteSpace(textWithoutComment)
                let trimmedTextWithoutComment = textWithoutComment.TrimEnd()
                select trimmedTextWithoutComment;

            foreach (var line in query)
            {
                var parts = line.Split(';');
                if (parts.Length >= 1 && !string.IsNullOrWhiteSpace(parts[0]))
                {
                    var declarationId = parts[0].Trim();
                    var message = parts.Length > 1 ? parts[1].Trim() : "";
                    var symbols = GetSymbolsFromDeclarationId(compilation, declarationId);
                    
                    if (symbols.Any())
                    {
                        result[declarationId] = new BanFileEntry(declarationId, message, symbols);
                    }
                }
            }

            return result.Count > 0 ? result : null;
        }

        private ImmutableArray<ISymbol> GetSymbolsFromDeclarationId(Compilation compilation, string declarationId)
        {
            // Simple implementation using DocumentationCommentId
            try
            {
                var symbols = DocumentationCommentId.GetSymbolsForDeclarationId(declarationId, compilation);
                return symbols.ToArray().ToImmutableArray();
            }
            catch
            {
                // If parsing fails, return empty array
                return ImmutableArray<ISymbol>.Empty;
            }
        }

        private void AnalyzeOperationInContext(
            OperationAnalysisContext context,
            Dictionary<string, BanFileEntry> bannedApis)
        {
            // Check if we're in a class that implements IThreadSafeTask
            var containingType = GetContainingType(context.Operation);
            if (containingType == null || !IsIThreadSafeTaskImplementation(containingType))
            {
                return;
            }

            // Analyze the operation
            context.CancellationToken.ThrowIfCancellationRequested();
            
            switch (context.Operation)
            {
                case IInvocationOperation invocation:
                    VerifySymbol(context.ReportDiagnostic, invocation.TargetMethod, context.Operation.Syntax, bannedApis);
                    VerifyType(context.ReportDiagnostic, invocation.TargetMethod.ContainingType, context.Operation.Syntax, bannedApis);
                    break;

                case IMemberReferenceOperation memberReference:
                    VerifySymbol(context.ReportDiagnostic, memberReference.Member, context.Operation.Syntax, bannedApis);
                    VerifyType(context.ReportDiagnostic, memberReference.Member.ContainingType, context.Operation.Syntax, bannedApis);
                    break;

                case IObjectCreationOperation objectCreation:
                    if (objectCreation.Constructor != null)
                    {
                        VerifySymbol(context.ReportDiagnostic, objectCreation.Constructor, context.Operation.Syntax, bannedApis);
                    }
                    VerifyType(context.ReportDiagnostic, objectCreation.Type, context.Operation.Syntax, bannedApis);
                    break;
            }
        }

        private INamedTypeSymbol? GetContainingType(IOperation operation)
        {
            var current = operation;
            while (current != null)
            {
                if (current.SemanticModel != null)
                {
                    var typeDeclaration = current.Syntax.Ancestors().FirstOrDefault(IsTypeDeclaration);
                    if (typeDeclaration != null)
                    {
                        var symbol = current.SemanticModel.GetDeclaredSymbol(typeDeclaration);
                        if (symbol is INamedTypeSymbol typeSymbol)
                        {
                            return typeSymbol;
                        }
                    }
                }
                current = current.Parent;
            }
            return null;
        }

        private bool IsIThreadSafeTaskImplementation(INamedTypeSymbol typeSymbol)
        {
            return typeSymbol.AllInterfaces.Any(i => i.ToDisplayString() == IThreadSafeTaskInterfaceName);
        }

        private void VerifySymbol(
            Action<Diagnostic> reportDiagnostic,
            ISymbol symbol,
            SyntaxNode syntaxNode,
            Dictionary<string, BanFileEntry> bannedApis)
        {
            foreach (var kvp in bannedApis)
            {
                var declarationId = kvp.Key;
                var entry = kvp.Value;
                if (entry.Symbols.Any(bannedSymbol => SymbolEqualityComparer.Default.Equals(symbol, bannedSymbol)))
                {
                    var diagnostic = Diagnostic.Create(
                        ThreadSafeTaskSymbolIsBannedRule,
                        syntaxNode.GetLocation(),
                        symbol.ToDisplayString(SymbolDisplayFormat),
                        string.IsNullOrWhiteSpace(entry.Message) ? "" : ": " + entry.Message);
                    
                    reportDiagnostic(diagnostic);
                    return;
                }
            }
        }

        private void VerifyType(
            Action<Diagnostic> reportDiagnostic,
            ITypeSymbol? type,
            SyntaxNode syntaxNode,
            Dictionary<string, BanFileEntry> bannedApis)
        {
            if (type == null)
            {
                return;
            }

            foreach (var kvp in bannedApis)
            {
                var declarationId = kvp.Key;
                var entry = kvp.Value;
                if (entry.Symbols.Any(bannedSymbol => SymbolEqualityComparer.Default.Equals(type, bannedSymbol)))
                {
                    var diagnostic = Diagnostic.Create(
                        ThreadSafeTaskSymbolIsBannedRule,
                        syntaxNode.GetLocation(),
                        type.ToDisplayString(SymbolDisplayFormat),
                        string.IsNullOrWhiteSpace(entry.Message) ? "" : ": " + entry.Message);
                    
                    reportDiagnostic(diagnostic);
                    return;
                }
            }
        }

        private sealed class BanFileEntry
        {
            public string DeclarationId { get; }
            public string Message { get; }
            public ImmutableArray<ISymbol> Symbols { get; }

            public BanFileEntry(string declarationId, string message, ImmutableArray<ISymbol> symbols)
            {
                DeclarationId = declarationId;
                Message = message;
                Symbols = symbols;
            }
        }
    }
}
