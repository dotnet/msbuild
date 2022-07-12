// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace Microsoft.Build.Experimental
{
    public enum MSBuildClientExitType
    {
        /// <summary>
        /// The MSBuild client successfully processed the build request.
        /// </summary>
        Success,
        /// <summary>
        /// Server is busy. This would invoke a fallback behavior.
        /// </summary>
        ServerBusy,
        /// <summary>
        /// Client was unable to connect to the server. This would invoke a fallback behavior.
        /// </summary>
        UnableToConnect,
        /// <summary>
        /// Client was unable to launch the server. This would invoke a fallback behavior.
        /// </summary>
        LaunchError,
        /// <summary>
        /// The build stopped unexpectedly, for example,
        /// because a named pipe between the server and the client was unexpectedly closed.
        /// </summary>
        Unexpected
    }
}
