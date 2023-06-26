// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.Pipes;
using System.Runtime.Versioning;

namespace Microsoft.DotNet.Installer.Windows
{
    [SupportedOSPlatform("windows")]
    internal abstract class InstallElevationContextBase : ElevationContextBase
    {
        /// <summary>
        /// Gets whether this context is associated with the client or server instance of the
        /// installer.
        /// </summary>
        public abstract bool IsClient
        {
            get;
        }

        /// <summary>
        /// <see langword="true"/> if the the current user belongs to the administrators group.
        /// </summary>
        public override bool IsElevated => WindowsUtils.IsAdministrator();

        /// <summary>
        /// Command dispatcher to handle IPC between the elevated and non-elevated processes.
        /// </summary>
        public InstallMessageDispatcher Dispatcher
        {
            get;
            private set;
        }

        protected void InitializeDispatcher(PipeStream pipeStream)
        {
            Dispatcher = new InstallMessageDispatcher(pipeStream);
        }
    }
}
