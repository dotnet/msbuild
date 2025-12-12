// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.BackEnd.Components.Logging;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.BackEnd.SdkResolution
{
    /// <summary>
    /// An implementation of <see cref="ISdkResolverService"/> that is hosted in the main node for multi-proc builds.  This instance of the service
    /// handles requests from out-of-proc nodes so that SDK resolution is handled in a central location.  This instance is registered in <see cref="BuildComponentFactoryCollection.RegisterDefaultFactories"/>
    /// and can be overridden for different contexts.  This service calls the <see cref="SdkResolverService"/> to do any actual SDK resolution
    /// because the <see cref="SdkResolverService"/> is used for stand-alone evaluations where there is no build context available so caching
    /// is not an option.
    ///
    /// Since this object is a registered <see cref="IBuildComponent"/>, it is a singleton for the main process.  To get an instance of it, you
    /// must have access to an <see cref="IBuildComponentHost"/> and call <see cref="IBuildComponentHost.GetComponent"/> and pass <see cref="BuildComponentType.SdkResolverService"/>.
    /// </summary>
    internal sealed class MainNodeSdkResolverService : HostedSdkResolverServiceBase
    {
        private readonly ISdkResolverService _cachedSdkResolver = new CachingSdkResolverService();

        /// <summary>
        /// A factory which is registered to create an instance of this class.
        /// </summary>
        public static IBuildComponent CreateComponent(BuildComponentType type)
        {
            ErrorUtilities.VerifyThrowArgumentOutOfRange(type == BuildComponentType.SdkResolverService, nameof(type));

            return new MainNodeSdkResolverService();
        }

        // Test hook
        internal void InitializeForTests(SdkResolverLoader resolverLoader = null, IReadOnlyList<SdkResolver> resolvers = null)
        {
            ((CachingSdkResolverService)_cachedSdkResolver).InitializeForTests(resolverLoader, resolvers);
        }

        /// <inheritdoc cref="ISdkResolverService.ClearCache"/>
        public override void ClearCache(int submissionId)
        {
            _cachedSdkResolver.ClearCache(submissionId);
        }

        public override void ClearCaches()
        {
            _cachedSdkResolver.ClearCaches();
        }

        /// <inheritdoc cref="INodePacketHandler.PacketReceived"/>
        public override void PacketReceived(int node, INodePacket packet)
        {
            if (packet is not SdkResolverRequest request)
            {
                return;
            }

            // Associate the node with the request
            request.NodeId = node;

            SdkResult response = null;

            // Create an SdkReference from the request; the SdkReference constructor below never throws.
            SdkReference sdkReference = new SdkReference(request.Name, request.Version, request.MinimumVersion);
            try
            {
                ILoggingService loggingService = Host.GetComponent(BuildComponentType.LoggingService) as ILoggingService;
                bool failOnUnresolvedSdk = !Host.BuildParameters.ProjectLoadSettings.HasFlag(ProjectLoadSettings.IgnoreMissingImports) || Host.BuildParameters.ProjectLoadSettings.HasFlag(ProjectLoadSettings.FailOnUnresolvedSdk);

                // This call is usually cached so is very fast but can take longer for a new SDK that is downloaded.  Other queued threads for different SDKs will complete sooner and continue on which unblocks evaluations
                response = ResolveSdk(request.SubmissionId, sdkReference, new EvaluationLoggingContext(loggingService, request.BuildEventContext, request.ProjectPath), request.ElementLocation, request.SolutionPath, request.ProjectPath, request.Interactive, request.IsRunningInVisualStudio, failOnUnresolvedSdk);
            }
            catch (Exception e)
            {
                ILoggingService loggingService = Host.GetComponent(BuildComponentType.LoggingService) as ILoggingService;

                EvaluationLoggingContext loggingContext = new EvaluationLoggingContext(loggingService, request.BuildEventContext, request.ProjectPath);

                loggingService.LogFatalBuildError(loggingContext.BuildEventContext, e, new BuildEventFileInfo(request.ElementLocation));
            }
            finally
            {
                // Get the node manager and send the response back to the node that requested the SDK
                INodeManager nodeManager = Host.GetComponent(BuildComponentType.NodeManager) as INodeManager;

                nodeManager.SendData(request.NodeId, response ?? new SdkResult(sdkReference, null, null));
            }
        }

        /// <inheritdoc cref="ISdkResolverService.ResolveSdk"/>
        public override SdkResult ResolveSdk(int submissionId, SdkReference sdk, LoggingContext loggingContext, ElementLocation sdkReferenceLocation, string solutionPath, string projectPath, bool interactive, bool isRunningInVisualStudio, bool failOnUnresolvedSdk)
        {
            ErrorUtilities.VerifyThrowInternalNull(sdk, nameof(sdk));
            ErrorUtilities.VerifyThrowInternalNull(loggingContext, nameof(loggingContext));
            ErrorUtilities.VerifyThrowInternalNull(sdkReferenceLocation, nameof(sdkReferenceLocation));
            ErrorUtilities.VerifyThrowInternalLength(projectPath, nameof(projectPath));

            return _cachedSdkResolver.ResolveSdk(submissionId, sdk, loggingContext, sdkReferenceLocation, solutionPath, projectPath, interactive, isRunningInVisualStudio, failOnUnresolvedSdk);
        }
    }
}
