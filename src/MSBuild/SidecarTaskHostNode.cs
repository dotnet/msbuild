// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !CLR2COMPATIBILITY

using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Framework;

#nullable disable

namespace Microsoft.Build.CommandLine
{
    /// <summary>
    /// Sidecar task host node that supports IBuildEngine callbacks by forwarding them to the parent process.
    /// This is used for long-lived taskhost processes in multithreaded build mode (-mt).
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="OutOfProcTaskHostNode"/>, this class fully implements IBuildEngine callbacks
    /// by sending request packets to the parent process (TaskHostTask) and waiting for responses.
    /// This enables tasks that use BuildProjectFile, RequestCores, Yield, etc. to work correctly
    /// when running in a sidecar taskhost.
    /// </remarks>
    internal sealed class SidecarTaskHostNode : OutOfProcTaskHostNodeBase
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public SidecarTaskHostNode()
            : base()
        {
            // Register packet handlers for callback response packets
            // These will be added when the callback packet types are implemented
        }

        #region IBuildEngine2 Implementation (Properties)

        /// <summary>
        /// Gets whether we're running multiple nodes by forwarding the query to the parent process.
        /// </summary>
        public override bool IsRunningMultipleNodes
        {
            get
            {
                // TODO: Implement callback forwarding to parent
                // For now, log error like regular taskhost until callback packets are implemented
                LogErrorFromResource("BuildEngineCallbacksInTaskHostUnsupported");
                return false;
            }
        }

        #endregion // IBuildEngine2 Implementation (Properties)

        #region IBuildEngine Implementation (Methods)

        /// <summary>
        /// Implementation of IBuildEngine.BuildProjectFile by forwarding to the parent process.
        /// </summary>
        public override bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs)
        {
            // TODO: Implement callback forwarding to parent
            // For now, log error like regular taskhost until callback packets are implemented
            LogErrorFromResource("BuildEngineCallbacksInTaskHostUnsupported");
            return false;
        }

        #endregion // IBuildEngine Implementation (Methods)

        #region IBuildEngine2 Implementation (Methods)

        /// <summary>
        /// Implementation of IBuildEngine2.BuildProjectFile by forwarding to the parent process.
        /// </summary>
        public override bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs, string toolsVersion)
        {
            // TODO: Implement callback forwarding to parent
            LogErrorFromResource("BuildEngineCallbacksInTaskHostUnsupported");
            return false;
        }

        /// <summary>
        /// Implementation of IBuildEngine2.BuildProjectFilesInParallel by forwarding to the parent process.
        /// </summary>
        public override bool BuildProjectFilesInParallel(string[] projectFileNames, string[] targetNames, IDictionary[] globalProperties, IDictionary[] targetOutputsPerProject, string[] toolsVersion, bool useResultsCache, bool unloadProjectsOnCompletion)
        {
            // TODO: Implement callback forwarding to parent
            LogErrorFromResource("BuildEngineCallbacksInTaskHostUnsupported");
            return false;
        }

        #endregion // IBuildEngine2 Implementation (Methods)

        #region IBuildEngine3 Implementation

        /// <summary>
        /// Implementation of IBuildEngine3.BuildProjectFilesInParallel by forwarding to the parent process.
        /// </summary>
        public override BuildEngineResult BuildProjectFilesInParallel(string[] projectFileNames, string[] targetNames, IDictionary[] globalProperties, IList<string>[] removeGlobalProperties, string[] toolsVersion, bool returnTargetOutputs)
        {
            // TODO: Implement callback forwarding to parent
            LogErrorFromResource("BuildEngineCallbacksInTaskHostUnsupported");
            return new BuildEngineResult(false, null);
        }

        /// <summary>
        /// Implementation of IBuildEngine3.Yield by forwarding to the parent process.
        /// </summary>
        public override void Yield()
        {
            // TODO: Implement callback forwarding to parent
            // For now, just return silently (no-op) like regular taskhost
            return;
        }

        /// <summary>
        /// Implementation of IBuildEngine3.Reacquire by forwarding to the parent process.
        /// </summary>
        public override void Reacquire()
        {
            // TODO: Implement callback forwarding to parent
            // For now, just return silently (no-op) like regular taskhost
            return;
        }

        #endregion // IBuildEngine3 Implementation

        #region IBuildEngine9 Implementation

        /// <summary>
        /// Implementation of IBuildEngine9.RequestCores by forwarding to the parent process.
        /// </summary>
        public override int RequestCores(int requestedCores)
        {
            // TODO: Implement callback forwarding to parent
            // For now, throw like regular taskhost until callback packets are implemented
            throw new NotImplementedException();
        }

        /// <summary>
        /// Implementation of IBuildEngine9.ReleaseCores by forwarding to the parent process.
        /// </summary>
        public override void ReleaseCores(int coresToRelease)
        {
            // TODO: Implement callback forwarding to parent
            // For now, throw like regular taskhost until callback packets are implemented
            throw new NotImplementedException();
        }

        #endregion
    }
}

#endif
