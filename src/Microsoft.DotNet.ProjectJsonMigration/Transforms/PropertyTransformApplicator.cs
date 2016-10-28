// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Build.Construction;
using System.Linq;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.ProjectJsonMigration.Transforms
{
    internal class PropertyTransformApplicator : ITransformApplicator
    {
        private readonly ProjectRootElement _projectElementGenerator = ProjectRootElement.Create();

        public void Execute<T, U>(
            T element,
            U destinationElement,
            bool mergeExisting) where T : ProjectElement where U : ProjectElementContainer
        {
            if (typeof(T) != typeof(ProjectPropertyElement))
            {
                throw new ArgumentException($"Expected element to be of type {nameof(ProjectPropertyElement)}, but got {nameof(T)}");
            }

            if (typeof(U) != typeof(ProjectPropertyGroupElement))
            {
                throw new ArgumentException($"Expected element to be of type {nameof(ProjectPropertyGroupElement)}, but got {nameof(U)}");
            }

            if (element == null)
            {
                return;
            }

            var property = element as ProjectPropertyElement;
            var destinationPropertyGroup = destinationElement as ProjectPropertyGroupElement;

            if (mergeExisting)
            {
                var mergedProperty = MergePropertyWithProject(property, destinationPropertyGroup);
                if (mergedProperty != null && !string.IsNullOrEmpty(mergedProperty.Value))
                {
                    TracePropertyInfo("Merging property, output merged property", mergedProperty);
                    AddPropertyToPropertyGroup(mergedProperty, destinationPropertyGroup);
                }
                else
                {
                    TracePropertyInfo("Ignoring fully merged property", property);
                }
            }
            else
            {
                AddPropertyToPropertyGroup(property, destinationPropertyGroup);
            }
        }

        private ProjectPropertyElement MergePropertyWithProject(
            ProjectPropertyElement property, 
            ProjectPropertyGroupElement destinationPropertyGroup)
        {
            var propertiesToMergeWith = FindPropertiesWithSameNameAndSameOrNoCondition(
                property, 
                destinationPropertyGroup.ContainingProject);

            foreach (var propertyToMergeWith in propertiesToMergeWith)
            {
                property = MergeProperties(propertyToMergeWith, property);
            }

            return property;
        }

        private IEnumerable<ProjectPropertyElement> FindPropertiesWithSameNameAndSameOrNoCondition(
            ProjectPropertyElement property, 
            ProjectRootElement project)
        {
            return project.Properties
                .Where(otherProperty => 
                    property.Name == otherProperty.Name 
                    && (property.Condition == otherProperty.Condition 
                        || (property.Condition == otherProperty.Parent.Condition && string.IsNullOrEmpty(otherProperty.Condition))
                        || !otherProperty.ConditionChain().Any()));
        }

        private ProjectPropertyElement MergeProperties(ProjectPropertyElement baseProperty, ProjectPropertyElement addedProperty)
        {
            var mergedProperty = _projectElementGenerator.AddProperty("___TEMP___", "___TEMP___");
            mergedProperty.CopyFrom(addedProperty);

            var basePropertyValues = baseProperty.Value.Split(';');
            var addedPropertyValues = addedProperty.Value.Split(';');

            var intersectedValues = basePropertyValues.Intersect(addedPropertyValues, StringComparer.Ordinal);
            intersectedValues = RemoveValuesWithVariable(intersectedValues);

            mergedProperty.Value = string.Join(";", addedPropertyValues.Except(intersectedValues));

            return mergedProperty;
        }

        private IEnumerable<string> RemoveValuesWithVariable(IEnumerable<string> intersectedValues)
        {
            return intersectedValues.Where(v => ! Regex.IsMatch(v, @"\$\(.*?\)"));
        }

        private void AddPropertyToPropertyGroup(ProjectPropertyElement property, ProjectPropertyGroupElement destinationPropertyGroup)
        {
            var outputProperty = destinationPropertyGroup.ContainingProject.CreatePropertyElement("___TEMP___");
            outputProperty.CopyFrom(property);

            destinationPropertyGroup.AppendChild(outputProperty);
        }

        public void Execute<T, U>(
            IEnumerable<T> elements,
            U destinationElement,
            bool mergeExisting) where T : ProjectElement where U : ProjectElementContainer
        {
            foreach (var element in elements)
            {
                Execute(element, destinationElement, mergeExisting);
            }
        }

        private void TracePropertyInfo(string message, ProjectPropertyElement mergedProperty)
        {
            MigrationTrace.Instance.WriteLine($"{nameof(PropertyTransformApplicator)}: {message}, {{ Name={mergedProperty.Name}, Value={mergedProperty.Value} }}");
        }
    }
}
