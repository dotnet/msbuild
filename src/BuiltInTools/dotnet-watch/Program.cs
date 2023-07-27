// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Diagnostics;
using System.Runtime.Loader;
using Microsoft.Build.Graph;
using Microsoft.Build.Locator;
using Microsoft.DotNet.Watcher.Internal;
using Microsoft.DotNet.Watcher.Tools;
using Microsoft.Extensions.Tools.Internal;
using IConsole = Microsoft.Extensions.Tools.Internal.IConsole;

namespace Microsoft.DotNet.Watcher
{
    internal sealed class Program : IDisposable
    {
        private readonly IConsole _console;
        private readonly string _workingDirectory;
        private readonly string _muxerPath;
        private readonly CancellationTokenSource _cts;
        private IReporter _reporter;
        private IRequester _requester;

        public Program(IConsole console, string workingDirectory, string muxerPath)
        {
            Ensure.NotNull(console, nameof(console));
            Ensure.NotNullOrEmpty(workingDirectory, nameof(workingDirectory));

            _console = console;
            _workingDirectory = workingDirectory;
            _muxerPath = muxerPath;
            _cts = new CancellationTokenSource();
            console.CancelKeyPress += OnCancelKeyPress;

            var suppressEmojis = ShouldSuppressEmojis();
            _reporter = CreateReporter(verbose: true, quiet: false, console: _console, suppressEmojis);
            _requester = new ConsoleRequester(_console, quiet: false, suppressEmojis);
        }

        public static async Task<int> Main(string[] args)
        {
            try
            {
                var muxerPath = Environment.ProcessPath;
                Debug.Assert(muxerPath != null);
                Debug.Assert(Path.GetFileNameWithoutExtension(muxerPath) == "dotnet", $"Invalid muxer path {muxerPath}");

#if DEBUG
                var sdkRootDirectory = Environment.GetEnvironmentVariable("DOTNET_WATCH_DEBUG_SDK_DIRECTORY");
#else
                var sdkRootDirectory = "";
#endif

                // We can register the MSBuild that is bundled with the SDK to perform MSBuild things.
                // In production deployment dotnet-watch is in a nested folder of the SDK's root, we'll back up to it.
                // AppContext.BaseDirectory = $sdkRoot\$sdkVersion\DotnetTools\dotnet-watch\$version\tools\net6.0\any\
                // MSBuild.dll is located at $sdkRoot\$sdkVersion\MSBuild.dll
                if (string.IsNullOrEmpty(sdkRootDirectory))
                {
                    sdkRootDirectory = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..");
                }

                MSBuildLocator.RegisterMSBuildPath(sdkRootDirectory);

                // Register listeners that load Roslyn-related assemblies from the `Roslyn/bincore` directory.
                RegisterAssemblyResolutionEvents(sdkRootDirectory);

                using var program = new Program(PhysicalConsole.Singleton, Directory.GetCurrentDirectory(), muxerPath);
                return await program.RunAsync(args);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Unexpected error:");
                Console.Error.WriteLine(ex.ToString());
                return 1;
            }
        }

        internal async Task<int> RunAsync(string[] args)
        {
            var options = CommandLineOptions.Parse(args, _reporter, out var errorCode);

            // an error reported or help printed:
            if (options == null)
            {
                return errorCode;
            }

            // update reporter as configured by options
            var suppressEmojis = ShouldSuppressEmojis();
            _reporter = CreateReporter(options.Verbose, options.Quiet, _console, suppressEmojis);
            _requester = new ConsoleRequester(_console, quiet: options.Quiet, suppressEmojis);

            try
            {
                if (_cts.IsCancellationRequested)
                {
                    return 1;
                }

                if (options.List)
                {
                    return await ListFilesAsync(options, _reporter, _cts.Token);
                }
                else
                {
                    return await RunAsync(options, _cts.Token);
                }
            }
            catch (Exception ex)
            {
                if (ex is TaskCanceledException || ex is OperationCanceledException)
                {
                    // swallow when only exception is the CTRL+C forced an exit
                    return 0;
                }

                _reporter.Error(ex.ToString());
                _reporter.Error("An unexpected error occurred");
                return 1;
            }
        }

        private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs args)
        {
            // suppress CTRL+C on the first press
            args.Cancel = !_cts.IsCancellationRequested;

            if (args.Cancel)
            {
                _reporter.Output("Shutdown requested. Press Ctrl+C again to force exit.", emoji: "🛑");
            }

            _cts.Cancel();
        }

        private async Task<int> RunAsync(CommandLineOptions options, CancellationToken cancellationToken)
        {
            // TODO multiple projects should be easy enough to add here
            string projectFile;
            try
            {
                projectFile = MsBuildProjectFinder.FindMsBuildProject(_workingDirectory, options.Project);
            }
            catch (FileNotFoundException ex)
            {
                _reporter.Error(ex.Message);
                return 1;
            }

            var watchOptions = DotNetWatchOptions.Default;
            watchOptions.NonInteractive = options.NonInteractive;

            var fileSetFactory = new MsBuildFileSetFactory(
                watchOptions,
                _reporter,
                _muxerPath,
                projectFile,
                options.TargetFramework,
                options.BuildProperties,
                outputSink: null,
                waitOnError: true,
                trace: true);

            if (FileWatcherFactory.IsPollingEnabled)
            {
                _reporter.Output("Polling file watcher is enabled");
            }

            var projectDirectory = Path.GetDirectoryName(projectFile);
            Debug.Assert(projectDirectory != null);

            var projectGraph = TryReadProject(projectFile, options);

            bool enableHotReload;
            if (options.NoHotReload)
            {
                _reporter.Verbose("Hot Reload disabled by command line switch.");
                enableHotReload = false;
            }
            else if (projectGraph is null || !IsHotReloadSupported(projectGraph))
            {
                _reporter.Verbose("Project does not support Hot Reload.");
                enableHotReload = false;
            }
            else
            {
                _reporter.Verbose("Watching with Hot Reload.");
                enableHotReload = true;
            }

            var args = options.GetLaunchProcessArguments(enableHotReload, _reporter, out var noLaunchProfile, out var launchProfileName);
            var launchProfile = (noLaunchProfile ? null : LaunchSettingsProfile.ReadLaunchProfile(projectDirectory, launchProfileName, _reporter)) ?? new();

            // If no args forwarded to the app were specified use the ones in the profile.
            var escapedArgs = (enableHotReload && args is []) ? launchProfile.CommandLineArgs : null;

            var context = new DotNetWatchContext
            {
                HotReloadEnabled = enableHotReload,
                ProcessSpec = new ProcessSpec
                {
                    WorkingDirectory = projectDirectory,
                    Arguments = args,
                    EscapedArguments = escapedArgs,
                    EnvironmentVariables =
                    {
                        ["DOTNET_WATCH"] = "1"
                    },
                },
                ProjectGraph = projectGraph,
                Reporter = _reporter,
                SuppressMSBuildIncrementalism = watchOptions.SuppressMSBuildIncrementalism,
                LaunchSettingsProfile = launchProfile,
                TargetFramework = options.TargetFramework,
                BuildProperties = options.BuildProperties,
            };

            if (enableHotReload)
            {
                await using var watcher = new HotReloadDotNetWatcher(_reporter, _requester, fileSetFactory, watchOptions, _console, _workingDirectory, _muxerPath);
                await watcher.WatchAsync(context, cancellationToken);
            }
            else
            {
                await using var watcher = new DotNetWatcher(_reporter, fileSetFactory, watchOptions, _muxerPath);
                await watcher.WatchAsync(context, cancellationToken);
            }

            return 0;
        }

        private ProjectGraph? TryReadProject(string project, CommandLineOptions options)
        {
            var globalOptions = new Dictionary<string, string>();
            if (options.TargetFramework != null)
            {
                globalOptions.Add("TargetFramework", options.TargetFramework);
            }

            if (options.BuildProperties != null)
            {
                foreach (var (name, value) in options.BuildProperties)
                {
                    globalOptions[name] = value;
                }
            }

            try
            {
                return new ProjectGraph(project, globalOptions);
            }
            catch (Exception ex)
            {
                _reporter.Verbose("Reading the project instance failed.");
                _reporter.Verbose(ex.ToString());
            }

            return null;
        }

        private static bool IsHotReloadSupported(ProjectGraph projectGraph)
        {
            var projectInstance = projectGraph.EntryPointNodes.FirstOrDefault()?.ProjectInstance;
            if (projectInstance is null)
            {
                return false;
            }

            var projectCapabilities = projectInstance.GetItems("ProjectCapability");
            foreach (var item in projectCapabilities)
            {
                if (item.EvaluatedInclude == "SupportsHotReload")
                {
                    return true;
                }
            }
            return false;
        }

        private async Task<int> ListFilesAsync(
            CommandLineOptions options,
            IReporter reporter,
            CancellationToken cancellationToken)
        {
            // TODO multiple projects should be easy enough to add here
            string projectFile;
            try
            {
                projectFile = MsBuildProjectFinder.FindMsBuildProject(_workingDirectory, options.Project);
            }
            catch (FileNotFoundException ex)
            {
                reporter.Error(ex.Message);
                return 1;
            }

            var fileSetFactory = new MsBuildFileSetFactory(
                DotNetWatchOptions.Default,
                reporter,
                _muxerPath,
                projectFile,
                options.TargetFramework,
                options.BuildProperties,
                outputSink: null,
                waitOnError: false,
                trace: false);

            var files = await fileSetFactory.CreateAsync(cancellationToken);

            if (files == null)
            {
                return 1;
            }

            foreach (var file in files)
            {
                _console.Out.WriteLine(file.FilePath);
            }

            return 0;
        }

        private static IReporter CreateReporter(bool verbose, bool quiet, IConsole console, bool suppressEmojis)
            => new ConsoleReporter(console, verbose || IsGlobalVerbose(), quiet, suppressEmojis);

        private static bool IsGlobalVerbose()
        {
            bool.TryParse(Environment.GetEnvironmentVariable("DOTNET_CLI_CONTEXT_VERBOSE"), out bool globalVerbose);
            return globalVerbose;
        }

        public void Dispose()
        {
            _console.CancelKeyPress -= OnCancelKeyPress;
            _cts.Dispose();
        }

        private static bool ShouldSuppressEmojis()
        {
            var suppressEmojisEnvironmentVariable = Environment.GetEnvironmentVariable("DOTNET_WATCH_SUPPRESS_EMOJIS");
            var suppressEmojis = suppressEmojisEnvironmentVariable == "1" || string.Equals(suppressEmojisEnvironmentVariable, "true", StringComparison.OrdinalIgnoreCase);
            return suppressEmojis;
        }

        private static void RegisterAssemblyResolutionEvents(string sdkRootDirectory)
        {
            var roslynPath = Path.Combine(sdkRootDirectory, "Roslyn", "bincore");

            AssemblyLoadContext.Default.Resolving += (context, assembly) =>
            {
                if (assembly.Name is "Microsoft.CodeAnalysis" or "Microsoft.CodeAnalysis.CSharp")
                {
                    var loadedAssembly = context.LoadFromAssemblyPath(Path.Combine(roslynPath, assembly.Name + ".dll"));
                    // Avoid scenarios where the assembly in rosylnPath is older than what we expect
                    if (loadedAssembly.GetName().Version < assembly.Version)
                    {
                        throw new Exception($"Found a version of {assembly.Name} that was lower than the target version of {assembly.Version}");
                    }
                    return loadedAssembly;
                }
                return null;
            };
        }
    }
}
