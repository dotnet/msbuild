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
            ImmutableArray.Create(DiagnosticIds.PreferTypedPathParameter, DiagnosticIds.PreferTypedTaskItem);

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
                        title: $"Change '{plan.Property.Name}' to '{plan.NewTypeDisplay}'",
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
            bool isArray = propertyDeclaration.Type is ArrayTypeSyntax;

            // The strong type the rewritten conversions must produce (used to guard against losing semantics).
            ITypeSymbol? expectedResultType = ResolvePathTypeSymbol(semanticModel.Compilation, suggestedType);

            var siteEdits = new List<(SyntaxNode, SyntaxNode)>();
            if (!TryCollectSiteEdits(semanticModel, typeDeclaration, property, suggestedType, expectedResultType, isItemRule, isArray, siteEdits, cancellationToken))
            {
                return null;
            }

            if (siteEdits.Count == 0)
            {
                return null;
            }

            string newTypeName = BuildNewTypeName(suggestedType, isItemRule, isArray);
            string newTypeDisplay = BuildDisplayTypeName(suggestedType, isItemRule, isArray);
            bool newTypeIsValueType = !isItemRule && suggestedType == "AbsolutePath";

            return new PropertyFixPlan(property, propertyDeclaration, newTypeName, newTypeDisplay, newTypeIsValueType, siteEdits);
        }

        /// <summary>
        /// Visits every reference to <paramref name="property"/> within the task type and verifies each one is a
        /// conversion that can be rewritten. Populates <paramref name="siteEdits"/>; returns false if any reference
        /// cannot be safely rewritten (in which case no fix should be offered).
        /// </summary>
        private static bool TryCollectSiteEdits(
            SemanticModel semanticModel,
            TypeDeclarationSyntax typeDeclaration,
            IPropertySymbol property,
            string suggestedType,
            ITypeSymbol? expectedResultType,
            bool isItemRule,
            bool isArray,
            List<(SyntaxNode, SyntaxNode)> siteEdits,
            CancellationToken cancellationToken)
        {
            foreach (var identifier in typeDeclaration.DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                if (identifier.Identifier.Text != property.Name)
                {
                    continue;
                }

                if (!SymbolEqualityComparer.Default.Equals(semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol, property))
                {
                    continue;
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

            return true;
        }

        /// <summary>
        /// MSBuildTask0006: rewrites <c>new T(prop)</c> or <c>TaskEnvironment.GetAbsolutePath(prop)</c> to just
        /// <c>prop</c> after the property is retyped to <paramref name="suggestedType"/>.
        /// </summary>
        private static bool TryRewritePathConversion(
            SemanticModel semanticModel,
            ExpressionSyntax propertyAccess,
            string suggestedType,
            ITypeSymbol? expectedResultType,
            List<(SyntaxNode, SyntaxNode)> siteEdits,
            CancellationToken cancellationToken)
        {
            var conversion = GetSingleArgumentConversion(propertyAccess);
            if (conversion is null || !IsExpectedConversion(semanticModel, conversion, suggestedType, expectedResultType, isItemRule: false, cancellationToken))
            {
                return false;
            }

            var replacement = propertyAccess
                .WithoutTrivia()
                .WithTriviaFrom(conversion)
                .WithAdditionalAnnotations(Formatter.Annotation);

            siteEdits.Add((conversion, replacement));
            return true;
        }

        /// <summary>
        /// MSBuildTask0007 (scalar): rewrites <c>new T(item.ItemSpec)</c> or <c>int.Parse(item.ItemSpec)</c> to
        /// <c>item.Value</c> after the property is retyped to <c>ITaskItem&lt;T&gt;</c>.
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
            // itemAccess must be the receiver of an `.ItemSpec` member access.
            if (itemAccess.Parent is not MemberAccessExpressionSyntax itemSpecAccess ||
                itemSpecAccess.Expression != itemAccess ||
                itemSpecAccess.Name.Identifier.Text != "ItemSpec")
            {
                return false;
            }

            var conversion = GetSingleArgumentConversion(itemSpecAccess);
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

            // A string initializer (e.g. = "") is invalid once the type changes; replace it with a compatible default.
            if (plan.PropertyDeclaration.Initializer is not null)
            {
                ExpressionSyntax defaultValue = plan.NewTypeIsValueType
                    ? SyntaxFactory.LiteralExpression(SyntaxKind.DefaultLiteralExpression, SyntaxFactory.Token(SyntaxKind.DefaultKeyword))
                    : SyntaxFactory.PostfixUnaryExpression(
                        SyntaxKind.SuppressNullableWarningExpression,
                        SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression));

                newProperty = newProperty.WithInitializer(
                    plan.PropertyDeclaration.Initializer.WithValue(defaultValue));
            }

            return newProperty.WithAdditionalAnnotations(Formatter.Annotation);
        }

        private static ITypeSymbol? ResolvePathTypeSymbol(Compilation compilation, string suggestedType) => suggestedType switch
        {
            "AbsolutePath" => compilation.GetTypeByMetadataName(WellKnownTypeNames.AbsolutePathFullName),
            "FileInfo" => compilation.GetTypeByMetadataName("System.IO.FileInfo"),
            "DirectoryInfo" => compilation.GetTypeByMetadataName("System.IO.DirectoryInfo"),
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
                bool newTypeIsValueType,
                List<(SyntaxNode, SyntaxNode)> siteEdits)
            {
                Property = property;
                PropertyDeclaration = propertyDeclaration;
                NewTypeName = newTypeName;
                NewTypeDisplay = newTypeDisplay;
                NewTypeIsValueType = newTypeIsValueType;
                SiteEdits = siteEdits;
            }

            public IPropertySymbol Property { get; }

            public PropertyDeclarationSyntax PropertyDeclaration { get; }

            public string NewTypeName { get; }

            public string NewTypeDisplay { get; }

            public bool NewTypeIsValueType { get; }

            public List<(SyntaxNode OldNode, SyntaxNode NewNode)> SiteEdits { get; }
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
