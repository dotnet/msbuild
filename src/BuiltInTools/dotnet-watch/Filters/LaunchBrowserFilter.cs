// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Tools
{
    internal sealed class LaunchBrowserFilter : IWatchFilter, IAsyncDisposable
    {
        private static readonly Regex NowListeningRegex = new Regex(@"Now listening on: (?<url>.*)\s*$", RegexOptions.None | RegexOptions.Compiled, TimeSpan.FromSeconds(10));
        private readonly DotNetWatchOptions _options;
        private readonly string? _browserPath;
        private bool _attemptedBrowserLaunch;
        private Process? _browserProcess;
        private IReporter? _reporter;
        private string? _launchPath;
        private CancellationToken _cancellationToken;
        private DotNetWatchContext? _watchContext;

        public LaunchBrowserFilter(DotNetWatchOptions dotNetWatchOptions)
        {
            _options = dotNetWatchOptions;
            _browserPath = Environment.GetEnvironmentVariable("DOTNET_WATCH_BROWSER_PATH");
        }

        public ValueTask ProcessAsync(DotNetWatchContext context, CancellationToken cancellationToken)
        {
            Debug.Assert(context.ProcessSpec != null);

            if (_options.SuppressLaunchBrowser)
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
                else if (_options.TestFlags.HasFlag(TestFlags.BrowserRequired))
                {
                    _reporter.Error("Test requires browser to launch");
                }
            }

            return default;
        }

        private void OnOutput(object sender, DataReceivedEventArgs eventArgs)
        {
            Debug.Assert(_reporter != null);

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
            Debug.Assert(_reporter != null);

            var fileName = Uri.TryCreate(_launchPath, UriKind.Absolute, out _) ? _launchPath : launchUrl + "/" + _launchPath;
            var args = string.Empty;
            if (!string.IsNullOrEmpty(_browserPath))
            {
                args = fileName;
                fileName = _browserPath;
            }

            if (_options.TestFlags != TestFlags.None)
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

        private static bool CanLaunchBrowser(DotNetWatchContext context, out string? launchUrl)
        {
            Debug.Assert(context.ProcessSpec != null);
            Debug.Assert(context.FileSet?.Project != null);

            launchUrl = null;
            var reporter = context.Reporter;

            if (!context.FileSet.Project.IsNetCoreApp31OrNewer())
            {
                // Browser refresh middleware supports 3.1 or newer
                reporter.Verbose("Browser refresh is only supported in .NET Core 3.1 or newer projects.");
                return false;
            }

            if (!context.HotReloadEnabled)
            {
                var dotnetCommand = context.ProcessSpec.Arguments?.FirstOrDefault();
                if (!string.Equals(dotnetCommand, "run", StringComparison.Ordinal))
                {
                    reporter.Verbose("Browser refresh is only supported for run commands.");
                    return false;
                }
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
