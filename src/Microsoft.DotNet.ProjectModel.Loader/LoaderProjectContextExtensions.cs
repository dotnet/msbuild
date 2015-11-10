using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.ProjectModel;

namespace Microsoft.DotNet.ProjectModel.Loader
{
    public static class LoaderProjectContextExtensions
    {
        public static AssemblyLoadContext Create(this ProjectContext context, string configuration = "Debug")
        {
            var exporter = context.CreateExporter(configuration);
            var assemblies = new Dictionary<AssemblyName, string>();
            var dllImports = new Dictionary<string, string>();

            foreach (var export in exporter.GetAllExports())
            {
                // TODO: Handle project references
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

            return new ProjectContextLoadContext(assemblies, dllImports);
        }
    }
}