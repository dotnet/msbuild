// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.Build.Utilities.Analyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class CSharpIThreadSafeTaskBannedAnalyzer : IThreadSafeTaskBannedAnalyzer<Microsoft.CodeAnalysis.CSharp.SyntaxKind>
    {
        protected override SymbolDisplayFormat SymbolDisplayFormat => SymbolDisplayFormat.CSharpShortErrorMessageFormat;

        protected override bool IsTypeDeclaration(SyntaxNode node) =>
            node is ClassDeclarationSyntax ||
            node is StructDeclarationSyntax ||
            node is RecordDeclarationSyntax ||
            node is InterfaceDeclarationSyntax;
    }
}
