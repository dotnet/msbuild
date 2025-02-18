// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Eventing;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.BackEnd.SdkResolution
{
    /// <summary>
    /// The main implementation of <see cref="ISdkResolverService"/> which resolves SDKs.  This class is the central location for all SDK resolution and is used
    /// directly by the main node and non-build evaluations and is used indirectly by the out-of-proc node when it sends requests to the main node.
    /// </summary>
    internal class SdkResolverService : ISdkResolverService
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
        /// Stores resolver state by build submission ID.
        /// </summary>
        private readonly ConcurrentDictionary<int, ConcurrentDictionary<SdkResolver, object>> _resolverStateBySubmission = new ConcurrentDictionary<int, ConcurrentDictionary<SdkResolver, object>>();

        /// <summary>
        /// Stores the loaded SDK resolvers, mapped to the manifest from which they came.
        /// </summary>
        private Dictionary<SdkResolverManifest, IReadOnlyList<SdkResolver>> _manifestToResolvers;

        /// <summary>
        /// Stores the list of manifests of specific SDK resolvers which could be loaded.
        /// </summary>
        protected IReadOnlyList<SdkResolverManifest> _specificResolversManifestsRegistry;

        /// <summary>
        /// Stores the list of manifests of general SDK resolvers which could be loaded.
        /// </summary>
        protected IReadOnlyList<SdkResolverManifest> _generalResolversManifestsRegistry;

        /// <summary>
        /// Stores an <see cref="SdkResolverLoader"/> which can load registered SDK resolvers.
        /// </summary>
        /// <remarks>
        /// Unless the 17.10 changewave is disabled, we use a singleton instance because the set of SDK resolvers
        /// is not expected to change during the lifetime of the process.
        /// </remarks>
        protected SdkResolverLoader _sdkResolverLoader = ChangeWaves.AreFeaturesEnabled(ChangeWaves.Wave17_10)
            ? CachingSdkResolverLoader.Instance
            : new SdkResolverLoader();

        public SdkResolverService()
        {
        }

        /// <summary>
        /// Gets the current instance of <see cref="SdkResolverService"/> for this process.
        /// </summary>
        public static SdkResolverService Instance => InstanceLazy.Value;

        /// <inheritdoc cref="ISdkResolverService.SendPacket"/>
        public Action<INodePacket> SendPacket { get; }

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
            if (String.IsNullOrEmpty(sdk.Version))
            {
                return true;
            }

            return String.Equals(sdk.Version, version, StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc cref="ISdkResolverService.ClearCache"/>
        public virtual void ClearCache(int submissionId)
        {
            _resolverStateBySubmission.TryRemove(submissionId, out _);
        }

        public virtual void ClearCaches()
        {
            _resolverStateBySubmission.Clear();
        }

        /// <inheritdoc cref="ISdkResolverService.ResolveSdk"/>
        public virtual SdkResult ResolveSdk(int submissionId, SdkReference sdk, LoggingContext loggingContext, ElementLocation sdkReferenceLocation, string solutionPath, string projectPath, bool interactive, bool isRunningInVisualStudio, bool failOnUnresolvedSdk)
        {
            // If we are running in .NET core, we ask the built-in default resolver first.
            // - It is a perf optimization (no need to discover and load any of the plug-in assemblies to resolve an "in-box" Sdk).
            // - It brings `dotnet build` to parity with `MSBuild.exe` functionally, as the Framework build of Microsoft.DotNet.MSBuildSdkResolver
            //   contains the same logic and it is the first resolver in priority order.
            //
            // In an attempt to avoid confusion, this text uses "SDK" to refer to the installation unit, e.g. "C:\Program Files\dotnet\sdk\8.0.100",
            // and "Sdk" to refer to the set of imports for targeting a specific project type, e.g. "Microsoft.NET.Sdk.Web".
            //
            // Here's the flow on Framework (`MSBuild.exe`):
            // 1. Microsoft.DotNet.MSBuildSdkResolver is loaded and asked to resolve the Sdk required by the project.
            //    1.1. It resolves the SDK (as in installation directory) using machine-wide state and global.json.
            //    1.2. It checks the Sdks subdirectory of the SDK installation directory for a matching in-box Sdk.
            //    1.3. If no match, checks installed workloads.
            // 2. If no match so far, Microsoft.Build.NuGetSdkResolver is loaded and asked to resolve the Sdk.
            // 3. If no match still, DefaultSdkResolver checks the Sdks subdirectory of the Visual Studio\MSBuild directory.
            //
            // Here's the flow on Core (`dotnet build`):
            // 1. DefaultSdkResolver checks the Sdks subdirectory of our SDK installation. Note that the work of resolving the
            //    SDK version using machine-wide state and global.json (step 1.1. in `MSBuild.exe` above) has already been done
            //    by the `dotnet` muxer. We know which SDK (capital letters) we are in, so the in-box Sdk lookup is trivial.
            // 2. If no match, Microsoft.NET.Sdk.WorkloadMSBuildSdkResolver is loaded and asked to resolve the Sdk required by the project.
            //    2.1. It checks installed workloads.
            // 3. If no match still, Microsoft.Build.NuGetSdkResolver is loaded and asked to resolve the Sdk.
            //
            // Overall, while Sdk resolvers look like a general plug-in system, there are good reasons why some of the logic is hard-coded.
            // It's not really meant to be modified outside of very special/internal scenarios.
#if NET
            if (ChangeWaves.AreFeaturesEnabled(ChangeWaves.Wave17_10))
            {
                if (TryResolveSdkUsingSpecifiedResolvers(
                    _sdkResolverLoader.GetDefaultResolvers(),
                    BuildEventContext.InvalidSubmissionId, // disables GetResolverState/SetResolverState
                    sdk,
                    loggingContext,
                    sdkReferenceLocation,
                    solutionPath,
                    projectPath,
                    interactive,
                    isRunningInVisualStudio,
                    out SdkResult sdkResult,
                    out _,
                    out _))
                {
                    return sdkResult;
                }
            }
#endif

            return ResolveSdkUsingResolversWithPatternsFirst(submissionId, sdk, loggingContext, sdkReferenceLocation, solutionPath, projectPath, interactive, isRunningInVisualStudio, failOnUnresolvedSdk);
        }

        /// <remarks>
        /// Resolves the sdk in two passes. First pass consists of all specific resolvers (i.e. resolvers with pattern), which match the sdk name.
        /// The resolvers are ordered by the priority in first pass and are tried until one of them succeeds.
        /// If the first pass is unsuccessful, on the second pass all the general resolvers (i.e. resolvers without pattern), ordered by their priority, are tried one after one.
        /// After that, if the second pass is unsuccessful, sdk resolution is unsuccessful.
        /// </remarks>
        private SdkResult ResolveSdkUsingResolversWithPatternsFirst(int submissionId, SdkReference sdk, LoggingContext loggingContext, ElementLocation sdkReferenceLocation, string solutionPath, string projectPath, bool interactive, bool isRunningInVisualStudio, bool failOnUnresolvedSdk)
        {
            if (_specificResolversManifestsRegistry == null || _generalResolversManifestsRegistry == null)
            {
                RegisterResolversManifests(sdkReferenceLocation);
            }

            // Pick up the matching specific resolvers from the list of resolvers.
            List<SdkResolverManifest> matchingResolversManifests = new();
            foreach (SdkResolverManifest manifest in _specificResolversManifestsRegistry)
            {
                WaitIfTestRequires(); 
                try
                {
                    if (manifest.ResolvableSdkRegex.IsMatch(sdk.Name))
                    {
                        matchingResolversManifests.Add(manifest);
                    }
                }
                catch (RegexMatchTimeoutException ex)
                {
                    ErrorUtilities.ThrowInternalError("Timeout exceeded matching sdk \"{0}\" to <ResolvableSdkPattern> from sdk resolver manifest {1}.", ex, sdk.Name, manifest.DisplayName);
                }
            }

            List<SdkResolver> resolvers;
            SdkResult sdkResult;
            List<string> errors = new List<string>(0);
            List<string> warnings = new List<string>(0);
            if (matchingResolversManifests.Count != 0)
            {
                // First pass.
                resolvers = GetResolvers(matchingResolversManifests, loggingContext, sdkReferenceLocation);

                if (TryResolveSdkUsingSpecifiedResolvers(
                    resolvers,
                    submissionId,
                    sdk,
                    loggingContext,
                    sdkReferenceLocation,
                    solutionPath,
                    projectPath,
                    interactive,
                    isRunningInVisualStudio,
                    out sdkResult,
                    out IEnumerable<string> firstErrors,
                    out IEnumerable<string> firstWarnings))
                {
                    return sdkResult;
                }

                errors.AddRange(firstErrors);
                warnings.AddRange(firstWarnings);
            }

            // Second pass: fallback to general resolvers.
            resolvers = GetResolvers(
                _generalResolversManifestsRegistry,
                loggingContext,
                sdkReferenceLocation).ToList();

            if (TryResolveSdkUsingSpecifiedResolvers(
                resolvers,
                submissionId,
                sdk,
                loggingContext,
                sdkReferenceLocation,
                solutionPath,
                projectPath,
                interactive,
                isRunningInVisualStudio,
                out sdkResult,
                out IEnumerable<string> moreErrors,
                out IEnumerable<string> moreWarnings))
            {
                return sdkResult;
            }

            errors.AddRange(moreErrors);
            warnings.AddRange(moreWarnings);

            if (failOnUnresolvedSdk)
            {
                loggingContext.LogError(new BuildEventFileInfo(sdkReferenceLocation), "FailedToResolveSDK", sdk.Name, string.Join($"{Environment.NewLine}  ", errors));
            }

            LogWarnings(loggingContext, sdkReferenceLocation, warnings);

            // No resolvers resolved the sdk.
            return new SdkResult(sdk, null, null);
        }

        private List<SdkResolver> GetResolvers(IReadOnlyList<SdkResolverManifest> resolversManifests, LoggingContext loggingContext, ElementLocation sdkReferenceLocation)
        {
            // Create a sorted by priority list of resolvers. Load them if needed.
            List<SdkResolver> resolvers = new List<SdkResolver>();
            foreach (var resolverManifest in resolversManifests)
            {
                if (!_manifestToResolvers.TryGetValue(resolverManifest, out IReadOnlyList<SdkResolver> newResolvers))
                {
                    lock (_lockObject)
                    {
                        if (!_manifestToResolvers.TryGetValue(resolverManifest, out newResolvers))
                        {
                            // Loading of the needed resolvers.
                            newResolvers = _sdkResolverLoader.LoadResolversFromManifest(resolverManifest, sdkReferenceLocation);
                            _manifestToResolvers[resolverManifest] = newResolvers;
                        }
                    }
                }

                resolvers.AddRange(newResolvers);
            }

            resolvers.Sort((l, r) => l.Priority.CompareTo(r.Priority));
            return resolvers;
        }

        private bool TryResolveSdkUsingSpecifiedResolvers(
            IReadOnlyList<SdkResolver> resolvers,
            int submissionId,
            SdkReference sdk,
            LoggingContext loggingContext,
            ElementLocation sdkReferenceLocation,
            string solutionPath,
            string projectPath,
            bool interactive,
            bool isRunningInVisualStudio,
            out SdkResult sdkResult,
            out IEnumerable<string> errors,
            out IEnumerable<string> warnings)
        {
            List<SdkResult> results = new List<SdkResult>();
            errors = null;
            warnings = null;

            // Loop through resolvers which have already been sorted by priority, returning the first result that was successful
            SdkLogger buildEngineLogger = new SdkLogger(loggingContext);

            foreach (SdkResolver sdkResolver in resolvers)
            {
                SdkResolverContext context = new SdkResolverContext(buildEngineLogger, projectPath, solutionPath, ProjectCollection.Version, interactive, isRunningInVisualStudio)
                {
                    State = GetResolverState(submissionId, sdkResolver)
                };

                SdkResultFactory resultFactory = new SdkResultFactory(sdk);

                SdkResult result = null;

                try
                {
                    MSBuildEventSource.Log.SdkResolverResolveSdkStart();
                    result = (SdkResult)sdkResolver.Resolve(sdk, context, resultFactory);

                    // We have had issues, for example dotnet/msbuild/issues/9537, where the SDK resolver returned null as particular warnings or errors.
                    // Since this can be caused by custom and 3rd party SDK resolvers, we want to log this information to help diagnose the issue.
                    if (result?.Warnings?.Any(s => s is null) == true || result?.Errors?.Any(s => s is null) == true)
                    {
                        loggingContext.LogComment(MessageImportance.Low, "SDKResolverNullMessage", sdkResolver.Name, sdk.ToString());
                    }
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
                finally
                {
                    MSBuildEventSource.Log.SdkResolverResolveSdkStop(sdkResolver.Name, sdk.Name, solutionPath ?? string.Empty, projectPath ?? string.Empty, result?.Path ?? string.Empty, result?.Success ?? false);
                }

                SetResolverState(submissionId, sdkResolver, context.State);

                result ??= (SdkResult)resultFactory.IndicateFailure([ResourceUtilities.FormatResourceStringStripCodeAndKeyword("SDKResolverReturnedNull", sdkResolver.Name)], []);

                if (result.Success)
                {
                    loggingContext.LogComment(MessageImportance.Low, "SucceededToResolveSDK", sdk.ToString(), sdkResolver.Name, result.Path ?? "null", result.Version ?? "null");

                    LogWarnings(loggingContext, sdkReferenceLocation, result.Warnings);

                    if (!IsReferenceSameVersion(sdk, result.Version))
                    {
                        // MSB4241: The SDK reference "{0}" version "{1}" was resolved to version "{2}" instead.  You could be using a different version than expected if you do not update the referenced version to match.
                        loggingContext.LogWarning(null, new BuildEventFileInfo(sdkReferenceLocation), "SdkResultVersionDifferentThanReference", sdk.Name, sdk.Version, result.Version);
                    }

                    // Associate the element location of the resolved SDK reference
                    result.ElementLocation = sdkReferenceLocation;

                    sdkResult = result;
                    return true;
                }
                else if (loggingContext.LoggingService.MinimumRequiredMessageImportance >= MessageImportance.Low)
                {
                    string resultWarnings = result.Warnings?.Any() == true ? string.Join(Environment.NewLine, result.Warnings) : "null";
                    string resultErrors = result.Errors?.Any() == true ? string.Join(Environment.NewLine, result.Errors) : "null";

                    loggingContext.LogComment(MessageImportance.Low, "SDKResolverAttempt", sdkResolver.Name, sdk.ToString(), resultWarnings, resultErrors);
                }

                results.Add(result);
            }

            warnings = results.SelectMany(r => r.Warnings ?? []);
            errors = results.SelectMany(r => r.Errors ?? []);

            sdkResult = new SdkResult(sdk, null, null);
            return false;
        }

        internal virtual void WaitIfTestRequires() { }

        // This is a convenience wrapper that we override for one test so that we don't introduce unnecessary #if DEBUG
        // segments into the production code.
        internal virtual IReadOnlyList<SdkResolverManifest> GetResolverManifests(ElementLocation location) => _sdkResolverLoader.GetResolversManifests(location);

        /// <summary>
        /// Used for unit tests only.  This is currently only called through reflection in Microsoft.Build.Engine.UnitTests.TransientSdkResolution.CallResetForTests
        /// </summary>
        /// <param name="resolverLoader">An <see cref="SdkResolverLoader"/> to use for loading SDK resolvers.</param>
        /// <param name="resolvers">Explicit set of SdkResolvers to use for all SDK resolution.</param>
        internal virtual void InitializeForTests(SdkResolverLoader resolverLoader = null, IReadOnlyList<SdkResolver> resolvers = null)
        {
            if (resolverLoader != null)
            {
                _sdkResolverLoader = resolverLoader;
            }
            else
            {
                _sdkResolverLoader = CachingSdkResolverLoader.Instance;
            }

            List<SdkResolverManifest> specificResolversManifestsRegistry = null;
            List<SdkResolverManifest> generalResolversManifestsRegistry = null;
            _manifestToResolvers = null;

            if (resolvers != null)
            {
                specificResolversManifestsRegistry = new List<SdkResolverManifest>();
                generalResolversManifestsRegistry = new List<SdkResolverManifest>();
                _manifestToResolvers = new Dictionary<SdkResolverManifest, IReadOnlyList<SdkResolver>>();

                SdkResolverManifest sdkResolverManifest = new SdkResolverManifest(DisplayName: "TestResolversManifest", Path: null, ResolvableSdkRegex: null);
                generalResolversManifestsRegistry.Add(sdkResolverManifest);
                _manifestToResolvers[sdkResolverManifest] = resolvers;
                _generalResolversManifestsRegistry = generalResolversManifestsRegistry.AsReadOnly();
                _specificResolversManifestsRegistry = specificResolversManifestsRegistry.AsReadOnly();
            }
        }

        private static void LogWarnings(LoggingContext loggingContext, ElementLocation location, IEnumerable<string> warnings)
        {
            if (warnings == null)
            {
                return;
            }

            foreach (string warning in warnings)
            {
                // Do not fail on returned null messages
                if (!string.IsNullOrWhiteSpace(warning))
                {
                    loggingContext.LogWarningFromText(null, null, null, new BuildEventFileInfo(location), warning);
                }
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

        private void RegisterResolversManifests(ElementLocation location)
        {
            lock (_lockObject)
            {
                if (_specificResolversManifestsRegistry != null && _generalResolversManifestsRegistry != null)
                {
                    return;
                }

                IReadOnlyList<SdkResolverManifest> allResolversManifests = GetResolverManifests(location);
                _manifestToResolvers = new Dictionary<SdkResolverManifest, IReadOnlyList<SdkResolver>>();

                SdkResolverManifest sdkDefaultResolversManifest = null;
#if NET
                if (!ChangeWaves.AreFeaturesEnabled(ChangeWaves.Wave17_10))
#endif
                {
                    // Load and add the manifest for the default resolvers, located directly in this dll.
                    IReadOnlyList<SdkResolver> defaultResolvers = _sdkResolverLoader.GetDefaultResolvers();
                    if (defaultResolvers.Count > 0)
                    {
                        sdkDefaultResolversManifest = new SdkResolverManifest(DisplayName: "DefaultResolversManifest", Path: null, ResolvableSdkRegex: null);
                        _manifestToResolvers[sdkDefaultResolversManifest] = defaultResolvers;
                    }
                }

                var specificResolversManifestsRegistry = new List<SdkResolverManifest>();
                var generalResolversManifestsRegistry = new List<SdkResolverManifest>();

                // Break the list of all resolvers manifests into two parts: manifests with specific and general resolvers.
                // Since the collections are meant to be immutable, we have to only ever assign them when they're complete.
                // Otherwise race can happen, see https://github.com/dotnet/msbuild/issues/7927
                foreach (SdkResolverManifest manifest in allResolversManifests)
                {
                    WaitIfTestRequires();

                    if (manifest.ResolvableSdkRegex == null)
                    {
                        generalResolversManifestsRegistry.Add(manifest);
                    }
                    else
                    {
                        specificResolversManifestsRegistry.Add(manifest);
                    }
                }
                if (sdkDefaultResolversManifest != null)
                {
                    generalResolversManifestsRegistry.Add(sdkDefaultResolversManifest);
                }

                // Until this is set(and this is under lock), the ResolveSdkUsingResolversWithPatternsFirst will always
                // enter if branch leaving to this section.
                // Then it will wait at the lock and return after we release it since the collections we have filled them before releasing the lock.
                // The collections are never modified after this point.
                // So I've made them ReadOnly
                _specificResolversManifestsRegistry = specificResolversManifestsRegistry.AsReadOnly();
                _generalResolversManifestsRegistry = generalResolversManifestsRegistry.AsReadOnly();
            }
        }

        private void SetResolverState(int submissionId, SdkResolver resolver, object state)
        {
            // Do not set state for resolution requests that are not associated with a valid build submission ID
            if (submissionId != BuildEventContext.InvalidSubmissionId)
            {
                ConcurrentDictionary<SdkResolver, object> resolverState = _resolverStateBySubmission.GetOrAdd(
                    submissionId,
                    _ => new ConcurrentDictionary<SdkResolver, object>(
                        NativeMethodsShared.GetLogicalCoreCount(),
                        _specificResolversManifestsRegistry.Count + _generalResolversManifestsRegistry.Count));

                resolverState.AddOrUpdate(resolver, state, (sdkResolver, obj) => state);
            }
        }
    }
}
