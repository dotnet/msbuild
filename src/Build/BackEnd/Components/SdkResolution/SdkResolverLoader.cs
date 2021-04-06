// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.BackEnd.SdkResolution
{
    internal class SdkResolverLoader
    {
#if FEATURE_ASSEMBLYLOADCONTEXT
        private readonly CoreClrAssemblyLoader _loader = new CoreClrAssemblyLoader();
#endif

        private readonly string IncludeDefaultResolver = Environment.GetEnvironmentVariable("MSBUILDINCLUDEDEFAULTSDKRESOLVER");

        //  Test hook for loading SDK Resolvers from additional folders.  Support runtime-specific test hook environment variables,
        //  as an SDK resolver built for .NET Framework probably won't work on .NET Core, and vice versa.
        private readonly string AdditionalResolversFolder = Environment.GetEnvironmentVariable(
#if NETFRAMEWORK
            "MSBUILDADDITIONALSDKRESOLVERSFOLDER_NETFRAMEWORK"
#elif NET
            "MSBUILDADDITIONALSDKRESOLVERSFOLDER_NET"
#endif
            ) ?? Environment.GetEnvironmentVariable("MSBUILDADDITIONALSDKRESOLVERSFOLDER");

        internal virtual IList<SdkResolver> LoadResolvers(LoggingContext loggingContext,
            ElementLocation location)
        {
            var resolvers = !String.Equals(IncludeDefaultResolver, "false", StringComparison.OrdinalIgnoreCase) ? 
                new List<SdkResolver> {new DefaultSdkResolver()}
                : new List<SdkResolver>();

            var potentialResolvers = FindPotentialSdkResolvers(
                Path.Combine(BuildEnvironmentHelper.Instance.MSBuildToolsDirectory32, "SdkResolvers"), location);

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
        /// <param name="location"></param>
        /// <returns></returns>
        internal virtual IList<string> FindPotentialSdkResolvers(string rootFolder, ElementLocation location)
        {
            var assembliesList = new List<string>();

            if ((string.IsNullOrEmpty(rootFolder) || !FileUtilities.DirectoryExistsNoThrow(rootFolder)) && AdditionalResolversFolder == null)
            {
                return assembliesList;
            }

            DirectoryInfo[] subfolders = GetSubfolders(rootFolder, AdditionalResolversFolder);

            foreach (var subfolder in subfolders)
            {
                var assembly = Path.Combine(subfolder.FullName, $"{subfolder.Name}.dll");
                var manifest = Path.Combine(subfolder.FullName, $"{subfolder.Name}.xml");

                var assemblyAdded = TryAddAssembly(assembly, assembliesList);
                if (!assemblyAdded)
                {
                    assemblyAdded = TryAddAssemblyFromManifest(manifest, subfolder.FullName, assembliesList, location);
                }

                if (!assemblyAdded)
                {
                    ProjectFileErrorUtilities.ThrowInvalidProjectFile(new BuildEventFileInfo(location), "SdkResolverNoDllOrManifest", subfolder.FullName);
                }
            }

            return assembliesList;
        }

        private DirectoryInfo[] GetSubfolders(string rootFolder, string additionalResolversFolder)
        {
            DirectoryInfo[] subfolders = null;
            if (!string.IsNullOrEmpty(rootFolder) && FileUtilities.DirectoryExistsNoThrow(rootFolder))
            {
                subfolders = new DirectoryInfo(rootFolder).GetDirectories();
            }

            if (additionalResolversFolder != null)
            {
                var resolversDirInfo = new DirectoryInfo(additionalResolversFolder);
                if (resolversDirInfo.Exists)
                {
                    HashSet<DirectoryInfo> overrideFolders = resolversDirInfo.GetDirectories().ToHashSet(new DirInfoNameComparer());
                    if (subfolders != null)
                    {
                        overrideFolders.UnionWith(subfolders);
                    }
                    return overrideFolders.ToArray();
                }
            }

            return subfolders;
        }

        private class DirInfoNameComparer : IEqualityComparer<DirectoryInfo>
        {
            public bool Equals(DirectoryInfo first, DirectoryInfo second)
            {
                return string.Equals(first.Name, second.Name, StringComparison.OrdinalIgnoreCase);
            }

            public int GetHashCode(DirectoryInfo value)
            {
                return value.Name.GetHashCode();
            }
        }

        private bool TryAddAssemblyFromManifest(string pathToManifest, string manifestFolder, List<string> assembliesList, ElementLocation location)
        {
            if (!string.IsNullOrEmpty(pathToManifest) && !FileUtilities.FileExistsNoThrow(pathToManifest)) return false;

            string path = null;

            try
            {
                // <SdkResolver>
                //   <Path>...</Path>
                // </SdkResolver>
                var manifest = SdkResolverManifest.Load(pathToManifest);

                if (manifest == null || string.IsNullOrEmpty(manifest.Path))
                {
                    ProjectFileErrorUtilities.ThrowInvalidProjectFile(new BuildEventFileInfo(location), "SdkResolverDllInManifestMissing", pathToManifest, string.Empty);
                }

                path = FileUtilities.FixFilePath(manifest.Path);
            }
            catch (XmlException e)
            {
                // Note: Not logging e.ToString() as most of the information is not useful, the Message will contain what is wrong with the XML file.
                ProjectFileErrorUtilities.ThrowInvalidProjectFile(new BuildEventFileInfo(location), e, "SdkResolverManifestInvalid", pathToManifest, e.Message);
            }

            if (!Path.IsPathRooted(path))
            {
                path = Path.Combine(manifestFolder, path);
                path = Path.GetFullPath(path);
            }

            if (!TryAddAssembly(path, assembliesList))
            {
                ProjectFileErrorUtilities.ThrowInvalidProjectFile(new BuildEventFileInfo(location), "SdkResolverDllInManifestMissing", pathToManifest, path);
            }

            return true;
        }

        private bool TryAddAssembly(string assemblyPath, List<string> assembliesList)
        {
            if (string.IsNullOrEmpty(assemblyPath) || !FileUtilities.FileExistsNoThrow(assemblyPath)) return false;

            assembliesList.Add(assemblyPath);
            return true;
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
#if !FEATURE_ASSEMBLYLOADCONTEXT
            return Assembly.LoadFrom(resolverPath);
#else
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
