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

        public override bool Execute()
        {
            LockFile lockFile = new LockFileCache(BuildEngine4).GetLockFile(LockFilePath);

            DependencyContext dependencyContext = new DependencyContextBuilder().Build(
                projectName: AssemblyName,
                projectVersion: AssemblyVersion,
                compilerOptions: null, // TODO: PreserveCompilationContext - https://github.com/dotnet/sdk/issues/11
                lockFile: lockFile,
                framework: TargetFramework == null ? null : NuGetFramework.Parse(TargetFramework),
                runtime: RuntimeIdentifier);

            var writer = new DependencyContextWriter();
            using (var fileStream = File.Create(DepsFilePath))
            {
                writer.Write(dependencyContext, fileStream);
            }

            return true;
        }
    }
}
