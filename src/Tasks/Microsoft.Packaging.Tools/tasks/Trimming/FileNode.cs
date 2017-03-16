// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace Microsoft.DotNet.Build.Tasks
{
    internal class FileNode : IIsIncluded
    {
        IEnumerable<FileNode> _dependencies;

        internal const string NuGetPackageIdMetadata = "NuGetPackageId";
        internal const string NuGetPackageVersionMetadata = "NuGetPackageVersion";
        internal const string AdditionalDependenciesFileSuffix = ".dependencies";

        public FileNode(ITaskItem fileItem, IDictionary<string, NuGetPackageNode> allPackages)
        {
            Name = fileItem.GetMetadata("Filename") + fileItem.GetMetadata("Extension");
            OriginalItem = fileItem;
            PackageId = fileItem.GetMetadata(NuGetPackageIdMetadata);
            SourceFile = fileItem.GetMetadata("FullPath");

            if (string.IsNullOrEmpty(PackageId))
            {
                PackageId = NuGetUtilities.GetPackageIdFromSourcePath(SourceFile);
            }

            if (!string.IsNullOrEmpty(PackageId))
            {
                NuGetPackageNode package;

                if (!allPackages.TryGetValue(PackageId, out package))
                {
                    // some file came from a package that wasn't found in the lock file
                }
                else
                {
                    Package = package;
                    Package.Files.Add(this);
                }
            }
        }

        public bool IsIncluded { get; set; }
        public string Name { get; }
        public ITaskItem OriginalItem { get; }
        public string PackageId { get; }
        public string SourceFile { get; }
        public NuGetPackageNode Package { get; }
        public IEnumerable<FileNode> Dependencies { get { return _dependencies; } }

        public void PopulateDependencies(Dictionary<string, FileNode> allFiles, bool preferNativeImage, ILog log)
        {
            List<FileNode> dependencies = new List<FileNode>();

            try
            {
                using (var peReader = new PEReader(new FileStream(SourceFile, FileMode.Open, FileAccess.Read, FileShare.Delete | FileShare.Read)))
                {
                    if (peReader.HasMetadata)
                    {
                        var reader = peReader.GetMetadataReader();
                        foreach (var handle in reader.AssemblyReferences)
                        {
                            var reference = reader.GetAssemblyReference(handle);
                            var referenceName = reader.GetString(reference.Name);

                            var referenceCandidates = preferNativeImage ? 
                                new[] { referenceName + ".ni.dll", referenceName + ".dll" } :
                                new[] { referenceName + ".dll", referenceName + ".ni.dll" };

                            FileNode referencedFile = null;
                            foreach (var referenceCandidate in referenceCandidates)
                            {
                                if (allFiles.TryGetValue(referenceCandidate, out referencedFile))
                                {
                                    break;
                                }
                            }

                            if (referencedFile != null)
                            {
                                dependencies.Add(referencedFile);
                            }
                            else
                            {
                                // static dependency that wasn't satisfied, this can happen if folks use 
                                // lightup code to guard the static dependency.
                                // this can also happen when referencing a package that isn't implemented
                                // on this platform but don't fail the build here
                                log.LogMessage(LogImportance.Low, $"Could not locate assembly dependency {referenceName} of {SourceFile}.");
                            }
                        }

                        for (int i = 1, count = reader.GetTableRowCount(TableIndex.ModuleRef); i <= count; i++)
                        {
                            var moduleRef = reader.GetModuleReference(MetadataTokens.ModuleReferenceHandle(i));
                            var moduleName = reader.GetString(moduleRef.Name);

                            var moduleRefCandidates = new[] { moduleName, moduleName + ".dll", moduleName + ".so", moduleName + ".dylib" };

                            FileNode referencedNativeFile = null;
                            foreach (var moduleRefCandidate in moduleRefCandidates)
                            {
                                if (allFiles.TryGetValue(moduleRefCandidate, out referencedNativeFile))
                                {
                                    break;
                                }
                            }

                            if (referencedNativeFile != null)
                            {
                                dependencies.Add(referencedNativeFile);
                            }
                            else
                            {
                                // DLLImport that wasn't satisfied
                            }
                        }
                    }
                }
            }
            catch(BadImageFormatException)
            {
                // not a PE
            }

            // allow for components to specify their dependencies themselves, by placing a file next to their source file.
            var additionalDependenciesFile = SourceFile + AdditionalDependenciesFileSuffix;

            if (File.Exists(additionalDependenciesFile))
            {
                foreach(var additionalDependency in File.ReadAllLines(additionalDependenciesFile))
                {
                    if (additionalDependency.Length == 0 || additionalDependency[0] == '#')
                    {
                        continue;
                    }

                    FileNode additionalDependencyFile;
                    if (allFiles.TryGetValue(additionalDependency, out additionalDependencyFile))
                    {
                        dependencies.Add(additionalDependencyFile);
                    }
                    else
                    {
                        log.LogMessage(LogImportance.Low, $"Could not locate explicit dependency {additionalDependency} of {SourceFile} specified in {additionalDependenciesFile}.");
                    }
                }
            }

            _dependencies = dependencies;
        }
    }
}
