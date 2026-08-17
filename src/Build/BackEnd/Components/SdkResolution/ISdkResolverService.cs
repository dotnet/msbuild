// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework;

#nullable disable

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
        /// Clear the entire cache
        /// </summary>
        void ClearCaches();

        /// <summary>
        ///  Resolves the full path to the specified SDK.
        /// </summary>
        /// <param name="submissionId">The build submission ID that the resolution request is for.</param>
        /// <param name="sdk">The <see cref="SdkReference"/> containing information about the referenced SDK.</param>
        /// <param name="loggingContext">The <see cref="LoggingContext"/> to use when logging messages during resolution.</param>
        /// <param name="sdkReferenceLocation">The <see cref="ElementLocation"/> of the element which referenced the SDK.</param>
        /// <param name="solutionPath">The full path to the solution file, if any, that is resolving the SDK.</param>
        /// <param name="projectPath">The full path to the project file that is resolving the SDK.</param>
        /// <param name="interactive">Indicates whether or not the resolver is allowed to be interactive.</param>
        /// <param name="isRunningInVisualStudio">Indicates whether or not the resolver is running in Visual Studio.</param>
        /// <param name="failOnUnresolvedSdk">Whether to throw an exception should the SDK fail to be resolved.</param>
        /// <returns>An <see cref="SdkResult"/> containing information about the resolved SDK. If no resolver was able to resolve it, then <see cref="Framework.SdkResult.Success"/> == false. </returns>
        SdkResult ResolveSdk(int submissionId, SdkReference sdk, LoggingContext loggingContext, ElementLocation sdkReferenceLocation, string solutionPath, string projectPath, bool interactive, bool isRunningInVisualStudio, bool failOnUnresolvedSdk);
    }
}
