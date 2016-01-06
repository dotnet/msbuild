// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.ProjectModel;
using NuGet;
using System.Collections.Generic;
using System.IO;
using Microsoft.DotNet.Tools.Pack;

namespace Microsoft.DotNet.Tools.Compiler
{
    public class SymbolPackageGenerator: PackageGenerator
    {
        public SymbolPackageGenerator(Project project, string configuration, ArtifactPathsCalculator artifactPathsCalculator) 
            : base(project, configuration, artifactPathsCalculator)
        {
        }

        protected override string GetPackageName()
        {
            return $"{Project.Name}.{Project.Version}.symbols";
        }

        protected override void ProcessContext(ProjectContext context)
        {
            base.ProcessContext(context);

            var inputFolder = ArtifactPathsCalculator.InputPathForContext(context);
            TryAddOutputFile(context, inputFolder, $"{Project.Name}.pdb");
            TryAddOutputFile(context, inputFolder, $"{Project.Name}.mdb");
        }

        protected override bool GeneratePackage(string nupkg, List<DiagnosticMessage> packDiagnostics)
        {
            foreach (var path in Project.Files.SourceFiles)
            {
                var srcFile = new PhysicalPackageFile
                {
                    SourcePath = path,
                    TargetPath = Path.Combine("src", Common.PathUtility.GetRelativePath(Project.ProjectDirectory, path))
                };

                PackageBuilder.Files.Add(srcFile);
            }
            return base.GeneratePackage(nupkg, packDiagnostics);
        }
    }
}