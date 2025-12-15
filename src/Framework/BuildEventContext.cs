// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Will provide location information for an event, this is especially
    /// needed in a multi processor environment.
    /// 
    /// BuildEventContext objects should be created using the static CreateInitial method
    /// for the root context, then using the fluent WithXxx methods to create derived contexts
    /// that preserve all ID properties while updating specific ones.
    /// </summary>
    [Serializable]
    public class BuildEventContext
    {
        #region Data

        /// <summary>
        /// Node event was in
        /// </summary>
        private readonly int _nodeId;

        /// <summary>
        /// Target event was in
        /// </summary>
        private readonly int _targetId;

        /// <summary>
        /// The node-unique project request context the event was in
        /// </summary>
        private readonly int _projectContextId;

        /// <summary>
        /// Id of the task the event was caused from
        /// </summary>
        private readonly int _taskId;

        /// <summary>
        /// The id of the project instance to which this event refers.
        /// </summary>
        private readonly int _projectInstanceId;

        /// <summary>
        /// The id of the submission.
        /// </summary>
        private readonly int _submissionId;

        /// <summary>
        /// The id of the evaluation
        /// </summary>
        private readonly int _evaluationId;

        #endregion

        #region Constructor

        /// <summary>
        /// Constructs a BuildEventContext with all parameters specified.
        /// This constructor should only be used internally for serialization/deserialization
        /// and by the fluent WithXxx methods. External code should use CreateInitial() and fluent methods.
        /// </summary>
        internal BuildEventContext(
            int submissionId,
            int nodeId,
            int evaluationId,
            int projectInstanceId,
            int projectContextId,
            int targetId,
            int taskId)
        {
            _submissionId = submissionId;
            _nodeId = nodeId;
            _evaluationId = evaluationId;
            _targetId = targetId;
            _projectContextId = projectContextId;
            _taskId = taskId;
            _projectInstanceId = projectInstanceId;
        }

        /// <summary>
        /// Creates an initial BuildEventContext for the beginning of a build.
        /// </summary>
        /// <param name="submissionId">The submission ID</param>
        /// <param name="nodeId">The node ID</param>
        /// <returns>A new BuildEventContext with the specified submission and node ID</returns>
        public static BuildEventContext CreateInitial(int submissionId, int nodeId)
        {
            return new BuildEventContext(
                submissionId,
                nodeId,
                InvalidEvaluationId,
                InvalidProjectInstanceId,
                InvalidProjectContextId,
                InvalidTargetId,
                InvalidTaskId);
        }
        #endregion

        internal BuildEventContext WithInstanceIdAndContextId(int projectInstanceId, int projectContextId)
        {
            return new BuildEventContext(_submissionId, _nodeId, _evaluationId, projectInstanceId, projectContextId,
                _targetId, _taskId);
        }

        internal BuildEventContext WithInstanceIdAndContextId(BuildEventContext other)
        {
            return WithInstanceIdAndContextId(other.ProjectInstanceId, other.ProjectContextId);
        }

        /// <summary>
        /// Creates a new BuildEventContext with the specified submission ID, preserving all other IDs.
        /// </summary>
        /// <param name="submissionId">The new submission ID</param>
        /// <returns>A new BuildEventContext with the updated submission ID</returns>
        public BuildEventContext WithSubmissionId(int submissionId)
        {
            return new BuildEventContext(submissionId, _nodeId, _evaluationId, _projectInstanceId, _projectContextId, _targetId, _taskId);
        }

        /// <summary>
        /// Creates a new BuildEventContext with the specified node ID, preserving all other IDs.
        /// </summary>
        /// <param name="nodeId">The new node ID</param>
        /// <returns>A new BuildEventContext with the updated node ID</returns>
        public BuildEventContext WithNodeId(int nodeId)
        {
            return new BuildEventContext(_submissionId, nodeId, _evaluationId, _projectInstanceId, _projectContextId, _targetId, _taskId);
        }

        /// <summary>
        /// Creates a new BuildEventContext with the specified evaluation ID, preserving all other IDs.
        /// </summary>
        /// <param name="evaluationId">The new evaluation ID</param>
        /// <returns>A new BuildEventContext with the updated evaluation ID</returns>
        public BuildEventContext WithEvaluationId(int evaluationId)
        {
            return new BuildEventContext(_submissionId, _nodeId, evaluationId, _projectInstanceId, _projectContextId, _targetId, _taskId);
        }

        /// <summary>
        /// Creates a new BuildEventContext with the specified project instance ID, preserving all other IDs.
        /// </summary>
        /// <param name="projectInstanceId">The new project instance ID</param>
        /// <returns>A new BuildEventContext with the updated project instance ID</returns>
        public BuildEventContext WithProjectInstanceId(int projectInstanceId)
        {
            return new BuildEventContext(_submissionId, _nodeId, _evaluationId, projectInstanceId, _projectContextId, _targetId, _taskId);
        }

        /// <summary>
        /// Creates a new BuildEventContext with the specified project context ID, preserving all other IDs.
        /// </summary>
        /// <param name="projectContextId">The new project context ID</param>
        /// <returns>A new BuildEventContext with the updated project context ID</returns>
        public BuildEventContext WithProjectContextId(int projectContextId)
        {
            return new BuildEventContext(_submissionId, _nodeId, _evaluationId, _projectInstanceId, projectContextId, _targetId, _taskId);
        }

        /// <summary>
        /// Creates a new BuildEventContext with the specified target ID, preserving all other IDs.
        /// </summary>
        /// <param name="targetId">The new target ID</param>
        /// <returns>A new BuildEventContext with the updated target ID</returns>
        public BuildEventContext WithTargetId(int targetId)
        {
            return new BuildEventContext(_submissionId, _nodeId, _evaluationId, _projectInstanceId, _projectContextId, targetId, _taskId);
        }

        /// <summary>
        /// Creates a new BuildEventContext with the specified task ID, preserving all other IDs.
        /// </summary>
        /// <param name="taskId">The new task ID</param>
        /// <returns>A new BuildEventContext with the updated task ID</returns>
        public BuildEventContext WithTaskId(int taskId)
        {
            return new BuildEventContext(_submissionId, _nodeId, _evaluationId, _projectInstanceId, _projectContextId, _targetId, taskId);
        }

        #region Properties

        /// <summary>
        /// Returns a default invalid BuildEventContext
        /// </summary>
        public static BuildEventContext Invalid { get; } = new(InvalidSubmissionId, InvalidNodeId, InvalidEvaluationId, InvalidProjectInstanceId, InvalidProjectContextId, InvalidTargetId, InvalidTaskId);

        /// <summary>
        /// Retrieves the Evaluation id.
        /// </summary>
        public int EvaluationId => _evaluationId;

        /// <summary>
        /// NodeId where event took place
        /// </summary>
        public int NodeId => _nodeId;

        /// <summary>
        /// Id of the target the event was in when the event was fired
        /// </summary>
        public int TargetId => _targetId;

        /// <summary>
        /// Retrieves the Project Context id.
        /// </summary>
        public int ProjectContextId => _projectContextId;

        /// <summary>
        /// Retrieves the task id.
        /// </summary>
        public int TaskId => _taskId;

        /// <summary>
        /// Retrieves the project instance id.
        /// </summary>
        public int ProjectInstanceId => _projectInstanceId;

        /// <summary>
        /// Retrieves the Submission id.
        /// </summary>
        public int SubmissionId => _submissionId;

        /// <summary>
        /// Retrieves the BuildRequest id.  Note that this is not the same as the global request id on a BuildRequest or BuildResult.
        /// </summary>
        public long BuildRequestId => GetHashCode();

        #endregion

        #region Constants
        /// <summary>
        /// Indicates an invalid project context identifier.
        /// </summary>
        public const int InvalidProjectContextId = -2;
        /// <summary>
        /// Indicates an invalid task identifier.
        /// </summary>
        public const int InvalidTaskId = -1;
        /// <summary>
        /// Indicates an invalid target identifier.
        /// </summary>
        public const int InvalidTargetId = -1;
        /// <summary>
        /// Indicates an invalid node identifier.
        /// </summary>
        public const int InvalidNodeId = -2;
        /// <summary>
        /// Indicates an invalid project instance identifier.
        /// </summary>
        public const int InvalidProjectInstanceId = -1;
        /// <summary>
        /// Indicates an invalid submission identifier.
        /// </summary>
        public const int InvalidSubmissionId = -1;
        /// <summary>
        /// Indicates an invalid evaluation identifier.
        /// </summary>
        public const int InvalidEvaluationId = -1;

        #endregion

        #region Equals

        /// <summary>
        /// Retrieves a hash code for this BuildEventContext.
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            var hash = 17;
            // submission ID does not contribute to equality
            // hash = hash * 31 + _submissionId;
            hash = (hash * 31) + _nodeId;
            hash = (hash * 31) + _evaluationId;
            hash = (hash * 31) + _targetId;
            hash = (hash * 31) + _projectContextId;
            hash = (hash * 31) + _taskId;
            hash = (hash * 31) + _projectInstanceId;

            return hash;
        }

        /// <summary>
        /// Compare a BuildEventContext with this BuildEventContext.
        /// A build event context is compared in the following way.
        ///
        /// 1. If the object references are the same the contexts are equivalent
        /// 2. If the object type is the same and the Id values in the context are the same, the contexts are equivalent
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object? obj)
        {
            // If the references are the same no need to do any more comparing
            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj is null)
            {
                return false;
            }

            // The types do not match, they cannot be the same
            if (GetType() != obj.GetType())
            {
                return false;
            }

            return InternalEquals((BuildEventContext)obj);
        }
        /// <summary>
        /// Override == so the  equals comparison using this operator will be the same as
        /// .Equals
        /// </summary>
        /// <param name="left">Left hand side operand</param>
        /// <param name="right">Right hand side operand</param>
        /// <returns>True if the object values are identical, false if they are not identical</returns>
        public static bool operator ==(BuildEventContext? left, BuildEventContext? right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left is null)
            {
                return false;
            }

            return left.Equals(right);
        }

        /// <summary>
        /// Override != so the  equals comparison using this operator will be the same as
        ///  ! Equals
        /// </summary>
        /// <param name="left">Left hand side operand</param>
        /// <param name="right">Right hand side operand</param>
        /// <returns>True if the object values are not identical, false if they are identical</returns>
        public static bool operator !=(BuildEventContext? left, BuildEventContext? right)
        {
            return !(left == right);
        }

        /// <summary>
        /// Verify the fields are identical
        /// </summary>
        /// <param name="buildEventContext">BuildEventContext to compare to this instance</param>
        /// <returns>True if the value fields are the same, false if otherwise</returns>
        private bool InternalEquals(BuildEventContext buildEventContext)
        {
            return _nodeId == buildEventContext.NodeId
                   && _projectContextId == buildEventContext.ProjectContextId
                   && _targetId == buildEventContext.TargetId
                   && _taskId == buildEventContext.TaskId
                   && _evaluationId == buildEventContext._evaluationId
                   && _projectInstanceId == buildEventContext._projectInstanceId;
        }
        #endregion

        public override string ToString()
        {
            return $"Node={NodeId} Submission={SubmissionId} ProjectContext={ProjectContextId} ProjectInstance={ProjectInstanceId} Eval={EvaluationId} Target={TargetId} Task={TaskId}";
        }
    }
}
