// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.ProjectModel.Files;
using Microsoft.DotNet.Tools.Pack;
using NuGet;

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
            var ouptutName =  Project.GetCompilerOptions(context.TargetFramework, Configuration).OutputName;

            TryAddOutputFile(context, inputFolder, $"{ouptutName}.pdb");
            TryAddOutputFile(context, inputFolder, $"{ouptutName}.mdb");
        }

        protected override bool GeneratePackage(string nupkg, List<DiagnosticMessage> packDiagnostics)
        {
            var compilerOptions = Project.GetCompilerOptions(
                Project.GetTargetFramework(targetFramework: null).FrameworkName, Configuration);

            if (compilerOptions.CompileInclude == null)
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
            }
            else
            {
                var includeFiles = IncludeFilesResolver.GetIncludeFiles(compilerOptions.CompileInclude, "/", diagnostics: null);
                foreach (var entry in includeFiles)
                {
                    var srcFile = new PhysicalPackageFile
                    {
                        SourcePath = entry.SourcePath,
                        TargetPath = Path.Combine("src", entry.TargetPath)
                    };

                    PackageBuilder.Files.Add(srcFile);
                }
            }

            return base.GeneratePackage(nupkg, packDiagnostics);
        }
    }
}