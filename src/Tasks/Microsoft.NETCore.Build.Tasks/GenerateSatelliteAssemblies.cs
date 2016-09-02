// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyModel;

namespace Microsoft.NETCore.Build.Tasks
{
    /// <summary>
    /// Generates the $(project).deps.json file.
    /// </summary>
    public class GenerateSatelliteAssemblies : Task
    {
        [Required]
        public ITaskItem[] EmbedResources { get; set; }

        [Output]
        [Required]
        public ITaskItem OutputAssembly { get; set; }

        [Required]
        public string Culture { get; set; }

        [Required]
        public string[] References { get; set; }

        public string Version { get; set; }

        public override bool Execute()
        {
            var resourceDescriptions = new List<ResourceDescription>();

            foreach (var input in EmbedResources)
            {
                var fileName = input.GetMetadata("Filename");
                var fullPath = input.GetMetadata("FullPath");
                var fileInfo = new FileInfo(fullPath);
                resourceDescriptions.Add(new ResourceDescription(fileName, () => fileInfo.OpenRead(), true));
            }

            var compilationOptions = new CSharpCompilationOptions(outputKind: OutputKind.DynamicallyLinkedLibrary);
            var compilation = CSharpCompilation.Create(OutputAssembly.GetMetadata("Filename"),
                references: References.Select(reference => MetadataReference.CreateFromFile(reference)),
                options: compilationOptions);

            var metadata = new AssemblyInfoOptions
            {
                Culture = Culture,
                AssemblyVersion = Version,
            };

            var cs = AssemblyInfoFileGenerator.GenerateCSharp(metadata, Enumerable.Empty<string>());            
            compilation = compilation.AddSyntaxTrees(new[]
            {
                CSharpSyntaxTree.ParseText(cs)
            });

            var satelliteAssembly = OutputAssembly.GetMetadata("FullPath");
            using (var outputStream = new FileInfo(satelliteAssembly).Create())
            {
                var result = compilation.Emit(outputStream, manifestResources: resourceDescriptions);

                if (!result.Success)
                {
                    Log.LogError($"Errors ocurred when emitting satellite assembly - {satelliteAssembly}");
                    foreach (var diagnostic in result.Diagnostics)
                    {
                        Log.LogError(diagnostic.ToString());
                    }                    
                }
            }

            return !Log.HasLoggedErrors;
        }
    }
}
