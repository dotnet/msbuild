// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
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

namespace Microsoft.Build.Graph
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
        private const string InnerBuildReferenceItemName = "_ProjectSelfReference";
        internal static string TransitiveReferenceItemName = "_TransitiveProjectReference";
        internal const string AddTransitiveProjectReferencesInStaticGraphPropertyName = "AddTransitiveProjectReferencesInStaticGraph";

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

        internal readonly struct ReferenceInfo
        {
            public ConfigurationMetadata ReferenceConfiguration { get; }
            public ProjectItemInstance ProjectReferenceItem { get; }

            public ReferenceInfo(ConfigurationMetadata referenceConfiguration, ProjectItemInstance projectReferenceItem)
            {
                ReferenceConfiguration = referenceConfiguration;
                ProjectReferenceItem = projectReferenceItem;
            }
        }

        public IEnumerable<ReferenceInfo> GetReferences(ProjectInstance requesterInstance)
        {
            IEnumerable<ProjectItemInstance> projectReferenceItems;
            IEnumerable<GlobalPropertiesModifier> globalPropertiesModifiers = null;

            switch (GetProjectType(requesterInstance))
            {
                case ProjectType.OuterBuild:
                    projectReferenceItems = ConstructInnerBuildReferences(requesterInstance);
                    break;
                case ProjectType.InnerBuild:
                    globalPropertiesModifiers = ModifierForNonMultitargetingNodes.Add((parts, reference) => parts.AddPropertyToUndefine(GetInnerBuildPropertyName(requesterInstance)));
                    projectReferenceItems = requesterInstance.GetItems(ItemTypeNames.ProjectReference);
                    break;
                case ProjectType.NonMultitargeting:
                    globalPropertiesModifiers = ModifierForNonMultitargetingNodes;
                    projectReferenceItems = requesterInstance.GetItems(ItemTypeNames.ProjectReference);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            foreach (var projectReferenceItem in projectReferenceItems)
            {
                if (!String.IsNullOrEmpty(projectReferenceItem.GetMetadataValue(ToolsVersionMetadataName)))
                {
                    throw new InvalidOperationException(
                        String.Format(
                            CultureInfo.InvariantCulture,
                            ResourceUtilities.GetResourceString(
                                "ProjectGraphDoesNotSupportProjectReferenceWithToolset"),
                            projectReferenceItem.EvaluatedInclude,
                            requesterInstance.FullPath));
                }

                var projectReferenceFullPath = projectReferenceItem.GetMetadataValue(FullPathMetadataName);

                var referenceGlobalProperties = GetGlobalPropertiesForItem(projectReferenceItem, requesterInstance.GlobalPropertiesDictionary, globalPropertiesModifiers);

                var referenceConfig = new ConfigurationMetadata(projectReferenceFullPath, referenceGlobalProperties);

                yield return new ReferenceInfo(referenceConfig, projectReferenceItem);
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

        /// <summary>
        /// To avoid calling nuget at graph construction time, the graph is initially constructed with outer build nodes referencing inner build nodes.
        /// However, at build time, for non root outer builds, the inner builds are NOT referenced by the outer build, but by the nodes referencing the
        /// outer build. Change the graph to mimic this behaviour.
        /// Examples
        /// OuterAsRoot -> Inner go to OuterAsRoot -> Inner. Inner builds remain the same, parented to their outer build
        /// Node -> Outer -> Inner go to: Node -> Outer; Node->Inner; Outer -> empty. Inner builds get reparented to Node
        /// </summary>
        public void ReparentInnerBuilds(Dictionary<ConfigurationMetadata, ParsedProject> allNodes, GraphBuilder graphBuilder)
        {
            foreach (var node in allNodes)
            {
                var outerBuild = node.Value.GraphNode;

                if (GetProjectType(outerBuild.ProjectInstance) == ProjectType.OuterBuild && outerBuild.ReferencingProjects.Count != 0)
                {
                    foreach (var innerBuild in outerBuild.ProjectReferences)
                    {
                        foreach (var outerBuildReferencingProject in outerBuild.ReferencingProjects)
                        {
                            // Which edge should be used to connect the outerBuildReferencingProject to the inner builds?
                            // Decided to use the outerBuildBuildReferencingProject -> outerBuild edge in order to preserve any extra metadata
                            // information that may be present on the edge, like the "Targets" metadata which specifies what
                            // targets to call on the references.
                            var newInnerBuildEdge = graphBuilder.Edges[(outerBuildReferencingProject, outerBuild)];

                            if (outerBuildReferencingProject.ProjectReferences.Contains(innerBuild))
                            {
                                graphBuilder.Edges.TryGetEdge((outerBuildReferencingProject, innerBuild), out var existingEdge);

                                ErrorUtilities.VerifyThrow(
                                    graphBuilder.Edges[(outerBuildReferencingProject, innerBuild)]
                                        .ItemType.Equals(
                                            TransitiveReferenceItemName,
                                            StringComparison.OrdinalIgnoreCase),
                                    "Only transitive references may reference inner builds that got generated by outer builds");

                                outerBuildReferencingProject.RemoveReference(innerBuild, graphBuilder.Edges);
                            }

                            outerBuildReferencingProject.AddProjectReference(innerBuild, newInnerBuildEdge, graphBuilder.Edges);
                        }
                    }

                    outerBuild.RemoveReferences(graphBuilder.Edges);
                }
            }
        }

        private static IEnumerable<ProjectItemInstance> ConstructInnerBuildReferences(ProjectInstance outerBuild)
        {
            var globalPropertyName = GetInnerBuildPropertyName(outerBuild);
            var globalPropertyValues = GetInnerBuildPropertyValues(outerBuild);

            ErrorUtilities.VerifyThrow(!String.IsNullOrWhiteSpace(globalPropertyName), "Must have an inner build property");
            ErrorUtilities.VerifyThrow(!String.IsNullOrWhiteSpace(globalPropertyValues), "Must have values for the inner build property");

            foreach (var globalPropertyValue in ExpressionShredder.SplitSemiColonSeparatedList(globalPropertyValues))
            {
                yield return new ProjectItemInstance(
                    project: outerBuild,
                    itemType: InnerBuildReferenceItemName,
                    includeEscaped: outerBuild.FullPath,
                    directMetadata: new[] {new KeyValuePair<string, string>(ItemMetadataNames.PropertiesMetadataName, $"{globalPropertyName}={globalPropertyValue}")},
                    definingFileEscaped: outerBuild.FullPath);
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

                // This is used as the list of entry targets for both inner builds and non-multitargeting projects.
                // It represents the concatenation of outer build targets and non outer build targets, in this order.
                // Non-multitargeting projects use these targets because they act as both outer and inner builds.
                _allTargets = outerBuildTargets.AddRange(nonOuterBuildTargets);
            }

            /// <summary>
            /// Given a project and a set of entry targets the project would get called with,
            /// parse the project's project reference target specification and compute how the target would call its references.
            ///
            /// The calling code should then call <see cref="GetApplicableTargetsForReference"/> for each of the project's references
            /// to get the concrete targets for each reference.
            /// </summary>
            /// <param name="project">Project containing the PRT protocol</param>
            /// <param name="entryTargets">Targets with which <paramref name="project"/> will get called</param>
            /// <returns></returns>
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

                            var targetsAreForOuterBuild = MSBuildStringIsTrue(projectReferenceTarget.GetMetadataValue(ProjectReferenceTargetIsOuterBuildMetadataName));

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

            public ImmutableList<string> GetApplicableTargetsForReference(ProjectInstance reference)
            {
                return (GetProjectType(reference)) switch
                {
                    ProjectType.InnerBuild => _allTargets,
                    ProjectType.OuterBuild => _outerBuildTargets,
                    ProjectType.NonMultitargeting => _allTargets,
                    _ => throw new ArgumentOutOfRangeException(),
                };
            }
        }

        public bool RequiresTransitiveProjectReferences(ProjectInstance projectInstance)
        {
            // Outer builds do not get edges based on ProjectReference or their transitive closure, only inner builds do.
            if (GetProjectType(projectInstance) == ProjectType.OuterBuild)
            {
                return false;
            }

            // special case for Quickbuild which updates msbuild binaries independent of props/targets. Remove this when all QB repos will have
            // migrated to new enough Visual Studio versions whose Microsoft.Managed.After.Targets enable transitive references.
            if (string.IsNullOrWhiteSpace(projectInstance.GetPropertyValue(AddTransitiveProjectReferencesInStaticGraphPropertyName)) &&
                MSBuildStringIsTrue(projectInstance.GetPropertyValue("UsingMicrosoftNETSdk")) &&
                MSBuildStringIsFalse(projectInstance.GetPropertyValue("DisableTransitiveProjectReferences")))
            {
                return true;
            }

            return MSBuildStringIsTrue(
                projectInstance.GetPropertyValue(AddTransitiveProjectReferencesInStaticGraphPropertyName));
        }

        private static bool MSBuildStringIsTrue(string msbuildString) =>
            ConversionUtilities.ConvertStringToBool(msbuildString, nullOrWhitespaceIsFalse: true);

        private static bool MSBuildStringIsFalse(string msbuildString) => !MSBuildStringIsTrue(msbuildString);
    }
}
