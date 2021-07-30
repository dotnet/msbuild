// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable disable

using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;

namespace Microsoft.DotNet.Installer.Windows
{
    /// <summary>
    /// Represents a log file whose entries are timestamped. Additional loggers from external processes
    /// can send requests through named pipes to the logger. Each pipe is assigned a separate thread to
    /// queue messages.
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal class TimestampedFileLogger : SetupLoggerBase, IDisposable, ISetupLogger
    {
        /// <summary>
        /// Thread safe queue use to store incoming log request messages.
        /// </summary>
        private ConcurrentQueue<string> _messageQueue = new ConcurrentQueue<string>();

        private bool _disposed;
        private readonly StreamWriter _stream;

        /// <summary>
        /// The number of write operations that can execute before the underlying stream is flushed. The default
        /// value is 20.
        /// </summary>
        public readonly int FlushThreshold;

        /// <summary>
        /// The full path of the log file.
        /// </summary>
        public string LogPath
        {
            get;
        }

        private bool Done = false;

        private Thread LogWriter;

        /// <summary>
        /// The locale-neutral timestamp prefix.
        /// </summary>
        protected static string TimeStamp => $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}]";

        /// <summary>
        /// Creates a new <see cref="TimestampedFileLogger"/> instance.
        /// </summary>
        /// <param name="path">The path of the log file.</param>
        public TimestampedFileLogger(string path) : this(path, 20)
        {

        }

        /// <summary>
        /// Creates a new <see cref="TimestampedFileLogger"/> instance.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="flushThreshold"></param>
        /// <param name="logPipeNames"></param>
        public TimestampedFileLogger(string path, int flushThreshold, params string[] logPipeNames)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            _stream = File.CreateText(path);
            LogPath = Path.GetFullPath(path);
            FlushThreshold = flushThreshold;

            // Capture control events. While console applications do support the CancelKeyPres event, users can
            // terminate a CLI command by simply closing the command window, rebooting or logging off.
            //NativeMethods.Windows.SetConsoleCtrlHandler(CtrlHandler, true);
            AppDomain.CurrentDomain.ProcessExit += OnExit;

            // Spin up additional threads to listen for log requests coming in from external processes.
            foreach (string logPipeName in logPipeNames)
            {
                Thread logRequestThread = new Thread(ProcessLogRequests) { IsBackground = true };
                logRequestThread.Start(logPipeName);
            }

            // The primary thread responsible for dequeuing messages and writing
            // messages to the log file.
            LogWriter = new Thread(WriteLog) { IsBackground = true };
            LogWriter.Start(FlushThreshold);

            LogMessage($"=== Logging started ===");
        }

        /// <summary>
        /// Starts a new thread to list for log requests messages from external processes.
        /// </summary>
        /// <param name="pipeName">The name of the pipe.</param>
        public void AddNamedPipe(string pipeName)
        {
            Thread logRequestThread = new Thread(ProcessLogRequests) { IsBackground = true };
            logRequestThread.Start(pipeName);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                Done = true;
                LogWriter?.Join();
                _stream.WriteLine($"{TimeStamp} {FormatMessage("=== Logging ended ===")}");

                if (disposing)
                {
                    _stream?.Dispose();
                }

                _disposed = true;
            }
        }

        /// <summary>
        /// Event handler for when the current process terminates.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnExit(object sender, EventArgs e)
        {
            LogMessage($"Process is exiting");
            Dispose();
        }

        /// <summary>
        /// Thread used to process external log requests received from a single pipe.
        /// </summary>
        /// <param name="logPipeName"></param>
        private void ProcessLogRequests(object logPipeName)
        {
            NamedPipeClientStream logPipe = new NamedPipeClientStream(".", (string)logPipeName, PipeDirection.InOut);
            PipeStreamMessageDispatcherBase dispatcher = new(logPipe);
            dispatcher.Connect();
            LogMessage($"Log connected.");

            while (dispatcher.IsConnected)
            {
                try
                {
                    // We'll block waiting for messages to arrive before sending them to the queue. We don't call LogMessage
                    // directly since the external logger should have stamped the message with the process ID.
                    WriteMessage(UTF8Encoding.UTF8.GetString(dispatcher.ReadMessage()));
                }
                catch (Exception e)
                {
                    LogMessage(e.Message);
                }
            }

            LogMessage($"Shutting down log processor. Thread ID: {Thread.CurrentThread.ManagedThreadId}, Dispatcher connected: {dispatcher.IsConnected}");
        }

        /// <summary>
        /// Primary thread for dequeuing messages, timestamping them, and writing them to the log file.
        /// </summary>
        private void WriteLog(object flushThreshold)
        {
            int writeCount = 0;
            int threshold = (int)flushThreshold;

            while (!Done)
            {
                if (_messageQueue.TryDequeue(out string message))
                {
                    _stream.WriteLine($"{TimeStamp} {message}");
                    writeCount = (writeCount + 1) % threshold;

                    if (writeCount == 0)
                    {
                        _stream.Flush();
                    }
                }
            }

            // Clean out any remaining messages.
            while (!_messageQueue.IsEmpty)
            {
                if (_messageQueue.TryDequeue(out string message))
                {
                    _stream.WriteLine($"{TimeStamp} {message}");
                }
            }

            _stream.Flush();
        }

        protected override void WriteMessage(string message)
        {
            _messageQueue.Enqueue(message);
        }
    }
}
