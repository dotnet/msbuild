// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Net.Sockets;
using Microsoft.DotNet.ProjectModel.Server.Models;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.ProjectModel.Server
{
    internal class ConnectionContext
    {
        private readonly string _hostName;
        private readonly ProcessingQueue _queue;
        private readonly IDictionary<int, ProjectManager> _projects;

        public ConnectionContext(Socket acceptedSocket,
            string hostName,
            ProtocolManager protocolManager,
            WorkspaceContext workspaceContext,
            IDictionary<int, ProjectManager> projects,
            ILoggerFactory loggerFactory)
        {
            _hostName = hostName;
            _projects = projects;

            _queue = new ProcessingQueue(new NetworkStream(acceptedSocket), loggerFactory);
            _queue.OnReceive += message =>
            {
                if (protocolManager.IsProtocolNegotiation(message))
                {
                    message.Sender = this;
                    protocolManager.Negotiate(message);
                }
                else
                {
                    message.Sender = this;
                    ProjectManager projectManager;
                    if (!_projects.TryGetValue(message.ContextId, out projectManager))
                    {
                        projectManager = new ProjectManager(message.ContextId,
                                                           loggerFactory,
                                                           workspaceContext,
                                                           protocolManager);

                        _projects[message.ContextId] = projectManager;
                    }

                    projectManager.OnReceive(message);
                }
            };
        }

        public void QueueStart()
        {
            _queue.Start();
        }

        public bool Transmit(Message message)
        {
            message.HostId = _hostName;
            return _queue.Send(message);
        }
    }
}