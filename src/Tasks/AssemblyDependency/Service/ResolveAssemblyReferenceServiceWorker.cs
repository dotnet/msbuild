// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;

namespace Microsoft.Build.Tasks.AssemblyDependency
{
    internal class ResolveAssemblyReferenceServiceWorker : ResolveAssemblyReferenceNodeBase, IDisposable
    {
        private const int MaxBuildEventsBeforeFlush = 100;

        private readonly string _workerId;

        private readonly NamedPipeServerStream _pipe;

        private readonly ConcurrentDictionary<string, byte> _seenStateFiles;

        private readonly RarExecutionCache _evaluationCache;

        private readonly Queue<RarBuildEventArgs> _buildEventQueue;

        private readonly byte[] _resuableBuffer = new byte[DefaultBufferSizeInBytes];

        private readonly MemoryStream _memoryStream = new(DefaultBufferSizeInBytes);

        internal ResolveAssemblyReferenceServiceWorker(
            string workerId,
            int maxNumberOfServerInstances,
            RarExecutionCache evaluationCache,
            ConcurrentDictionary<string, byte> seenStateFiles)
        {
            _workerId = workerId;
            _evaluationCache = evaluationCache;
            _seenStateFiles = seenStateFiles;
            _buildEventQueue = new(MaxBuildEventsBeforeFlush);

#if FEATURE_PIPE_SECURITY && FEATURE_NAMED_PIPE_SECURITY_CONSTRUCTOR
            SecurityIdentifier identifier = WindowsIdentity.GetCurrent().Owner;
            PipeSecurity security = new();

            // Restrict access to just this account.  We set the owner specifically here, and on the
            // pipe client side they will check the owner against this one - they must have identical
            // SIDs or the client will reject this server.  This is used to avoid attacks where a
            // hacked server creates a less restricted pipe in an attempt to lure us into using it and
            // then sending build requests to the real pipe client (which is the MSBuild Build Manager.)
            PipeAccessRule rule = new(identifier, PipeAccessRights.ReadWrite, AccessControlType.Allow);
            security.AddAccessRule(rule);
            security.SetOwner(identifier);

            _pipe = new(
                _pipeName,
                PipeDirection.InOut,
                maxNumberOfServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.None
#if FEATURE_PIPEOPTIONS_CURRENTUSERONLY
                | PipeOptions.CurrentUserOnly
#endif
                ,
                0, // Default input buffer
                0);  // Default output buffer
                     // TODO: Settings pipe security breaks client connection for unknown reason.
                     // TODO: Unknown why this breaks, as this is mostly taken from MSBuild Server implementation.
                     // TODO: Might be an issue on the client implementation and not the server?
                     // security,
                     // HandleInheritability.None);
#else
            _pipe = new(
                _pipeName,
                PipeDirection.InOut,
                maxNumberOfServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.None
#if FEATURE_PIPEOPTIONS_CURRENTUSERONLY
                | PipeOptions.CurrentUserOnly
#endif
                ,
                0,
                0);
#endif
        }

        internal async Task RunServerAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await WaitForConnectionAsync(cancellationToken);

                    Stopwatch e2eTime = new();
                    e2eTime.Start();
                    Console.WriteLine($"({_workerId}) Connected to client.");

                    try
                    {

                        RarExecutionRequest request = ReadRequest();
                        RarExecutionResponse response = await ResolveAssemblyReferencesAsync(request, cancellationToken);
                        SendResponse(response);

                        // Avoid replaying build events on future runs.
                        response.BuildEventArgsQueue = [];

                        if (NativeMethodsShared.IsWindows)
                        {
                            _pipe.WaitForPipeDrain();
                        }

                        e2eTime.Stop();
                        Console.WriteLine($"({_workerId}) Request completed for '{request.TargetPath}' in {e2eTime.ElapsedMilliseconds} ms.");
                    }
                    catch (Exception e) when (e is not OperationCanceledException)
                    {
                        Console.Error.WriteLine(e);
                    }

                    _pipe.Disconnect();
                }
            }
            catch (OperationCanceledException)
            {
                // Swallow cancellation excpetions for now. We're using this as a simple way to gracefully shutdown the
                // server, instead of having to implement separate Start / Stop methods and deferring to the caller.
                // Can reevaluate if we need more granular control over cancellation vs shutdown.
            }
        }

        private async Task WaitForConnectionAsync(CancellationToken cancellationToken)
        {
            await _pipe.WaitForConnectionAsync(cancellationToken);
            bool gotValidConnection = false;

            while (!gotValidConnection)
            {
                try
                {
                    gotValidConnection = PerformHandshake();
                }
                catch (IOException e)
                {
                    // We will get here when:
                    // 1. The host (OOP main node) connects to us, it immediately checks for user privileges
                    //    and if they don't match it disconnects immediately leaving us still trying to read the blank handshake
                    // 2. The host is too old sending us bits we automatically reject in the handshake
                    // 3. We expected to read the EndOfHandshake signal, but we received something else
                    CommunicationsUtilities.Trace("Client connection failed but we will wait for another connection. Exception: {0}", e.Message);
                }
                catch (InvalidOperationException)
                {
                }

                if (!gotValidConnection && _pipe.IsConnected)
                {
                    if (NativeMethodsShared.IsWindows)
                    {
                        _pipe.WaitForPipeDrain();
                    }

                    _pipe.Disconnect();
                }
            }
        }

        private bool PerformHandshake()
        {
            int[] handshakeComponents = _handshake.RetrieveHandshakeComponents();
            for (int i = 0; i < handshakeComponents.Length; i++)
            {
#pragma warning disable SA1111, SA1009 // Closing parenthesis should be on line of last parameter
                int handshakePart = _pipe.ReadIntForHandshake(
                    byteToAccept: i == 0 ? CommunicationsUtilities.handshakeVersion : null /* this will disconnect a < 16.8 host; it expects leading 00 or F5 or 06. 0x00 is a wildcard */
#if NETCOREAPP2_1_OR_GREATER
                , ClientConnectTimeout /* wait a long time for the handshake from this side */
#endif
                );
#pragma warning restore SA1111, SA1009 // Closing parenthesis should be on line of last parameter

                if (handshakePart != handshakeComponents[i])
                {
                    CommunicationsUtilities.Trace("Handshake failed. Received {0} from host not {1}. Probably the host is a different MSBuild build.", handshakePart, handshakeComponents[i]);
                    _pipe.WriteIntForHandshake(i + 1);
                    return false;
                }
            }

            // To ensure that our handshake and theirs have the same number of bytes, receive and send a magic number indicating EOS.
#if NETCOREAPP2_1_OR_GREATER
            _pipe.ReadEndOfHandshakeSignal(false, ClientConnectTimeout); /* wait a long time for the handshake from this side */
#else
            _pipe.ReadEndOfHandshakeSignal(false);
#endif
            _pipe.WriteEndOfHandshakeSignal();

#if FEATURE_SECURITY_PERMISSIONS
            // We will only talk to a host that was started by the same user as us.  Even though the pipe access is set to only allow this user, we want to ensure they
            // haven't attempted to change those permissions out from under us.  This ensures that the only way they can truly gain access is to be impersonating the
            // user we were started by.
            WindowsIdentity currentIdentity = WindowsIdentity.GetCurrent();
            WindowsIdentity? clientIdentity = null;
            _pipe.RunAsClient(() =>
            {
                clientIdentity = WindowsIdentity.GetCurrent(true);
            });

            if (clientIdentity == null || !string.Equals(clientIdentity.Name, currentIdentity.Name, StringComparison.OrdinalIgnoreCase))
            {
                CommunicationsUtilities.Trace("Handshake failed. Host user is {0} but we were created by {1}.", (clientIdentity == null) ? "<unknown>" : clientIdentity.Name, currentIdentity.Name);
                return false;
            }
#endif
            return true;
        }

        private RarExecutionRequest ReadRequest()
        {
            // Read raw bytes to a temporary buffer to reduce IO calls.
            int bytesRead = ReadPipe(_pipe, _resuableBuffer, 0, MessageOffsetInBytes);
            int messageLength = ParseMessageLength(_resuableBuffer);

            // Additional reads for the remaining message.
            byte[] buffer = EnsureBufferSize(_resuableBuffer, messageLength);
            ReadPipe(_pipe, buffer, bytesRead, messageLength);

            Console.WriteLine($"({_workerId}) Received request of size: '{messageLength}'");

            return Deserialize<RarExecutionRequest>(buffer, messageLength, setHash: true);
        }

        private void SendResponse(RarExecutionResponse response)
        {
            // Serialize to temporary buffer to reduce IO calls.
            Serialize(response, _memoryStream, setHash: true);
            SetMessageLength(_memoryStream);
            WritePipe(_pipe, _memoryStream);

            Console.WriteLine($"({_workerId}) Sent response of size: '{_memoryStream.Length}'");
        }

        private async Task<RarExecutionResponse> ResolveAssemblyReferencesAsync(RarExecutionRequest request, CancellationToken cancellationToken)
        {
            bool isCacheable = request.StateFile != null;

            if (isCacheable)
            {
                RarExecutionResponse? cachedResult = await _evaluationCache.GetCachedEvaluation(request);

                if (cachedResult != null)
                {
                    Console.WriteLine($"({_workerId}) Cache hit for '{request.TargetPath}'. Skipping RAR.')");
                    return cachedResult;
                }
            }

            Console.WriteLine($"({_workerId}) Executing RAR for '{request.TargetPath}'.");
            Stopwatch execTime = new();
            execTime.Start();

            EventQueueBuildEngine buildEngine = new(
                (MessageImportance)request.MinimumMessageImportance,
                request.IsTaskLoggingEnabled);
            Task buildEventTask = Task.Run(
                () => ProcessBuildEvents(buildEngine, cancellationToken),
                cancellationToken);
            RarExecutionResponse result = HandleRequest(request, buildEngine);

            execTime.Stop();
            Console.WriteLine($"({_workerId}) RAR completed for '{request.TargetPath}' in {execTime.ElapsedMilliseconds} ms.'");
            buildEngine.Complete();

            await buildEventTask;

            result.BuildEventArgsQueue = [.. _buildEventQueue];
            _buildEventQueue.Clear();

            if (isCacheable)
            {
                _evaluationCache.CacheEvaluation(request, result);
            }

            return result;
        }

        private async Task ProcessBuildEvents(EventQueueBuildEngine buildEngine, CancellationToken cancellationToken)
        {
            try
            {
                while (cancellationToken.IsCancellationRequested)
                {
                    RarBuildEventArgs buildEventArgs = await buildEngine.EventQueue.ReadAsync(cancellationToken);
                    _buildEventQueue.Enqueue(buildEventArgs);

                    if (_buildEventQueue.Count == MaxBuildEventsBeforeFlush)
                    {
                        Console.WriteLine($"({_workerId}) Flushing build events.");
                        RarExecutionResponse response = new()
                        {
                            BuildEventArgsQueue = [.. _buildEventQueue],
                        };
                        SendResponse(response);
                        _buildEventQueue.Clear();
                    }
                }
            }
            catch (ChannelClosedException)
            {
            }
        }

        private RarExecutionResponse HandleRequest(RarExecutionRequest request, EventQueueBuildEngine buildEngine)
        {
            // Only load the state file on the first run.
            bool shouldLoadStateFile = !string.IsNullOrEmpty(request.StateFile) && _seenStateFiles.TryAdd(request.StateFile!, 0);

            ResolveAssemblyReference rarTask = new()
            {
                ShouldExecuteOutOfProcess = false,
                AllowedAssemblyExtensions = [.. request.AllowedAssemblyExtensions],
                AllowedRelatedFileExtensions = [.. request.AllowedRelatedFileExtensions],
                AppConfigFile = request.AppConfigFile,
                Assemblies = [.. request.Assemblies],
                AssemblyFiles = [.. request.AssemblyFiles],
                AutoUnify = request.AutoUnify,
                BuildEngine = buildEngine,
                CandidateAssemblyFiles = [.. request.CandidateAssemblyFiles],
                CopyLocalDependenciesWhenParentReferenceInGac = request.CopyLocalDependenciesWhenParentReferenceInGac,
                DoNotCopyLocalIfInGac = request.DoNotCopyLocalIfInGac,
                FindDependencies = request.FindDependencies,
                FindDependenciesOfExternallyResolvedReferences = request.FindDependenciesOfExternallyResolvedReferences,
                FindRelatedFiles = request.FindRelatedFiles,
                FindSatellites = request.FindSatellites,
                FindSerializationAssemblies = request.FindSerializationAssemblies,
                FullFrameworkAssemblyTables = [.. request.FullFrameworkAssemblyTables],
                FullFrameworkFolders = [.. request.FullFrameworkFolders],
                FullTargetFrameworkSubsetNames = [.. request.FullTargetFrameworkSubsetNames],
                IgnoreDefaultInstalledAssemblySubsetTables = request.IgnoreDefaultInstalledAssemblySubsetTables,
                IgnoreDefaultInstalledAssemblyTables = request.IgnoreDefaultInstalledAssemblyTables,
                IgnoreTargetFrameworkAttributeVersionMismatch = request.IgnoreTargetFrameworkAttributeVersionMismatch,
                IgnoreVersionForFrameworkReferences = request.IgnoreVersionForFrameworkReferences,
                InstalledAssemblyTables = [.. request.InstalledAssemblyTables],
                InstalledAssemblySubsetTables = [.. request.InstalledAssemblySubsetTables],
                LatestTargetFrameworkDirectories = [.. request.LatestTargetFrameworkDirectories],
                ProfileName = request.ProfileName,
                ResolvedSDKReferences = [.. request.ResolvedSDKReferences],
                SearchPaths = [.. request.SearchPaths],
                Silent = request.Silent,
                StateFile = shouldLoadStateFile ? request.StateFile : null,
                SupportsBindingRedirectGeneration = request.SupportsBindingRedirectGeneration,
                TargetFrameworkDirectories = [.. request.TargetFrameworkDirectories],
                TargetFrameworkMoniker = request.TargetFrameworkMoniker,
                TargetFrameworkMonikerDisplayName = request.TargetFrameworkMonikerDisplayName,
                TargetFrameworkSubsets = [.. request.TargetFrameworkSubsets],
                TargetFrameworkVersion = request.TargetFrameworkVersion,
                TargetProcessorArchitecture = request.TargetProcessorArchitecture,
                TargetedRuntimeVersion = request.TargetedRuntimeVersion,
                UnresolveFrameworkAssembliesFromHigherFrameworks = request.UnresolveFrameworkAssembliesFromHigherFrameworks,
                WarnOrErrorOnTargetArchitectureMismatch = request.WarnOrErrorOnTargetArchitectureMismatch,
            };

            bool success = rarTask.ExecuteInProcess();

            RarExecutionResponse resp = CreateResponse(rarTask, buildEngine, success);

            return resp;
        }

        private static RarExecutionResponse CreateResponse(
            ResolveAssemblyReference rarTask,
            EventQueueBuildEngine buildEngine,
            bool success)
        {
            HashSet<ITaskItem> copyLocalFiles = new(rarTask.CopyLocalFiles);

            RarExecutionResponse resp = new()
            {
                IsComplete = true,
                Success = success,
                NumCopyLocalFiles = rarTask.CopyLocalFiles.Length,
                DependsOnNetStandard = rarTask.DependsOnNETStandard,
                DependsOnSystemRuntime = rarTask.DependsOnSystemRuntime,
                FilesWritten = ConvertTaskItems(rarTask.FilesWritten),
                RelatedFiles = ConvertTaskItems(rarTask.RelatedFiles),
                ResolvedDependencyFiles = ConvertTaskItems(rarTask.ResolvedDependencyFiles),
                ResolvedFiles = ConvertTaskItems(rarTask.ResolvedFiles),
                SatelliteFiles = ConvertTaskItems(rarTask.SatelliteFiles),
                ScatterFiles = ConvertTaskItems(rarTask.ScatterFiles),
                SerializationAssemblyFiles = ConvertTaskItems(rarTask.SerializationAssemblyFiles),
                SuggestedRedirects = ConvertTaskItems(rarTask.SuggestedRedirects),
                UnresolvedAssemblyConflicts = ConvertTaskItems(rarTask.UnresolvedAssemblyConflicts),
                Cache = rarTask.Cache,
            };

            return resp;

            RarTaskItemOutput[] ConvertTaskItems(ITaskItem[] taskItems)
            {
                RarTaskItemOutput[] responseItems = new RarTaskItemOutput[taskItems.Length];

                for (int i = 0; i < taskItems.Length; i++)
                {
                    ITaskItem taskItem = taskItems[i];
                    responseItems[i] = new RarTaskItemOutput(taskItem, copyLocalFiles.Contains(taskItem));
                }

                return responseItems;
            }
        }

        public void Dispose()
        {
            _pipe.Dispose();
        }
    }
}
