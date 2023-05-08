// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
