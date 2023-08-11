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
    internal class ProjectItemInstanceSnapshot
    {
        public string FullPath = string.Empty;
        public string EvaluatedInclude = string.Empty;
        public string ItemType = string.Empty;

        private List<(string Name, string Value)> Metadata;

        public ProjectItemInstanceSnapshot(List<(string Name, string Value)> metadata)
        {
            Metadata = metadata;
        }

        public ProjectItemInstanceSnapshot(ProjectItemInstance projectItemInstance)
        {
            Metadata = new(projectItemInstance.MetadataCount);
            foreach (var metadata in projectItemInstance.Metadata)
            {
                Metadata.Add((metadata.Name, metadata.EvaluatedValue));
            }

            FullPath = projectItemInstance.GetMetadataValue(FullPathMetadataName);
        }

        public string GetMetadataValue(string metadataName)
        {
            // Note: FullPath is a special metadata that doesn't count towards the DirectMetadataCount.
            if (FullPathMetadataName == metadataName)
            {
                return FullPath;
            }

            // Note: caller expect empty string instead of null.
            var result = Metadata.FirstOrDefault((k) => k.Name == metadataName);
            return string.IsNullOrEmpty(result.Value) ? string.Empty : result.Value;
        }

        public void SetMetadata(string metadataName, string value)
        {
            RemoveMetadata(metadataName);
            Metadata.Add((metadataName, value));
        }

        public void RemoveMetadata(string metadataName)
        {
            int count = Metadata.Count;
            for (int i = 0; i < count; i++)
            {
                if (Metadata[i].Name == metadataName)
                {
                    Metadata.RemoveAt(i);
                    break;
                }
            }
        }

        public bool HasMetadata(string metadataName)
        {
            return Metadata.Any( (kv) => kv.Name == metadataName);
        }

        public int DirectMetadataCount => Metadata.Count;
    }

    internal class ProjectInstanceSnapshot
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
                    { BuildProjectInSolutionPropertyName, instance.GetPropertyValue(BuildProjectInSolutionPropertyName) },
                    { PropertyNames.InnerBuildProperty, innerBuildPropName },
                    { PropertyNames.InnerBuildPropertyValues, innerBuildPropValues },
                    { UsingMicrosoftNETSdkPropertyName, instance.GetPropertyValue(UsingMicrosoftNETSdkPropertyName) },
                    { DisableTransitiveProjectReferencesPropertyName, instance.GetPropertyValue(DisableTransitiveProjectReferencesPropertyName) },
                    { SolutionProjectGenerator.CurrentSolutionConfigurationContents, instance.GetPropertyValue(SolutionProjectGenerator.CurrentSolutionConfigurationContents) },
                    { PlatformMetadataName, instance.GetPropertyValue(PlatformMetadataName) },
                    { ConfigurationMetadataName, instance.GetPropertyValue(ConfigurationMetadataName) },
                    { PlatformLookupTableMetadataName, instance.GetPropertyValue(PlatformLookupTableMetadataName) },
                    { ShouldUnsetParentConfigurationAndPlatformPropertyName, instance.GetPropertyValue(ShouldUnsetParentConfigurationAndPlatformPropertyName) },
                };

            if (!string.IsNullOrEmpty(innerBuildPropName))
            {
                Properties[innerBuildPropName] = innerBuildPropValue;
            }

            if (!string.IsNullOrEmpty(innerBuildPropValues))
            {
                Properties[innerBuildPropValues] = innerBuildPropValuesValue;
            }

            var projectReferenceTargets = instance.GetItems(ItemTypeNames.ProjectReferenceTargets);
            ProjectReferenceByTargets = new(projectReferenceTargets.Count) { };
            foreach (ProjectItemInstance projectReferenceTarget in projectReferenceTargets)
            {
                ProjectReferenceByTargets.Add(new ProjectItemInstanceSnapshot(projectReferenceTarget)
                {
                    ItemType = projectReferenceTarget.ItemType,
                    EvaluatedInclude = projectReferenceTarget.EvaluatedInclude,
                });
            }

            var projectReferences = instance.GetItems(ItemTypeNames.ProjectReference);
            ProjectReference = new(projectReferences.Count) { };
            foreach (ProjectItemInstance projectReference in projectReferences)
            {
                ProjectReference.Add(new ProjectItemInstanceSnapshot(projectReference)
                {
                    ItemType = projectReference.ItemType,
                    EvaluatedInclude = projectReference.EvaluatedInclude,
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

        public int PropertiesCount => Properties.Count;
        public IDictionary<string, string> GlobalProperties;
        public List<ProjectItemInstanceSnapshot> ProjectReferenceByTargets;
        public List<ProjectItemInstanceSnapshot> ProjectReference;
        public List<ProjectCacheDescriptor> ProjectCacheDescriptors;

        private Dictionary<string, string> Properties;

        public string GetPropertyValue(string propertyName)
        {
            if (Properties.TryGetValue(propertyName, out string result))
            {
                return result;
            }

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
        internal ProjectGraphNode(ProjectInstance projectInstance, bool keepProjectInstance)
        {
            ErrorUtilities.VerifyThrowInternalNull(projectInstance, nameof(projectInstance));
            ProjectInstanceSnapshot = new(projectInstance);

            // Keep ProjectInstance for API compatibility
            if (keepProjectInstance)
            {
                ProjectInstance = projectInstance;
            }
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
        public ProjectInstance ProjectInstance { get; }

        /// <summary>
        /// Gets the evaluated project instance represented by this node in the graph.
        /// </summary>
        internal ProjectInstanceSnapshot ProjectInstanceSnapshot { get; }

        private string DebugString()
        {
            var truncatedProjectFile = FileUtilities.TruncatePathToTrailingSegments(ProjectInstanceSnapshot.FullPath, 2);

            return
                $"{truncatedProjectFile}, #GlobalProps={ProjectInstanceSnapshot.GlobalProperties.Count}, #Props={ProjectInstanceSnapshot.PropertiesCount}, #in={ReferencingProjects.Count}, #out={ProjectReferences.Count}";
        }

        internal void AddProjectReference(ProjectGraphNode reference, ProjectItemInstanceSnapshot projectReferenceItem, GraphBuilder.GraphEdges edges)
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
            return new ConfigurationMetadata(ProjectInstanceSnapshot.FullPath, ProjectInstanceSnapshot.GlobalPropertiesDictionary);
        }
    }
}
