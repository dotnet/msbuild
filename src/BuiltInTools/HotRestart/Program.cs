// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Loader;

namespace Microsoft.Extensions.HotReload
{
    internal static class Program
    {
        public static void Main(string[] args)
        {
            const string envName = "_DOTNET_WATCH_APP_ASSEMBLY_PATH";
            var appAssemblyPath = Environment.GetEnvironmentVariable(envName) ??
                throw new InvalidOperationException($"No value found for environment variable '{envName}'.");

            var observer = new Observer();
            using var _ = DiagnosticListener.AllListeners.Subscribe(observer);

            // Microsoft.Extensions.DotNetApplier's startup hook emulates Control-C to terminate the running app when
            // it sees changes to Startup.cs / Program.cs during a hot reload session.
            // We need to differentiate between the emulated Control-C and a Control-C performed by the user on the terminal.
            // We use a DiagnosticListener to signal any time the StartupHook emulates Control-C, so that we know
            // when it is not-signaled, it must be a real Control-C.

            var first = true;
            while (observer.EmulatedControlC)
            {
                observer.EmulatedControlC = false;
                if (!first)
                {
                    Console.WriteLine("[watch] Application restarting because changes were detected to the app's initialization code.");
                }
                first = false;

                var currentLoadContext = new HotRestartLoadContext();
                var mainAssembly = currentLoadContext.LoadFromAssemblyPath(appAssemblyPath);
                // See https://github.com/dotnet/coreclr/blob/master/Documentation/design-docs/AssemblyLoadContext.ContextualReflection.md for details.
                using var __ = AssemblyLoadContext.EnterContextualReflection(mainAssembly);

                mainAssembly.EntryPoint!.Invoke(null, new object[] { args });
                currentLoadContext.Unload();
            }
        }

        internal sealed class HotRestartLoadContext : AssemblyLoadContext
        {
            public HotRestartLoadContext() :
                base(isCollectible: true)
            {
            }
        }

        private sealed class Observer : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>
        {
            public volatile bool EmulatedControlC = true;

            public void OnCompleted() { }
            public void OnError(Exception error) { }
            public void OnNext(DiagnosticListener value)
            {
                if (value.Name == "_DOTNET_WATCH_EMULATED_CONTROL_C")
                {
                    value.Subscribe(this);
                }
            }

            public void OnNext(KeyValuePair<string, object?> value)
            {
                EmulatedControlC = true;
            }
        }
    }
}
