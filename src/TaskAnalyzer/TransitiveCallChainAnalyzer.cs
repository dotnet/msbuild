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
    /// Reports MSBuildTask0005 with the full call chain for traceability.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class TransitiveCallChainAnalyzer : DiagnosticAnalyzer
    {
        /// <summary>
        /// Maximum BFS depth. The visited set already prevents cycles, but this limits
        /// exploration of very deep non-cyclic call chains for performance.
        /// </summary>
        private const int MaxCallChainDepth = 20;

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

            // Thread-safe collections for building the graph across concurrent operation callbacks
            var callGraph = new ConcurrentDictionary<ISymbol, ConcurrentBag<ISymbol>>(SymbolEqualityComparer.Default);
            var directViolations = new ConcurrentDictionary<ISymbol, ConcurrentBag<ViolationInfo>>(SymbolEqualityComparer.Default);

            // Phase 1: Scan ALL operations in the compilation to build call graph + record violations
            compilationContext.RegisterOperationAction(opCtx =>
            {
                ScanOperation(opCtx, callGraph, directViolations, bannedApiLookup, filePathTypes,
                    taskEnvironmentType, absolutePathType, iTaskItemType, consoleType, iTaskType);
            },
            OperationKind.Invocation,
            OperationKind.ObjectCreation,
            OperationKind.PropertyReference,
            OperationKind.FieldReference);

            // Phase 2: At compilation end, compute transitive closure from task methods
            compilationContext.RegisterCompilationEndAction(endCtx =>
            {
                AnalyzeTransitiveViolations(endCtx, callGraph, directViolations, iTaskType,
                    bannedApiLookup, filePathTypes, taskEnvironmentType, absolutePathType, iTaskItemType, consoleType,
                    analyzeAllTasks, iMultiThreadableTaskType, multiThreadableTaskAttributeType, analyzedAttributeType);
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
            INamedTypeSymbol iTaskType)
        {
            var containingSymbol = context.ContainingSymbol;
            if (containingSymbol is not IMethodSymbol containingMethod)
            {
                return;
            }

            // Normalize to OriginalDefinition for generic methods
            var callerKey = containingMethod.OriginalDefinition;

            // Check if this method is inside a task type
            var containingType = containingMethod.ContainingType;
            bool isInsideTask = containingType is not null && ImplementsInterface(containingType, iTaskType);

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
                callGraph.GetOrAdd(callerKey, static _ => new ConcurrentBag<ISymbol>()).Add(calleeKey);
            }
            else if (referencedSymbol is IPropertySymbol property)
            {
                // Record edges to property getter and setter methods
                if (property.GetMethod is not null)
                {
                    callGraph.GetOrAdd(callerKey, static _ => new ConcurrentBag<ISymbol>()).Add(property.GetMethod.OriginalDefinition);
                }

                if (property.SetMethod is not null)
                {
                    callGraph.GetOrAdd(callerKey, static _ => new ConcurrentBag<ISymbol>()).Add(property.SetMethod.OriginalDefinition);
                }
            }

            // Only record violations for NON-task methods
            // Task methods get direct analysis from MultiThreadableTaskAnalyzer
            if (isInsideTask)
            {
                return;
            }

            // Check if this is a banned API call → record as a direct violation
            if (bannedApiLookup.TryGetValue(referencedSymbol, out var entry))
            {
                var displayName = referencedSymbol.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat);
                var violation = new ViolationInfo(entry.Category.ToString(), displayName, entry.Message);
                directViolations.GetOrAdd(callerKey, static _ => new ConcurrentBag<ViolationInfo>()).Add(violation);
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
                    var violation = new ViolationInfo("CriticalError", displayName, message);
                    directViolations.GetOrAdd(callerKey, static _ => new ConcurrentBag<ViolationInfo>()).Add(violation);
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
                            "may resolve relative paths against the process working directory");
                        directViolations.GetOrAdd(callerKey, static _ => new ConcurrentBag<ViolationInfo>()).Add(violation);
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
            INamedTypeSymbol iTaskType,
            Dictionary<ISymbol, BannedApiEntry> bannedApiLookup,
            ImmutableHashSet<INamedTypeSymbol> filePathTypes,
            INamedTypeSymbol? taskEnvironmentType,
            INamedTypeSymbol? absolutePathType,
            INamedTypeSymbol? iTaskItemType,
            INamedTypeSymbol? consoleType,
            bool analyzeAllTasks,
            INamedTypeSymbol? iMultiThreadableTaskType,
            INamedTypeSymbol? multiThreadableTaskAttributeType,
            INamedTypeSymbol? analyzedAttributeType)
        {
            // Find all task types in the compilation
            var taskTypes = new List<INamedTypeSymbol>();
            FindTaskTypes(context.Compilation.Assembly.GlobalNamespace, iTaskType, taskTypes);

            if (taskTypes.Count == 0)
            {
                return;
            }

            // When scope is "multithreadable_only", filter to only multithreadable tasks
            if (!analyzeAllTasks)
            {
                int destination = 0;
                for (int i = 0; i < taskTypes.Count; i++)
                {
                    INamedTypeSymbol taskType = taskTypes[i];
                    if (IsAnalyzedTaskType(
                        taskType,
                        iMultiThreadableTaskType,
                        multiThreadableTaskAttributeType,
                        analyzedAttributeType))
                    {
                        taskTypes[destination++] = taskType;
                    }
                }

                if (destination < taskTypes.Count)
                {
                    taskTypes.RemoveRange(destination, taskTypes.Count - destination);
                }

                if (taskTypes.Count == 0)
                {
                    return;
                }
            }

            foreach (var taskType in taskTypes)
            {
                // Track reported violations per task type to avoid flooding with duplicates.
                // Key: target banned API display name. We report only the shortest chain per API.
                var reportedPerTaskType = new HashSet<string>(StringComparer.Ordinal);
                var visited = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
                var parentBySymbol = new Dictionary<ISymbol, ISymbol>(SymbolEqualityComparer.Default);
                var queue = new Queue<(ISymbol Current, int Depth)>();

                foreach (var member in taskType.GetMembers())
                {
                    if (member is not IMethodSymbol method || method.IsImplicitlyDeclared)
                    {
                        continue;
                    }

                    // BFS from this method through the call graph
                    visited.Clear();
                    parentBySymbol.Clear();
                    queue.Clear();

                    // Seed with methods called directly from this task method
                    var methodKey = method.OriginalDefinition;
                    if (callGraph.TryGetValue(methodKey, out var directCallees))
                    {
                        EnqueueCallees(directCallees, methodKey, depth: 2, visited, parentBySymbol, queue);
                    }

                    while (queue.Count > 0)
                    {
                        var (current, depth) = queue.Dequeue();

                        // Check if this method has direct violations (from source scan)
                        if (directViolations.TryGetValue(current, out var violations))
                        {
                            foreach (ViolationInfo violation in violations)
                            {
                                ReportTransitiveViolation(
                                    context,
                                    method,
                                    current,
                                    violation,
                                    parentBySymbol,
                                    reportedPerTaskType);
                            }
                        }

                        if (depth >= MaxCallChainDepth)
                        {
                            continue;
                        }

                        if (callGraph.TryGetValue(current, out var callees))
                        {
                            EnqueueCallees(callees, current, depth + 1, visited, parentBySymbol, queue);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Reports a transitive violation with deduplication per task type.
        /// Only the first (shortest) chain reaching each banned API is reported.
        /// </summary>
        private static void ReportTransitiveViolation(
            CompilationAnalysisContext context,
            IMethodSymbol taskMethod,
            ISymbol violatingSymbol,
            ViolationInfo violation,
            Dictionary<ISymbol, ISymbol> parentBySymbol,
            HashSet<string> reportedPerTaskType)
        {
            // Deduplicate by target API — report each banned API only once per task type
            if (!reportedPerTaskType.Add(violation.ApiDisplayName))
            {
                return;
            }

            var chain = new List<ISymbol>();
            ISymbol current = violatingSymbol;
            while (!SymbolEqualityComparer.Default.Equals(current, taskMethod.OriginalDefinition))
            {
                chain.Add(current);
                current = parentBySymbol[current];
            }

            var chainBuilder = new System.Text.StringBuilder();
            chainBuilder.Append(FormatMethodShort(taskMethod));
            for (int i = chain.Count - 1; i >= 0; i--)
            {
                chainBuilder.Append(" → ");
                chainBuilder.Append(FormatSymbolShort(chain[i]));
            }

            chainBuilder.Append(" → ");
            chainBuilder.Append(violation.ApiDisplayName);

            var location = taskMethod.Locations.Length > 0 ? taskMethod.Locations[0] : Location.None;
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.TransitiveUnsafeCall,
                location,
                FormatMethodFull(taskMethod),
                violation.ApiDisplayName,
                chainBuilder.ToString()));
        }

        private static void EnqueueCallees(
            ConcurrentBag<ISymbol> callees,
            ISymbol parent,
            int depth,
            HashSet<ISymbol> visited,
            Dictionary<ISymbol, ISymbol> parentBySymbol,
            Queue<(ISymbol Current, int Depth)> queue)
        {
            foreach (ISymbol callee in callees)
            {
                Enqueue(callee, parent, depth, visited, parentBySymbol, queue);
            }
        }

        private static void Enqueue(
            ISymbol callee,
            ISymbol parent,
            int depth,
            HashSet<ISymbol> visited,
            Dictionary<ISymbol, ISymbol> parentBySymbol,
            Queue<(ISymbol Current, int Depth)> queue)
        {
            if (visited.Add(callee))
            {
                parentBySymbol.Add(callee, parent);
                queue.Enqueue((callee, depth));
            }
        }

        private static bool IsAnalyzedTaskType(
            INamedTypeSymbol taskType,
            INamedTypeSymbol? iMultiThreadableTaskType,
            INamedTypeSymbol? multiThreadableTaskAttributeType,
            INamedTypeSymbol? analyzedAttributeType)
        {
            if (iMultiThreadableTaskType is not null && ImplementsInterface(taskType, iMultiThreadableTaskType))
            {
                return true;
            }

            foreach (AttributeData attribute in taskType.GetAttributes())
            {
                if ((multiThreadableTaskAttributeType is not null &&
                     SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, multiThreadableTaskAttributeType)) ||
                    (analyzedAttributeType is not null &&
                     SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, analyzedAttributeType)))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Recursively finds all types implementing ITask in the namespace tree.
        /// </summary>
        private static void FindTaskTypes(INamespaceSymbol ns, INamedTypeSymbol iTaskType, List<INamedTypeSymbol> result)
        {
            foreach (var member in ns.GetMembers())
            {
                if (member is INamespaceSymbol childNs)
                {
                    FindTaskTypes(childNs, iTaskType, result);
                }
                else if (member is INamedTypeSymbol type)
                {
                    if (!type.IsAbstract && ImplementsInterface(type, iTaskType))
                    {
                        result.Add(type);
                    }

                    FindNestedTaskTypes(type, iTaskType, result);
                }
            }
        }

        /// <summary>
        /// Recursively discovers task types in arbitrarily nested type hierarchies.
        /// </summary>
        private static void FindNestedTaskTypes(INamedTypeSymbol parentType, INamedTypeSymbol iTaskType, List<INamedTypeSymbol> result)
        {
            foreach (var nested in parentType.GetTypeMembers())
            {
                if (!nested.IsAbstract && ImplementsInterface(nested, iTaskType))
                {
                    result.Add(nested);
                }

                FindNestedTaskTypes(nested, iTaskType, result);
            }
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

            public ViolationInfo(string category, string apiDisplayName, string message)
            {
                Category = category;
                ApiDisplayName = apiDisplayName;
                Message = message;
            }
        }
    }
}
