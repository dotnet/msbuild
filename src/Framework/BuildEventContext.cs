// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Globalization;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Will provide location information for an event, this is especially 
    /// needed in a multi processor environment
    /// </summary>
#if FEATURE_BINARY_SERIALIZATION
    [Serializable]
#endif
    public class BuildEventContext
    {
        #region Data

        /// <summary>
        /// Node event was in 
        /// </summary>
        private int nodeId;

        /// <summary>
        /// Target event was in
        /// </summary>
        private int targetId;

        /// <summary>
        ///The node-unique project request context the event was in
        /// </summary>
        private int projectContextId;

        /// <summary>
        /// Id of the task the event was caused from
        /// </summary>
        private int taskId;

        /// <summary>
        /// The id of the project instance to which this event refers.
        /// </summary>
        private int projectInstanceId;

        /// <summary>
        /// The id of the submission.
        /// </summary>
        private int submissionId;

        #endregion

        #region Constructor

        /// <summary>
        /// This is the original constructor.  No one should ever use this except internally for backward compatibility.
        /// </summary>
        public BuildEventContext
        (
            int nodeId,
            int targetId,
            int projectContextId,
            int taskId
        )
            : this(InvalidSubmissionId, nodeId, InvalidProjectInstanceId, projectContextId, targetId, taskId)
        {
            // UNDONE: This is obsolete.
        }

        /// <summary>
        /// Constructs a BuildEventContext with a specified project instance id.
        /// </summary>
        public BuildEventContext
        (
            int nodeId,
            int projectInstanceId,
            int projectContextId,
            int targetId,
            int taskId
        )
            : this(InvalidSubmissionId, nodeId, projectInstanceId, projectContextId, targetId, taskId)
        {
        }

        /// <summary>
        /// Constructs a BuildEventContext with a specific submission id
        /// </summary>
        public BuildEventContext
        (
            int submissionId,
            int nodeId,
            int projectInstanceId,
            int projectContextId,
            int targetId,
            int taskId
        )
        {
            this.submissionId = submissionId;
            this.nodeId = nodeId;
            this.targetId = targetId;
            this.projectContextId = projectContextId;
            this.taskId = taskId;
            this.projectInstanceId = projectInstanceId;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns a default invalid BuildEventContext
        /// </summary>
        public static BuildEventContext Invalid
        {
            get
            {
                return new BuildEventContext(BuildEventContext.InvalidNodeId, BuildEventContext.InvalidTargetId, BuildEventContext.InvalidProjectContextId, BuildEventContext.InvalidTaskId);
            }
        }

        /// <summary>
        /// NodeId where event took place
        /// </summary>
        public int NodeId
        {
            get
            {
                return nodeId;
            }
        }

        /// <summary>
        /// Id of the target the event was in when the event was fired
        /// </summary>
        public int TargetId
        {
            get
            {
                return targetId;
            }
        }

        /// <summary>
        /// Retrieves the Project Context id.
        /// </summary>
        public int ProjectContextId
        {
            get
            {
                return projectContextId;
            }
        }

        /// <summary>
        /// Retrieves the task id.
        /// </summary>
        public int TaskId
        {
            get
            {
                return taskId;
            }
        }

        /// <summary>
        /// Retrieves the project instance id.
        /// </summary>
        public int ProjectInstanceId
        {
            get
            {
                return projectInstanceId;
            }
        }

        /// <summary>
        /// Retrieves the Submission id.
        /// </summary>
        public int SubmissionId
        {
            get
            {
                return submissionId;
            }
        }

        /// <summary>
        /// Retrieves the BuildRequest id.  Note that this is not the same as the global request id on a BuildRequest or BuildResult.
        /// </summary>
        public long BuildRequestId
        {
            get
            {
                return ((long)nodeId << 32) + projectContextId;
            }
        }

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
        #endregion

        #region Equals

        /// <summary>
        /// Retrieves a hash code for this BuildEventContext.
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return (ProjectContextId + (NodeId << 24));
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
        public override bool Equals(object obj)
        {
            // If the references are the same no need to do any more comparing
            if (object.ReferenceEquals(this, obj))
            {
                return true;
            }

            if (object.ReferenceEquals(obj, null))
            {
                return false;
            }

            // The types do not match, they cannot be the same
            if (this.GetType() != obj.GetType())
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
        public static bool operator ==(BuildEventContext left, BuildEventContext right)
        {
            if (Object.ReferenceEquals(left, right))
            {
                return true;
            }

            if (Object.ReferenceEquals(left, null))
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
        public static bool operator !=(BuildEventContext left, BuildEventContext right)
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
            return ((nodeId == buildEventContext.NodeId)
                   && (projectContextId == buildEventContext.ProjectContextId)
                   && (targetId == buildEventContext.TargetId)
                   && (taskId == buildEventContext.TaskId));
        }
        #endregion

    }
}
