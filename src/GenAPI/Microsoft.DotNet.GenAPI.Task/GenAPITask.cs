// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.NET.Build.Tasks;
using Microsoft.DotNet.ApiSymbolExtensions.Logging;

namespace Microsoft.DotNet.GenAPI.Task
{
    /// <summary>
    /// MSBuild task frontend for the Roslyn-based GenAPI.
    /// </summary>
    public class GenAPITask : TaskBase
    {
        /// <summary>
        /// The path to one or more assemblies or directories with assemblies.
        /// </summary>
        [Required]
        public string[]? Assemblies { get; set; }

        /// <summary>
        /// Paths to assembly references or their underlying directories for a specific target framework in the package.
        /// </summary>
        public string[]? AssemblyReferences { get; set; }

        /// <summary>
        /// Output path. Default is the console. Can specify an existing directory as well and
        /// then a file will be created for each assembly with the matching name of the assembly.
        /// </summary>
        public string? OutputPath { get; set; }

        /// <summary>
        /// Specify a file with an alternate header content to prepend to output.
        /// </summary>
        public string? HeaderFile { get; set; }

        /// <summary>
        /// Method bodies should throw PlatformNotSupportedException.
        /// </summary>
        public string? ExceptionMessage { get; set; }

        /// <summary>
        /// The path to one or more attribute exclusion files with types in DocId format.
        /// </summary>
        public string[]? ExcludeAttributesFiles { get; set; }

        /// <summary>
        /// Include internal API's. Default is false.
        /// </summary>
        public bool IncludeVisibleOutsideOfAssembly { get; set; }

        protected override void ExecuteCore()
        {
            GenAPIApp.Run(new MSBuildLog(Log), new GenAPIApp.Context(
                Assemblies!,
                AssemblyReferences,
                OutputPath,
                HeaderFile,
                ExceptionMessage,
                ExcludeAttributesFiles,
                IncludeVisibleOutsideOfAssembly
            ));
        }
    }
}
