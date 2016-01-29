// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.DotNet.ProjectModel.Server.Helpers;
using Microsoft.DotNet.ProjectModel.Server.Messengers;
using Microsoft.DotNet.ProjectModel.Server.Models;
using Microsoft.Extensions.Logging;
using NuGet.Frameworks;

namespace Microsoft.DotNet.ProjectModel.Server
{
    internal class ProjectManager
    {
        private readonly ILogger _log;

        private readonly object _processingLock = new object();
        private readonly Queue<Message> _inbox = new Queue<Message>();
        private readonly ProtocolManager _protocolManager;

        private ConnectionContext _initializedContext;

        // triggers
        private readonly Trigger<string> _appPath = new Trigger<string>();
        private readonly Trigger<string> _configure = new Trigger<string>();
        private readonly Trigger<int> _refreshDependencies = new Trigger<int>();
        private readonly Trigger<int> _filesChanged = new Trigger<int>();

        private ProjectSnapshot _local = new ProjectSnapshot();
        private ProjectSnapshot _remote = new ProjectSnapshot();

        private readonly WorkspaceContext _workspaceContext;
        private int? _contextProtocolVersion;

        private readonly List<Messenger<ProjectContextSnapshot>> _messengers;

        private ProjectDiagnosticsMessenger _projectDiagnosticsMessenger;
        private GlobalErrorMessenger _globalErrorMessenger;
        private ProjectInformationMessenger _projectInforamtionMessenger;

        public ProjectManager(int contextId,
                                     ILoggerFactory loggerFactory,
                                     WorkspaceContext workspaceContext,
                                     ProtocolManager protocolManager)
        {
            Id = contextId;
            _log = loggerFactory.CreateLogger<ProjectManager>();
            _workspaceContext = workspaceContext;
            _protocolManager = protocolManager;

            _messengers = new List<Messenger<ProjectContextSnapshot>>
            {
                new DependencyDiagnosticsMessenger(Transmit),
                new ReferencesMessenger(Transmit),
                new DependenciesMessenger(Transmit),
                new CompilerOptionsMessenger(Transmit),
                new SourcesMessenger(Transmit)
            };

            _projectDiagnosticsMessenger = new ProjectDiagnosticsMessenger(Transmit);
            _globalErrorMessenger = new GlobalErrorMessenger(Transmit);
            _projectInforamtionMessenger = new ProjectInformationMessenger(Transmit);
        }

        public int Id { get; }

        public string ProjectPath { get { return _appPath.Value; } }

        public int ProtocolVersion
        {
            get
            {
                if (_contextProtocolVersion.HasValue)
                {
                    return _contextProtocolVersion.Value;
                }
                else
                {
                    return _protocolManager.CurrentVersion;
                }
            }
        }

        public void OnReceive(Message message)
        {
            lock (_inbox)
            {
                _inbox.Enqueue(message);
            }

            ThreadPool.QueueUserWorkItem(state => ((ProjectManager)state).ProcessLoop(), this);
        }

        private void Transmit(string messageType, object payload)
        {
            var message = Message.FromPayload(messageType, Id, payload);
            _initializedContext.Transmit(message);
        }

        private void ProcessLoop()
        {
            if (!Monitor.TryEnter(_processingLock))
            {
                return;
            }

            try
            {
                lock (_inbox)
                {
                    if (!_inbox.Any())
                    {
                        return;
                    }
                }

                DoProcessLoop();
            }
            catch (Exception ex)
            {
                // TODO: review error handing logic

                _log.LogError($"Error occurred: {ex}");

                var error = new ErrorMessage
                {
                    Message = ex.Message
                };

                var fileFormatException = ex as FileFormatException;
                if (fileFormatException != null)
                {
                    error.Path = fileFormatException.Path;
                    error.Line = fileFormatException.Line;
                    error.Column = fileFormatException.Column;
                }

                var message = Message.FromPayload(MessageTypes.Error, Id, error);

                _initializedContext.Transmit(message);
                _remote.GlobalErrorMessage = error;
            }
            finally
            {
                Monitor.Exit(_processingLock);
            }
        }

        private void DoProcessLoop()
        {
            while (true)
            {
                DrainInbox();

                UpdateProject();
                SendOutgingMessages();

                lock (_inbox)
                {
                    if (_inbox.Count == 0)
                    {
                        return;
                    }
                }
            }
        }

        private void DrainInbox()
        {
            _log.LogInformation("Begin draining inbox.");

            while (ProcessMessage()) { }

            _log.LogInformation("Finish draining inbox.");
        }

        private bool ProcessMessage()
        {
            Message message;

            lock (_inbox)
            {
                if (!_inbox.Any())
                {
                    return false;
                }

                message = _inbox.Dequeue();
                Debug.Assert(message != null);
            }

            _log.LogInformation($"Received {message.MessageType}");

            switch (message.MessageType)
            {
                case MessageTypes.Initialize:
                    Initialize(message);
                    break;
                case MessageTypes.ChangeConfiguration:
                    // TODO: what if the payload is null or represent empty string?
                    _configure.Value = message.Payload.GetValue("Configuration");
                    break;
                case MessageTypes.RefreshDependencies:
                case MessageTypes.RestoreComplete:
                    _refreshDependencies.Value = 0;
                    break;
                case MessageTypes.FilesChanged:
                    _filesChanged.Value = 0;
                    break;
            }

            return true;
        }

        private void Initialize(Message message)
        {
            if (_initializedContext != null)
            {
                _log.LogWarning($"Received {message.MessageType} message more than once for {_appPath.Value}");
                return;
            }

            _initializedContext = message.Sender;
            _appPath.Value = message.Payload.GetValue("ProjectFolder");
            _configure.Value = message.Payload.GetValue("Configuration") ?? "Debug";

            var version = message.Payload.GetValue<int>("Version");
            if (version != 0 && !_protocolManager.EnvironmentOverridden)
            {
                _contextProtocolVersion = Math.Min(version, _protocolManager.MaxVersion);
                _log.LogInformation($"Set context protocol version to {_contextProtocolVersion.Value}");
            }
        }

        private bool UpdateProject()
        {
            ProjectSnapshot newSnapshot = null;

            if (_appPath.WasAssigned || _configure.WasAssigned || _filesChanged.WasAssigned || _refreshDependencies.WasAssigned)
            {
                _appPath.ClearAssigned();
                _configure.ClearAssigned();
                _filesChanged.ClearAssigned();
                _refreshDependencies.ClearAssigned();

                newSnapshot = ProjectSnapshot.Create(_appPath.Value, _configure.Value, _workspaceContext, _remote.ProjectSearchPaths);
            }

            if (newSnapshot == null)
            {
                return false;
            }

            _local = newSnapshot;

            return true;
        }

        private void SendOutgingMessages()
        {
            _projectInforamtionMessenger.UpdateRemote(_local, _remote);
            _projectDiagnosticsMessenger.UpdateRemote(_local, _remote);

            var unprocessedFrameworks = new HashSet<NuGetFramework>(_remote.ProjectContexts.Keys);
            foreach (var pair in _local.ProjectContexts)
            {
                ProjectContextSnapshot localProjectSnapshot = pair.Value;
                ProjectContextSnapshot remoteProjectSnapshot;

                if (!_remote.ProjectContexts.TryGetValue(pair.Key, out remoteProjectSnapshot))
                {
                    remoteProjectSnapshot = new ProjectContextSnapshot();
                    _remote.ProjectContexts[pair.Key] = remoteProjectSnapshot;
                }

                unprocessedFrameworks.Remove(pair.Key);

                foreach (var messenger in _messengers)
                {
                    messenger.UpdateRemote(localProjectSnapshot,
                                           remoteProjectSnapshot);
                }
            }

            // Remove all processed frameworks from the remote view
            foreach (var framework in unprocessedFrameworks)
            {
                _remote.ProjectContexts.Remove(framework);
            }

            _globalErrorMessenger.UpdateRemote(_local, _remote);
        }

        private class Trigger<TValue>
        {
            private TValue _value;

            public bool WasAssigned { get; private set; }

            public void ClearAssigned()
            {
                WasAssigned = false;
            }

            public TValue Value
            {
                get { return _value; }
                set
                {
                    WasAssigned = true;
                    _value = value;
                }
            }
        }
    }
}
