// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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

#nullable disable

namespace Microsoft.Build.BackEnd.SdkResolution
{
    internal class SdkResolverLoader
    {
#if FEATURE_ASSEMBLYLOADCONTEXT
        private static readonly CoreClrAssemblyLoader s_loader = new CoreClrAssemblyLoader();
#endif

        private readonly string IncludeDefaultResolver = Environment.GetEnvironmentVariable("MSBUILDINCLUDEDEFAULTSDKRESOLVER");

        // Test hook for loading SDK Resolvers from additional folders.  Support runtime-specific test hook environment variables,
        //  as an SDK resolver built for .NET Framework probably won't work on .NET Core, and vice versa.
        private readonly string AdditionalResolversFolder = Environment.GetEnvironmentVariable(
#if NETFRAMEWORK
            "MSBUILDADDITIONALSDKRESOLVERSFOLDER_NETFRAMEWORK")
#elif NET
            "MSBUILDADDITIONALSDKRESOLVERSFOLDER_NET")
#endif
            ?? Environment.GetEnvironmentVariable("MSBUILDADDITIONALSDKRESOLVERSFOLDER");

        internal virtual IReadOnlyList<SdkResolver> GetDefaultResolvers()
        {
            var resolvers = !string.Equals(IncludeDefaultResolver, "false", StringComparison.OrdinalIgnoreCase) ?
                new List<SdkResolver> { new DefaultSdkResolver() }
                : new List<SdkResolver>();

            return resolvers;
        }

        internal virtual IReadOnlyList<SdkResolver> LoadAllResolvers(ElementLocation location)
        {
            var resolvers = !string.Equals(IncludeDefaultResolver, "false", StringComparison.OrdinalIgnoreCase) ?
                new List<SdkResolver> { new DefaultSdkResolver() }
                : new List<SdkResolver>();

            var potentialResolvers = FindPotentialSdkResolvers(
                Path.Combine(BuildEnvironmentHelper.Instance.MSBuildToolsDirectory32, "SdkResolvers"), location);

            if (potentialResolvers.Count == 0)
            {
                return resolvers;
            }

            foreach (var potentialResolver in potentialResolvers)
            {
                LoadResolvers(potentialResolver, location, resolvers);
            }

            return resolvers.OrderBy(t => t.Priority).ToList();
        }

        internal virtual IReadOnlyList<SdkResolverManifest> GetResolversManifests(ElementLocation location)
        {
            return FindPotentialSdkResolversManifests(
                Path.Combine(BuildEnvironmentHelper.Instance.MSBuildToolsDirectoryRoot, "SdkResolvers"), location);
        }

        /// <summary>
        ///     Find all files that are to be considered SDK Resolvers. Pattern will match
        ///     Root\SdkResolver\(ResolverName)\(ResolverName).dll.
        /// </summary>
        /// <param name="rootFolder"></param>
        /// <param name="location"></param>
        /// <returns></returns>
        internal virtual IReadOnlyList<string> FindPotentialSdkResolvers(string rootFolder, ElementLocation location)
        {
            var manifestsList = FindPotentialSdkResolversManifests(rootFolder, location);

            return manifestsList.Select(manifest => manifest.Path).ToList();
        }

        internal virtual IReadOnlyList<SdkResolverManifest> FindPotentialSdkResolversManifests(string rootFolder, ElementLocation location)
        {
            List<SdkResolverManifest> manifestsList = new List<SdkResolverManifest>();

            if ((string.IsNullOrEmpty(rootFolder) || !FileUtilities.DirectoryExistsNoThrow(rootFolder)) && AdditionalResolversFolder == null)
            {
                return manifestsList;
            }

            DirectoryInfo[] subfolders = GetSubfolders(rootFolder, AdditionalResolversFolder);

            foreach (var subfolder in subfolders)
            {
                var manifest = Path.Combine(subfolder.FullName, $"{subfolder.Name}.xml");
                var assembly = Path.Combine(subfolder.FullName, $"{subfolder.Name}.dll");
                bool assemblyAdded = false;

                if (ChangeWaves.AreFeaturesEnabled(ChangeWaves.Wave17_4))
                {
                    // Prefer manifest over the assembly. Try to read the xml first, and if not found then look for an assembly.
                    assemblyAdded = TryAddAssemblyManifestFromXml(manifest, subfolder.FullName, manifestsList, location);
                    if (!assemblyAdded)
                    {
                        assemblyAdded = TryAddAssemblyManifestFromDll(assembly, manifestsList);
                    }
                }
                else
                {
                    assemblyAdded = TryAddAssemblyManifestFromDll(assembly, manifestsList);
                    if (!assemblyAdded)
                    {
                        assemblyAdded = TryAddAssemblyManifestFromXml(manifest, subfolder.FullName, manifestsList, location);
                    }
                }

                if (!assemblyAdded)
                {
                    ProjectFileErrorUtilities.ThrowInvalidProjectFile(new BuildEventFileInfo(location), "SdkResolverNoDllOrManifest", subfolder.FullName);
                }
            }

            return manifestsList;
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

        private bool TryAddAssemblyManifestFromXml(string pathToManifest, string manifestFolder, List<SdkResolverManifest> manifestsList, ElementLocation location)
        {
            if (!string.IsNullOrEmpty(pathToManifest) && !FileUtilities.FileExistsNoThrow(pathToManifest))
            {
                return false;
            }

            SdkResolverManifest manifest = null;
            try
            {
                // <SdkResolver>
                //   <Path>...</Path>
                //   <ResolvableSdkPattern>(Optional field)</ResolvableSdkPattern>
                // </SdkResolver>
                manifest = SdkResolverManifest.Load(pathToManifest, manifestFolder);

                if (manifest == null || string.IsNullOrEmpty(manifest.Path))
                {
                    ProjectFileErrorUtilities.ThrowInvalidProjectFile(new BuildEventFileInfo(location), "SdkResolverDllInManifestMissing", pathToManifest, string.Empty);
                }
            }
            catch (XmlException e)
            {
                // Note: Not logging e.ToString() as most of the information is not useful, the Message will contain what is wrong with the XML file.
                ProjectFileErrorUtilities.ThrowInvalidProjectFile(new BuildEventFileInfo(location), e, "SdkResolverManifestInvalid", pathToManifest, e.Message);
            }

            if (string.IsNullOrEmpty(manifest.Path) || !FileUtilities.FileExistsNoThrow(manifest.Path))
            {
                ProjectFileErrorUtilities.ThrowInvalidProjectFile(new BuildEventFileInfo(location), "SdkResolverDllInManifestMissing", pathToManifest, manifest.Path);
            }

            manifestsList.Add(manifest);

            return true;
        }

        private bool TryAddAssemblyManifestFromDll(string assemblyPath, List<SdkResolverManifest> manifestsList)
        {
            if (string.IsNullOrEmpty(assemblyPath) || !FileUtilities.FileExistsNoThrow(assemblyPath))
            {
                return false;
            }

            manifestsList.Add(new SdkResolverManifest(DisplayName: assemblyPath, Path: assemblyPath, ResolvableSdkRegex: null));
            return true;
        }

        protected virtual IEnumerable<Type> GetResolverTypes(Assembly assembly)
        {
            return assembly.ExportedTypes
                .Select(type => new { type, info = type.GetTypeInfo() })
                .Where(t => t.info.IsClass && t.info.IsPublic && !t.info.IsAbstract && typeof(SdkResolver).IsAssignableFrom(t.type))
                .Select(t => t.type);
        }

        protected virtual Assembly LoadResolverAssembly(string resolverPath)
        {
#if !FEATURE_ASSEMBLYLOADCONTEXT
            return Assembly.LoadFrom(resolverPath);
#else
            return s_loader.LoadFromPath(resolverPath);
#endif
        }

        protected internal virtual IReadOnlyList<SdkResolver> LoadResolversFromManifest(SdkResolverManifest manifest, ElementLocation location)
        {
            var resolvers = new List<SdkResolver>();
            LoadResolvers(manifest.Path, location, resolvers);
            return resolvers;
        }

        protected virtual void LoadResolvers(string resolverPath, ElementLocation location, List<SdkResolver> resolvers)
        {
            Assembly assembly;
            try
            {
                assembly = LoadResolverAssembly(resolverPath);
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
