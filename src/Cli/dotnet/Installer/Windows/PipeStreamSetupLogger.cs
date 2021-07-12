// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable disable

using System.IO.Pipes;
using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Text;

namespace Microsoft.DotNet.Installer.Windows
{
    /// <summary>
    /// Provides logging support for external processes, allowing them to send log requests through a named pipe.
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal class PipeStreamSetupLogger : SetupLoggerBase, ISetupLogger
    {
        private PipeStreamMessageDispatcherBase _dispatcher;

        /// <summary>
        /// Queue to track log requests issued before the pipestream is connected.
        /// </summary>
        private readonly Queue<string> _messageQueue = new Queue<string>();

        public string LogPath
        {
            get;
            private set;
        }

        /// <summary>
        /// Creates a new <see cref="PipeStreamSetupLogger"/> instance.
        /// </summary>
        /// <param name="pipeStream">The <see cref="PipeStream"/> to use for sending log requests.</param>
        /// <param name="pipeName"></param>
        public PipeStreamSetupLogger(PipeStream pipeStream, string pipeName)
        {
            _dispatcher = new PipeStreamMessageDispatcherBase(pipeStream);
            LogPath = pipeName;
        }

        /// <summary>
        /// Waits for the underlying pipe stream to become connected.
        /// </summary>
        public void Connect()
        {
            _dispatcher.Connect();

            // Flush out any queued messages.
            while (_messageQueue.Count > 0)
            {
                if (_messageQueue.TryDequeue(out string message))
                {
                    _dispatcher.WriteMessage(Encoding.UTF8.GetBytes(message));
                }
            }
        }

        /// <summary>
        /// Writes the message to the underlying named pipe.
        /// </summary>
        /// <param name="message">The message to write.</param>
        protected override void WriteMessage(string message)
        {
            if (_dispatcher.IsConnected)
            {
                _dispatcher.WriteMessage(Encoding.UTF8.GetBytes(message));
            }
            else
            {
                _messageQueue.Enqueue(message);
            }
        }
    }
}
