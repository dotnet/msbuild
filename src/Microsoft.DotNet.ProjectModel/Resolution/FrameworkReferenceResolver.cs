// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Xml.Linq;
using Microsoft.DotNet.ProjectModel.Utilities;
using NuGet.Frameworks;

namespace Microsoft.DotNet.ProjectModel.Resolution
{
    public class FrameworkReferenceResolver
    {
        // FrameworkConstants doesn't have dnx46 yet
        private static readonly NuGetFramework Dnx46 = new NuGetFramework(
            FrameworkConstants.FrameworkIdentifiers.Dnx,
            new Version(4, 6));

        private readonly IDictionary<NuGetFramework, FrameworkInformation> _cache = new Dictionary<NuGetFramework, FrameworkInformation>();

        private static readonly IDictionary<NuGetFramework, NuGetFramework[]> _aliases = new Dictionary<NuGetFramework, NuGetFramework[]>
        {
            { FrameworkConstants.CommonFrameworks.Dnx451, new [] { FrameworkConstants.CommonFrameworks.Net451 } },
            { Dnx46, new [] { FrameworkConstants.CommonFrameworks.Net46 } }
        };
        
        public FrameworkReferenceResolver(string referenceAssembliesPath)
        {
            ReferenceAssembliesPath = referenceAssembliesPath;
        }
        
        public string ReferenceAssembliesPath { get; }

        public bool TryGetAssembly(string name, NuGetFramework targetFramework, out string path, out Version version)
        {
            path = null;
            version = null;

            var information = _cache.GetOrAdd(targetFramework, GetFrameworkInformation);

            if (information == null || !information.Exists)
            {
                return false;
            }

            lock (information.Assemblies)
            {
                AssemblyEntry entry;
                if (information.Assemblies.TryGetValue(name, out entry))
                {
                    if (string.IsNullOrEmpty(entry.Path))
                    {
                        entry.Path = GetAssemblyPath(information.SearchPaths, name);
                    }

                    if (!string.IsNullOrEmpty(entry.Path) && entry.Version == null)
                    {
                        // This code path should only run on mono
                        entry.Version = VersionUtility.GetAssemblyVersion(entry.Path).Version;
                    }

                    path = entry.Path;
                    version = entry.Version;
                }
            }

            return !string.IsNullOrEmpty(path);
        }

        public bool IsInstalled(NuGetFramework targetFramework)
        {
            var information = _cache.GetOrAdd(targetFramework, GetFrameworkInformation);

            return information?.Exists == true;
        }

        public string GetFrameworkRedistListPath(NuGetFramework targetFramework)
        {
            var information = _cache.GetOrAdd(targetFramework, GetFrameworkInformation);

            if (information == null || !information.Exists)
            {
                return null;
            }

            return information.RedistListPath;
        }
        private FrameworkInformation GetFrameworkInformation(NuGetFramework targetFramework)
        {
            string referenceAssembliesPath = ReferenceAssembliesPath;

            if (string.IsNullOrEmpty(referenceAssembliesPath))
            {
                return null;
            }

            NuGetFramework[] candidates;
            if (_aliases.TryGetValue(targetFramework, out candidates))
            {
                foreach (var framework in candidates)
                {
                    var information = GetFrameworkInformation(framework);

                    if (information != null)
                    {
                        return information;
                    }
                }

                return null;
            }
            else
            {
                return GetFrameworkInformation(targetFramework, referenceAssembliesPath);
            }
        }

        private static FrameworkInformation GetFrameworkInformation(NuGetFramework targetFramework, string referenceAssembliesPath)
        {
            // Check for legacy frameworks
            if (targetFramework.IsDesktop() && targetFramework.Version <= new Version(3, 5))
            {
                return GetLegacyFrameworkInformation(targetFramework, referenceAssembliesPath);
            }

            var basePath = Path.Combine(referenceAssembliesPath,
                                        targetFramework.Framework,
                                        "v" + GetDisplayVersion(targetFramework));

            if (!string.IsNullOrEmpty(targetFramework.Profile))
            {
                basePath = Path.Combine(basePath, "Profile", targetFramework.Profile);
            }

            var version = new DirectoryInfo(basePath);
            if (!version.Exists)
            {
                return null;
            }

            return GetFrameworkInformation(version, targetFramework);
        }

        private static FrameworkInformation GetLegacyFrameworkInformation(NuGetFramework targetFramework, string referenceAssembliesPath)
        {
            var frameworkInfo = new FrameworkInformation();

            // Always grab .NET 2.0 data
            var searchPaths = new List<string>();
            var net20Dir = Path.Combine(Environment.GetEnvironmentVariable("WINDIR"), "Microsoft.NET", "Framework", "v2.0.50727");

            if (!Directory.Exists(net20Dir))
            {
                return null;
            }

            // Grab reference assemblies first, if present for this framework
            if (targetFramework.Version.Major == 3)
            {
                // Most specific first (i.e. 3.5)
                if (targetFramework.Version.Minor == 5)
                {
                    var refAsms35Dir = Path.Combine(referenceAssembliesPath, "v3.5");
                    if (!string.IsNullOrEmpty(targetFramework.Profile))
                    {
                        // The 3.5 Client Profile assemblies ARE in .NETFramework... it's weird.
                        refAsms35Dir = Path.Combine(referenceAssembliesPath, ".NETFramework", "v3.5", "Profile", targetFramework.Profile);
                    }
                    if (Directory.Exists(refAsms35Dir))
                    {
                        searchPaths.Add(refAsms35Dir);
                    }
                }

                // Always search the 3.0 reference assemblies
                if (string.IsNullOrEmpty(targetFramework.Profile))
                {
                    // a) 3.0 didn't have profiles
                    // b) When using a profile, we don't want to fall back to 3.0 or 2.0
                    var refAsms30Dir = Path.Combine(referenceAssembliesPath, "v3.0");
                    if (Directory.Exists(refAsms30Dir))
                    {
                        searchPaths.Add(refAsms30Dir);
                    }
                }
            }

            // .NET 2.0 reference assemblies go last (but only if there's no profile in the TFM)
            if (string.IsNullOrEmpty(targetFramework.Profile))
            {
                searchPaths.Add(net20Dir);
            }

            frameworkInfo.Exists = true;
            frameworkInfo.Path = searchPaths.First();
            frameworkInfo.SearchPaths = searchPaths;

            // Load the redist list in reverse order (most general -> most specific)
            for (int i = searchPaths.Count - 1; i >= 0; i--)
            {
                var dir = new DirectoryInfo(searchPaths[i]);
                if (dir.Exists)
                {
                    PopulateFromRedistList(dir, frameworkInfo);
                }
            }

            if (string.IsNullOrEmpty(frameworkInfo.Name))
            {
                frameworkInfo.Name = SynthesizeFrameworkFriendlyName(targetFramework);
            }
            return frameworkInfo;
        }

        private static string SynthesizeFrameworkFriendlyName(NuGetFramework targetFramework)
        {
            // Names are not present in the RedistList.xml file for older frameworks or on Mono
            // We do some custom version string rendering to match how net40 is rendered (.NET Framework 4)
            if (targetFramework.Framework.Equals(FrameworkConstants.FrameworkIdentifiers.Net))
            {
                string versionString = targetFramework.Version.Minor == 0 ?
                    targetFramework.Version.Major.ToString() :
                    GetDisplayVersion(targetFramework).ToString();

                string profileString = string.IsNullOrEmpty(targetFramework.Profile) ?
                    string.Empty :
                    $" {targetFramework.Profile} Profile";
                return ".NET Framework " + versionString + profileString;
            }
            return targetFramework.ToString();
        }

        private static FrameworkInformation GetFrameworkInformation(DirectoryInfo directory, NuGetFramework targetFramework)
        {
            var frameworkInfo = new FrameworkInformation();
            frameworkInfo.Exists = true;
            frameworkInfo.Path = directory.FullName;
            frameworkInfo.SearchPaths = new[] {
                frameworkInfo.Path,
                Path.Combine(frameworkInfo.Path, "Facades")
            };

            PopulateFromRedistList(directory, frameworkInfo);
            if (string.IsNullOrEmpty(frameworkInfo.Name))
            {
                frameworkInfo.Name = SynthesizeFrameworkFriendlyName(targetFramework);
            }
            return frameworkInfo;
        }

        private static void PopulateFromRedistList(DirectoryInfo directory, FrameworkInformation frameworkInfo)
        {
            // The redist list contains the list of assemblies for this target framework
            string redistList = Path.Combine(directory.FullName, "RedistList", "FrameworkList.xml");

            if (File.Exists(redistList))
            {
                frameworkInfo.RedistListPath = redistList;

                using (var stream = File.OpenRead(redistList))
                {
                    var frameworkList = XDocument.Load(stream);

                    // On mono, the RedistList.xml has an entry pointing to the TargetFrameworkDirectory
                    // It basically uses the GAC as the reference assemblies for all .NET framework
                    // profiles
                    var targetFrameworkDirectory = frameworkList.Root.Attribute("TargetFrameworkDirectory")?.Value;

                    if (!string.IsNullOrEmpty(targetFrameworkDirectory))
                    {
                        // For some odd reason, the paths are actually listed as \ so normalize them here
                        targetFrameworkDirectory = targetFrameworkDirectory.Replace('\\', Path.DirectorySeparatorChar);

                        // The specified path is the relative path from the RedistList.xml itself
                        var resovledPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(redistList), targetFrameworkDirectory));

                        // Update the path to the framework
                        frameworkInfo.Path = resovledPath;

                        PopulateAssemblies(frameworkInfo.Assemblies, resovledPath);
                        PopulateAssemblies(frameworkInfo.Assemblies, Path.Combine(resovledPath, "Facades"));
                    }
                    else
                    {
                        foreach (var e in frameworkList.Root.Elements())
                        {
                            var assemblyName = e.Attribute("AssemblyName").Value;
                            var version = e.Attribute("Version")?.Value;

                            var entry = new AssemblyEntry();
                            entry.Version = version != null ? Version.Parse(version) : null;
                            frameworkInfo.Assemblies[assemblyName] = entry;
                        }
                    }

                    var nameAttribute = frameworkList.Root.Attribute("Name");

                    frameworkInfo.Name = nameAttribute == null ? null : nameAttribute.Value;
                }
            }
        }

        private static void PopulateAssemblies(IDictionary<string, AssemblyEntry> assemblies, string path)
        {
            if (!Directory.Exists(path))
            {
                return;
            }

            foreach (var assemblyPath in Directory.GetFiles(path, "*.dll"))
            {
                var name = Path.GetFileNameWithoutExtension(assemblyPath);
                var entry = new AssemblyEntry();
                entry.Path = assemblyPath;
                assemblies[name] = entry;
            }
        }

        private static string GetAssemblyPath(IEnumerable<string> basePaths, string assemblyName)
        {
            foreach (var basePath in basePaths)
            {
                var assemblyPath = Path.Combine(basePath, assemblyName + ".dll");

                if (File.Exists(assemblyPath))
                {
                    return assemblyPath;
                }
            }

            return null;
        }

        private static Version GetDisplayVersion(NuGetFramework framework)
        {
            // Fix the target framework version due to https://github.com/NuGet/Home/issues/1600, this is relevant
            // when looking up in the reference assembly folder
            return new FrameworkName(framework.DotNetFrameworkName).Version;
        }
    }
}
