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
    /// Roslyn analyzer that performs transitive call graph analysis to detect unsafe API usage
    /// reachable from MSBuild task implementations.
    ///
    /// Unlike <see cref="MultiThreadableTaskAnalyzer"/> which only checks direct API calls within
    /// a task class, this analyzer builds a compilation-wide call graph and traces method calls
    /// transitively to find unsafe APIs called by helper methods, utility classes, etc.
    ///
    /// Reports MSBuildTask0005 at the unsafe call site — so that <c>#pragma warning disable</c> and
    /// <c>[SuppressMessage]</c> next to the reviewed call are honored — with the full call chain in
    /// the message and the task entry point as an additional location.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class TransitiveCallChainAnalyzer : DiagnosticAnalyzer
    {
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(DiagnosticDescriptors.TransitiveUnsafeCall);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private void OnCompilationStart(CompilationStartAnalysisContext compilationContext)
        {
            var iTaskType = compilationContext.Compilation.GetTypeByMetadataName(WellKnownTypeNames.ITaskFullName);
            if (iTaskType is null)
            {
                return;
            }

            // Read scope option from .editorconfig
            bool analyzeAllTasks = SharedAnalyzerHelpers.ReadAnalyzeAllTasksOption(compilationContext.Options.AnalyzerConfigOptionsProvider);

            var iMultiThreadableTaskType = compilationContext.Compilation.GetTypeByMetadataName(WellKnownTypeNames.IMultiThreadableTaskFullName);
            var multiThreadableTaskAttributeType = compilationContext.Compilation.GetTypeByMetadataName(WellKnownTypeNames.MultiThreadableTaskAttributeFullName);
            var analyzedAttributeType = compilationContext.Compilation.GetTypeByMetadataName(WellKnownTypeNames.AnalyzedAttributeFullName);

            var taskEnvironmentType = compilationContext.Compilation.GetTypeByMetadataName(WellKnownTypeNames.TaskEnvironmentFullName);
            var absolutePathType = compilationContext.Compilation.GetTypeByMetadataName(WellKnownTypeNames.AbsolutePathFullName);
            var iTaskItemType = compilationContext.Compilation.GetTypeByMetadataName(WellKnownTypeNames.ITaskItemFullName);
            var consoleType = compilationContext.Compilation.GetTypeByMetadataName(WellKnownTypeNames.ConsoleFullName);

            var bannedApiLookup = BuildBannedApiLookup(compilationContext.Compilation);
            var filePathTypes = ResolveFilePathTypes(compilationContext.Compilation);
            TaskTypeAnalysis taskTypeAnalysis = BuildTaskTypeAnalysis(
                compilationContext.Compilation,
                iTaskType,
                iMultiThreadableTaskType,
                multiThreadableTaskAttributeType,
                analyzedAttributeType);

            // Thread-safe collections for building the graph across concurrent operation callbacks
            var callGraph = new ConcurrentDictionary<ISymbol, ConcurrentBag<ISymbol>>(SymbolEqualityComparer.Default);
            var directViolations = new ConcurrentDictionary<ISymbol, ConcurrentBag<ViolationInfo>>(SymbolEqualityComparer.Default);

            // Phase 1: Scan ALL operations in the compilation to build call graph + record violations
            compilationContext.RegisterOperationAction(opCtx =>
            {
                ScanOperation(opCtx, callGraph, directViolations, bannedApiLookup, filePathTypes,
                    taskEnvironmentType, absolutePathType, iTaskItemType, consoleType, taskTypeAnalysis);
            },
            OperationKind.Invocation,
            OperationKind.ObjectCreation,
            OperationKind.PropertyReference,
            OperationKind.FieldReference);

            // Phase 2: At compilation end, compute transitive closure from task methods
            compilationContext.RegisterCompilationEndAction(endCtx =>
            {
                AnalyzeTransitiveViolations(
                    endCtx,
                    callGraph,
                    directViolations,
                    taskTypeAnalysis,
                    analyzeAllTasks);
            });
        }

        /// <summary>
        /// Phase 1: For each operation in the compilation, record call graph edges and direct violations.
        /// </summary>
        private static void ScanOperation(
            OperationAnalysisContext context,
            ConcurrentDictionary<ISymbol, ConcurrentBag<ISymbol>> callGraph,
            ConcurrentDictionary<ISymbol, ConcurrentBag<ViolationInfo>> directViolations,
            Dictionary<ISymbol, BannedApiEntry> bannedApiLookup,
            ImmutableHashSet<INamedTypeSymbol> filePathTypes,
            INamedTypeSymbol? taskEnvironmentType,
            INamedTypeSymbol? absolutePathType,
            INamedTypeSymbol? iTaskItemType,
            INamedTypeSymbol? consoleType,
            TaskTypeAnalysis taskTypeAnalysis)
        {
            var containingSymbol = context.ContainingSymbol;
            if (containingSymbol is not IMethodSymbol containingMethod)
            {
                return;
            }

            // Normalize to OriginalDefinition for generic methods
            var callerKey = containingMethod.OriginalDefinition;

            // Direct analysis owns operations in task and explicitly analyzed helper hierarchies.
            var containingType = containingMethod.ContainingType;
            bool isHandledByDirectAnalyzer = containingType is not null &&
                (taskTypeAnalysis.TaskHierarchyTypes.Contains(containingType) ||
                 taskTypeAnalysis.AnalyzedHelperHierarchyTypes.Contains(containingType));

            ISymbol? referencedSymbol = null;
            ImmutableArray<IArgumentOperation> arguments = default;

            switch (context.Operation)
            {
                case IInvocationOperation invocation:
                    referencedSymbol = invocation.TargetMethod;
                    arguments = invocation.Arguments;
                    break;

                case IObjectCreationOperation creation:
                    referencedSymbol = creation.Constructor;
                    arguments = creation.Arguments;
                    break;

                case IPropertyReferenceOperation propRef:
                    referencedSymbol = propRef.Property;
                    break;

                case IFieldReferenceOperation fieldRef:
                    referencedSymbol = fieldRef.Field;
                    break;
            }

            if (referencedSymbol is null)
            {
                return;
            }

            // ALWAYS record call graph edges (even for task methods — needed for BFS traversal)
            if (referencedSymbol is IMethodSymbol calleeMethod)
            {
                var calleeKey = calleeMethod.OriginalDefinition;
                callGraph.GetOrAdd(callerKey, _ => new ConcurrentBag<ISymbol>()).Add(calleeKey);
            }
            else if (referencedSymbol is IPropertySymbol property)
            {
                // Record edges to property getter and setter methods
                if (property.GetMethod is not null)
                {
                    callGraph.GetOrAdd(callerKey, _ => new ConcurrentBag<ISymbol>()).Add(property.GetMethod.OriginalDefinition);
                }

                if (property.SetMethod is not null)
                {
                    callGraph.GetOrAdd(callerKey, _ => new ConcurrentBag<ISymbol>()).Add(property.SetMethod.OriginalDefinition);
                }
            }

            // Task and explicitly analyzed helper hierarchies get direct analysis
            // from MultiThreadableTaskAnalyzer.
            if (isHandledByDirectAnalyzer)
            {
                return;
            }

            // Check if this is a banned API call → record as a direct violation
            if (bannedApiLookup.TryGetValue(referencedSymbol, out var entry))
            {
                var displayName = referencedSymbol.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat);
                var violation = new ViolationInfo(entry.Category.ToString(), displayName, entry.Message, context.Operation.Syntax.GetLocation());
                directViolations.GetOrAdd(callerKey, _ => new ConcurrentBag<ViolationInfo>()).Add(violation);
                return;
            }

            // Check Console type-level ban
            if (consoleType is not null)
            {
                var memberContainingType = referencedSymbol.ContainingType;
                if (memberContainingType is not null && SymbolEqualityComparer.Default.Equals(memberContainingType, consoleType))
                {
                    var displayName = referencedSymbol.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat);
                    string message = referencedSymbol.Name.StartsWith("Read", StringComparison.Ordinal)
                        ? "may cause deadlocks in automated builds"
                        : "interferes with build logging; use Log.LogMessage instead";
                    var violation = new ViolationInfo("CriticalError", displayName, message, context.Operation.Syntax.GetLocation());
                    directViolations.GetOrAdd(callerKey, _ => new ConcurrentBag<ViolationInfo>()).Add(violation);
                    return;
                }
            }

            // Check file path APIs
            if (!arguments.IsDefaultOrEmpty && referencedSymbol is IMethodSymbol method)
            {
                var methodContainingType = method.ContainingType;
                if (methodContainingType is not null && filePathTypes.Contains(methodContainingType))
                {
                    if (HasUnwrappedPathArgument(arguments, taskEnvironmentType, absolutePathType, iTaskItemType))
                    {
                        var displayName = referencedSymbol.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat);
                        var violation = new ViolationInfo("FilePathRequiresAbsolute", displayName,
                            "may resolve relative paths against the process working directory", context.Operation.Syntax.GetLocation());
                        directViolations.GetOrAdd(callerKey, _ => new ConcurrentBag<ViolationInfo>()).Add(violation);
                    }
                }
            }
        }

        /// <summary>
        /// Phase 2: For each task type, BFS the call graph from its methods to find transitive violations.
        /// </summary>
        private static void AnalyzeTransitiveViolations(
            CompilationAnalysisContext context,
            ConcurrentDictionary<ISymbol, ConcurrentBag<ISymbol>> callGraph,
            ConcurrentDictionary<ISymbol, ConcurrentBag<ViolationInfo>> directViolations,
            TaskTypeAnalysis taskTypeAnalysis,
            bool analyzeAllTasks)
        {
            var taskTypes = new List<INamedTypeSymbol>();
            foreach (INamedTypeSymbol taskType in taskTypeAnalysis.ConcreteTaskTypes)
            {
                if (analyzeAllTasks || taskTypeAnalysis.TypesAnalyzedAsMultiThreadableTasks.Contains(taskType))
                {
                    taskTypes.Add(taskType);
                }
            }

            foreach (var taskType in taskTypes)
            {
                // Track reported violations per task type to avoid flooding with duplicates.
                // Key: the location the diagnostic is reported at plus the target banned API display name.
                // Keeping the location in the key means a suppression on one reviewed call does not hide a
                // second, unreviewed call to the same API. Only the shortest chain per location is reported.
                var reportedPerTaskType = new HashSet<(string ApiDisplayName, Location Location)>();

                foreach (IMethodSymbol method in GetMethodsIncludingBaseTypes(taskType))
                {
                    // BFS from this method through the call graph
                    var methodKey = method.OriginalDefinition;
                    var visited = new HashSet<ISymbol>(SymbolEqualityComparer.Default) { methodKey };
                    var predecessors = new Dictionary<ISymbol, ISymbol>(SymbolEqualityComparer.Default);
                    var queue = new Queue<ISymbol>();

                    // Seed with methods called directly from this task method
                    if (callGraph.TryGetValue(methodKey, out var directCallees))
                    {
                        // Snapshot ConcurrentBag to avoid thread-local enumeration issues
                        foreach (var callee in directCallees.ToArray())
                        {
                            if (visited.Add(callee))
                            {
                                predecessors.Add(callee, methodKey);
                                queue.Enqueue(callee);
                            }
                        }
                    }

                    while (queue.Count > 0)
                    {
                        context.CancellationToken.ThrowIfCancellationRequested();

                        ISymbol current = queue.Dequeue();

                        // Check if this method has direct violations (from source scan)
                        if (directViolations.TryGetValue(current, out var violations))
                        {
                            foreach (var v in violations)
                            {
                                ReportTransitiveViolation(
                                    context,
                                    method,
                                    methodKey,
                                    current,
                                    predecessors,
                                    v,
                                    reportedPerTaskType);
                            }
                        }

                        // Try source-level call graph first
                        bool hasSourceEdges = callGraph.TryGetValue(current, out var callees);

                        if (hasSourceEdges)
                        {
                            // Snapshot ConcurrentBag to avoid thread-local enumeration issues
                            foreach (var callee in callees.ToArray())
                            {
                                if (visited.Add(callee))
                                {
                                    predecessors.Add(callee, current);
                                    queue.Enqueue(callee);
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Reports a transitive violation with deduplication per task type.
        /// Only the first (shortest) chain reaching each unsafe call site is reported.
        /// </summary>
        /// <remarks>
        /// The diagnostic is reported at the unsafe call site rather than at the task entry point so that
        /// a <c>#pragma warning disable MSBuildTask0005</c> — or a <c>[SuppressMessage]</c> attribute on the
        /// containing member — placed next to the reviewed call actually suppresses it. The task entry point
        /// is still named in the message and carried as an additional location.
        /// </remarks>
        private static void ReportTransitiveViolation(
            CompilationAnalysisContext context,
            IMethodSymbol taskMethod,
            ISymbol taskMethodKey,
            ISymbol violatingMethod,
            Dictionary<ISymbol, ISymbol> predecessors,
            ViolationInfo violation,
            HashSet<(string ApiDisplayName, Location Location)> reportedPerTaskType)
        {
            var taskMethodLocation = taskMethod.Locations.Length > 0 ? taskMethod.Locations[0] : Location.None;

            // Prefer the call site; fall back to the task entry point when the call site has no source location.
            bool hasCallSite = violation.Location.SourceTree is not null;
            var location = hasCallSite ? violation.Location : taskMethodLocation;

            // Deduplicate by the location the diagnostic is actually reported at, plus the target API. Keying
            // on the call site means a suppression on one reviewed call does not hide a second, unreviewed
            // call to the same API; keying on the *effective* location means the fallback above does not
            // collapse violations that land on different task members.
            if (!reportedPerTaskType.Add((violation.ApiDisplayName, location)))
            {
                return;
            }

            var chain = new List<string>();
            for (ISymbol current = violatingMethod;
                 !SymbolEqualityComparer.Default.Equals(current, taskMethodKey);
                 current = predecessors[current])
            {
                chain.Add(FormatSymbolShort(current));
            }

            chain.Add(FormatMethodShort(taskMethod));
            chain.Reverse();
            chain.Add(violation.ApiDisplayName);
            var chainStr = string.Join(" → ", chain);

            var additionalLocations = hasCallSite && taskMethodLocation.SourceTree is not null
                ? ImmutableArray.Create(taskMethodLocation)
                : ImmutableArray<Location>.Empty;

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.TransitiveUnsafeCall,
                location,
                additionalLocations,
                FormatMethodFull(taskMethod),
                violation.ApiDisplayName,
                chainStr));
        }

        private static string FormatMethodShort(IMethodSymbol method)
        {
            return $"{method.ContainingType?.Name}.{method.Name}";
        }

        private static string FormatMethodFull(IMethodSymbol method)
        {
            return $"{method.ContainingType?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}.{method.Name}";
        }

        private static string FormatSymbolShort(ISymbol symbol)
        {
            if (symbol is IMethodSymbol m)
            {
                return $"{m.ContainingType?.Name}.{m.Name}";
            }

            return symbol.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat);
        }

        internal readonly struct ViolationInfo
        {
            public string Category { get; }
            public string ApiDisplayName { get; }
            public string Message { get; }

            /// <summary>
            /// Source location of the unsafe call itself. MSBuildTask0005 is reported here so that
            /// suppressions placed next to the reviewed call are honored.
            /// </summary>
            public Location Location { get; }

            public ViolationInfo(string category, string apiDisplayName, string message, Location location)
            {
                Category = category;
                ApiDisplayName = apiDisplayName;
                Message = message;
                Location = location;
            }
        }
    }
}
