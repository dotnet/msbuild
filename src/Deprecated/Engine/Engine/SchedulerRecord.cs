// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Diagnostics; // For the debugger attribute

namespace Microsoft.Build.BuildEngine
{
    /// <summary>
    /// This class is a container used by the scheduler to keep track of what work is being done by which node.
    /// This includes seeing which build requests are blocked waiting for another build request to complete and which
    /// are actively in progress
    /// </summary>
    [DebuggerDisplay("Record ({recordKey.handleId},{recordKey.requestId})")]
    internal class ScheduleRecord
    {
        #region Constructors
        internal ScheduleRecord
        (
            ScheduleRecordKey recordKey, 
            ScheduleRecordKey parentKey, 
            int evaluationNode,
            string projectName,
            string toolsVersion,
            string [] targetsBuild
        )
        {
            this.recordKey = recordKey;
            this.parentKey = parentKey;
            this.evaluationNode = evaluationNode;
            this.blockedFlag = false;
            this.projectName = projectName;
            this.toolsVersion = toolsVersion;
            this.targetsBuild = targetsBuild;
        }
        #endregion

        #region Properties
        /// <summary>
        /// Returns true if this request is blocked waiting for the child requests to 
        /// complete
        /// </summary>
        internal bool Blocked
        {
            get
            {
                if ((requestIdToChildRecord == null || requestIdToChildRecord.Count == 0) && !blockedFlag)
                {
                    return false;
                }
                return true;
            }
            set
            {
                blockedFlag = value;
            }
        }

        /// <summary>
        /// Returns the node on which the request is being build
        /// </summary>
        internal int EvaluationNode
        {
            get
            {
                return evaluationNode;
            }
        }

        /// <summary>
        /// Key to the parent object
        /// </summary>
        internal ScheduleRecordKey ParentKey
        {
            get
            {
                return parentKey;
            }
        }

        /// <summary>
        /// Key to the current object
        /// </summary>
        internal ScheduleRecordKey RecordKey
        {
            get
            {
                return recordKey;
            }
        }

        /// <summary>
        /// Name of the project being build
        /// </summary>
        internal string ProjectName
        {
            get
            {
                return projectName;
            }
        }

        /// <summary>
        /// The version of the project
        /// </summary>
        internal string ToolsVersion
        {
            get
            {
                return toolsVersion;
            }
        }

        /// <summary>
        /// Targets being build in the project
        /// </summary>
        internal string[] TargetsBuild
        {
            get
            {
                return targetsBuild;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Add a child request to this record. Child requests block the parent.
        /// </summary>
        /// <param name="childRecord"></param>
        internal void AddChildRecord(ScheduleRecord childRecord)
        {
            if (requestIdToChildRecord == null)
            {
                requestIdToChildRecord = new Dictionary<ScheduleRecordKey, ScheduleRecord>();
            }

            requestIdToChildRecord.Add(childRecord.RecordKey, childRecord);
        }

        /// <summary>
        /// Remove a completed child request possibly unblocking the parent
        /// </summary>
        /// <param name="key"></param>
        internal void ReportChildCompleted(ScheduleRecordKey key)
        {
            requestIdToChildRecord.Remove(key);
        }

        #endregion

        #region Data
        // Key to the current object
        private ScheduleRecordKey recordKey;
        // Key to the parent object
        private ScheduleRecordKey parentKey;
        // Name of the project
        private string projectName;
        // Toolset version
        private string toolsVersion;
        // Targets being build
        private string[] targetsBuild;
        // Node on which the request is being build
        private int evaluationNode;
        // Marks the request as blocked
        private bool blockedFlag;
        // Dictionary of child requests (lazily initialized)
        private Dictionary<ScheduleRecordKey, ScheduleRecord> requestIdToChildRecord;
        #endregion
    }

    /// <summary>
    /// This class is used as a key combining both HandleId and RequestId into a single class.
    /// </summary>
    [DebuggerDisplay("Key ({handleId},{requestId})")]
    internal class ScheduleRecordKey 
    {
        #region Constructors
        internal ScheduleRecordKey(int handleId, int requestId)
        {
            this.handleId = handleId;
            this.requestId = requestId;
        }
        #endregion

        #region Properties
        internal int HandleId
        {
            get
            {
                return handleId;
            }
        }
        internal int RequestId
        {
            get
            {
                return requestId;
            }
        }
        #endregion

        #region Methods
        /// <summary>
        /// Override the equals operator to give valuetype comparison semantics
        /// </summary>
        public override bool Equals(object obj)
        {
            ScheduleRecordKey other = obj as ScheduleRecordKey;
            if (other != null)
            {
                if (other.handleId == handleId && other.requestId == requestId)
                {
                    return true;
                }
                return false;
            }

            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return handleId + (requestId << 24);
        }
        #endregion

        #region Data
        // Handle Id
        private int handleId;
        // Request Id
        private int requestId;
        #endregion
    }
}
