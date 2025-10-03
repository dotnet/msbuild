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

namespace Microsoft.Build.Utilities.Analyzer
{
    /// <summary>
    /// Code fixer for IMultiThreadableTask banned API analyzer.
    /// Provides fixes for:
    /// - MSB9997: File path APIs - wraps with TaskEnvironment.GetAbsolutePath()
    /// - MSB9998: Simple API migrations - replaces with TaskEnvironment equivalents
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CSharpIMultiThreadableTaskCodeFixProvider)), Shared]
    public class CSharpIMultiThreadableTaskCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => 
            ImmutableArray.Create("MSB9997", "MSB9998");

        public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            if (root == null)
            {
                return;
            }

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;
            var node = root.FindNode(diagnosticSpan);

            // Handle based on diagnostic ID
            if (diagnostic.Id == "MSB9997")
            {
                // MSB9997: File path wrapping
                if (IsPathStringArgument(node, out var argumentSyntax))
                {
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            title: "Wrap with TaskEnvironment.GetAbsolutePath()",
                            createChangedDocument: c => WrapWithGetAbsolutePathAsync(context.Document, argumentSyntax!, c),
                            equivalenceKey: "WrapWithGetAbsolutePath"),
                        diagnostic);
                }
            }
            else if (diagnostic.Id == "MSB9998")
            {
                // MSB9998: TaskEnvironment API migrations
                RegisterMSB9998Fixes(context, node, diagnostic);
            }
        }

        private void RegisterMSB9998Fixes(CodeFixContext context, SyntaxNode node, Diagnostic diagnostic)
        {
            // Handle Environment.CurrentDirectory - diagnostic is on the member access itself
            if (node is MemberAccessExpressionSyntax memberAccess)
            {
                var memberName = (memberAccess.Name as IdentifierNameSyntax)?.Identifier.Text;
                var targetType = (memberAccess.Expression as IdentifierNameSyntax)?.Identifier.Text;

                if (targetType == "Environment" && memberName == "CurrentDirectory")
                {
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            title: "Use TaskEnvironment.ProjectCurrentDirectory",
                            createChangedDocument: c => ReplaceEnvironmentCurrentDirectoryAsync(context.Document, memberAccess, c),
                            equivalenceKey: "UseProjectCurrentDirectory"),
                        diagnostic);
                    return;
                }
            }

            // Also check if node is identifier within member access (when cursor is on identifier)
            if (node is IdentifierNameSyntax identifier)
            {
                if (identifier.Identifier.Text == "CurrentDirectory")
                {
                    var parentMemberAccess = identifier.Parent as MemberAccessExpressionSyntax;
                    if (parentMemberAccess?.Expression is IdentifierNameSyntax envIdentifier && 
                        envIdentifier.Identifier.Text == "Environment")
                    {
                        context.RegisterCodeFix(
                            CodeAction.Create(
                                title: "Use TaskEnvironment.ProjectCurrentDirectory",
                                createChangedDocument: c => ReplaceEnvironmentCurrentDirectoryAsync(context.Document, parentMemberAccess, c),
                                equivalenceKey: "UseProjectCurrentDirectory"),
                            diagnostic);
                        return;
                    }
                }
            }

            // Handle Environment.GetEnvironmentVariable and SetEnvironmentVariable
            if (node is InvocationExpressionSyntax invocation)
            {
                if (invocation.Expression is MemberAccessExpressionSyntax invocationMemberAccess)
                {
                    var methodName = (invocationMemberAccess.Name as IdentifierNameSyntax)?.Identifier.Text;
                    var targetType = (invocationMemberAccess.Expression as IdentifierNameSyntax)?.Identifier.Text;

                    if (targetType == "Environment")
                    {
                        if (methodName == "GetEnvironmentVariable")
                        {
                            context.RegisterCodeFix(
                                CodeAction.Create(
                                    title: "Use TaskEnvironment.GetEnvironmentVariable()",
                                    createChangedDocument: c => ReplaceEnvironmentMethodAsync(context.Document, invocation, "GetEnvironmentVariable", c),
                                    equivalenceKey: "UseGetEnvironmentVariable"),
                                diagnostic);
                            return;
                        }
                        else if (methodName == "SetEnvironmentVariable")
                        {
                            // Only offer fix for 2-parameter version
                            if (invocation.ArgumentList.Arguments.Count == 2)
                            {
                                context.RegisterCodeFix(
                                    CodeAction.Create(
                                        title: "Use TaskEnvironment.SetEnvironmentVariable()",
                                        createChangedDocument: c => ReplaceEnvironmentMethodAsync(context.Document, invocation, "SetEnvironmentVariable", c),
                                        equivalenceKey: "UseSetEnvironmentVariable"),
                                    diagnostic);
                            }
                            return;
                        }
                    }
                    else if (targetType == "Path" && methodName == "GetFullPath")
                    {
                        context.RegisterCodeFix(
                            CodeAction.Create(
                                title: "Use TaskEnvironment.GetAbsolutePath()",
                                createChangedDocument: c => ReplacePathGetFullPathAsync(context.Document, invocation, c),
                                equivalenceKey: "UseGetAbsolutePath"),
                            diagnostic);
                        return;
                    }
                }
            }
        }

        private bool IsPathStringArgument(SyntaxNode node, out ArgumentSyntax? argumentSyntax)
        {
            argumentSyntax = null;

            // Try to find the argument containing the path string
            var invocation = node as InvocationExpressionSyntax ?? node.Parent as InvocationExpressionSyntax;
            var objectCreation = node as ObjectCreationExpressionSyntax ?? node.Parent as ObjectCreationExpressionSyntax;

            ArgumentListSyntax? argumentList = null;

            if (invocation != null)
            {
                argumentList = invocation.ArgumentList;
            }
            else if (objectCreation != null)
            {
                argumentList = objectCreation.ArgumentList;
            }

            if (argumentList == null || argumentList.Arguments.Count == 0)
            {
                return false;
            }

            // Get the first argument (typically the path parameter)
            var firstArg = argumentList.Arguments[0];

            // Check if it's a string-type expression
            argumentSyntax = firstArg;
            return true;
        }

        private async Task<Document> WrapWithGetAbsolutePathAsync(
            Document document,
            ArgumentSyntax argumentSyntax,
            CancellationToken cancellationToken)
        {
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            var generator = editor.Generator;

            // Get the expression from the argument
            var pathExpression = argumentSyntax.Expression;

            // Create: TaskEnvironment.GetAbsolutePath(pathExpression)
            var taskEnvironmentType = generator.IdentifierName("TaskEnvironment");
            var getAbsolutePathMember = generator.MemberAccessExpression(
                taskEnvironmentType,
                "GetAbsolutePath");

            var wrappedExpression = generator.InvocationExpression(
                getAbsolutePathMember,
                pathExpression);

            // Replace the argument expression with the wrapped version
            var newArgument = argumentSyntax.WithExpression((ExpressionSyntax)wrappedExpression);
            editor.ReplaceNode(argumentSyntax, newArgument);

            return editor.GetChangedDocument();
        }

        private async Task<Document> ReplaceEnvironmentCurrentDirectoryAsync(
            Document document,
            MemberAccessExpressionSyntax memberAccess,
            CancellationToken cancellationToken)
        {
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

            // Replace Environment.CurrentDirectory with TaskEnvironment.ProjectCurrentDirectory
            var newExpression = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName("TaskEnvironment"),
                SyntaxFactory.IdentifierName("ProjectCurrentDirectory"));

            editor.ReplaceNode(memberAccess, newExpression);
            return editor.GetChangedDocument();
        }

        private async Task<Document> ReplaceEnvironmentMethodAsync(
            Document document,
            InvocationExpressionSyntax invocation,
            string methodName,
            CancellationToken cancellationToken)
        {
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

            // Replace Environment.MethodName(...) with TaskEnvironment.MethodName(...)
            var newMemberAccess = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName("TaskEnvironment"),
                SyntaxFactory.IdentifierName(methodName));

            var newInvocation = invocation.WithExpression(newMemberAccess);
            editor.ReplaceNode(invocation, newInvocation);
            return editor.GetChangedDocument();
        }

        private async Task<Document> ReplacePathGetFullPathAsync(
            Document document,
            InvocationExpressionSyntax invocation,
            CancellationToken cancellationToken)
        {
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

            // Replace Path.GetFullPath(...) with TaskEnvironment.GetAbsolutePath(...)
            var newMemberAccess = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName("TaskEnvironment"),
                SyntaxFactory.IdentifierName("GetAbsolutePath"));

            var newInvocation = invocation.WithExpression(newMemberAccess);
            editor.ReplaceNode(invocation, newInvocation);
            return editor.GetChangedDocument();
        }
    }
}
