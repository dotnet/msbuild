// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.Tools.Compiler;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Tools.Build
{
    public class BuildCommand
    {
        public static int Run(string[] args) => Run(args, null);

        public static int Run(string[] args, BuildWorkspace workspace)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            try
            {
                var app = new BuildCommandApp(
                    "dotnet build",
                    ".NET Builder",
                    "Builder for the .NET Platform. It performs incremental compilation if it's safe to do so. Otherwise it delegates to dotnet-compile which performs non-incremental compilation",
                    workspace);
                return app.Execute(OnExecute, args);
            }
            catch (Exception ex)
            {
#if DEBUG
                Console.Error.WriteLine(ex);
#else
                Console.Error.WriteLine(ex.Message);
#endif
                return 1;
            }
        }

        private static bool OnExecute(IEnumerable<string> files, IEnumerable<NuGetFramework> frameworks, BuildCommandApp args)
        {
            var builderCommandApp = args;
            var graphCollector = new ProjectGraphCollector(
                !builderCommandApp.ShouldSkipDependencies,
                (project, target) => args.Workspace.GetProjectContext(project, target));

            IEnumerable<ProjectContext> contexts;
            using (PerfTrace.Current.CaptureTiming(string.Empty, nameof(ResolveRootContexts)))
            {
                contexts = ResolveRootContexts(files, frameworks, args);
            }

            ProjectGraphNode[] graph;
            using (PerfTrace.Current.CaptureTiming(string.Empty, "Collect graph"))
            {
                graph = graphCollector.Collect(contexts).ToArray();
            }
            var builder = new DotNetProjectBuilder(builderCommandApp);
            return builder.Build(graph).ToArray().All(r => r != CompilationResult.Failure);
        }

        private static IEnumerable<ProjectContext> ResolveRootContexts(
            IEnumerable<string> files,
            IEnumerable<NuGetFramework> frameworks,
            BuildCommandApp args)
        {
            List<Task<ProjectContext>> tasks = new List<Task<ProjectContext>>();

            foreach (var file in files)
            {
                Project project;
                using (PerfTrace.Current.CaptureTiming(file, "Loading project.json"))
                {
                    project = args.Workspace.GetProject(file);
                }
                var projectFrameworks = project.GetTargetFrameworks().Select(f => f.FrameworkName);
                if (!projectFrameworks.Any())
                {
                    throw new InvalidOperationException(
                        $"Project '{file}' does not have any frameworks listed in the 'frameworks' section.");
                }
                IEnumerable<NuGetFramework> selectedFrameworks;
                if (frameworks != null)
                {
                    var unsupportedByProject = frameworks.Where(f => !projectFrameworks.Contains(f));
                    if (unsupportedByProject.Any())
                    {
                        throw new InvalidOperationException(
                            $"Project \'{file}\' does not support framework: {string.Join(", ", unsupportedByProject.Select(fx => fx.DotNetFrameworkName))}.");
                    }

                    selectedFrameworks = frameworks;
                }
                else
                {
                    selectedFrameworks = projectFrameworks;
                }

                foreach (var framework in selectedFrameworks)
                {
                    tasks.Add(Task.Run(() => args.Workspace.GetProjectContext(file, framework)));
                }
            }
            using (PerfTrace.Current.CaptureTiming(string.Empty, "Waiting for project contexts to finish loading"))
            {
                return Task.WhenAll(tasks).GetAwaiter().GetResult();
            }
        }
    }
}
