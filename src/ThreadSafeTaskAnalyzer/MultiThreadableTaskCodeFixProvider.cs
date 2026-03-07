// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace Microsoft.Build.TaskAuthoring.Analyzer
{
    /// <summary>
    /// Code fixer for the thread-safe task analyzer.
    /// Fixes:
    /// - MSBuildTask0002: Replaces banned APIs with TaskEnvironment equivalents
    /// - MSBuildTask0003: Wraps path arguments with TaskEnvironment.GetAbsolutePath()
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MultiThreadableTaskCodeFixProvider)), Shared]
    public sealed class MultiThreadableTaskCodeFixProvider : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds =>
            ImmutableArray.Create(DiagnosticIds.TaskEnvironmentRequired, DiagnosticIds.FilePathRequiresAbsolute);

        public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            if (root is null)
            {
                return;
            }

            foreach (var diagnostic in context.Diagnostics)
            {
                var node = root.FindNode(diagnostic.Location.SourceSpan);

                if (diagnostic.Id == DiagnosticIds.FilePathRequiresAbsolute)
                {
                    RegisterFilePathFix(context, node, diagnostic);
                }
                else if (diagnostic.Id == DiagnosticIds.TaskEnvironmentRequired)
                {
                    RegisterTaskEnvironmentFix(context, node, diagnostic);
                }
            }
        }

        private static void RegisterFilePathFix(CodeFixContext context, SyntaxNode node, Diagnostic diagnostic)
        {
            // Find the invocation or object creation expression
            var invocation = FindContainingCall(node);
            if (invocation is null)
            {
                return;
            }

            ArgumentListSyntax? argumentList = invocation switch
            {
                InvocationExpressionSyntax inv => inv.ArgumentList,
                ObjectCreationExpressionSyntax obj => obj.ArgumentList,
                ImplicitObjectCreationExpressionSyntax impl => impl.ArgumentList,
                _ => null,
            };

            if (argumentList is null || argumentList.Arguments.Count == 0)
            {
                return;
            }

            // Find the first argument that is NOT already wrapped with TaskEnvironment.GetAbsolutePath()
            ArgumentSyntax? targetArg = null;
            foreach (var arg in argumentList.Arguments)
            {
                if (!IsAlreadyWrapped(arg.Expression))
                {
                    targetArg = arg;
                    break;
                }
            }

            if (targetArg is null)
            {
                return;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Wrap with TaskEnvironment.GetAbsolutePath()",
                    createChangedDocument: ct => WrapArgumentWithGetAbsolutePathAsync(context.Document, targetArg, ct),
                    equivalenceKey: "WrapWithGetAbsolutePath"),
                diagnostic);
        }

        /// <summary>
        /// Checks whether an argument expression is already wrapped in TaskEnvironment.GetAbsolutePath().
        /// </summary>
        private static bool IsAlreadyWrapped(ExpressionSyntax expression)
        {
            if (expression is InvocationExpressionSyntax inv &&
                inv.Expression is MemberAccessExpressionSyntax ma &&
                ma.Name.Identifier.Text == "GetAbsolutePath")
            {
                var receiverName = GetSimpleTypeName(ma.Expression);
                return receiverName == "TaskEnvironment";
            }

            return false;
        }

        private static void RegisterTaskEnvironmentFix(CodeFixContext context, SyntaxNode node, Diagnostic diagnostic)
        {
            // Try to determine which API replacement to offer
            var invocation = node as InvocationExpressionSyntax ?? node.AncestorsAndSelf().OfType<InvocationExpressionSyntax>().FirstOrDefault();
            var memberAccess = node as MemberAccessExpressionSyntax ?? node.AncestorsAndSelf().OfType<MemberAccessExpressionSyntax>().FirstOrDefault();

            if (invocation is not null && invocation.Expression is MemberAccessExpressionSyntax invMemberAccess)
            {
                var targetTypeName = GetSimpleTypeName(invMemberAccess.Expression);
                var methodName = invMemberAccess.Name.Identifier.Text;

                if (targetTypeName == "Environment")
                {
                    switch (methodName)
                    {
                        case "GetEnvironmentVariable":
                            RegisterSimpleReplacement(context, diagnostic, invocation,
                                "TaskEnvironment", "GetEnvironmentVariable",
                                "Use TaskEnvironment.GetEnvironmentVariable()");
                            return;

                        case "SetEnvironmentVariable" when invocation.ArgumentList.Arguments.Count == 2:
                            RegisterSimpleReplacement(context, diagnostic, invocation,
                                "TaskEnvironment", "SetEnvironmentVariable",
                                "Use TaskEnvironment.SetEnvironmentVariable()");
                            return;

                        case "GetEnvironmentVariables":
                            RegisterSimpleReplacement(context, diagnostic, invocation,
                                "TaskEnvironment", "GetEnvironmentVariables",
                                "Use TaskEnvironment.GetEnvironmentVariables()");
                            return;
                    }
                }
                else if (targetTypeName == "Path" && methodName == "GetFullPath")
                {
                    // Only offer fix for single-argument overload
                    if (invocation.ArgumentList.Arguments.Count == 1)
                    {
                        RegisterSimpleReplacement(context, diagnostic, invocation,
                            "TaskEnvironment", "GetAbsolutePath",
                            "Use TaskEnvironment.GetAbsolutePath()");
                    }
                    return;
                }
                else if (targetTypeName == "Directory" && methodName == "GetCurrentDirectory")
                {
                    // Directory.GetCurrentDirectory() → TaskEnvironment.ProjectDirectory
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            title: "Use TaskEnvironment.ProjectDirectory",
                            createChangedDocument: ct => ReplaceInvocationWithPropertyAsync(
                                context.Document, invocation, "TaskEnvironment", "ProjectDirectory", ct),
                            equivalenceKey: "UseProjectDirectory"),
                        diagnostic);
                    return;
                }
            }

            // Handle Environment.CurrentDirectory (property access, not invocation)
            if (memberAccess is not null)
            {
                var targetTypeName = GetSimpleTypeName(memberAccess.Expression);
                var memberName = memberAccess.Name.Identifier.Text;

                if (targetTypeName == "Environment" && memberName == "CurrentDirectory")
                {
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            title: "Use TaskEnvironment.ProjectDirectory",
                            createChangedDocument: ct => ReplacePropertyAccessAsync(
                                context.Document, memberAccess, "TaskEnvironment", "ProjectDirectory", ct),
                            equivalenceKey: "UseProjectDirectory"),
                        diagnostic);
                }
            }
        }

        private static void RegisterSimpleReplacement(
            CodeFixContext context, Diagnostic diagnostic,
            InvocationExpressionSyntax invocation,
            string newTypeName, string newMethodName, string title)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: title,
                    createChangedDocument: ct => ReplaceInvocationTargetAsync(
                        context.Document, invocation, newTypeName, newMethodName, ct),
                    equivalenceKey: title),
                diagnostic);
        }

        private static async Task<Document> WrapArgumentWithGetAbsolutePathAsync(
            Document document, ArgumentSyntax argument, CancellationToken ct)
        {
            var editor = await DocumentEditor.CreateAsync(document, ct).ConfigureAwait(false);

            var wrappedExpr = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName("TaskEnvironment"),
                    SyntaxFactory.IdentifierName("GetAbsolutePath")),
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.Argument(argument.Expression))));

            var newArgument = argument.WithExpression(wrappedExpr);
            editor.ReplaceNode(argument, newArgument);

            return editor.GetChangedDocument();
        }

        private static async Task<Document> ReplaceInvocationTargetAsync(
            Document document, InvocationExpressionSyntax invocation,
            string newTypeName, string newMethodName, CancellationToken ct)
        {
            var editor = await DocumentEditor.CreateAsync(document, ct).ConfigureAwait(false);

            var newMemberAccess = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName(newTypeName),
                SyntaxFactory.IdentifierName(newMethodName));

            var newInvocation = invocation.WithExpression(newMemberAccess);
            editor.ReplaceNode(invocation, newInvocation);

            return editor.GetChangedDocument();
        }

        private static async Task<Document> ReplacePropertyAccessAsync(
            Document document, MemberAccessExpressionSyntax memberAccess,
            string newTypeName, string newPropertyName, CancellationToken ct)
        {
            var editor = await DocumentEditor.CreateAsync(document, ct).ConfigureAwait(false);

            var newExpression = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName(newTypeName),
                SyntaxFactory.IdentifierName(newPropertyName));

            editor.ReplaceNode(memberAccess, newExpression);

            return editor.GetChangedDocument();
        }

        /// <summary>
        /// Replaces an invocation (e.g. Directory.GetCurrentDirectory()) with a property access (e.g. TaskEnvironment.ProjectDirectory).
        /// </summary>
        private static async Task<Document> ReplaceInvocationWithPropertyAsync(
            Document document, InvocationExpressionSyntax invocation,
            string newTypeName, string newPropertyName, CancellationToken ct)
        {
            var editor = await DocumentEditor.CreateAsync(document, ct).ConfigureAwait(false);

            var newExpression = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName(newTypeName),
                SyntaxFactory.IdentifierName(newPropertyName));

            editor.ReplaceNode(invocation, newExpression);

            return editor.GetChangedDocument();
        }

        /// <summary>
        /// Extracts the simple type name from an expression (handles both simple and qualified names).
        /// </summary>
        private static string? GetSimpleTypeName(ExpressionSyntax expression)
        {
            return expression switch
            {
                IdentifierNameSyntax id => id.Identifier.Text,
                MemberAccessExpressionSyntax ma => ma.Name.Identifier.Text,
                _ => null,
            };
        }

        /// <summary>
        /// Finds the containing invocation or object creation from a diagnostic node.
        /// </summary>
        private static SyntaxNode? FindContainingCall(SyntaxNode node)
        {
            return node.AncestorsAndSelf().FirstOrDefault(n =>
                n is InvocationExpressionSyntax ||
                n is ObjectCreationExpressionSyntax ||
                n is ImplicitObjectCreationExpressionSyntax);
        }
    }
}
