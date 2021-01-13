// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.Tools.Internal;
using IReporter = Microsoft.Extensions.Tools.Internal.IReporter;

namespace Microsoft.DotNet.Watcher.Internal
{
    public class MsBuildFileSetFactory : IFileSetFactory
    {
        private const string TargetName = "GenerateWatchList";
        private const string WatchTargetsFileName = "DotNetWatch.targets";

        private readonly string _muxerPath;
        private readonly IReporter _reporter;
        private readonly string _projectFile;
        private readonly OutputSink _outputSink;
        private readonly ProcessRunner _processRunner;
        private readonly bool _waitOnError;
        private readonly IReadOnlyList<string> _buildFlags;

        public MsBuildFileSetFactory(
            IReporter reporter,
            string projectFile,
            bool waitOnError,
            bool trace)
            : this(new Muxer().MuxerPath, reporter, projectFile, new OutputSink(), waitOnError, trace)
        {
        }

        // output sink is for testing
        internal MsBuildFileSetFactory(
            string muxerPath,
            IReporter reporter,
            string projectFile,
            OutputSink outputSink,
            bool waitOnError,
            bool trace)
        {
            Ensure.NotNull(reporter, nameof(reporter));
            Ensure.NotNullOrEmpty(projectFile, nameof(projectFile));
            Ensure.NotNull(outputSink, nameof(outputSink));

            _muxerPath = muxerPath;
            _reporter = reporter;
            _projectFile = projectFile;
            _outputSink = outputSink;
            _processRunner = new ProcessRunner(reporter);
            _buildFlags = InitializeArgs(FindTargetsFile(), trace);
            _waitOnError = waitOnError;
        }

        public async Task<IFileSet> CreateAsync(CancellationToken cancellationToken)
        {
            var watchList = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            try
            {
                var projectDir = Path.GetDirectoryName(_projectFile);

                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var capture = _outputSink.StartCapture();
                    var processSpec = new ProcessSpec
                    {
                        Executable = _muxerPath,
                        WorkingDirectory = projectDir,
                        Arguments = new[]
                        {
                            "msbuild",
                            "/nologo",
                            _projectFile,
                            $"/p:_DotNetWatchListFile={watchList}",
                        }.Concat(_buildFlags),
                        OutputCapture = capture
                    };

                    _reporter.Verbose($"Running MSBuild target '{TargetName}' on '{_projectFile}'");

                    var exitCode = await _processRunner.RunAsync(processSpec, cancellationToken);

                    if (exitCode == 0 && File.Exists(watchList))
                    {
                        var lines = File.ReadAllLines(watchList);
                        var isNetCoreApp31OrNewer = lines.FirstOrDefault() == "true";

                        var fileset = new FileSet(
                            isNetCoreApp31OrNewer,
                            lines.Skip(1)
                                .Select(l => l?.Trim())
                                .Where(l => !string.IsNullOrEmpty(l)));

                        _reporter.Verbose($"Watching {fileset.Count} file(s) for changes");
#if DEBUG

                        foreach (var file in fileset)
                        {
                            _reporter.Verbose($"  -> {file}");
                        }

                        Debug.Assert(fileset.All(Path.IsPathRooted), "All files should be rooted paths");
#endif

                        return fileset;
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

                        var fileSet = new FileSet(false, new[] { _projectFile });

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

        private IReadOnlyList<string> InitializeArgs(string watchTargetsFile, bool trace)
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
            var searchPaths = new[]
            {
                AppContext.BaseDirectory,
                assemblyDir,
            };

            var targetPath = searchPaths.Select(p => Path.Combine(p, WatchTargetsFileName)).FirstOrDefault(File.Exists);
            if (targetPath == null)
            {
                _reporter.Error("Fatal error: could not find DotNetWatch.targets");
                return null;
            }
            return targetPath;
        }
    }
}
