// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if FEATURE_REPORTFILEACCESSES
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.Versioning;
using System.Threading;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using Microsoft.Build.Experimental.FileAccess;
using Microsoft.Build.Shared;

namespace Microsoft.Build.FileAccesses
{
    internal sealed class FileAccessManager : IFileAccessManager
    {
        private record Handlers(Action<BuildRequest, FileAccessData> FileAccessHander, Action<BuildRequest, ProcessData> ProcessHandler);

        // In order to synchronize between the node communication and the file access reporting, a special file access
        // is used to mark when the file accesses should be considered complete. Only after both this special file access is seen
        // and the build result is reported can plugins be notified about project completion.
        // NOTE! This is currently Windows-specific and will need to change once this feature is opened up to more scenarios.
        private static readonly string FileAccessCompletionPrefix = $@"{BuildParameters.StartupDirectory[0]}:\{{MSBuildFileAccessCompletion}}\";

        private IScheduler? _scheduler;
        private IConfigCache? _configCache;

        private object _handlersWriteLock = new object();
        private Handlers[] _handlers = Array.Empty<Handlers>();
        private string? _tempDirectory;

        // Keyed on global request id
        private readonly ConcurrentDictionary<int, ManualResetEventSlim> _fileAccessCompletionWaitHandles = new();

        public static IBuildComponent CreateComponent(BuildComponentType type)
        {
            ErrorUtilities.VerifyThrowArgumentOutOfRange(type == BuildComponentType.FileAccessManager, nameof(type));
            return new FileAccessManager();
        }

        public void InitializeComponent(IBuildComponentHost host)
        {
            _scheduler = host.GetComponent(BuildComponentType.Scheduler) as IScheduler;
            _configCache = host.GetComponent(BuildComponentType.ConfigCache) as IConfigCache;
            _tempDirectory = FileUtilities.EnsureNoTrailingSlash(FileUtilities.TempFileDirectory);
        }

        public void ShutdownComponent()
        {
            _scheduler = null;
            _configCache = null;
            _tempDirectory = null;
            _fileAccessCompletionWaitHandles.Clear();
        }

        public void ReportFileAccess(FileAccessData fileAccessData, int nodeId)
        {
            string fileAccessPath = fileAccessData.Path;

            // Intercept and avoid forwarding the file access completion
            if (fileAccessPath.StartsWith(FileAccessCompletionPrefix, StringComparison.Ordinal))
            {
                // Parse out the global request id. Note, this must match what NotifyFileAccessCompletion does.
                int globalRequestId = int.Parse(fileAccessPath.Substring(FileAccessCompletionPrefix.Length));

                ManualResetEventSlim handle = _fileAccessCompletionWaitHandles.GetOrAdd(globalRequestId, static _ => new ManualResetEventSlim());
                handle.Set();
            }
            else if (_tempDirectory != null && fileAccessPath.StartsWith(_tempDirectory))
            {
                // Ignore MSBuild's temp directory as these are related to internal MSBuild functionality and not always directly related to the execution of the project itself,
                // so should not be exposed to handlers. Note that this is not %TEMP% but instead a subdir under %TEMP% which is only expected to be used by MSBuild.
                return;
            }
            else
            {
                // Forward the file access to handlers.
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

        // The [SupportedOSPlatform] attribute is a safeguard to ensure that the comment on FileAccessCompletionPrefix regarding being Windows-only gets addressed.
        // [SupportedOSPlatform] doesn't apply to fields, so using it here as a reasonable proxy.
        [SupportedOSPlatform("windows")]
        public static void NotifyFileAccessCompletion(int globalRequestId)
        {
            // Make a dummy file access to use as a notification that the file accesses should be completed for a project.
            string filePath = FileAccessCompletionPrefix + globalRequestId.ToString();
            _ = File.Exists(filePath);
        }

        public void WaitForFileAccessReportCompletion(int globalRequestId, CancellationToken cancellationToken)
        {
            ManualResetEventSlim handle = _fileAccessCompletionWaitHandles.GetOrAdd(globalRequestId, static _ => new ManualResetEventSlim());
            if (!handle.IsSet)
            {
                handle.Wait(cancellationToken);
            }

            // Try to keep the collection clean. A request should not need to be completed twice.
            _fileAccessCompletionWaitHandles.TryRemove(globalRequestId, out _);
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
#endif
