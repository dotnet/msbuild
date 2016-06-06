// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.DotNet.ProjectModel.Compilation;
using Microsoft.Extensions.DependencyModel;

namespace Microsoft.DotNet.ProjectModel.Loader
{
    public static class LoaderProjectContextExtensions
    {
        public static AssemblyLoadContext CreateLoadContext(
            this ProjectContext context,
            string configuration) => CreateLoadContext(context, context.RuntimeIdentifier, configuration);

        public static AssemblyLoadContext CreateLoadContext(
            this ProjectContext context,
            string runtimeIdentifier,
            string configuration) => CreateLoadContext(context, runtimeIdentifier, configuration);

        public static AssemblyLoadContext CreateLoadContext(
            this ProjectContext context,
            string runtimeIdentifier,
            string configuration,
            string outputPath)
        {
            var exporter = context.CreateExporter(configuration);
            var assemblies = new Dictionary<AssemblyName, string>(AssemblyNameComparer.OrdinalIgnoreCase);
            var nativeLibs = new Dictionary<string, string>();
            var rids = DependencyContext.Default?.RuntimeGraph ?? Enumerable.Empty<RuntimeFallbacks>();
            var fallbacks = rids.FirstOrDefault(r => r.Runtime.Equals(runtimeIdentifier));

            foreach (var export in exporter.GetAllExports())
            {
                // Process managed assets
                var group = string.IsNullOrEmpty(runtimeIdentifier) ?
                    export.RuntimeAssemblyGroups.GetDefaultGroup() :
                    GetGroup(export.RuntimeAssemblyGroups, runtimeIdentifier, fallbacks);
                if(group != null)
                {
                    foreach(var asset in group.Assets)
                    {
                        assemblies[asset.GetAssemblyName()] = asset.ResolvedPath;
                    }
                }

                // Process native assets
                group = string.IsNullOrEmpty(runtimeIdentifier) ?
                    export.NativeLibraryGroups.GetDefaultGroup() :
                    GetGroup(export.NativeLibraryGroups, runtimeIdentifier, fallbacks);
                if(group != null)
                {
                    foreach(var asset in group.Assets)
                    {
                        nativeLibs[asset.Name] = asset.ResolvedPath;
                    }
                }

                // Process resource assets
                foreach(var asset in export.ResourceAssemblies)
                {
                    var name = asset.Asset.GetAssemblyName();
                    name.CultureName = asset.Locale;
                    assemblies[name] = asset.Asset.ResolvedPath;
                }
            }

            return new ProjectLoadContext(
                assemblies,
                nativeLibs,

                // Add the project's output directory path to ensure project-to-project references get located
                new[] { context.GetOutputPaths(configuration, outputPath: outputPath).CompilationOutputPath });
        }

        private static LibraryAssetGroup GetGroup(IEnumerable<LibraryAssetGroup> groups, string runtimeIdentifier, RuntimeFallbacks fallbacks)
        {
            IEnumerable<string> rids = new[] { runtimeIdentifier };
            if(fallbacks != null)
            {
                rids = Enumerable.Concat(rids, fallbacks.Fallbacks);
            }

            foreach(var rid in rids)
            {
                var group = groups.GetRuntimeGroup(rid);
                if(group != null)
                {
                    return group;
                }
            }
            return null;
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