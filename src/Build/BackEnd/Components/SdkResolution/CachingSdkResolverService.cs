// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Threading;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation.Context;
using Microsoft.Build.Eventing;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.BackEnd.SdkResolution
{
    internal sealed class CachingSdkResolverService : SdkResolverService, ISdkResolverCacheValidator
    {
        private const string CacheKeyComparer = "MSBuildNameIgnoreCase";

        private static long s_nextCacheIdentity;

        /// <summary>
        /// Stores the cache in a set of concurrent dictionaries.  The main dictionary is by build submission ID and the inner dictionary contains a case-insensitive SDK name and the cached <see cref="SdkResult"/>.
        /// </summary>
        private readonly ConcurrentDictionary<int, SubmissionCache> _cache = new();
        private readonly long _cacheIdentity = Interlocked.Increment(ref s_nextCacheIdentity);
        private long _nextCacheEpoch;
        private long _nextEntryId;

        internal bool TestOnlyDisableCache { get; set; }

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

        public bool IsCacheEntryCurrent(SdkResolverCacheIdentity cacheIdentity)
        {
            if (!cacheIdentity.CacheEnabled ||
                cacheIdentity.OwnerId != _cacheIdentity ||
                !string.Equals(
                    cacheIdentity.OwnerType,
                    typeof(CachingSdkResolverService).FullName,
                    StringComparison.Ordinal) ||
                !string.Equals(cacheIdentity.ScopeKind, "Submission", StringComparison.Ordinal) ||
                !string.Equals(cacheIdentity.KeyComparer, CacheKeyComparer, StringComparison.Ordinal))
            {
                return false;
            }

            return _cache.TryGetValue(cacheIdentity.ScopeId, out SubmissionCache submissionCache) &&
                submissionCache.Epoch == cacheIdentity.Epoch &&
                submissionCache.Entries.TryGetValue(cacheIdentity.Key, out CacheEntry entry) &&
                entry.Id == cacheIdentity.EntryId;
        }

        public override SdkResult ResolveSdk(int submissionId, SdkReference sdk, LoggingContext loggingContext, ElementLocation sdkReferenceLocation, string solutionPath, string projectPath, bool interactive, bool isRunningInVisualStudio, bool failOnUnresolvedSdk)
        {
            SdkResult result;
            bool wasResultCached = true;
            SdkResolverCacheIdentity cacheIdentity;

            MSBuildEventSource.Log.CachedSdkResolverServiceResolveSdkStart(sdk.Name, solutionPath ?? string.Empty, projectPath ?? string.Empty);

            if (TestOnlyDisableCache || Traits.Instance.EscapeHatches.DisableSdkResolutionCache)
            {
                wasResultCached = false;
                cacheIdentity = new SdkResolverCacheIdentity(
                    typeof(CachingSdkResolverService).FullName,
                    _cacheIdentity,
                    "None",
                    0,
                    0,
                    Interlocked.Increment(ref _nextEntryId),
                    sdk.Name,
                    CacheKeyComparer,
                    cacheEnabled: false);
                result = base.ResolveSdk(submissionId, sdk, loggingContext, sdkReferenceLocation, solutionPath, projectPath, interactive, isRunningInVisualStudio, failOnUnresolvedSdk);
            }
            else
            {
                // Get the dictionary for the specified submission if one is already added otherwise create a new dictionary for the submission.
                SubmissionCache cached = _cache.GetOrAdd(
                    submissionId,
                    _ => new SubmissionCache(Interlocked.Increment(ref _nextCacheEpoch)));

                /*
                 * Get a Lazy<SdkResult> if available, otherwise create a Lazy<SdkResult> which will resolve the SDK with the SdkResolverService.Instance.  If multiple projects are attempting to resolve
                 * the same SDK, they will all get back the same Lazy<SdkResult> which ensures that a single build submission resolves each unique SDK only one time.
                 */
                CacheEntry entry;
                if (cached.Entries.TryGetValue(sdk.Name, out entry))
                {
                    wasResultCached = true;
                }
                else
                {
                    var candidate = new CacheEntry(
                        Interlocked.Increment(ref _nextEntryId),
                        new Lazy<SdkResult>(() =>
                            base.ResolveSdk(submissionId, sdk, loggingContext, sdkReferenceLocation, solutionPath, projectPath, interactive, isRunningInVisualStudio, failOnUnresolvedSdk)));
                    entry = cached.Entries.GetOrAdd(sdk.Name, candidate);
                    wasResultCached = !ReferenceEquals(entry, candidate);
                }

                // Get the lazy value which will block all waiting threads until the SDK is resolved at least once while subsequent calls get cached results.
                result = entry.Result.Value;
                cacheIdentity = new SdkResolverCacheIdentity(
                    typeof(CachingSdkResolverService).FullName,
                    _cacheIdentity,
                    "Submission",
                    submissionId,
                    cached.Epoch,
                    entry.Id,
                    sdk.Name,
                    CacheKeyComparer,
                    cacheEnabled: true);
            }

            if (result != null &&
                !SdkResolverService.IsReferenceSameVersion(sdk, result.SdkReference.Version) &&
                !SdkResolverService.IsReferenceSameVersion(sdk, result.Version))
            {
                // MSB4240: Multiple versions of the same SDK "{0}" cannot be specified. The previously resolved SDK version "{1}" from location "{2}" will be used and the version "{3}" will be ignored.
                loggingContext.LogWarning(null, new BuildEventFileInfo(sdkReferenceLocation), "ReferencingMultipleVersionsOfTheSameSdk", sdk.Name, result.Version, result.ElementLocation, sdk.Version);
            }

            EvaluationObservationSession.Current?.RecordSdkResolution(
                submissionId,
                sdk,
                result,
                wasResultCached,
                cacheIdentity,
                projectPath,
                solutionPath,
                interactive,
                isRunningInVisualStudio,
                failOnUnresolvedSdk,
                sdkReferenceLocation);

            MSBuildEventSource.Log.CachedSdkResolverServiceResolveSdkStop(sdk.Name, solutionPath ?? string.Empty, projectPath ?? string.Empty, result.Success, wasResultCached);

            return result;
        }

        private sealed class SubmissionCache
        {
            internal SubmissionCache(long epoch)
            {
                Epoch = epoch;
                Entries = new ConcurrentDictionary<string, CacheEntry>(MSBuildNameIgnoreCaseComparer.Default);
            }

            internal long Epoch { get; }
            internal ConcurrentDictionary<string, CacheEntry> Entries { get; }
        }

        private sealed class CacheEntry
        {
            internal CacheEntry(long id, Lazy<SdkResult> result)
            {
                Id = id;
                Result = result;
            }

            internal long Id { get; }
            internal Lazy<SdkResult> Result { get; }
        }
    }
}
