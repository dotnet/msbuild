// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Wraps an unevaluated propertygroup under a target.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Build.Collections;
using Microsoft.Build.Shared;
using Microsoft.Build.Evaluation;

using Microsoft.Build.Construction;

namespace Microsoft.Build.Execution
{
    /// <summary>
    /// Wraps an unevaluated propertygroup under a target.
    /// Immutable.
    /// </summary>
    [DebuggerDisplay("Condition={_condition}")]
    public class ProjectPropertyGroupTaskInstance : ProjectTargetInstanceChild
    {
        /// <summary>
        /// Condition, if any
        /// </summary>
        private readonly string _condition;

        /// <summary>
        /// Child properties.
        /// Not ProjectPropertyInstances, as these are evaluated during the build.
        /// </summary>
        private readonly ICollection<ProjectPropertyGroupTaskPropertyInstance> _properties;

        /// <summary>
        /// Location of this element
        /// </summary>
        private readonly ElementLocation _location;

        /// <summary>
        /// Location of the condition, if any
        /// </summary>
        private readonly ElementLocation _conditionLocation;

        /// <summary>
        /// Constructor called by the Evaluator.
        /// Assumes ProjectPropertyGroupTaskPropertyInstance is an immutable type.
        /// </summary>
        internal ProjectPropertyGroupTaskInstance
            (
            string condition,
            ElementLocation location,
            ElementLocation conditionLocation,
            IEnumerable<ProjectPropertyGroupTaskPropertyInstance> properties
            )
        {
            ErrorUtilities.VerifyThrowInternalNull(condition, "condition");
            ErrorUtilities.VerifyThrowInternalNull(location, "location");
            ErrorUtilities.VerifyThrowInternalNull(properties, "properties");

            _condition = condition;
            _location = location;
            _conditionLocation = conditionLocation;

            if (properties != null)
            {
                _properties = (properties is ICollection<ProjectPropertyGroupTaskPropertyInstance>) ?
                    ((ICollection<ProjectPropertyGroupTaskPropertyInstance>)properties) :
                    new List<ProjectPropertyGroupTaskPropertyInstance>(properties);
            }
        }

        /// <summary>
        /// Cloning constructor
        /// </summary>
        private ProjectPropertyGroupTaskInstance(ProjectPropertyGroupTaskInstance that)
        {
            // All members are immutable
            _condition = that._condition;
            _properties = that._properties;
        }

        /// <summary>
        /// Condition, if any.
        /// May be empty string.
        /// </summary>
        public override string Condition
        {
            [DebuggerStepThrough]
            get
            { return _condition; }
        }

        /// <summary>
        /// Child properties
        /// </summary>
        public ICollection<ProjectPropertyGroupTaskPropertyInstance> Properties
        {
            [DebuggerStepThrough]
            get
            {
                return (_properties == null) ?
                    (ICollection<ProjectPropertyGroupTaskPropertyInstance>)ReadOnlyEmptyCollection<ProjectPropertyGroupTaskPropertyInstance>.Instance :
                    new ReadOnlyCollection<ProjectPropertyGroupTaskPropertyInstance>(_properties);
            }
        }

        /// <summary>
        /// Location of the element itself
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
        /// Deep clone
        /// </summary>
        internal ProjectPropertyGroupTaskInstance DeepClone()
        {
            return new ProjectPropertyGroupTaskInstance(this);
        }
    }
}
