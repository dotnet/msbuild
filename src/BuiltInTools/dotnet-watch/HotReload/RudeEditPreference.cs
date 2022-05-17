// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Tools
{
    public class RudeEditDialog
    {
        private readonly IReporter _reporter;
        private readonly IConsole _console;
        private bool? _restartImmediatelySessionPreference; // Session preference

        public RudeEditDialog(IReporter reporter, IConsole console)
        {
            _reporter = reporter;
            _console = console;
        }

        public async Task EvaluateAsync(CancellationToken cancellationToken)
        {
            if (_restartImmediatelySessionPreference.HasValue)
            {
                await GetRudeEditResult(_restartImmediatelySessionPreference.Value, cancellationToken);
                return;
            }

            while (true)
            {
                _reporter.Output("Do you want to restart your app - Yes (y) / No (n) / Always (a) / Never (v)?");
                var tcs = new TaskCompletionSource<ConsoleKeyInfo>(TaskCreationOptions.RunContinuationsAsynchronously);
                _console.KeyPressed += KeyPressed;
                ConsoleKeyInfo key;
                try
                {
                    key = await tcs.Task.WaitAsync(cancellationToken);
                }
                finally
                {
                    _console.KeyPressed -= KeyPressed;
                }

                switch (key.Key)
                {
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

                void KeyPressed(ConsoleKeyInfo key)
                {
                    if (key.Key is ConsoleKey.Y or ConsoleKey.N or ConsoleKey.A or ConsoleKey.V)
                    {
                        tcs.TrySetResult(key);
                    }
                }
            }
        }

        private Task GetRudeEditResult(bool restartImmediately, CancellationToken cancellationToken)
        {
            if (restartImmediately)
            {
                return Task.CompletedTask;
            }

            _reporter.Output("Hot reload suspended. To continue hot reload, press \"Ctrl + R\".");

            return Task.Delay(-1, cancellationToken);
        }
    }
}
