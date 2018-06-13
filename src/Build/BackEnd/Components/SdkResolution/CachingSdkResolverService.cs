// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.BackEnd.SdkResolution
{

    internal sealed class CachingSdkResolverService: SdkResolverService
    {
        /// <summary>
        /// Stores the cache in a set of concurrent dictionaries.  The main dictionary is by build submission ID and the inner dictionary contains a case-insensitive SDK name and the cached <see cref="SdkResult"/>.
        /// </summary>
        private readonly ConcurrentDictionary<int, ConcurrentDictionary<string, SdkResult>> _cache = new ConcurrentDictionary<int, ConcurrentDictionary<string, SdkResult>>();

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

        public override SdkResult ResolveSdk(int submissionId, SdkReference sdk, LoggingContext loggingContext, ElementLocation sdkReferenceLocation, string solutionPath, string projectPath)
        {
            SdkResult result;

            if (Traits.Instance.EscapeHatches.DisableSdkResolutionCache)
            {
                result = base.ResolveSdk(submissionId, sdk, loggingContext, sdkReferenceLocation, solutionPath, projectPath);
            }
            else
            {
                // Get the dictionary for the specified submission if one is already added otherwise create a new dictionary for the submission.
                ConcurrentDictionary<string, SdkResult> cached = _cache.GetOrAdd(submissionId, new ConcurrentDictionary<string, SdkResult>(MSBuildNameIgnoreCaseComparer.Default));

                /*
                 * Get a cached result if available, otherwise resolve the SDK with the SdkResolverService.Instance.  If multiple projects are attempting to resolve
                 * the same SDK, they will all block while the first one resolves.  Blocked requests will then get the cached result.  This ensures that a single
                 * build submission resolves each unique SDK only one time.
                 */
                result = cached.GetOrAdd(
                    sdk.Name,
                    key => base.ResolveSdk(submissionId, sdk, loggingContext, sdkReferenceLocation, solutionPath, projectPath));
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
    }
}
