// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.Build.TaskAuthoring.Analyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class RequiredTaskPropertyInitializationSuppressor : DiagnosticSuppressor
    {
        private static readonly SuppressionDescriptor s_requiredTaskPropertyInitialization = new(
            id: "MSBuildTaskSPR0001",
            suppressedDiagnosticId: "CS8618",
            justification: "MSBuild initializes [Required] task properties before Execute runs.");

        public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions => [s_requiredTaskPropertyInitialization];

        public override void ReportSuppressions(SuppressionAnalysisContext context)
        {
            var requiredAttributeType = context.Compilation.GetTypeByMetadataName(WellKnownTypeNames.RequiredAttributeFullName);
            var iTaskType = context.Compilation.GetTypeByMetadataName(WellKnownTypeNames.ITaskFullName);
            if (requiredAttributeType is null || iTaskType is null)
            {
                return;
            }

            foreach (Diagnostic diagnostic in context.ReportedDiagnostics)
            {
                if (!string.Equals(diagnostic.Id, "CS8618", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!TryGetPropertySymbol(context, diagnostic, out IPropertySymbol? propertyCandidate) || propertyCandidate is null)
                {
                    continue;
                }

                IPropertySymbol property = propertyCandidate;
                INamedTypeSymbol? containingType = property.ContainingType;
                if (containingType is null || !SharedAnalyzerHelpers.ImplementsInterface(containingType, iTaskType))
                {
                    continue;
                }

                if (!CanBeAssignedByMSBuild(property) || !HasAttribute(property, requiredAttributeType))
                {
                    continue;
                }

                context.ReportSuppression(Suppression.Create(s_requiredTaskPropertyInitialization, diagnostic));
            }
        }

        private static bool TryGetPropertySymbol(
            SuppressionAnalysisContext context,
            Diagnostic diagnostic,
            out IPropertySymbol? property)
        {
            if (TryGetPropertySymbol(context, diagnostic.Location, out property))
            {
                return true;
            }

            foreach (Location location in diagnostic.AdditionalLocations)
            {
                if (TryGetPropertySymbol(context, location, out property))
                {
                    return true;
                }
            }

            property = null;
            return false;
        }

        private static bool TryGetPropertySymbol(
            SuppressionAnalysisContext context,
            Location location,
            out IPropertySymbol? property)
        {
            property = null;
            if (!location.IsInSource || location.SourceTree is null)
            {
                return false;
            }

            SemanticModel semanticModel = context.GetSemanticModel(location.SourceTree);
            SyntaxNode root = location.SourceTree.GetRoot(context.CancellationToken);
            SyntaxNode node = root.FindNode(location.SourceSpan, getInnermostNodeForTie: true);

            for (SyntaxNode? current = node; current is not null; current = current.Parent)
            {
                if (current is PropertyDeclarationSyntax propertyDeclaration)
                {
                    property = semanticModel.GetDeclaredSymbol(propertyDeclaration, context.CancellationToken) as IPropertySymbol;
                    return property is not null;
                }
            }

            ISymbol? symbol = semanticModel.GetEnclosingSymbol(location.SourceSpan.Start, context.CancellationToken);
            property = symbol switch
            {
                IPropertySymbol propertySymbol => propertySymbol,
                IMethodSymbol { AssociatedSymbol: IPropertySymbol propertySymbol } => propertySymbol,
                _ => null,
            };

            return property is not null;
        }

        private static bool CanBeAssignedByMSBuild(IPropertySymbol property)
        {
            IMethodSymbol? setMethod = property.SetMethod;
            return setMethod is not null && setMethod.DeclaredAccessibility == Accessibility.Public;
        }

        private static bool HasAttribute(IPropertySymbol property, INamedTypeSymbol attributeType)
        {
            foreach (AttributeData attribute in property.GetAttributes())
            {
                if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeType))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
