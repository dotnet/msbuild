// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Construction;
using Microsoft.Build.Execution;
using Microsoft.Build.Experimental.ProjectCache;
using Microsoft.Build.Shared;
using static Microsoft.Build.Graph.ProjectInterpretation;

#nullable disable

namespace Microsoft.Build.Graph
{
    public class ProjectReferenceSnapshot
    {
        public string FullPath = string.Empty;
        public string EvaluatedInclude = string.Empty;
        public string ItemType = string.Empty;

        private Dictionary<string, string> Metadata;

        public ProjectReferenceSnapshot(Dictionary<string, string> metadata)
        {
            Metadata = metadata;
        }

        public ProjectReferenceSnapshot(ProjectItemInstance projectReferenceTarget)
        {
            Metadata = new()
            {
                { ItemMetadataNames.ProjectReferenceTargetsMetadataName, projectReferenceTarget.GetMetadataValue(ItemMetadataNames.ProjectReferenceTargetsMetadataName) },
                { SkipNonexistentTargetsMetadataName , projectReferenceTarget.GetMetadataValue(SkipNonexistentTargetsMetadataName) },
                { ProjectReferenceTargetIsOuterBuildMetadataName, projectReferenceTarget.GetMetadataValue(ProjectReferenceTargetIsOuterBuildMetadataName) },
                { ItemMetadataNames.PropertiesMetadataName , projectReferenceTarget.GetMetadataValue(ItemMetadataNames.PropertiesMetadataName) },
                { ItemMetadataNames.AdditionalPropertiesMetadataName , projectReferenceTarget.GetMetadataValue(ItemMetadataNames.AdditionalPropertiesMetadataName) },
                { ItemMetadataNames.UndefinePropertiesMetadataName , projectReferenceTarget.GetMetadataValue(ItemMetadataNames.UndefinePropertiesMetadataName) },
                { ProjectMetadataName , projectReferenceTarget.GetMetadataValue(ProjectMetadataName) },
                { ToolsVersionMetadataName ,projectReferenceTarget.GetMetadataValue(ToolsVersionMetadataName) },
                { SetPlatformMetadataName , projectReferenceTarget.GetMetadataValue(SetPlatformMetadataName) },
                { GlobalPropertiesToRemoveMetadataName , projectReferenceTarget.GetMetadataValue(GlobalPropertiesToRemoveMetadataName) },
                { OverridePlatformNegotiationValue , projectReferenceTarget.GetMetadataValue(OverridePlatformNegotiationValue) },
                { SetConfigurationMetadataName , projectReferenceTarget.GetMetadataValue(SetConfigurationMetadataName) },
                { SetTargetFrameworkMetadataName ,projectReferenceTarget.GetMetadataValue(SetTargetFrameworkMetadataName) },
            };

            FullPath = projectReferenceTarget.GetMetadataValue(FullPathMetadataName);
        }

        public string GetMetadataValue(string metadataName)
        {
            // Note: FullPath is a special metadata that doesn't count towards the DirectMetadataCount.
            if (FullPathMetadataName == metadataName)
            {
                return FullPath;
            }

            if (Metadata.TryGetValue(metadataName, out string result))
            {
                return result;
            }

            return string.Empty;
            // throw new System.Exception($"Metadata not found {metadataName} in {ItemType}::{EvaluatedInclude} snapshot.");
        }

        public void SetMetadata(string metadataName, string value)
        {
            Metadata[metadataName] = value;
        }

        public bool HasMetadata(string metadataName)
        {
            return Metadata.TryGetValue(metadataName, out string result) && !string.IsNullOrEmpty(result);
        }

        public int DirectMetadataCount => Metadata.Count;
    }

    public class ProjectInstanceSnapshot
    {
        public ProjectInstanceSnapshot(ProjectInstance instance)
        {
            FullPath = instance.FullPath;
            DefaultTargets = instance.DefaultTargets;
            ProjectFileLocation = instance.ProjectFileLocation;
            GlobalPropertiesDictionary = instance.GlobalPropertiesDictionary;
            GlobalProperties = instance.GlobalProperties;
            ToolsVersion = instance.ToolsVersion;

            var innerBuildPropName = instance.GetPropertyValue(PropertyNames.InnerBuildProperty);
            var innerBuildPropValue = instance.GetPropertyValue(innerBuildPropName);

            var innerBuildPropValues = instance.GetPropertyValue(PropertyNames.InnerBuildPropertyValues);
            var innerBuildPropValuesValue = instance.GetPropertyValue(innerBuildPropValues);

            var isOuterBuild = string.IsNullOrWhiteSpace(innerBuildPropValue) && !string.IsNullOrWhiteSpace(innerBuildPropValuesValue);
            var isInnerBuild = !string.IsNullOrWhiteSpace(innerBuildPropValue);

            ProjectType = isOuterBuild
                ? ProjectType.OuterBuild
                : isInnerBuild
                    ? ProjectType.InnerBuild
                    : ProjectType.NonMultitargeting;

            Targets = instance.Targets.Keys.ToList();
            Properties = new()
                {
                    { AddTransitiveProjectReferencesInStaticGraphPropertyName, instance.GetPropertyValue(AddTransitiveProjectReferencesInStaticGraphPropertyName) },
                    { EnableDynamicPlatformResolutionPropertyName, instance.GetPropertyValue(EnableDynamicPlatformResolutionPropertyName) },
                    { "TargetFrameworks", instance.GetPropertyValue("TargetFrameworks") },
                    { PropertyNames.InnerBuildProperty, innerBuildPropName },
                    { PropertyNames.InnerBuildPropertyValues, innerBuildPropValues },
                    { "UsingMicrosoftNETSdk", instance.GetPropertyValue("UsingMicrosoftNETSdk") },
                    { "DisableTransitiveProjectReferences", instance.GetPropertyValue("DisableTransitiveProjectReferences") },
                    { SolutionProjectGenerator.CurrentSolutionConfigurationContents, instance.GetPropertyValue(SolutionProjectGenerator.CurrentSolutionConfigurationContents) },
                    { "Platform", instance.GetPropertyValue("Platform") },
                    { "Configuration", instance.GetPropertyValue("Configuration") },
                    { "PlatformLookupTable", instance.GetPropertyValue("PlatformLookupTable") },
                    { "ShouldUnsetParentConfigurationAndPlatform", instance.GetPropertyValue("ShouldUnsetParentConfigurationAndPlatform") },
                };

            if (!string.IsNullOrEmpty(innerBuildPropName))
            {
                Properties[innerBuildPropName] = innerBuildPropValue;
            }

            if (!string.IsNullOrEmpty(innerBuildPropValues))
            {
                Properties[innerBuildPropValues] = innerBuildPropValuesValue;
            }

            var projectReferenceTargets = instance.GetItems(ItemTypeNames.ProjectReference).ToList();

            ProjectReferenceByTargets = new(projectReferenceTargets.Count) { };

            foreach (ProjectItemInstance projectReferenceTarget in instance.GetItems(ItemTypeNames.ProjectReference))
            {
                ProjectReferenceByTargets.Add(new ProjectReferenceSnapshot(projectReferenceTarget)
                {
                    ItemType = projectReferenceTarget.ItemType,
                    EvaluatedInclude = projectReferenceTarget.EvaluatedInclude,
                });
            }

            var items = instance.GetItems(ItemTypeNames.ProjectCachePlugin);
            ProjectCacheDescriptors = new(items.Count);
            foreach (ProjectItemInstance item in items)
            {
                string pluginPath = FileUtilities.NormalizePath(System.IO.Path.Combine(item.Project.Directory, item.EvaluatedInclude));

                var pluginSettings = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
                foreach (ProjectMetadataInstance metadatum in item.Metadata)
                {
                    pluginSettings.Add(metadatum.Name, metadatum.EvaluatedValue);
                }

                ProjectCacheDescriptors.Add(ProjectCacheDescriptor.FromAssemblyPath(pluginPath, pluginSettings));
            }
        }

        public string FullPath;
        public string ToolsVersion;
        public List<string> Targets;
        internal ProjectType ProjectType;
        public List<string> DefaultTargets;
        public ElementLocation ProjectFileLocation;
        internal Collections.PropertyDictionary<ProjectPropertyInstance> GlobalPropertiesDictionary;
        public IDictionary<string, string> GlobalProperties;
        public Dictionary<string, string> Properties;
        public List<ProjectReferenceSnapshot> ProjectReferenceByTargets;
        public List<ProjectCacheDescriptor> ProjectCacheDescriptors;

        public string GetPropertyValue(string propertyName)
        {
            if (Properties.TryGetValue(propertyName, out string result))
            {
                return result;
            }

            // throw new System.Exception($"Property '{propertyName}' not found in '{FullPath}' project snapshot.");
            return string.Empty;
        }
    }

    /// <summary>
    /// Represents the node for a particular project in a project graph.
    /// A node is defined by (ProjectPath, ToolsVersion, GlobalProperties).
    /// </summary>
    [DebuggerDisplay(@"{DebugString()}")]
    public sealed class ProjectGraphNode
    {
        private readonly HashSet<ProjectGraphNode> _projectReferences = new HashSet<ProjectGraphNode>();
        private readonly HashSet<ProjectGraphNode> _referencingProjects = new HashSet<ProjectGraphNode>();

        // No public creation.
        internal ProjectGraphNode(ProjectInstance projectInstance)
        {
            ErrorUtilities.VerifyThrowInternalNull(projectInstance, nameof(projectInstance));
            ProjectInstance = new(projectInstance);
        }

        /// <summary>
        /// Gets an unordered collection of graph nodes for projects which this project references.
        /// </summary>
        public IReadOnlyCollection<ProjectGraphNode> ProjectReferences => _projectReferences;

        /// <summary>
        /// Gets a list of graph nodes for projects that have a project reference for this project
        /// </summary>
        public IReadOnlyCollection<ProjectGraphNode> ReferencingProjects => _referencingProjects;


        /// <summary>
        /// Gets the evaluated project instance represented by this node in the graph.
        /// </summary>
        public ProjectInstanceSnapshot ProjectInstance { get; }

        private string DebugString()
        {
            var truncatedProjectFile = FileUtilities.TruncatePathToTrailingSegments(ProjectInstance.FullPath, 2);

            return
                $"{truncatedProjectFile}, #GlobalProps={ProjectInstance.GlobalProperties.Count}, #Props={ProjectInstance.Properties.Count}, #in={ReferencingProjects.Count}, #out={ProjectReferences.Count}";
        }

        internal void AddProjectReference(ProjectGraphNode reference, ProjectReferenceSnapshot projectReferenceItem, GraphBuilder.GraphEdges edges)
        {
            _projectReferences.Add(reference);
            reference._referencingProjects.Add(this);

            edges.AddOrUpdateEdge((this, reference), projectReferenceItem);
        }

        internal void RemoveReference(ProjectGraphNode reference, GraphBuilder.GraphEdges edges)
        {
            _projectReferences.Remove(reference);
            reference._referencingProjects.Remove(reference);

            edges.RemoveEdge((this, reference));
        }

        internal void RemoveReferences(GraphBuilder.GraphEdges edges)
        {
            foreach (var reference in _projectReferences)
            {
                ErrorUtilities.VerifyThrow(reference._referencingProjects.Contains(this), "references should point to the nodes referencing them");
                reference._referencingProjects.Remove(this);

                edges.RemoveEdge((this, reference));
            }

            _projectReferences.Clear();
        }

        internal ConfigurationMetadata ToConfigurationMetadata()
        {
            return new ConfigurationMetadata(ProjectInstance.FullPath, ProjectInstance.GlobalPropertiesDictionary);
        }
    }
}
