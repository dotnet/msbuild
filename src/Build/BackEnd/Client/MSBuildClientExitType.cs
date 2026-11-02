// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;

namespace Microsoft.Build.Server
{
    /// <summary>
    /// Enumeration of the various ways in which an <see cref="MSBuildClient"/> execution can exit.
    /// </summary>
    /// <remarks>
    /// This type is public only so that the MSBuild command-line application can host the MSBuild server;
    /// third-party use is not expected or supported. The server APIs only work to wrap the MSBuild CLI,
    /// so invoke the CLI instead.
    /// </remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
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
        Unexpected,
        /// <summary>
        /// The client is not able to identify the server state.
        /// </summary>
        /// <remarks>
        /// This may happen when mutex that is regulating the server state throws.
        /// See: https://github.com/dotnet/msbuild/issues/7993.
        /// </remarks>
        UnknownServerState
    }
}
