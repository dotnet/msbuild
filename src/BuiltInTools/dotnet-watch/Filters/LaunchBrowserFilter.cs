// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Tools
{
    public sealed class LaunchBrowserFilter : IWatchFilter, IAsyncDisposable
    {
        private static readonly Regex NowListeningRegex = new Regex(@"Now listening on: (?<url>.*)\s*$", RegexOptions.None | RegexOptions.Compiled, TimeSpan.FromSeconds(10));
        private readonly bool _runningInTest;
        private readonly bool _suppressLaunchBrowser;
        private readonly string _browserPath;
        private bool _attemptedBrowserLaunch;
        private Process _browserProcess;
        private IReporter _reporter;
        private string _launchPath;
        private CancellationToken _cancellationToken;
        private DotNetWatchContext _watchContext;

        public LaunchBrowserFilter(DotNetWatchOptions dotNetWatchOptions, bool allowBrowserRefreshWithoutLaunchBrowser = false)
        {
            _suppressLaunchBrowser = dotNetWatchOptions.SuppressLaunchBrowser;
            _runningInTest = dotNetWatchOptions.RunningAsTest;
            _browserPath = Environment.GetEnvironmentVariable("DOTNET_WATCH_BROWSER_PATH");
        }

        public ValueTask ProcessAsync(DotNetWatchContext context, CancellationToken cancellationToken)
        {
            if (_suppressLaunchBrowser)
            {
                return default;
            }

            _watchContext = context;

            if (context.Iteration == 0)
            {
                _reporter = context.Reporter;

                if (CanLaunchBrowser(context, out var launchPath))
                {
                    context.Reporter.Verbose("dotnet-watch is configured to launch a browser on ASP.NET Core application startup.");
                    _launchPath = launchPath;
                    _cancellationToken = cancellationToken;

                    // We've redirected the output, but want to ensure that it continues to appear in the user's console.
                    context.ProcessSpec.OnOutput += (_, eventArgs) => Console.WriteLine(eventArgs.Data);
                    context.ProcessSpec.OnOutput += OnOutput;
                }
            }

            return default;
        }

        private void OnOutput(object sender, DataReceivedEventArgs eventArgs)
        {
            if (string.IsNullOrEmpty(eventArgs.Data))
            {
                return;
            }

            var match = NowListeningRegex.Match(eventArgs.Data);
            if (match.Success)
            {
                var launchUrl = match.Groups["url"].Value;

                var process = (Process)sender;
                process.OutputDataReceived -= OnOutput;

                if (!_attemptedBrowserLaunch)
                {
                    _attemptedBrowserLaunch = true;

                    _reporter.Verbose("Launching browser.");

                    try
                    {
                        LaunchBrowser(launchUrl);
                    }
                    catch (Exception ex)
                    {
                        _reporter.Verbose($"An exception occurred when attempting to launch a browser: {ex}");
                        _browserProcess = null;
                    }

                    if (_browserProcess is null || _browserProcess.HasExited)
                    {
                        // dotnet-watch, by default, relies on URL file association to launch browsers. On Windows and MacOS, this works fairly well
                        // where URLs are associated with the default browser. On Linux, this is a bit murky.
                        // From emperical observation, it's noted that failing to launch a browser results in either Process.Start returning a null-value
                        // or for the process to have immediately exited.
                        // We can use this to provide a helpful message.
                        _reporter.Output($"Unable to launch the browser. Navigate to {launchUrl}", emoji: "🌐");
                    }
                }
                else if (_watchContext?.BrowserRefreshServer is { } browserRefresh)
                {
                    _reporter.Verbose("Reloading browser.");
                    _ = browserRefresh.ReloadAsync(_cancellationToken);
                }
            }
        }

        private void LaunchBrowser(string launchUrl)
        {
            var fileName = Uri.TryCreate(_launchPath, UriKind.Absolute, out _) ? _launchPath : launchUrl + "/" + _launchPath;
            var args = string.Empty;
            if (!string.IsNullOrEmpty(_browserPath))
            {
                args = fileName;
                fileName = _browserPath;
            }

            if (_runningInTest)
            {
                _reporter.Output($"Launching browser: {fileName} {args}");
                return;
            }

            _browserProcess = Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args,
                UseShellExecute = true,
            });
        }

        private static bool CanLaunchBrowser(DotNetWatchContext context, out string launchUrl)
        {
            launchUrl = null;
            var reporter = context.Reporter;

            if (!context.FileSet.Project.IsNetCoreApp31OrNewer())
            {
                // Browser refresh middleware supports 3.1 or newer
                reporter.Verbose("Browser refresh is only supported in .NET Core 3.1 or newer projects.");
                return false;
            }

            var dotnetCommand = context.ProcessSpec.Arguments.FirstOrDefault();
            if (!string.Equals(dotnetCommand, "run", StringComparison.Ordinal))
            {
                reporter.Verbose("Browser refresh is only supported for run commands.");
                return false;
            }

            if (context.LaunchSettingsProfile is not { LaunchBrowser: true })
            {
                reporter.Verbose("launchSettings does not allow launching browsers.");
                return false;
            }

            launchUrl = context.LaunchSettingsProfile.LaunchUrl;
            return true;
        }

        public ValueTask DisposeAsync()
        {
            _browserProcess?.Dispose();
            return default;
        }
    }
}
