// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Extensions.DependencyModel;
using Newtonsoft.Json;
using NuGet.Frameworks;
using NuGet.ProjectModel;

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// Generates the $(project).deps.json file.
    /// </summary>
    public class GenerateDepsFile : TaskBase
    {
        [Required]
        public string ProjectPath { get; set; }

        [Required]
        public string AssetsFilePath { get; set; }

        [Required]
        public string DepsFilePath { get; set; }

        [Required]
        public string TargetFramework { get; set; }

        public string RuntimeIdentifier { get; set; }

        [Required]
        public string AssemblyName { get; set; }

        [Required]
        public string AssemblyVersion { get; set; }

        [Required]
        public ITaskItem[] AssemblySatelliteAssemblies { get; set; }

        public ITaskItem CompilerOptions { get; set; }

        public ITaskItem[] PrivateAssetsPackageReferences { get; set; }

        protected override void ExecuteCore()
        {
            LockFile lockFile = new LockFileCache(BuildEngine4).GetLockFile(AssetsFilePath);
            CompilationOptions compilationOptions = CompilationOptionsConverter.ConvertFrom(CompilerOptions);
            SingleProjectInfo mainProject = SingleProjectInfo.Create(ProjectPath, AssemblyName, AssemblyVersion, AssemblySatelliteAssemblies);
            IEnumerable<string> privateAssets = PackageReferenceConverter.GetPackageIds(PrivateAssetsPackageReferences);

            DependencyContext dependencyContext = new DependencyContextBuilder()
                .WithPrivateAssets(privateAssets)
                .Build(
                    mainProject,
                    compilationOptions,
                    lockFile,
                    TargetFramework == null ? null : NuGetFramework.Parse(TargetFramework),
                    RuntimeIdentifier);

            var writer = new DependencyContextWriter();
            using (var fileStream = File.Create(DepsFilePath))
            {
                writer.Write(dependencyContext, fileStream);
            }
        }
    }
}
