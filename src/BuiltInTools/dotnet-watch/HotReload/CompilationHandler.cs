// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.ExternalAccess.Watch.Api;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Tools
{
    internal class CompilationHandler : IDisposable
    {
        private readonly IReporter _reporter;
        private Task<(Solution, WatchHotReloadService)>? _initializeTask;
        private Solution? _currentSolution;
        private WatchHotReloadService? _hotReloadService;
        private IDeltaApplier? _deltaApplier;

        public CompilationHandler(IReporter reporter)
        {
            _reporter = reporter;
        }

        public async ValueTask InitializeAsync(DotNetWatchContext context, CancellationToken cancellationToken)
        {
            if (_deltaApplier is null)
            {
                _deltaApplier = context.DefaultLaunchSettingsProfile.HotReloadProfile switch
                {
                    "blazorwasm" => new BlazorWebAssemblyDeltaApplier(_reporter),
                    "blazorwasmhosted" => new BlazorWebAssemblyHostedDeltaApplier(_reporter),
                    _ => new AspNetCoreDeltaApplier(_reporter),
                };
            }

            await _deltaApplier.InitializeAsync(context, cancellationToken);

            if (context.Iteration == 0)
            {
                var instance = MSBuildLocator.QueryVisualStudioInstances().First();

                _reporter.Verbose($"Using MSBuild at '{instance.MSBuildPath}' to load projects.");
                MSBuildLocator.RegisterInstance(instance);
            }
            else if (_currentSolution is not null)
            {
                _currentSolution.Workspace.Dispose();
                _currentSolution = null;
            }

            _initializeTask = Task.Run(() => CompilationWorkspaceProvider.CreateWorkspaceAsync(context.FileSet.Project.ProjectPath, _reporter, cancellationToken), cancellationToken);

            return;
        }

        public async ValueTask<bool> TryHandleFileChange(DotNetWatchContext context, FileItem file, CancellationToken cancellationToken)
        {
            HotReloadEventSource.Log.HotReloadStart(HotReloadEventSource.StartType.CompilationHandler);
            if (!file.FilePath.EndsWith(".cs", StringComparison.Ordinal) &&
                !file.FilePath.EndsWith(".razor", StringComparison.Ordinal) &&
                !file.FilePath.EndsWith(".cshtml", StringComparison.Ordinal))
            {
                HotReloadEventSource.Log.HotReloadEnd(HotReloadEventSource.StartType.CompilationHandler);
                return false;
            }

            if (!await EnsureSolutionInitializedAsync())
            {
                HotReloadEventSource.Log.HotReloadEnd(HotReloadEventSource.StartType.CompilationHandler);
                return false;
            }
            Debug.Assert(_hotReloadService != null);
            Debug.Assert(_currentSolution != null);
            Debug.Assert(_deltaApplier != null);

            Solution? updatedSolution = null;
            ProjectId updatedProjectId;
            if (_currentSolution.Projects.SelectMany(p => p.Documents).FirstOrDefault(d => string.Equals(d.FilePath, file.FilePath, StringComparison.OrdinalIgnoreCase)) is Document documentToUpdate)
            {
                var sourceText = await GetSourceTextAsync(file.FilePath);
                updatedSolution = documentToUpdate.WithText(sourceText).Project.Solution;
                updatedProjectId = documentToUpdate.Project.Id;
            }
            else if (_currentSolution.Projects.SelectMany(p => p.AdditionalDocuments).FirstOrDefault(d => string.Equals(d.FilePath, file.FilePath, StringComparison.OrdinalIgnoreCase)) is AdditionalDocument additionalDocument)
            {
                var sourceText = await GetSourceTextAsync(file.FilePath);
                updatedSolution = _currentSolution.WithAdditionalDocumentText(additionalDocument.Id, sourceText, PreservationMode.PreserveValue);
                updatedProjectId = additionalDocument.Project.Id;
            }
            else
            {
                _reporter.Verbose($"Could not find document with path {file.FilePath} in the workspace.");
                HotReloadEventSource.Log.HotReloadEnd(HotReloadEventSource.StartType.CompilationHandler);
                return false;
            }

            var (updates, hotReloadDiagnostics) = await _hotReloadService.EmitSolutionUpdateAsync(updatedSolution, cancellationToken);

            if (hotReloadDiagnostics.IsDefaultOrEmpty && updates.IsDefaultOrEmpty)
            {
                // It's possible that there are compilation errors which prevented the solution update
                // from being updated. Let's look to see if there are compilation errors.
                var diagnostics = GetDiagnostics(updatedSolution, cancellationToken);
                if (diagnostics.IsDefaultOrEmpty)
                {
                    await _deltaApplier.Apply(context, file.FilePath, updates, cancellationToken);
                }
                else
                {
                    await _deltaApplier.ReportDiagnosticsAsync(context, diagnostics, cancellationToken);
                }

                HotReloadEventSource.Log.HotReloadEnd(HotReloadEventSource.StartType.CompilationHandler);
                // Even if there were diagnostics, continue treating this as a success
                return true;
            }

            if (!hotReloadDiagnostics.IsDefaultOrEmpty)
            {
                // Rude edit.
                _reporter.Output("Unable to apply hot reload because of a rude edit. Rebuilding the app...");
                foreach (var diagnostic in hotReloadDiagnostics)
                {
                    _reporter.Verbose(CSharpDiagnosticFormatter.Instance.Format(diagnostic));
                }

                HotReloadEventSource.Log.HotReloadEnd(HotReloadEventSource.StartType.CompilationHandler);
                return false;
            }

            _currentSolution = updatedSolution;

            var applyState = await _deltaApplier.Apply(context, file.FilePath, updates, cancellationToken);
            HotReloadEventSource.Log.HotReloadEnd(HotReloadEventSource.StartType.CompilationHandler);
            return applyState;
        }

        private ImmutableArray<string> GetDiagnostics(Solution solution, CancellationToken cancellationToken)
        {
            var @lock = new object();
            var builder = ImmutableArray<string>.Empty;
            Parallel.ForEach(solution.Projects, project =>
            {
                if (!project.TryGetCompilation(out var compilation))
                {
                    return;
                }

                var compilationDiagnostics = compilation.GetDiagnostics(cancellationToken);
                if (compilationDiagnostics.IsDefaultOrEmpty)
                {
                    return;
                }

                var projectDiagnostics = ImmutableArray<string>.Empty;
                foreach (var item in compilationDiagnostics)
                {
                    if (item.Severity == DiagnosticSeverity.Error)
                    {
                        var diagnostic = CSharpDiagnosticFormatter.Instance.Format(item);
                        _reporter.Output(diagnostic);
                        projectDiagnostics = projectDiagnostics.Add(diagnostic);
                    }
                }

                lock (@lock)
                {
                    builder = builder.AddRange(projectDiagnostics);
                }
            });

            return builder;
        }

        private async ValueTask<bool> EnsureSolutionInitializedAsync()
        {
            if (_currentSolution != null)
            {
                return true;
            }

            if (_initializeTask is null)
            {
                return false;
            }

            try
            {
                (_currentSolution, _hotReloadService) = await _initializeTask;
                return true;
            }
            catch (Exception ex)
            {
                _reporter.Warn(ex.Message);
                return false;
            }
        }

        private async ValueTask<SourceText> GetSourceTextAsync(string filePath)
        {
            for (var attemptIndex = 0; attemptIndex < 6; attemptIndex++)
            {
                try
                {
                    using var stream = File.OpenRead(filePath);
                    return SourceText.From(stream, Encoding.UTF8);
                }
                catch (IOException) when (attemptIndex < 5)
                {
                    await Task.Delay(20 * (attemptIndex + 1));
                }
            }

            Debug.Fail("This shouldn't happen.");
            return null;
        }

        public void Dispose()
        {
            _hotReloadService?.EndSession();
            if (_deltaApplier is not null)
            {
                _deltaApplier.Dispose();
            }

            if (_currentSolution is not null)
            {
                _currentSolution.Workspace.Dispose();
            }
        }
    }
}
