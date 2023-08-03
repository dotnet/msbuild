// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Runtime.Versioning;

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
        private readonly BlockingCollection<string> _messageQueue = new BlockingCollection<string>();

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
        /// <param name="path">The path of the log file.</param>
        /// <param name="flushThreshold">The number of writes to allow before flushing the underlying stream.</param>
        /// <param name="logPipeNames">Additional named pipes that can be used to send log requests from other processes.</param>
        public TimestampedFileLogger(string path, int flushThreshold, params string[] logPipeNames)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            _stream = File.CreateText(path);
            LogPath = Path.GetFullPath(path);
            FlushThreshold = flushThreshold;

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
        /// Starts a new thread to listen for log requests messages from external processes.
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
                _messageQueue.CompleteAdding();
                LogWriter?.Join();
                _stream.WriteLine($"{TimeStamp} {FormatMessage("=== Logging ended ===")}");

                if (disposing)
                {
                    _stream?.Dispose();
                    _messageQueue.Dispose();
                }

                _disposed = true;
            }
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
            LogMessage($"Log connected: {logPipeName}.");

            while (dispatcher.IsConnected)
            {
                try
                {
                    // We'll block waiting for messages to arrive before sending them to the queue. We don't call LogMessage
                    // directly since the external logger should have stamped the message with the process ID.
                    string msg = UTF8Encoding.UTF8.GetString(dispatcher.ReadMessage());

                    if (!string.IsNullOrWhiteSpace(msg))
                    {
                        WriteMessage(msg);
                    }
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

            foreach (string message in _messageQueue.GetConsumingEnumerable())
            {
                _stream.WriteLine($"{TimeStamp} {message}");
                writeCount = (writeCount + 1) % threshold;

                if (writeCount == 0)
                {
                    _stream.Flush();
                }
            }
        }

        /// <summary>
        /// Writes the specified message to the log file. The message will first be added to an internal queue to be timestamped
        /// before it's dequeued and written to the log.
        /// </summary>
        /// <param name="message">The message to log.</param>
        protected override void WriteMessage(string message)
        {
            if (!_messageQueue.IsAddingCompleted)
            {
                _messageQueue.Add(message);
            }
        }
    }
}
