// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;

namespace Microsoft.Build.Execution
{
    /// <summary>
    /// Overall results for targets and requests
    /// </summary>
    public enum BuildResultCode
    {
        /// <summary>
        /// The target or request was a complete success.
        /// </summary>
        Success,

        /// <summary>
        /// The target or request failed in some way.
        /// </summary>
        Failure
    }

    /// <summary>
    /// Contains the current results for all of the targets which have produced results for a particular configuration.
    /// </summary>
    public class BuildResult : INodePacket, IBuildResults
    {
        /// <summary>
        /// The submission with which this result is associated.
        /// </summary>
        private int _submissionId;

        /// <summary>
        /// The configuration ID with which this result is associated.
        /// </summary>
        private int _configurationId;

        /// <summary>
        /// The global build request ID for which these results are intended.
        /// </summary>
        private int _globalRequestId;

        /// <summary>
        /// The global build request ID which issued the request leading to this result.
        /// </summary>
        private int _parentGlobalRequestId;

        /// <summary>
        /// The build request ID on the originating node.
        /// </summary>
        private int _nodeRequestId;

        /// <summary>
        /// The first build request to generate results for a configuration will set this so that future
        /// requests may be properly satisfied from the cache.
        /// </summary>
        private List<string> _initialTargets;

        /// <summary>
        /// The first build request to generate results for a configuration will set this so that future
        /// requests may be properly satisfied from the cache.
        /// </summary>
        private List<string> _defaultTargets;

        /// <summary>
        /// The set of results for each target.
        /// </summary>
        private ConcurrentDictionary<string, TargetResult> _resultsByTarget;

        /// <summary>
        /// The request caused a circular dependency in scheduling.
        /// </summary>
        private bool _circularDependency;

        /// <summary>
        /// The exception generated while this request was running, if any.
        /// Note that this can be set if the request itself fails, or if it receives
        /// an exception from a target or task.
        /// </summary>
        private Exception _requestException;

        /// <summary>
        /// The overall result calculated in the constructor.
        /// </summary>
        private bool _baseOverallResult = true;

        /// <summary>
        /// Snapshot of the environment from the configuration this results comes from.
        /// This should only be populated when the configuration for this result is moved between nodes.
        /// </summary>
        private Dictionary<string, string> _savedEnvironmentVariables;

        /// <summary>
        /// Snapshot of the current directory from the configuration this result comes from.
        /// This should only be populated when the configuration for this result is moved between nodes.
        /// </summary>
        private string _savedCurrentDirectory;

        /// <summary>
        /// <see cref="ProjectInstance"/> state after the build. This is only provided if <see cref="BuildRequest.BuildRequestDataFlags"/>
        /// includes <see cref="BuildRequestDataFlags.ProvideProjectStateAfterBuild"/> or
        /// <see cref="BuildRequestDataFlags.ProvideSubsetOfStateAfterBuild"/> for the build request which this object is a result of,
        /// and will be <c>null</c> otherwise. Where available, it may be a non buildable-dummy object, and should only
        /// be used to retrieve <see cref="ProjectInstance.Properties"/>, <see cref="ProjectInstance.GlobalProperties"/> and
        /// <see cref="ProjectInstance.Items"/> from it. No other operation is guaranteed to be supported.
        /// </summary>
        private ProjectInstance _projectStateAfterBuild;

        private string _schedulerInducedError;

        /// <summary>
        /// Constructor for serialization.
        /// </summary>
        public BuildResult()
        {
        }

        /// <summary>
        /// Constructor creates an empty build result
        /// </summary>
        /// <param name="request">The build request to which these results should be associated.</param>
        internal BuildResult(BuildRequest request)
            : this(request, null)
        {
        }

        /// <summary>
        /// Constructs a build result with an exception
        /// </summary>
        /// <param name="request">The build request to which these results should be associated.</param>
        /// <param name="exception">The exception, if any.</param>
        internal BuildResult(BuildRequest request, Exception exception)
            : this(request, null, exception)
        {
        }

        /// <summary>
        /// Constructor creates a build result indicating a circular dependency was created.
        /// </summary>
        /// <param name="request">The build request to which these results should be associated.</param>
        /// <param name="circularDependency">Set to true if a circular dependency was detected.</param>
        internal BuildResult(BuildRequest request, bool circularDependency)
            : this(request, null)
        {
            _circularDependency = circularDependency;
        }

        /// <summary>
        /// Constructs a new build result based on existing results, but filtered by a specified set of target names
        /// </summary>
        /// <param name="existingResults">The existing results.</param>
        /// <param name="targetNames">The target names whose results we will take from the existing results, if they exist.</param>
        internal BuildResult(BuildResult existingResults, string[] targetNames)
        {
            _submissionId = existingResults._submissionId;
            _configurationId = existingResults._configurationId;
            _globalRequestId = existingResults._globalRequestId;
            _parentGlobalRequestId = existingResults._parentGlobalRequestId;
            _nodeRequestId = existingResults._nodeRequestId;
            _requestException = existingResults._requestException;
            _resultsByTarget = CreateTargetResultDictionaryWithContents(existingResults, targetNames);
            _baseOverallResult = existingResults.OverallResult == BuildResultCode.Success;

            _circularDependency = existingResults._circularDependency;
        }

        /// <summary>
        /// Constructs a new build result with existing results, but associated with the specified request.
        /// </summary>
        /// <param name="request">The build request with which these results should be associated.</param>
        /// <param name="existingResults">The existing results, if any.</param>
        /// <param name="exception">The exception, if any</param>
        internal BuildResult(BuildRequest request, BuildResult existingResults, Exception exception)
            : this(request, existingResults, null, exception)
        {
        }

        /// <summary>
        /// Constructs a new build result with existing results, but associated with the specified request.
        /// </summary>
        /// <param name="request">The build request with which these results should be associated.</param>
        /// <param name="existingResults">The existing results, if any.</param>
        /// <param name="targetNames">The list of target names that are the subset of results that should be returned.</param>
        /// <param name="exception">The exception, if any</param>
        internal BuildResult(BuildRequest request, BuildResult existingResults, string[] targetNames, Exception exception)
        {
            ErrorUtilities.VerifyThrow(request != null, "Must specify a request.");
            _submissionId = request.SubmissionId;
            _configurationId = request.ConfigurationId;
            _globalRequestId = request.GlobalRequestId;
            _parentGlobalRequestId = request.ParentGlobalRequestId;
            _nodeRequestId = request.NodeRequestId;
            _circularDependency = false;
            _baseOverallResult = true;

            if (existingResults == null)
            {
                _requestException = exception;
                _resultsByTarget = CreateTargetResultDictionary(0);
            }
            else
            {
                _requestException = exception ?? existingResults._requestException;
                _resultsByTarget = targetNames == null ? existingResults._resultsByTarget : CreateTargetResultDictionaryWithContents(existingResults, targetNames);
            }
        }

        /// <summary>
        /// Constructor which allows reporting results for a different nodeRequestId
        /// </summary>
        internal BuildResult(BuildResult result, int nodeRequestId)
        {
            _configurationId = result._configurationId;
            _globalRequestId = result._globalRequestId;
            _parentGlobalRequestId = result._parentGlobalRequestId;
            _nodeRequestId = nodeRequestId;
            _requestException = result._requestException;
            _resultsByTarget = result._resultsByTarget;
            _circularDependency = result._circularDependency;
            _initialTargets = result._initialTargets;
            _defaultTargets = result._defaultTargets;
            _baseOverallResult = result.OverallResult == BuildResultCode.Success;
        }

        internal BuildResult(BuildResult result, int submissionId, int configurationId, int requestId, int parentRequestId, int nodeRequestId)
        {
            _submissionId = submissionId;
            _configurationId = configurationId;
            _globalRequestId = requestId;
            _parentGlobalRequestId = parentRequestId;
            _nodeRequestId = nodeRequestId;

            _requestException = result._requestException;
            _resultsByTarget = result._resultsByTarget;
            _circularDependency = result._circularDependency;
            _initialTargets = result._initialTargets;
            _defaultTargets = result._defaultTargets;
            _baseOverallResult = result.OverallResult == BuildResultCode.Success;
        }

        /// <summary>
        /// Constructor for deserialization
        /// </summary>
        private BuildResult(ITranslator translator)
        {
            ((ITranslatable)this).Translate(translator);
        }

        /// <summary>
        /// Returns the submission id.
        /// </summary>
        public int SubmissionId
        {
            [DebuggerStepThrough]
            get
            { return _submissionId; }
        }

        /// <summary>
        /// Returns the configuration ID for this result.
        /// </summary>
        public int ConfigurationId
        {
            [DebuggerStepThrough]
            get
            { return _configurationId; }
        }

        /// <summary>
        /// Returns the build request id for which this result was generated
        /// </summary>
        public int GlobalRequestId
        {
            [DebuggerStepThrough]
            get
            { return _globalRequestId; }
        }

        /// <summary>
        /// Returns the build request id for the parent of the request for which this result was generated
        /// </summary>
        public int ParentGlobalRequestId
        {
            [DebuggerStepThrough]
            get
            { return _parentGlobalRequestId; }
        }

        /// <summary>
        /// Returns the node build request id for which this result was generated
        /// </summary>
        public int NodeRequestId
        {
            [DebuggerStepThrough]
            get
            { return _nodeRequestId; }
        }

        /// <summary>
        /// Returns the exception generated while this result was run, if any. 
        /// </summary>
        public Exception Exception
        {
            [DebuggerStepThrough]
            get
            { return _requestException; }

            [DebuggerStepThrough]
            internal set
            { _requestException = value; }
        }

        /// <summary>
        /// Returns a flag indicating if a circular dependency was detected.
        /// </summary>
        public bool CircularDependency
        {
            [DebuggerStepThrough]
            get
            { return _circularDependency; }
        }

        /// <summary>
        /// Returns the overall result for this result set.
        /// </summary>
        public BuildResultCode OverallResult
        {
            get
            {
                if (null != _requestException || _circularDependency || !_baseOverallResult)
                {
                    return BuildResultCode.Failure;
                }

                foreach (KeyValuePair<string, TargetResult> result in _resultsByTarget)
                {
                    if ((result.Value.ResultCode == TargetResultCode.Failure && !result.Value.TargetFailureDoesntCauseBuildFailure)
                        || result.Value.AfterTargetsHaveFailed)
                    {
                        return BuildResultCode.Failure;
                    }
                }

                return BuildResultCode.Success;
            }
        }

        /// <summary>
        /// Returns an enumerator for all target results in this build result
        /// </summary>
        public IDictionary<string, TargetResult> ResultsByTarget
        {
            [DebuggerStepThrough]
            get
            { return _resultsByTarget; }
        }

        /// <summary>
        /// <see cref="ProjectInstance"/> state after the build. In general, it may be a non buildable-dummy object, and should only
        /// be used to retrieve <see cref="ProjectInstance.Properties"/>, <see cref="ProjectInstance.GlobalProperties"/> and
        /// <see cref="ProjectInstance.Items"/> from it. Any other operation is not guaranteed to be supported.
        /// </summary>
        public ProjectInstance ProjectStateAfterBuild
        {
            get => _projectStateAfterBuild;
            set => _projectStateAfterBuild = value;
        }

        /// <summary>
        /// Returns the node packet type.
        /// </summary>
        NodePacketType INodePacket.Type
        {
            [DebuggerStepThrough]
            get
            { return NodePacketType.BuildResult; }
        }

        /// <summary>
        /// Holds a snapshot of the environment at the time we blocked.
        /// </summary>
        Dictionary<string, string> IBuildResults.SavedEnvironmentVariables
        {
            get => _savedEnvironmentVariables;

            set => _savedEnvironmentVariables = value;
        }

        /// <summary>
        /// Holds a snapshot of the current working directory at the time we blocked.
        /// </summary>
        string IBuildResults.SavedCurrentDirectory
        {
            get => _savedCurrentDirectory;

            set => _savedCurrentDirectory = value;
        }

        /// <summary>
        /// Returns the initial targets for the configuration which requested these results.
        /// </summary>
        internal List<string> InitialTargets
        {
            [DebuggerStepThrough]
            get
            { return _initialTargets; }

            [DebuggerStepThrough]
            set
            { _initialTargets = value; }
        }

        /// <summary>
        /// Returns the default targets for the configuration which requested these results.
        /// </summary>
        internal List<string> DefaultTargets
        {
            [DebuggerStepThrough]
            get
            { return _defaultTargets; }

            [DebuggerStepThrough]
            set
            { _defaultTargets = value; }
        }

        /// <summary>
        /// Container used to transport errors from the scheduler (issued while computing a build result)
        /// to the TaskHost that has the proper logging context (project id, target id, task id, file location)
        /// </summary>
        internal string SchedulerInducedError
        {
            get => _schedulerInducedError;
            set => _schedulerInducedError = value;
        }

        /// <summary>
        /// Indexer which sets or returns results for the specified target
        /// </summary>
        /// <param name="target">The target</param>
        /// <returns>The results for the specified target</returns>
        /// <exception>KeyNotFoundException is returned if the specified target doesn't exist when reading this property.</exception>
        /// <exception>ArgumentException is returned if the specified target already has results.</exception>
        public ITargetResult this[string target]
        {
            [DebuggerStepThrough]
            get
            { return _resultsByTarget[target]; }
        }

        /// <summary>
        /// Adds the results for the specified target to this result collection.
        /// </summary>
        /// <param name="target">The target to which these results apply.</param>
        /// <param name="result">The results for the target.</param>
        public void AddResultsForTarget(string target, TargetResult result)
        {
            ErrorUtilities.VerifyThrowArgumentNull(target, nameof(target));
            ErrorUtilities.VerifyThrowArgumentNull(result, nameof(result));
            if (_resultsByTarget.ContainsKey(target))
            {
                ErrorUtilities.VerifyThrow(_resultsByTarget[target].ResultCode == TargetResultCode.Skipped, "Items already exist for target {0}.", target);
            }

            _resultsByTarget[target] = result;
        }

        /// <summary>
        /// Merges the specified results with the results contained herein.
        /// </summary>
        /// <param name="results">The results to merge in.</param>
        public void MergeResults(BuildResult results)
        {
            ErrorUtilities.VerifyThrowArgumentNull(results, nameof(results));
            ErrorUtilities.VerifyThrow(results.ConfigurationId == ConfigurationId, "Result configurations don't match");

            // If we are merging with ourself or with a shallow clone, do nothing.
            if (ReferenceEquals(this, results) || ReferenceEquals(_resultsByTarget, results._resultsByTarget))
            {
                return;
            }

            // Merge in the results
            foreach (KeyValuePair<string, TargetResult> targetResult in results._resultsByTarget)
            {
                // NOTE: I believe that because we only allow results for a given target to be produced and cached once for a given configuration,
                // we can never receive conflicting results for that target, since the cache and build request manager would always return the
                // cached results after the first time the target is built.  As such, we can allow "duplicates" to be merged in because there is
                // no change.  If, however, this turns out not to be the case, we need to re-evaluate this merging and possibly re-enable the
                // assertion below.
                // ErrorUtilities.VerifyThrow(!HasResultsForTarget(targetResult.Key), "Results already exist");

                // Copy the new results in.
                _resultsByTarget[targetResult.Key] = targetResult.Value;
            }

            // If there is an exception and we did not previously have one, add it in.
            _requestException = _requestException ?? results.Exception;
        }

        /// <summary>
        /// Determines if there are any results for the specified target.
        /// </summary>
        /// <param name="target">The target for which results are desired.</param>
        /// <returns>True if results exist, false otherwise.</returns>
        public bool HasResultsForTarget(string target)
        {
            return _resultsByTarget.ContainsKey(target);
        }

        #region INodePacket Members

        /// <summary>
        /// Reads or writes the packet to the serializer.
        /// </summary>
        void ITranslatable.Translate(ITranslator translator)
        {
            translator.Translate(ref _submissionId);
            translator.Translate(ref _configurationId);
            translator.Translate(ref _globalRequestId);
            translator.Translate(ref _parentGlobalRequestId);
            translator.Translate(ref _nodeRequestId);
            translator.Translate(ref _initialTargets);
            translator.Translate(ref _defaultTargets);
            translator.Translate(ref _circularDependency);
            translator.TranslateException(ref _requestException);
            translator.TranslateDictionary(ref _resultsByTarget, TargetResult.FactoryForDeserialization, CreateTargetResultDictionary);
            translator.Translate(ref _baseOverallResult);
            translator.Translate(ref _projectStateAfterBuild, ProjectInstance.FactoryForDeserialization);
            translator.Translate(ref _savedCurrentDirectory);
            translator.Translate(ref _schedulerInducedError);
            translator.TranslateDictionary(ref _savedEnvironmentVariables, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Factory for serialization
        /// </summary>
        internal static BuildResult FactoryForDeserialization(ITranslator translator)
        {
            return new BuildResult(translator);
        }

        #endregion

        /// <summary>
        /// Caches all of the targets results we can.
        /// </summary>
        internal void CacheIfPossible()
        {
            foreach (string target in _resultsByTarget.Keys)
            {
                _resultsByTarget[target].CacheItems(ConfigurationId, target);
            }
        }

        /// <summary>
        /// Clear cached files from disk.
        /// </summary>
        internal void ClearCachedFiles()
        {
            string resultsDirectory = TargetResult.GetCacheDirectory(_configurationId, "None" /*Does not matter because we just need the directory name not the file*/);
            if (FileSystems.Default.DirectoryExists(resultsDirectory))
            {
                FileUtilities.DeleteDirectoryNoThrow(resultsDirectory, true /*recursive*/);
            }
        }

        /// <summary>
        /// Clones the build result (the resultsByTarget field is only a shallow copy).
        /// </summary>
        internal BuildResult Clone()
        {
            BuildResult result = new BuildResult
            {
                _submissionId = _submissionId,
                _configurationId = _configurationId,
                _globalRequestId = _globalRequestId,
                _parentGlobalRequestId = _parentGlobalRequestId,
                _nodeRequestId = _nodeRequestId,
                _requestException = _requestException,
                _resultsByTarget = new ConcurrentDictionary<string, TargetResult>(
                    _resultsByTarget,
                    StringComparer.OrdinalIgnoreCase),
                _baseOverallResult = OverallResult == BuildResultCode.Success,
                _circularDependency = _circularDependency
            };

            return result;
        }

        /// <summary>
        /// Sets the overall result.
        /// </summary>
        /// <param name="overallResult"><code>true</code> if the result is success, otherwise <code>false</code>.</param>
        internal void SetOverallResult(bool overallResult)
        {
            _baseOverallResult = false;
        }

        /// <summary>
        /// Creates the target result dictionary.
        /// </summary>
        private static ConcurrentDictionary<string, TargetResult> CreateTargetResultDictionary(int capacity)
        {
            return new ConcurrentDictionary<string, TargetResult>(1, capacity, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Creates the target result dictionary and populates it with however many target results are 
        /// available given the list of targets passed. 
        /// </summary>
        private static ConcurrentDictionary<string, TargetResult> CreateTargetResultDictionaryWithContents(BuildResult existingResults, string[] targetNames)
        {
            ConcurrentDictionary<string, TargetResult> resultsByTarget = CreateTargetResultDictionary(targetNames.Length);

            foreach (string target in targetNames)
            {
                if (existingResults.ResultsByTarget.TryGetValue(target, out TargetResult targetResult))
                {
                    resultsByTarget[target] = targetResult;
                }
            }

            return resultsByTarget;
        }
    }
}
