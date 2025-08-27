// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.BackEnd;
using Microsoft.Build.Construction;
using Microsoft.Build.Shared;

#nullable disable

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
