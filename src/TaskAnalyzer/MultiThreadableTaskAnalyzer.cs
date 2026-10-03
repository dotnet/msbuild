// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

using static Microsoft.Build.TaskAuthoring.Analyzer.SharedAnalyzerHelpers;

namespace Microsoft.Build.TaskAuthoring.Analyzer
{
    /// <summary>
    /// Roslyn analyzer that detects unsafe API usage in MSBuild task implementations.
    /// 
    /// By default, MSBuildTask0002 and MSBuildTask0003 apply only to MT-scoped code: a type carrying
    /// [MSBuildMultiThreadableTask] or [MSBuildMultiThreadableTaskAnalyzed], or a task that declares
    /// IMultiThreadableTask in its own base list. The declared interface signals analyzer migration intent
    /// only -- runtime routing still depends on [MSBuildMultiThreadableTask].
    /// The "msbuild_task_analyzer.run_mt_analyzers_on_all_tasks" option enables these rules for all tasks.
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
            var multiThreadableTaskType = compilationContext.Compilation.GetTypeByMetadataName(WellKnownTypeNames.IMultiThreadableTaskFullName);
            var contributingMultiThreadableTaskBaseTypes = FindContributingMultiThreadableTaskBaseTypes(
                compilationContext.Compilation,
                iTaskType,
                multiThreadableTaskType);

            // Build symbol lookup for banned APIs
            var bannedApiLookup = BuildBannedApiLookup(compilationContext.Compilation);

            // Build set of file-path types for MSBuildTask0003
            var filePathTypes = ResolveFilePathTypes(compilationContext.Compilation);
            var analyzeAllTasksByTree = new ConcurrentDictionary<SyntaxTree, bool>();
            var possibleMethodReferences = BuildPossibleMethodReferences(
                compilationContext.Compilation,
                compilationContext.CancellationToken);

            // Use RegisterSymbolStartAction for efficient per-type scoping
            compilationContext.RegisterSymbolStartAction(symbolStartContext =>
            {
                var namedType = (INamedTypeSymbol)symbolStartContext.Symbol;

                if (!IsDirectlyAnalyzedType(
                    namedType,
                    iTaskType,
                    multiThreadableTaskType,
                    contributingMultiThreadableTaskBaseTypes,
                    out bool analyzeAsMultiThreadable))
                {
                    return;
                }

                var pathParameterCallSafety = new ConcurrentDictionary<IParameterSymbol, bool>(SymbolEqualityComparer.Default);
                var deferredPathDiagnostics = new ConcurrentBag<DeferredPathDiagnostic>();

                symbolStartContext.RegisterOperationAction(
                    context => CollectPathParameterUsageSafety(
                        context,
                        taskEnvironmentType,
                        absolutePathType,
                        iTaskItemType,
                        pathParameterCallSafety,
                        possibleMethodReferences),
                    OperationKind.Invocation,
                    OperationKind.MethodReference);

                // Register operation-level analysis within this type
                symbolStartContext.RegisterOperationAction(
                    ctx => AnalyzeOperation(ctx, bannedApiLookup, filePathTypes, analyzeAsMultiThreadable, analyzeAllTasksByTree,
                        taskEnvironmentType, absolutePathType, iTaskItemType, consoleType, possibleMethodReferences, deferredPathDiagnostics),
                    OperationKind.Invocation,
                    OperationKind.ObjectCreation,
                    OperationKind.PropertyReference,
                    OperationKind.FieldReference,
                    OperationKind.MethodReference,
                    OperationKind.EventReference);

                symbolStartContext.RegisterSymbolEndAction(context =>
                {
                    foreach (DeferredPathDiagnostic deferredDiagnostic in deferredPathDiagnostics)
                    {
                        bool allCallsSafe = true;
                        foreach (IParameterSymbol parameter in deferredDiagnostic.Parameters)
                        {
                            if (!pathParameterCallSafety.TryGetValue(parameter, out bool parameterCallsSafe) ||
                                !parameterCallsSafe)
                            {
                                allCallsSafe = false;
                                break;
                            }
                        }

                        if (!allCallsSafe)
                        {
                            context.ReportDiagnostic(deferredDiagnostic.Diagnostic);
                        }
                    }
                });
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
            INamedTypeSymbol? consoleType,
            Dictionary<string, ImmutableArray<SimpleNameSyntax>> possibleMethodReferences,
            ConcurrentBag<DeferredPathDiagnostic> deferredPathDiagnostics)
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

                // MSBuildTask0002 (TaskEnvironment) is gated by scope setting
                if (entry.Category == BannedApiDefinitions.ApiCategory.TaskEnvironment &&
                    !ShouldReportEnvironmentRules(context, analyzeAsMultiThreadable, analyzeAllTasksByTree))
                {
                    return;
                }

                var descriptor = GetDescriptor(entry.Category);
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

            // MSBuildTask0003 is limited to MT-scoped code unless migration analysis is enabled.
            if (!arguments.IsDefaultOrEmpty && referencedSymbol is IMethodSymbol method)
            {
                var containingType = method.ContainingType;
                if (containingType is not null &&
                    filePathTypes.Contains(containingType) &&
                    ShouldReportEnvironmentRules(context, analyzeAsMultiThreadable, analyzeAllTasksByTree) &&
                    HasUnwrappedPathArgument(arguments, taskEnvironmentType, absolutePathType, iTaskItemType))
                {
                    string displayName = isConstructor
                        ? $"new {containingType.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat)}(...)"
                        : referencedSymbol.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat);

                    string hint = "wrap path argument with TaskEnvironment.GetAbsolutePath()";
                    Diagnostic diagnostic = Diagnostic.Create(
                        DiagnosticDescriptors.FilePathRequiresAbsolute,
                        context.Operation.Syntax.GetLocation(),
                        displayName, hint);

                    if (TryGetDeferredPathParameters(
                        arguments,
                        taskEnvironmentType,
                        absolutePathType,
                        iTaskItemType,
                        possibleMethodReferences,
                        out ImmutableArray<IParameterSymbol> parameters))
                    {
                        deferredPathDiagnostics.Add(new DeferredPathDiagnostic(diagnostic, parameters));
                    }
                    else
                    {
                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }

        private static void CollectPathParameterUsageSafety(
            OperationAnalysisContext context,
            INamedTypeSymbol? taskEnvironmentType,
            INamedTypeSymbol? absolutePathType,
            INamedTypeSymbol? iTaskItemType,
            ConcurrentDictionary<IParameterSymbol, bool> pathParameterCallSafety,
            Dictionary<string, ImmutableArray<SimpleNameSyntax>> possibleMethodReferences)
        {
            if (context.Operation is IMethodReferenceOperation methodReference)
            {
                foreach (IParameterSymbol parameter in methodReference.Method.Parameters)
                {
                    if (parameter.Type.SpecialType == SpecialType.System_String &&
                        IsPathParameterName(parameter.Name) &&
                        IsEligiblePathHelperParameter(
                            parameter,
                            possibleMethodReferences))
                    {
                        pathParameterCallSafety[parameter.OriginalDefinition] = false;
                    }
                }

                return;
            }

            var invocation = (IInvocationOperation)context.Operation;
            if (invocation.TargetMethod.DeclaringSyntaxReferences.Length == 0)
            {
                return;
            }

            foreach (IArgumentOperation argument in invocation.Arguments)
            {
                IParameterSymbol? parameter = argument.Parameter;
                if (parameter is null ||
                    parameter.Type.SpecialType != SpecialType.System_String ||
                    !IsPathParameterName(parameter.Name) ||
                    !IsEligiblePathHelperParameter(
                        parameter,
                        possibleMethodReferences))
                {
                    continue;
                }

                bool isSafe = IsWrappedSafely(
                    argument.Value,
                    taskEnvironmentType,
                    absolutePathType,
                    iTaskItemType);

                IParameterSymbol parameterKey = parameter.OriginalDefinition;
                if (isSafe)
                {
                    pathParameterCallSafety.TryAdd(parameterKey, true);
                }
                else
                {
                    pathParameterCallSafety[parameterKey] = false;
                }
            }
        }

        private static bool TryGetDeferredPathParameters(
            ImmutableArray<IArgumentOperation> arguments,
            INamedTypeSymbol? taskEnvironmentType,
            INamedTypeSymbol? absolutePathType,
            INamedTypeSymbol? iTaskItemType,
            Dictionary<string, ImmutableArray<SimpleNameSyntax>> possibleMethodReferences,
            out ImmutableArray<IParameterSymbol> parameters)
        {
            var builder = ImmutableArray.CreateBuilder<IParameterSymbol>();
            for (int i = 0; i < arguments.Length; i++)
            {
                IArgumentOperation argument = arguments[i];
                IParameterSymbol? targetParameter = argument.Parameter;
                if (targetParameter is null ||
                    targetParameter.Type.SpecialType != SpecialType.System_String ||
                    !IsPathParameterName(targetParameter.Name) ||
                    IsWrappedSafely(argument.Value, taskEnvironmentType, absolutePathType, iTaskItemType))
                {
                    continue;
                }

                IOperation value = argument.Value;
                while (value is IConversionOperation conversion)
                {
                    value = conversion.Operand;
                }

                if (value is IParameterReferenceOperation parameterReference &&
                    IsEligiblePathHelperParameter(
                        parameterReference.Parameter,
                        possibleMethodReferences))
                {
                    builder.Add(parameterReference.Parameter.OriginalDefinition);
                    continue;
                }

                parameters = default;
                return false;
            }

            parameters = builder.ToImmutable();
            return parameters.Length > 0;
        }

        private static bool IsEligiblePathHelperParameter(
            IParameterSymbol parameter,
            Dictionary<string, ImmutableArray<SimpleNameSyntax>> possibleMethodReferences)
        {
            IMethodSymbol method = (IMethodSymbol)parameter.ContainingSymbol;
            if (!method.IsStatic ||
                method.IsVirtual ||
                method.IsOverride)
            {
                return false;
            }

            if (method.DeclaringSyntaxReferences.Length == 0)
            {
                return false;
            }

            if (method.DeclaredAccessibility is not (Accessibility.Private or Accessibility.Internal or Accessibility.ProtectedAndInternal))
            {
                return false;
            }

            if (!possibleMethodReferences.TryGetValue(method.Name, out ImmutableArray<SimpleNameSyntax> references))
            {
                return true;
            }

            foreach (SimpleNameSyntax reference in references)
            {
                if (!IsWithinContainingType(reference, method.ContainingType))
                {
                    return false;
                }
            }

            return true;
        }

        private static Dictionary<string, ImmutableArray<SimpleNameSyntax>> BuildPossibleMethodReferences(
            Compilation compilation,
            System.Threading.CancellationToken cancellationToken)
        {
            var builders = new Dictionary<string, ImmutableArray<SimpleNameSyntax>.Builder>(StringComparer.Ordinal);
            foreach (SyntaxTree syntaxTree in compilation.SyntaxTrees)
            {
                SyntaxNode root = syntaxTree.GetRoot(cancellationToken);
                foreach (SyntaxNode node in root.DescendantNodes())
                {
                    if (node is not SimpleNameSyntax simpleName)
                    {
                        continue;
                    }

                    string name = simpleName.Identifier.ValueText;
                    if (!builders.TryGetValue(name, out ImmutableArray<SimpleNameSyntax>.Builder? builder))
                    {
                        builder = ImmutableArray.CreateBuilder<SimpleNameSyntax>();
                        builders.Add(name, builder);
                    }

                    builder.Add(simpleName);
                }
            }

            var references = new Dictionary<string, ImmutableArray<SimpleNameSyntax>>(builders.Count, StringComparer.Ordinal);
            foreach (KeyValuePair<string, ImmutableArray<SimpleNameSyntax>.Builder> pair in builders)
            {
                references.Add(pair.Key, pair.Value.ToImmutable());
            }

            return references;
        }

        private static bool IsWithinContainingType(SyntaxNode node, INamedTypeSymbol containingType)
        {
            BaseTypeDeclarationSyntax? nearestType = null;
            for (SyntaxNode? ancestor = node.Parent; ancestor is not null; ancestor = ancestor.Parent)
            {
                if (ancestor is BaseTypeDeclarationSyntax typeDeclaration)
                {
                    nearestType = typeDeclaration;
                    break;
                }
            }

            if (nearestType is null)
            {
                return false;
            }

            if (node.Parent is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Name == node &&
                memberAccess.Expression is not ThisExpressionSyntax)
            {
                if (memberAccess.Expression is not IdentifierNameSyntax qualifier ||
                    qualifier.Identifier.ValueText != containingType.Name)
                {
                    return false;
                }
            }

            foreach (SyntaxReference declaration in containingType.DeclaringSyntaxReferences)
            {
                if (declaration.SyntaxTree == nearestType.SyntaxTree &&
                    declaration.Span == nearestType.Span)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ShouldReportEnvironmentRules(
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

        private readonly struct DeferredPathDiagnostic
        {
            internal DeferredPathDiagnostic(Diagnostic diagnostic, ImmutableArray<IParameterSymbol> parameters)
            {
                Diagnostic = diagnostic;
                Parameters = parameters;
            }

            internal Diagnostic Diagnostic { get; }

            internal ImmutableArray<IParameterSymbol> Parameters { get; }
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
