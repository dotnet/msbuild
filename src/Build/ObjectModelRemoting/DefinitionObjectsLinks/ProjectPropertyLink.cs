// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;

namespace Microsoft.Build.ObjectModelRemoting
{
    /// <summary>
    /// External projects support.
    /// Allow for creating a local representation to external object of type <see cref="ProjectProperty"/>
    /// </summary>
    public abstract class ProjectPropertyLink
    {
        /// <summary>
        /// Access to remote <see cref="ProjectProperty.Project"/>.
        /// </summary>
        public abstract Project Project { get; }

        /// <summary>
        /// Access to remote <see cref="ProjectProperty.Xml"/>.
        /// </summary>
        public abstract ProjectPropertyElement Xml { get; }

        /// <summary>
        /// Access to remote <see cref="ProjectProperty.Name"/>.
        /// (note can not Use Xml.Name since for global properties Xml is null;
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Allow implement the <see cref="ProjectProperty.EvaluatedValue"/> for remoted objects.
        /// </summary>
        public abstract string EvaluatedIncludeEscaped { get; }

        /// <summary>
        /// Access to remote <see cref="ProjectProperty.UnevaluatedValue"/>.
        /// </summary>
        public abstract string UnevaluatedValue { get; set; }

        /// <summary>
        /// Access to remote <see cref="ProjectProperty.IsEnvironmentProperty"/>.
        /// </summary>
        public abstract bool IsEnvironmentProperty { get; }

        /// <summary>
        /// Access to remote <see cref="ProjectProperty.IsGlobalProperty"/>.
        /// </summary>
        public abstract bool IsGlobalProperty { get; }

        /// <summary>
        /// Access to remote <see cref="ProjectProperty.IsReservedProperty"/>.
        /// </summary>
        public abstract bool IsReservedProperty { get; }

        /// <summary>
        /// Access to remote <see cref="ProjectProperty.Predecessor"/>.
        /// </summary>
        public abstract ProjectProperty Predecessor { get; }

        /// <summary>
        /// Access to remote <see cref="ProjectProperty.IsImported"/>.
        /// </summary>
        public abstract bool IsImported { get; }

        /// <summary>
        /// Helper utility for External projects provider implementation to get access of the EvaluatedValueEscaped
        public static string GetEvaluatedValueEscaped(ProjectProperty property)
        {
            return property.EvaluatedValueEscapedIntenral;
        }
    }

}
