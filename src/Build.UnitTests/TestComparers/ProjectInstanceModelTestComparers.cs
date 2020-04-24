// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Construction;
using Microsoft.Build.Execution;
using Microsoft.Build.UnitTests;
using Xunit;
using EvaluatorData =
    Microsoft.Build.Evaluation.IEvaluatorData<Microsoft.Build.Execution.ProjectPropertyInstance, Microsoft.Build.Execution.ProjectItemInstance,
        Microsoft.Build.Execution.ProjectMetadataInstance, Microsoft.Build.Execution.ProjectItemDefinitionInstance>;

namespace Microsoft.Build.Engine.UnitTests.TestComparers
{
    public static class ProjectInstanceModelTestComparers
    {
        public class ProjectInstanceComparer : IEqualityComparer<ProjectInstance>
        {
            public bool Equals(ProjectInstance x, ProjectInstance y)
            {
                Assert.Equal(x.TranslateEntireState, y.TranslateEntireState);
                Assert.Equal(x.Properties, y.Properties, EqualityComparer<ProjectPropertyInstance>.Default);
                Assert.Equal(x.TestEnvironmentalProperties, y.TestEnvironmentalProperties, EqualityComparer<ProjectPropertyInstance>.Default);
                Helpers.AssertDictionariesEqual(x.GlobalProperties, y.GlobalProperties);
                Assert.Equal(((EvaluatorData) x).GlobalPropertiesToTreatAsLocal, ((EvaluatorData) y).GlobalPropertiesToTreatAsLocal);

                Assert.Equal(x.Items.ToArray(), y.Items.ToArray(), ProjectItemInstance.ProjectItemInstanceEqualityComparer.Default);

                Helpers.AssertDictionariesEqual(
                    x.Targets,
                    y.Targets,
                    (xPair, yPair) =>
                    {
                        Assert.Equal(xPair.Key, yPair.Key);
                        Assert.Equal(xPair.Value, yPair.Value, new TargetComparer());
                    });
                Helpers.AssertDictionariesEqual(((EvaluatorData)x).BeforeTargets, ((EvaluatorData)y).BeforeTargets, AssertTargetSpecificationPairsEqual);
                Helpers.AssertDictionariesEqual(((EvaluatorData)x).AfterTargets, ((EvaluatorData)y).AfterTargets, AssertTargetSpecificationPairsEqual);
                Assert.Equal(x.DefaultTargets, y.DefaultTargets);
                Assert.Equal(x.InitialTargets, y.InitialTargets);

                Assert.Equal(x.Toolset, y.Toolset, new TaskRegistryComparers.ToolsetComparer());
                Assert.Equal(x.UsingDifferentToolsVersionFromProjectFile, y.UsingDifferentToolsVersionFromProjectFile);
                Assert.Equal(x.ExplicitToolsVersionSpecified, y.ExplicitToolsVersionSpecified);
                Assert.Equal(x.OriginalProjectToolsVersion, y.OriginalProjectToolsVersion);
                Assert.Equal(x.SubToolsetVersion, y.SubToolsetVersion);

                Assert.Equal(x.Directory, y.Directory);
                Assert.Equal(x.ProjectFileLocation, y.ProjectFileLocation, new Helpers.ElementLocationComparerIgnoringType());
                Assert.Equal(x.TaskRegistry, y.TaskRegistry, new TaskRegistryComparers.TaskRegistryComparer());
                Assert.Equal(x.IsImmutable, y.IsImmutable);

                Helpers.AssertDictionariesEqual(x.ItemDefinitions, y.ItemDefinitions,
                    (xPair, yPair) =>
                    {
                        Assert.Equal(xPair.Key, yPair.Key);
                        Assert.Equal(xPair.Value, yPair.Value, new ItemDefinitionComparer());
                    });

                return true;
            }

            private void AssertTargetSpecificationPairsEqual(KeyValuePair<string, List<TargetSpecification>> xPair, KeyValuePair<string, List<TargetSpecification>> yPair)
            {
                Assert.Equal(xPair.Key, yPair.Key);
                Assert.Equal(xPair.Value, yPair.Value, new TargetSpecificationComparer());
            }

            public int GetHashCode(ProjectInstance obj)
            {
                throw new NotImplementedException();
            }
        }

        public class TargetComparer : IEqualityComparer<ProjectTargetInstance>
        {
            public bool Equals(ProjectTargetInstance x, ProjectTargetInstance y)
            {
                Assert.Equal(x.Name, y.Name);
                Assert.Equal(x.Condition, y.Condition);
                Assert.Equal(x.Inputs, y.Inputs);
                Assert.Equal(x.Outputs, y.Outputs);
                Assert.Equal(x.Returns, y.Returns);
                Assert.Equal(x.KeepDuplicateOutputs, y.KeepDuplicateOutputs);
                Assert.Equal(x.DependsOnTargets, y.DependsOnTargets);
                Assert.Equal(x.BeforeTargets, y.BeforeTargets);
                Assert.Equal(x.AfterTargets, y.AfterTargets);
                Assert.Equal(x.ParentProjectSupportsReturnsAttribute, y.ParentProjectSupportsReturnsAttribute);
                Assert.Equal(x.Location, y.Location, new Helpers.ElementLocationComparerIgnoringType());
                Assert.Equal(x.ConditionLocation, y.ConditionLocation, new Helpers.ElementLocationComparerIgnoringType());
                Assert.Equal(x.InputsLocation, y.InputsLocation, new Helpers.ElementLocationComparerIgnoringType());
                Assert.Equal(x.OutputsLocation, y.OutputsLocation, new Helpers.ElementLocationComparerIgnoringType());
                Assert.Equal(x.ReturnsLocation, y.ReturnsLocation, new Helpers.ElementLocationComparerIgnoringType());
                Assert.Equal(x.KeepDuplicateOutputsLocation, y.KeepDuplicateOutputsLocation, new Helpers.ElementLocationComparerIgnoringType());
                Assert.Equal(x.DependsOnTargetsLocation, y.DependsOnTargetsLocation, new Helpers.ElementLocationComparerIgnoringType());
                Assert.Equal(x.BeforeTargetsLocation, y.BeforeTargetsLocation, new Helpers.ElementLocationComparerIgnoringType());
                Assert.Equal(x.AfterTargetsLocation, y.AfterTargetsLocation, new Helpers.ElementLocationComparerIgnoringType());

                Assert.Equal(x.Children, y.Children, new TargetChildComparer());
                Assert.Equal(x.OnErrorChildren, y.OnErrorChildren, new TargetOnErrorComparer());

                return true;
            }

            public int GetHashCode(ProjectTargetInstance obj)
            {
                throw new NotImplementedException();
            }
        }

        public class TargetChildComparer : IEqualityComparer<ProjectTargetInstanceChild>
        {
            public bool Equals(ProjectTargetInstanceChild x, ProjectTargetInstanceChild y)
            {
                if (x is ProjectItemGroupTaskInstance)
                {
                    return new TargetItemGroupComparer().Equals((ProjectItemGroupTaskInstance) x, (ProjectItemGroupTaskInstance) y);
                }

                if (x is ProjectPropertyGroupTaskInstance)
                {
                    return new TargetPropertyGroupComparer().Equals((ProjectPropertyGroupTaskInstance) x, (ProjectPropertyGroupTaskInstance) y);
                }

                if (x is ProjectOnErrorInstance)
                {
                    return new TargetOnErrorComparer().Equals((ProjectOnErrorInstance) x, (ProjectOnErrorInstance) y);
                }

                if (x is ProjectTaskInstance)
                {
                    return new TargetTaskComparer().Equals((ProjectTaskInstance) x, (ProjectTaskInstance) y);
                }

                throw new NotImplementedException();
            }

            public int GetHashCode(ProjectTargetInstanceChild obj)
            {
                throw new NotImplementedException();
            }
        }

        public class TargetItemGroupComparer : IEqualityComparer<ProjectItemGroupTaskInstance>
        {
            public bool Equals(ProjectItemGroupTaskInstance x, ProjectItemGroupTaskInstance y)
            {
                Assert.Equal(x.Condition, y.Condition);
                Assert.Equal(x.Location, y.Location, new Helpers.ElementLocationComparerIgnoringType());
                Assert.Equal(x.ConditionLocation, y.ConditionLocation, new Helpers.ElementLocationComparerIgnoringType());

                Assert.Equal(x.Items, y.Items, new TargetItemComparer());

                return true;
            }

            public int GetHashCode(ProjectItemGroupTaskInstance obj)
            {
                throw new NotImplementedException();
            }
        }

        public class TargetItemComparer : IEqualityComparer<ProjectItemGroupTaskItemInstance>
        {
            public bool Equals(ProjectItemGroupTaskItemInstance x, ProjectItemGroupTaskItemInstance y)
            {
                Assert.Equal(x.ItemType, y.ItemType);
                Assert.Equal(x.Include, y.Include);
                Assert.Equal(x.Exclude, y.Exclude);
                Assert.Equal(x.Remove, y.Remove);
                Assert.Equal(x.KeepMetadata, y.KeepMetadata);
                Assert.Equal(x.RemoveMetadata, y.RemoveMetadata);
                Assert.Equal(x.KeepDuplicates, y.KeepDuplicates);
                Assert.Equal(x.Condition, y.Condition);
                Assert.Equal(x.Location, y.Location, new Helpers.ElementLocationComparerIgnoringType());
                Assert.Equal(x.IncludeLocation, y.IncludeLocation, new Helpers.ElementLocationComparerIgnoringType());
                Assert.Equal(x.ExcludeLocation, y.ExcludeLocation, new Helpers.ElementLocationComparerIgnoringType());
                Assert.Equal(x.RemoveLocation, y.RemoveLocation, new Helpers.ElementLocationComparerIgnoringType());
                Assert.Equal(x.KeepMetadataLocation, y.KeepMetadataLocation, new Helpers.ElementLocationComparerIgnoringType());
                Assert.Equal(x.RemoveMetadataLocation, y.RemoveMetadataLocation, new Helpers.ElementLocationComparerIgnoringType());
                Assert.Equal(x.ConditionLocation, y.ConditionLocation, new Helpers.ElementLocationComparerIgnoringType());

                Assert.Equal(x.Metadata, y.Metadata, new TargetItemMetadataComparer());

                return true;
            }

            public int GetHashCode(ProjectItemGroupTaskItemInstance obj)
            {
                throw new NotImplementedException();
            }
        }

        public class TargetItemMetadataComparer : IEqualityComparer<ProjectItemGroupTaskMetadataInstance>
        {
            public bool Equals(ProjectItemGroupTaskMetadataInstance x, ProjectItemGroupTaskMetadataInstance y)
            {
                Assert.Equal(x.Name, y.Name);
                Assert.Equal(x.Value, y.Value);
                Assert.Equal(x.Condition, y.Condition);
                Assert.Equal(x.Location, y.Location, new Helpers.ElementLocationComparerIgnoringType());
                Assert.Equal(x.ConditionLocation, y.ConditionLocation, new Helpers.ElementLocationComparerIgnoringType());

                return true;
            }

            public int GetHashCode(ProjectItemGroupTaskMetadataInstance obj)
            {
                throw new NotImplementedException();
            }
        }

        public class TargetPropertyGroupComparer : IEqualityComparer<ProjectPropertyGroupTaskInstance>
        {
            public bool Equals(ProjectPropertyGroupTaskInstance x, ProjectPropertyGroupTaskInstance y)
            {
                Assert.Equal(x.Condition, y.Condition);
                Assert.Equal(x.Location, y.Location, new Helpers.ElementLocationComparerIgnoringType());
                Assert.Equal(x.ConditionLocation, y.ConditionLocation, new Helpers.ElementLocationComparerIgnoringType());

                Assert.Equal(x.Properties, y.Properties, new TargetPropertyComparer());

                return true;
            }

            public int GetHashCode(ProjectPropertyGroupTaskInstance obj)
            {
                throw new NotImplementedException();
            }
        }
    }

    public class ItemDefinitionComparer : IEqualityComparer<ProjectItemDefinitionInstance>
    {
        public bool Equals(ProjectItemDefinitionInstance x, ProjectItemDefinitionInstance y)
        {
            Assert.Equal(x.ItemType, y.ItemType);
            Assert.Equal(x.Metadata, y.Metadata, EqualityComparer<ProjectMetadataInstance>.Default);

            return true;
        }

        public int GetHashCode(ProjectItemDefinitionInstance obj)
        {
            throw new NotImplementedException();
        }
    }

    internal class TargetSpecificationComparer : IEqualityComparer<TargetSpecification>
    {
        public bool Equals(TargetSpecification x, TargetSpecification y)
        {
            Assert.Equal(x.TargetName, y.TargetName);
            Assert.Equal(x.ReferenceLocation, y.ReferenceLocation, new Helpers.ElementLocationComparerIgnoringType());

            return true;
        }

        public int GetHashCode(TargetSpecification obj)
        {
            throw new NotImplementedException();
        }
    }

    public class TargetPropertyComparer : IEqualityComparer<ProjectPropertyGroupTaskPropertyInstance>
    {
        public bool Equals(ProjectPropertyGroupTaskPropertyInstance x, ProjectPropertyGroupTaskPropertyInstance y)
        {
            Assert.Equal(x.Name, y.Name);
            Assert.Equal(x.Value, y.Value);
            Assert.Equal(x.Condition, y.Condition);
            Assert.Equal(x.Location, y.Location, new Helpers.ElementLocationComparerIgnoringType());
            Assert.Equal(x.ConditionLocation, y.ConditionLocation, new Helpers.ElementLocationComparerIgnoringType());

            return true;
        }

        public int GetHashCode(ProjectPropertyGroupTaskPropertyInstance obj)
        {
            throw new NotImplementedException();
        }
    }

    public class TargetOnErrorComparer : IEqualityComparer<ProjectOnErrorInstance>
    {
        public bool Equals(ProjectOnErrorInstance x, ProjectOnErrorInstance y)
        {
            Assert.Equal(x.ExecuteTargets, y.ExecuteTargets);
            Assert.Equal(x.Condition, y.Condition);
            Assert.Equal(x.Location, y.Location, new Helpers.ElementLocationComparerIgnoringType());
            Assert.Equal(x.ConditionLocation, y.ConditionLocation, new Helpers.ElementLocationComparerIgnoringType());
            Assert.Equal(x.ExecuteTargetsLocation, y.ExecuteTargetsLocation, new Helpers.ElementLocationComparerIgnoringType());

            return true;
        }

        public int GetHashCode(ProjectOnErrorInstance obj)
        {
            throw new NotImplementedException();
        }
    }

    public class TargetTaskComparer : IEqualityComparer<ProjectTaskInstance>
    {
        public bool Equals(ProjectTaskInstance x, ProjectTaskInstance y)
        {
            Assert.Equal(x.Name, y.Name);
            Assert.Equal(x.Condition, y.Condition);
            Assert.Equal(x.ContinueOnError, y.ContinueOnError);
            Assert.Equal(x.MSBuildRuntime, y.MSBuildRuntime);
            Assert.Equal(x.MSBuildArchitecture, y.MSBuildArchitecture);
            Assert.Equal(x.Location, y.Location, new Helpers.ElementLocationComparerIgnoringType());
            Assert.Equal(x.ConditionLocation, y.ConditionLocation, new Helpers.ElementLocationComparerIgnoringType());
            Assert.Equal(x.ContinueOnErrorLocation, y.ContinueOnErrorLocation, new Helpers.ElementLocationComparerIgnoringType());
            Assert.Equal(x.MSBuildRuntimeLocation, y.MSBuildRuntimeLocation, new Helpers.ElementLocationComparerIgnoringType());
            Assert.Equal(x.MSBuildRuntimeLocation, y.MSBuildRuntimeLocation, new Helpers.ElementLocationComparerIgnoringType());

            Assert.Equal(x.Outputs, y.Outputs, new ProjectTaskInstanceChildComparer());

            AssertParametersEqual(x.TestGetParameters, y.TestGetParameters);

            return true;
        }

        public int GetHashCode(ProjectTaskInstance obj)
        {
            throw new NotImplementedException();
        }

        private void AssertParametersEqual(IDictionary<string, (string, ElementLocation)> x, IDictionary<string, (string, ElementLocation)> y)
        {
            Assert.Equal(x.Count, y.Count);

            for (var i = 0; i < x.Count; i++)
            {
                var xPair = x.ElementAt(i);
                var yPair = y.ElementAt(i);

                Assert.Equal(xPair.Key, yPair.Key);
                Assert.Equal(xPair.Value.Item1, yPair.Value.Item1);
                Assert.Equal(xPair.Value.Item2, yPair.Value.Item2, new Helpers.ElementLocationComparerIgnoringType());
            }
        }

        public class ProjectTaskInstanceChildComparer : IEqualityComparer<ProjectTaskInstanceChild>
        {
            public bool Equals(ProjectTaskInstanceChild x, ProjectTaskInstanceChild y)
            {
                if (x is ProjectTaskOutputItemInstance)
                {
                    return new ProjectTaskOutputItemComparer().Equals((ProjectTaskOutputItemInstance) x, (ProjectTaskOutputItemInstance) y);
                }
                if (x is ProjectTaskOutputPropertyInstance)
                {
                    return new ProjectTaskOutputPropertyComparer().Equals((ProjectTaskOutputPropertyInstance) x, (ProjectTaskOutputPropertyInstance) y);
                }

                throw new NotImplementedException();
            }

            public int GetHashCode(ProjectTaskInstanceChild obj)
            {
                throw new NotImplementedException();
            }
        }

        public class ProjectTaskOutputItemComparer : IEqualityComparer<ProjectTaskOutputItemInstance>
        {
            public bool Equals(ProjectTaskOutputItemInstance x, ProjectTaskOutputItemInstance y)
            {
                Assert.Equal(x.ItemType, y.ItemType);
                Assert.Equal(x.TaskParameter, y.TaskParameter);
                Assert.Equal(x.Condition, y.Condition);
                Assert.Equal(x.ConditionLocation, y.ConditionLocation, new Helpers.ElementLocationComparerIgnoringType());
                Assert.Equal(x.Location, y.Location, new Helpers.ElementLocationComparerIgnoringType());
                Assert.Equal(x.TaskParameterLocation, y.TaskParameterLocation, new Helpers.ElementLocationComparerIgnoringType());
                Assert.Equal(x.ItemTypeLocation, y.ItemTypeLocation, new Helpers.ElementLocationComparerIgnoringType());

                return true;
            }

            public int GetHashCode(ProjectTaskOutputItemInstance obj)
            {
                throw new NotImplementedException();
            }
        }

        public class ProjectTaskOutputPropertyComparer : IEqualityComparer<ProjectTaskOutputPropertyInstance>
        {
            public bool Equals(ProjectTaskOutputPropertyInstance x, ProjectTaskOutputPropertyInstance y)
            {
                Assert.Equal(x.PropertyName, y.PropertyName);
                Assert.Equal(x.TaskParameter, y.TaskParameter);
                Assert.Equal(x.Condition, y.Condition);
                Assert.Equal(x.ConditionLocation, y.ConditionLocation, new Helpers.ElementLocationComparerIgnoringType());
                Assert.Equal(x.Location, y.Location, new Helpers.ElementLocationComparerIgnoringType());
                Assert.Equal(x.PropertyNameLocation, y.PropertyNameLocation, new Helpers.ElementLocationComparerIgnoringType());
                Assert.Equal(x.TaskParameterLocation, y.TaskParameterLocation, new Helpers.ElementLocationComparerIgnoringType());

                return true;
            }

            public int GetHashCode(ProjectTaskOutputPropertyInstance obj)
            {
                throw new NotImplementedException();
            }
        }
    }
}
