// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Tools
{
    internal sealed class RudeEditDialog
    {
        private readonly IReporter _reporter;
        private readonly IRequester _requester;
        private readonly IConsole _console;
        private bool? _restartImmediatelySessionPreference; // Session preference

        public RudeEditDialog(IReporter reporter, IRequester requester, IConsole console)
        {
            _reporter = reporter;
            _requester = requester;
            _console = console;

            var alwaysRestart = Environment.GetEnvironmentVariable("DOTNET_WATCH_RESTART_ON_RUDE_EDIT");

            if (alwaysRestart == "1" || string.Equals(alwaysRestart, "true", StringComparison.OrdinalIgnoreCase))
            {
                _reporter.Verbose($"DOTNET_WATCH_RESTART_ON_RUDE_EDIT = '{alwaysRestart}'. Restarting without prompt.");
                _restartImmediatelySessionPreference = true;
            }
        }

        public async Task EvaluateAsync(CancellationToken cancellationToken)
        {
            if (_restartImmediatelySessionPreference.HasValue)
            {
                await GetRudeEditResult(_restartImmediatelySessionPreference.Value, cancellationToken);
                return;
            }

            var key = await _requester.GetKeyAsync(
                "Do you want to restart your app - Yes (y) / No (n) / Always (a) / Never (v)?",
                KeyPressed,
                cancellationToken);

            switch (key)
            {
                case ConsoleKey.Escape:
                case ConsoleKey.Y:
                    await GetRudeEditResult(restartImmediately: true, cancellationToken);
                    return;
                case ConsoleKey.N:
                    await GetRudeEditResult(restartImmediately: false, cancellationToken);
                    return;
                case ConsoleKey.A:
                    _restartImmediatelySessionPreference = true;
                    await GetRudeEditResult(restartImmediately: true, cancellationToken);
                    return;
                case ConsoleKey.V:
                    _restartImmediatelySessionPreference = false;
                    await GetRudeEditResult(restartImmediately: false, cancellationToken);
                    return;
            }

            static bool KeyPressed(ConsoleKey key)
            {
                return key is ConsoleKey.Y or ConsoleKey.N or ConsoleKey.A or ConsoleKey.V;
            }
        }

        private Task GetRudeEditResult(bool restartImmediately, CancellationToken cancellationToken)
        {
            if (restartImmediately)
            {
                return Task.CompletedTask;
            }

            _reporter.Output("Hot reload suspended. To continue hot reload, press \"Ctrl + R\".", emoji: "🔥");

            return Task.Delay(-1, cancellationToken);
        }
    }
}
