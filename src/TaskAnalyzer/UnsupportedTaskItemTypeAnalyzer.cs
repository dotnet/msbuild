// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
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
    /// MSBuildTask0008: ITaskItem&lt;T&gt; used with unsupported type argument.
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

            // Collect fully-qualified metadata names for supported T types (mirrors ValueTypeParser)
            var absolutePathType = compilationContext.Compilation.GetTypeByMetadataName(WellKnownTypeNames.AbsolutePathFullName);
            var fileInfoType = compilationContext.Compilation.GetTypeByMetadataName("System.IO.FileInfo");
            var directoryInfoType = compilationContext.Compilation.GetTypeByMetadataName("System.IO.DirectoryInfo");

            var supportedSpecialTypes = new HashSet<SpecialType>
            {
                SpecialType.System_Boolean,
                SpecialType.System_Char,
                SpecialType.System_Byte,
                SpecialType.System_SByte,
                SpecialType.System_Int16,
                SpecialType.System_UInt16,
                SpecialType.System_Int32,
                SpecialType.System_UInt32,
                SpecialType.System_Int64,
                SpecialType.System_UInt64,
                SpecialType.System_Single,
                SpecialType.System_Double,
                SpecialType.System_Decimal,
                SpecialType.System_String,
            };

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

                    if (property.DeclaredAccessibility != Accessibility.Public || property.SetMethod is null)
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
                    if (IsSupportedTypeArgument(typeArg, supportedSpecialTypes, absolutePathType, fileInfoType, directoryInfoType))
                    {
                        continue;
                    }

                    symbolContext.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.UnsupportedTaskItemType,
                        property.Locations[0],
                        property.Name,
                        typeArg.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
                }
            }, SymbolKind.NamedType);
        }

        private static bool IsSupportedTypeArgument(
            ITypeSymbol typeArg,
            HashSet<SpecialType> supportedSpecialTypes,
            INamedTypeSymbol? absolutePathType,
            INamedTypeSymbol? fileInfoType,
            INamedTypeSymbol? directoryInfoType)
        {
            if (typeArg.SpecialType != SpecialType.None && supportedSpecialTypes.Contains(typeArg.SpecialType))
            {
                return true;
            }

            if (absolutePathType is not null && SymbolEqualityComparer.Default.Equals(typeArg, absolutePathType))
            {
                return true;
            }

            if (fileInfoType is not null && SymbolEqualityComparer.Default.Equals(typeArg, fileInfoType))
            {
                return true;
            }

            if (directoryInfoType is not null && SymbolEqualityComparer.Default.Equals(typeArg, directoryInfoType))
            {
                return true;
            }

            return false;
        }
    }
}
