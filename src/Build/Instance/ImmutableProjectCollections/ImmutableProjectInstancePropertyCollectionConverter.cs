// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System;
using System.Collections.Generic;
using Microsoft.Build.Collections;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Instance
{
    /// <inheritdoc />
    internal sealed class ImmutableProjectInstancePropertyCollectionConverter :
        ImmutableElementCollectionConverter<ProjectProperty, ProjectPropertyInstance>,
        IRetrievableValuedEntryHashSet<ProjectPropertyInstance>
    {
        private readonly Project _linkedProject;

        public ImmutableProjectInstancePropertyCollectionConverter(
            Project linkedProject,
            IDictionary<string, ProjectProperty> projectElements,
            IDictionary<(string, int, int), ProjectProperty> constrainedProjectElements,
            Func<ProjectProperty, ProjectPropertyInstance> convertElement)
            : base(projectElements, constrainedProjectElements, convertElement)
        {
            _linkedProject = linkedProject ?? throw new ArgumentNullException(nameof(linkedProject));
        }

        public bool TryGetEscapedValue(string key, out string escapedValue)
        {
            string unescapedValue = _linkedProject.GetPropertyValue(key);
            if (string.IsNullOrEmpty(unescapedValue))
            {
                // maintain the behavior of the original implementation
                if (!ContainsKey(key))
                {
                    escapedValue = null;
                    return false;
                }
            }

            escapedValue = EscapingUtilities.Escape(unescapedValue);
            return true;
        }
    }
}
