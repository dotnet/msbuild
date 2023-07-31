// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.ExternalAccess.Watch.Api;
using Microsoft.CodeAnalysis.Host;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Tools
{
    internal sealed class CompilationHandler : IDisposable
    {
        private readonly IReporter _reporter;
        private Task<WatchHotReloadService>? _sessionTask;
        private Solution? _currentSolution;
        private WatchHotReloadService? _hotReloadService;
        private DeltaApplier? _deltaApplier;
        private MSBuildWorkspace? _workspace;

        public CompilationHandler(IReporter reporter)
        {
            _reporter = reporter;
        }

        public void Dispose()
        {
            _hotReloadService?.EndSession();
            _deltaApplier?.Dispose();
            _workspace?.Dispose();
        }

        public async Task InitializeAsync(DotNetWatchContext context, CancellationToken cancellationToken)
        {
            Debug.Assert(context.ProjectGraph is not null);
            Debug.Assert(context.FileSet?.Project is not null);

            if (_deltaApplier is null)
            {
                var hotReloadProfile = HotReloadProfileReader.InferHotReloadProfile(context.ProjectGraph, _reporter);
                _deltaApplier = hotReloadProfile switch
                {
                    HotReloadProfile.BlazorWebAssembly => new BlazorWebAssemblyDeltaApplier(_reporter),
                    HotReloadProfile.BlazorHosted => new BlazorWebAssemblyHostedDeltaApplier(_reporter),
                    _ => new DefaultDeltaApplier(_reporter),
                };
            }

            _deltaApplier.Initialize(context, cancellationToken);

            if (_workspace is not null)
            {
                _workspace.Dispose();
            }

            _workspace = MSBuildWorkspace.Create();

            _workspace.WorkspaceFailed += (_sender, diag) =>
            {
                // Errors reported here are not fatal, an exception would be thrown for fatal issues.
                _reporter.Verbose($"MSBuildWorkspace warning: {diag.Diagnostic}");
            };

            var project = await _workspace.OpenProjectAsync(context.FileSet.Project.ProjectPath, cancellationToken: cancellationToken);

            _currentSolution = project.Solution;

            _sessionTask = StartSessionAsync(
                _workspace.Services,
                project.Solution,
                _deltaApplier.GetApplyUpdateCapabilitiesAsync(context, cancellationToken),
                _reporter,
                cancellationToken);

            PrepareCompilationsAsync(project.Solution, cancellationToken);
        }

        private static void PrepareCompilationsAsync(Solution solution, CancellationToken cancellationToken)
        {
            // Warm up the compilation. This would help make the deltas for first edit appear much more quickly
            foreach (var project in solution.Projects)
            {
                // fire and forget:
                _ = project.GetCompilationAsync(cancellationToken);
            }
        }

        private static async Task<WatchHotReloadService> StartSessionAsync(
            HostWorkspaceServices services,
            Solution initialSolution,
            Task<ImmutableArray<string>> hotReloadCapabilitiesTask,
            IReporter reporter,
            CancellationToken cancellationToken)
        {
            ImmutableArray<string> hotReloadCapabilities;
            try
            {
                hotReloadCapabilities = await hotReloadCapabilitiesTask;
            }
            catch (Exception ex)
            {
                throw new ApplicationException("Failed to read Hot Reload capabilities: " + ex.Message, ex);
            }

            reporter.Verbose($"Hot reload capabilities: {string.Join(" ", hotReloadCapabilities)}.", emoji: "🔥");

            var hotReloadService = new WatchHotReloadService(services, hotReloadCapabilities);
            await hotReloadService.StartSessionAsync(initialSolution, cancellationToken);
            return hotReloadService;
        }

        public async ValueTask<bool> TryHandleFileChange(DotNetWatchContext context, FileItem[] files, CancellationToken cancellationToken)
        {
            HotReloadEventSource.Log.HotReloadStart(HotReloadEventSource.StartType.CompilationHandler);
            var compilationFiles = files.Where(static file =>
                file.FilePath.EndsWith(".cs", StringComparison.Ordinal) ||
                file.FilePath.EndsWith(".razor", StringComparison.Ordinal) ||
                file.FilePath.EndsWith(".cshtml", StringComparison.Ordinal))
                .ToArray();

            if (compilationFiles.Length == 0)
            {
                HotReloadEventSource.Log.HotReloadEnd(HotReloadEventSource.StartType.CompilationHandler);
                return false;
            }

            if (!await EnsureSessionStartedAsync())
            {
                HotReloadEventSource.Log.HotReloadEnd(HotReloadEventSource.StartType.CompilationHandler);
                return false;
            }

            Debug.Assert(_hotReloadService != null);
            Debug.Assert(_currentSolution != null);
            Debug.Assert(_deltaApplier != null);

            var updatedSolution = _currentSolution;

            var foundFiles = false;
            foreach (var file in compilationFiles)
            {
                if (updatedSolution.Projects.SelectMany(p => p.Documents).FirstOrDefault(d => string.Equals(d.FilePath, file.FilePath, StringComparison.OrdinalIgnoreCase)) is Document documentToUpdate)
                {
                    var sourceText = await GetSourceTextAsync(file.FilePath);
                    updatedSolution = documentToUpdate.WithText(sourceText).Project.Solution;
                    foundFiles = true;
                }
                else if (updatedSolution.Projects.SelectMany(p => p.AdditionalDocuments).FirstOrDefault(d => string.Equals(d.FilePath, file.FilePath, StringComparison.OrdinalIgnoreCase)) is AdditionalDocument additionalDocument)
                {
                    var sourceText = await GetSourceTextAsync(file.FilePath);
                    updatedSolution = updatedSolution.WithAdditionalDocumentText(additionalDocument.Id, sourceText, PreservationMode.PreserveValue);
                    foundFiles = true;
                }
                else
                {
                    _reporter.Verbose($"Could not find document with path {file.FilePath} in the workspace.");
                }
            }

            if (!foundFiles)
            {
                HotReloadEventSource.Log.HotReloadEnd(HotReloadEventSource.StartType.CompilationHandler);
                return false;
            }

            var (updates, hotReloadDiagnostics) = await _hotReloadService.EmitSolutionUpdateAsync(updatedSolution, cancellationToken);
            // hotReloadDiagnostics currently includes semantic Warnings and Errors for types being updated. We want to limit rude edits to the class
            // of unrecoverable errors that a user cannot fix and requires an app rebuild.
            var rudeEdits = hotReloadDiagnostics.RemoveAll(d => d.Severity == DiagnosticSeverity.Warning || !d.Descriptor.Id.StartsWith("ENC", StringComparison.Ordinal));

            if (rudeEdits.IsEmpty && updates.IsEmpty)
            {
                var compilationErrors = GetCompilationErrors(updatedSolution, cancellationToken);
                if (compilationErrors.IsEmpty)
                {
                    _reporter.Output("No hot reload changes to apply.");
                }

                // report or clear diagnostics in the browser UI
                if (context.BrowserRefreshServer != null)
                {
                    _reporter.Verbose($"Updating diagnostics in the browser.");
                    if (compilationErrors.IsEmpty)
                    {
                        await context.BrowserRefreshServer.SendJsonSerlialized(new AspNetCoreHotReloadApplied(), cancellationToken);
                    }
                    else
                    {
                        await context.BrowserRefreshServer.SendJsonSerlialized(new HotReloadDiagnostics { Diagnostics = compilationErrors }, cancellationToken);
                    }
                }

                HotReloadEventSource.Log.HotReloadEnd(HotReloadEventSource.StartType.CompilationHandler);

                // Return true so that the watcher continues to keep the current hot reload session alive.
                // If there were errors, this allows the user to fix errors and continue working on the running app.
                return true;
            }

            if (!rudeEdits.IsEmpty)
            {
                // Rude edit.
                _reporter.Output("Unable to apply hot reload because of a rude edit.");
                foreach (var diagnostic in hotReloadDiagnostics)
                {
                    _reporter.Verbose(CSharpDiagnosticFormatter.Instance.Format(diagnostic));
                }

                HotReloadEventSource.Log.HotReloadEnd(HotReloadEventSource.StartType.CompilationHandler);
                return false;
            }

            _currentSolution = updatedSolution;

            var applyStatus = await _deltaApplier.Apply(context, updates, cancellationToken) != ApplyStatus.Failed;
            _reporter.Verbose($"Received {(applyStatus ? "successful" : "failed")} apply from delta applier.");
            HotReloadEventSource.Log.HotReloadEnd(HotReloadEventSource.StartType.CompilationHandler);
            if (applyStatus)
            {
                _reporter.Output($"Hot reload of changes succeeded.", emoji: "🔥");

                // BrowserRefreshServer will be null in non web projects or if we failed to establish a websocket connection
                if (context.BrowserRefreshServer != null)
                {
                    _reporter.Verbose($"Refreshing browser.");
                    await context.BrowserRefreshServer.SendJsonSerlialized(new AspNetCoreHotReloadApplied(), cancellationToken);
                }
            }

            return applyStatus;
        }

        private readonly struct HotReloadDiagnostics
        {
            public string Type => "HotReloadDiagnosticsv1";

            public IEnumerable<string> Diagnostics { get; init; }
        }

        private readonly struct AspNetCoreHotReloadApplied
        {
            public string Type => "AspNetCoreHotReloadApplied";
        }

        private ImmutableArray<string> GetCompilationErrors(Solution solution, CancellationToken cancellationToken)
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
                if (compilationDiagnostics.IsEmpty)
                {
                    return;
                }

                var projectDiagnostics = ImmutableArray<string>.Empty;
                foreach (var item in compilationDiagnostics)
                {
                    if (item.Severity == DiagnosticSeverity.Error)
                    {
                        var diagnostic = CSharpDiagnosticFormatter.Instance.Format(item);
                        _reporter.Output("\x1B[40m\x1B[31m" + diagnostic);
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

        private async ValueTask<bool> EnsureSessionStartedAsync()
        {
            if (_sessionTask is null)
            {
                return false;
            }

            try
            {
                _hotReloadService = await _sessionTask;
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
            var zeroLengthRetryPerformed = false;
            for (var attemptIndex = 0; attemptIndex < 6; attemptIndex++)
            {
                try
                {
                    // File.OpenRead opens the file with FileShare.Read. This may prevent IDEs from saving file
                    // contents to disk
                    SourceText sourceText;
                    using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        sourceText = SourceText.From(stream, Encoding.UTF8);
                    }

                    if (!zeroLengthRetryPerformed && sourceText.Length == 0)
                    {
                        zeroLengthRetryPerformed = true;

                        // VSCode (on Windows) will sometimes perform two separate writes when updating a file on disk.
                        // In the first update, it clears the file contents, and in the second, it writes the intended
                        // content.
                        // It's atypical that a file being watched for hot reload would be empty. We'll use this as a
                        // hueristic to identify this case and perform an additional retry reading the file after a delay.
                        await Task.Delay(20);

                        using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        sourceText = SourceText.From(stream, Encoding.UTF8);
                    }

                    return sourceText;
                }
                catch (IOException) when (attemptIndex < 5)
                {
                    await Task.Delay(20 * (attemptIndex + 1));
                }
            }

            Debug.Fail("This shouldn't happen.");
            return null;
        }
    }
}
