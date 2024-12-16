// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net.Http;
using System.Security.Principal;
using System.Text;
using System.Xml.Serialization;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks.AssemblyDependency
{
    internal class ResolveAssemblyReferenceClient : ResolveAssemblyReferenceNodeBase, IDisposable
    {
        private static readonly byte[] ReusableBuffer = new byte[DefaultBufferSizeInBytes];

        private readonly MemoryStream _memoryStream = new(DefaultBufferSizeInBytes);

        private readonly NamedPipeClientStream _pipe;

        internal ResolveAssemblyReferenceClient()
        {
#pragma warning disable SA1111, SA1009 // Closing parenthesis should be on line of last parameter
            _pipe = new(
                serverName: ".",
                _pipeName,
                PipeDirection.InOut,
                PipeOptions.None
#if FEATURE_PIPEOPTIONS_CURRENTUSERONLY
                | PipeOptions.CurrentUserOnly
#endif
            );
#pragma warning restore SA1111, SA1009 // Closing parenthesis should be on line of last parameter
        }

        public bool Execute(ResolveAssemblyReference rarTask)
        {
            // RAR service may have a different working directory, so convert potential relative paths to absolute.
            string? appConfigFile = rarTask.AppConfigFile != null ? Path.GetFullPath(rarTask.AppConfigFile) : null;
            string? stateFile = rarTask.StateFile != null ? Path.GetFullPath(rarTask.StateFile) : null;

            // Allow the service to avoid processing messages which would never be logged by the client.
            MessageImportance minimumMessageImportance = GetMinimumMessageImportance(rarTask.Log);

            RarExecutionRequest req = new()
            {
                AutoUnify = rarTask.AutoUnify,
                CopyLocalDependenciesWhenParentReferenceInGac = rarTask.CopyLocalDependenciesWhenParentReferenceInGac,
                DoNotCopyLocalIfInGac = rarTask.DoNotCopyLocalIfInGac,
                FindDependencies = rarTask.FindDependencies,
                FindDependenciesOfExternallyResolvedReferences = rarTask.FindDependenciesOfExternallyResolvedReferences,
                FindRelatedFiles = rarTask.FindRelatedFiles,
                FindSatellites = rarTask.FindSatellites,
                FindSerializationAssemblies = rarTask.FindSerializationAssemblies,
                IgnoreDefaultInstalledAssemblySubsetTables = rarTask.IgnoreDefaultInstalledAssemblySubsetTables,
                IgnoreDefaultInstalledAssemblyTables = rarTask.IgnoreDefaultInstalledAssemblyTables,
                IgnoreTargetFrameworkAttributeVersionMismatch = rarTask.IgnoreTargetFrameworkAttributeVersionMismatch,
                IgnoreVersionForFrameworkReferences = rarTask.IgnoreVersionForFrameworkReferences,
                Silent = rarTask.Silent,
                SupportsBindingRedirectGeneration = rarTask.SupportsBindingRedirectGeneration,
                UnresolveFrameworkAssembliesFromHigherFrameworks = rarTask.UnresolveFrameworkAssembliesFromHigherFrameworks,
                IsTaskLoggingEnabled = rarTask.Log.IsTaskInputLoggingEnabled,
                MinimumMessageImportance = minimumMessageImportance,
                TargetPath = rarTask.TargetPath,
                AppConfigFile = appConfigFile,
                ProfileName = rarTask.ProfileName,
                StateFile = stateFile,
                TargetedRuntimeVersion = rarTask.TargetedRuntimeVersion,
                TargetFrameworkMoniker = rarTask.TargetFrameworkMoniker,
                TargetFrameworkMonikerDisplayName = rarTask.TargetFrameworkMonikerDisplayName,
                TargetFrameworkVersion = rarTask.TargetFrameworkVersion,
                TargetProcessorArchitecture = rarTask.TargetProcessorArchitecture,
                WarnOrErrorOnTargetArchitectureMismatch = rarTask.WarnOrErrorOnTargetArchitectureMismatch,
                AllowedAssemblyExtensions = rarTask.AllowedAssemblyExtensions,
                AllowedRelatedFileExtensions = rarTask.AllowedRelatedFileExtensions,
                Assemblies = ConvertTaskItems(rarTask.Assemblies),
                AssemblyFiles = ConvertTaskItems(rarTask.AssemblyFiles),
                CandidateAssemblyFiles = rarTask.CandidateAssemblyFiles,
                FullFrameworkAssemblyTables = ConvertTaskItems(rarTask.FullFrameworkAssemblyTables),
                FullFrameworkFolders = rarTask.FullFrameworkFolders,
                FullTargetFrameworkSubsetNames = rarTask.FullTargetFrameworkSubsetNames,
                InstalledAssemblyTables = ConvertTaskItems(rarTask.InstalledAssemblyTables),
                InstalledAssemblySubsetTables = ConvertTaskItems(rarTask.InstalledAssemblySubsetTables),
                LatestTargetFrameworkDirectories = rarTask.LatestTargetFrameworkDirectories,
                ResolvedSDKReferences = ConvertTaskItems(rarTask.ResolvedSDKReferences),
                SearchPaths = rarTask.SearchPaths,
                TargetFrameworkDirectories = rarTask.TargetFrameworkDirectories,
                TargetFrameworkSubsets = rarTask.TargetFrameworkSubsets,
            };

            RarExecutionResponse resp = ResolveAssemblyReferences(req, rarTask.BuildEngine);
            SetTaskOutputs(rarTask, resp);

            return resp.Success;

            static RarTaskItemInput[] ConvertTaskItems(ITaskItem[] taskItems)
            {
                RarTaskItemInput[] requestItems = new RarTaskItemInput[taskItems.Length];

                for (int i = 0; i < taskItems.Length; i++)
                {
                    requestItems[i] = new RarTaskItemInput(taskItems[i]);
                }

                return requestItems;
            }
        }

        private MessageImportance GetMinimumMessageImportance(TaskLoggingHelper log) =>
            log.LogsMessagesOfImportance(MessageImportance.Low) ? MessageImportance.Low
                : log.LogsMessagesOfImportance(MessageImportance.Normal) ? MessageImportance.Normal
                : MessageImportance.High;

        private RarExecutionResponse ResolveAssemblyReferences(RarExecutionRequest request, IBuildEngine buildEngine)
        {
            ConnectToServer();

            SendRequest(request);
            RarExecutionResponse response = ReadResponse();

            // The RAR service will reply with queued build events before the task has completed.
            // Process these while waiting for completion.
            while (!response.IsComplete)
            {
                LogBuildEvents(buildEngine, response.BuildEventArgsQueue);
                response = ReadResponse();
            }

            LogBuildEvents(buildEngine, response.BuildEventArgsQueue);

            return response;
        }

        private void ConnectToServer()
        {
            CommunicationsUtilities.Trace("Attempting connect to pipe {1} with timeout {2} ms", _pipeName, _pipeName, ClientConnectTimeout);

            _pipe.Connect(ClientConnectTimeout);

#if !FEATURE_PIPEOPTIONS_CURRENTUSERONLY
            if (NativeMethodsShared.IsWindows)
            {
                // Verify that the owner of the pipe is us.  This prevents a security hole where a remote node has
                // been faked up with ACLs that would let us attach to it.  It could then issue fake build requests back to
                // us, potentially causing us to execute builds that do harmful or unexpected things.  The pipe owner can
                // only be set to the user's own SID by a normal, unprivileged process.  The conditions where a faked up
                // remote node could set the owner to something else would also let it change owners on other objects, so
                // this would be a security flaw upstream of us.
                ValidateRemotePipeSecurityOnWindows();
            }
#endif

            PerformHandshake();
        }

        private void PerformHandshake()
        {
            int[] handshakeComponents = _handshake.RetrieveHandshakeComponents();

            for (int i = 0; i < handshakeComponents.Length; i++)
            {
                CommunicationsUtilities.Trace("Writing handshake part {0} ({1}) to pipe {2}", i, handshakeComponents[i], _pipeName);
                _pipe.WriteIntForHandshake(handshakeComponents[i]);
            }

            _pipe.WriteEndOfHandshakeSignal();

            CommunicationsUtilities.Trace("Reading handshake from pipe {0}", _pipeName);

#if NETCOREAPP2_1_OR_GREATER
            _pipe.ReadEndOfHandshakeSignal(true, ClientConnectTimeout);
#else
            _pipe.ReadEndOfHandshakeSignal(true);
#endif
            CommunicationsUtilities.Trace("Successfully connected to pipe {0}...!", _pipeName);
        }

#if !FEATURE_PIPEOPTIONS_CURRENTUSERONLY
        // This code needs to be in a separate method so that we don't try (and fail) to load the Windows-only APIs when JIT-ing the code
        //  on non-Windows operating systems
        private void ValidateRemotePipeSecurityOnWindows()
        {
            SecurityIdentifier identifier = WindowsIdentity.GetCurrent().Owner;
#if FEATURE_PIPE_SECURITY
            PipeSecurity remoteSecurity = _pipe.GetAccessControl();
#else
            var remoteSecurity = new PipeSecurity(_pipe.SafePipeHandle, System.Security.AccessControl.AccessControlSections.Access |
                System.Security.AccessControl.AccessControlSections.Owner | System.Security.AccessControl.AccessControlSections.Group);
#endif
            IdentityReference remoteOwner = remoteSecurity.GetOwner(typeof(SecurityIdentifier));
            if (remoteOwner != identifier)
            {
                CommunicationsUtilities.Trace("The remote pipe owner {0} does not match {1}", remoteOwner.Value, identifier.Value);
                throw new UnauthorizedAccessException();
            }
        }
#endif

        private void SendRequest(RarExecutionRequest request)
        {
            // Serialize to temporary buffer to reduce IO calls.
            Serialize(request, _memoryStream);
            SetMessageLength(_memoryStream);
            WritePipe(_pipe, _memoryStream);
        }

        private RarExecutionResponse ReadResponse()
        {
            // Read raw bytes to a temporary buffer to reduce IO calls.
            int bytesRead = ReadPipe(_pipe, ReusableBuffer, 0, MessageOffsetInBytes);
            int messageLength = ParseMessageLength(ReusableBuffer);

            // Additional reads for the remaining message.
            byte[] buffer = EnsureBufferSize(ReusableBuffer, messageLength);
            bytesRead = ReadPipe(_pipe, buffer, bytesRead, messageLength);

            if (bytesRead > messageLength)
            {
                // TODO: Check event args are handled correctly. Server may send multiple responses for one request.
                throw new Exception("Should not be reading into next message!");
            }

            return Deserialize<RarExecutionResponse>(buffer, messageLength);
        }

        private static void SetTaskOutputs(ResolveAssemblyReference rarTask, RarExecutionResponse response)
        {
            rarTask.DependsOnNETStandard = response.DependsOnNetStandard;
            rarTask.DependsOnSystemRuntime = response.DependsOnSystemRuntime;
            List<ITaskItem> copyLocalFiles = new(response.NumCopyLocalFiles);
            rarTask.FilesWritten = ExtractTaskItems(response.FilesWritten);
            rarTask.RelatedFiles = ExtractTaskItems(response.RelatedFiles);
            rarTask.ResolvedDependencyFiles = ExtractTaskItems(response.ResolvedDependencyFiles);
            rarTask.ResolvedFiles = ExtractTaskItems(response.ResolvedFiles);
            rarTask.SatelliteFiles = ExtractTaskItems(response.SatelliteFiles);
            rarTask.ScatterFiles = ExtractTaskItems(response.ScatterFiles);
            rarTask.SerializationAssemblyFiles = ExtractTaskItems(response.SerializationAssemblyFiles);
            rarTask.SuggestedRedirects = ExtractTaskItems(response.SuggestedRedirects);
            rarTask.UnresolvedAssemblyConflicts = ExtractTaskItems(response.UnresolvedAssemblyConflicts);

            ITaskItem[] ExtractTaskItems(RarTaskItemOutput[] responseItems)
            {
                ITaskItem[] taskItems = new ITaskItem[responseItems.Length];

                for (int i = 0; i < responseItems.Length; i++)
                {
                    RarTaskItemOutput responseItem = responseItems[i];

                    TaskItem taskItem = new(responseItem.EvaluatedIncludeEscaped);
                    taskItems[i] = taskItem;

                    if (responseItem.IsCopyLocalFile)
                    {
                        copyLocalFiles.Add(taskItem);
                    }
                }

                return taskItems;
            }
        }

        private static void LogBuildEvents(IBuildEngine buildEngine, RarBuildEventArgs[] buildEventsArgsQueue)
        {
            foreach (RarBuildEventArgs buildEventArgs in buildEventsArgsQueue)
            {
                DateTime eventTimestamp = new(buildEventArgs.EventTimestamp, DateTimeKind.Utc);

                switch (buildEventArgs.EventType)
                {
                    case RarBuildEventArgsType.Message:
                        BuildMessageEventArgs messageEventArgs = new(
                            buildEventArgs.Subcategory,
                            buildEventArgs.Code,
                            buildEventArgs.File,
                            buildEventArgs.LineNumber,
                            buildEventArgs.ColumnNumber,
                            buildEventArgs.EndLineNumber,
                            buildEventArgs.EndColumnNumber,
                            buildEventArgs.Message,
                            buildEventArgs.HelpKeyword,
                            buildEventArgs.SenderName,
                            (MessageImportance)buildEventArgs.Importance,
                            eventTimestamp,
                            buildEventArgs.MessageArgs);

                        buildEngine.LogMessageEvent(messageEventArgs);
                        break;
                    case RarBuildEventArgsType.Warning:
                        BuildWarningEventArgs warningEventArgs = new(
                            buildEventArgs.Subcategory,
                            buildEventArgs.Code,
                            buildEventArgs.File,
                            buildEventArgs.LineNumber,
                            buildEventArgs.ColumnNumber,
                            buildEventArgs.EndLineNumber,
                            buildEventArgs.EndColumnNumber,
                            buildEventArgs.Message,
                            buildEventArgs.HelpKeyword,
                            buildEventArgs.SenderName,
                            eventTimestamp,
                            buildEventArgs.MessageArgs);

                        buildEngine.LogWarningEvent(warningEventArgs);
                        break;
                    case RarBuildEventArgsType.Error:
                        BuildErrorEventArgs errorEventArgs = new(
                            buildEventArgs.Subcategory,
                            buildEventArgs.Code,
                            buildEventArgs.File,
                            buildEventArgs.LineNumber,
                            buildEventArgs.ColumnNumber,
                            buildEventArgs.EndLineNumber,
                            buildEventArgs.EndColumnNumber,
                            buildEventArgs.Message,
                            buildEventArgs.HelpKeyword,
                            buildEventArgs.SenderName,
                            eventTimestamp,
                            buildEventArgs.MessageArgs);

                        buildEngine.LogErrorEvent(errorEventArgs);
                        break;
                }
            }
        }

        public void Dispose()
        {
            _pipe.Dispose();
        }
    }
}
