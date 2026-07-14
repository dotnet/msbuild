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
    /// <c>ITaskItem&lt;T&gt;[]</c> where <c>T</c> is not supported by MSBuild's task parameter binder.
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

                foreach (IPropertySymbol property in GetPropertiesIncludingBaseTypes(namedType))
                {
                    if (property.DeclaredAccessibility != Accessibility.Public ||
                        property.SetMethod?.DeclaredAccessibility != Accessibility.Public ||
                        property.IsStatic)
                    {
                        continue;
                    }

                    // A source-declared base task is analyzed by its own symbol action. Avoid reporting its
                    // properties again for every source-derived task while still covering metadata base tasks.
                    if (!SymbolEqualityComparer.Default.Equals(property.ContainingType, namedType) &&
                        property.ContainingType is not null &&
                        ImplementsInterface(property.ContainingType, iTaskType) &&
                        HasSourceLocation(property))
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
                    if (SupportedTaskItemTypes.IsSupportedTaskItemType(typeArg, absolutePathType, fileInfoType, directoryInfoType))
                    {
                        continue;
                    }

                    symbolContext.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.UnsupportedTaskItemType,
                        GetDiagnosticLocation(property, namedType),
                        property.Name,
                        typeArg.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                        SupportedTaskItemTypes.SupportedTaskItemTypeDisplayNames));
                }
            }, SymbolKind.NamedType);
        }

        private static bool HasSourceLocation(ISymbol symbol)
        {
            foreach (Location location in symbol.Locations)
            {
                if (location.IsInSource)
                {
                    return true;
                }
            }

            return false;
        }

        private static Location GetDiagnosticLocation(IPropertySymbol property, INamedTypeSymbol taskType)
        {
            foreach (Location location in property.Locations)
            {
                if (location.IsInSource)
                {
                    return location;
                }
            }

            return taskType.Locations.Length > 0 ? taskType.Locations[0] : Location.None;
        }
    }
}
