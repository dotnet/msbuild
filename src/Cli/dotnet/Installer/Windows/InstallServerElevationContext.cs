// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.Pipes;
using System.Runtime.Versioning;

namespace Microsoft.DotNet.Installer.Windows
{
    /// <summary>
    /// Elevation context for the server instance.
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal class InstallServerElevationContext : InstallElevationContextBase
    {
        public override bool IsClient => false;

        /// <summary>
        /// Creates a new <see cref="InstallServerElevationContext"/> instance.
        /// </summary>
        /// <param name="pipeStream">The pipe stream used for IPC.</param>
        public InstallServerElevationContext(PipeStream pipeStream)
        {
            InitializeDispatcher(pipeStream);
        }

        public override void Elevate()
        {
        }
    }
}
