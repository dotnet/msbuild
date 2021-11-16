// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
