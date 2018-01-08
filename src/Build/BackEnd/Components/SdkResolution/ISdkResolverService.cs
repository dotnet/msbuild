// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework;
using System;

namespace Microsoft.Build.BackEnd.SdkResolution
{
    /// <summary>
    /// An interface for services which resolve SDKs.
    /// </summary>
    internal interface ISdkResolverService
    {
        /// <summary>
        /// A method to use when sending packets to a remote host.
        /// </summary>
        Action<INodePacket> SendPacket { get; }

        /// <summary>
        /// Clears the cache for the specified build submission ID.
        /// </summary>
        /// <param name="submissionId">The build submission ID to clear from the cache.</param>
        void ClearCache(int submissionId);

        /// <summary>
        /// Resolves the full path to the specified SDK.
        /// </summary>
        /// <param name="submissionId">The build submission ID that the resolution request is for.</param>
        /// <param name="sdk">A <see cref="SdkReference"/> that contains information about the SDK to resolve.</param>
        /// <param name="loggingContext">A <see cref="LoggingContext"/> to use when logging.</param>
        /// <param name="sdkReferenceLocation">The <see cref="ElementLocation"/> that specified the SDK.</param>
        /// <param name="solutionPath">The full path to the solution file, if any, that is resolving the SDK.</param>
        /// <param name="projectPath">The full path to the project file that is resolving the SDK.</param>
        /// <returns>The full path to the resolved SDK if found, otherwise <code>null</code>.</returns>
        string ResolveSdk(int submissionId, SdkReference sdk, LoggingContext loggingContext, ElementLocation sdkReferenceLocation, string solutionPath, string projectPath);
    }
}
