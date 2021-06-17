// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.ExternalAccess.Watch.Api;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Tools
{
    internal static class CompilationWorkspaceProvider
    {
        public static Task<(Solution, WatchHotReloadService)> CreateWorkspaceAsync(string projectPath, IReporter reporter, CancellationToken cancellationToken)
        {
            var taskCompletionSource = new TaskCompletionSource<(Solution, WatchHotReloadService)>(TaskCreationOptions.RunContinuationsAsynchronously);
            try
            {
                CreateProject(taskCompletionSource, projectPath, reporter, cancellationToken);
            }
            catch (Exception ex)
            {
                taskCompletionSource.TrySetException(ex);
            }

            return taskCompletionSource.Task;
        }

        static async void CreateProject(TaskCompletionSource<(Solution, WatchHotReloadService)> taskCompletionSource, string projectPath, IReporter reporter, CancellationToken cancellationToken)
        {
            var workspace = MSBuildWorkspace.Create();

            workspace.WorkspaceFailed += (_sender, diag) =>
            {
                if (diag.Diagnostic.Kind == WorkspaceDiagnosticKind.Warning)
                {
                    reporter.Verbose($"MSBuildWorkspace warning: {diag.Diagnostic}");
                }
                else
                {
                    taskCompletionSource.TrySetException(new InvalidOperationException($"Failed to create MSBuildWorkspace: {diag.Diagnostic}"));
                }
            };

            await workspace.OpenProjectAsync(projectPath, cancellationToken: cancellationToken);
            var currentSolution = workspace.CurrentSolution;
            var hotReloadService = new WatchHotReloadService(workspace.Services, ImmutableArray.Create("Baseline", "AddDefinitionToExistingType", "NewTypeDefinition"));
            await hotReloadService.StartSessionAsync(currentSolution, cancellationToken);

            // Read the documents to memory
            await Task.WhenAll(
                currentSolution.Projects.SelectMany(p => p.Documents.Concat(p.AdditionalDocuments)).Select(d => d.GetTextAsync(cancellationToken)));

            // Warm up the compilation. This would help make the deltas for first edit appear much more quickly
            foreach (var project in currentSolution.Projects)
            {
                await project.GetCompilationAsync(cancellationToken);
            }

            taskCompletionSource.TrySetResult((currentSolution, hotReloadService));
        }
    }
}
