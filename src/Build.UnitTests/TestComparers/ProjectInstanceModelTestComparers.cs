// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Construction;
using Microsoft.Build.Execution;
using Microsoft.Build.UnitTests;
using EvaluatorData =
    Microsoft.Build.Evaluation.IEvaluatorData<Microsoft.Build.Execution.ProjectPropertyInstance, Microsoft.Build.Execution.ProjectItemInstance,
        Microsoft.Build.Execution.ProjectMetadataInstance, Microsoft.Build.Execution.ProjectItemDefinitionInstance>;

#nullable disable

namespace Microsoft.Build.Engine.UnitTests.TestComparers
{
    public static class ProjectInstanceModelTestComparers
    {
        public class ProjectInstanceComparer : IEqualityComparer<ProjectInstance>
        {
            public bool Equals(ProjectInstance x, ProjectInstance y)
            {
                Assert.AreEqual(x.TranslateEntireState, y.TranslateEntireState);
                Assert.IsTrue(x.Properties.SequenceEqual(y.Properties, EqualityComparer<ProjectPropertyInstance>.Default));
                Assert.IsTrue(x.TestEnvironmentalProperties.SequenceEqual(y.TestEnvironmentalProperties, EqualityComparer<ProjectPropertyInstance>.Default));
                Helpers.AssertDictionariesEqual(x.GlobalProperties, y.GlobalProperties);
                ISet<string> globalToLocalX = ((EvaluatorData)x).GlobalPropertiesToTreatAsLocal;
                ISet<string> globalToLocalY = ((EvaluatorData)y).GlobalPropertiesToTreatAsLocal;
                Assert.IsTrue((globalToLocalX == null && globalToLocalY == null) || (globalToLocalX != null && globalToLocalY != null && globalToLocalX.SetEquals(globalToLocalY)));

                Assert.IsTrue(x.Items.ToArray().SequenceEqual(y.Items.ToArray(), ProjectItemInstance.ProjectItemInstanceEqualityComparer.Default));

                Helpers.AssertDictionariesEqual(
                    x.Targets,
                    y.Targets,
                    (xPair, yPair) =>
                    {
                        Assert.AreEqual(xPair.Key, yPair.Key);
                        Assert.AreEqual(xPair.Value, yPair.Value, new TargetComparer());
                    });
                Helpers.AssertDictionariesEqual(((EvaluatorData)x).BeforeTargets, ((EvaluatorData)y).BeforeTargets, AssertTargetSpecificationPairsEqual);
                Helpers.AssertDictionariesEqual(((EvaluatorData)x).AfterTargets, ((EvaluatorData)y).AfterTargets, AssertTargetSpecificationPairsEqual);
                CollectionAssert.AreEqual(x.DefaultTargets, y.DefaultTargets);
                CollectionAssert.AreEqual(x.InitialTargets, y.InitialTargets);

                Assert.AreEqual(x.Toolset, y.Toolset, new TaskRegistryComparers.ToolsetComparer());
                Assert.AreEqual(x.UsingDifferentToolsVersionFromProjectFile, y.UsingDifferentToolsVersionFromProjectFile);
                Assert.AreEqual(x.ExplicitToolsVersionSpecified, y.ExplicitToolsVersionSpecified);
                Assert.AreEqual(x.OriginalProjectToolsVersion, y.OriginalProjectToolsVersion);
                Assert.AreEqual(x.SubToolsetVersion, y.SubToolsetVersion);

                Assert.AreEqual(x.Directory, y.Directory);
                Assert.AreEqual(x.ProjectFileLocation, y.ProjectFileLocation, new Helpers.ElementLocationComparerIgnoringType());
                Assert.AreEqual(x.TaskRegistry, y.TaskRegistry, new TaskRegistryComparers.TaskRegistryComparer());
                Assert.AreEqual(x.IsImmutable, y.IsImmutable);

                Helpers.AssertDictionariesEqual(x.ItemDefinitions, y.ItemDefinitions,
                    (xPair, yPair) =>
                    {
                        Assert.AreEqual(xPair.Key, yPair.Key);
                        Assert.AreEqual(xPair.Value, yPair.Value, new ItemDefinitionComparer());
                    });

                return true;
            }

            private void AssertTargetSpecificationPairsEqual(KeyValuePair<string, List<TargetSpecification>> xPair, KeyValuePair<string, List<TargetSpecification>> yPair)
            {
                Assert.AreEqual(xPair.Key, yPair.Key);
                Assert.IsTrue(xPair.Value.SequenceEqual(yPair.Value, new TargetSpecificationComparer()));
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
                Assert.AreEqual(x.Name, y.Name);
                Assert.AreEqual(x.Condition, y.Condition);
                Assert.AreEqual(x.Inputs, y.Inputs);
                Assert.AreEqual(x.Outputs, y.Outputs);
                Assert.AreEqual(x.Returns, y.Returns);
                Assert.AreEqual(x.KeepDuplicateOutputs, y.KeepDuplicateOutputs);
                Assert.AreEqual(x.DependsOnTargets, y.DependsOnTargets);
                Assert.AreEqual(x.BeforeTargets, y.BeforeTargets);
                Assert.AreEqual(x.AfterTargets, y.AfterTargets);
                Assert.AreEqual(x.ParentProjectSupportsReturnsAttribute, y.ParentProjectSupportsReturnsAttribute);
                Assert.AreEqual(x.Location, y.Location, new Helpers.ElementLocationComparerIgnoringType());
                Assert.AreEqual(x.ConditionLocation, y.ConditionLocation, new Helpers.ElementLocationComparerIgnoringType());
                Assert.AreEqual(x.InputsLocation, y.InputsLocation, new Helpers.ElementLocationComparerIgnoringType());
                Assert.AreEqual(x.OutputsLocation, y.OutputsLocation, new Helpers.ElementLocationComparerIgnoringType());
                Assert.AreEqual(x.ReturnsLocation, y.ReturnsLocation, new Helpers.ElementLocationComparerIgnoringType());
                Assert.AreEqual(x.KeepDuplicateOutputsLocation, y.KeepDuplicateOutputsLocation, new Helpers.ElementLocationComparerIgnoringType());
                Assert.AreEqual(x.DependsOnTargetsLocation, y.DependsOnTargetsLocation, new Helpers.ElementLocationComparerIgnoringType());
                Assert.AreEqual(x.BeforeTargetsLocation, y.BeforeTargetsLocation, new Helpers.ElementLocationComparerIgnoringType());
                Assert.AreEqual(x.AfterTargetsLocation, y.AfterTargetsLocation, new Helpers.ElementLocationComparerIgnoringType());

                Assert.IsTrue(x.Children.SequenceEqual(y.Children, new TargetChildComparer()));
                Assert.IsTrue(x.OnErrorChildren.SequenceEqual(y.OnErrorChildren, new TargetOnErrorComparer()));

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
                    return new TargetItemGroupComparer().Equals((ProjectItemGroupTaskInstance)x, (ProjectItemGroupTaskInstance)y);
                }

                if (x is ProjectPropertyGroupTaskInstance)
                {
                    return new TargetPropertyGroupComparer().Equals((ProjectPropertyGroupTaskInstance)x, (ProjectPropertyGroupTaskInstance)y);
                }

                if (x is ProjectOnErrorInstance)
                {
                    return new TargetOnErrorComparer().Equals((ProjectOnErrorInstance)x, (ProjectOnErrorInstance)y);
                }

                if (x is ProjectTaskInstance)
                {
                    return new TargetTaskComparer().Equals((ProjectTaskInstance)x, (ProjectTaskInstance)y);
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
                Assert.AreEqual(x.Condition, y.Condition);
                Assert.AreEqual(x.Location, y.Location, new Helpers.ElementLocationComparerIgnoringType());
                Assert.AreEqual(x.ConditionLocation, y.ConditionLocation, new Helpers.ElementLocationComparerIgnoringType());

                Assert.IsTrue(x.Items.SequenceEqual(y.Items, new TargetItemComparer()));

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
                Assert.AreEqual(x.ItemType, y.ItemType);
                Assert.AreEqual(x.Include, y.Include);
                Assert.AreEqual(x.Exclude, y.Exclude);
                Assert.AreEqual(x.Remove, y.Remove);
                Assert.AreEqual(x.KeepMetadata, y.KeepMetadata);
                Assert.AreEqual(x.RemoveMetadata, y.RemoveMetadata);
                Assert.AreEqual(x.KeepDuplicates, y.KeepDuplicates);
                Assert.AreEqual(x.Condition, y.Condition);
                Assert.AreEqual(x.Location, y.Location, new Helpers.ElementLocationComparerIgnoringType());
                Assert.AreEqual(x.IncludeLocation, y.IncludeLocation, new Helpers.ElementLocationComparerIgnoringType());
                Assert.AreEqual(x.ExcludeLocation, y.ExcludeLocation, new Helpers.ElementLocationComparerIgnoringType());
                Assert.AreEqual(x.RemoveLocation, y.RemoveLocation, new Helpers.ElementLocationComparerIgnoringType());
                Assert.AreEqual(x.KeepMetadataLocation, y.KeepMetadataLocation, new Helpers.ElementLocationComparerIgnoringType());
                Assert.AreEqual(x.RemoveMetadataLocation, y.RemoveMetadataLocation, new Helpers.ElementLocationComparerIgnoringType());
                Assert.AreEqual(x.ConditionLocation, y.ConditionLocation, new Helpers.ElementLocationComparerIgnoringType());

                Assert.AreEqual(x.MatchOnMetadata, y.MatchOnMetadata);
                Assert.AreEqual(x.MatchOnMetadataLocation, y.MatchOnMetadataLocation, new Helpers.ElementLocationComparerIgnoringType());

                Assert.AreEqual(x.MatchOnMetadataOptions, y.MatchOnMetadataOptions);
                Assert.AreEqual(x.MatchOnMetadataOptionsLocation, y.MatchOnMetadataOptionsLocation, new Helpers.ElementLocationComparerIgnoringType());

                Assert.IsTrue(x.Metadata.SequenceEqual(y.Metadata, new TargetItemMetadataComparer()));

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
                Assert.AreEqual(x.Name, y.Name);
                Assert.AreEqual(x.Value, y.Value);
                Assert.AreEqual(x.Condition, y.Condition);
                Assert.AreEqual(x.Location, y.Location, new Helpers.ElementLocationComparerIgnoringType());
                Assert.AreEqual(x.ConditionLocation, y.ConditionLocation, new Helpers.ElementLocationComparerIgnoringType());

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
                Assert.AreEqual(x.Condition, y.Condition);
                Assert.AreEqual(x.Location, y.Location, new Helpers.ElementLocationComparerIgnoringType());
                Assert.AreEqual(x.ConditionLocation, y.ConditionLocation, new Helpers.ElementLocationComparerIgnoringType());

                Assert.IsTrue(x.Properties.SequenceEqual(y.Properties, new TargetPropertyComparer()));

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
            Assert.AreEqual(x.ItemType, y.ItemType);
            Assert.IsTrue(x.Metadata.SequenceEqual(y.Metadata, EqualityComparer<ProjectMetadataInstance>.Default));

            return true;
        }

        public int GetHashCode(ProjectItemDefinitionInstance obj)
        {
            throw new NotImplementedException();
        }
    }

    internal sealed class TargetSpecificationComparer : IEqualityComparer<TargetSpecification>
    {
        public bool Equals(TargetSpecification x, TargetSpecification y)
        {
            Assert.AreEqual(x.TargetName, y.TargetName);
            Assert.AreEqual(x.ReferenceLocation, y.ReferenceLocation, new Helpers.ElementLocationComparerIgnoringType());

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
            Assert.AreEqual(x.Name, y.Name);
            Assert.AreEqual(x.Value, y.Value);
            Assert.AreEqual(x.Condition, y.Condition);
            Assert.AreEqual(x.Location, y.Location, new Helpers.ElementLocationComparerIgnoringType());
            Assert.AreEqual(x.ConditionLocation, y.ConditionLocation, new Helpers.ElementLocationComparerIgnoringType());

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
            Assert.AreEqual(x.ExecuteTargets, y.ExecuteTargets);
            Assert.AreEqual(x.Condition, y.Condition);
            Assert.AreEqual(x.Location, y.Location, new Helpers.ElementLocationComparerIgnoringType());
            Assert.AreEqual(x.ConditionLocation, y.ConditionLocation, new Helpers.ElementLocationComparerIgnoringType());
            Assert.AreEqual(x.ExecuteTargetsLocation, y.ExecuteTargetsLocation, new Helpers.ElementLocationComparerIgnoringType());

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
            Assert.AreEqual(x.Name, y.Name);
            Assert.AreEqual(x.Condition, y.Condition);
            Assert.AreEqual(x.ContinueOnError, y.ContinueOnError);
            Assert.AreEqual(x.MSBuildRuntime, y.MSBuildRuntime);
            Assert.AreEqual(x.MSBuildArchitecture, y.MSBuildArchitecture);
            Assert.AreEqual(x.Location, y.Location, new Helpers.ElementLocationComparerIgnoringType());
            Assert.AreEqual(x.ConditionLocation, y.ConditionLocation, new Helpers.ElementLocationComparerIgnoringType());
            Assert.AreEqual(x.ContinueOnErrorLocation, y.ContinueOnErrorLocation, new Helpers.ElementLocationComparerIgnoringType());
            Assert.AreEqual(x.MSBuildRuntimeLocation, y.MSBuildRuntimeLocation, new Helpers.ElementLocationComparerIgnoringType());
            Assert.AreEqual(x.MSBuildRuntimeLocation, y.MSBuildRuntimeLocation, new Helpers.ElementLocationComparerIgnoringType());

            Assert.IsTrue(x.Outputs.SequenceEqual(y.Outputs, new ProjectTaskInstanceChildComparer()));

            AssertParametersEqual(x.TestGetParameters, y.TestGetParameters);

            return true;
        }

        public int GetHashCode(ProjectTaskInstance obj)
        {
            throw new NotImplementedException();
        }

        private void AssertParametersEqual(IDictionary<string, (string, ElementLocation)> x, IDictionary<string, (string, ElementLocation)> y)
        {
            Assert.AreEqual(x.Count, y.Count);

            for (var i = 0; i < x.Count; i++)
            {
                var xPair = x.ElementAt(i);
                var yPair = y.ElementAt(i);

                Assert.AreEqual(xPair.Key, yPair.Key);
                Assert.AreEqual(xPair.Value.Item1, yPair.Value.Item1);
                Assert.AreEqual(xPair.Value.Item2, yPair.Value.Item2, new Helpers.ElementLocationComparerIgnoringType());
            }
        }

        public class ProjectTaskInstanceChildComparer : IEqualityComparer<ProjectTaskInstanceChild>
        {
            public bool Equals(ProjectTaskInstanceChild x, ProjectTaskInstanceChild y)
            {
                if (x is ProjectTaskOutputItemInstance)
                {
                    return new ProjectTaskOutputItemComparer().Equals((ProjectTaskOutputItemInstance)x, (ProjectTaskOutputItemInstance)y);
                }
                if (x is ProjectTaskOutputPropertyInstance)
                {
                    return new ProjectTaskOutputPropertyComparer().Equals((ProjectTaskOutputPropertyInstance)x, (ProjectTaskOutputPropertyInstance)y);
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
                Assert.AreEqual(x.ItemType, y.ItemType);
                Assert.AreEqual(x.TaskParameter, y.TaskParameter);
                Assert.AreEqual(x.Condition, y.Condition);
                Assert.AreEqual(x.ConditionLocation, y.ConditionLocation, new Helpers.ElementLocationComparerIgnoringType());
                Assert.AreEqual(x.Location, y.Location, new Helpers.ElementLocationComparerIgnoringType());
                Assert.AreEqual(x.TaskParameterLocation, y.TaskParameterLocation, new Helpers.ElementLocationComparerIgnoringType());
                Assert.AreEqual(x.ItemTypeLocation, y.ItemTypeLocation, new Helpers.ElementLocationComparerIgnoringType());

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
                Assert.AreEqual(x.PropertyName, y.PropertyName);
                Assert.AreEqual(x.TaskParameter, y.TaskParameter);
                Assert.AreEqual(x.Condition, y.Condition);
                Assert.AreEqual(x.ConditionLocation, y.ConditionLocation, new Helpers.ElementLocationComparerIgnoringType());
                Assert.AreEqual(x.Location, y.Location, new Helpers.ElementLocationComparerIgnoringType());
                Assert.AreEqual(x.PropertyNameLocation, y.PropertyNameLocation, new Helpers.ElementLocationComparerIgnoringType());
                Assert.AreEqual(x.TaskParameterLocation, y.TaskParameterLocation, new Helpers.ElementLocationComparerIgnoringType());

                return true;
            }

            public int GetHashCode(ProjectTaskOutputPropertyInstance obj)
            {
                throw new NotImplementedException();
            }
        }
    }
}
