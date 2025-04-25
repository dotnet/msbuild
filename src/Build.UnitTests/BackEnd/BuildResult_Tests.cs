// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Build.BackEnd;
using Microsoft.Build.Construction;
using Microsoft.Build.Engine.UnitTests.TestComparers;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Unittest;
using Xunit;
using TaskItem = Microsoft.Build.Execution.ProjectItemInstance.TaskItem;

namespace Microsoft.Build.UnitTests.BackEnd
{
    public class BuildResult_Tests
    {
        private int _nodeRequestId;

        public BuildResult_Tests()
        {
            _nodeRequestId = 1;
        }

        [Fact]
        public void TestConstructorGood()
        {
            BuildRequest request = CreateNewBuildRequest(1, Array.Empty<string>());
            BuildResult result2 = new BuildResult(request);
        }

        [Fact]
        public void Clone()
        {
            BuildRequest request = CreateNewBuildRequest(1, Array.Empty<string>());
            BuildResult result1 = new BuildResult(request);
            result1.ResultsByTarget?.Add("FOO", BuildResultUtilities.GetEmptySucceedingTargetResult());
            Assert.True(result1.ResultsByTarget?.ContainsKey("foo")); // test comparer

            BuildResult result2 = result1.Clone();

            result1.ResultsByTarget?.Add("BAR", BuildResultUtilities.GetEmptySucceedingTargetResult());
            Assert.True(result1.ResultsByTarget?.ContainsKey("foo")); // test comparer
            Assert.True(result1.ResultsByTarget?.ContainsKey("bar"));

            Assert.Equal(result1.SubmissionId, result2.SubmissionId);
            Assert.Equal(result1.ConfigurationId, result2.ConfigurationId);
            Assert.Equal(result1.GlobalRequestId, result2.GlobalRequestId);
            Assert.Equal(result1.ParentGlobalRequestId, result2.ParentGlobalRequestId);
            Assert.Equal(result1.NodeRequestId, result2.NodeRequestId);
            Assert.Equal(result1.CircularDependency, result2.CircularDependency);
            Assert.Equal(result1.ResultsByTarget?["foo"], result2.ResultsByTarget?["foo"]);
            Assert.Equal(result1.OverallResult, result2.OverallResult);
        }

        [Fact]
        public void TestConstructorBad()
        {
            Assert.Throws<NullReferenceException>(() =>
            {
                BuildResult result = new BuildResult(null!);
            });
        }
        [Fact]
        public void TestConfigurationId()
        {
            BuildRequest request = CreateNewBuildRequest(-1, Array.Empty<string>());
            BuildResult result = new BuildResult(request);
            Assert.Equal(-1, result.ConfigurationId);

            BuildRequest request2 = CreateNewBuildRequest(1, Array.Empty<string>());
            BuildResult result2 = new BuildResult(request2);
            Assert.Equal(1, result2.ConfigurationId);
        }

        [Fact]
        public void TestExceptionGood()
        {
            BuildRequest request = CreateNewBuildRequest(1, Array.Empty<string>());
            BuildResult result = new BuildResult(request);
            Assert.Null(result.Exception);
            AccessViolationException e = new AccessViolationException();
            result = new BuildResult(request, e);

            Assert.Equal(e, result.Exception);
        }

        [Fact]
        public void TestOverallResult()
        {
            BuildRequest request = CreateNewBuildRequest(1, Array.Empty<string>());
            BuildResult result = new BuildResult(request);
            Assert.Equal(BuildResultCode.Success, result.OverallResult);

            result.AddResultsForTarget("foo", BuildResultUtilities.GetEmptySucceedingTargetResult());
            Assert.Equal(BuildResultCode.Success, result.OverallResult);

            result.AddResultsForTarget("bar", new TargetResult(Array.Empty<TaskItem>(), new WorkUnitResult(WorkUnitResultCode.Success, WorkUnitActionCode.Continue, new Exception())));
            Assert.Equal(BuildResultCode.Success, result.OverallResult);

            result.AddResultsForTarget("baz", new TargetResult(Array.Empty<TaskItem>(), BuildResultUtilities.GetStopWithErrorResult(new Exception())));
            Assert.Equal(BuildResultCode.Failure, result.OverallResult);

            BuildRequest request2 = CreateNewBuildRequest(2, Array.Empty<string>());
            BuildResult result2 = new BuildResult(request2);
            result2.AddResultsForTarget("foo", BuildResultUtilities.GetEmptySucceedingTargetResult());
            result2.AddResultsForTarget("bar", BuildResultUtilities.GetEmptyFailingTargetResult());
            Assert.Equal(BuildResultCode.Failure, result2.OverallResult);
        }

        [Fact]
        public void TestPacketType()
        {
            BuildRequest request = CreateNewBuildRequest(1, Array.Empty<string>());
            BuildResult result = new BuildResult(request);
            Assert.Equal(NodePacketType.BuildResult, ((INodePacket)result).Type);
        }

        [Fact]
        public void TestAddAndRetrieve()
        {
            BuildRequest request = CreateNewBuildRequest(1, Array.Empty<string>());
            BuildResult result = new BuildResult(request);
            result.AddResultsForTarget("foo", BuildResultUtilities.GetEmptySucceedingTargetResult());
            result.AddResultsForTarget("bar", BuildResultUtilities.GetEmptyFailingTargetResult());

            Assert.Equal(TargetResultCode.Success, result["foo"].ResultCode);
            Assert.Equal(TargetResultCode.Failure, result["bar"].ResultCode);
        }

        [Fact]
        public void TestIndexerBad1()
        {
            Assert.Throws<KeyNotFoundException>(() =>
            {
                BuildRequest request = CreateNewBuildRequest(1, Array.Empty<string>());
                BuildResult result = new BuildResult(request);
                ITargetResult targetResult = result["foo"];
            });
        }

        [Fact]
        public void TestIndexerBad2()
        {
            Assert.Throws<KeyNotFoundException>(() =>
            {
                BuildRequest request = CreateNewBuildRequest(1, Array.Empty<string>());
                BuildResult result = new BuildResult(request);
                result.AddResultsForTarget("foo", BuildResultUtilities.GetEmptySucceedingTargetResult());
                ITargetResult targetResult = result["bar"];
            });
        }

        [Fact]
        public void TestAddResultsInvalid1()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                BuildRequest request = CreateNewBuildRequest(1, Array.Empty<string>());
                BuildResult result = new BuildResult(request);
                result.AddResultsForTarget(null!, BuildResultUtilities.GetEmptySucceedingTargetResult());
            });
        }

        [Fact]
        public void TestAddResultsInvalid2()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                BuildRequest request = CreateNewBuildRequest(1, Array.Empty<string>());
                BuildResult result = new BuildResult(request);
                result.AddResultsForTarget("foo", null!);
            });
        }

        [Fact]
        public void TestAddResultsInvalid3()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                BuildRequest request = CreateNewBuildRequest(1, Array.Empty<string>());
                BuildResult result = new BuildResult(request);
                result.AddResultsForTarget(null!, BuildResultUtilities.GetEmptySucceedingTargetResult());
            });
        }
        [Fact]
        public void TestMergeResults()
        {
            BuildRequest request = CreateNewBuildRequest(1, Array.Empty<string>());
            BuildResult result = new BuildResult(request);
            result.AddResultsForTarget("foo", BuildResultUtilities.GetEmptySucceedingTargetResult());

            BuildResult result2 = new BuildResult(request);
            result.AddResultsForTarget("bar", BuildResultUtilities.GetEmptyFailingTargetResult());

            result.MergeResults(result2);
            Assert.Equal(TargetResultCode.Success, result["foo"].ResultCode);
            Assert.Equal(TargetResultCode.Failure, result["bar"].ResultCode);

            BuildResult result3 = new BuildResult(request);
            result.MergeResults(result3);

            BuildResult result4 = new BuildResult(request);
            result4.AddResultsForTarget("xor", BuildResultUtilities.GetEmptySucceedingTargetResult());
            result.MergeResults(result4);
            Assert.Equal(TargetResultCode.Success, result["foo"].ResultCode);
            Assert.Equal(TargetResultCode.Failure, result["bar"].ResultCode);
            Assert.Equal(TargetResultCode.Success, result["xor"].ResultCode);
        }

        [Fact]
        public void TestMergeResultsBad1()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                BuildRequest request = CreateNewBuildRequest(1, Array.Empty<string>());
                BuildResult result = new BuildResult(request);
                result.AddResultsForTarget("foo", BuildResultUtilities.GetEmptySucceedingTargetResult());

                result.MergeResults(null!);
            });
        }

        [Fact]
        public void TestMergeResultsBad3()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                BuildRequest request = CreateNewBuildRequest(1, Array.Empty<string>());
                BuildResult result = new BuildResult(request);
                result.AddResultsForTarget("foo", BuildResultUtilities.GetEmptySucceedingTargetResult());

                BuildRequest request2 = CreateNewBuildRequest(2, Array.Empty<string>());
                BuildResult result2 = new BuildResult(request2);
                result2.AddResultsForTarget("bar", BuildResultUtilities.GetEmptySucceedingTargetResult());

                result.MergeResults(result2);
            });
        }
        [Fact]
        public void TestHasResultsForTarget()
        {
            BuildRequest request = CreateNewBuildRequest(1, Array.Empty<string>());
            BuildResult result = new BuildResult(request);
            result.AddResultsForTarget("foo", BuildResultUtilities.GetEmptySucceedingTargetResult());

            Assert.True(result.HasResultsForTarget("foo"));
            Assert.False(result.HasResultsForTarget("bar"));
        }

        [Fact]
        public void TestEnumerator()
        {
            BuildRequest request = CreateNewBuildRequest(1, Array.Empty<string>());
            BuildResult result = new BuildResult(request);
            int countFound = result.ResultsByTarget?.Count ?? 0;
            Assert.Equal(0, countFound);

            result.AddResultsForTarget("foo", BuildResultUtilities.GetEmptySucceedingTargetResult());
            bool foundFoo = false;
            countFound = 0;
            if (result.ResultsByTarget != null)
            {
                foreach (KeyValuePair<string, TargetResult> resultPair in result.ResultsByTarget)
                {
                    if (resultPair.Key == "foo")
                    {
                        foundFoo = true;
                    }

                    countFound++;
                }
            }

            Assert.Equal(1, countFound);
            Assert.True(foundFoo);

            result.AddResultsForTarget("bar", BuildResultUtilities.GetEmptySucceedingTargetResult());
            foundFoo = false;
            bool foundBar = false;
            countFound = 0;
            if (result.ResultsByTarget != null)
            {
                foreach (KeyValuePair<string, TargetResult> resultPair in result.ResultsByTarget)
                {
                    if (resultPair.Key == "foo")
                    {
                        Assert.False(foundFoo);
                        foundFoo = true;
                    }

                    if (resultPair.Key == "bar")
                    {
                        Assert.False(foundBar);
                        foundBar = true;
                    }

                    countFound++;
                }
            }

            Assert.Equal(2, countFound);
            Assert.True(foundFoo);
            Assert.True(foundBar);
        }

        [Fact]
        public void TestTranslation()
        {
            BuildRequest request = new BuildRequest(1, 1, 2, new string[] { "alpha", "omega" }, null, new BuildEventContext(1, 1, 2, 3, 4, 5), null);
            BuildResult result = new BuildResult(request, new BuildAbortedException());

            TaskItem fooTaskItem = new TaskItem("foo", "asdf.proj");
            fooTaskItem.SetMetadata("meta1", "metavalue1");
            fooTaskItem.SetMetadata("meta2", "metavalue2");

            result.InitialTargets = new List<string> { "a", "b" };
            result.DefaultTargets = new List<string> { "c", "d" };

            result.AddResultsForTarget("alpha", new TargetResult(new TaskItem[] { fooTaskItem }, BuildResultUtilities.GetSuccessResult()));
            result.AddResultsForTarget("omega", new TargetResult(Array.Empty<TaskItem>(), BuildResultUtilities.GetStopWithErrorResult(new ArgumentException("The argument was invalid"))));

            Assert.Equal(NodePacketType.BuildResult, (result as INodePacket).Type);
            ((ITranslatable)result).Translate(TranslationHelpers.GetWriteTranslator());
            INodePacket packet = BuildResult.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            BuildResult deserializedResult = (packet as BuildResult)!;

            Assert.Equal(result.ConfigurationId, deserializedResult.ConfigurationId);
            Assert.True(TranslationHelpers.CompareCollections(result.DefaultTargets, deserializedResult.DefaultTargets, StringComparer.Ordinal));
            Assert.True(TranslationHelpers.CompareExceptions(result.Exception, deserializedResult.Exception, out string diffReason), diffReason);
            Assert.Equal(result.Exception?.Message, deserializedResult.Exception?.Message);
            Assert.Equal(result.GlobalRequestId, deserializedResult.GlobalRequestId);
            Assert.True(TranslationHelpers.CompareCollections(result.InitialTargets, deserializedResult.InitialTargets, StringComparer.Ordinal));
            Assert.Equal(result.NodeRequestId, deserializedResult.NodeRequestId);
            Assert.Equal(result["alpha"].ResultCode, deserializedResult["alpha"].ResultCode);
            Assert.True(TranslationHelpers.CompareExceptions(result["alpha"].Exception, deserializedResult["alpha"].Exception, out diffReason), diffReason);
            Assert.True(TranslationHelpers.CompareCollections(result["alpha"].Items, deserializedResult["alpha"].Items, TaskItemComparer.Instance));
            Assert.Equal(result["omega"].ResultCode, deserializedResult["omega"].ResultCode);
            Assert.True(TranslationHelpers.CompareExceptions(result["omega"].Exception, deserializedResult["omega"].Exception, out diffReason), diffReason);
            Assert.True(TranslationHelpers.CompareCollections(result["omega"].Items, deserializedResult["omega"].Items, TaskItemComparer.Instance));
        }

        [Fact]
        public void TestMergeResultsWithProjectItems()
        {
            BuildRequest request = CreateNewBuildRequest(1, Array.Empty<string>());
            BuildResult result1 = new BuildResult(request);
            BuildResult result2 = new BuildResult(request);

            ProjectInstance project1 = new ProjectInstance(ProjectRootElement.Create());
            var item1 = project1.AddItem("B", "test");
            item1.SetMetadata("Meta1", "Value1");
            result1.ProjectStateAfterBuild = project1;

            ProjectInstance project2 = new ProjectInstance(ProjectRootElement.Create());
            var item2 = project2.AddItem("B", "test");
            item2.SetMetadata("Meta2", "Value2");
            item2.SetMetadata("Meta1", "UpdatedValue2");
            result2.ProjectStateAfterBuild = project2;

            result1.MergeResults(result2);

            var mergedItems = result1.ProjectStateAfterBuild.GetItems("B").ToList();
            Assert.Equal(1, mergedItems.Count);

            var mergedItem = mergedItems.FirstOrDefault(i => i.EvaluatedInclude == "test");
            Assert.NotNull(mergedItem);
            Assert.Equal("UpdatedValue2", mergedItem.GetMetadataValue("Meta1"));
            Assert.Equal("Value2", mergedItem.GetMetadataValue("Meta2"));
        }

        [Fact]
        public void TestMergeResultsWithProjectProperties()
        {
            BuildRequest request = CreateNewBuildRequest(1, Array.Empty<string>());
            BuildResult result1 = new BuildResult(request);
            BuildResult result2 = new BuildResult(request);

            ProjectInstance project1 = new ProjectInstance(ProjectRootElement.Create());
            var property1 = project1.SetProperty("A", "test1");
            result1.ProjectStateAfterBuild = project1;

            ProjectInstance project2 = new ProjectInstance(ProjectRootElement.Create());
            var property2 = project2.SetProperty("B", "test2");
            result2.ProjectStateAfterBuild = project2;

            result1.MergeResults(result2);

            var mergedProperty1 = result1.ProjectStateAfterBuild.GetProperty("A");
            Assert.NotNull(mergedProperty1);
            Assert.Equal("test1", mergedProperty1.EvaluatedValue);

            var mergedProperty2 = result1.ProjectStateAfterBuild.GetProperty("B");
            Assert.NotNull(mergedProperty2);
            Assert.Equal("test2", mergedProperty2.EvaluatedValue);
        }

        [Fact]
        public void TestMergeResultsWithRequestedProjectStatePropertyFilters()
        {
            // Create a request and two build results
            BuildRequest request = CreateNewBuildRequest(1, Array.Empty<string>());
            BuildResult result1 = new BuildResult(request);
            BuildResult result2 = new BuildResult(request);

            var filter1 = new RequestedProjectState();
            filter1.PropertyFilters = new List<string> { "Prop1" };
            filter1.ItemFilters = new Dictionary<string, List<string>>() { };

            var filter2 = new RequestedProjectState();
            filter2.PropertyFilters = new List<string> { "Prop2" };
            filter2.ItemFilters = new Dictionary<string, List<string>>() { };

            ProjectInstance project1 = new ProjectInstance(ProjectRootElement.Create());
            project1.SetProperty("Prop1", "Value1");
            project1.DefaultTargets.Add("Build");

            ProjectInstance project2 = new ProjectInstance(ProjectRootElement.Create());
            project2.SetProperty("Prop2", "Value2");
            project2.SetProperty("Prop3", "Value3");
            project2.DefaultTargets.Add("Build");

            result1.ProjectStateAfterBuild = project1.FilteredCopy(filter1);
            result2.ProjectStateAfterBuild = project2.FilteredCopy(filter2);

            result1.MergeResults(result2);

            var mergedFilter = result1.ProjectStateAfterBuild.RequestedProjectStateFilter;
            Assert.Contains("Prop1", mergedFilter.PropertyFilters);
            Assert.Contains("Prop2", mergedFilter.PropertyFilters);
            Assert.NotNull(result1.ProjectStateAfterBuild.GetProperty("Prop1"));
            Assert.NotNull(result1.ProjectStateAfterBuild.GetProperty("Prop2"));

            Assert.Null(result1.ProjectStateAfterBuild.GetProperty("Prop3"));
        }

        [Fact]
        public void TestMergeResultsWithItemFilters()
        {
            BuildRequest request = CreateNewBuildRequest(1, Array.Empty<string>());
            BuildResult result1 = new BuildResult(request);
            BuildResult result2 = new BuildResult(request);

            ProjectInstance project1 = new ProjectInstance(ProjectRootElement.Create());
            project1.AddItem("Compile", "File1.cs", new Dictionary<string, string> { { "CustomMetadata1", "Value1" }, });

            ProjectInstance project2 = new ProjectInstance(ProjectRootElement.Create());
            project2.AddItem("Resource1", "Resource1.resx", new Dictionary<string, string> { { "CustomMetadata2", "Value2" }, { "CustomMetadata3", "Value3" } });
            project2.AddItem("Resource2", "Resource2.resx", new Dictionary<string, string> { });

            var filter1 = new RequestedProjectState();
            filter1.ItemFilters = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase) { { "Compile", new List<string>() { "CustomMetadata1" } }, };

            var filter2 = new RequestedProjectState();
            filter2.ItemFilters = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase) { { "Resource1", new List<string>() { "CustomMetadata3" } }, };

            result1.ProjectStateAfterBuild = project1.FilteredCopy(filter1);
            result2.ProjectStateAfterBuild = project2.FilteredCopy(filter2);

            result1.MergeResults(result2);

            // Validate merged filters
            var mergedFilter = result1.ProjectStateAfterBuild.RequestedProjectStateFilter;
            Assert.Contains("Compile", mergedFilter.ItemFilters.Keys);
            Assert.Contains("Resource1", mergedFilter.ItemFilters.Keys);
            Assert.DoesNotContain("Resource2", mergedFilter.ItemFilters.Keys);

            // validate the filtered instance
            var mergedCompileItem = result1.ProjectStateAfterBuild.GetItems("Compile").ToList();
            Assert.Equal(1, mergedCompileItem.Count);
            Assert.Equal("File1.cs", mergedCompileItem.First().EvaluatedInclude);
            Assert.Equal("Value1", mergedCompileItem.First().GetMetadataValue("CustomMetadata1"));

            var mergedResourceItem = result1.ProjectStateAfterBuild.GetItems("Resource1").ToList();
            Assert.Equal(1, mergedResourceItem.Count);
            Assert.Equal("Resource1.resx", mergedResourceItem.First().EvaluatedInclude);
            Assert.Equal("Value3", mergedResourceItem.First().GetMetadataValue("CustomMetadata3"));
            Assert.Empty(mergedResourceItem.First().GetMetadataValue("CustomMetadata2"));
        }

        [Fact]
        public void TestMergeResultsWithOverlappingItemFilters()
        {
            // Create a request and two build results
            BuildRequest request = CreateNewBuildRequest(1, Array.Empty<string>());
            BuildResult result1 = new BuildResult(request);
            BuildResult result2 = new BuildResult(request);

            ProjectInstance project1 = new ProjectInstance(ProjectRootElement.Create());
            ProjectInstance project2 = new ProjectInstance(ProjectRootElement.Create());

            // Create filters with overlapping item types but different metadata
            var filter1 = new RequestedProjectState();
            filter1.ItemFilters = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                { "Compile", new List<string> { "CustomMetadata1" } }
            };

            var filter2 = new RequestedProjectState();
            filter2.ItemFilters = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                { "Compile", new List<string> { "CustomMetadata2" } }
            };

            // Create filtered copies
            var filteredProject1 = project1.FilteredCopy(filter1);
            var filteredProject2 = project2.FilteredCopy(filter2);

            // Assign filtered projects to results
            result1.ProjectStateAfterBuild = filteredProject1;
            result2.ProjectStateAfterBuild = filteredProject2;

            // Merge results
            result1.MergeResults(result2);

            // Verify the merged state has combined filters
            Assert.NotNull(result1.ProjectStateAfterBuild);
            Assert.NotNull(result1.ProjectStateAfterBuild.RequestedProjectStateFilter);

            // The merged filter should contain both metadata for the Compile item type
            var mergedFilter = result1.ProjectStateAfterBuild.RequestedProjectStateFilter;
            Assert.Contains("CustomMetadata1", mergedFilter.ItemFilters["Compile"]);
            Assert.Contains("CustomMetadata2", mergedFilter.ItemFilters["Compile"]);
            Assert.Equal(2, mergedFilter.ItemFilters["Compile"].Count);
        }

        [Fact]
        public void TestMergeResultsWithBothPropertyAndItemFilters()
        {
            // Create a request and two build results
            BuildRequest request = CreateNewBuildRequest(1, Array.Empty<string>());
            BuildResult result1 = new BuildResult(request);
            BuildResult result2 = new BuildResult(request);

            // Create ProjectInstances
            ProjectInstance project1 = new ProjectInstance(ProjectRootElement.Create());
            ProjectInstance project2 = new ProjectInstance(ProjectRootElement.Create());

            // Create filter with property filters
            var filter1 = new RequestedProjectState();
            filter1.PropertyFilters = new List<string> { "Prop1", "Prop2" };

            // Create filter with item filters
            var filter2 = new RequestedProjectState();
            filter2.ItemFilters = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                { "Compile", new List<string> { "CustomMetadata1" } }
            };

            // Create filtered copies
            var filteredProject1 = project1.FilteredCopy(filter1);
            var filteredProject2 = project2.FilteredCopy(filter2);

            // Assign filtered projects to results
            result1.ProjectStateAfterBuild = filteredProject1;
            result2.ProjectStateAfterBuild = filteredProject2;

            // Merge results
            result1.MergeResults(result2);

            // Verify the merged state has both property and item filters
            Assert.NotNull(result1.ProjectStateAfterBuild);
            Assert.NotNull(result1.ProjectStateAfterBuild.RequestedProjectStateFilter);

            // The merged filter should have both property and item filters
            var mergedFilter = result1.ProjectStateAfterBuild.RequestedProjectStateFilter;
            Assert.NotNull(mergedFilter.PropertyFilters);
            Assert.NotNull(mergedFilter.ItemFilters);

            // Check property filters
            Assert.Contains("Prop1", mergedFilter.PropertyFilters);
            Assert.Contains("Prop2", mergedFilter.PropertyFilters);

            // Check item filters
            Assert.Contains("Compile", mergedFilter.ItemFilters.Keys);
            Assert.Contains("CustomMetadata1", mergedFilter.ItemFilters["Compile"]);
        }

        private BuildRequest CreateNewBuildRequest(int configurationId, string[] targets) => new BuildRequest(1 /* submissionId */, _nodeRequestId++, configurationId, targets, null, BuildEventContext.Invalid, null);
    }
}
