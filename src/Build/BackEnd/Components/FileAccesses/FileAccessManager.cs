// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework.FileAccess;
using Microsoft.Build.Shared;

namespace Microsoft.Build.FileAccesses
{
    internal sealed class FileAccessManager : IFileAccessManager, IBuildComponent
    {
        private record Handlers(Action<BuildRequest, FileAccessData> FileAccessHander, Action<BuildRequest, ProcessData> ProcessHandler);

        private IScheduler? _scheduler;
        private IConfigCache? _configCache;

        private object _handlersWriteLock = new object();
        private Handlers[] _handlers = Array.Empty<Handlers>();

        public static IBuildComponent CreateComponent(BuildComponentType type)
        {
            ErrorUtilities.VerifyThrowArgumentOutOfRange(type == BuildComponentType.FileAccessManager, nameof(type));
            return new FileAccessManager();
        }

        public void InitializeComponent(IBuildComponentHost host)
        {
            _scheduler = host.GetComponent(BuildComponentType.Scheduler) as IScheduler;
            _configCache = host.GetComponent(BuildComponentType.ConfigCache) as IConfigCache;
        }

        public void ShutdownComponent()
        {
            _scheduler = null;
            _configCache = null;
        }

        public void ReportFileAccess(FileAccessData fileAccessData, int nodeId)
        {
            BuildRequest? buildRequest = GetBuildRequest(nodeId);
            if (buildRequest != null)
            {
                Handlers[] localHandlers = _handlers;
                foreach (Handlers handlers in localHandlers)
                {
                    handlers.FileAccessHander.Invoke(buildRequest, fileAccessData);
                }
            }
        }

        public void ReportProcess(ProcessData processData, int nodeId)
        {
            BuildRequest? buildRequest = GetBuildRequest(nodeId);
            if (buildRequest != null)
            {
                Handlers[] localHandlers = _handlers;
                foreach (Handlers handlers in localHandlers)
                {
                    handlers.ProcessHandler.Invoke(buildRequest, processData);
                }
            }
        }

        public HandlerRegistration RegisterHandlers(Action<BuildRequest, FileAccessData> fileAccessHandler, Action<BuildRequest, ProcessData> processHandler)
        {
            lock (_handlersWriteLock)
            {
                Handlers[] newHandlers = new Handlers[_handlers.Length + 1];
                _handlers.CopyTo(newHandlers, 0);

                Handlers addedHandlers = new(fileAccessHandler, processHandler);
                newHandlers[_handlers.Length] = addedHandlers;

                _handlers = newHandlers;

                return new HandlerRegistration(() => UnregisterHandlers(addedHandlers));
            }
        }

        private void UnregisterHandlers(Handlers handlersToRemove)
        {
            lock (_handlersWriteLock)
            {
                Handlers[] newHandlers = new Handlers[_handlers.Length - 1];
                int newHandlersIdx = 0;
                for (int handlersIdx = 0; handlersIdx < _handlers.Length; handlersIdx++)
                {
                    if (_handlers[handlersIdx] != handlersToRemove)
                    {
                        newHandlers[newHandlersIdx] = _handlers[handlersIdx];
                        newHandlersIdx++;
                    }
                }

                _handlers = newHandlers;
            }
        }

        private BuildRequest? GetBuildRequest(int nodeId)
        {
            ErrorUtilities.VerifyThrow(
                _scheduler != null && _configCache != null,
                "Component has not been initialized");

            // Note: If the node isn't executing anything it may be accessing binaries required to run, eg. the MSBuild binaries
            return _scheduler!.GetExecutingRequestByNode(nodeId);
        }

        internal readonly struct HandlerRegistration : IDisposable
        {
            private readonly Action _unregisterAction;

            public HandlerRegistration(Action unregisterAction)
            {
                _unregisterAction = unregisterAction;
            }

            public void Dispose()
            {
                _unregisterAction();
            }
        }
    }
}
