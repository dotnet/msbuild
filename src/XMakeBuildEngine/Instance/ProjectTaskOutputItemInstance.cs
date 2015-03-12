// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Represents a task output item tag for build purposes.</summary>
//-----------------------------------------------------------------------

using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

using Microsoft.Build.Construction;

namespace Microsoft.Build.Execution
{
    /// <summary>
    /// Wraps an output item element under a task element
    /// </summary>
    /// <remarks>
    /// Immutable.
    /// </remarks>
    public sealed class ProjectTaskOutputItemInstance : ProjectTaskInstanceChild
    {
        /// <summary>
        /// Name of the property to put the output in
        /// </summary>
        private readonly string _itemType;

        /// <summary>
        /// Property on the task class to retrieve the output from
        /// </summary>
        private readonly string _taskParameter;

        /// <summary>
        /// Condition on the output element
        /// </summary>
        private readonly string _condition;

        /// <summary>
        /// Location of the original element
        /// </summary>
        private ElementLocation _location;

        /// <summary>
        /// Location of the original item type attribute
        /// </summary>
        private ElementLocation _itemTypeLocation;

        /// <summary>
        /// Location of the original task parameter attribute
        /// </summary>
        private ElementLocation _taskParameterLocation;

        /// <summary>
        /// Location of the original condition attribute
        /// </summary>
        private ElementLocation _conditionLocation;

        /// <summary>
        /// Constructor called by evaluator
        /// </summary>
        internal ProjectTaskOutputItemInstance(string itemType, string taskParameter, string condition, ElementLocation location, ElementLocation itemTypeLocation, ElementLocation taskParameterLocation, ElementLocation conditionLocation)
        {
            ErrorUtilities.VerifyThrowInternalLength(itemType, "itemType");
            ErrorUtilities.VerifyThrowInternalLength(taskParameter, "taskParameter");
            ErrorUtilities.VerifyThrowInternalNull(location, "location");
            ErrorUtilities.VerifyThrowInternalNull(itemTypeLocation, "itemTypeLocation");
            ErrorUtilities.VerifyThrowInternalNull(taskParameterLocation, "taskParameterLocation");

            _itemType = itemType;
            _taskParameter = taskParameter;
            _condition = condition;
            _location = location;
            _itemTypeLocation = itemTypeLocation;
            _taskParameterLocation = taskParameterLocation;
            _conditionLocation = conditionLocation;
        }

        /// <summary>
        /// Name of the item type that the outputs go into
        /// </summary>
        public string ItemType
        {
            get { return _itemType; }
        }

        /// <summary>
        /// Property on the task class to retrieve the outputs from
        /// </summary>
        public string TaskParameter
        {
            get { return _taskParameter; }
        }

        /// <summary>
        /// Condition on the element.
        /// If there is no condition, returns empty string.
        /// </summary>
        public override string Condition
        {
            get { return _condition; }
        }

        /// <summary>
        /// Location of the original element
        /// </summary>
        public override ElementLocation Location
        {
            get { return _location; }
        }

        /// <summary>
        /// Location of the condition, if any
        /// </summary>
        public override ElementLocation ConditionLocation
        {
            get { return _conditionLocation; }
        }

        /// <summary>
        /// Location of the TaskParameter attribute
        /// </summary>
        public override ElementLocation TaskParameterLocation
        {
            get { return _taskParameterLocation; }
        }

        /// <summary>
        /// Location of the ItemType attribute
        /// </summary>
        public ElementLocation ItemTypeLocation
        {
            get { return _itemTypeLocation; }
        }
    }
}
