// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

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
                    ctx => AnalyzeOperation(ctx, bannedApiLookup, filePathTypes, reportEnvironmentRules,
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
                                DiagnosticDescriptors.FilePathRequiresAbsolute,
                                context.Operation.Syntax.GetLocation(),
                                displayName, hint));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Checks whether ANY path-typed string parameter of a file API call is NOT wrapped with a safe pattern.
        /// Only checks parameters whose names suggest they are file paths (e.g., "path", "fileName", "sourceFileName").
        /// Non-path string parameters (e.g., "contents", "searchPattern") are skipped.
        /// </summary>
        private static bool HasUnwrappedPathArgument(
            ImmutableArray<IArgumentOperation> arguments,
            INamedTypeSymbol? taskEnvironmentType,
            INamedTypeSymbol? absolutePathType,
            INamedTypeSymbol? iTaskItemType)
        {
            // Check each argument, using its associated parameter to determine if it's a path
            for (int i = 0; i < arguments.Length; i++)
            {
                var arg = arguments[i];
                var param = arg.Parameter;
                if (param is null || param.Type.SpecialType != SpecialType.System_String)
                {
                    continue;
                }

                // Only check parameters that are likely paths based on their name
                if (!IsPathParameterName(param.Name))
                {
                    continue;
                }

                var argValue = arg.Value;

                if (!IsWrappedSafely(argValue, taskEnvironmentType, absolutePathType, iTaskItemType))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines if a parameter name suggests it represents a file system path.
        /// </summary>
        private static bool IsPathParameterName(string paramName)
        {
            // Common path parameter names in System.IO, System.Xml, and other BCL APIs
            return paramName.IndexOf("path", StringComparison.OrdinalIgnoreCase) >= 0
                || paramName.IndexOf("file", StringComparison.OrdinalIgnoreCase) >= 0
                || paramName.IndexOf("dir", StringComparison.OrdinalIgnoreCase) >= 0
                || paramName.IndexOf("folder", StringComparison.OrdinalIgnoreCase) >= 0
                || paramName.IndexOf("source", StringComparison.OrdinalIgnoreCase) >= 0
                || paramName.IndexOf("dest", StringComparison.OrdinalIgnoreCase) >= 0
                || paramName.IndexOf("uri", StringComparison.OrdinalIgnoreCase) >= 0
                || paramName.IndexOf("url", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Recursively checks if an operation represents a safely-wrapped path.
        /// </summary>
        private static bool IsWrappedSafely(
            IOperation operation,
            INamedTypeSymbol? taskEnvironmentType,
            INamedTypeSymbol? absolutePathType,
            INamedTypeSymbol? iTaskItemType)
        {
            // Unwrap conversions (implicit AbsolutePath -> string, etc.)
            while (operation is IConversionOperation conversion)
            {
                // Check if converting from AbsolutePath or Nullable<AbsolutePath>
                if (absolutePathType is not null &&
                    IsAbsolutePathType(conversion.Operand.Type, absolutePathType))
                {
                    return true;
                }

                operation = conversion.Operand;
            }

            // Null literals and default values are safe — they don't represent relative paths
            if (operation is ILiteralOperation lit && lit.ConstantValue.HasValue && lit.ConstantValue.Value is null)
            {
                return true;
            }

            if (operation is IDefaultValueOperation)
            {
                return true;
            }

            // Check: TaskEnvironment.GetAbsolutePath(...)
            if (operation is IInvocationOperation invocation)
            {
                if (invocation.TargetMethod.Name == "GetAbsolutePath" &&
                    taskEnvironmentType is not null &&
                    SymbolEqualityComparer.Default.Equals(invocation.TargetMethod.ContainingType, taskEnvironmentType))
                {
                    return true;
                }

                // Check: ITaskItem.GetMetadata("FullPath") / GetMetadataValue("FullPath")
                if ((invocation.TargetMethod.Name == "GetMetadata" || invocation.TargetMethod.Name == "GetMetadataValue") && iTaskItemType is not null)
                {
                    var receiverType = invocation.TargetMethod.ContainingType;
                    if (receiverType is not null && (SymbolEqualityComparer.Default.Equals(receiverType, iTaskItemType) || ImplementsInterface(receiverType, iTaskItemType)))
                    {
                        // Check if the argument is the literal string "FullPath"
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

                // Check: Path.GetDirectoryName(safe) — directory of an absolute path is absolute
                if (invocation.TargetMethod.Name == "GetDirectoryName" &&
                    invocation.TargetMethod.ContainingType?.ToDisplayString() == "System.IO.Path" &&
                    invocation.Arguments.Length >= 1 &&
                    IsWrappedSafely(invocation.Arguments[0].Value, taskEnvironmentType, absolutePathType, iTaskItemType))
                {
                    return true;
                }

                // Check: Path.Combine(safe, ...) — result is absolute when first arg is absolute
                if (invocation.TargetMethod.Name == "Combine" &&
                    invocation.TargetMethod.ContainingType?.ToDisplayString() == "System.IO.Path" &&
                    invocation.Arguments.Length >= 2 &&
                    IsWrappedSafely(invocation.Arguments[0].Value, taskEnvironmentType, absolutePathType, iTaskItemType))
                {
                    return true;
                }

                // Check: Path.GetFullPath(safe) — safe only when input is already absolute.
                // If input is relative, GetFullPath resolves against CWD (wrong in MT).
                // The GetFullPath call itself is still flagged by MSBuildTask0002 regardless.
                if (invocation.TargetMethod.Name == "GetFullPath" &&
                    invocation.TargetMethod.ContainingType?.ToDisplayString() == "System.IO.Path" &&
                    invocation.Arguments.Length >= 1 &&
                    IsWrappedSafely(invocation.Arguments[0].Value, taskEnvironmentType, absolutePathType, iTaskItemType))
                {
                    return true;
                }
            }

            // Check: .FullName property (FileSystemInfo.FullName - base class of FileInfo/DirectoryInfo)
            if (operation is IPropertyReferenceOperation propRef)
            {
                if (propRef.Property.Name == "FullName")
                {
                    var containingTypeName = propRef.Property.ContainingType?.ToDisplayString();
                    // FullName is declared on FileSystemInfo (base of FileInfo, DirectoryInfo)
                    if (containingTypeName is "System.IO.FileSystemInfo" or "System.IO.FileInfo" or "System.IO.DirectoryInfo")
                    {
                        return true;
                    }
                }
            }

            // Check: argument type is AbsolutePath or Nullable<AbsolutePath>
            if (absolutePathType is not null &&
                operation.Type is not null &&
                IsAbsolutePathType(operation.Type, absolutePathType))
            {
                return true;
            }

            // Trace through local variable assignments: if `string dir = Path.GetDirectoryName(abs);`
            // then `dir` is safe because its initializer is safe.
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

            // Direct match: AbsolutePath
            if (SymbolEqualityComparer.Default.Equals(type, absolutePathType))
            {
                return true;
            }

            // Nullable<AbsolutePath> — the type is Nullable<T> where T is AbsolutePath
            if (type is INamedTypeSymbol namedType &&
                namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T &&
                namedType.TypeArguments.Length == 1 &&
                SymbolEqualityComparer.Default.Equals(namedType.TypeArguments[0], absolutePathType))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Builds a dictionary mapping resolved ISymbols to their banned API entry for O(1) lookup.
        /// </summary>
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

        /// <summary>
        /// Resolves the set of types whose methods take path parameters that need absolutization.
        /// </summary>
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

        private static DiagnosticDescriptor GetDescriptor(BannedApiDefinitions.ApiCategory category)
        {
            return category switch
            {
                BannedApiDefinitions.ApiCategory.CriticalError => DiagnosticDescriptors.CriticalError,
                BannedApiDefinitions.ApiCategory.TaskEnvironment => DiagnosticDescriptors.TaskEnvironmentRequired,
                BannedApiDefinitions.ApiCategory.PotentialIssue => DiagnosticDescriptors.PotentialIssue,
                _ => DiagnosticDescriptors.TaskEnvironmentRequired,
            };
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

        private readonly struct BannedApiEntry
        {
            public BannedApiDefinitions.ApiCategory Category { get; }
            public string Message { get; }

            public BannedApiEntry(BannedApiDefinitions.ApiCategory category, string message)
            {
                Category = category;
                Message = message;
            }
        }
    }
}
