// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

using static Microsoft.Build.TaskAuthoring.Analyzer.SharedAnalyzerHelpers;

namespace Microsoft.Build.TaskAuthoring.Analyzer
{
    /// <summary>
    /// Roslyn analyzer that detects unsafe API usage in MSBuild task implementations.
    /// 
    /// Scope (controlled by .editorconfig option "msbuild_task_analyzer.scope"):
    /// - "all" (default): All rules fire on ALL ITask implementations
    /// - "multithreadable_only": MSBuildTask0002, 0003 fire only on IMultiThreadableTask or [MSBuildMultiThreadableTask]
    ///   (MSBuildTask0001 and MSBuildTask0004 always fire on all tasks regardless)
    /// 
    /// Per review feedback from @rainersigwald:
    /// - Console.* promoted to MSBuildTask0001 (always wrong in tasks)
    /// - Helper classes can opt in via [MSBuildMultiThreadableTaskAnalyzed] attribute
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class MultiThreadableTaskAnalyzer : DiagnosticAnalyzer
    {
        /// <summary>
        /// The .editorconfig key controlling analysis scope.
        /// Values: "all" (default) | "multithreadable_only"
        /// </summary>
        internal const string ScopeOptionKey = "msbuild_task_analyzer.scope";
        internal const string ScopeAll = "all";
        internal const string ScopeMultiThreadableOnly = "multithreadable_only";
        // Well-known type names
        private const string ITaskFullName = "Microsoft.Build.Framework.ITask";
        private const string IMultiThreadableTaskFullName = "Microsoft.Build.Framework.IMultiThreadableTask";
        private const string TaskEnvironmentFullName = "Microsoft.Build.Framework.TaskEnvironment";
        private const string AbsolutePathFullName = "Microsoft.Build.Framework.AbsolutePath";
        private const string ITaskItemFullName = "Microsoft.Build.Framework.ITaskItem";
        private const string AnalyzedAttributeFullName = "Microsoft.Build.Framework.MSBuildMultiThreadableTaskAnalyzedAttribute";
        private const string MultiThreadableTaskAttributeFullName = "Microsoft.Build.Framework.MSBuildMultiThreadableTaskAttribute";

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => DiagnosticDescriptors.All;

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private void OnCompilationStart(CompilationStartAnalysisContext compilationContext)
        {
            // Resolve well-known types
            var iTaskType = compilationContext.Compilation.GetTypeByMetadataName(ITaskFullName);
            if (iTaskType is null)
            {
                // No ITask in compilation - nothing to analyze
                return;
            }

            // Read scope option from .editorconfig: "all" (default) or "multithreadable_only"
            bool analyzeAllTasks = true;
            if (compilationContext.Options.AnalyzerConfigOptionsProvider
                    .GlobalOptions.TryGetValue($"build_property.{ScopeOptionKey}", out var scopeValue) ||
                compilationContext.Options.AnalyzerConfigOptionsProvider
                    .GlobalOptions.TryGetValue(ScopeOptionKey, out scopeValue))
            {
                analyzeAllTasks = !string.Equals(scopeValue, ScopeMultiThreadableOnly, StringComparison.OrdinalIgnoreCase);
            }

            var iMultiThreadableTaskType = compilationContext.Compilation.GetTypeByMetadataName(IMultiThreadableTaskFullName);
            var taskEnvironmentType = compilationContext.Compilation.GetTypeByMetadataName(TaskEnvironmentFullName);
            var absolutePathType = compilationContext.Compilation.GetTypeByMetadataName(AbsolutePathFullName);
            var iTaskItemType = compilationContext.Compilation.GetTypeByMetadataName(ITaskItemFullName);
            var consoleType = compilationContext.Compilation.GetTypeByMetadataName("System.Console");
            var analyzedAttributeType = compilationContext.Compilation.GetTypeByMetadataName(AnalyzedAttributeFullName);
            var multiThreadableTaskAttributeType = compilationContext.Compilation.GetTypeByMetadataName(MultiThreadableTaskAttributeFullName);

            // Build symbol lookup for banned APIs
            var bannedApiLookup = BuildBannedApiLookup(compilationContext.Compilation);

            // Build set of file-path types for MSBuildTask0003
            var filePathTypes = ResolveFilePathTypes(compilationContext.Compilation);

            // Use RegisterSymbolStartAction for efficient per-type scoping
            compilationContext.RegisterSymbolStartAction(symbolStartContext =>
            {
                var namedType = (INamedTypeSymbol)symbolStartContext.Symbol;

                // Determine what kind of task this is
                bool isTask = ImplementsInterface(namedType, iTaskType);
                bool isMultiThreadableTask = iMultiThreadableTaskType is not null && ImplementsInterface(namedType, iMultiThreadableTaskType);

                // Helper classes can opt-in via [MSBuildMultiThreadableTaskAnalyzed] attribute
                bool hasAnalyzedAttribute = analyzedAttributeType is not null &&
                    namedType.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, analyzedAttributeType));

                // Tasks marked with [MSBuildMultiThreadableTask] should be analyzed as multithreadable
                bool hasMultiThreadableAttribute = multiThreadableTaskAttributeType is not null &&
                    namedType.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, multiThreadableTaskAttributeType));

                if (!isTask && !hasAnalyzedAttribute)
                {
                    return;
                }

                // Helper classes with the attribute or tasks with [MSBuildMultiThreadableTask] are treated as IMultiThreadableTask
                bool analyzeAsMultiThreadable = isMultiThreadableTask || hasAnalyzedAttribute || hasMultiThreadableAttribute;

                // When scope is "multithreadable_only", only analyze MSBuildTask0002/0003 for multithreadable tasks
                bool reportEnvironmentRules = analyzeAllTasks || analyzeAsMultiThreadable;

                // Register operation-level analysis within this type
                symbolStartContext.RegisterOperationAction(
                    ctx => AnalyzeOperation(ctx, bannedApiLookup, filePathTypes, reportEnvironmentRules, analyzeAsMultiThreadable,
                        taskEnvironmentType, absolutePathType, iTaskItemType, consoleType),
                    OperationKind.Invocation,
                    OperationKind.ObjectCreation,
                    OperationKind.PropertyReference,
                    OperationKind.FieldReference,
                    OperationKind.MethodReference,
                    OperationKind.EventReference);
            }, SymbolKind.NamedType);
        }

        private static void AnalyzeOperation(
            OperationAnalysisContext context,
            Dictionary<ISymbol, BannedApiEntry> bannedApiLookup,
            ImmutableHashSet<INamedTypeSymbol> filePathTypes,
            bool reportEnvironmentRules,
            bool isMultiThreadable,
            INamedTypeSymbol? taskEnvironmentType,
            INamedTypeSymbol? absolutePathType,
            INamedTypeSymbol? iTaskItemType,
            INamedTypeSymbol? consoleType)
        {
            ISymbol? referencedSymbol = null;
            ImmutableArray<IArgumentOperation> arguments = default;
            bool isConstructor = false;

            switch (context.Operation)
            {
                case IInvocationOperation invocation:
                    referencedSymbol = invocation.TargetMethod;
                    arguments = invocation.Arguments;
                    break;

                case IObjectCreationOperation creation:
                    referencedSymbol = creation.Constructor;
                    arguments = creation.Arguments;
                    isConstructor = true;
                    break;

                case IPropertyReferenceOperation propRef:
                    referencedSymbol = propRef.Property;
                    break;

                case IFieldReferenceOperation fieldRef:
                    referencedSymbol = fieldRef.Field;
                    break;

                case IMethodReferenceOperation methodRef:
                    referencedSymbol = methodRef.Method;
                    break;

                case IEventReferenceOperation eventRef:
                    referencedSymbol = eventRef.Event;
                    break;
            }

            if (referencedSymbol is null)
            {
                return;
            }

            // Check banned API lookup (handles MSBuildTask0001, 0002, 0004)
            if (bannedApiLookup.TryGetValue(referencedSymbol, out var entry))
            {
                // MSBuildTask0002 (TaskEnvironment) is gated by scope setting
                if (entry.Category == BannedApiDefinitions.ApiCategory.TaskEnvironment && !reportEnvironmentRules)
                {
                    return;
                }

                var descriptor = GetDescriptor(entry.Category, isMultiThreadable);
                var displayName = referencedSymbol.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat);
                context.ReportDiagnostic(Diagnostic.Create(descriptor, context.Operation.Syntax.GetLocation(),
                    displayName, entry.Message));
                return;
            }

            // Type-level Console ban: ANY member of System.Console is flagged.
            // This catches all Console methods/properties including ones added in newer .NET versions.
            if (consoleType is not null)
            {
                var containingType = referencedSymbol.ContainingType;
                if (containingType is not null && SymbolEqualityComparer.Default.Equals(containingType, consoleType))
                {
                    var displayName = referencedSymbol.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat);
                    string message = referencedSymbol.Name.StartsWith("Read", StringComparison.Ordinal)
                        ? "may cause deadlocks in automated builds"
                        : "interferes with build logging; use Log.LogMessage instead";
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.CriticalError,
                        context.Operation.Syntax.GetLocation(),
                        displayName, message));
                    return;
                }
            }

            // Check file path APIs (MSBuildTask0003) - gated by scope setting
            if (reportEnvironmentRules && !arguments.IsDefaultOrEmpty)
            {
                var method = referencedSymbol as IMethodSymbol;
                if (method is not null)
                {
                    var containingType = method.ContainingType;
                    if (containingType is not null && filePathTypes.Contains(containingType))
                    {
                        if (HasUnwrappedPathArgument(arguments, taskEnvironmentType, absolutePathType, iTaskItemType))
                        {
                            string displayName = isConstructor
                                ? $"new {containingType.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat)}(...)"
                                : referencedSymbol.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat);

                            string hint = "wrap path argument with TaskEnvironment.GetAbsolutePath()";
                            context.ReportDiagnostic(Diagnostic.Create(
                                isMultiThreadable ? DiagnosticDescriptors.FilePathRequiresAbsolute : DiagnosticDescriptors.FilePathRequiresAbsoluteInfo,
                                context.Operation.Syntax.GetLocation(),
                                displayName, hint));
                        }
                    }
                }
            }
        }

        private static DiagnosticDescriptor GetDescriptor(BannedApiDefinitions.ApiCategory category, bool isMultiThreadable)
        {
            return category switch
            {
                BannedApiDefinitions.ApiCategory.CriticalError => DiagnosticDescriptors.CriticalError,
                BannedApiDefinitions.ApiCategory.TaskEnvironment => isMultiThreadable
                    ? DiagnosticDescriptors.TaskEnvironmentRequired
                    : DiagnosticDescriptors.TaskEnvironmentRequiredInfo,
                BannedApiDefinitions.ApiCategory.PotentialIssue => isMultiThreadable
                    ? DiagnosticDescriptors.PotentialIssue
                    : DiagnosticDescriptors.PotentialIssueInfo,
                _ => isMultiThreadable
                    ? DiagnosticDescriptors.TaskEnvironmentRequired
                    : DiagnosticDescriptors.TaskEnvironmentRequiredInfo,
            };
        }
    }
}
