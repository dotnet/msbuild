// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using Microsoft.Build.Execution;
using Microsoft.Build.Graph;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Tools
{
    internal static class HotReloadProfileReader
    {
        public static HotReloadProfile InferHotReloadProfile(ProjectGraph projectGraph, IReporter reporter)
        {
            var queue = new Queue<ProjectGraphNode>(projectGraph.EntryPointNodes);

            ProjectInstance? aspnetCoreProject = null;

            while (queue.Count > 0)
            {
                var currentNode = queue.Dequeue();
                var projectCapability = currentNode.ProjectInstance.GetItems("ProjectCapability");

                foreach (var item in projectCapability)
                {
                    if (item.EvaluatedInclude == "AspNetCore")
                    {
                        aspnetCoreProject = currentNode.ProjectInstance;
                        break;
                    }
                    else if (item.EvaluatedInclude == "WebAssembly")
                    {
                        // We saw a previous project that was AspNetCore. This must he a blazor hosted app.
                        if (aspnetCoreProject is not null && aspnetCoreProject != currentNode.ProjectInstance)
                        {
                            reporter.Verbose($"HotReloadProfile: BlazorHosted. {aspnetCoreProject.FullPath} references BlazorWebAssembly project {currentNode.ProjectInstance.FullPath}.", emoji: "🔥");
                            return HotReloadProfile.BlazorHosted;
                        }

                        reporter.Verbose("HotReloadProfile: BlazorWebAssembly.", emoji: "🔥");
                        return HotReloadProfile.BlazorWebAssembly;
                    }
                }

                foreach (var project in currentNode.ProjectReferences)
                {
                    queue.Enqueue(project);
                }
            }

            reporter.Verbose("HotReloadProfile: Default.", emoji: "🔥");
            return HotReloadProfile.Default;
        }
    }
}
