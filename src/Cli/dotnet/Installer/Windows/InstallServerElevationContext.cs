// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
