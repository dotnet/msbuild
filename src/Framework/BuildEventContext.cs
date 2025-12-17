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

        #region Builders

        /// <summary>
        /// Creates an initial BuildEventContext for the beginning of a build.
        /// Uses the efficient builder pattern to minimize allocations.
        /// </summary>
        /// <param name="submissionId">The submission ID</param>
        /// <param name="nodeId">The node ID</param>
        /// <returns>A new BuildEventContext with the specified submission and node ID</returns>
        public static BuildEventContextBuilder CreateInitial(int submissionId, int nodeId) => new BuildEventContextBuilder().WithSubmissionId(submissionId).WithNodeId(nodeId);

        /// <summary>
        /// Creates a new builder with the specified submission ID, preserving all other IDs.
        /// Returns a builder to enable efficient chaining without intermediate allocations.
        /// Call Build() to create the final BuildEventContext.
        /// </summary>
        /// <param name="submissionId">The new submission ID</param>
        /// <returns>A builder with the updated submission ID</returns>
        public BuildEventContextBuilder WithSubmissionId(int submissionId) => Builder(this).WithSubmissionId(submissionId);

        /// <summary>
        /// Creates a new builder with the specified node ID, preserving all other IDs.
        /// Returns a builder to enable efficient chaining without intermediate allocations.
        /// Call Build() to create the final BuildEventContext.
        /// </summary>
        /// <param name="nodeId">The new node ID</param>
        /// <returns>A builder with the updated node ID</returns>
        public BuildEventContextBuilder WithNodeId(int nodeId) => Builder(this).WithNodeId(nodeId);

        /// <summary>
        /// Creates a new builder with the specified evaluation ID, preserving all other IDs.
        /// Returns a builder to enable efficient chaining without intermediate allocations.
        /// Call Build() to create the final BuildEventContext.
        /// </summary>
        /// <param name="evaluationId">The new evaluation ID</param>
        /// <returns>A builder with the updated evaluation ID</returns>
        public BuildEventContextBuilder WithEvaluationId(int evaluationId) => Builder(this).WithEvaluationId(evaluationId);

        /// <summary>
        /// Creates a new builder with the specified project instance ID, preserving all other IDs.
        /// Returns a builder to enable efficient chaining without intermediate allocations.
        /// Call Build() to create the final BuildEventContext.
        /// </summary>
        /// <param name="projectInstanceId">The new project instance ID</param>
        /// <returns>A builder with the updated project instance ID</returns>
        public BuildEventContextBuilder WithProjectInstanceId(int projectInstanceId) => Builder(this).WithProjectInstanceId(projectInstanceId);

        /// <summary>
        /// Creates a new builder with the specified project context ID, preserving all other IDs.
        /// Returns a builder to enable efficient chaining without intermediate allocations.
        /// Call Build() to create the final BuildEventContext.
        /// </summary>
        /// <param name="projectContextId">The new project context ID</param>
        /// <returns>A builder with the updated project context ID</returns>
        public BuildEventContextBuilder WithProjectContextId(int projectContextId) => Builder(this).WithProjectContextId(projectContextId);

        /// <summary>
        /// Creates a new builder with the specified target ID, preserving all other IDs.
        /// Returns a builder to enable efficient chaining without intermediate allocations.
        /// Call Build() to create the final BuildEventContext.
        /// </summary>
        /// <param name="targetId">The new target ID</param>
        /// <returns>A builder with the updated target ID</returns>
        public BuildEventContextBuilder WithTargetId(int targetId) => Builder(this).WithTargetId(targetId);

        /// <summary>
        /// Creates a new builder with the specified task ID, preserving all other IDs.
        /// Returns a builder to enable efficient chaining without intermediate allocations.
        /// Call Build() to create the final BuildEventContext.
        /// </summary>
        /// <param name="taskId">The new task ID</param>
        /// <returns>A builder with the updated task ID</returns>
        public BuildEventContextBuilder WithTaskId(int taskId) => Builder(this).WithTaskId(taskId);
        #endregion

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
        private bool InternalEquals(BuildEventContext buildEventContext) => _nodeId == buildEventContext.NodeId
                   && _projectContextId == buildEventContext.ProjectContextId
                   && _targetId == buildEventContext.TargetId
                   && _taskId == buildEventContext.TaskId
                   && _evaluationId == buildEventContext._evaluationId
                   && _projectInstanceId == buildEventContext._projectInstanceId;
        #endregion

        public override string ToString() => $"Node={NodeId} Submission={SubmissionId} ProjectContext={ProjectContextId} ProjectInstance={ProjectInstanceId} Eval={EvaluationId} Target={TargetId} Task={TaskId}";

        #region Builder Pattern

        /// <summary>
        /// Creates a new builder initialized from an existing BuildEventContext.
        /// This allows for efficient copying and modification of existing contexts.
        /// </summary>
        /// <param name="source">The BuildEventContext to copy values from</param>
        /// <returns>A new BuildEventContextBuilder initialized with the source values</returns>
        public static BuildEventContextBuilder Builder(BuildEventContext source) => new(source);

        #endregion
    }

    /// <summary>
    /// A ref struct builder for efficiently constructing BuildEventContext instances.
    /// This builder eliminates heap allocations during the building process and provides
    /// a fluent API for setting context properties.
    /// 
    /// Usage:
    /// var context = BuildEventContext.Builder()
    ///     .WithSubmissionId(1)
    ///     .WithNodeId(2)
    ///     .WithProjectInstanceId(3)
    ///     .Build();
    /// </summary>
    public ref struct BuildEventContextBuilder
    {
        private int _submissionId;
        private int _nodeId;
        private int _evaluationId;
        private int _projectInstanceId;
        private int _projectContextId;
        private int _targetId;
        private int _taskId;

        /// <summary>
        /// Initializes a new BuildEventContextBuilder with invalid values for all IDs.
        /// </summary>
        public BuildEventContextBuilder()
        {
            _submissionId = BuildEventContext.InvalidSubmissionId;
            _nodeId = BuildEventContext.InvalidNodeId;
            _evaluationId = BuildEventContext.InvalidEvaluationId;
            _projectInstanceId = BuildEventContext.InvalidProjectInstanceId;
            _projectContextId = BuildEventContext.InvalidProjectContextId;
            _targetId = BuildEventContext.InvalidTargetId;
            _taskId = BuildEventContext.InvalidTaskId;
        }

        /// <summary>
        /// Initializes a new BuildEventContextBuilder with values from an existing BuildEventContext.
        /// </summary>
        /// <param name="source">The BuildEventContext to copy values from</param>
        public BuildEventContextBuilder(BuildEventContext source)
        {
            _submissionId = source.SubmissionId;
            _nodeId = source.NodeId;
            _evaluationId = source.EvaluationId;
            _projectInstanceId = source.ProjectInstanceId;
            _projectContextId = source.ProjectContextId;
            _targetId = source.TargetId;
            _taskId = source.TaskId;
        }

        /// <summary>
        /// Sets the submission ID and returns this builder for chaining.
        /// </summary>
        /// <param name="submissionId">The submission ID</param>
        /// <returns>This builder instance</returns>
        public BuildEventContextBuilder WithSubmissionId(int submissionId)
        {
            _submissionId = submissionId;
            return this;
        }

        /// <summary>
        /// Sets the node ID and returns this builder for chaining.
        /// </summary>
        /// <param name="nodeId">The node ID</param>
        /// <returns>This builder instance</returns>
        public BuildEventContextBuilder WithNodeId(int nodeId)
        {
            _nodeId = nodeId;
            return this;
        }

        /// <summary>
        /// Sets the evaluation ID and returns this builder for chaining.
        /// </summary>
        /// <param name="evaluationId">The evaluation ID</param>
        /// <returns>This builder instance</returns>
        public BuildEventContextBuilder WithEvaluationId(int evaluationId)
        {
            _evaluationId = evaluationId;
            return this;
        }

        /// <summary>
        /// Sets the project instance ID and returns this builder for chaining.
        /// </summary>
        /// <param name="projectInstanceId">The project instance ID</param>
        /// <returns>This builder instance</returns>
        public BuildEventContextBuilder WithProjectInstanceId(int projectInstanceId)
        {
            _projectInstanceId = projectInstanceId;
            return this;
        }

        /// <summary>
        /// Sets the project context ID and returns this builder for chaining.
        /// </summary>
        /// <param name="projectContextId">The project context ID</param>
        /// <returns>This builder instance</returns>
        public BuildEventContextBuilder WithProjectContextId(int projectContextId)
        {
            _projectContextId = projectContextId;
            return this;
        }

        /// <summary>
        /// Sets the target ID and returns this builder for chaining.
        /// </summary>
        /// <param name="targetId">The target ID</param>
        /// <returns>This builder instance</returns>
        public BuildEventContextBuilder WithTargetId(int targetId)
        {
            _targetId = targetId;
            return this;
        }

        /// <summary>
        /// Sets the task ID and returns this builder for chaining.
        /// </summary>
        /// <param name="taskId">The task ID</param>
        /// <returns>This builder instance</returns>
        public BuildEventContextBuilder WithTaskId(int taskId)
        {
            _taskId = taskId;
            return this;
        }

        /// <summary>
        /// Builds the final BuildEventContext instance.
        /// This is the only operation that allocates memory on the heap.
        /// </summary>
        /// <returns>A new BuildEventContext with the configured values</returns>
        public readonly BuildEventContext Build() => new BuildEventContext(
                _submissionId,
                _nodeId,
                _evaluationId,
                _projectInstanceId,
                _projectContextId,
                _targetId,
                _taskId);

        /// <summary>
        /// Implicit conversion from builder to BuildEventContext for convenience.
        /// This allows the builder to be used directly where a BuildEventContext is expected.
        /// </summary>
        /// <param name="builder">The builder to convert</param>
        /// <returns>A new BuildEventContext built from the builder</returns>
        public static implicit operator BuildEventContext(BuildEventContextBuilder builder)
        {
            return builder.Build();
        }
    }
}
