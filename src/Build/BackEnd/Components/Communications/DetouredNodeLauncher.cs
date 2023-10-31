// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if FEATURE_REPORTFILEACCESSES
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BuildXL.Processes;
using BuildXL.Utilities.Core;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Experimental.FileAccess;
using Microsoft.Build.FileAccesses;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;

#nullable disable

namespace Microsoft.Build.BackEnd
{
    internal sealed class DetouredNodeLauncher : INodeLauncher, IBuildComponent
    {
        private readonly List<ISandboxedProcess> _sandboxedProcesses = new();

        private readonly BuildParameters.IBuildParameters _environmentVariables = CreateEnvironmentVariables();

        private IFileAccessManager _fileAccessManager;

        public static IBuildComponent CreateComponent(BuildComponentType type)
        {
            ErrorUtilities.VerifyThrowArgumentOutOfRange(type == BuildComponentType.NodeLauncher, nameof(type));
            return new DetouredNodeLauncher();
        }

        public void InitializeComponent(IBuildComponentHost host)
        {
            _fileAccessManager = (IFileAccessManager)host.GetComponent(BuildComponentType.FileAccessManager);
        }

        public void ShutdownComponent()
        {
            _fileAccessManager = null;

            foreach (ISandboxedProcess sandboxedProcess in _sandboxedProcesses)
            {
                sandboxedProcess.Dispose();
            }

            _sandboxedProcesses.Clear();
        }

        /// <summary>
        /// Creates a new MSBuild process
        /// </summary>
        public Process Start(string msbuildLocation, string commandLineArgs, int nodeId)
        {
            // Should always have been set already.
            ErrorUtilities.VerifyThrowInternalLength(msbuildLocation, nameof(msbuildLocation));

            ErrorUtilities.VerifyThrowInternalNull(_fileAccessManager, nameof(_fileAccessManager));

            if (!FileSystems.Default.FileExists(msbuildLocation))
            {
                throw new BuildAbortedException(ResourceUtilities.FormatResourceStringStripCodeAndKeyword("CouldNotFindMSBuildExe", msbuildLocation));
            }

            // Repeat the executable name as the first token of the command line because the command line
            // parser logic expects it and will otherwise skip the first argument
            commandLineArgs = $"\"{msbuildLocation}\" {commandLineArgs}";

            CommunicationsUtilities.Trace("Launching node from {0}", msbuildLocation);

            string exeName = msbuildLocation;

#if RUNTIME_TYPE_NETCORE
            // Run the child process with the same host as the currently-running process.
            exeName = CurrentHost.GetCurrentHost();
#endif

            var eventListener = new DetoursEventListener(_fileAccessManager, nodeId);
            eventListener.SetMessageHandlingFlags(MessageHandlingFlags.DebugMessageNotify | MessageHandlingFlags.FileAccessNotify | MessageHandlingFlags.ProcessDataNotify | MessageHandlingFlags.ProcessDetoursStatusNotify);

            var info = new SandboxedProcessInfo(
                fileStorage: null, // Don't write stdout/stderr to files
                fileName: exeName,
                disableConHostSharing: false,
                detoursEventListener: eventListener,
                createJobObjectForCurrentProcess: false)
            {
                SandboxKind = SandboxKind.Default,
                PipDescription = "MSBuild",
                PipSemiStableHash = 0,
                Arguments = commandLineArgs,
                EnvironmentVariables = _environmentVariables,
                MaxLengthInMemory = 0, // Don't buffer any output
            };

            // FileAccessManifest.AddScope is used to define the list of files which the running process is allowed to access and what kinds of file accesses are allowed
            // Tracker internally uses AbsolutePath.Invalid to represent the root, just like Unix '/' root.
            // this code allows all types of accesses for all files
            info.FileAccessManifest.AddScope(
                AbsolutePath.Invalid,
                FileAccessPolicy.MaskNothing,
                FileAccessPolicy.AllowAll | FileAccessPolicy.ReportAccess);

            // Support shared compilation
            info.FileAccessManifest.ChildProcessesToBreakawayFromSandbox = new string[] { NativeMethodsShared.IsWindows ? "VBCSCompiler.exe" : "VBCSCompiler" };
            info.FileAccessManifest.MonitorChildProcesses = true;
            info.FileAccessManifest.IgnoreReparsePoints = true;
            info.FileAccessManifest.UseExtraThreadToDrainNtClose = false;
            info.FileAccessManifest.UseLargeNtClosePreallocatedList = true;
            info.FileAccessManifest.LogProcessData = true;

            // needed for logging process arguments when a new process is invoked; see DetoursEventListener.cs
            info.FileAccessManifest.ReportProcessArgs = true;

            // By default, BuildXL sets the timestamp of all input files to January 1, 1970
            // This breaks some tools like Robocopy which will not copy a file to the destination if the file exists at the destination and has a timestamp that is more recent than the source file
            info.FileAccessManifest.NormalizeReadTimestamps = false;

            // If a process exits but its child processes survive, Tracker waits 30 seconds by default to wait for this process to exit.
            // This slows down C++ builds in which mspdbsrv.exe doesn't exit when it's parent exits. Set this time to 0.
            info.NestedProcessTerminationTimeout = TimeSpan.Zero;

            ISandboxedProcess sp = SandboxedProcessFactory.StartAsync(info, forceSandboxing: false).GetAwaiter().GetResult();
            lock (_sandboxedProcesses)
            {
                _sandboxedProcesses.Add(sp);
            }

            CommunicationsUtilities.Trace("Successfully launched {1} node with PID {0}", sp.ProcessId, exeName);
            return Process.GetProcessById(sp.ProcessId);
        }

        private static BuildParameters.IBuildParameters CreateEnvironmentVariables()
        {
            var envVars = new Dictionary<string, string>();
            foreach (DictionaryEntry baseVar in Environment.GetEnvironmentVariables())
            {
                envVars.Add((string)baseVar.Key, (string)baseVar.Value);
            }

            return BuildParameters.GetFactory().PopulateFromDictionary(envVars);
        }

        private sealed class DetoursEventListener : IDetoursEventListener
        {
            private readonly IFileAccessManager _fileAccessManager;
            private readonly int _nodeId;

            public DetoursEventListener(IFileAccessManager fileAccessManager, int nodeId)
            {
                _fileAccessManager = fileAccessManager;
                _nodeId = nodeId;
            }

            public override void HandleDebugMessage(DebugData debugData)
            {
            }

            public override void HandleFileAccess(FileAccessData fileAccessData) => _fileAccessManager.ReportFileAccess(
                new Experimental.FileAccess.FileAccessData(
                    (Experimental.FileAccess.ReportedFileOperation)fileAccessData.Operation,
                    (Experimental.FileAccess.RequestedAccess)fileAccessData.RequestedAccess,
                    fileAccessData.ProcessId,
                    fileAccessData.Error,
                    (Experimental.FileAccess.DesiredAccess)fileAccessData.DesiredAccess,
                    (Experimental.FileAccess.FlagsAndAttributes)fileAccessData.FlagsAndAttributes,
                    fileAccessData.Path,
                    fileAccessData.ProcessArgs,
                    fileAccessData.IsAnAugmentedFileAccess),
                _nodeId);

            public override void HandleProcessData(ProcessData processData) => _fileAccessManager.ReportProcess(
                new Experimental.FileAccess.ProcessData(
                    processData.ProcessName,
                    processData.ProcessId,
                    processData.ParentProcessId,
                    processData.CreationDateTime,
                    processData.ExitDateTime,
                    processData.ExitCode),
                _nodeId);

            public override void HandleProcessDetouringStatus(ProcessDetouringStatusData data)
            {
            }
        }
    }
}
#endif
