// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.BackEnd.SdkResolution
{
    internal class SdkResolverLoader
    {
        internal virtual IList<SdkResolver> LoadResolvers(LoggingContext loggingContext,
            ElementLocation location)
        {
            // Always add the default resolver
            var resolvers = new List<SdkResolver> {new DefaultSdkResolver()};

            var potentialResolvers = FindPotentialSdkResolvers(
                Path.Combine(BuildEnvironmentHelper.Instance.MSBuildToolsDirectory32, "SdkResolvers"));

            if (potentialResolvers.Count == 0)
            {
                return resolvers;
            }

            foreach (var potentialResolver in potentialResolvers)
            {
                LoadResolvers(potentialResolver, loggingContext, location, resolvers);
            }

            return resolvers.OrderBy(t => t.Priority).ToList();
        }

        /// <summary>
        ///     Find all files that are to be considered SDK Resolvers. Pattern will match
        ///     Root\SdkResolver\(ResolverName)\(ResolverName).dll.
        /// </summary>
        /// <param name="rootFolder"></param>
        /// <returns></returns>
        internal virtual IList<string> FindPotentialSdkResolvers(string rootFolder)
        {
            if (string.IsNullOrEmpty(rootFolder) || !FileUtilities.DirectoryExistsNoThrow(rootFolder))
                return new List<string>();

            return new DirectoryInfo(rootFolder).GetDirectories()
                .Select(subfolder => Path.Combine(subfolder.FullName, $"{subfolder.Name}.dll"))
                .Where(FileUtilities.FileExistsNoThrow)
                .ToList();
        }

        protected virtual IEnumerable<Type> GetResolverTypes(Assembly assembly)
        {
            return assembly.ExportedTypes
                .Select(type => new {type, info = type.GetTypeInfo()})
                .Where(t => t.info.IsClass && t.info.IsPublic && !t.info.IsAbstract && typeof(SdkResolver).IsAssignableFrom(t.type))
                .Select(t => t.type);
        }

        protected virtual Assembly LoadResolverAssembly(
            string resolverPath,
            LoggingContext loggingContext,
            ElementLocation location
#if !FEATURE_ASSEMBLY_LOADFROM
            , CoreClrAssemblyLoader loader
#endif
        )
        {
#if FEATURE_ASSEMBLY_LOADFROM
                return Assembly.LoadFrom(resolverPath);
#else
                loader.AddDependencyLocation(Path.GetDirectoryName(potentialResolver));
                return loader.LoadFromPath(potentialResolver);
#endif
        }

        protected virtual void LoadResolvers(string resolverPath, LoggingContext loggingContext, ElementLocation location, List<SdkResolver> resolvers)
        {
#if !FEATURE_ASSEMBLY_LOADFROM
            var loader = new CoreClrAssemblyLoader();
#endif

            try
            {
                Assembly assembly = LoadResolverAssembly(
                    resolverPath,
                    loggingContext,
                    location
#if !FEATURE_ASSEMBLY_LOADFROM
                    , loader
#endif
                );

                if (assembly != null)
                {
                    resolvers.AddRange(GetResolverTypes(assembly).Select(t => (SdkResolver)Activator.CreateInstance(t)));
                }
            }
            catch (Exception e)
            {
                loggingContext.LogWarning(null, new BuildEventFileInfo(location), "CouldNotLoadSdkResolver", e.Message);
            }
        }
    }
}
