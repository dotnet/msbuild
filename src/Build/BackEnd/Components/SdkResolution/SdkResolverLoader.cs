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

namespace Microsoft.Build.BackEnd.SdkResolution
{
    internal class SdkResolverLoader
    {
#if !FEATURE_ASSEMBLY_LOADFROM
        private readonly CoreClrAssemblyLoader _loader = new CoreClrAssemblyLoader();
#endif

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

        protected virtual Assembly LoadResolverAssembly(string resolverPath, LoggingContext loggingContext, ElementLocation location)
        {
#if FEATURE_ASSEMBLY_LOADFROM
            return Assembly.LoadFrom(resolverPath);
#else
            _loader.AddDependencyLocation(Path.GetDirectoryName(resolverPath));
            return _loader.LoadFromPath(resolverPath);
#endif
        }

        protected virtual void LoadResolvers(string resolverPath, LoggingContext loggingContext, ElementLocation location, List<SdkResolver> resolvers)
        {
            Assembly assembly;
            try
            {
                assembly = LoadResolverAssembly(resolverPath, loggingContext, location);
            }
            catch (Exception e)
            {
                ProjectFileErrorUtilities.ThrowInvalidProjectFile(new BuildEventFileInfo(location), e, "CouldNotLoadSdkResolverAssembly", resolverPath, e.Message);

                return;
            }

            foreach (Type type in GetResolverTypes(assembly))
            {
                try
                {
                    resolvers.Add((SdkResolver)Activator.CreateInstance(type));
                }
                catch (TargetInvocationException e)
                {
                    // .NET wraps the original exception inside of a TargetInvocationException which masks the original message
                    // Attempt to get the inner exception in this case, but fall back to the top exception message
                    string message = e.InnerException?.Message ?? e.Message;

                    ProjectFileErrorUtilities.ThrowInvalidProjectFile(new BuildEventFileInfo(location), e.InnerException ?? e, "CouldNotLoadSdkResolver", type.Name, message);
                }
                catch (Exception e)
                {
                    ProjectFileErrorUtilities.ThrowInvalidProjectFile(new BuildEventFileInfo(location), e, "CouldNotLoadSdkResolver", type.Name, e.Message);
                }
            }
        }
    }
}
