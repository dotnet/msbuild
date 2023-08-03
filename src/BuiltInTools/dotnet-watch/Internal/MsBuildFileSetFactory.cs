// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Diagnostics;
using System.Text.Json;
using Microsoft.DotNet.Cli;
using IReporter = Microsoft.Extensions.Tools.Internal.IReporter;

namespace Microsoft.DotNet.Watcher.Internal
{
    internal sealed class MsBuildFileSetFactory : IFileSetFactory
    {
        private const string TargetName = "GenerateWatchList";
        private const string WatchTargetsFileName = "DotNetWatch.targets";

        private readonly IReporter _reporter;
        private readonly DotNetWatchOptions _dotNetWatchOptions;
        private readonly string _muxerPath;
        private readonly string _projectFile;
        private readonly OutputSink _outputSink;
        private readonly ProcessRunner _processRunner;
        private readonly bool _waitOnError;
        private readonly IReadOnlyList<string> _buildFlags;

        public MsBuildFileSetFactory(
            DotNetWatchOptions dotNetWatchOptions,
            IReporter reporter,
            string muxerPath,
            string projectFile,
            string? targetFramework,
            IReadOnlyList<(string, string)>? buildProperties,
            OutputSink? outputSink,
            bool waitOnError,
            bool trace)
        {
            _reporter = reporter;
            _dotNetWatchOptions = dotNetWatchOptions;
            _muxerPath = muxerPath;
            _projectFile = projectFile;
            _outputSink = outputSink ?? new OutputSink();
            _processRunner = new ProcessRunner(reporter);
            _buildFlags = InitializeArgs(FindTargetsFile(), targetFramework, buildProperties, trace);

            _waitOnError = waitOnError;
        }

        public async Task<FileSet?> CreateAsync(CancellationToken cancellationToken)
        {
            var watchList = Path.GetTempFileName();
            try
            {
                var projectDir = Path.GetDirectoryName(_projectFile);

                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var capture = _outputSink.StartCapture();
                    var arguments = new List<string>
                    {
                        "msbuild",
                        "/nologo",
                        _projectFile,
                        $"/p:_DotNetWatchListFile={watchList}",
                    };

                    if (_dotNetWatchOptions.SuppressHandlingStaticContentFiles)
                    {
                        arguments.Add("/p:DotNetWatchContentFiles=false");
                    }

                    arguments.AddRange(_buildFlags);

                    var processSpec = new ProcessSpec
                    {
                        Executable = _muxerPath,
                        WorkingDirectory = projectDir,
                        Arguments = arguments,
                        OutputCapture = capture
                    };

                    _reporter.Verbose($"Running MSBuild target '{TargetName}' on '{_projectFile}'");

                    var exitCode = await _processRunner.RunAsync(processSpec, cancellationToken);

                    if (exitCode == 0 && File.Exists(watchList))
                    {
                        using var watchFile = File.OpenRead(watchList);
                        var result = await JsonSerializer.DeserializeAsync<MSBuildFileSetResult>(watchFile, cancellationToken: cancellationToken);
                        Debug.Assert(result != null);

                        var fileItems = new List<FileItem>();
                        foreach (var project in result.Projects)
                        {
                            var value = project.Value;
                            var fileCount = value.Files.Count;

                            for (var i = 0; i < fileCount; i++)
                            {
                                fileItems.Add(new FileItem
                                {
                                    FilePath = value.Files[i],
                                    ProjectPath = project.Key,
                                });
                            }

                            var staticItemsCount = value.StaticFiles.Count;
                            for (var i = 0; i < staticItemsCount; i++)
                            {
                                var item = value.StaticFiles[i];
                                fileItems.Add(new FileItem
                                {
                                    FilePath = item.FilePath,
                                    ProjectPath = project.Key,
                                    IsStaticFile = true,
                                    StaticWebAssetPath = item.StaticWebAssetPath,
                                });
                            }
                        }


                        _reporter.Verbose($"Watching {fileItems.Count} file(s) for changes");
#if DEBUG

                        foreach (var file in fileItems)
                        {
                            _reporter.Verbose($"  -> {file.FilePath} {(file.IsStaticFile ? file.StaticWebAssetPath : null)}");
                        }

                        Debug.Assert(fileItems.All(f => Path.IsPathRooted(f.FilePath)), "All files should be rooted paths");
#endif

                        var projectInfo = new ProjectInfo(
                            _projectFile,
                            result.IsNetCoreApp,
                            EnvironmentVariableNames.TryParseTargetFrameworkVersion(result.TargetFrameworkVersion),
                            result.RuntimeIdentifier,
                            result.DefaultAppHostRuntimeIdentifier,
                            result.RunCommand,
                            result.RunArguments,
                            result.RunWorkingDirectory);
                        return new FileSet(projectInfo, fileItems);
                    }

                    _reporter.Error($"Error(s) finding watch items project file '{Path.GetFileName(_projectFile)}'");

                    _reporter.Output($"MSBuild output from target '{TargetName}':");
                    _reporter.Output(string.Empty);

                    foreach (var line in capture.Lines)
                    {
                        _reporter.Output($"   {line}");
                    }

                    _reporter.Output(string.Empty);

                    if (!_waitOnError)
                    {
                        return null;
                    }
                    else
                    {
                        _reporter.Warn("Fix the error to continue or press Ctrl+C to exit.");

                        var fileSet = new FileSet(projectInfo: null, new[] { new FileItem { FilePath = _projectFile } });

                        using (var watcher = new FileSetWatcher(fileSet, _reporter))
                        {
                            await watcher.GetChangedFileAsync(cancellationToken);

                            _reporter.Output($"File changed: {_projectFile}");
                        }
                    }
                }
            }
            finally
            {
                if (File.Exists(watchList))
                {
                    File.Delete(watchList);
                }
            }
        }

        private IReadOnlyList<string> InitializeArgs(string watchTargetsFile, string? targetFramework, IReadOnlyList<(string name, string value)>? buildProperties, bool trace)
        {
            var args = new List<string>
            {
                "/nologo",
                "/v:n",
                "/t:" + TargetName,
                "/p:DotNetWatchBuild=true", // extensibility point for users
                "/p:DesignTimeBuild=true", // don't do expensive things
                "/p:CustomAfterMicrosoftCommonTargets=" + watchTargetsFile,
                "/p:CustomAfterMicrosoftCommonCrossTargetingTargets=" + watchTargetsFile,
            };

            if (targetFramework != null)
            {
                args.Add("/p:TargetFramework=" + targetFramework);
            }

            if (buildProperties != null)
            {
                args.AddRange(buildProperties.Select(p => $"/p:{p.name}={p.value}"));
            }

            if (trace)
            {
                // enables capturing markers to know which projects have been visited
                args.Add("/p:_DotNetWatchTraceOutput=true");
            }

            return args;
        }

        private string FindTargetsFile()
        {
            var assemblyDir = Path.GetDirectoryName(typeof(MsBuildFileSetFactory).Assembly.Location);
            Debug.Assert(assemblyDir != null);

            var searchPaths = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "assets"),
                Path.Combine(assemblyDir, "assets"),
                AppContext.BaseDirectory,
                assemblyDir,
            };

            var targetPath = searchPaths.Select(p => Path.Combine(p, WatchTargetsFileName)).FirstOrDefault(File.Exists);
            return targetPath ?? throw new FileNotFoundException("Fatal error: could not find DotNetWatch.targets");
        }
    }
}
