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
using Microsoft.CodeAnalysis.Simplification;

namespace Microsoft.Build.TaskAuthoring.Analyzer
{
    /// <summary>
    /// Code fix for MSBuildTask0012: declares multithreading support on a concrete task type by applying
    /// <c>[MSBuildMultiThreadableTask]</c>, implementing <c>IMultiThreadableTask</c>, and adding the
    /// <c>TaskEnvironment</c> property the engine injects. The remaining rules then report whatever is actually
    /// unsafe in the task body.
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RequireMultiThreadableTaskCodeFixProvider))]
    [Shared]
    public sealed class RequireMultiThreadableTaskCodeFixProvider : CodeFixProvider
    {
        private const string EquivalenceKey = "DeclareMultiThreadingSupport";
        private const string TaskEnvironmentPropertyName = "TaskEnvironment";
        private const string FallbackPropertyName = "Fallback";

        public override ImmutableArray<string> FixableDiagnosticIds =>
            ImmutableArray.Create(DiagnosticIds.RequireMultiThreadableTask);

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
                var classDeclaration = root.FindNode(diagnostic.Location.SourceSpan)
                    .FirstAncestorOrSelf<ClassDeclarationSyntax>();
                if (classDeclaration is null)
                {
                    continue;
                }

                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: "Declare multithreading support",
                        createChangedDocument: ct => DeclareMultiThreadingSupportAsync(context.Document, classDeclaration, ct),
                        equivalenceKey: EquivalenceKey),
                    diagnostic);
            }
        }

        private static async Task<Document> DeclareMultiThreadingSupportAsync(
            Document document, ClassDeclarationSyntax classDeclaration, CancellationToken cancellationToken)
        {
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            var compilation = editor.SemanticModel.Compilation;
            var taskType = editor.SemanticModel.GetDeclaredSymbol(classDeclaration, cancellationToken);

            var multiThreadableTaskType = compilation.GetTypeByMetadataName(WellKnownTypeNames.IMultiThreadableTaskFullName);
            var taskEnvironmentType = compilation.GetTypeByMetadataName(WellKnownTypeNames.TaskEnvironmentFullName);

            // A task can also opt in with the attribute alone, which is the only option when the compilation
            // targets an MSBuild version without IMultiThreadableTask. Add the interface only when the task does
            // not already have it — the attribute is what the engine's routing looks at, and it is not inherited.
            if (taskType is not null &&
                multiThreadableTaskType is not null &&
                taskEnvironmentType is not null &&
                !SharedAnalyzerHelpers.ImplementsInterface(taskType, multiThreadableTaskType))
            {
                if (!SharedAnalyzerHelpers.GetPropertiesIncludingBaseTypes(taskType)
                        .Any(property => property.Name == TaskEnvironmentPropertyName))
                {
                    editor.InsertMembers(classDeclaration, 0, [CreateTaskEnvironmentProperty(editor.Generator, taskEnvironmentType)]);
                }

                editor.AddInterfaceType(
                    classDeclaration,
                    editor.Generator.TypeExpression(multiThreadableTaskType).WithAdditionalAnnotations(Simplifier.Annotation));
            }

            editor.AddAttribute(classDeclaration, CreateMultiThreadableTaskAttribute(editor.Generator));

            return editor.GetChangedDocument();
        }

        /// <summary>
        /// Builds <c>public TaskEnvironment TaskEnvironment { get; set; } = TaskEnvironment.Fallback;</c>. The
        /// initializer keeps the property usable when the task is instantiated outside the engine, and avoids
        /// introducing a nullable warning; it is omitted when the referenced framework has no <c>Fallback</c>.
        /// </summary>
        private static SyntaxNode CreateTaskEnvironmentProperty(SyntaxGenerator generator, INamedTypeSymbol taskEnvironmentType)
        {
            var typeExpression = generator.TypeExpression(taskEnvironmentType).WithAdditionalAnnotations(Simplifier.Annotation);
            var property = generator.PropertyDeclaration(
                TaskEnvironmentPropertyName,
                typeExpression,
                Accessibility.Public);

            if (property is not PropertyDeclarationSyntax propertyDeclaration)
            {
                return property;
            }

            ExpressionSyntax initializerValue = taskEnvironmentType
                    .GetMembers(FallbackPropertyName)
                    .Any(member => member is IPropertySymbol { IsStatic: true, DeclaredAccessibility: Accessibility.Public })
                ? SyntaxFactory.ParseExpression($"global::{WellKnownTypeNames.TaskEnvironmentFullName}.{FallbackPropertyName}")
                    .WithAdditionalAnnotations(Simplifier.Annotation)
                : SyntaxFactory.PostfixUnaryExpression(
                    SyntaxKind.SuppressNullableWarningExpression,
                    SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression));

            return propertyDeclaration
                .WithInitializer(SyntaxFactory.EqualsValueClause(initializerValue))
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
        }

        private static SyntaxNode CreateMultiThreadableTaskAttribute(SyntaxGenerator generator) =>
            generator.Attribute(
                SyntaxFactory.ParseName("global::" + WellKnownTypeNames.MultiThreadableTaskAttributeFullName)
                    .WithAdditionalAnnotations(Simplifier.Annotation));
    }
}
