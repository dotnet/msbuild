// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Diagnostics;
using System.Globalization;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Watcher.Internal;
using Microsoft.DotNet.Watcher.Tools;
using Microsoft.Extensions.Tools.Internal;
using IReporter = Microsoft.Extensions.Tools.Internal.IReporter;

namespace Microsoft.DotNet.Watcher
{
    internal sealed class HotReloadDotNetWatcher : IAsyncDisposable
    {
        private readonly IReporter _reporter;
        private readonly IConsole _console;
        private readonly ProcessRunner _processRunner;
        private readonly DotNetWatchOptions _dotNetWatchOptions;
        private readonly IWatchFilter[] _filters;
        private readonly RudeEditDialog? _rudeEditDialog;
        private readonly string _workingDirectory;
        private readonly string _muxerPath;

        public HotReloadDotNetWatcher(IReporter reporter, IRequester requester, IFileSetFactory fileSetFactory, DotNetWatchOptions dotNetWatchOptions, IConsole console, string workingDirectory, string muxerPath)
        {
            Ensure.NotNull(reporter, nameof(reporter));
            Ensure.NotNull(requester, nameof(requester));
            Ensure.NotNullOrEmpty(workingDirectory, nameof(workingDirectory));

            _reporter = reporter;
            _processRunner = new ProcessRunner(reporter);
            _dotNetWatchOptions = dotNetWatchOptions;
            _console = console;
            _workingDirectory = workingDirectory;
            _muxerPath = muxerPath;

            _filters = new IWatchFilter[]
            {
                new DotNetBuildFilter(fileSetFactory, _processRunner, _reporter, muxerPath),
                new LaunchBrowserFilter(dotNetWatchOptions),
                new BrowserRefreshFilter(dotNetWatchOptions, _reporter, muxerPath),
            };

            if (!dotNetWatchOptions.NonInteractive)
            {
                _rudeEditDialog = new(reporter, requester, _console);
            }
        }

        public async Task WatchAsync(DotNetWatchContext context, CancellationToken cancellationToken)
        {
            Debug.Assert(context.ProcessSpec != null);

            var processSpec = context.ProcessSpec;

            var forceReload = new CancellationTokenSource();
            var hotReloadEnabledMessage = "Hot reload enabled. For a list of supported edits, see https://aka.ms/dotnet/hot-reload.";

            if (!_dotNetWatchOptions.NonInteractive)
            {
                _reporter.Output($"{hotReloadEnabledMessage}{Environment.NewLine}  {(_dotNetWatchOptions.SuppressEmojis ? string.Empty : "💡")} Press \"Ctrl + R\" to restart.", emoji: "🔥");

                _console.KeyPressed += (key) =>
                {
                    var modifiers = ConsoleModifiers.Control;
                    if ((key.Modifiers & modifiers) == modifiers && key.Key == ConsoleKey.R)
                    {
                        var cancellationTokenSource = Interlocked.Exchange(ref forceReload, new CancellationTokenSource());
                        cancellationTokenSource.Cancel();
                    }
                };
            }
            else
            {
                _reporter.Output(hotReloadEnabledMessage, emoji: "🔥");
            }

            while (true)
            {
                context.Iteration++;

                for (var i = 0; i < _filters.Length; i++)
                {
                    await _filters[i].ProcessAsync(context, cancellationToken);
                }

                processSpec.EnvironmentVariables["DOTNET_WATCH_ITERATION"] = (context.Iteration + 1).ToString(CultureInfo.InvariantCulture);
                processSpec.EnvironmentVariables["DOTNET_LAUNCH_PROFILE"] = context.LaunchSettingsProfile?.LaunchProfileName ?? string.Empty;

                var fileSet = context.FileSet;
                if (fileSet == null)
                {
                    _reporter.Error("Failed to find a list of files to watch");
                    return;
                }

                Debug.Assert(fileSet.Project != null);

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
                    using var hotReload = new HotReload(_reporter);

                    // Solution must be initialized before we start watching for file changes to avoid race condition
                    // when the solution captures state of the file after the changes has already been made.
                    await hotReload.InitializeAsync(context, cancellationToken);

                    _reporter.Verbose($"Running {processSpec.ShortDisplayName()} with the following arguments: '{string.Join(" ", processSpec.Arguments ?? Array.Empty<string>())}'");
                    var processTask = _processRunner.RunAsync(processSpec, combinedCancellationSource.Token);

                    _reporter.Output("Started", emoji: "🚀");

                    Task<FileItem[]?> fileSetTask;
                    Task finishedTask;

                    while (true)
                    {
                        fileSetTask = fileSetWatcher.GetChangedFileAsync(combinedCancellationSource.Token);
                        finishedTask = await Task.WhenAny(processTask, fileSetTask).WaitAsync(combinedCancellationSource.Token);

                        if (finishedTask != fileSetTask || fileSetTask.Result is not FileItem[] fileItems)
                        {
                            if (processTask.IsFaulted && finishedTask == processTask && !cancellationToken.IsCancellationRequested)
                            {
                                // Only show this error message if the process exited non-zero due to a normal process exit.
                                // Don't show this if dotnet-watch killed the inner process due to file change or CTRL+C by the user
                                _reporter.Error($"Application failed to start: {processTask.Exception?.InnerException?.Message}");
                            }
                            break;
                        }
                        else
                        {
                            if (MayRequireRecompilation(context, fileItems) is { } newFile)
                            {
                                _reporter.Output($"New file: {GetRelativeFilePath(newFile.FilePath)}. Rebuilding the application.");
                                break;
                            }
                            else if (fileItems.All(f => f.IsNewFile))
                            {
                                // If every file is a new file and none of them need to be compiled, keep moving.
                                continue;
                            }

                            if (fileItems.Length > 1)
                            {
                                // Filter out newly added files from the list to make the reporting cleaner.
                                // Any action we needed to take on significant newly added files is handled by MayRequiredRecompilation.
                                fileItems = fileItems.Where(f => !f.IsNewFile).ToArray();
                            }

                            if (fileItems.Length == 1)
                            {
                                _reporter.Output($"File changed: {GetRelativeFilePath(fileItems[0].FilePath)}.");
                            }
                            else
                            {
                                _reporter.Output($"Files changed: {string.Join(", ", fileItems.Select(f => GetRelativeFilePath(f.FilePath)))}");
                            }
                            var start = Stopwatch.GetTimestamp();
                            if (await hotReload.TryHandleFileChange(context, fileItems, combinedCancellationSource.Token))
                            {
                                var totalTime = TimeSpan.FromTicks(Stopwatch.GetTimestamp() - start);
                                _reporter.Verbose($"Hot reload change handled in {totalTime.TotalMilliseconds}ms.", emoji: "🔥");
                            }
                            else
                            {
                                if (_rudeEditDialog is not null)
                                {
                                    await _rudeEditDialog.EvaluateAsync(combinedCancellationSource.Token);
                                }
                                else
                                {
                                    _reporter.Verbose("Restarting without prompt since dotnet-watch is running in non-interactive mode.");
                                }
                                break;
                            }
                        }
                    }

                    // Regardless of the which task finished first, make sure everything is cancelled
                    // and wait for dotnet to exit. We don't want orphan processes
                    currentRunCancellationSource.Cancel();

                    await Task.WhenAll(processTask, fileSetTask);

                    if (!processTask.IsFaulted && processTask.Result != 0 && finishedTask == processTask && !cancellationToken.IsCancellationRequested)
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
                        // Now wait for a file to change before restarting process
                        _reporter.Warn("Waiting for a file to change before restarting dotnet...", emoji: "⏳");
                        await fileSetWatcher.GetChangedFileAsync(cancellationToken, forceWaitForNewUpdate: true);
                    }
                    else
                    {
                        Debug.Assert(finishedTask == fileSetTask);
                    }
                }
                catch (Exception e)
                {
                    if (e is not OperationCanceledException)
                    {
                        _reporter.Verbose($"Caught top-level exception from hot reload: {e}");
                    }

                    if (!currentRunCancellationSource.IsCancellationRequested)
                    {
                        currentRunCancellationSource.Cancel();
                    }

                    if (forceReload.IsCancellationRequested)
                    {
                        _console.Clear();
                        _reporter.Output("Restart requested.", emoji: "🔄");
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
                    context.ProjectGraph!.GraphRoots.FirstOrDefault() is { } project &&
                    project.ProjectInstance.GetPropertyValue("AddCshtmlFilesToDotNetWatchList") is not "false")
                {
                    // For cshtml files, runtime compilation can opt out of watching cshtml files.
                    // Obviously this does not work if a user explicitly removed files out of the watch list,
                    // but we could wait for someone to report it before we think about ways to address it.
                    return file;
                }

                if (filePath.EndsWith(".razor.css", StringComparison.Ordinal) || filePath.EndsWith(".cshtml.css", StringComparison.Ordinal))
                {
                    return file;
                }
            }

            return default;
        }

        private void ConfigureExecutable(DotNetWatchContext context, ProcessSpec processSpec)
        {
            var project = context.FileSet?.Project;
            Debug.Assert(project != null);

            processSpec.Executable = project.RunCommand;
            if (!string.IsNullOrEmpty(project.RunArguments))
            {
                processSpec.EscapedArguments = project.RunArguments;
            }

            if (!string.IsNullOrEmpty(project.RunWorkingDirectory))
            {
                processSpec.WorkingDirectory = project.RunWorkingDirectory;
            }

            if (!string.IsNullOrEmpty(context.LaunchSettingsProfile.ApplicationUrl))
            {
                processSpec.EnvironmentVariables["ASPNETCORE_URLS"] = context.LaunchSettingsProfile.ApplicationUrl;
            }

            var rootVariableName = EnvironmentVariableNames.TryGetDotNetRootVariableName(
                project.RuntimeIdentifier ?? "",
                project.DefaultAppHostRuntimeIdentifier ?? "",
                project.TargetFrameworkVersion);

            if (rootVariableName != null && string.IsNullOrEmpty(Environment.GetEnvironmentVariable(rootVariableName)))
            {
                processSpec.EnvironmentVariables[rootVariableName] = Path.GetDirectoryName(_muxerPath)!;
            }

            if (context.LaunchSettingsProfile.EnvironmentVariables is { } envVariables)
            {
                foreach (var entry in envVariables)
                {
                    var value = Environment.ExpandEnvironmentVariables(entry.Value);
                    // NOTE: MSBuild variables are not expanded like they are in VS
                    processSpec.EnvironmentVariables[entry.Key] = value;
                }
            }
        }

        private string GetRelativeFilePath(string path)
        {
            var relativePath = path;
            if (path.StartsWith(_workingDirectory, StringComparison.Ordinal) && path.Length > _workingDirectory.Length)
            {
                relativePath = path.Substring(_workingDirectory.Length);

                return $".{(relativePath.StartsWith(Path.DirectorySeparatorChar) ? string.Empty : Path.DirectorySeparatorChar)}{relativePath}";
            }

            return relativePath;
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
