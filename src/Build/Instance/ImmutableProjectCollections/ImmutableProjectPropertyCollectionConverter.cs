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
    internal sealed class ImmutableProjectPropertyCollectionConverter :
        ImmutableElementCollectionConverter<ProjectProperty, ProjectPropertyInstance>,
        IRetrievableValuedEntryHashSet<ProjectPropertyInstance>,
        IRetrievableUnescapedValuedEntryHashSet
    {
        private readonly Project _linkedProject;

        public ImmutableProjectPropertyCollectionConverter(
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
            if (TryGetUnescapedValue(key, out string unescapedValue))
            {
                escapedValue = EscapingUtilities.Escape(unescapedValue);
                return true;
            }

            escapedValue = null;
            return false;
        }

        public bool TryGetUnescapedValue(string key, out string unescapedValue)
        {
            unescapedValue = _linkedProject.GetPropertyValue(key);
            if (string.IsNullOrEmpty(unescapedValue))
            {
                // maintain the behavior of the original implementation
                if (!ContainsKey(key))
                {
                    unescapedValue = null;
                    return false;
                }
            }

            return true;
        }
    }
}
