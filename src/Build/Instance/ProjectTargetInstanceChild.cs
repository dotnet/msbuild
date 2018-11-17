// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Shared;

using Microsoft.Build.Construction;

namespace Microsoft.Build.Execution
{
    /// <summary>
    /// Type for ProjectTaskInstance and ProjectPropertyGroupTaskInstance and ProjectItemGroupTaskInstance
    /// allowing them to be used in a single collection of target children
    /// </summary>
    public abstract class ProjectTargetInstanceChild : ITranslatable
    {
        /// <summary>
        /// Condition on the element
        /// </summary>
        public abstract string Condition { get; }

        /// <summary>
        /// Full path to the file in which the originating element was originally 
        /// defined.
        /// If it originated in a project that was not loaded and has never been 
        /// given a path, returns an empty string.
        /// </summary>
        public string FullPath
        {
            get { return Location.File; }
        }

        /// <summary>
        /// Location of the original element
        /// </summary>
        public abstract ElementLocation Location { get; }

        /// <summary>
        /// Location of the original condition attribute
        /// if any
        /// </summary>
        public abstract ElementLocation ConditionLocation { get; }

        void ITranslatable.Translate(ITranslator translator)
        {
            // all subclasses should be translateable
            ErrorUtilities.ThrowInternalErrorUnreachable();
        }

        internal static ProjectTargetInstanceChild FactoryForDeserialization(ITranslator translator)
        {
            return translator.FactoryForDeserializingTypeWithName<ProjectTargetInstanceChild>();
        }
    }
}
