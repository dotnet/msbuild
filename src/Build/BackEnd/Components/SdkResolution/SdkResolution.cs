// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    ///     Component responsible for resolving an SDK to a file path. Loads and coordinates
    ///     with <see cref="SdkResolver" /> plug-ins.
    /// </summary>
    internal class SdkResolution
    {
        private readonly object _lockObject = new object();
        private readonly SdkResolverLoader _sdkResolverLoader;
        private IList<SdkResolver> _resolvers;

        /// <summary>
        ///     Create an instance with a specified resolver assembly loading strategy. Used
        ///     for testing purposes.
        /// </summary>
        /// <param name="sdkResolverLoader">Resolver loading strategy.</param>
        internal SdkResolution(SdkResolverLoader sdkResolverLoader)
        {
            _sdkResolverLoader = sdkResolverLoader;
        }

        internal static SdkResolution Instance { get; } = new SdkResolution(new SdkResolverLoader());

        /// <summary>
        ///     Get path on disk to the referenced SDK.
        /// </summary>
        /// <param name="sdk">SDK referenced by the Project.</param>
        /// <param name="loggingContext">The logging service</param>
        /// <param name="sdkReferenceLocation">Location of the element within the project which referenced the SDK.</param>
        /// <param name="solutionPath">Path to the solution if known.</param>
        /// <param name="projectPath">Path to the project being built.</param>
        /// <returns>Path to the root of the referenced SDK.</returns>
        internal string GetSdkPath(SdkReference sdk, LoggingContext loggingContext,
            ElementLocation sdkReferenceLocation, string solutionPath, string projectPath)
        {
            ErrorUtilities.VerifyThrowInternalNull(sdk, nameof(sdk));
            ErrorUtilities.VerifyThrowInternalNull(loggingContext, nameof(loggingContext));
            ErrorUtilities.VerifyThrowInternalNull(sdkReferenceLocation, nameof(sdkReferenceLocation));

            if (_resolvers == null) Initialize(loggingContext, sdkReferenceLocation);

            var results = new List<SdkResultImpl>();

            try
            {
                var buildEngineLogger = new SdkLoggerImpl(loggingContext);
                foreach (var sdkResolver in _resolvers)
                {
                    var context = new SdkResolverContextImpl(buildEngineLogger, projectPath, solutionPath, ProjectCollection.Version);
                    var resultFactory = new SdkResultFactoryImpl(sdk);
                    try
                    {
                        var result = (SdkResultImpl)sdkResolver.Resolve(sdk, context, resultFactory);
                        if (result != null && result.Success)
                        {
                            LogWarnings(loggingContext, sdkReferenceLocation, result);
                            return result.Path;
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

            foreach (var result in results)
            {
                LogWarnings(loggingContext, sdkReferenceLocation, result);

                if (result.Errors != null)
                {
                    foreach (var error in result.Errors)
                    {
                        loggingContext.LogErrorFromText(subcategoryResourceName: null, errorCode: null,
                            helpKeyword: null, file: new BuildEventFileInfo(sdkReferenceLocation), message: error);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Used for unit tests only.
        /// </summary>
        /// <param name="resolvers">Explicit set of SdkResolvers to use for all SDK resolution.</param>
        internal void InitializeForTests(IList<SdkResolver> resolvers)
        {
            _resolvers = resolvers;
        }

        private void Initialize(LoggingContext loggingContext, ElementLocation location)
        {
            lock (_lockObject)
            {
                if (_resolvers != null) return;
                _resolvers = _sdkResolverLoader.LoadResolvers(loggingContext, location);
            }
        }

        private static void LogWarnings(LoggingContext loggingContext, ElementLocation location,
            SdkResultImpl result)
        {
            if (result.Warnings == null) return;

            foreach (var warning in result.Warnings)
                loggingContext.LogWarningFromText(null, null, null, new BuildEventFileInfo(location), warning);
        }

        private class SdkLoggerImpl : SdkLogger
        {
            private readonly LoggingContext _loggingContext;

            public SdkLoggerImpl(LoggingContext loggingContext)
            {
                _loggingContext = loggingContext;
            }

            public override void LogMessage(string message, MessageImportance messageImportance = MessageImportance.Low)
            {
                _loggingContext.LogCommentFromText(messageImportance, message);
            }
        }

        private class SdkResultImpl : SdkResult
        {
            public SdkResultImpl(SdkReference sdkReference, IEnumerable<string> errors, IEnumerable<string> warnings)
            {
                Success = false;
                Sdk = sdkReference;
                Errors = errors;
                Warnings = warnings;
            }

            public SdkResultImpl(SdkReference sdkReference, string path, string version, IEnumerable<string> warnings)
            {
                Success = true;
                Sdk = sdkReference;
                Path = path;
                Version = version;
                Warnings = warnings;
            }

            public SdkReference Sdk { get; }

            public string Path { get; }

            public string Version { get; }

            public IEnumerable<string> Errors { get; }

            public IEnumerable<string> Warnings { get; }
        }

        private class SdkResultFactoryImpl : SdkResultFactory
        {
            private readonly SdkReference _sdkReference;

            internal SdkResultFactoryImpl(SdkReference sdkReference)
            {
                _sdkReference = sdkReference;
            }

            public override SdkResult IndicateSuccess(string path, string version, IEnumerable<string> warnings = null)
            {
                return new SdkResultImpl(_sdkReference, path, version, warnings);
            }

            public override SdkResult IndicateFailure(IEnumerable<string> errors, IEnumerable<string> warnings = null)
            {
                return new SdkResultImpl(_sdkReference, errors, warnings);
            }
        }

        private sealed class SdkResolverContextImpl : SdkResolverContext
        {
            public SdkResolverContextImpl(SdkLogger logger, string projectFilePath, string solutionPath, Version msBuildVersion)
            {
                Logger = logger;
                ProjectFilePath = projectFilePath;
                SolutionFilePath = solutionPath;
                MSBuildVersion = msBuildVersion;
            }
        }
    }
}
