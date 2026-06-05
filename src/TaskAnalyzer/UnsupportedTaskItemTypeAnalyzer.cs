// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using static Microsoft.Build.TaskAuthoring.Analyzer.SharedAnalyzerHelpers;

namespace Microsoft.Build.TaskAuthoring.Analyzer
{
    /// <summary>
    /// Roslyn analyzer that warns when a task property is typed as <c>ITaskItem&lt;T&gt;</c> or
    /// <c>ITaskItem&lt;T&gt;[]</c> where <c>T</c> is not in the set of types that MSBuild's
    /// <c>ValueTypeParser</c> can parse at runtime.
    ///
    /// MSBuildTask0009: ITaskItem&lt;T&gt; used with unsupported type argument.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class UnsupportedTaskItemTypeAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
            ImmutableArray.Create(DiagnosticDescriptors.UnsupportedTaskItemType);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private static void OnCompilationStart(CompilationStartAnalysisContext compilationContext)
        {
            var iTaskType = compilationContext.Compilation.GetTypeByMetadataName(WellKnownTypeNames.ITaskFullName);
            if (iTaskType is null)
            {
                return;
            }

            var iTaskItemOfTType = compilationContext.Compilation.GetTypeByMetadataName(WellKnownTypeNames.ITaskItemOfTFullName);
            if (iTaskItemOfTType is null)
            {
                return;
            }

            var outputAttributeType = compilationContext.Compilation.GetTypeByMetadataName(WellKnownTypeNames.OutputAttributeFullName);
            var absolutePathType = compilationContext.Compilation.GetTypeByMetadataName(WellKnownTypeNames.AbsolutePathFullName);
            var fileInfoType = compilationContext.Compilation.GetTypeByMetadataName(WellKnownTypeNames.FileInfoFullName);
            var directoryInfoType = compilationContext.Compilation.GetTypeByMetadataName(WellKnownTypeNames.DirectoryInfoFullName);

            compilationContext.RegisterSymbolAction(symbolContext =>
            {
                var namedType = (INamedTypeSymbol)symbolContext.Symbol;

                // Only check ITask implementations
                if (!ImplementsInterface(namedType, iTaskType))
                {
                    return;
                }

                foreach (var member in namedType.GetMembers())
                {
                    if (member is not IPropertySymbol property)
                    {
                        continue;
                    }

                    if (property.DeclaredAccessibility != Accessibility.Public ||
                        property.SetMethod is null ||
                        HasAttribute(property, outputAttributeType))
                    {
                        continue;
                    }

                    // Unwrap array: ITaskItem<T>[] → ITaskItem<T>
                    ITypeSymbol propertyType = property.Type;
                    if (propertyType is IArrayTypeSymbol arrayType)
                    {
                        propertyType = arrayType.ElementType;
                    }

                    // Check if the property type is ITaskItem<T>
                    if (propertyType is not INamedTypeSymbol namedPropertyType ||
                        !namedPropertyType.IsGenericType ||
                        !SymbolEqualityComparer.Default.Equals(namedPropertyType.OriginalDefinition, iTaskItemOfTType))
                    {
                        continue;
                    }

                    ITypeSymbol typeArg = namedPropertyType.TypeArguments[0];

                    // Check if T is in the supported set
                    if (SupportedTaskItemTypes.IsSupported(typeArg, absolutePathType, fileInfoType, directoryInfoType))
                    {
                        continue;
                    }

                    symbolContext.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.UnsupportedTaskItemType,
                        property.Locations[0],
                        property.Name,
                        typeArg.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                        SupportedTaskItemTypes.DisplayNames));
                }
            }, SymbolKind.NamedType);
        }

        private static bool HasAttribute(IPropertySymbol property, INamedTypeSymbol? attributeType)
        {
            if (attributeType is null)
            {
                return false;
            }

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
