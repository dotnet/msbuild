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
using Microsoft.CodeAnalysis.Formatting;

namespace Microsoft.Build.TaskAuthoring.Analyzer
{
    /// <summary>
    /// Code fixer for MSBuildTask0012: adds a <c>TaskEnvironment</c> entry to the object initializer of a
    /// task constructed inside another task, so the constructed task receives the constructing task's environment.
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TaskEnvironmentPropagationCodeFixProvider))]
    [Shared]
    public sealed class TaskEnvironmentPropagationCodeFixProvider : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds =>
            ImmutableArray.Create(DiagnosticIds.PropagateTaskEnvironmentToConstructedTask);

        public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            if (root is null || semanticModel is null)
            {
                return;
            }

            var taskEnvironmentType = semanticModel.Compilation.GetTypeByMetadataName(WellKnownTypeNames.TaskEnvironmentFullName);
            if (taskEnvironmentType is null)
            {
                return;
            }

            foreach (var diagnostic in context.Diagnostics)
            {
                var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
                if (node.AncestorsAndSelf().OfType<BaseObjectCreationExpressionSyntax>().FirstOrDefault() is not BaseObjectCreationExpressionSyntax creation)
                {
                    continue;
                }

                // A collection initializer cannot carry a member assignment.
                if (creation.Initializer is not null && !creation.Initializer.IsKind(SyntaxKind.ObjectInitializerExpression))
                {
                    continue;
                }

                if (semanticModel.GetTypeInfo(creation, context.CancellationToken).Type is not INamedTypeSymbol createdType ||
                    !TaskEnvironmentPropagationAnalyzer.TryGetTaskEnvironmentProperty(createdType, taskEnvironmentType, out IPropertySymbol? taskEnvironmentProperty) ||
                    taskEnvironmentProperty is null)
                {
                    continue;
                }

                if (FindTaskEnvironmentSource(semanticModel, creation, taskEnvironmentType, context.CancellationToken) is not string sourceName)
                {
                    continue;
                }

                string targetName = taskEnvironmentProperty.Name;
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: $"Assign {targetName} from the constructing task",
                        createChangedDocument: ct => AddTaskEnvironmentInitializerAsync(context.Document, creation, targetName, sourceName, ct),
                        equivalenceKey: "PropagateTaskEnvironment"),
                    diagnostic);
            }
        }

        /// <summary>
        /// Finds a readable <c>TaskEnvironment</c> member of the constructing type that is accessible and usable
        /// at the creation site, and returns the name to reference it by.
        /// </summary>
        private static string? FindTaskEnvironmentSource(
            SemanticModel semanticModel,
            SyntaxNode creation,
            INamedTypeSymbol taskEnvironmentType,
            CancellationToken cancellationToken)
        {
            int position = creation.SpanStart;
            ISymbol? enclosingSymbol = semanticModel.GetEnclosingSymbol(position, cancellationToken);
            if (enclosingSymbol is null || IsInStaticContext(enclosingSymbol))
            {
                return null;
            }

            INamedTypeSymbol? containingType = enclosingSymbol as INamedTypeSymbol ?? enclosingSymbol.ContainingType;
            if (containingType is null)
            {
                return null;
            }

            foreach (IPropertySymbol property in SharedAnalyzerHelpers.GetPropertiesIncludingBaseTypes(containingType))
            {
                if (!property.IsStatic &&
                    property.GetMethod is not null &&
                    TaskEnvironmentPropagationAnalyzer.IsTaskEnvironmentType(property.Type, taskEnvironmentType) &&
                    semanticModel.IsAccessible(position, property))
                {
                    return property.Name;
                }
            }

            for (INamedTypeSymbol? current = containingType;
                 current is not null && current.SpecialType != SpecialType.System_Object;
                 current = current.BaseType)
            {
                foreach (ISymbol member in current.GetMembers())
                {
                    if (member is IFieldSymbol { IsStatic: false, IsImplicitlyDeclared: false } field &&
                        TaskEnvironmentPropagationAnalyzer.IsTaskEnvironmentType(field.Type, taskEnvironmentType) &&
                        semanticModel.IsAccessible(position, field))
                    {
                        return field.Name;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Determines whether instance members are unavailable at the creation site, walking out of lambdas and
        /// local functions to the member that encloses them.
        /// </summary>
        private static bool IsInStaticContext(ISymbol enclosingSymbol)
        {
            for (ISymbol? symbol = enclosingSymbol; symbol is not null and not INamedTypeSymbol; symbol = symbol.ContainingSymbol)
            {
                bool isStatic = symbol switch
                {
                    IMethodSymbol method => method.IsStatic,
                    IFieldSymbol field => field.IsStatic,
                    IPropertySymbol property => property.IsStatic,
                    IEventSymbol @event => @event.IsStatic,
                    _ => false,
                };

                if (isStatic)
                {
                    return true;
                }
            }

            return false;
        }

        private static async Task<Document> AddTaskEnvironmentInitializerAsync(
            Document document,
            BaseObjectCreationExpressionSyntax creation,
            string targetName,
            string sourceName,
            CancellationToken cancellationToken)
        {
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

            var assignment = SyntaxFactory.AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                SyntaxFactory.IdentifierName(targetName),
                SyntaxFactory.IdentifierName(sourceName));

            InitializerExpressionSyntax initializer = creation.Initializer is InitializerExpressionSyntax existingInitializer
                ? existingInitializer.WithExpressions(existingInitializer.Expressions.Add(assignment))
                : SyntaxFactory.InitializerExpression(
                    SyntaxKind.ObjectInitializerExpression,
                    SyntaxFactory.SingletonSeparatedList<ExpressionSyntax>(assignment));

            SyntaxNode newCreation = creation
                .WithInitializer(initializer)
                .WithAdditionalAnnotations(Formatter.Annotation);

            editor.ReplaceNode(creation, newCreation);

            return editor.GetChangedDocument();
        }
    }
}
