// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Collections;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Experimental.Graph
{
    internal sealed class ProjectInterpretation
    {
        private const string FullPathMetadataName = "FullPath";
        private const string ToolsVersionMetadataName = "ToolsVersion";
        private const string SetConfigurationMetadataName = "SetConfiguration";
        private const string SetPlatformMetadataName = "SetPlatform";
        private const string SetTargetFrameworkMetadataName = "SetTargetFramework";
        private const string GlobalPropertiesToRemoveMetadataName = "GlobalPropertiesToRemove";
        private const string ProjectReferenceTargetIsOuterBuildMetadataName = "OuterBuild";

        private static readonly char[] PropertySeparator = MSBuildConstants.SemicolonChar;

        public static ProjectInterpretation Instance = new ProjectInterpretation();

        private ProjectInterpretation()
        {
        }

        private static readonly ImmutableList<GlobalPropertiesModifier> ModifierForNonMultitargetingNodes = new[] {(GlobalPropertiesModifier) ProjectReferenceGlobalPropertiesModifier}.ToImmutableList();

        internal enum ProjectType
        {
            OuterBuild, InnerBuild, NonMultitargeting
        }

        public IEnumerable<ConfigurationMetadata> GetReferences(ProjectInstance requesterInstance)
        {
            IEnumerable<ProjectItemInstance> references;
            IEnumerable<GlobalPropertiesModifier> globalPropertiesModifiers = null;

            switch (GetProjectType(requesterInstance))
            {
                case ProjectType.OuterBuild:
                    references = GetInnerBuildReferences(requesterInstance);
                    break;
                case ProjectType.InnerBuild:
                    globalPropertiesModifiers = ModifierForNonMultitargetingNodes.Add((parts, reference) => parts.AddPropertyToUndefine(GetInnerBuildPropertyName(requesterInstance)));
                    references = requesterInstance.GetItems(ItemTypeNames.ProjectReference);
                    break;
                case ProjectType.NonMultitargeting:
                    globalPropertiesModifiers = ModifierForNonMultitargetingNodes;
                    references = requesterInstance.GetItems(ItemTypeNames.ProjectReference);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            foreach (var projectReference in references)
            {
                if (!String.IsNullOrEmpty(projectReference.GetMetadataValue(ToolsVersionMetadataName)))
                {
                    throw new InvalidOperationException(
                        String.Format(
                            CultureInfo.InvariantCulture,
                            ResourceUtilities.GetResourceString(
                                "ProjectGraphDoesNotSupportProjectReferenceWithToolset"),
                            projectReference.EvaluatedInclude,
                            requesterInstance.FullPath));
                }

                var projectReferenceFullPath = projectReference.GetMetadataValue(FullPathMetadataName);

                var referenceGlobalProperties = GetGlobalPropertiesForItem(projectReference, requesterInstance.GlobalPropertiesDictionary, globalPropertiesModifiers);

                var referenceConfig = new ConfigurationMetadata(projectReferenceFullPath, referenceGlobalProperties);

                yield return referenceConfig;
            }
        }

        private static string GetInnerBuildPropertyValue(ProjectInstance project)
        {
            return project.GetPropertyValue(GetInnerBuildPropertyName(project));
        }

        private static string GetInnerBuildPropertyName(ProjectInstance project)
        {
            return project.GetPropertyValue(PropertyNames.InnerBuildProperty);
        }

        private static string GetInnerBuildPropertyValues(ProjectInstance project)
        {
            return project.GetPropertyValue(project.GetPropertyValue(PropertyNames.InnerBuildPropertyValues));
        }

        internal static ProjectType GetProjectType(ProjectInstance project)
        {
            var isOuterBuild = String.IsNullOrWhiteSpace(GetInnerBuildPropertyValue(project)) && !String.IsNullOrWhiteSpace(GetInnerBuildPropertyValues(project));
            var isInnerBuild = !String.IsNullOrWhiteSpace(GetInnerBuildPropertyValue(project));

            ErrorUtilities.VerifyThrow(!(isOuterBuild && isInnerBuild), $"A project cannot be an outer and inner build at the same time: ${project.FullPath}");

            return isOuterBuild
                ? ProjectType.OuterBuild
                : isInnerBuild
                    ? ProjectType.InnerBuild
                    : ProjectType.NonMultitargeting;
        }

        public static void PostProcess(ConcurrentDictionary<ConfigurationMetadata, ProjectGraphNode> allNodes)
        {
            foreach (var nodeKvp in allNodes)
            {
                var outerBuild = nodeKvp.Value;

                if (GetProjectType(outerBuild.ProjectInstance) == ProjectType.OuterBuild && outerBuild.ReferencingProjects.Count != 0)
                {
                    foreach (var innerBuild in outerBuild.ProjectReferences)
                    {
                        foreach (var referencingProject in outerBuild.ReferencingProjects)
                        {
                            referencingProject.AddProjectReference(innerBuild);
                        }
                    }

                    outerBuild.RemoveReferences();
                }
            }
        }

        private static IEnumerable<ProjectItemInstance> GetInnerBuildReferences(ProjectInstance outerBuild)
        {
            var globalPropertyName = GetInnerBuildPropertyName(outerBuild);
            var globalPropertyValues = GetInnerBuildPropertyValues(outerBuild);

            ErrorUtilities.VerifyThrow(!String.IsNullOrWhiteSpace(globalPropertyName), "Must have an inner build property");
            ErrorUtilities.VerifyThrow(!String.IsNullOrWhiteSpace(globalPropertyValues), "Must have values for the inner build property");

            foreach (var globalPropertyValue in ExpressionShredder.SplitSemiColonSeparatedList(globalPropertyValues))
            {
                yield return new ProjectItemInstance(
                    outerBuild,
                    "_ProjectSelfReference",
                    outerBuild.FullPath,
                    new[] {new KeyValuePair<string, string>(ItemMetadataNames.PropertiesMetadataName, $"{globalPropertyName}={globalPropertyValue}")},
                    outerBuild.FullPath);
            }
        }

        /// <summary>
        ///     Gets the effective global properties for a project reference item.
        /// </summary>
        /// <remarks>
        ///     The behavior of this method should match the logic in the SDK
        /// </remarks>
        private static GlobalPropertyPartsForMSBuildTask ProjectReferenceGlobalPropertiesModifier(
            GlobalPropertyPartsForMSBuildTask defaultParts,
            ProjectItemInstance projectReference
        )
        {
            // ProjectReference defines yet another metadata name containing properties to undefine. Merge it in if non empty.
            var globalPropertiesToRemove = SplitPropertyNames(projectReference.GetMetadataValue(GlobalPropertiesToRemoveMetadataName));

            var newUndefineProperties = defaultParts.UndefineProperties;

            newUndefineProperties = newUndefineProperties.AddRange(defaultParts.UndefineProperties);
            newUndefineProperties = newUndefineProperties.AddRange(globalPropertiesToRemove);

            newUndefineProperties.Add("InnerBuildProperty");

            var newProperties = defaultParts.Properties;

            // The properties on the project reference supersede the ones from the MSBuild task instead of appending.
            if (newProperties.Count == 0)
            {
                // TODO: Mimic AssignProjectConfiguration's behavior for determining the values for these.
                var setConfigurationString = projectReference.GetMetadataValue(SetConfigurationMetadataName);
                var setPlatformString = projectReference.GetMetadataValue(SetPlatformMetadataName);
                var setTargetFrameworkString = projectReference.GetMetadataValue(SetTargetFrameworkMetadataName);

                if (!String.IsNullOrEmpty(setConfigurationString) || !String.IsNullOrEmpty(setPlatformString) || !String.IsNullOrEmpty(setTargetFrameworkString))
                {
                    newProperties = SplitPropertyNameValuePairs(
                        ItemMetadataNames.PropertiesMetadataName,
                        $"{setConfigurationString};{setPlatformString};{setTargetFrameworkString}").ToImmutableDictionary();
                }
            }

            return new GlobalPropertyPartsForMSBuildTask(newProperties, defaultParts.AdditionalProperties, newUndefineProperties);
        }

        private readonly struct GlobalPropertyPartsForMSBuildTask
        {
            public ImmutableDictionary<string, string> Properties { get; }
            public ImmutableDictionary<string, string> AdditionalProperties { get; }
            public ImmutableList<string> UndefineProperties { get; }

            public GlobalPropertyPartsForMSBuildTask(
                ImmutableDictionary<string, string> properties,
                ImmutableDictionary<string, string> additionalProperties,
                ImmutableList<string> undefineProperties)
            {
                Properties = properties;
                AdditionalProperties = additionalProperties;
                UndefineProperties = undefineProperties;
            }

            public bool AllEmpty()
            {
                return Properties.Count == 0 && AdditionalProperties.Count == 0 && UndefineProperties.Count == 0;
            }

            public GlobalPropertyPartsForMSBuildTask AddPropertyToUndefine(string propertyToUndefine)
            {
                return new GlobalPropertyPartsForMSBuildTask(Properties, AdditionalProperties, UndefineProperties.Add(propertyToUndefine));
            }
        }

        delegate GlobalPropertyPartsForMSBuildTask GlobalPropertiesModifier(GlobalPropertyPartsForMSBuildTask defaultParts, ProjectItemInstance projectReference);

        /// <summary>
        ///     Gets the effective global properties for an item that will get passed to <see cref="MSBuild.Projects"/>.
        /// </summary>
        /// <remarks>
        ///     The behavior of this method matches the hardcoded behaviour of the msbuild task
        ///     and the <paramref name="globalPropertyModifiers"/> parameter can contain other mutations done at build time in targets / tasks
        /// </remarks>
        private static PropertyDictionary<ProjectPropertyInstance> GetGlobalPropertiesForItem(
            ProjectItemInstance projectReference,
            PropertyDictionary<ProjectPropertyInstance> requesterGlobalProperties,
            IEnumerable<GlobalPropertiesModifier> globalPropertyModifiers = null)
        {
            ErrorUtilities.VerifyThrowInternalNull(projectReference, nameof(projectReference));
            ErrorUtilities.VerifyThrowArgumentNull(requesterGlobalProperties, nameof(requesterGlobalProperties));

            var properties = SplitPropertyNameValuePairs(ItemMetadataNames.PropertiesMetadataName, projectReference.GetMetadataValue(ItemMetadataNames.PropertiesMetadataName));
            var additionalProperties = SplitPropertyNameValuePairs(ItemMetadataNames.AdditionalPropertiesMetadataName, projectReference.GetMetadataValue(ItemMetadataNames.AdditionalPropertiesMetadataName));
            var undefineProperties = SplitPropertyNames(projectReference.GetMetadataValue(ItemMetadataNames.UndefinePropertiesMetadataName));

            var defaultParts = new GlobalPropertyPartsForMSBuildTask(properties.ToImmutableDictionary(), additionalProperties.ToImmutableDictionary(), undefineProperties.ToImmutableList());

            var globalPropertyParts = globalPropertyModifiers?.Aggregate(defaultParts, (currentProperties, modifier) => modifier(currentProperties, projectReference)) ?? defaultParts;

            if (globalPropertyParts.AllEmpty())
            {
                return requesterGlobalProperties;
            }

            // Make a copy to avoid mutating the requester
            var globalProperties = new PropertyDictionary<ProjectPropertyInstance>(requesterGlobalProperties);

            // Append and remove properties as specified by the various metadata
            MergeIntoPropertyDictionary(globalProperties, globalPropertyParts.Properties);
            MergeIntoPropertyDictionary(globalProperties, globalPropertyParts.AdditionalProperties);
            RemoveFromPropertyDictionary(globalProperties, globalPropertyParts.UndefineProperties);

            return globalProperties;
        }

        private static void MergeIntoPropertyDictionary(
            PropertyDictionary<ProjectPropertyInstance> destination,
            IReadOnlyDictionary<string, string> source)
        {
            foreach (var pair in source)
            {
                destination[pair.Key] = ProjectPropertyInstance.Create(pair.Key, pair.Value);
            }
        }

        private static IReadOnlyDictionary<string, string> SplitPropertyNameValuePairs(string syntaxName, string propertyNameAndValuesString)
        {
            if (String.IsNullOrEmpty(propertyNameAndValuesString))
            {
                return ImmutableDictionary<string, string>.Empty;
            }

            if (PropertyParser.GetTableWithEscaping(
                null,
                null,
                null,
                propertyNameAndValuesString.Split(PropertySeparator, StringSplitOptions.RemoveEmptyEntries),
                out var propertiesTable))
            {
                return propertiesTable;
            }

            throw new InvalidProjectFileException(
                String.Format(
                    CultureInfo.InvariantCulture,
                    ResourceUtilities.GetResourceString("General.InvalidPropertyError"),
                    syntaxName,
                    propertyNameAndValuesString));
        }

        private static IReadOnlyCollection<string> SplitPropertyNames(string propertyNamesString)
        {
            if (String.IsNullOrEmpty(propertyNamesString))
            {
                return ImmutableArray<string>.Empty;
            }

            return propertyNamesString.Split(PropertySeparator, StringSplitOptions.RemoveEmptyEntries);
        }

        private static void RemoveFromPropertyDictionary(
            PropertyDictionary<ProjectPropertyInstance> properties,
            IReadOnlyCollection<string> propertyNamesToRemove)
        {
            foreach (var propertyName in propertyNamesToRemove)
            {
                properties.Remove(propertyName);
            }
        }

        public readonly struct TargetsToPropagate
        {
            private readonly ImmutableList<string> _outerBuildTargets;
            private readonly ImmutableList<string> _allTargets;

            private TargetsToPropagate(ImmutableList<string> outerBuildTargets, ImmutableList<string> nonOuterBuildTargets)
            {
                _outerBuildTargets = outerBuildTargets;

                // This is used as the list of entry targets for both inner builds and non-multitargeting projects
                // It represents the concatenation of outer build targets and non outer build targets, in this order.
                // Non-multitargeting projects use these targets because they act as both outer and inner builds.
                _allTargets = outerBuildTargets.AddRange(nonOuterBuildTargets);
            }

            public static TargetsToPropagate FromProjectAndEntryTargets(ProjectInstance project, ImmutableList<string> entryTargets)
            {
                var targetsForOuterBuild = ImmutableList.CreateBuilder<string>();
                var targetsForInnerBuild = ImmutableList.CreateBuilder<string>();

                var projectReferenceTargets = project.GetItems(ItemTypeNames.ProjectReferenceTargets);

                foreach (var entryTarget in entryTargets)
                {
                    foreach (var projectReferenceTarget in projectReferenceTargets)
                    {
                        if (projectReferenceTarget.EvaluatedInclude.Equals(entryTarget, StringComparison.OrdinalIgnoreCase))
                        {
                            var targetsMetadataValue = projectReferenceTarget.GetMetadataValue(ItemMetadataNames.ProjectReferenceTargetsMetadataName);

                            var targetsAreForOuterBuild = projectReferenceTarget.GetMetadataValue(ProjectReferenceTargetIsOuterBuildMetadataName).Equals("true", StringComparison.OrdinalIgnoreCase);

                            var targets = ExpressionShredder.SplitSemiColonSeparatedList(targetsMetadataValue).ToArray();

                            if (targetsAreForOuterBuild)
                            {
                                targetsForOuterBuild.AddRange(targets);
                            }
                            else
                            {
                                targetsForInnerBuild.AddRange(targets);
                            }
                        }
                    }
                }

                return new TargetsToPropagate(targetsForOuterBuild.ToImmutable(), targetsForInnerBuild.ToImmutable());
            }

            public ImmutableList<string> GetApplicableTargets(ProjectInstance project)
            {
                switch (GetProjectType(project))
                {
                    case ProjectType.InnerBuild:
                        return _allTargets;
                    case ProjectType.OuterBuild:
                        return _outerBuildTargets;
                    case ProjectType.NonMultitargeting:
                        return _allTargets;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }
    }
}
