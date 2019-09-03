// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.Build.Construction;

namespace Microsoft.Build.ObjectModelRemoting
{
    /// <summary>
    /// External projects support.
    /// Allow for creating a local representation to external object of type <see cref="ProjectTaskElement"/>
    /// </summary>
    public abstract class ProjectTaskElementLink : ProjectElementContainerLink
    {
        /// <summary>
        /// Access to remote <see cref="ProjectTaskElement.Parameters"/>.
        /// </summary>
        public abstract IDictionary<string, string> Parameters { get; }

        /// <summary>
        /// Access to remote <see cref="ProjectTaskElement.ParameterLocations"/>.
        /// </summary>
        public abstract IEnumerable<KeyValuePair<string, ElementLocation>> ParameterLocations { get; }

        /// <summary>
        /// Facilitate remoting the <see cref="ProjectTaskElement.GetParameter"/>.
        /// </summary>
        public abstract string GetParameter(string name);

        /// <summary>
        /// Facilitate remoting the <see cref="ProjectTaskElement.SetParameter"/>.
        /// </summary>
        public abstract void SetParameter(string name, string unevaluatedValue);

        /// <summary>
        /// Facilitate remoting the <see cref="ProjectTaskElement.RemoveParameter"/>.
        /// </summary>
        public abstract void RemoveParameter(string name);

        /// <summary>
        /// Facilitate remoting the <see cref="ProjectTaskElement.RemoveAllParameters"/>.
        /// </summary>
        public abstract void RemoveAllParameters();
    }
}
