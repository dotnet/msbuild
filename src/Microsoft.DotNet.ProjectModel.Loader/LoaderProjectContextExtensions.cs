// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Loader;

namespace Microsoft.DotNet.ProjectModel.Loader
{
    public static class LoaderProjectContextExtensions
    {
        public static AssemblyLoadContext CreateLoadContext(this ProjectContext context, string configuration = "Debug")
        {
            var exporter = context.CreateExporter(configuration);
            var assemblies = new Dictionary<AssemblyName, string>();
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
                new[] { context.GetOutputPathCalculator().GetOutputDirectoryPath(configuration) });
        }
    }
}