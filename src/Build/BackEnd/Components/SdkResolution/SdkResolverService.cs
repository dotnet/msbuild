// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.BackEnd.Components.Logging;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Eventing;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.BackEnd.SdkResolution
{
    /// <summary>
    /// The main implementation of <see cref="ISdkResolverService"/> which resolves SDKs.  This class is the central location for all SDK resolution and is used
    /// directly by the main node and non-build evaluations and is used indirectly by the out-of-proc node when it sends requests to the main node.
    /// </summary>
    internal class SdkResolverService : HostedSdkResolverServiceBase
    {
        /// <summary>
        /// Stores the singleton instance for a particular process.
        /// </summary>
        private static readonly Lazy<SdkResolverService> InstanceLazy = new Lazy<SdkResolverService>(() => new SdkResolverService());

        /// <summary>
        /// A lock object used for this class.
        /// </summary>
        private readonly object _lockObject = new object();

        /// <summary>
        /// Stores resolver state by build submission ID.
        /// </summary>
        private readonly ConcurrentDictionary<int, ConcurrentDictionary<SdkResolver, object>> _resolverStateBySubmission = new ConcurrentDictionary<int, ConcurrentDictionary<SdkResolver, object>>();

        /// <summary>
        /// Stores the cache in a set of concurrent dictionaries.  The main dictionary is by build submission ID and the inner dictionary contains a case-insensitive SDK name and the cached <see cref="SdkResult"/>.
        /// </summary>
        private readonly ConcurrentDictionary<int, ConcurrentDictionary<string, Lazy<SdkResult>>> _resultCacheBySubmissionId = new ConcurrentDictionary<int, ConcurrentDictionary<string, Lazy<SdkResult>>>();

        private bool _isResultCacheDisabled = Traits.Instance.EscapeHatches.DisableSdkResolutionCache;

        /// <summary>
        /// Stores the list of SDK resolvers which were loaded.
        /// </summary>
        private IList<SdkResolver> _resolvers;

        /// <summary>
        /// Stores an <see cref="SdkResolverLoader"/> which can load registered SDK resolvers.
        /// </summary>
        private SdkResolverLoader _sdkResolverLoader = new SdkResolverLoader();

        private SdkResolverService(SdkResolverLoader resolverLoader = null, IList<SdkResolver> resolvers = null)
        {
            if (resolverLoader != null)
            {
                _sdkResolverLoader = resolverLoader;
            }

            _resolvers ??= resolvers;
        }

        /// <summary>
        /// Gets the current instance of <see cref="SdkResolverService"/> for this process.
        /// </summary>
        public static SdkResolverService Instance => InstanceLazy.Value;

        /// <summary>
        /// A factory which is registered to create an instance of this class.
        /// </summary>
        public static IBuildComponent CreateComponent(BuildComponentType type)
        {
            return Instance;
        }

        /// <summary>
        /// Determines if the <see cref="SdkReference"/> is the same as the specified version.  If the <paramref name="sdk"/> object has <code>null</code> for the version,
        /// this method will always return true since <code>null</code> can match any version.
        /// </summary>
        /// <param name="sdk">An <see cref="SdkReference"/> object.</param>
        /// <param name="version">The version to compare.</param>
        /// <returns><code>true</code> if the specified SDK reference has the same version as the specified result, otherwise <code>false</code>.</returns>
        public static bool IsReferenceSameVersion(SdkReference sdk, string version)
        {
            // If the reference has a null version, it matches any result
            if (string.IsNullOrEmpty(sdk.Version))
            {
                return true;
            }

            return string.Equals(sdk.Version, version, StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc cref="ISdkResolverService.ClearCache"/>
        public override void ClearCache(int submissionId)
        {
            _resolverStateBySubmission.TryRemove(submissionId, out _);
        }

        public override void ClearCaches()
        {
            _resolverStateBySubmission.Clear();
        }

        /// <inheritdoc cref="INodePacketHandler.PacketReceived"/>
        public override void PacketReceived(int node, INodePacket packet)
        {
            if (packet.Type != NodePacketType.ResolveSdkRequest || packet is not SdkResolverRequest request)
            {
                return;
            }

            Task.Run(() => HandleRequest(node, request)).ConfigureAwait(continueOnCapturedContext: false);
        }

        /// <inheritdoc cref="ISdkResolverService.ResolveSdk"/>
        public override SdkResult ResolveSdk(int submissionId, SdkReference sdk, LoggingContext loggingContext, ElementLocation sdkReferenceLocation, string solutionPath, string projectPath, bool interactive, bool isRunningInVisualStudio)
        {
            SdkResult result;

            if (_isResultCacheDisabled)
            {
                result = ResolveSdkWithResolvers(submissionId, sdk, loggingContext, sdkReferenceLocation, solutionPath, projectPath, interactive, isRunningInVisualStudio);
            }
            else
            {
                // Get the dictionary for the specified submission if one is already added otherwise create a new dictionary for the submission.
                ConcurrentDictionary<string, Lazy<SdkResult>> cached = _resultCacheBySubmissionId.GetOrAdd(submissionId, new ConcurrentDictionary<string, Lazy<SdkResult>>(MSBuildNameIgnoreCaseComparer.Default));

                /*
                 * Get a Lazy<SdkResult> if available, otherwise create a Lazy<SdkResult> which will resolve the SDK with the SdkResolverService.Instance.  If multiple projects are attempting to resolve
                 * the same SDK, they will all get back the same Lazy<SdkResult> which ensures that a single build submission resolves each unique SDK only one time.
                 */
                Lazy<SdkResult> resultLazy = cached.GetOrAdd(
                    sdk.Name,
                    key => new Lazy<SdkResult>(() =>
                    {
                        if (sdk.Name.StartsWith("Test"))
                        {
                            System.IO.File.WriteAllText($@"D:\Temp\nugetsdkresolverrepro\Logs\{sdk.Name}-{System.IO.Path.GetFileNameWithoutExtension(sdkReferenceLocation.File)}-msbuild.resolvesdk-[{System.Diagnostics.Process.GetCurrentProcess().Id}]-({submissionId})-{Guid.NewGuid()}", $"submissionId={submissionId}");
                        }

                        return ResolveSdkWithResolvers(submissionId, sdk, loggingContext, sdkReferenceLocation, solutionPath, projectPath, interactive, isRunningInVisualStudio);
                    }));

                // Get the lazy value which will block all waiting threads until the SDK is resolved at least once while subsequent calls get cached results.
                result = resultLazy.Value;
            }

            if (result != null &&
                !SdkResolverService.IsReferenceSameVersion(sdk, result.SdkReference.Version) &&
                !SdkResolverService.IsReferenceSameVersion(sdk, result.Version))
            {
                // MSB4240: Multiple versions of the same SDK "{0}" cannot be specified. The previously resolved SDK version "{1}" from location "{2}" will be used and the version "{3}" will be ignored.
                loggingContext.LogWarning(null, new BuildEventFileInfo(sdkReferenceLocation), "ReferencingMultipleVersionsOfTheSameSdk", sdk.Name, result.Version, result.ElementLocation, sdk.Version);
            }

            return result;
        }

        /// <summary>
        /// UNIT TEST CONSTRUCTOR ONLY
        /// </summary>
        /// <param name="resolverLoader">An optional <see cref="SdkResolverLoader" /> to use when loading SDK resolvers.</param>
        /// <param name="resolvers">An optional <see cref="IList{T}" /> containing <see cref="SdkResolver" /> objects to use to resolver SDKs.</param>
        internal static SdkResolverService CreateForUnitTests(SdkResolverLoader resolverLoader = null, IList<SdkResolver> resolvers = null)
        {
            return new SdkResolverService(resolverLoader, resolvers);
        }

        /// <summary>
        /// Disables the result cache for unit tests that are resolving multiple SDKs with the same name in a different location.
        /// </summary>
        internal void DisableResultCacheForUnitTests()
        {
            _isResultCacheDisabled = true;

            _resultCacheBySubmissionId.Clear();
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

        private object GetResolverState(int submissionId, SdkResolver resolver)
        {
            // Do not fetch state for resolution requests that are not associated with a valid build submission ID
            if (submissionId != BuildEventContext.InvalidSubmissionId)
            {
                ConcurrentDictionary<SdkResolver, object> resolverState;

                if (_resolverStateBySubmission.TryGetValue(submissionId, out resolverState))
                {
                    object state;

                    if (resolverState.TryGetValue(resolver, out state))
                    {
                        return state;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Handles a request from a remote node.
        /// </summary>
        /// <param name="node">The ID of the remote node.</param>
        /// <param name="request">The <see cref="SdkResolverRequest"/> containing information about the SDK to resolve.</param>
        private void HandleRequest(int node, SdkResolverRequest request)
        {
            Thread.CurrentThread.Name ??= $"Handle request for SDK {request.Name}/{request.Version} from out-of-proc node {node}";

            request.NodeId = node;

            SdkResult response = null;
            try
            {
                // Create an SdkReference from the request
                SdkReference sdkReference = new SdkReference(request.Name, request.Version, request.MinimumVersion);

                ILoggingService loggingService = Host.GetComponent(BuildComponentType.LoggingService) as ILoggingService;

                // This call is usually cached so is very fast but can take longer for a new SDK that is downloaded.  Other queued threads for different SDKs will complete sooner and continue on which unblocks evaluations
                response = ResolveSdk(request.SubmissionId, sdkReference, new EvaluationLoggingContext(loggingService, request.BuildEventContext, request.ProjectPath), request.ElementLocation, request.SolutionPath, request.ProjectPath, request.Interactive, request.IsRunningInVisualStudio);
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

                nodeManager.SendData(request.NodeId, response);
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

                MSBuildEventSource.Log.SdkResolverServiceInitializeStart();
                _resolvers = _sdkResolverLoader.LoadResolvers(loggingContext, location);
                MSBuildEventSource.Log.SdkResolverServiceInitializeStop(_resolvers.Count);
            }
        }

        /// <inheritdoc cref="ISdkResolverService.ResolveSdk"/>
        private SdkResult ResolveSdkWithResolvers(int submissionId, SdkReference sdk, LoggingContext loggingContext, ElementLocation sdkReferenceLocation, string solutionPath, string projectPath, bool interactive, bool isRunningInVisualStudio)
        {
            // Lazy initialize the SDK resolvers
            if (_resolvers == null)
            {
                Initialize(loggingContext, sdkReferenceLocation);
            }

            List<SdkResult> results = new List<SdkResult>();

            // Loop through resolvers which have already been sorted by priority, returning the first result that was successful
            SdkLogger buildEngineLogger = new SdkLogger(loggingContext);

            loggingContext.LogComment(MessageImportance.Low, "SdkResolving", sdk.ToString());

            foreach (SdkResolver sdkResolver in _resolvers)
            {
                SdkResolverContext context = new SdkResolverContext(buildEngineLogger, projectPath, solutionPath, ProjectCollection.Version, interactive, isRunningInVisualStudio)
                {
                    State = GetResolverState(submissionId, sdkResolver)
                };

                SdkResultFactory resultFactory = new SdkResultFactory(sdk);

                SdkResult result;

                try
                {
                    MSBuildEventSource.Log.SdkResolverResolveSdkStart();
                    result = (SdkResult)sdkResolver.Resolve(sdk, context, resultFactory);
                    MSBuildEventSource.Log.SdkResolverResolveSdkStop(sdkResolver.Name, sdk.Name, solutionPath, projectPath, result?.Path, result?.Success ?? false);
                }
                catch (Exception e) when ((e is FileNotFoundException || e is FileLoadException) && sdkResolver.GetType().GetTypeInfo().Name.Equals("NuGetSdkResolver", StringComparison.Ordinal))
                {
                    // Since we explicitly add the NuGetSdkResolver, we special case this.  The NuGetSdkResolver has special logic
                    // to load NuGet assemblies at runtime which could fail if the user is not running installed MSBuild.  Rather
                    // than give them a generic error, we want to give a more specific message.  This exception cannot be caught by
                    // the resolver itself because it is usually thrown before the class is loaded
                    // The NuGet-based SDK resolver failed to run because NuGet assemblies could not be located.  Check your installation of MSBuild or set the environment variable "{0}" to the folder that contains the required NuGet assemblies. {1}
                    throw new SdkResolverException("CouldNotRunNuGetSdkResolver", sdkResolver, sdk, e, MSBuildConstants.NuGetAssemblyPathEnvironmentVariableName, e.ToString());
                }
                catch (Exception e)
                {
                    // The SDK resolver "{0}" failed while attempting to resolve the SDK "{1}": {2}
                    throw new SdkResolverException("SDKResolverFailed", sdkResolver, sdk, e, sdkResolver.Name, sdk.ToString(), e.ToString());
                }

                SetResolverState(submissionId, sdkResolver, context.State);

                if (result == null)
                {
                    continue;
                }

                if (result.Success)
                {
                    LogWarnings(loggingContext, sdkReferenceLocation, result);

                    if (!IsReferenceSameVersion(sdk, result.Version))
                    {
                        // MSB4241: The SDK reference "{0}" version "{1}" was resolved to version "{2}" instead.  You could be using a different version than expected if you do not update the referenced version to match.
                        loggingContext.LogWarning(null, new BuildEventFileInfo(sdkReferenceLocation), "SdkResultVersionDifferentThanReference", sdk.Name, sdk.Version, result.Version);
                    }

                    // Associate the element location of the resolved SDK reference
                    result.ElementLocation = sdkReferenceLocation;

                    return result;
                }

                results.Add(result);
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

            return new SdkResult(sdk, null, null);
        }

        private void SetResolverState(int submissionId, SdkResolver resolver, object state)
        {
            // Do not set state for resolution requests that are not associated with a valid build submission ID
            if (submissionId != BuildEventContext.InvalidSubmissionId)
            {
                ConcurrentDictionary<SdkResolver, object> resolverState = _resolverStateBySubmission.GetOrAdd(
                    submissionId,
                    _ => new ConcurrentDictionary<SdkResolver, object>(NativeMethodsShared.GetLogicalCoreCount(), _resolvers.Count));

                resolverState.AddOrUpdate(resolver, state, (sdkResolver, obj) => state);
            }
        }
    }
}
