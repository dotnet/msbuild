// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Collections;
using Microsoft.Build.Shared;
using Microsoft.Build.Evaluation;

using Microsoft.Build.Construction;

namespace Microsoft.Build.Execution
{
    /// <summary>
    /// Wraps an unevaluated itemgroup under a target.
    /// Immutable.
    /// </summary>
    [DebuggerDisplay("Condition={_condition}")]
    public class ProjectItemGroupTaskInstance : ProjectTargetInstanceChild, ITranslatable
    {
        /// <summary>
        /// Condition, if any
        /// </summary>
        private string _condition;

        /// <summary>
        /// Child items.
        /// Not ProjectItemInstances, as these are evaluated during the build.
        /// </summary>
        private List<ProjectItemGroupTaskItemInstance> _items;

        /// <summary>
        /// Location of this element
        /// </summary>
        private ElementLocation _location;

        /// <summary>
        /// Location of the condition, if any
        /// </summary>
        private ElementLocation _conditionLocation;

        /// <summary>
        /// Constructor called by the Evaluator.
        /// Assumes ProjectItemGroupTaskItemInstance is an immutable type.
        /// </summary>
        internal ProjectItemGroupTaskInstance
            (
            string condition,
            ElementLocation location,
            ElementLocation conditionLocation,
            List<ProjectItemGroupTaskItemInstance> items
            )
        {
            ErrorUtilities.VerifyThrowInternalNull(condition, "condition");
            ErrorUtilities.VerifyThrowInternalNull(location, "location");
            ErrorUtilities.VerifyThrowInternalNull(items, "items");

            _condition = condition;
            _location = location;
            _conditionLocation = conditionLocation;
            _items = items;
        }

        /// <summary>
        /// Cloning constructor
        /// </summary>
        private ProjectItemGroupTaskInstance(ProjectItemGroupTaskInstance that)
        {
            // All members are immutable
            _condition = that._condition;
            _items = that._items;
        }

        private ProjectItemGroupTaskInstance()
        {
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
        /// Child items
        /// </summary>
        public ICollection<ProjectItemGroupTaskItemInstance> Items
        {
            [DebuggerStepThrough]
            get
            {
                return (_items == null) ?
                    (ICollection<ProjectItemGroupTaskItemInstance>)ReadOnlyEmptyCollection<ProjectItemGroupTaskItemInstance>.Instance :
                    new ReadOnlyCollection<ProjectItemGroupTaskItemInstance>(_items);
            }
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
        /// Deep clone
        /// </summary>
        internal ProjectItemGroupTaskInstance DeepClone()
        {
            return new ProjectItemGroupTaskInstance(this);
        }

        void ITranslatable.Translate(ITranslator translator)
        {
            if (translator.Mode == TranslationDirection.WriteToStream)
            {
                var typeName = this.GetType().FullName;
                translator.Translate(ref typeName);
            }

            translator.Translate(ref _condition);
            translator.Translate(ref _items, ProjectItemGroupTaskItemInstance.FactoryForDeserialization);
            translator.Translate(ref _location, ElementLocation.FactoryForDeserialization);
            translator.Translate(ref _conditionLocation, ElementLocation.FactoryForDeserialization);
        }
    }
}
