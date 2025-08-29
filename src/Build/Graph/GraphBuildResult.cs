﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;

#nullable disable

namespace Microsoft.Build.Graph
{
    public sealed class GraphBuildResult
    {
        /// <summary>
        /// Constructor creates a build result with results for each graph node.
        /// </summary>
        /// <param name="submissionId">The id of the build submission.</param>
        /// <param name="resultsByNode">The set of results for each graph node.</param>
        internal GraphBuildResult(int submissionId, IReadOnlyDictionary<ProjectGraphNode, BuildResult> resultsByNode)
        {
            SubmissionId = submissionId;
            ResultsByNode = resultsByNode;
        }

        /// <summary>
        /// Constructs a graph build result with an exception
        /// </summary>
        /// <param name="submissionId">The id of the build submission.</param>
        /// <param name="exception">The exception, if any.</param>
        internal GraphBuildResult(int submissionId, Exception exception)
        {
            SubmissionId = submissionId;
            Exception = exception;
        }

        /// <summary>
        /// Returns the submission id.
        /// </summary>
        public int SubmissionId { get; }

        /// <summary>
        /// Returns a flag indicating if a circular dependency was detected.
        /// </summary>
        public bool CircularDependency => Exception is CircularDependencyException;

        /// <summary>
        /// Returns the exception generated while this result was run, if any.
        /// </summary>
        public Exception Exception { get; internal set; }

        /// <summary>
        /// Returns the overall result for this result set.
        /// </summary>
        public BuildResultCode OverallResult
        {
            get
            {
                if (Exception != null)
                {
                    return BuildResultCode.Failure;
                }

                foreach (KeyValuePair<ProjectGraphNode, BuildResult> result in ResultsByNode)
                {
                    if (result.Value.OverallResult == BuildResultCode.Failure)
                    {
                        return BuildResultCode.Failure;
                    }
                }

                return BuildResultCode.Success;
            }
        }

        /// <summary>
        /// Returns an enumerator for all build results in this graph build result
        /// </summary>
        public IReadOnlyDictionary<ProjectGraphNode, BuildResult> ResultsByNode { get; }

        /// <summary>
        /// Indexer which sets or returns results for the specified node
        /// </summary>
        /// <param name="node">The node</param>
        /// <returns>The results for the specified node</returns>
        /// <exception>KeyNotFoundException is returned if the specified node doesn't exist when reading this property.</exception>
        public BuildResult this[ProjectGraphNode node] => ResultsByNode[node];
    }
}
