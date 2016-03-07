// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Loader;

namespace Microsoft.DotNet.ProjectModel.Loader
{
    public static class LoaderProjectContextExtensions
    {
        public static AssemblyLoadContext CreateLoadContext(
            this ProjectContext context, 
            string configuration = "Debug", 
            string outputPath = null)
        {
            var exporter = context.CreateExporter(configuration);
            var assemblies = new Dictionary<AssemblyName, string>(AssemblyNameComparer.OrdinalIgnoreCase);
            var dllImports = new Dictionary<string, string>();

            foreach (var export in exporter.GetAllExports())
            {
                // TODO: Handle resource assemblies
                foreach (var asset in export.RuntimeAssemblies)
                {
                    // REVIEW: Should we use the following?
                    // AssemblyLoadContext.GetAssemblyName(asset.ResolvedPath);
                    var assemblyName = new AssemblyName(asset.Name);
                    assemblies[assemblyName] = asset.ResolvedPath;
                }

                foreach (var asset in export.NativeLibraries)
                {
                    dllImports[asset.Name] = asset.ResolvedPath;
                }
            }

            return new ProjectLoadContext(
                assemblies,
                dllImports,

                // Add the project's output directory path to ensure project-to-project references get located
                new[] { context.GetOutputPaths(configuration, outputPath: outputPath).CompilationOutputPath });
        }

        private class AssemblyNameComparer : IEqualityComparer<AssemblyName>
        {
            public static readonly IEqualityComparer<AssemblyName> OrdinalIgnoreCase = new AssemblyNameComparer();

            private AssemblyNameComparer()
            {
            }

            public bool Equals(AssemblyName x, AssemblyName y)
            {
                // Ignore case because that's what Assembly.Load does.
                return string.Equals(x.Name, y.Name, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(x.CultureName ?? string.Empty, y.CultureName ?? string.Empty, StringComparison.Ordinal);
            }

            public int GetHashCode(AssemblyName obj)
            {
                var hashCode = 0;
                if (obj.Name != null)
                {
                    hashCode ^= obj.Name.GetHashCode();
                }

                hashCode ^= (obj.CultureName ?? string.Empty).GetHashCode();
                return hashCode;
            }
        }
    }
}