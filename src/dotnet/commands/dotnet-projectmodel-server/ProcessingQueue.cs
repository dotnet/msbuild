// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Threading;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ProjectModel.Server.Models;
using Newtonsoft.Json;

namespace Microsoft.DotNet.ProjectModel.Server
{
    internal class ProcessingQueue
    {
        private readonly BinaryReader _reader;
        private readonly BinaryWriter _writer;

        public ProcessingQueue(Stream stream)
        {
            _reader = new BinaryReader(stream);
            _writer = new BinaryWriter(stream);
        }

        public event Action<Message> OnReceive;

        public void Start()
        {
            Reporter.Output.WriteLine("Start");
            new Thread(ReceiveMessages).Start();
        }

        public bool Send(Action<BinaryWriter> writeAction)
        {
            lock (_writer)
            {
                try
                {
                    writeAction(_writer);
                    return true;
                }
                catch (IOException ex)
                {
                    // swallow
                    Reporter.Output.WriteLine($"Ignore {nameof(IOException)} during sending message: \"{ex.Message}\".");
                }
                catch (Exception ex)
                {
                    Reporter.Output.WriteLine($"Unexpected exception {ex.GetType().Name} during sending message: \"{ex.Message}\".");
                    throw;
                }
            }

            return false;
        }

        public bool Send(Message message)
        {
            return Send(_writer =>
            {
                Reporter.Output.WriteLine($"OnSend ({message})");
                _writer.Write(JsonConvert.SerializeObject(message));
            });
        }

        private void ReceiveMessages()
        {
            try
            {
                while (true)
                {
                    var content = _reader.ReadString();
                    var message = JsonConvert.DeserializeObject<Message>(content);

                    Reporter.Output.WriteLine($"OnReceive ({message})");
                    OnReceive(message);
                }
            }
            catch (IOException ex)
            {
                Reporter.Output.WriteLine($"Ignore {nameof(IOException)} during receiving messages: \"{ex}\".");
            }
            catch (Exception ex)
            {
                Reporter.Error.WriteLine($"Unexpected exception {ex.GetType().Name} during receiving messages: \"{ex}\".");
            }
        }
    }
}
