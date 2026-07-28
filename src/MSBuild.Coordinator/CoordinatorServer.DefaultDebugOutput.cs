// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Coordinator;

namespace Microsoft.Build.Coordinator;

internal sealed partial class CoordinatorServer
{
    /// <summary>
    ///  Debug tracing output for the coordinator server. Writes timestamped entries
    ///  to a file, gated on the MSBUILDDEBUGCOMM environment variable.
    /// </summary>
    private sealed class DefaultDebugOutput : ICoordinatorDebugOutput
    {
        private static readonly bool s_isEnabled = Traits.Instance.DebugNodeCommunication;

        private static readonly object s_lock = new();
        private static long s_lastTicks = DateTime.UtcNow.Ticks;

        private readonly string _debugDumpDirectory;
        private readonly string _debugDumpTraceFilePath;

        public static readonly DefaultDebugOutput Instance = new();

        private DefaultDebugOutput()
        {
            _debugDumpDirectory = FrameworkDebugUtils.DebugPath;

            if (string.IsNullOrEmpty(_debugDumpDirectory))
            {
                _debugDumpDirectory = FileUtilities.TempFileDirectory;
            }

            _debugDumpTraceFilePath = Path.Combine(_debugDumpDirectory, $"MSBuild_CoordinatorTrace_PID_{EnvironmentUtilities.CurrentProcessId}.txt");
        }

        public bool IsEnabled => s_isEnabled;

        public void WriteLine(string message)
        {
            if (s_isEnabled)
            {
                WriteLineCore(message);
            }
        }

        public void WriteLine([InterpolatedStringHandlerArgument("")] ref ICoordinatorDebugOutput.WriteLineInterpolatedStringHandler handler)
        {
            if (s_isEnabled)
            {
                WriteLineCore(handler.GetFormattedText());
            }
        }

        private void WriteLineCore(string message)
        {
            lock (s_lock)
            {
                FileUtilities.EnsureDirectoryExists(_debugDumpDirectory);

                try
                {
                    using (StreamWriter writer = FileUtilities.OpenWrite(_debugDumpTraceFilePath, append: true))
                    {
                        long now = DateTime.UtcNow.Ticks;
                        float millisecondsSinceLastLog = (float)(now - s_lastTicks) / 10000L;
                        s_lastTicks = now;

                        writer.WriteLine($"{Thread.CurrentThread.Name} (TID {Environment.CurrentManagedThreadId}) {now,15} +{millisecondsSinceLastLog,10}ms: {message}");
                    }
                }
                catch (IOException)
                {
                    // Ignore
                }
            }
        }
    }
}
