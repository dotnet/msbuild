// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.Build.TaskAuthoring.Analyzer
{
    /// <summary>
    /// Suggests constructor injection for concrete tasks that implement <c>IMultiThreadableTask</c>.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class TaskEnvironmentConstructorInjectionAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(DiagnosticDescriptors.PreferTaskEnvironmentConstructorInjection);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private static void OnCompilationStart(CompilationStartAnalysisContext context)
        {
            INamedTypeSymbol? multiThreadableTaskType =
                context.Compilation.GetTypeByMetadataName(WellKnownTypeNames.IMultiThreadableTaskFullName);
            if (multiThreadableTaskType is null)
            {
                return;
            }

            INamedTypeSymbol? taskEnvironmentType =
                context.Compilation.GetTypeByMetadataName(WellKnownTypeNames.TaskEnvironmentFullName);
            if (taskEnvironmentType is null)
            {
                return;
            }

            context.RegisterSymbolAction(
                symbolContext => AnalyzeNamedType(
                    symbolContext,
                    multiThreadableTaskType,
                    taskEnvironmentType),
                SymbolKind.NamedType);
        }

        private static void AnalyzeNamedType(
            SymbolAnalysisContext context,
            INamedTypeSymbol multiThreadableTaskType,
            INamedTypeSymbol taskEnvironmentType)
        {
            var taskType = (INamedTypeSymbol)context.Symbol;
            if (taskType.TypeKind != TypeKind.Class ||
                taskType.IsAbstract ||
                !SharedAnalyzerHelpers.ImplementsInterface(taskType, multiThreadableTaskType) ||
                HasTaskEnvironmentConstructor(taskType, taskEnvironmentType))
            {
                return;
            }

            foreach (Location location in taskType.Locations)
            {
                if (location.IsInSource)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.PreferTaskEnvironmentConstructorInjection,
                        location,
                        taskType.Name));
                    return;
                }
            }
        }

        private static bool HasTaskEnvironmentConstructor(
            INamedTypeSymbol taskType,
            INamedTypeSymbol taskEnvironmentType)
        {
            foreach (IMethodSymbol constructor in taskType.InstanceConstructors)
            {
                if (constructor.DeclaredAccessibility == Accessibility.Public &&
                    constructor.Parameters.Length == 1 &&
                    SymbolEqualityComparer.Default.Equals(constructor.Parameters[0].Type, taskEnvironmentType))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
