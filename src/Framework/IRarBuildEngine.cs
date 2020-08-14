// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO.Pipes;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// This interface provides necessary functionality from <see cref="Microsoft.Build"/> to RAR as a service functionality.
    /// </summary>
    internal interface IRarBuildEngine
    {
        /// <summary>
        /// Inialize new RAR node
        /// </summary>
        internal bool CreateRarNode();

        /// <summary>
        /// Provides RAR node name for current configuration
        /// </summary>
        internal string GetRarPipeName();

        /// <summary>
        /// Constructs <seealso cref="NamedPipeClientStream"/>
        /// </summary>
        internal NamedPipeClientStream GetRarClientStream(string pipeName, int timeout);
    }
}
