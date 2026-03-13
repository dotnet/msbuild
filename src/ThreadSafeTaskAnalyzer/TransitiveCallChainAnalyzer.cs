// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

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
        private const string ITaskFullName = "Microsoft.Build.Framework.ITask";
        private const string TaskEnvironmentFullName = "Microsoft.Build.Framework.TaskEnvironment";
        private const string AbsolutePathFullName = "Microsoft.Build.Framework.AbsolutePath";
        private const string ITaskItemFullName = "Microsoft.Build.Framework.ITaskItem";

        /// <summary>
        /// Maximum call chain depth to prevent infinite traversal in recursive call graphs.
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
            var iTaskType = compilationContext.Compilation.GetTypeByMetadataName(ITaskFullName);
            if (iTaskType is null)
            {
                return;
            }

            // Read scope option from .editorconfig
            bool analyzeAllTasks = true;
            if (compilationContext.Options.AnalyzerConfigOptionsProvider
                    .GlobalOptions.TryGetValue($"build_property.{MultiThreadableTaskAnalyzer.ScopeOptionKey}", out var scopeValue) ||
                compilationContext.Options.AnalyzerConfigOptionsProvider
                    .GlobalOptions.TryGetValue(MultiThreadableTaskAnalyzer.ScopeOptionKey, out scopeValue))
            {
                analyzeAllTasks = !string.Equals(scopeValue, MultiThreadableTaskAnalyzer.ScopeMultiThreadableOnly, StringComparison.OrdinalIgnoreCase);
            }

            var iMultiThreadableTaskType = analyzeAllTasks ? null : compilationContext.Compilation.GetTypeByMetadataName("Microsoft.Build.Framework.IMultiThreadableTask");
            var multiThreadableTaskAttributeType = analyzeAllTasks ? null : compilationContext.Compilation.GetTypeByMetadataName("Microsoft.Build.Framework.MSBuildMultiThreadableTaskAttribute");
            var analyzedAttributeType = analyzeAllTasks ? null : compilationContext.Compilation.GetTypeByMetadataName("Microsoft.Build.Framework.MSBuildMultiThreadableTaskAnalyzedAttribute");

            var taskEnvironmentType = compilationContext.Compilation.GetTypeByMetadataName(TaskEnvironmentFullName);
            var absolutePathType = compilationContext.Compilation.GetTypeByMetadataName(AbsolutePathFullName);
            var iTaskItemType = compilationContext.Compilation.GetTypeByMetadataName(ITaskItemFullName);
            var consoleType = compilationContext.Compilation.GetTypeByMetadataName("System.Console");

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
            // Phase 1.5 (IL-based cross-assembly analysis) is integrated into BFS
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
                    var violation = new ViolationInfo("CriticalError", displayName, message);
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
                            "may resolve relative paths against the process working directory");
                        directViolations.GetOrAdd(callerKey, _ => new ConcurrentBag<ViolationInfo>()).Add(violation);
                    }
                }
            }
        }

        /// <summary>
        /// Phase 2: For each task type, BFS the call graph from its methods to find transitive violations.
        /// Phase 1.5 (IL analysis) is integrated: when BFS hits an external method, we read its IL
        /// to discover call edges and violations in referenced assemblies.
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
            FindTaskTypes(context.Compilation.GlobalNamespace, iTaskType, taskTypes);

            if (taskTypes.Count == 0)
            {
                return;
            }

            // When scope is "multithreadable_only", filter to only multithreadable tasks
            if (!analyzeAllTasks)
            {
                taskTypes = taskTypes.Where(t =>
                    (iMultiThreadableTaskType is not null && t.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, iMultiThreadableTaskType))) ||
                    (multiThreadableTaskAttributeType is not null && t.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, multiThreadableTaskAttributeType))) ||
                    (analyzedAttributeType is not null && t.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, analyzedAttributeType)))
                ).ToList();

                if (taskTypes.Count == 0)
                {
                    return;
                }
            }

            // Phase 1.5: Create IL extender for cross-assembly analysis
            using var ilExtender = new ILCallGraphExtender(context.Compilation);

            foreach (var taskType in taskTypes)
            {
                foreach (var member in taskType.GetMembers())
                {
                    if (member is not IMethodSymbol method || method.IsImplicitlyDeclared)
                    {
                        continue;
                    }

                    // BFS from this method through the call graph
                    var visited = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
                    var queue = new Queue<(ISymbol current, List<string> chain)>();

                    // Seed with methods called directly from this task method
                    var methodKey = method.OriginalDefinition;
                    if (callGraph.TryGetValue(methodKey, out var directCallees))
                    {
                        // Snapshot ConcurrentBag to avoid thread-local enumeration issues
                        foreach (var callee in directCallees.ToArray())
                        {
                            if (visited.Add(callee))
                            {
                                var chain = new List<string>(4)
                                {
                                    FormatMethodShort(method),
                                    FormatSymbolShort(callee),
                                };
                                queue.Enqueue((callee, chain));
                            }
                        }
                    }

                    // Track reported violations to avoid duplicates
                    var reportedViolations = new HashSet<string>(StringComparer.Ordinal);

                    while (queue.Count > 0)
                    {
                        var (current, chain) = queue.Dequeue();

                        // Check if this method has direct violations (from source scan)
                        if (directViolations.TryGetValue(current, out var violations))
                        {
                            foreach (var v in violations)
                            {
                                ReportTransitiveViolation(context, method, v, chain, reportedViolations);
                            }
                        }

                        if (chain.Count >= MaxCallChainDepth)
                        {
                            continue;
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
                                    var newChain = new List<string>(chain) { FormatSymbolShort(callee) };
                                    queue.Enqueue((callee, newChain));
                                }
                            }
                        }

                        // Phase 1.5: If no source edges and method is external, analyze via IL
                        if (!hasSourceEdges && current is IMethodSymbol currentMethod && ILCallGraphExtender.IsExternalMethod(currentMethod))
                        {
                            // Skip BCL assemblies
                            if (ILCallGraphExtender.IsInBclAssembly(currentMethod))
                            {
                                continue;
                            }

                            // Skip methods in safe types (their internals are trusted)
                            if (ILCallGraphExtender.IsInSafeType(currentMethod))
                            {
                                continue;
                            }

                            // Read IL to discover call targets
                            var ilTargets = ilExtender.GetCallTargets(currentMethod);

                            foreach (var target in ilTargets)
                            {
                                var calleeKey = target.Method.OriginalDefinition;

                                // Check if the IL-discovered callee is itself a banned API
                                CheckILDiscoveredViolation(
                                    calleeKey, chain, bannedApiLookup, filePathTypes, consoleType,
                                    context, method, reportedViolations, directViolations);

                                // Add to BFS queue for further traversal
                                if (visited.Add(calleeKey))
                                {
                                    var newChain = new List<string>(chain) { FormatSymbolShort(calleeKey) };
                                    queue.Enqueue((calleeKey, newChain));
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Reports a transitive violation with deduplication.
        /// </summary>
        private static void ReportTransitiveViolation(
            CompilationAnalysisContext context,
            IMethodSymbol taskMethod,
            ViolationInfo violation,
            List<string> chain,
            HashSet<string> reportedViolations)
        {
            var chainWithApi = new List<string>(chain) { violation.ApiDisplayName };
            var chainStr = string.Join(" → ", chainWithApi);

            var dedupeKey = $"{violation.ApiDisplayName}|{chainStr}";
            if (!reportedViolations.Add(dedupeKey))
            {
                return;
            }

            var location = taskMethod.Locations.Length > 0 ? taskMethod.Locations[0] : Location.None;
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.TransitiveUnsafeCall,
                location,
                FormatMethodFull(taskMethod),
                violation.ApiDisplayName,
                chainStr));
        }

        /// <summary>
        /// Checks if an IL-discovered callee is a banned API and records the violation.
        /// This is needed because IL-discovered calls don't go through Phase 1 source scanning.
        /// </summary>
        private static void CheckILDiscoveredViolation(
            ISymbol calleeSymbol,
            List<string> currentChain,
            Dictionary<ISymbol, BannedApiEntry> bannedApiLookup,
            ImmutableHashSet<INamedTypeSymbol> filePathTypes,
            INamedTypeSymbol? consoleType,
            CompilationAnalysisContext context,
            IMethodSymbol taskMethod,
            HashSet<string> reportedViolations,
            ConcurrentDictionary<ISymbol, ConcurrentBag<ViolationInfo>> directViolations)
        {
            // Check if the callee itself is a banned API
            BannedApiEntry entry;
            bool found = bannedApiLookup.TryGetValue(calleeSymbol, out entry);

            // For property accessors (get_X/set_X), also check the associated property symbol
            if (!found && calleeSymbol is IMethodSymbol methodSym && methodSym.AssociatedSymbol is IPropertySymbol prop)
            {
                found = bannedApiLookup.TryGetValue(prop, out entry);
            }

            if (found)
            {
                var displayName = calleeSymbol.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat);
                var violation = new ViolationInfo(entry.Category.ToString(), displayName, entry.Message);

                // Record in directViolations so it's found when BFS reaches this node
                directViolations.GetOrAdd(calleeSymbol, _ => new ConcurrentBag<ViolationInfo>()).Add(violation);

                // Also report immediately with current chain
                var chainWithApi = new List<string>(currentChain) { displayName };
                var chainStr = string.Join(" → ", chainWithApi);
                var dedupeKey = $"{displayName}|{chainStr}";
                if (reportedViolations.Add(dedupeKey))
                {
                    var location = taskMethod.Locations.Length > 0 ? taskMethod.Locations[0] : Location.None;
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.TransitiveUnsafeCall,
                        location,
                        FormatMethodFull(taskMethod),
                        displayName,
                        chainStr));
                }
                return;
            }

            // Check Console type-level ban
            if (consoleType is not null && calleeSymbol.ContainingType is not null)
            {
                if (SymbolEqualityComparer.Default.Equals(calleeSymbol.ContainingType, consoleType))
                {
                    var displayName = calleeSymbol.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat);
                    string message = calleeSymbol.Name.StartsWith("Read", StringComparison.Ordinal)
                        ? "may cause deadlocks in automated builds"
                        : "interferes with build logging; use Log.LogMessage instead";
                    var violation = new ViolationInfo("CriticalError", displayName, message);
                    directViolations.GetOrAdd(calleeSymbol, _ => new ConcurrentBag<ViolationInfo>()).Add(violation);
                    return;
                }
            }

            // Check file-path APIs (constructors/methods on File, FileStream, etc. with path params)
            if (calleeSymbol is IMethodSymbol calledMethod &&
                calledMethod.ContainingType is not null &&
                filePathTypes.Contains(calledMethod.ContainingType))
            {
                // Check ALL overloads of this method name for path-like parameters,
                // not just the single overload that was resolved from IL (which may be wrong
                // due to overload resolution limitations — we match by name, not signature).
                bool hasPathParam = false;
                foreach (var member in calledMethod.ContainingType.GetMembers(calledMethod.Name))
                {
                    if (member is IMethodSymbol overload)
                    {
                        foreach (var param in overload.Parameters)
                        {
                            if (param.Type.SpecialType == SpecialType.System_String && IsPathParameterName(param.Name))
                            {
                                hasPathParam = true;
                                break;
                            }
                        }

                        if (hasPathParam)
                        {
                            break;
                        }
                    }
                }

                // Also check constructors if the resolved method is a constructor
                if (!hasPathParam && calledMethod.MethodKind == MethodKind.Constructor)
                {
                    foreach (var ctor in calledMethod.ContainingType.Constructors)
                    {
                        foreach (var param in ctor.Parameters)
                        {
                            if (param.Type.SpecialType == SpecialType.System_String && IsPathParameterName(param.Name))
                            {
                                hasPathParam = true;
                                break;
                            }
                        }

                        if (hasPathParam)
                        {
                            break;
                        }
                    }
                }

                if (hasPathParam)
                {
                    var displayName = calleeSymbol.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat);
                    var violation = new ViolationInfo("FilePathRequiresAbsolute", displayName,
                        "may resolve relative paths against the process working directory");
                    directViolations.GetOrAdd(calleeSymbol, _ => new ConcurrentBag<ViolationInfo>()).Add(violation);
                }
            }
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

                    // Check nested types
                    foreach (var nested in type.GetTypeMembers())
                    {
                        if (!nested.IsAbstract && ImplementsInterface(nested, iTaskType))
                        {
                            result.Add(nested);
                        }
                    }
                }
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

        #region Shared helpers (duplicated from MultiThreadableTaskAnalyzer for independence)

        private static bool HasUnwrappedPathArgument(
            ImmutableArray<IArgumentOperation> arguments,
            INamedTypeSymbol? taskEnvironmentType,
            INamedTypeSymbol? absolutePathType,
            INamedTypeSymbol? iTaskItemType)
        {
            for (int i = 0; i < arguments.Length; i++)
            {
                var arg = arguments[i];
                var param = arg.Parameter;
                if (param is null || param.Type.SpecialType != SpecialType.System_String)
                {
                    continue;
                }

                if (!IsPathParameterName(param.Name))
                {
                    continue;
                }

                if (!IsWrappedSafely(arg.Value, taskEnvironmentType, absolutePathType, iTaskItemType))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsPathParameterName(string paramName)
        {
            return paramName.IndexOf("path", StringComparison.OrdinalIgnoreCase) >= 0
                || paramName.IndexOf("file", StringComparison.OrdinalIgnoreCase) >= 0
                || paramName.IndexOf("dir", StringComparison.OrdinalIgnoreCase) >= 0
                || paramName.IndexOf("folder", StringComparison.OrdinalIgnoreCase) >= 0
                || paramName.IndexOf("source", StringComparison.OrdinalIgnoreCase) >= 0
                || paramName.IndexOf("dest", StringComparison.OrdinalIgnoreCase) >= 0;
        }

         private static bool IsWrappedSafely(
            IOperation operation,
            INamedTypeSymbol? taskEnvironmentType,
            INamedTypeSymbol? absolutePathType,
            INamedTypeSymbol? iTaskItemType)
        {
            while (operation is IConversionOperation conversion)
            {
                if (absolutePathType is not null &&
                    IsAbsolutePathType(conversion.Operand.Type, absolutePathType))
                {
                    return true;
                }

                operation = conversion.Operand;
            }

            // Null literals and default values are safe
            if (operation is ILiteralOperation lit && lit.ConstantValue.HasValue && lit.ConstantValue.Value is null)
            {
                return true;
            }

            if (operation is IDefaultValueOperation)
            {
                return true;
            }

            if (operation is IInvocationOperation invocation)
            {
                if (invocation.TargetMethod.Name == "GetAbsolutePath" &&
                    taskEnvironmentType is not null &&
                    SymbolEqualityComparer.Default.Equals(invocation.TargetMethod.ContainingType, taskEnvironmentType))
                {
                    return true;
                }

                if ((invocation.TargetMethod.Name == "GetMetadata" || invocation.TargetMethod.Name == "GetMetadataValue") && iTaskItemType is not null)
                {
                    var receiverType = invocation.TargetMethod.ContainingType;
                    if (receiverType is not null && (SymbolEqualityComparer.Default.Equals(receiverType, iTaskItemType) || ImplementsInterface(receiverType, iTaskItemType)))
                    {
                        if (invocation.Arguments.Length > 0 &&
                            invocation.Arguments[0].Value is ILiteralOperation literal &&
                            literal.ConstantValue.HasValue &&
                            literal.ConstantValue.Value is string metadataName &&
                            string.Equals(metadataName, "FullPath", StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }

                // Path.GetDirectoryName(safe) — directory of an absolute path is absolute
                if (invocation.TargetMethod.Name == "GetDirectoryName" &&
                    invocation.TargetMethod.ContainingType?.ToDisplayString() == "System.IO.Path" &&
                    invocation.Arguments.Length >= 1 &&
                    IsWrappedSafely(invocation.Arguments[0].Value, taskEnvironmentType, absolutePathType, iTaskItemType))
                {
                    return true;
                }

                // Path.Combine(safe, ...) — result is absolute when first arg is absolute
                if (invocation.TargetMethod.Name == "Combine" &&
                    invocation.TargetMethod.ContainingType?.ToDisplayString() == "System.IO.Path" &&
                    invocation.Arguments.Length >= 2 &&
                    IsWrappedSafely(invocation.Arguments[0].Value, taskEnvironmentType, absolutePathType, iTaskItemType))
                {
                    return true;
                }

                // Path.GetFullPath(safe) — safe only when input is already absolute
                if (invocation.TargetMethod.Name == "GetFullPath" &&
                    invocation.TargetMethod.ContainingType?.ToDisplayString() == "System.IO.Path" &&
                    invocation.Arguments.Length >= 1 &&
                    IsWrappedSafely(invocation.Arguments[0].Value, taskEnvironmentType, absolutePathType, iTaskItemType))
                {
                    return true;
                }
            }

            if (operation is IPropertyReferenceOperation propRef && propRef.Property.Name == "FullName")
            {
                var containingTypeName = propRef.Property.ContainingType?.ToDisplayString();
                if (containingTypeName is "System.IO.FileSystemInfo" or "System.IO.FileInfo" or "System.IO.DirectoryInfo")
                {
                    return true;
                }
            }

            if (absolutePathType is not null &&
                operation.Type is not null &&
                IsAbsolutePathType(operation.Type, absolutePathType))
            {
                return true;
            }

            // Trace through local variable assignments
            if (operation is ILocalReferenceOperation localRef)
            {
                var local = localRef.Local;
                if (local.DeclaringSyntaxReferences.Length == 1)
                {
                    var syntax = local.DeclaringSyntaxReferences[0].GetSyntax();
                    var semanticModel = operation.SemanticModel;
                    if (semanticModel is not null)
                    {
                        var declOp = semanticModel.GetOperation(syntax);
                        if (declOp is IVariableDeclaratorOperation declarator &&
                            declarator.Initializer?.Value is IOperation initValue)
                        {
                            return IsWrappedSafely(initValue, taskEnvironmentType, absolutePathType, iTaskItemType);
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if a type is AbsolutePath or Nullable&lt;AbsolutePath&gt;.
        /// </summary>
        private static bool IsAbsolutePathType(ITypeSymbol? type, INamedTypeSymbol absolutePathType)
        {
            if (type is null)
            {
                return false;
            }

            if (SymbolEqualityComparer.Default.Equals(type, absolutePathType))
            {
                return true;
            }

            if (type is INamedTypeSymbol namedType &&
                namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T &&
                namedType.TypeArguments.Length == 1 &&
                SymbolEqualityComparer.Default.Equals(namedType.TypeArguments[0], absolutePathType))
            {
                return true;
            }

            return false;
        }

        private static Dictionary<ISymbol, BannedApiEntry> BuildBannedApiLookup(Compilation compilation)
        {
            var result = new Dictionary<ISymbol, BannedApiEntry>(SymbolEqualityComparer.Default);
            var definitions = BannedApiDefinitions.GetAll();

            foreach (var def in definitions)
            {
                ImmutableArray<ISymbol> symbols;
                try
                {
                    var resolved = DocumentationCommentId.GetSymbolsForDeclarationId(def.DeclarationId, compilation);
                    symbols = resolved.IsDefault ? ImmutableArray<ISymbol>.Empty : resolved;
                }
                catch
                {
                    continue;
                }

                foreach (var symbol in symbols)
                {
                    if (!result.ContainsKey(symbol))
                    {
                        result[symbol] = new BannedApiEntry(def.Category, def.Message);
                    }
                }
            }

            return result;
        }

        private static ImmutableHashSet<INamedTypeSymbol> ResolveFilePathTypes(Compilation compilation)
        {
            var typeNames = new[]
            {
                // Core System.IO file/directory types
                "System.IO.File",
                "System.IO.Directory",
                "System.IO.FileInfo",
                "System.IO.DirectoryInfo",
                "System.IO.FileStream",
                "System.IO.StreamReader",
                "System.IO.StreamWriter",
                "System.IO.FileSystemWatcher",
                "System.IO.BinaryReader",
                "System.IO.BinaryWriter",

                // XML types that accept file paths in Load/Save/Create
                "System.Xml.Linq.XDocument",
                "System.Xml.Linq.XElement",
                "System.Xml.XmlDocument",
                "System.Xml.XmlReader",
                "System.Xml.XmlWriter",
                "System.Xml.XmlTextReader",
                "System.Xml.XmlTextWriter",
                "System.Xml.Xsl.XslCompiledTransform",
                "System.Xml.Schema.XmlSchema",

                // Compression types that accept file paths
                "System.IO.Compression.ZipFile",

                // Memory-mapped files
                "System.IO.MemoryMappedFiles.MemoryMappedFile",

                // Security / certificates
                "System.Security.Cryptography.X509Certificates.X509Certificate",
                "System.Security.Cryptography.X509Certificates.X509Certificate2",

                // Diagnostics
                "System.Diagnostics.FileVersionInfo",
                "System.Diagnostics.TextWriterTraceListener",

                // Resources
                "System.Resources.ResourceReader",
                "System.Resources.ResourceWriter",

                // Assembly loading (supplements the banned-API list for path-based overloads)
                "System.Runtime.Loader.AssemblyLoadContext",
            };

            var builder = ImmutableHashSet.CreateBuilder<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            foreach (var name in typeNames)
            {
                var type = compilation.GetTypeByMetadataName(name);
                if (type is not null)
                {
                    builder.Add(type);
                }
            }

            return builder.ToImmutable();
        }

        private static bool ImplementsInterface(INamedTypeSymbol type, INamedTypeSymbol interfaceType)
        {
            foreach (var iface in type.AllInterfaces)
            {
                if (SymbolEqualityComparer.Default.Equals(iface, interfaceType))
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        internal readonly struct BannedApiEntry
        {
            public BannedApiDefinitions.ApiCategory Category { get; }
            public string Message { get; }

            public BannedApiEntry(BannedApiDefinitions.ApiCategory category, string message)
            {
                Category = category;
                Message = message;
            }
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
