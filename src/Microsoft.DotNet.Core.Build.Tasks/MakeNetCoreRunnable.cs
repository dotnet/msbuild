// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Cli.Compiler.Common;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.InternalAbstractions;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.ProjectModel.Compilation;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Cli.Tasks
{
    public class MakeNetCoreRunnable : Task
    {
        [Required]
        public string ProjectPath { get; set; }

        [Required]
        public string Configuration { get; set; }

        [Required]
        public string OutputPath { get; set; }

        [Required]
        public string OutputType { get; set; }

        public string Runtime { get; set; }

        private bool HasRuntimeOutput
        {
            get { return string.Equals("Exe", OutputType, StringComparison.OrdinalIgnoreCase); }
        }

        public override bool Execute()
        {
            BuildWorkspace workspace = BuildWorkspace.Create();

            string framework = null; //TODO: should we take a NuGet framework ?

            IEnumerable<ProjectContext> projectContexts = GetProjectContexts(workspace,
                framework == null ? null : NuGetFramework.Parse(framework),
                Runtime);

            if (!projectContexts.Any())
            {
                Log.LogError($"'{ProjectPath}' cannot be made runnable for '{framework ?? "<no framework provided>"}' '{Runtime ?? "<no runtime provided>"}'");
                return false;
            }

            foreach (ProjectContext projectContext in projectContexts)
            {
                string buildBasePath = null; // TODO: Is there an "Intermediate Directory" property we can take?

                projectContext.ProjectFile.OverrideIsRunnable = HasRuntimeOutput;

                OutputPaths outputPaths = projectContext.GetOutputPaths(Configuration, buildBasePath, OutputPath);
                LibraryExporter libraryExporter = projectContext.CreateExporter(Configuration, buildBasePath);

                Executable executable = new Executable(projectContext, outputPaths, libraryExporter, Configuration);
                executable.MakeCompilationOutputRunnable(skipRuntimeConfig: true);
            }

            return true;
        }

        private IEnumerable<ProjectContext> GetProjectContexts(BuildWorkspace workspace, NuGetFramework framework, string runtime)
        {
            var contexts = workspace.GetProjectContextCollection(ProjectPath)
                .EnsureValid(ProjectPath)
                .FrameworkOnlyContexts;

            contexts = framework == null ?
                contexts :
                contexts.Where(c => Equals(c.TargetFramework, framework));

            var rids = string.IsNullOrEmpty(runtime) ?
                RuntimeEnvironmentRidExtensions.GetAllCandidateRuntimeIdentifiers() :
                new[] { runtime };

            return contexts.Select(c => workspace.GetRuntimeContext(c, rids));
        }
    }
}
