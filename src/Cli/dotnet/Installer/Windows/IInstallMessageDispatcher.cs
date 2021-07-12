// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.DotNet.Installer.Windows
{
    /// <summary>
    /// Provides synchronous inter-process communication primitives for sending and receiving messages.
    /// </summary>
    internal interface IInstallMessageDispatcher
    {
        /// <summary>
        /// Sends a request message. The sender blocks until a response is received.
        /// </summary>
        /// <param name="message">The request message to send.</param>
        /// <returns>A response message, indicating the result of the request.</returns>
        InstallResponseMessage Send(InstallRequestMessage message);

        /// <summary>
        /// Sends a response message back to the sender.
        /// </summary>
        /// <param name="message">The response message to send in the reply.</param>
        void Reply(InstallResponseMessage message);
    }
}
