// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Collections;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Instance
{
    internal class ImmutableProjectMetadataCollectionConverter :
        ImmutableElementCollectionConverter<ProjectMetadata, ProjectMetadataInstance>,
        ICopyOnWritePropertyDictionary<ProjectMetadataInstance>
    {
        /// <summary>
        /// Immutable project item.
        /// </summary>
        private readonly ProjectItem _linkedProjectItem;

        public ImmutableProjectMetadataCollectionConverter(
            ProjectItem linkedProjectItem,
            IDictionary<string, ProjectMetadata> properties,
            Func<ProjectMetadata, ProjectMetadataInstance> convertProperty)
            : base(properties, constrainedProjectElements: null, convertProperty)
        {
            _linkedProjectItem = linkedProjectItem ?? throw new ArgumentNullException(nameof(linkedProjectItem));
        }

        public bool Contains(string name) => ContainsKey(name);

        public string? GetEscapedValue(string name)
        {
            // just check the name, instead of creating ProjectMetadataInstance
            if (Contains(name))
            {
                return EscapingUtilities.Escape(_linkedProjectItem.GetMetadataValue(name));
            }

            return null;
        }

        public ICopyOnWritePropertyDictionary<ProjectMetadataInstance> DeepClone() => this;

        public void ImportProperties(IEnumerable<ProjectMetadataInstance> other) => throw new NotSupportedException();

        public void Set(ProjectMetadataInstance projectProperty) => throw new NotSupportedException();

        public bool Equals(ICopyOnWritePropertyDictionary<ProjectMetadataInstance>? other)
        {
            if (other == null || Count != other.Count)
            {
                return false;
            }

            if (other is ImmutableProjectMetadataCollectionConverter otherImmutableDict)
            {
                // When comparing to another CollectionConverter we compare the TCached values
                // in order to avoid causing the instantiation of each T instance.
                foreach (var propKvp in _projectElements)
                {
                    if (!otherImmutableDict._projectElements.TryGetValue(propKvp.Key, out ProjectMetadata? otherProperty) ||
                        !EqualityComparer<ProjectMetadata>.Default.Equals(propKvp.Value, otherProperty))
                    {
                        return false;
                    }
                }
            }
            else
            {
                foreach (ProjectMetadataInstance thisProp in Values)
                {
                    if (!other.TryGetValue(((IKeyed)thisProp).Key, out ProjectMetadataInstance? thatProp) ||
                        !EqualityComparer<ProjectMetadataInstance>.Default.Equals(thisProp, thatProp))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
