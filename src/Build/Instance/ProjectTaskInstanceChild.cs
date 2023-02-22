// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.BackEnd;
using Microsoft.Build.Construction;
using Microsoft.Build.Shared;

#nullable disable

namespace Microsoft.Build.Execution
{
    /// <summary>
    /// Type for TaskOutputItem and TaskOutputProperty
    /// allowing them to be used in a single collection
    /// </summary>
    public abstract class ProjectTaskInstanceChild : ITranslatable
    {
        /// <summary>
        /// Condition on the element
        /// </summary>
        public abstract string Condition
        {
            get;
        }

        /// <summary>
        /// Location of the original element
        /// </summary>
        public abstract ElementLocation Location
        {
            get;
        }

        /// <summary>
        /// Location of the TaskParameter attribute
        /// </summary>
        public abstract ElementLocation TaskParameterLocation
        {
            get;
        }

        /// <summary>
        /// Location of the original condition attribute, if any
        /// </summary>
        public abstract ElementLocation ConditionLocation
        {
            get;
        }

        void ITranslatable.Translate(ITranslator translator)
        {
            // all subclasses should be translateable
            ErrorUtilities.ThrowInternalErrorUnreachable();
        }

        internal static ProjectTaskInstanceChild FactoryForDeserialization(ITranslator translator)
        {
            return translator.FactoryForDeserializingTypeWithName<ProjectTaskInstanceChild>();
        }
    }
}
