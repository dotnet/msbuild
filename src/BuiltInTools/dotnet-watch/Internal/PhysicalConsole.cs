// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Extensions.Tools.Internal
{
    /// <summary>
    /// This API supports infrastructure and is not intended to be used
    /// directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public class PhysicalConsole : IConsole
    {
        private readonly List<Action<ConsoleKeyInfo>> _keyPressedListeners = new();

        private PhysicalConsole()
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.CancelKeyPress += (o, e) =>
            {
                CancelKeyPress?.Invoke(o, e);
            };
        }

        public event Action<ConsoleKeyInfo> KeyPressed
        {
            add
            {
                _keyPressedListeners.Add(value);
                ListenToConsoleKeyPress();
            }

            remove => _keyPressedListeners.Remove(value);
        }

        private void ListenToConsoleKeyPress()
        {
            Task.Factory.StartNew(() =>
            {
                while (true)
                {
                    var key = Console.ReadKey(intercept: true);
                    for (var i = 0; i < _keyPressedListeners.Count; i++)
                    {
                        _keyPressedListeners[i](key);
                    }
                }
            }, TaskCreationOptions.LongRunning);
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
