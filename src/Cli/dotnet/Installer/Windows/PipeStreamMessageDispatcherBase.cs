// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.Pipes;
using System.Runtime.Versioning;

namespace Microsoft.DotNet.Installer.Windows
{
    /// <summary>
    /// Base class used for dispatching messages (<see cref="PipeTransmissionMode.Message"/>) over a named pipe.
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal class PipeStreamMessageDispatcherBase
    {
        /// <summary>
        /// The maxmimum length of a message.
        /// </summary>
        public const int MaxMessageSize = 2048;

        /// <summary>
        /// The backing stream used for reading & writing messages.
        /// </summary>
        private PipeStream _pipeStream;

        /// <summary>
        /// The number of milliseconds to wait for a pipe connection to be established. See <see cref="Connect"/>.
        /// </summary>
        public const int ConnectionTimeout = 30000;

        /// <summary>
        /// Returns <see langword="true"/> if the dispatcher is using a <see cref="NamedPipeClientStream"/>.
        /// </summary>
        public bool IsClient => _pipeStream is NamedPipeServerStream;

        /// <summary>
        /// Gets whether the underlying stream is connected.
        /// </summary>
        public bool IsConnected => _pipeStream.IsConnected;

        /// <summary>
        /// Creates a new <see cref="PipeStreamMessageDispatcherBase"/> instance.
        /// </summary>
        /// <param name="pipeStream">The pipe stream to use for reading and writing messages. The pipe must be configured
        /// to use <see cref="PipeTransmissionMode.Message"/>.</param>
        /// <exception cref="ArgumentNullException" />
        public PipeStreamMessageDispatcherBase(PipeStream pipeStream)
        {
            _pipeStream = pipeStream ?? throw new ArgumentNullException(nameof(pipeStream));
        }

        /// <summary>
        /// Waits for the underlying <see cref="PipeStream"/> to establish a connection. If the stream is a 
        /// <see cref="NamedPipeClientStream"/>, its transmission mode will be set to <see cref="PipeTransmissionMode.Message"/>
        /// once the connection is established.
        /// </summary>
        /// <exception cref="TimeoutException"/>
        public void Connect()
        {
            if (!_pipeStream.IsConnected)
            {
                if (_pipeStream is NamedPipeServerStream)
                {
                    Task connectTask = Task.Factory.StartNew(() => ((NamedPipeServerStream)_pipeStream).WaitForConnection());
                    connectTask.Wait(ConnectionTimeout);

                    if (!connectTask.IsCompleted)
                    {
                        throw new TimeoutException("Timed out waiting for connection.");
                    }
                }
                else if (_pipeStream is NamedPipeClientStream)
                {
                    ((NamedPipeClientStream)_pipeStream).Connect(ConnectionTimeout);
                    // We cannot set the transmission mode of a pipe client until it's connected.
                    _pipeStream.ReadMode = PipeTransmissionMode.Message;
                }
            }
        }

        /// <summary>
        /// Reads a message from the underlying pipe stream. This method blocks until
        /// data is available to read.
        /// </summary>
        /// <returns>The message read from the pipe.</returns>
        public byte[] ReadMessage()
        {
            byte[] message = new byte[2048];
            int bytesRead = _pipeStream.Read(message, 0, message.Length);
            int messageLength = BitConverter.ToInt32(message, 0);
            byte[] messageBytes = new byte[messageLength];
            Array.Copy(message, 4, messageBytes, 0, messageLength);

            return messageBytes;
        }

        /// <summary>
        /// Writes a message to the underlying pipe stream.
        /// </summary>
        /// <param name="messageBytes">The message to write.</param>
        public void WriteMessage(byte[] messageBytes)
        {
            byte[] messageLengthBytes = BitConverter.GetBytes(messageBytes.Length);
            byte[] msg = messageLengthBytes.Concat(messageBytes).ToArray();

            if (msg.Length > MaxMessageSize)
            {
                throw new IOException($"The message is too long.");
            }

            _pipeStream.Write(msg, 0, msg.Length);
            _pipeStream.WaitForPipeDrain();
        }
    }
}
