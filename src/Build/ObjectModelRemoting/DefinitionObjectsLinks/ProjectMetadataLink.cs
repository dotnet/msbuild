// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;

namespace Microsoft.Build.ObjectModelRemoting
{

    /// <summary>
    /// External projects support.
    /// Allow for creating a local representation to external object of type <see cref="ProjectMetadata"/>
    /// </summary>
    public abstract class ProjectMetadataLink
    {
        /// <summary>
        /// MSBuild object that this meta data belong to.
        /// Can be either <see cref="ProjectItem"/>, or <see cref="ProjectItemDefinition"/>
        /// Not a public property on original ProjectMetadata object, but int is needed to create a local proxy object.
        /// </summary>
        public abstract object Parent { get; }

        /// <summary>
        /// Access to remote <see cref="ProjectMetadata.Xml"/>.
        /// </summary>
        public abstract ProjectMetadataElement Xml { get; }

        /// <summary>
        /// Access to remote <see cref="ProjectMetadata.EvaluatedValueEscaped"/>.
        /// </summary>
        public abstract string EvaluatedValueEscaped { get; }

        /// <summary>
        /// Access to remote <see cref="ProjectMetadata.Predecessor"/>.
        /// </summary>
        public abstract ProjectMetadata Predecessor { get; }

        /// <summary>
        /// Helper utility for External projects provider implementation to get access of the parent object.
        /// At this point this is internal property for <see cref="ProjectMetadata"/>.
        /// </summary>
        public static object GetParent(ProjectMetadata metadata)
        {
            return metadata?.Parent;
        }

        /// <summary>
        /// Helper utility for External projects provider implementation to get access of the EvaluatedValueEscaped
        public static string GetEvaluatedValueEscaped(ProjectMetadata metadata)
        {
            return metadata.EvaluatedValueEscaped;
        }
    }
}
