// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !NET
using System.Text.RegularExpressions;
#endif
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiSymbolExtensions;
using Microsoft.DotNet.ApiSymbolExtensions.Filtering;
using Microsoft.DotNet.ApiSymbolExtensions.Logging;
using Microsoft.DotNet.GenAPI.Filtering;

namespace Microsoft.DotNet.GenAPI
{
    /// <summary>
    /// Class to standardize initialization and running of GenAPI tool.
    /// Shared between CLI and MSBuild tasks frontends.
    /// </summary>
    public static class GenAPIApp
    {
        public readonly struct Context
        {
            public Context(
                string[] assemblies,
                string[]? assemblyReferences,
                string? outputPath,
                string? headerFile,
                string? exceptionMessage,
                string[]? excludeApiFiles,
                string[]? excludeAttributesFiles,
                bool respectInternals,
                bool includeAssemblyAttributes)
            {
                Assemblies = assemblies;
                AssemblyReferences = assemblyReferences;
                OutputPath = outputPath;
                HeaderFile = headerFile;
                ExceptionMessage = exceptionMessage;
                ExcludeApiFiles = excludeApiFiles;
                ExcludeAttributesFiles = excludeAttributesFiles;
                RespectInternals = respectInternals;
                IncludeAssemblyAttributes = includeAssemblyAttributes;
            }

            /// <summary>
            /// The path to one or more assemblies or directories with assemblies.
            /// </summary>
            public string[] Assemblies { get; }

            /// <summary>
            /// Paths to assembly references or their underlying directories for a specific target framework in the package.
            /// </summary>
            public string[]? AssemblyReferences { get; }

            /// <summary>
            /// Output path. Default is the console. Can specify an existing directory as well and
            /// then a file will be created for each assembly with the matching name of the assembly.
            /// </summary>
            public string? OutputPath { get; }

            /// <summary>
            /// Specify a file with an alternate header content to prepend to output.
            /// </summary>
            public string? HeaderFile { get; }

            /// <summary>
            /// Method bodies should throw PlatformNotSupportedException.
            /// </summary>
            public string? ExceptionMessage { get; }

            /// <summary>
            /// The path to one or more api exclusion files with types in DocId format.
            /// </summary>
            public string[]? ExcludeApiFiles { get; }

            /// <summary>
            /// The path to one or more attribute exclusion files with types in DocId format.
            /// </summary>
            public string[]? ExcludeAttributesFiles { get; }

            /// <summary>
            /// If true, includes both internal and public API.
            /// </summary>
            public bool RespectInternals { get; }

            /// <summary>
            /// Includes assembly attributes which are values that provide information about an assembly.
            /// </summary>
            public bool IncludeAssemblyAttributes { get; }
        }

        /// <summary>
        /// Initialize and run Roslyn-based GenAPI tool.
        /// </summary>
        public static void Run(ILog logger, Context context)
        {
            bool resolveAssemblyReferences = context.AssemblyReferences?.Length > 0;

            IAssemblySymbolLoader loader = new AssemblySymbolLoader(resolveAssemblyReferences, context.RespectInternals);

            if (context.AssemblyReferences is not null)
            {
                loader.AddReferenceSearchPaths(context.AssemblyReferences);
            }

            CompositeSymbolFilter compositeSymbolFilter = new CompositeSymbolFilter()
                .Add(new ImplicitSymbolFilter())
                .Add(new AccessibilitySymbolFilter(
                    context.RespectInternals,
                    includeEffectivelyPrivateSymbols: true,
                    includeExplicitInterfaceImplementationSymbols: true));

            if (context.ExcludeAttributesFiles is not null)
            {
                compositeSymbolFilter.Add(new DocIdSymbolFilter(context.ExcludeAttributesFiles));
            }

            if (context.ExcludeApiFiles is not null)
            {
                compositeSymbolFilter.Add(new DocIdSymbolFilter(context.ExcludeApiFiles));
            }

            IReadOnlyList<IAssemblySymbol?> assemblySymbols = loader.LoadAssemblies(context.Assemblies);
            foreach (IAssemblySymbol? assemblySymbol in assemblySymbols)
            {
                if (assemblySymbol == null)
                    continue;

                using TextWriter textWriter = GetTextWriter(context.OutputPath, assemblySymbol.Name);
                textWriter.Write(ReadHeaderFile(context.HeaderFile));

                using CSharpFileBuilder fileBuilder = new(logger,
                    compositeSymbolFilter,
                    textWriter,
                    context.ExceptionMessage,
                    context.IncludeAssemblyAttributes,
                    loader.MetadataReferences);

                fileBuilder.WriteAssembly(assemblySymbol);
            }

            if (loader.HasRoslynDiagnostics(out IReadOnlyList<Diagnostic> roslynDiagnostics))
            {
                foreach (Diagnostic warning in roslynDiagnostics)
                {
                    logger.LogWarning(warning.Id, warning.ToString());
                }
            }

            if (loader.HasLoadWarnings(out IReadOnlyList<AssemblyLoadWarning> loadWarnings))
            {
                foreach (AssemblyLoadWarning warning in loadWarnings)
                {
                    logger.LogWarning(warning.DiagnosticId, warning.Message);
                }
            }
        }

        /// <summary>
        /// Creates a TextWriter capable to write into Console or cs file.
        /// </summary>
        /// <param name="outputDirPath">Path to a directory where file with `assemblyName`.cs filename needs to be created.
        ///     If Null - output to Console.Out.</param>
        /// <param name="assemblyName">Name of an assembly. if outputDirPath is not a Null - represents a file name.</param>
        /// <returns></returns>
        private static TextWriter GetTextWriter(string? outputDirPath, string assemblyName)
        {
            if (outputDirPath is null)
            {
                return Console.Out;
            }

            string fileName = assemblyName + ".cs";
            if (Directory.Exists(outputDirPath))
            {
                return File.CreateText(Path.Combine(outputDirPath, fileName));
            }

            return File.CreateText(outputDirPath);
        }

        /// <summary>
        /// Read the header file if specified, or use default one.
        /// </summary>
        /// <param name="headerFile">File with an alternate header content to prepend to output</param>
        /// <returns></returns>
        private static string ReadHeaderFile(string? headerFile)
        {
            const string defaultFileHeader = """
            //------------------------------------------------------------------------------
            // <auto-generated>
            //     This code was generated by a tool.
            //
            //     Changes to this file may cause incorrect behavior and will be lost if
            //     the code is regenerated.
            // </auto-generated>
            //------------------------------------------------------------------------------

            """;

            string header = !string.IsNullOrEmpty(headerFile) ?
                File.ReadAllText(headerFile) :
                defaultFileHeader;

#if NET
            header = header.ReplaceLineEndings();
#else
            header = Regex.Replace(header, @"\r\n|\n\r|\n|\r", Environment.NewLine);
#endif

            return header;
        }
    }
}
