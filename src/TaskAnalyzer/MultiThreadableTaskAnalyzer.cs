// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

using static Microsoft.Build.TaskAuthoring.Analyzer.SharedAnalyzerHelpers;

namespace Microsoft.Build.TaskAuthoring.Analyzer
{
    /// <summary>
    /// Roslyn analyzer that detects unsafe API usage in MSBuild task implementations.
    /// 
    /// By default, MSBuildTask0002 and MSBuildTask0003 apply at their full severity only to MT-scoped code.
    /// Other tasks receive the same diagnostic IDs as migration guidance (Info/Suggestion).
    /// The "msbuild_task_analyzer.run_mt_analyzers_on_all_tasks" option reports these rules at their full
    /// severity for all tasks.
    ///   (MSBuildTask0001 and MSBuildTask0004 always fire on all tasks regardless)
    /// 
    /// Per review feedback from @rainersigwald:
    /// - Console.* promoted to MSBuildTask0001 (always wrong in tasks)
    /// - Helper classes can opt in via [MSBuildMultiThreadableTaskAnalyzed] attribute
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class MultiThreadableTaskAnalyzer : DiagnosticAnalyzer
    {
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
            var iTaskType = compilationContext.Compilation.GetTypeByMetadataName(WellKnownTypeNames.ITaskFullName);
            if (iTaskType is null)
            {
                // No ITask in compilation - nothing to analyze
                return;
            }

            var taskEnvironmentType = compilationContext.Compilation.GetTypeByMetadataName(WellKnownTypeNames.TaskEnvironmentFullName);
            var absolutePathType = compilationContext.Compilation.GetTypeByMetadataName(WellKnownTypeNames.AbsolutePathFullName);
            var iTaskItemType = compilationContext.Compilation.GetTypeByMetadataName(WellKnownTypeNames.ITaskItemFullName);
            var consoleType = compilationContext.Compilation.GetTypeByMetadataName(WellKnownTypeNames.ConsoleFullName);
            var contributingMultiThreadableTaskBaseTypes = FindContributingMultiThreadableTaskBaseTypes(
                compilationContext.Compilation,
                iTaskType);

            // Build symbol lookup for banned APIs
            var bannedApiLookup = BuildBannedApiLookup(compilationContext.Compilation);

            // Build set of file-path types for MSBuildTask0003
            var filePathTypes = ResolveFilePathTypes(compilationContext.Compilation);
            var analyzeAllTasksByTree = new ConcurrentDictionary<SyntaxTree, bool>();

            // Use RegisterSymbolStartAction for efficient per-type scoping
            compilationContext.RegisterSymbolStartAction(symbolStartContext =>
            {
                var namedType = (INamedTypeSymbol)symbolStartContext.Symbol;

                if (!IsDirectlyAnalyzedType(
                    namedType,
                    iTaskType,
                    contributingMultiThreadableTaskBaseTypes,
                    out bool analyzeAsMultiThreadable))
                {
                    return;
                }

                // Register operation-level analysis within this type
                symbolStartContext.RegisterOperationAction(
                    ctx => AnalyzeOperation(ctx, bannedApiLookup, filePathTypes, analyzeAsMultiThreadable, analyzeAllTasksByTree,
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
            bool analyzeAsMultiThreadable,
            ConcurrentDictionary<SyntaxTree, bool> analyzeAllTasksByTree,
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
                if (IsAbsolutePathCanonicalization(context.Operation, absolutePathType))
                {
                    return;
                }

                // MSBuildTask0002 (TaskEnvironment) keeps its severity for MT-scoped code and is
                // downgraded to migration guidance elsewhere.
                bool enforceAsMultiThreadable =
                    entry.Category != BannedApiDefinitions.ApiCategory.TaskEnvironment ||
                    ShouldEnforceEnvironmentRules(context, analyzeAsMultiThreadable, analyzeAllTasksByTree);

                var descriptor = GetDescriptor(entry.Category);
                var displayName = referencedSymbol.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat);
                context.ReportDiagnostic(CreateWithContextualSeverity(
                    descriptor,
                    context.Operation.Syntax.GetLocation(),
                    enforceAsMultiThreadable,
                    displayName,
                    entry.Message));
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

            // MSBuildTask0003 keeps its severity for MT-scoped code and is downgraded to migration
            // guidance elsewhere.
            if (!arguments.IsDefaultOrEmpty && referencedSymbol is IMethodSymbol method)
            {
                var containingType = method.ContainingType;
                if (containingType is not null &&
                    filePathTypes.Contains(containingType) &&
                    HasUnwrappedPathArgument(arguments, taskEnvironmentType, absolutePathType, iTaskItemType))
                {
                    string displayName = isConstructor
                        ? $"new {containingType.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat)}(...)"
                        : referencedSymbol.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat);

                    string hint = "wrap path argument with TaskEnvironment.GetAbsolutePath()";
                    context.ReportDiagnostic(CreateWithContextualSeverity(
                        DiagnosticDescriptors.FilePathRequiresAbsolute,
                        context.Operation.Syntax.GetLocation(),
                        ShouldEnforceEnvironmentRules(context, analyzeAsMultiThreadable, analyzeAllTasksByTree),
                        displayName,
                        hint));
                }
            }
        }

        /// <summary>
        /// Returns true when MT migration rules apply at their full severity: the code is MT-scoped, or
        /// the all-task migration option is enabled for the source tree.
        /// </summary>
        private static bool ShouldEnforceEnvironmentRules(
            OperationAnalysisContext context,
            bool analyzeAsMultiThreadable,
            ConcurrentDictionary<SyntaxTree, bool> analyzeAllTasksByTree)
        {
            if (analyzeAsMultiThreadable)
            {
                return true;
            }

            SyntaxTree syntaxTree = context.Operation.Syntax.SyntaxTree;
            if (analyzeAllTasksByTree.TryGetValue(syntaxTree, out bool analyzeAllTasks))
            {
                return analyzeAllTasks;
            }

            analyzeAllTasks = ReadAnalyzeAllTasksOption(
                context.Options.AnalyzerConfigOptionsProvider,
                syntaxTree);
            analyzeAllTasksByTree.TryAdd(syntaxTree, analyzeAllTasks);
            return analyzeAllTasks;
        }

        private static DiagnosticDescriptor GetDescriptor(BannedApiDefinitions.ApiCategory category)
        {
            return category switch
            {
                BannedApiDefinitions.ApiCategory.CriticalError => DiagnosticDescriptors.CriticalError,
                BannedApiDefinitions.ApiCategory.TaskEnvironment => DiagnosticDescriptors.TaskEnvironmentRequired,
                BannedApiDefinitions.ApiCategory.FilePathRequiresAbsolute => DiagnosticDescriptors.FilePathRequiresAbsolute,
                BannedApiDefinitions.ApiCategory.PotentialIssue => DiagnosticDescriptors.PotentialIssue,
                _ => DiagnosticDescriptors.TaskEnvironmentRequired,
            };
        }
    }
}
