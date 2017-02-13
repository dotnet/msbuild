// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Represents an output property tag on a task for build purposes</summary>
//-----------------------------------------------------------------------

using Microsoft.Build.Shared;
using Microsoft.Build.Construction;

namespace Microsoft.Build.Execution
{
    /// <summary>
    /// Represents an output property element beneath a task element
    /// </summary>
    /// <remarks>
    /// Immutable.
    /// </remarks>
    public sealed class ProjectTaskOutputPropertyInstance : ProjectTaskInstanceChild
    {
        /// <summary>
        /// Name of the property to put the output in
        /// </summary>
        private readonly string _propertyName;

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
        /// Location of the original property name attribute
        /// </summary>
        private ElementLocation _propertyNameLocation;

        /// <summary>
        /// Location of the original task parameter attribute
        /// </summary>
        private ElementLocation _taskParameterLocation;

        /// <summary>
        /// Location of the original condition attribute
        /// </summary>
        private ElementLocation _conditionLocation;

        /// <summary>
        /// Constructor called by Evaluator
        /// </summary>
        internal ProjectTaskOutputPropertyInstance(string propertyName, string taskParameter, string condition, ElementLocation location, ElementLocation propertyNameLocation, ElementLocation taskParameterLocation, ElementLocation conditionLocation)
        {
            ErrorUtilities.VerifyThrowInternalLength(propertyName, "propertyName");
            ErrorUtilities.VerifyThrowInternalLength(taskParameter, "taskParameter");
            ErrorUtilities.VerifyThrowInternalNull(location, "location");
            ErrorUtilities.VerifyThrowInternalNull(propertyNameLocation, "propertyNameLocation");
            ErrorUtilities.VerifyThrowInternalNull(taskParameterLocation, "taskParameterLocation");

            _propertyName = propertyName;
            _taskParameter = taskParameter;
            _condition = condition;
            _location = location;
            _propertyNameLocation = propertyNameLocation;
            _taskParameterLocation = taskParameterLocation;
            _conditionLocation = conditionLocation;
        }

        /// <summary>
        /// Name of the property to put the output in
        /// </summary>
        public string PropertyName
        {
            get { return _propertyName; }
        }

        /// <summary>
        /// Property on the task class to retrieve the output from
        /// </summary>
        public string TaskParameter
        {
            get { return _taskParameter; }
        }

        /// <summary>
        /// Condition on the output element.
        /// If there is no condition, returns empty string.
        /// </summary>
        public override string Condition
        {
            get { return _condition; }
        }

        /// <summary>
        /// Location of the original PropertyName attribute
        /// </summary>
        public ElementLocation PropertyNameLocation
        {
            get { return _propertyNameLocation; }
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
    }
}
