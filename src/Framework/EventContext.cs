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
    [Serializable]
    public class BuildEventContext
    {
        #region Constructor
        public BuildEventContext
        (
            int nodeId,
            int targetId,
            int projectContextId,
            int taskId
        )
        {
            this.nodeId = nodeId;
            this.targetId = targetId;
            this.projectContextId = projectContextId;
            this.taskId = taskId;
        }
        #endregion

        #region Properties
        /// <summary>
        /// NodeId where event took Place
        /// </summary>
        public int NodeId
        {
            get
            {
                return nodeId;
            }
        }

        /// <summary>
        /// TargetName of the target the event was in when the event was fired
        /// </summary>
        public int TargetId
        {
            get
            { 
                return targetId; 
            }
        }

        public int ProjectContextId
        {
            get
            {
                return projectContextId;
            }
        }

        public int TaskId
        {
            get
            {
                return this.taskId;
            }
        }
        #endregion

        #region Constants
        public const int InvalidProjectContextId = -2;
        public const int InvalidTaskId = -1;
        public const int InvalidTargetId = -1;
        public const int InvalidNodeId = -2;
        #endregion

        public override int GetHashCode()
        {
            return (ProjectContextId + (NodeId << 24));
        }

        public override bool Equals(object obj)
        {
            // If the references are the same no need to do any comparing
            if (base.Equals(obj))
            {
                return true;
            }

            BuildEventContext contextToCompare = obj as BuildEventContext;

            if (contextToCompare == null)
            {
                return false;
            }

            return (this.nodeId == contextToCompare.NodeId)
                   && (this.projectContextId == contextToCompare.ProjectContextId)
                   && (this.targetId == contextToCompare.TargetId)
                   && (this.taskId == contextToCompare.TaskId);
        }

        #region Data
        // Node event was in 
        private int nodeId;
        // Target event was in
        private int targetId;
        //ProjectContext the event was in
        private int projectContextId;
        // Id of the task the event was caused from
        private int taskId;
        #endregion
    }
}
