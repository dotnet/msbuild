// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Extensions.Tools.Internal
{
    /// <summary>
    /// This API supports infrastructure and is not intended to be used
    /// directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public class PhysicalConsole : IConsole
    {
        private CancellationTokenSource _cancellationTokenSource;

        private PhysicalConsole()
        {
            Console.CancelKeyPress += (o, e) =>
            {
                CancelKeyPress?.Invoke(o, e);
            };
        }

        public CancellationToken ListenForForceReloadRequest()
        {
            _cancellationTokenSource ??= new CancellationTokenSource();

            Task.Run(() =>
            {
                var key = Console.ReadKey(intercept: true);
                if (key.Modifiers == ConsoleModifiers.Control && key.Key == ConsoleKey.R)
                {
                    var cancellationTokenSource = Interlocked.Exchange(ref _cancellationTokenSource, new CancellationTokenSource());

                    cancellationTokenSource.Cancel();
                    cancellationTokenSource.Dispose();
                }
            });

            return _cancellationTokenSource.Token;
        }

        public static IConsole Singleton { get; } = new PhysicalConsole();

        public event ConsoleCancelEventHandler CancelKeyPress;
        public TextWriter Error => Console.Error;
        public TextReader In => Console.In;
        public TextWriter Out => Console.Out;
        public bool IsInputRedirected => Console.IsInputRedirected;
        public bool IsOutputRedirected => Console.IsOutputRedirected;
        public bool IsErrorRedirected => Console.IsErrorRedirected;
        public ConsoleColor ForegroundColor
        {
            get => Console.ForegroundColor;
            set => Console.ForegroundColor = value;
        }

        public void ResetColor() => Console.ResetColor();
        public void Clear() => Console.Clear();
    }
}
