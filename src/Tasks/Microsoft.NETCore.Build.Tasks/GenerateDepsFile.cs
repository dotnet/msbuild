// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Extensions.DependencyModel;
using Newtonsoft.Json;
using NuGet.Frameworks;
using NuGet.ProjectModel;

namespace Microsoft.NETCore.Build.Tasks
{
    /// <summary>
    /// Generates the $(project).deps.json file.
    /// </summary>
    public class GenerateDepsFile : Task
    {
        [Required]
        public string LockFilePath { get; set; }

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

        public override bool Execute()
        {
            LockFile lockFile = new LockFileCache(BuildEngine4).GetLockFile(LockFilePath);
            CompilationOptions compilationOptions = CompilationOptionsConverter.ConvertFrom(CompilerOptions);
            SingleProjectInfo mainProject = SingleProjectInfo.Create(AssemblyName, AssemblyVersion, AssemblySatelliteAssemblies);

            DependencyContext dependencyContext = new DependencyContextBuilder().Build(
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

            return true;
        }
    }
}
