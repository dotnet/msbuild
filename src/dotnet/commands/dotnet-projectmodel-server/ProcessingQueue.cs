// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Threading;
using Microsoft.DotNet.ProjectModel.Server.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Microsoft.DotNet.ProjectModel.Server
{
    internal class ProcessingQueue
    {
        private readonly BinaryReader _reader;
        private readonly BinaryWriter _writer;
        private readonly ILogger _log;

        public ProcessingQueue(Stream stream, ILoggerFactory loggerFactory)
        {
            _reader = new BinaryReader(stream);
            _writer = new BinaryWriter(stream);
            _log = loggerFactory.CreateLogger<ProcessingQueue>();
        }

        public event Action<Message> OnReceive;

        public void Start()
        {
            _log.LogInformation("Start");
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
                    _log.LogInformation($"Ignore {nameof(IOException)} during sending message: \"{ex.Message}\".");
                }
                catch (Exception ex)
                {
                    _log.LogWarning($"Unexpected exception {ex.GetType().Name} during sending message: \"{ex.Message}\".");
                    throw;
                }
            }

            return false;
        }

        public bool Send(Message message)
        {
            return Send(_writer =>
            {
                _log.LogInformation($"Send ({message})");
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

                    _log.LogInformation($"OnReceive({message})");
                    OnReceive(message);
                }
            }
            catch (IOException ex)
            {
                _log.LogInformation($"Ignore {nameof(IOException)} during receiving messages: \"{ex}\".");
            }
            catch (Exception ex)
            {
                _log.LogError($"Unexpected exception {ex.GetType().Name} during receiving messages: \"{ex}\".");
            }
        }
    }
}
