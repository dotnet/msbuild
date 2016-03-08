// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Microsoft.Extensions.DependencyModel
{
    public static class DependencyContextValidator
    {
        private static void Error(string message)
        {
            throw new InvalidOperationException(message);
        }

        private static void CheckMetadata(Library library)
        {
            if (string.Equals(library.Type, "package", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(library.Name) ||
                    string.IsNullOrWhiteSpace(library.Hash) ||
                    string.IsNullOrWhiteSpace(library.Version))
                {
                    Error($"Empty metadata for {library.GetType().ToString()} {library.Name}");
                }
            }
        }

        public static void Validate(bool full)
        {
            var context = DependencyContext.Default;
            if (full)
            {
                if (!context.CompileLibraries.Any())
                {
                    Error("Compilation libraries empty");
                }
                foreach (var compilationLibrary in context.CompileLibraries)
                {
                    CheckMetadata(compilationLibrary);
                    var resolvedPaths = compilationLibrary.ResolveReferencePaths();
                    foreach (var resolvedPath in resolvedPaths)
                    {
                        if (!File.Exists(resolvedPath))
                        {
                            Error($"Compilataion library resolved to non existent path {resolvedPath}");
                        }
                    }
                }
            }

            foreach (var runtimeLibrary in context.RuntimeLibraries)
            {
                CheckMetadata(runtimeLibrary);
                foreach (var runtimeAssembly in runtimeLibrary.Assemblies)
                {
                    var assembly = Assembly.Load(runtimeAssembly.Name);
                }
            }

        }
    }
}
