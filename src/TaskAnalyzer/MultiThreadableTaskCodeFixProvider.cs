// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Operations;

using static Microsoft.Build.TaskAuthoring.Analyzer.SharedAnalyzerHelpers;

namespace Microsoft.Build.TaskAuthoring.Analyzer
{
    /// <summary>
    /// Code fixer for the thread-safe task analyzer.
    /// Fixes:
    /// - MSBuildTask0002: Replaces banned APIs with TaskEnvironment equivalents
    /// - MSBuildTask0003: Wraps path arguments with TaskEnvironment.GetAbsolutePath()
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MultiThreadableTaskCodeFixProvider))]
    [Shared]
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

            var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            if (semanticModel is null)
            {
                return;
            }

            foreach (var diagnostic in context.Diagnostics)
            {
                // The analyzer reports on the operation's own syntax node, so anchor on exactly that node.
                // getInnermostNodeForTie is required because a call that is itself an argument of another
                // call shares its span with the enclosing ArgumentSyntax; without it the fix would walk up
                // to — and rewrite — the enclosing call instead of the flagged one.
                var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);

                if (diagnostic.Id == DiagnosticIds.FilePathRequiresAbsolute)
                {
                    RegisterFilePathFix(context, semanticModel, node, diagnostic);
                }
                else if (diagnostic.Id == DiagnosticIds.TaskEnvironmentRequired)
                {
                    RegisterTaskEnvironmentFix(context, semanticModel, node, diagnostic);
                }
            }
        }

        private static void RegisterFilePathFix(CodeFixContext context, SemanticModel semanticModel, SyntaxNode node, Diagnostic diagnostic)
        {
            ArgumentListSyntax? argumentList = node switch
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

            // The wrap references the instance TaskEnvironment member; withhold the fix rather than emit a
            // reference that cannot bind here.
            if (!CanReferenceTaskEnvironment(semanticModel, node))
            {
                return;
            }

            var targetArg = FindPathArgument(semanticModel, node, argumentList);
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
        /// Finds the argument of the flagged call that the analyzer considered an unrooted path: the first
        /// argument bound to a <see cref="string"/> parameter whose name reads as a path and whose value is
        /// not already rooted. Falls back to the first syntactically unwrapped argument when no semantic
        /// information is available.
        /// </summary>
        private static ArgumentSyntax? FindPathArgument(SemanticModel semanticModel, SyntaxNode call, ArgumentListSyntax argumentList)
        {
            ImmutableArray<IArgumentOperation> arguments = semanticModel.GetOperation(call) switch
            {
                IInvocationOperation invocation => invocation.Arguments,
                IObjectCreationOperation creation => creation.Arguments,
                _ => default,
            };

            if (!arguments.IsDefaultOrEmpty)
            {
                var compilation = semanticModel.Compilation;
                var taskEnvironmentType = compilation.GetTypeByMetadataName(WellKnownTypeNames.TaskEnvironmentFullName);
                var absolutePathType = compilation.GetTypeByMetadataName(WellKnownTypeNames.AbsolutePathFullName);
                var iTaskItemType = compilation.GetTypeByMetadataName(WellKnownTypeNames.ITaskItemFullName);

                foreach (var argument in arguments)
                {
                    var parameter = argument.Parameter;
                    if (parameter is null ||
                        parameter.Type.SpecialType != SpecialType.System_String ||
                        !IsPathParameterName(parameter.Name))
                    {
                        continue;
                    }

                    // Skip arguments that aren't written in this call's argument list (e.g. defaulted
                    // optional parameters, whose syntax is the call itself).
                    if (argument.Syntax is ArgumentSyntax argumentSyntax &&
                        argumentList.Arguments.Contains(argumentSyntax) &&
                        !IsWrappedSafely(argument.Value, taskEnvironmentType, absolutePathType, iTaskItemType))
                    {
                        return argumentSyntax;
                    }
                }

                return null;
            }

            foreach (var argument in argumentList.Arguments)
            {
                if (!IsAlreadyWrapped(argument.Expression))
                {
                    return argument;
                }
            }

            return null;
        }

        /// <summary>
        /// Determines whether a generated reference to the instance <c>TaskEnvironment</c> member would compile
        /// at <paramref name="node"/>: the enclosing type must actually expose such a member, and <c>this</c>
        /// must be reachable from there.
        /// </summary>
        private static bool CanReferenceTaskEnvironment(SemanticModel semanticModel, SyntaxNode node)
        {
            var enclosingSymbol = semanticModel.GetEnclosingSymbol(node.SpanStart);

            return !IsThisUnavailable(enclosingSymbol, node) &&
                HasTaskEnvironmentMember(enclosingSymbol?.ContainingType);
        }

        /// <summary>
        /// Determines whether <paramref name="type"/> or one of its base types declares a <c>TaskEnvironment</c>
        /// property or field. Tasks are only required to implement <c>ITask</c>, and the default analyzer scope
        /// covers all of them, so the member the fix would reference need not exist.
        /// </summary>
        private static bool HasTaskEnvironmentMember(INamedTypeSymbol? type)
        {
            for (INamedTypeSymbol? current = type; current is not null; current = current.BaseType)
            {
                foreach (var member in current.GetMembers("TaskEnvironment"))
                {
                    if (member is IPropertySymbol or IFieldSymbol)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether <paramref name="node"/> sits in a context where <c>this</c> is unavailable — a
        /// static member, a static local function, a static lambda, or an instance field or property
        /// initializer.
        /// </summary>
        private static bool IsThisUnavailable(ISymbol? enclosingSymbol, SyntaxNode node)
        {
            for (ISymbol? symbol = enclosingSymbol; symbol is not null; symbol = symbol.ContainingSymbol)
            {
                if (symbol.IsStatic)
                {
                    return true;
                }

                // A non-static lambda or local function inherits the staticness of what encloses it.
                if (symbol is IMethodSymbol { MethodKind: MethodKind.AnonymousFunction or MethodKind.LocalFunction })
                {
                    continue;
                }

                break;
            }

            // Instance field and property initializers run before `this` is usable (CS0236), including from
            // inside a lambda declared there.
            for (SyntaxNode? current = node; current is not null; current = current.Parent)
            {
                if (current is EqualsValueClauseSyntax &&
                    current.Parent is PropertyDeclarationSyntax or VariableDeclaratorSyntax { Parent.Parent: BaseFieldDeclarationSyntax })
                {
                    return true;
                }

                if (current is MemberDeclarationSyntax)
                {
                    break;
                }
            }

            return false;
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

        private static void RegisterTaskEnvironmentFix(CodeFixContext context, SemanticModel semanticModel, SyntaxNode node, Diagnostic diagnostic)
        {
            // The replacements below all reference the instance TaskEnvironment member; withhold the fix
            // rather than emit a reference that cannot bind here.
            if (!CanReferenceTaskEnvironment(semanticModel, node))
            {
                return;
            }

            // Anchor on the reported node itself: walking ancestors would rewrite an enclosing call when the
            // flagged one is nested as an argument.
            var invocation = node as InvocationExpressionSyntax;
            var memberAccess = node as MemberAccessExpressionSyntax;

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
    }
}
