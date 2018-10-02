// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.BackEnd;
using Microsoft.Build.Shared;

using Microsoft.Build.Construction;

namespace Microsoft.Build.Execution
{
    /// <summary>
    /// Type for TaskOutputItem and TaskOutputProperty
    /// allowing them to be used in a single collection
    /// </summary>
    public abstract class ProjectTaskInstanceChild : INodePacketTranslatable
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

        void INodePacketTranslatable.Translate(INodePacketTranslator translator)
        {
            // all subclasses should be translateable
            ErrorUtilities.ThrowInternalErrorUnreachable();
        }

        internal static ProjectTaskInstanceChild FactoryForDeserialization(INodePacketTranslator translator)
        {
            return translator.FactoryForDeserializingTypeWithName<ProjectTaskInstanceChild>();
        }
    }
}
