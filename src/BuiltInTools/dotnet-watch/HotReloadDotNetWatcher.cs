// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.Watcher.Internal;
using Microsoft.DotNet.Watcher.Tools;
using Microsoft.Extensions.Tools.Internal;
using IReporter = Microsoft.Extensions.Tools.Internal.IReporter;

namespace Microsoft.DotNet.Watcher
{
    public class HotReloadDotNetWatcher : IAsyncDisposable
    {
        private readonly IReporter _reporter;
        private readonly IConsole _console;
        private readonly ProcessRunner _processRunner;
        private readonly DotNetWatchOptions _dotNetWatchOptions;
        private readonly IWatchFilter[] _filters;
        private readonly RudeEditDialog _rudeEditDialog;

        public HotReloadDotNetWatcher(IReporter reporter, IFileSetFactory fileSetFactory, DotNetWatchOptions dotNetWatchOptions, IConsole console)
        {
            Ensure.NotNull(reporter, nameof(reporter));

            _reporter = reporter;
            _processRunner = new ProcessRunner(reporter);
            _dotNetWatchOptions = dotNetWatchOptions;
            _console = console;

            _filters = new IWatchFilter[]
            {
                new MSBuildEvaluationFilter(fileSetFactory),
                new DotNetBuildFilter(_processRunner, _reporter),
                new LaunchBrowserFilter(dotNetWatchOptions),
                new BrowserRefreshFilter(dotNetWatchOptions, _reporter),
            };
            _rudeEditDialog = new(reporter, _console);
        }

        public async Task WatchAsync(DotNetWatchContext context, CancellationToken cancellationToken)
        {
            var processSpec = context.ProcessSpec;

            if (context.SuppressMSBuildIncrementalism)
            {
                _reporter.Verbose("MSBuild incremental optimizations suppressed.");
            }

            _reporter.Output("Hot reload enabled. For a list of supported edits, see https://aka.ms/dotnet/hot-reload. " +
                "Press \"Ctrl + Shift + R\" to restart.");

            var forceReload = new CancellationTokenSource();

            _console.KeyPressed += (key) =>
            {
                var controlShift = ConsoleModifiers.Control | ConsoleModifiers.Shift;
                if ((key.Modifiers & controlShift) == controlShift && key.Key == ConsoleKey.R)
                {
                    var cancellationTokenSource = Interlocked.Exchange(ref forceReload, new CancellationTokenSource());
                    cancellationTokenSource.Cancel();
                }
            };

            while (true)
            {
                context.Iteration++;

                for (var i = 0; i < _filters.Length; i++)
                {
                    await _filters[i].ProcessAsync(context, cancellationToken);
                }

                // Reset for next run
                context.RequiresMSBuildRevaluation = false;

                processSpec.EnvironmentVariables["DOTNET_WATCH_ITERATION"] = (context.Iteration + 1).ToString(CultureInfo.InvariantCulture);

                var fileSet = context.FileSet;
                if (fileSet == null)
                {
                    _reporter.Error("Failed to find a list of files to watch");
                    return;
                }

                if (!fileSet.Project.IsNetCoreApp60OrNewer())
                {
                    _reporter.Error($"Hot reload based watching is only supported in .NET 6.0 or newer apps. Update the project's launchSettings.json to disable this feature.");
                    return;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                if (context.Iteration == 0)
                {
                    ConfigureExecutable(context, processSpec);
                }

                using var currentRunCancellationSource = new CancellationTokenSource();
                using var combinedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    currentRunCancellationSource.Token,
                    forceReload.Token);
                using var fileSetWatcher = new HotReloadFileSetWatcher(fileSet, _reporter);

                try
                {
                    using var hotReload = new HotReload(_processRunner, _reporter);
                    await hotReload.InitializeAsync(context, cancellationToken);

                    var processTask = _processRunner.RunAsync(processSpec, combinedCancellationSource.Token);
                    var args = string.Join(" ", processSpec.Arguments);
                    _reporter.Verbose($"Running {processSpec.ShortDisplayName()} with the following arguments: {args}");

                    _reporter.Output("Started");

                    Task<FileItem[]> fileSetTask;
                    Task finishedTask;

                    while (true)
                    {
                        fileSetTask = fileSetWatcher.GetChangedFileAsync(combinedCancellationSource.Token);
                        finishedTask = await Task.WhenAny(processTask, fileSetTask).WaitAsync(combinedCancellationSource.Token);

                        if (finishedTask != fileSetTask || fileSetTask.Result is not FileItem[] fileItems)
                        {
                            // The app exited.
                            break;
                        }
                        else
                        {
                            if (MayRequireRecompilation(context, fileItems) is { } newFile)
                            {
                                _reporter.Output($"New file: {newFile.FilePath}. Rebuilding the application.");
                                context.RequiresMSBuildRevaluation = true;
                                break;
                            }
                            else if (fileItems.All(f => f.IsNewFile))
                            {
                                // If every file is a new file and none of them need to be compiled, keep moving.
                                continue;
                            }

                            if (fileItems.Length == 1)
                            {
                                _reporter.Output($"File changed: {fileItems[0].FilePath}.");
                            }
                            else
                            {
                                _reporter.Output($"Files changed: {string.Join(", ", fileItems.Select(f => f.FilePath))}");
                            }
                            var start = Stopwatch.GetTimestamp();
                            if (await hotReload.TryHandleFileChange(context, fileItems, combinedCancellationSource.Token))
                            {
                                var totalTime = TimeSpan.FromTicks(Stopwatch.GetTimestamp() - start);
                                _reporter.Verbose($"Hot reload change handled in {totalTime.TotalMilliseconds}ms.");
                            }
                            else
                            {
                                _reporter.Output($"Unable to handle changes using hot reload.");
                                await _rudeEditDialog.EvaluateAsync(combinedCancellationSource.Token);

                                break;
                            }
                        }
                    }

                    // Regardless of the which task finished first, make sure everything is cancelled
                    // and wait for dotnet to exit. We don't want orphan processes
                    currentRunCancellationSource.Cancel();

                    await Task.WhenAll(processTask, fileSetTask);

                    if (processTask.Result != 0 && finishedTask == processTask && !cancellationToken.IsCancellationRequested)
                    {
                        // Only show this error message if the process exited non-zero due to a normal process exit.
                        // Don't show this if dotnet-watch killed the inner process due to file change or CTRL+C by the user
                        _reporter.Error($"Exited with error code {processTask.Result}");
                    }
                    else
                    {
                        _reporter.Output("Exited");
                    }

                    if (finishedTask == processTask)
                    {
                        // Process exited. Redo evaludation
                        context.RequiresMSBuildRevaluation = true;
                        // Now wait for a file to change before restarting process
                        _reporter.Warn("Waiting for a file to change before restarting dotnet...");
                        await fileSetWatcher.GetChangedFileAsync(cancellationToken);
                    }
                    else
                    {

                        Debug.Assert(finishedTask == fileSetTask);
                        var changedFiles = fileSetTask.Result;
                        context.RequiresMSBuildRevaluation = MayRequireRecompilation(context, changedFiles) is not null;
                    }
                }
                catch (Exception e)
                {
                    _reporter.Verbose($"Caught top-level exception from hot reload: {e}");
                    if (!currentRunCancellationSource.IsCancellationRequested)
                    {
                        currentRunCancellationSource.Cancel();
                    }

                    if (forceReload.IsCancellationRequested)
                    {
                        _console.Clear();
                        _reporter.Output("Restart requested.");
                    }
                }
            }
        }

        private static FileItem? MayRequireRecompilation(DotNetWatchContext context, FileItem[] fileInfo)
        {
            // This method is invoked when a new file is added to the workspace. To determine if we need to
            // recompile, we'll see if it's any of the usual suspects (.cs, .cshtml, .razor) files.

            foreach (var file in fileInfo)
            {
                if (!file.IsNewFile || file.IsStaticFile)
                {
                    continue;
                }

                var filePath = file.FilePath;

                if (filePath is null)
                {
                    continue;
                }

                if (filePath.EndsWith(".cs", StringComparison.Ordinal) || filePath.EndsWith(".razor", StringComparison.Ordinal))
                {
                    return file;
                }

                if (filePath.EndsWith(".cshtml", StringComparison.Ordinal) &&
                    context.ProjectGraph.GraphRoots.FirstOrDefault() is { } project &&
                    project.ProjectInstance.GetPropertyValue("AddCshtmlFilesToDotNetWatchList") is not "false")
                {
                    // For cshtml files, runtime compilation can opt out of watching cshtml files.
                    // Obviously this does not work if a user explicitly removed files out of the watch list,
                    // but we could wait for someone to report it before we think about ways to address it.
                    return file;
                }
            }

            return default;
        }

        private static void ConfigureExecutable(DotNetWatchContext context, ProcessSpec processSpec)
        {
            var project = context.FileSet.Project;
            processSpec.Executable = project.RunCommand;
            if (!string.IsNullOrEmpty(project.RunArguments))
            {
                processSpec.EscapedArguments = project.RunArguments;
            }

            if (!string.IsNullOrEmpty(project.RunWorkingDirectory))
            {
                processSpec.WorkingDirectory = project.RunWorkingDirectory;
            }

            if (!string.IsNullOrEmpty(context.DefaultLaunchSettingsProfile.ApplicationUrl))
            {
                processSpec.EnvironmentVariables["ASPNETCORE_URLS"] = context.DefaultLaunchSettingsProfile.ApplicationUrl;
            }

            var rootVariableName = Environment.Is64BitProcess ? "DOTNET_ROOT" : "DOTNET_ROOT(x86)";
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(rootVariableName)))
            {
                processSpec.EnvironmentVariables[rootVariableName] = Path.GetDirectoryName(DotnetMuxer.MuxerPath);
            }

            if (context.DefaultLaunchSettingsProfile.EnvironmentVariables is IDictionary<string, string> envVariables)
            {
                foreach (var entry in envVariables)
                {
                    var value = Environment.ExpandEnvironmentVariables(entry.Value);
                    // NOTE: MSBuild variables are not expanded like they are in VS
                    processSpec.EnvironmentVariables[entry.Key] = value;
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var filter in _filters)
            {
                if (filter is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync();
                }
                else if (filter is IDisposable diposable)
                {
                    diposable.Dispose();
                }
            }
        }
    }
}
