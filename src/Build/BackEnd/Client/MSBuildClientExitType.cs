// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Build.Execution
{
    public enum MSBuildClientExitType
    {
        /// <summary>
        /// The MSBuild client successfully processed the build request.
        /// </summary>
        Success,
        /// <summary>
        /// Server is busy.
        /// </summary>
        ServerBusy,
        /// <summary>
        /// Client was unable to connect to the server.
        /// </summary>
        ConnectionError,
        /// <summary>
        /// Client was unable to launch the server.
        /// </summary>
        LaunchError,
        /// <summary>
        /// The build stopped unexpectedly, for example,
        /// because a named pipe between the server and the client was unexpectedly closed.
        /// </summary>
        Unexpected
    }
}
