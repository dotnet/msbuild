// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.Build.TaskAuthoring.Analyzer
{
    /// <summary>
    /// Shared helpers used by both <see cref="MultiThreadableTaskAnalyzer"/> and
    /// <see cref="TransitiveCallChainAnalyzer"/> for path safety analysis and banned API resolution.
    /// </summary>
    internal static class SharedAnalyzerHelpers
    {
        /// <summary>
        /// The .editorconfig key controlling analysis scope.
        /// Values: "all" (default) | "multithreadable_only"
        /// </summary>
        internal const string ScopeOptionKey = "msbuild_task_analyzer.scope";
        internal const string ScopeAll = "all";
        internal const string ScopeMultiThreadableOnly = "multithreadable_only";

        /// <summary>
        /// Reads the scope option from the analyzer config options provider.
        /// Returns true if all tasks should be analyzed; false if only multithreadable tasks.
        /// </summary>
        internal static bool ReadAnalyzeAllTasksOption(AnalyzerConfigOptionsProvider optionsProvider)
        {
            if (optionsProvider.GlobalOptions.TryGetValue($"build_property.{ScopeOptionKey}", out var scopeValue) ||
                optionsProvider.GlobalOptions.TryGetValue(ScopeOptionKey, out scopeValue))
            {
                return !string.Equals(scopeValue, ScopeMultiThreadableOnly, StringComparison.OrdinalIgnoreCase);
            }

            return true; // default: analyze all tasks
        }
        /// <summary>
        /// Represents a resolved banned API entry for O(1) lookup during analysis.
        /// </summary>
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

        /// <summary>
        /// Determines if a parameter name suggests it represents a file system path.
        /// Excludes XML namespace parameters (namespaceURI, etc.) and non-path names.
        /// </summary>
        internal static bool IsPathParameterName(string paramName)
        {
            // Exclude known non-path parameter names that contain path-like substrings
            if (paramName.IndexOf("namespace", StringComparison.OrdinalIgnoreCase) >= 0
                || paramName.IndexOf("xpath", StringComparison.OrdinalIgnoreCase) >= 0
                || paramName.IndexOf("profile", StringComparison.OrdinalIgnoreCase) >= 0
                || string.Equals(paramName, "source", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Common path parameter names in System.IO and other BCL APIs
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
        /// Checks whether ANY path-typed string parameter of a file API call is NOT wrapped with a safe pattern.
        /// Only checks parameters whose names suggest they are file paths (e.g., "path", "fileName", "sourceFileName").
        /// Non-path string parameters (e.g., "contents", "searchPattern") are skipped.
        /// </summary>
        internal static bool HasUnwrappedPathArgument(
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

        /// <summary>
        /// Recursively checks if an operation represents a safely-wrapped path.
        /// </summary>
        internal static bool IsWrappedSafely(
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

            // Check: TaskEnvironment.GetAbsolutePath(...), ITaskItem.GetMetadata("FullPath"), Path.* helpers
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
            if (operation is IPropertyReferenceOperation propRef && propRef.Property.Name == "FullName")
            {
                var containingTypeName = propRef.Property.ContainingType?.ToDisplayString();
                if (containingTypeName is "System.IO.FileSystemInfo" or "System.IO.FileInfo" or "System.IO.DirectoryInfo")
                {
                    return true;
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
        internal static bool IsAbsolutePathType(ITypeSymbol? type, INamedTypeSymbol absolutePathType)
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
        internal static Dictionary<ISymbol, BannedApiEntry> BuildBannedApiLookup(Compilation compilation)
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
        internal static ImmutableHashSet<INamedTypeSymbol> ResolveFilePathTypes(Compilation compilation)
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

                // XML types — only include types where the majority of string
                // parameters are file paths. XmlDocument is excluded because methods
                // like CreateElement, CreateAttribute etc. take non-path strings.
                "System.Xml.Linq.XDocument",
                "System.Xml.Linq.XElement",
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

        /// <summary>
        /// Checks if a type implements a given interface.
        /// </summary>
        internal static bool ImplementsInterface(INamedTypeSymbol type, INamedTypeSymbol interfaceType)
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
    }
}
