// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiSymbolExtensions;

namespace Microsoft.DotNet.GenAPI
{
    /// <summary>
    /// Class to standertize initilization and running of GenAPI tool.
    ///     Shared between CLI and MSBuild tasks frontends.
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
                string[]? excludeAttributesFiles,
                bool includeVisibleOutsideOfAssembly)
            {
                Assemblies = assemblies;
                AssemblyReferences = assemblyReferences;
                OutputPath = outputPath;
                HeaderFile = headerFile;
                ExceptionMessage = exceptionMessage;
                ExcludeAttributesFiles = excludeAttributesFiles;
                IncludeVisibleOutsideOfAssembly = includeVisibleOutsideOfAssembly;
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
            /// The path to one or more attribute exclusion files with types in DocId format.
            /// </summary>
            public string[]? ExcludeAttributesFiles { get; }

            /// <summary>
            /// Include all API's not just public APIs. Default is public only.
            /// </summary>
            public bool IncludeVisibleOutsideOfAssembly { get; }
        }

        /// <summary>
        /// Initialize and run Roslyn-based GenAPI tool.
        /// </summary>
        public static void Run(Context context)
        {
            bool resolveAssemblyReferences = context.AssemblyReferences?.Length > 0;

            IAssemblySymbolLoader loader = new AssemblySymbolLoader(resolveAssemblyReferences);

            if (context.AssemblyReferences is not null)
            {
                loader.AddReferenceSearchPaths(context.AssemblyReferences);
            }

            var compositeFilter = new CompositeFilter()
                .Add<ImplicitSymbolsFilter>()
                .Add(new SymbolAccessibilityBasedFilter(context.IncludeVisibleOutsideOfAssembly));

            if (context.ExcludeAttributesFiles != null)
            {
                compositeFilter.Add(new AttributeSymbolFilter(context.ExcludeAttributesFiles));
            }

            IReadOnlyList<IAssemblySymbol?> assemblySymbols = loader.LoadAssemblies(context.Assemblies);
            foreach (IAssemblySymbol? assemblySymbol in assemblySymbols)
            {
                if (assemblySymbol == null) continue;

                using CSharpFileBuilder fileBuilder = new(
                    compositeFilter,
                    GetTextWriter(context.OutputPath, assemblySymbol.Name),
                    new CSharpSyntaxWriter(context.ExceptionMessage));

                fileBuilder.WriteAssembly(assemblySymbol);
            }

            // TODO: Add logging for the assembly symbol loading failure".
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
            if (outputDirPath == null)
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
            const string defaultFileHeader = @"
            //------------------------------------------------------------------------------
            // <auto-generated>
            //     This code was generated by a tool.
            //
            //     Changes to this file may cause incorrect behavior and will be lost if
            //     the code is regenerated.
            // </auto-generated>
            //------------------------------------------------------------------------------
            ";

            if (!string.IsNullOrEmpty(headerFile))
            {
                return File.ReadAllText(headerFile);
            }
            return defaultFileHeader;
        }
    }
}
