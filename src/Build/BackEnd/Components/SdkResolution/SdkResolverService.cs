// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using System;
using System.Collections.Generic;

namespace Microsoft.Build.BackEnd.SdkResolution
{
    /// <summary>
    /// The main implementation of <see cref="ISdkResolverService"/> which resolves SDKs.  This class is the central location for all SDK resolution and is used
    /// directly by the main node and non-build evaluations and is used indirectly by the out-of-proc node when it sends requests to the main node.
    /// 
    /// All access to this class must go through the singleton <see cref="SdkResolverService.Instance"/>.
    /// </summary>
    internal sealed class SdkResolverService : ISdkResolverService
    {
        /// <summary>
        /// Stores the singleton instance for a particular process.
        /// </summary>
        private static readonly Lazy<SdkResolverService> InstanceLazy = new Lazy<SdkResolverService>(() => new SdkResolverService(), isThreadSafe: true);

        /// <summary>
        /// A lock object used for this class.
        /// </summary>
        private readonly object _lockObject = new object();

        /// <summary>
        /// Stores an <see cref="SdkResolverLoader"/> which can load registered SDK resolvers.
        /// </summary>
        private readonly SdkResolverLoader _sdkResolverLoader = new SdkResolverLoader();

        /// <summary>
        /// Stores the list of SDK resolvers which were loaded.
        /// </summary>
        private IList<SdkResolver> _resolvers;

        private SdkResolverService()
        {
        }

        /// <summary>
        /// Gets the current instance of <see cref="SdkResolverService"/> for this process.
        /// </summary>
        public static SdkResolverService Instance => InstanceLazy.Value;

        /// <inheritdoc cref="ISdkResolverService.SendPacket"/>
        public Action<INodePacket> SendPacket { get; }

        /// <inheritdoc cref="ISdkResolverService.ClearCache"/>
        public void ClearCache(int submissionId)
        {
        }

        /// <summary>
        /// Resolves and SDK and gets a result.
        /// </summary>
        /// <param name="sdk">The <see cref="SdkReference"/> containing information about the referenced SDK.</param>
        /// <param name="loggingContext">The <see cref="LoggingContext"/> to use when logging messages during resolution.</param>
        /// <param name="sdkReferenceLocation">The <see cref="ElementLocation"/> of the element which referenced the SDK.</param>
        /// <param name="solutionPath">The full path to the solution, if any, that is being built.</param>
        /// <param name="projectPath">The full path to that referenced the SDK.</param>
        /// <returns>An <see cref="SdkResult"/> containing information of the SDK if it could be resolved, otherwise <code>null</code>.</returns>
        public SdkResult GetSdkResult(SdkReference sdk, LoggingContext loggingContext, ElementLocation sdkReferenceLocation, string solutionPath, string projectPath)
        {
            // Lazy initialize the SDK resolvers
            if (_resolvers == null)
            {
                Initialize(loggingContext, sdkReferenceLocation);
            }

            List<SdkResult> results = new List<SdkResult>();

            try
            {
                // Loop through resolvers which have already been sorted by priority, returning the first result that was successful
                SdkLogger buildEngineLogger = new SdkLogger(loggingContext);

                foreach (SdkResolver sdkResolver in _resolvers)
                {
                    SdkResolverContext context = new SdkResolverContext(buildEngineLogger, projectPath, solutionPath, ProjectCollection.Version);

                    SdkResultFactory resultFactory = new SdkResultFactory(sdk);
                    try
                    {
                        SdkResult result = (SdkResult) sdkResolver.Resolve(sdk, context, resultFactory);
                        if (result == null)
                        {
                            continue;
                        }

                        if (result.Success)
                        {
                            LogWarnings(loggingContext, sdkReferenceLocation, result);
                            return result;
                        }

                        results.Add(result);
                    }
                    catch (Exception e)
                    {
                        loggingContext.LogFatalBuildError(e, new BuildEventFileInfo(sdkReferenceLocation));
                    }
                }
            }
            catch (Exception e)
            {
                loggingContext.LogFatalBuildError(e, new BuildEventFileInfo(sdkReferenceLocation));
                throw;
            }

            foreach (SdkResult result in results)
            {
                LogWarnings(loggingContext, sdkReferenceLocation, result);

                if (result.Errors != null)
                {
                    foreach (string error in result.Errors)
                    {
                        loggingContext.LogErrorFromText(subcategoryResourceName: null, errorCode: null, helpKeyword: null, file: new BuildEventFileInfo(sdkReferenceLocation), message: error);
                    }
                }
            }

            return null;
        }

        /// <inheritdoc cref="ISdkResolverService.ResolveSdk"/>
        public string ResolveSdk(int submissionId, SdkReference sdk, LoggingContext loggingContext, ElementLocation sdkReferenceLocation, string solutionPath, string projectPath)
        {
            SdkResult result = GetSdkResult(sdk, loggingContext, sdkReferenceLocation, solutionPath, projectPath);

            return result?.Path;
        }

        private static void LogWarnings(LoggingContext loggingContext, ElementLocation location, SdkResult result)
        {
            if (result.Warnings == null)
            {
                return;
            }

            foreach (string warning in result.Warnings)
            {
                loggingContext.LogWarningFromText(null, null, null, new BuildEventFileInfo(location), warning);
            }
        }

        private void Initialize(LoggingContext loggingContext, ElementLocation location)
        {
            lock (_lockObject)
            {
                if (_resolvers != null)
                {
                    return;
                }

                _resolvers = _sdkResolverLoader.LoadResolvers(loggingContext, location);
            }
        }

        /// <summary>
        /// Used for unit tests only.  This is currently only called through reflection in Microsoft.Build.Engine.UnitTests.TransientSdkResolution.CallResetForTests
        /// </summary>
        /// <param name="resolvers">Explicit set of SdkResolvers to use for all SDK resolution.</param>
        // ReSharper disable once UnusedMember.Local
        private void InitializeForTests(IList<SdkResolver> resolvers)
        {
            _resolvers = resolvers;
        }
    }
}
