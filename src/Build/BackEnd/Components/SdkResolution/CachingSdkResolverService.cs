// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Eventing;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.BackEnd.SdkResolution
{
    internal sealed class CachingSdkResolverService : SdkResolverService
    {
        /// <summary>
        /// Stores the cache in a set of concurrent dictionaries.  The main dictionary is by build submission ID and the inner dictionary contains a case-insensitive SDK name and the cached <see cref="SdkResult"/>.
        /// </summary>
        private readonly ConcurrentDictionary<int, ConcurrentDictionary<string, Lazy<SdkResult>>> _cache = new ConcurrentDictionary<int, ConcurrentDictionary<string, Lazy<SdkResult>>>();

        /// <summary>
        /// Stores the singleton instance for a particular process.
        /// </summary>
        private static readonly Lazy<CachingSdkResolverService> InstanceLazy = new Lazy<CachingSdkResolverService>(() => new CachingSdkResolverService(), isThreadSafe: true);

        private CachingSdkResolverService()
        {
        }

        /// <summary>
        /// Gets the current instance of <see cref="CachingSdkResolverService"/> for this process.
        /// </summary>
        public static new CachingSdkResolverService Instance => InstanceLazy.Value;

        public override void ClearCache(int submissionId)
        {
            base.ClearCache(submissionId);

            _cache.TryRemove(submissionId, out _);
        }

        public override void ClearCaches()
        {
            base.ClearCaches();

            _cache.Clear();
        }

        public override SdkResult ResolveSdk(int submissionId, SdkReference sdk, LoggingContext loggingContext, ElementLocation sdkReferenceLocation, string solutionPath, string projectPath, bool interactive, bool isRunningInVisualStudio, bool failOnUnresolvedSdk)
        {
            SdkResult result;

            bool wasResultCached = true;

            MSBuildEventSource.Log.CachedSdkResolverServiceResolveSdkStart(sdk.Name, solutionPath ?? string.Empty, projectPath ?? string.Empty);

            if (Traits.Instance.EscapeHatches.DisableSdkResolutionCache)
            {
                result = base.ResolveSdk(submissionId, sdk, loggingContext, sdkReferenceLocation, solutionPath, projectPath, interactive, isRunningInVisualStudio, failOnUnresolvedSdk);
            }
            else
            {
                // Get the dictionary for the specified submission if one is already added otherwise create a new dictionary for the submission.
                ConcurrentDictionary<string, Lazy<SdkResult>> cached = _cache.GetOrAdd(
                    submissionId,
                    _ => new ConcurrentDictionary<string, Lazy<SdkResult>>(MSBuildNameIgnoreCaseComparer.Default));

                /*
                 * Get a Lazy<SdkResult> if available, otherwise create a Lazy<SdkResult> which will resolve the SDK with the SdkResolverService.Instance.  If multiple projects are attempting to resolve
                 * the same SDK, they will all get back the same Lazy<SdkResult> which ensures that a single build submission resolves each unique SDK only one time.
                 */
                Lazy<SdkResult> resultLazy = cached.GetOrAdd(
                    sdk.Name,
                    key => new Lazy<SdkResult>(() =>
                    {
                        wasResultCached = false;

                        return base.ResolveSdk(submissionId, sdk, loggingContext, sdkReferenceLocation, solutionPath, projectPath, interactive, isRunningInVisualStudio, failOnUnresolvedSdk);
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

            MSBuildEventSource.Log.CachedSdkResolverServiceResolveSdkStop(sdk.Name, solutionPath ?? string.Empty, projectPath ?? string.Empty, result.Success, wasResultCached);

            return result;
        }

        /// <summary>
        /// Used for unit tests only.
        /// </summary>
        /// <param name="resolverLoader">An <see cref="SdkResolverLoader"/> to use for loading SDK resolvers.</param>
        /// <param name="resolvers">Explicit set of SdkResolvers to use for all SDK resolution.</param>
        internal new void InitializeForTests(SdkResolverLoader resolverLoader = null, IReadOnlyList<SdkResolver> resolvers = null)
        {
            _cache.Clear();
            base.InitializeForTests(resolverLoader, resolvers);
        }

    }
}
