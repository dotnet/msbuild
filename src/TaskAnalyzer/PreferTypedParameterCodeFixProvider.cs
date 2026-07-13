// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
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
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Simplification;

namespace Microsoft.Build.TaskAuthoring.Analyzer
{
    /// <summary>
    /// Code fixer for the strongly-typed task parameter analyzer.
    /// Fixes:
    /// - MSBuildTask0006: Retypes a string task property to AbsolutePath/FileInfo/DirectoryInfo and
    ///   removes the now-redundant conversion at every use site.
    /// - MSBuildTask0007: Retypes an ITaskItem/ITaskItem[] task property to ITaskItem&lt;T&gt;/ITaskItem&lt;T&gt;[]
    ///   and replaces the manual ItemSpec parsing with the strongly-typed Value at every use site.
    /// - MSBuildTask0008: Retypes a path property whose default is a relative string and moves that default into
    ///   a guarded, TaskEnvironment-rooted assignment at the top of Execute() (since a relative default cannot be
    ///   rooted in a property initializer, which runs before the engine sets TaskEnvironment).
    /// </summary>
    /// <remarks>
    /// The fix is intentionally conservative: changing a property's type affects every reference to it, so a
    /// fix is only offered when EVERY reference to the property is a conversion the analyzer knows how to
    /// rewrite. Otherwise a leftover string-typed use (for example <c>File.Exists(InputPath)</c>) would no
    /// longer compile once the property is retyped. This guarantees the produced code compiles.
    /// </remarks>
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PreferTypedParameterCodeFixProvider))]
    [Shared]
    public sealed class PreferTypedParameterCodeFixProvider : CodeFixProvider
    {
        private const string EquivalenceKey = "PreferTypedParameter";

        public override ImmutableArray<string> FixableDiagnosticIds =>
            ImmutableArray.Create(
                DiagnosticIds.PreferTypedPathParameter,
                DiagnosticIds.PreferTypedTaskItem,
                DiagnosticIds.InitializeRelativeDefaultInExecute);

        public override FixAllProvider GetFixAllProvider() => new PreferTypedParameterFixAllProvider();

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            if (root is null || semanticModel is null)
            {
                return;
            }

            foreach (var diagnostic in context.Diagnostics)
            {
                var plan = TryBuildPlan(semanticModel, root, diagnostic, context.CancellationToken);
                if (plan is null)
                {
                    continue;
                }

                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: plan.InitializeInExecute
                            ? $"Change '{plan.Property.Name}' to '{plan.NewTypeDisplay}' and initialize it in Execute()"
                            : $"Change '{plan.Property.Name}' to '{plan.NewTypeDisplay}'",
                        createChangedDocument: ct => ApplyPlansAsync(context.Document, new[] { diagnostic }, ct),
                        equivalenceKey: EquivalenceKey),
                    diagnostic);
            }
        }

        /// <summary>
        /// Applies fixes for the supplied diagnostics, grouping them by target property so that a property whose
        /// type changes is retyped exactly once even when it has multiple conversion sites.
        /// </summary>
        internal static async Task<Document> ApplyPlansAsync(Document document, IReadOnlyList<Diagnostic> diagnostics, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            if (root is null || semanticModel is null)
            {
                return document;
            }

            var plans = new List<PropertyFixPlan>();
            var seenProperties = new List<IPropertySymbol>();
            foreach (var diagnostic in diagnostics)
            {
                var plan = TryBuildPlan(semanticModel, root, diagnostic, cancellationToken);
                if (plan is null)
                {
                    continue;
                }

                // Each plan already rewrites every site for its property; dedupe so we don't process a property twice.
                if (seenProperties.Any(p => SymbolEqualityComparer.Default.Equals(p, plan.Property)))
                {
                    continue;
                }

                seenProperties.Add(plan.Property);
                plans.Add(plan);
            }

            if (plans.Count == 0)
            {
                return document;
            }

            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            foreach (var plan in plans)
            {
                editor.ReplaceNode(plan.PropertyDeclaration, BuildRetypedProperty(plan));
                foreach (var (oldNode, newNode) in plan.SiteEdits)
                {
                    editor.ReplaceNode(oldNode, newNode);
                }

                if (plan.ExecutePrologue is not null)
                {
                    if (plan.ExecuteAnchor is not null)
                    {
                        // Insert as the first statement of Execute() without replacing the whole method, so this
                        // does not conflict with the site edits that rewrite nodes inside the method body.
                        editor.InsertBefore(plan.ExecuteAnchor, plan.ExecutePrologue);
                    }
                    else if (plan.ExecuteBody is not null)
                    {
                        // Execute() has an empty body: no descendant edits can exist there, so replacing the
                        // block outright is safe.
                        editor.ReplaceNode(
                            plan.ExecuteBody,
                            plan.ExecuteBody.WithStatements(SyntaxFactory.SingletonList(plan.ExecutePrologue)));
                    }
                }
            }

            return editor.GetChangedDocument();
        }

        /// <summary>
        /// Validates that the diagnostic's target property can be safely retyped (every reference is a
        /// rewritable conversion) and, if so, produces the set of edits to apply.
        /// </summary>
        private static PropertyFixPlan? TryBuildPlan(SemanticModel semanticModel, SyntaxNode root, Diagnostic diagnostic, CancellationToken cancellationToken)
        {
            if (!diagnostic.Properties.TryGetValue("PropertyName", out var propertyName) || propertyName is null ||
                !diagnostic.Properties.TryGetValue("SuggestedType", out var suggestedType) || suggestedType is null)
            {
                return null;
            }

            var conversionNode = root.FindNode(diagnostic.Location.SourceSpan);
            var typeDeclaration = conversionNode.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().FirstOrDefault();
            if (typeDeclaration is null)
            {
                return null;
            }

            if (semanticModel.GetDeclaredSymbol(typeDeclaration, cancellationToken) is not INamedTypeSymbol taskType)
            {
                return null;
            }

            var property = taskType.GetMembers(propertyName).OfType<IPropertySymbol>().FirstOrDefault();
            if (property is null)
            {
                return null;
            }

            // The property must be declared exactly once, in this syntax tree, as a property declaration we can edit.
            if (property.DeclaringSyntaxReferences.Length != 1 ||
                property.DeclaringSyntaxReferences[0].GetSyntax(cancellationToken) is not PropertyDeclarationSyntax propertyDeclaration ||
                propertyDeclaration.SyntaxTree != root.SyntaxTree)
            {
                return null;
            }

            bool isItemRule = diagnostic.Id == DiagnosticIds.PreferTypedTaskItem;
            bool isMoveDefaultRule = diagnostic.Id == DiagnosticIds.InitializeRelativeDefaultInExecute;
            bool isArray = propertyDeclaration.Type is ArrayTypeSyntax;

            // The strong type the rewritten conversions must produce (used to guard against losing semantics).
            ITypeSymbol? expectedResultType = ResolvePathTypeSymbol(semanticModel.Compilation, suggestedType);

            string newTypeName = BuildNewTypeName(suggestedType, isItemRule, isArray);
            string newTypeDisplay = BuildDisplayTypeName(suggestedType, isItemRule, isArray);
            bool newTypeIsValueType = !isItemRule && suggestedType == "AbsolutePath";

            ExpressionSyntax? initializerValue;
            if (isMoveDefaultRule)
            {
                // The relative default cannot live in the initializer once the property is retyped; it is moved
                // into Execute(). Leave the property with an "unset" default so an unset parameter is detectable.
                initializerValue = UnsetDefaultValue(newTypeIsValueType);
            }
            else if (!TryBuildInitializerValue(semanticModel, propertyDeclaration, suggestedType, newTypeIsValueType, isItemRule, cancellationToken, out initializerValue))
            {
                // Decide how to carry over an existing property initializer to the retyped property. A string
                // initializer is not assignable to the new type as-is, so it must be replaced or re-expressed;
                // if it can't be done safely the whole fix is skipped rather than dropping/corrupting the default.
                return null;
            }

            // Partial classes can spread references across multiple declarations (and files). Collect every
            // partial declaration of the task type so the "all references must be rewritable" guarantee holds
            // over the whole type, not just the part that happens to contain the flagged conversion.
            var typeDeclarations = taskType.DeclaringSyntaxReferences
                .Select(reference => reference.GetSyntax(cancellationToken))
                .OfType<TypeDeclarationSyntax>()
                .ToList();

            var siteEdits = new List<(SyntaxNode, SyntaxNode)>();
            if (!TryCollectSiteEdits(semanticModel, typeDeclarations, property, suggestedType, expectedResultType, isItemRule, isArray, siteEdits, cancellationToken))
            {
                return null;
            }

            if (siteEdits.Count == 0)
            {
                return null;
            }

            StatementSyntax? executePrologue = null;
            StatementSyntax? executeAnchor = null;
            BlockSyntax? executeBody = null;
            if (isMoveDefaultRule &&
                !TryBuildExecutePrologue(taskType, property, propertyDeclaration, suggestedType, newTypeIsValueType, root, cancellationToken, out executePrologue, out executeAnchor, out executeBody))
            {
                // No Execute() to root the default in (missing/expression-bodied), or no TaskEnvironment to root
                // it through: keep the diagnostic but offer no fix.
                return null;
            }

            return new PropertyFixPlan(property, propertyDeclaration, newTypeName, newTypeDisplay, initializerValue, siteEdits, isMoveDefaultRule, executePrologue, executeAnchor, executeBody);
        }

        private static ExpressionSyntax UnsetDefaultValue(bool newTypeIsValueType) => newTypeIsValueType
            ? SyntaxFactory.LiteralExpression(SyntaxKind.DefaultLiteralExpression, SyntaxFactory.Token(SyntaxKind.DefaultKeyword))
            : SyntaxFactory.PostfixUnaryExpression(
                SyntaxKind.SuppressNullableWarningExpression,
                SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression));

        /// <summary>
        /// Builds the guarded normalization statement to insert at the top of <c>Execute()</c> for a property
        /// whose relative default was moved out of its initializer (MSBuildTask0008). The statement only applies
        /// the default when the property is still unset, so a value bound from the project XML is not clobbered.
        /// Returns false (skip the fix) when there is no editable <c>Execute()</c> in this document or no
        /// accessible <c>TaskEnvironment</c> member to root the relative path through.
        /// </summary>
        private static bool TryBuildExecutePrologue(
            INamedTypeSymbol taskType,
            IPropertySymbol property,
            PropertyDeclarationSyntax propertyDeclaration,
            string suggestedType,
            bool newTypeIsValueType,
            SyntaxNode root,
            CancellationToken cancellationToken,
            out StatementSyntax? prologue,
            out StatementSyntax? anchor,
            out BlockSyntax? body)
        {
            prologue = null;
            anchor = null;
            body = null;

            // The relative literal we are relocating; only a plain string literal default reaches 0008.
            if (propertyDeclaration.Initializer?.Value is not LiteralExpressionSyntax relativeLiteral ||
                !relativeLiteral.IsKind(SyntaxKind.StringLiteralExpression))
            {
                return false;
            }

            if (!HasAccessibleTaskEnvironment(taskType))
            {
                return false;
            }

            var executeMethod = FindEditableExecuteMethod(taskType, root, cancellationToken);
            if (executeMethod?.Body is null)
            {
                return false;
            }

            body = executeMethod.Body;
            anchor = body.Statements.FirstOrDefault();

            // TaskEnvironment.GetAbsolutePath("relative")
            ExpressionSyntax rooted = SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName("TaskEnvironment"),
                        SyntaxFactory.IdentifierName("GetAbsolutePath")))
                .WithArgumentList(SyntaxFactory.ArgumentList(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.Argument(relativeLiteral.WithoutTrivia()))));

            // For FileInfo/DirectoryInfo the engine goes string -> AbsolutePath -> FileInfo/DirectoryInfo;
            // AbsolutePath implicitly converts to string, so new FileInfo(GetAbsolutePath(...)) mirrors that.
            ExpressionSyntax rhs = newTypeIsValueType
                ? rooted
                : SyntaxFactory.ObjectCreationExpression(
                        SyntaxFactory.ParseTypeName(FullyQualify(suggestedType)).WithAdditionalAnnotations(Simplifier.Annotation))
                    .WithArgumentList(SyntaxFactory.ArgumentList(
                        SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory.Argument(rooted))));

            var propertyAccess = SyntaxFactory.IdentifierName(property.Name);

            if (newTypeIsValueType)
            {
                // if (Prop == default) { Prop = TaskEnvironment.GetAbsolutePath("relative"); }
                prologue = SyntaxFactory.IfStatement(
                    SyntaxFactory.BinaryExpression(
                        SyntaxKind.EqualsExpression,
                        propertyAccess,
                        SyntaxFactory.LiteralExpression(SyntaxKind.DefaultLiteralExpression, SyntaxFactory.Token(SyntaxKind.DefaultKeyword))),
                    SyntaxFactory.Block(
                        SyntaxFactory.ExpressionStatement(
                            SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, propertyAccess, rhs))));
            }
            else
            {
                // Prop ??= new FileInfo(TaskEnvironment.GetAbsolutePath("relative"));
                prologue = SyntaxFactory.ExpressionStatement(
                    SyntaxFactory.AssignmentExpression(SyntaxKind.CoalesceAssignmentExpression, propertyAccess, rhs));
            }

            prologue = prologue.WithAdditionalAnnotations(Formatter.Annotation);
            return true;
        }

        /// <summary>
        /// True when the task type (or a base type) exposes a member named <c>TaskEnvironment</c> the generated
        /// prologue can read.
        /// </summary>
        private static bool HasAccessibleTaskEnvironment(INamedTypeSymbol taskType)
        {
            for (INamedTypeSymbol? type = taskType; type is not null; type = type.BaseType)
            {
                if (type.GetMembers("TaskEnvironment").Any(m => m is IPropertySymbol or IFieldSymbol))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Locates the parameterless <c>Execute()</c> method of the task declared in the same syntax tree as the
        /// document being fixed, or null when there is none we can edit here.
        /// </summary>
        private static MethodDeclarationSyntax? FindEditableExecuteMethod(INamedTypeSymbol taskType, SyntaxNode root, CancellationToken cancellationToken)
        {
            foreach (var method in taskType.GetMembers("Execute").OfType<IMethodSymbol>())
            {
                if (method.Parameters.Length != 0)
                {
                    continue;
                }

                foreach (var reference in method.DeclaringSyntaxReferences)
                {
                    if (reference.GetSyntax(cancellationToken) is MethodDeclarationSyntax declaration &&
                        declaration.SyntaxTree == root.SyntaxTree)
                    {
                        return declaration;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Determines the value expression to use for the retyped property's initializer when the original
        /// property has one. Returns false (skip the whole fix) when the existing default cannot be safely
        /// re-expressed as the new type.
        /// </summary>
        private static bool TryBuildInitializerValue(
            SemanticModel semanticModel,
            PropertyDeclarationSyntax propertyDeclaration,
            string suggestedType,
            bool newTypeIsValueType,
            bool isItemRule,
            CancellationToken cancellationToken,
            out ExpressionSyntax? initializerValue)
        {
            initializerValue = null;

            if (propertyDeclaration.Initializer is null)
            {
                // No initializer to carry over.
                return true;
            }

            ExpressionSyntax initExpr = propertyDeclaration.Initializer.Value;

            // Empty string / null / string.Empty: nothing meaningful to preserve — use a type-compatible default.
            if (IsEmptyOrNullDefault(semanticModel, initExpr, cancellationToken))
            {
                initializerValue = newTypeIsValueType
                    ? SyntaxFactory.LiteralExpression(SyntaxKind.DefaultLiteralExpression, SyntaxFactory.Token(SyntaxKind.DefaultKeyword))
                    : SyntaxFactory.PostfixUnaryExpression(
                        SyntaxKind.SuppressNullableWarningExpression,
                        SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression));
                return true;
            }

            // A non-empty compile-time string default on a path property (MSBuildTask0006) is preserved by
            // constructing the typed value in the initializer. MSBuild normally normalizes an incoming string
            // to a fully-qualified path via TaskEnvironment.GetAbsolutePath before binding it to a typed
            // parameter, but TaskEnvironment is only set by the engine *after* the task is constructed, so it
            // is not available inside a property initializer (which runs in the constructor). Therefore only a
            // default that is *already* fully qualified can be reproduced here without TaskEnvironment; a
            // relative default would need rooting we cannot perform at construction time, so the fix is skipped.
            if (!isItemRule &&
                semanticModel.GetConstantValue(initExpr, cancellationToken) is { HasValue: true, Value: string defaultString } &&
                PathDefaultClassifier.IsFullyQualifiedPath(defaultString))
            {
                // AbsolutePath itself validates that the literal is fully qualified. For FileInfo/DirectoryInfo
                // the engine goes string -> AbsolutePath -> FileInfo/DirectoryInfo, so mirror that chain by
                // constructing through AbsolutePath rather than passing the raw string.
                ExpressionSyntax absolutePathExpr = SyntaxFactory.ObjectCreationExpression(
                        SyntaxFactory.ParseTypeName(FullyQualify("AbsolutePath")).WithAdditionalAnnotations(Simplifier.Annotation))
                    .WithArgumentList(SyntaxFactory.ArgumentList(
                        SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory.Argument(initExpr.WithoutTrivia()))));

                initializerValue = suggestedType == "AbsolutePath"
                    ? absolutePathExpr
                    : SyntaxFactory.ObjectCreationExpression(
                            SyntaxFactory.ParseTypeName(FullyQualify(suggestedType)).WithAdditionalAnnotations(Simplifier.Annotation))
                        .WithArgumentList(SyntaxFactory.ArgumentList(
                            SyntaxFactory.SingletonSeparatedList(
                                SyntaxFactory.Argument(absolutePathExpr))));
                return true;
            }

            // Anything else (a non-constant default, or a relative/invalid path that would require rooting via
            // TaskEnvironment) can't be safely re-expressed as the new type. Skip the fix for this property
            // instead of silently changing behavior or emitting code that would throw at construction time.
            return false;
        }

        /// <summary>
        /// True when a property initializer represents an "empty" default that carries no meaningful value:
        /// the <c>null</c> literal, an empty string literal, or <c>string.Empty</c>.
        /// </summary>
        private static bool IsEmptyOrNullDefault(SemanticModel semanticModel, ExpressionSyntax expr, CancellationToken cancellationToken)
        {
            if (expr is LiteralExpressionSyntax literal)
            {
                if (literal.IsKind(SyntaxKind.NullLiteralExpression))
                {
                    return true;
                }

                if (literal.IsKind(SyntaxKind.StringLiteralExpression))
                {
                    return literal.Token.ValueText.Length == 0;
                }
            }

            var constant = semanticModel.GetConstantValue(expr, cancellationToken);
            if (constant.HasValue)
            {
                return constant.Value is null || (constant.Value is string s && s.Length == 0);
            }

            // string.Empty / String.Empty is a field (not a compile-time constant) but is semantically empty.
            return expr is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Name.Identifier.Text == "Empty" &&
                semanticModel.GetSymbolInfo(expr, cancellationToken).Symbol?.ContainingType?.SpecialType == SpecialType.System_String;
        }

        /// <summary>
        /// Visits every reference to <paramref name="property"/> across all partial declarations of the task
        /// type and verifies each one is a conversion that can be rewritten. Populates <paramref name="siteEdits"/>;
        /// returns false if any reference cannot be safely rewritten (in which case no fix should be offered).
        /// A reference living in a different syntax tree than the document being fixed also fails, because a
        /// single-document code fix cannot rewrite it and leaving it behind would not compile after retyping.
        /// </summary>
        private static bool TryCollectSiteEdits(
            SemanticModel semanticModel,
            IReadOnlyList<TypeDeclarationSyntax> typeDeclarations,
            IPropertySymbol property,
            string suggestedType,
            ITypeSymbol? expectedResultType,
            bool isItemRule,
            bool isArray,
            List<(SyntaxNode, SyntaxNode)> siteEdits,
            CancellationToken cancellationToken)
        {
            SyntaxTree documentTree = semanticModel.SyntaxTree;

            foreach (var typeDeclaration in typeDeclarations)
            {
                bool sameTree = typeDeclaration.SyntaxTree == documentTree;
                SemanticModel model = sameTree
                    ? semanticModel
                    : semanticModel.Compilation.GetSemanticModel(typeDeclaration.SyntaxTree);

                foreach (var identifier in typeDeclaration.DescendantNodes().OfType<IdentifierNameSyntax>())
                {
                    if (identifier.Identifier.Text != property.Name)
                    {
                        continue;
                    }

                    if (!SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(identifier, cancellationToken).Symbol, property))
                    {
                        continue;
                    }

                    // The property is referenced in another document; this fix can only edit the current one.
                    if (!sameTree)
                    {
                        return false;
                    }

                    ExpressionSyntax propertyAccess = GetPropertyAccessExpression(identifier);

                    if (isItemRule && isArray)
                    {
                        if (!TryRewriteForEachArray(semanticModel, propertyAccess, suggestedType, expectedResultType, siteEdits, cancellationToken))
                        {
                            return false;
                        }
                    }
                    else if (isItemRule)
                    {
                        if (!TryRewriteItemSpecConversion(semanticModel, propertyAccess, propertyAccess, suggestedType, expectedResultType, siteEdits, cancellationToken))
                        {
                            return false;
                        }
                    }
                    else
                    {
                        if (!TryRewritePathConversion(semanticModel, propertyAccess, suggestedType, expectedResultType, siteEdits, cancellationToken))
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// MSBuildTask0006: rewrites the site where a retyped path property is used. Handles three shapes:
        /// <list type="bullet">
        /// <item><c>new T(prop)</c> / <c>TaskEnvironment.GetAbsolutePath(prop)</c> collapses to <c>prop</c>.</item>
        /// <item><c>Path.GetFullPath(prop)</c> (the banned normalization) collapses to <c>prop</c> for AbsolutePath,
        /// or <c>prop.FullName</c> for FileInfo/DirectoryInfo, since the retyped value is already absolute.</item>
        /// <item>A raw string consumption (<c>File.Delete(prop)</c>, <c>new FileStream(prop, ...)</c>) needs no edit
        /// for AbsolutePath (it converts to string implicitly), or <c>prop.FullName</c> for FileInfo/DirectoryInfo.</item>
        /// </list>
        /// Returns false for any other reference shape so the conservative "all references rewritable" guarantee holds.
        /// </summary>
        private static bool TryRewritePathConversion(
            SemanticModel semanticModel,
            ExpressionSyntax propertyAccess,
            string suggestedType,
            ITypeSymbol? expectedResultType,
            List<(SyntaxNode, SyntaxNode)> siteEdits,
            CancellationToken cancellationToken)
        {
            // Conversion site: new T(prop) / TaskEnvironment.GetAbsolutePath(prop) => prop
            var conversion = GetSingleArgumentConversion(propertyAccess);
            if (conversion is not null &&
                IsExpectedConversion(semanticModel, conversion, suggestedType, expectedResultType, isItemRule: false, cancellationToken))
            {
                var replacement = propertyAccess
                    .WithoutTrivia()
                    .WithTriviaFrom(conversion)
                    .WithAdditionalAnnotations(Formatter.Annotation);

                siteEdits.Add((conversion, replacement));
                return true;
            }

            // Banned normalization site: Path.GetFullPath(prop). The retyped value is already absolute, so the
            // whole call collapses to the property (AbsolutePath, implicitly a string) or its absolute string.
            if (IsPathGetFullPathInvocation(semanticModel, propertyAccess, cancellationToken, out var getFullPath))
            {
                ExpressionSyntax replacement = (suggestedType == "AbsolutePath"
                        ? (ExpressionSyntax)propertyAccess.WithoutTrivia()
                        : AppendFullName(propertyAccess))
                    .WithTriviaFrom(getFullPath)
                    .WithAdditionalAnnotations(Formatter.Annotation);

                siteEdits.Add((getFullPath, replacement));
                return true;
            }

            // Raw string consumption: prop passed where a string is expected (File.Delete(prop), new FileStream(prop)).
            if (IsStringArgument(semanticModel, propertyAccess, cancellationToken))
            {
                // AbsolutePath converts to string implicitly, so the call still compiles unchanged; the retype
                // alone is the fix. FileInfo/DirectoryInfo have no implicit string conversion, so pass .FullName.
                if (suggestedType != "AbsolutePath")
                {
                    // A null-guard such as string.IsNullOrEmpty(prop) is specifically tolerant of a null property.
                    // Rewriting it to prop.FullName would dereference a possibly-null FileInfo/DirectoryInfo and
                    // throw, silently defeating the guard. We cannot safely rewrite this shape, so withhold the
                    // whole fix (the diagnostic still surfaces) rather than emit code that can NRE.
                    if (IsNullGuardArgument(semanticModel, propertyAccess, cancellationToken))
                    {
                        return false;
                    }

                    var replacement = AppendFullName(propertyAccess)
                        .WithTriviaFrom(propertyAccess)
                        .WithAdditionalAnnotations(Formatter.Annotation);

                    siteEdits.Add((propertyAccess, replacement));
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// True when <paramref name="propertyAccess"/> is the argument of a <c>string.IsNullOrEmpty</c> or
        /// <c>string.IsNullOrWhiteSpace</c> call. These are null-tolerant by design, so a FileInfo/DirectoryInfo
        /// retype cannot rewrite them to <c>prop.FullName</c> without risking a <see cref="System.NullReferenceException"/>.
        /// </summary>
        private static bool IsNullGuardArgument(
            SemanticModel semanticModel,
            ExpressionSyntax propertyAccess,
            CancellationToken cancellationToken)
        {
            if (propertyAccess.Parent is not ArgumentSyntax argument ||
                argument.Parent is not ArgumentListSyntax argumentList ||
                argumentList.Arguments.Count != 1 ||
                argumentList.Parent is not InvocationExpressionSyntax candidate)
            {
                return false;
            }

            return semanticModel.GetSymbolInfo(candidate, cancellationToken).Symbol is IMethodSymbol method &&
                method.ContainingType?.SpecialType == SpecialType.System_String &&
                method.Name is "IsNullOrEmpty" or "IsNullOrWhiteSpace";
        }

        /// <summary>
        /// Builds <c>prop.FullName</c> — the absolute path string exposed by FileInfo/DirectoryInfo (via
        /// FileSystemInfo) — used to feed a retyped path property into APIs that still expect a string.
        /// </summary>
        private static ExpressionSyntax AppendFullName(ExpressionSyntax propertyAccess) =>
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                propertyAccess.WithoutTrivia(),
                SyntaxFactory.IdentifierName("FullName"));

        /// <summary>
        /// True when <paramref name="propertyAccess"/> is the single argument of a <c>System.IO.Path.GetFullPath</c>
        /// call; outputs that invocation so it can be replaced wholesale.
        /// </summary>
        private static bool IsPathGetFullPathInvocation(
            SemanticModel semanticModel,
            ExpressionSyntax propertyAccess,
            CancellationToken cancellationToken,
            out InvocationExpressionSyntax invocation)
        {
            invocation = null!;

            if (propertyAccess.Parent is not ArgumentSyntax argument ||
                argument.Parent is not ArgumentListSyntax argumentList ||
                argumentList.Arguments.Count != 1 ||
                argumentList.Parent is not InvocationExpressionSyntax candidate)
            {
                return false;
            }

            if (semanticModel.GetSymbolInfo(candidate, cancellationToken).Symbol is not IMethodSymbol method ||
                method.Name != "GetFullPath" ||
                method.ContainingType?.ToDisplayString() != "System.IO.Path")
            {
                return false;
            }

            invocation = candidate;
            return true;
        }

        /// <summary>
        /// True when <paramref name="propertyAccess"/> is an argument bound to a <c>string</c> parameter of an
        /// invocation or object creation, i.e. a site where a retyped path property must be converted back to a
        /// string (AbsolutePath implicitly, FileInfo/DirectoryInfo via <c>.FullName</c>).
        /// </summary>
        private static bool IsStringArgument(
            SemanticModel semanticModel,
            ExpressionSyntax propertyAccess,
            CancellationToken cancellationToken)
        {
            if (propertyAccess.Parent is not ArgumentSyntax argument ||
                argument.Parent is not ArgumentListSyntax argumentList ||
                argumentList.Parent is not (InvocationExpressionSyntax or ObjectCreationExpressionSyntax))
            {
                return false;
            }

            ImmutableArray<IArgumentOperation> argumentOperations = semanticModel.GetOperation(argumentList.Parent, cancellationToken) switch
            {
                IInvocationOperation invocation => invocation.Arguments,
                IObjectCreationOperation creation => creation.Arguments,
                _ => default,
            };

            if (argumentOperations.IsDefault)
            {
                return false;
            }

            foreach (var argumentOperation in argumentOperations)
            {
                if (argumentOperation.Syntax == argument)
                {
                    return argumentOperation.Parameter?.Type.SpecialType == SpecialType.System_String;
                }
            }

            return false;
        }

        /// <summary>
        /// MSBuildTask0007 (scalar): rewrites <c>new T(item.ItemSpec)</c>, <c>int.Parse(item.ItemSpec)</c>, or the
        /// equivalent over <c>item.GetMetadata("FullPath")</c> to <c>item.Value</c> after the property is retyped
        /// to <c>ITaskItem&lt;T&gt;</c>.
        /// </summary>
        private static bool TryRewriteItemSpecConversion(
            SemanticModel semanticModel,
            ExpressionSyntax itemAccess,
            ExpressionSyntax valueReceiver,
            string suggestedType,
            ITypeSymbol? expectedResultType,
            List<(SyntaxNode, SyntaxNode)> siteEdits,
            CancellationToken cancellationToken)
        {
            // The path access is either `item.ItemSpec` or `item.GetMetadata("FullPath")`.
            ExpressionSyntax? pathAccess = GetItemPathAccess(itemAccess);
            if (pathAccess is null)
            {
                return false;
            }

            var conversion = GetSingleArgumentConversion(pathAccess);
            if (conversion is null || !IsExpectedConversion(semanticModel, conversion, suggestedType, expectedResultType, isItemRule: true, cancellationToken))
            {
                return false;
            }

            var valueAccess = SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    valueReceiver.WithoutTrivia(),
                    SyntaxFactory.IdentifierName("Value"))
                .WithTriviaFrom(conversion)
                .WithAdditionalAnnotations(Formatter.Annotation);

            siteEdits.Add((conversion, valueAccess));
            return true;
        }

        /// <summary>
        /// When <paramref name="itemAccess"/> is the receiver of an item path access — <c>item.ItemSpec</c> or
        /// <c>item.GetMetadata("FullPath")</c> (the documented way to read an item's absolute path) — returns the
        /// full path-access expression; otherwise null.
        /// </summary>
        private static ExpressionSyntax? GetItemPathAccess(ExpressionSyntax itemAccess)
        {
            if (itemAccess.Parent is not MemberAccessExpressionSyntax memberAccess ||
                memberAccess.Expression != itemAccess)
            {
                return null;
            }

            // item.ItemSpec
            if (memberAccess.Name.Identifier.Text == "ItemSpec")
            {
                return memberAccess;
            }

            // item.GetMetadata("FullPath")
            if (memberAccess.Name.Identifier.Text == "GetMetadata" &&
                memberAccess.Parent is InvocationExpressionSyntax invocation &&
                invocation.Expression == memberAccess &&
                invocation.ArgumentList.Arguments.Count == 1 &&
                invocation.ArgumentList.Arguments[0].Expression is LiteralExpressionSyntax literal &&
                literal.IsKind(SyntaxKind.StringLiteralExpression) &&
                string.Equals(literal.Token.ValueText, "FullPath", System.StringComparison.OrdinalIgnoreCase))
            {
                return invocation;
            }

            return null;
        }

        /// <summary>
        /// MSBuildTask0007 (array): the property is the source of a <c>foreach (var x in Prop)</c> whose loop
        /// variable's every use is an <c>x.ItemSpec</c> conversion. Rewrites each conversion to <c>x.Value</c>.
        /// Only <c>var</c> loop variables are handled so the element re-infers to <c>ITaskItem&lt;T&gt;</c>.
        /// </summary>
        private static bool TryRewriteForEachArray(
            SemanticModel semanticModel,
            ExpressionSyntax propertyAccess,
            string suggestedType,
            ITypeSymbol? expectedResultType,
            List<(SyntaxNode, SyntaxNode)> siteEdits,
            CancellationToken cancellationToken)
        {
            if (propertyAccess.Parent is not ForEachStatementSyntax forEach ||
                forEach.Expression != propertyAccess ||
                !forEach.Type.IsVar)
            {
                return false;
            }

            if (semanticModel.GetDeclaredSymbol(forEach, cancellationToken) is not ILocalSymbol loopVariable)
            {
                return false;
            }

            bool sawConversion = false;
            foreach (var identifier in forEach.Statement.DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                if (identifier.Identifier.Text != loopVariable.Name ||
                    !SymbolEqualityComparer.Default.Equals(semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol, loopVariable))
                {
                    continue;
                }

                var loopVariableReference = SyntaxFactory.IdentifierName(loopVariable.Name);
                if (!TryRewriteItemSpecConversion(semanticModel, identifier, loopVariableReference, suggestedType, expectedResultType, siteEdits, cancellationToken))
                {
                    return false;
                }

                sawConversion = true;
            }

            return sawConversion;
        }

        /// <summary>
        /// Returns the conversion expression (object creation or <c>Type.Parse</c> invocation) when
        /// <paramref name="argument"/> is its single argument; otherwise null.
        /// </summary>
        private static ExpressionSyntax? GetSingleArgumentConversion(ExpressionSyntax argument)
        {
            if (argument.Parent is not ArgumentSyntax arg ||
                arg.Parent is not ArgumentListSyntax argumentList ||
                argumentList.Arguments.Count != 1)
            {
                return null;
            }

            return argumentList.Parent switch
            {
                ObjectCreationExpressionSyntax creation => creation,
                InvocationExpressionSyntax invocation => invocation,
                _ => null,
            };
        }

        /// <summary>
        /// Verifies the conversion produces exactly the suggested strong type, so replacing it does not change
        /// the static type observed by surrounding code (e.g. <c>var x = ...;</c>).
        /// </summary>
        private static bool IsExpectedConversion(
            SemanticModel semanticModel,
            ExpressionSyntax conversion,
            string suggestedType,
            ITypeSymbol? expectedResultType,
            bool isItemRule,
            CancellationToken cancellationToken)
        {
            switch (conversion)
            {
                case ObjectCreationExpressionSyntax:
                    break;

                case InvocationExpressionSyntax invocation when invocation.Expression is MemberAccessExpressionSyntax memberAccess:
                    string methodName = memberAccess.Name.Identifier.Text;
                    if (!isItemRule)
                    {
                        // Path rule: only TaskEnvironment.GetAbsolutePath qualifies.
                        if (methodName != "GetAbsolutePath")
                        {
                            return false;
                        }
                    }
                    else
                    {
                        // Item rule: Parse, Convert.ToXxx, or TaskEnvironment.GetAbsolutePath. These mirror the
                        // conversions the analyzer reports as MSBuildTask0007; the result-type equality check
                        // below guarantees the rewrite to .Value preserves the statically observed type.
                        bool isConvertMethod = methodName.StartsWith("To", System.StringComparison.Ordinal) &&
                            semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol convertMethod &&
                            convertMethod.ContainingType?.ToDisplayString() == "System.Convert";

                        if (methodName != "Parse" && methodName != "GetAbsolutePath" && !isConvertMethod)
                        {
                            return false;
                        }
                    }

                    break;

                default:
                    return false;
            }

            var resultType = semanticModel.GetTypeInfo(conversion, cancellationToken).Type;
            if (resultType is null)
            {
                return false;
            }

            return expectedResultType is not null
                ? SymbolEqualityComparer.Default.Equals(resultType, expectedResultType)
                : resultType.ToDisplayString() == suggestedType;
        }

        /// <summary>
        /// Returns the expression that yields the property value: the identifier itself, or the enclosing
        /// <c>this.Prop</c> member access when the identifier is the member name.
        /// </summary>
        private static ExpressionSyntax GetPropertyAccessExpression(IdentifierNameSyntax identifier)
        {
            if (identifier.Parent is MemberAccessExpressionSyntax memberAccess && memberAccess.Name == identifier)
            {
                return memberAccess;
            }

            return identifier;
        }

        private static PropertyDeclarationSyntax BuildRetypedProperty(PropertyFixPlan plan)
        {
            var newType = SyntaxFactory.ParseTypeName(plan.NewTypeName)
                .WithTriviaFrom(plan.PropertyDeclaration.Type)
                .WithAdditionalAnnotations(Simplifier.Annotation);

            var newProperty = plan.PropertyDeclaration.WithType(newType);

            // The original string initializer is invalid once the type changes; swap in the value computed by
            // TryBuildInitializerValue (a type-compatible default, or the default re-expressed as the new type).
            if (plan.PropertyDeclaration.Initializer is not null && plan.InitializerValue is not null)
            {
                newProperty = newProperty.WithInitializer(
                    plan.PropertyDeclaration.Initializer.WithValue(plan.InitializerValue));
            }

            return newProperty.WithAdditionalAnnotations(Formatter.Annotation);
        }

        private static ITypeSymbol? ResolvePathTypeSymbol(Compilation compilation, string suggestedType) => suggestedType switch
        {
            "AbsolutePath" => compilation.GetTypeByMetadataName(WellKnownTypeNames.AbsolutePathFullName),
            "FileInfo" => compilation.GetTypeByMetadataName(WellKnownTypeNames.FileInfoFullName),
            "DirectoryInfo" => compilation.GetTypeByMetadataName(WellKnownTypeNames.DirectoryInfoFullName),
            _ => null,
        };

        private static string FullyQualify(string typeName) => typeName switch
        {
            "AbsolutePath" => "global::" + WellKnownTypeNames.AbsolutePathFullName,
            "FileInfo" => "global::System.IO.FileInfo",
            "DirectoryInfo" => "global::System.IO.DirectoryInfo",
            _ => typeName,
        };

        private static string BuildNewTypeName(string suggestedType, bool isItemRule, bool isArray)
        {
            if (!isItemRule)
            {
                return FullyQualify(suggestedType);
            }

            string itemType = $"global::{WellKnownTypeNames.ITaskItemFullName}<{FullyQualify(suggestedType)}>";
            return isArray ? itemType + "[]" : itemType;
        }

        private static string BuildDisplayTypeName(string suggestedType, bool isItemRule, bool isArray)
        {
            if (!isItemRule)
            {
                return suggestedType;
            }

            string itemType = $"ITaskItem<{suggestedType}>";
            return isArray ? itemType + "[]" : itemType;
        }

        /// <summary>
        /// The set of edits required to retype one property and rewrite all of its conversion sites.
        /// </summary>
        private sealed class PropertyFixPlan
        {
            public PropertyFixPlan(
                IPropertySymbol property,
                PropertyDeclarationSyntax propertyDeclaration,
                string newTypeName,
                string newTypeDisplay,
                ExpressionSyntax? initializerValue,
                List<(SyntaxNode, SyntaxNode)> siteEdits,
                bool initializeInExecute = false,
                StatementSyntax? executePrologue = null,
                StatementSyntax? executeAnchor = null,
                BlockSyntax? executeBody = null)
            {
                Property = property;
                PropertyDeclaration = propertyDeclaration;
                NewTypeName = newTypeName;
                NewTypeDisplay = newTypeDisplay;
                InitializerValue = initializerValue;
                SiteEdits = siteEdits;
                InitializeInExecute = initializeInExecute;
                ExecutePrologue = executePrologue;
                ExecuteAnchor = executeAnchor;
                ExecuteBody = executeBody;
            }

            public IPropertySymbol Property { get; }

            public PropertyDeclarationSyntax PropertyDeclaration { get; }

            public string NewTypeName { get; }

            public string NewTypeDisplay { get; }

            /// <summary>
            /// The value to use for the retyped property's initializer when the original property had one;
            /// null when the property has no initializer to carry over.
            /// </summary>
            public ExpressionSyntax? InitializerValue { get; }

            public List<(SyntaxNode OldNode, SyntaxNode NewNode)> SiteEdits { get; }

            /// <summary>True for MSBuildTask0008: the relative default is (re)initialized in Execute().</summary>
            public bool InitializeInExecute { get; }

            /// <summary>The guarded normalization statement to insert at the top of Execute(); null otherwise.</summary>
            public StatementSyntax? ExecutePrologue { get; }

            /// <summary>The existing first statement of Execute() to insert before; null when the body is empty.</summary>
            public StatementSyntax? ExecuteAnchor { get; }

            /// <summary>The Execute() body, used to place the prologue when the body has no statements yet.</summary>
            public BlockSyntax? ExecuteBody { get; }
        }

        /// <summary>
        /// Applies the property-scoped fix across all diagnostics in a document, grouping by property so each
        /// property is retyped once even when reported at multiple conversion sites.
        /// </summary>
        private sealed class PreferTypedParameterFixAllProvider : DocumentBasedFixAllProvider
        {
            protected override async Task<Document?> FixAllAsync(FixAllContext fixAllContext, Document document, ImmutableArray<Diagnostic> diagnostics)
            {
                if (diagnostics.IsDefaultOrEmpty)
                {
                    return document;
                }

                return await ApplyPlansAsync(document, diagnostics, fixAllContext.CancellationToken).ConfigureAwait(false);
            }
        }
    }
}
