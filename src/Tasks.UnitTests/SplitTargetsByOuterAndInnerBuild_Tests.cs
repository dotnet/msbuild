// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    public sealed class SplitTargetsByOuterAndInnerBuild_Tests : IDisposable
    {
        public SplitTargetsByOuterAndInnerBuild_Tests()
        {
            _env = TestEnvironment.Create();
            _env.DoNotLaunchDebugger();
        }

        public void Dispose()
        {
            _env.Dispose();
        }

        private static readonly ProjectReferenceTargetItem[] _projectReferenceTargets =
        {
            new ProjectReferenceTargetItem("a", "xi;yi", true),
            new ProjectReferenceTargetItem("b", "z;t", false),
            new ProjectReferenceTargetItem("c", "ki", true)
        };

        private readonly TestEnvironment _env;

        public readonly struct ProjectReferenceTargetItem
        {
            public string From { get; }
            public string To { get; }
            public bool IsInnerBuild { get; }
            public TaskItem TaskItem { get; }


            public ProjectReferenceTargetItem(string from, string to, bool isInnerBuild)
            {
                From = from;
                To = to;
                IsInnerBuild = isInnerBuild;

                TaskItem = new TaskItem(from);

                if (to != null)
                {
                    TaskItem.SetMetadata(ItemMetadataNames.ProjectReferenceTargetsMetadataName, to);
                }

                if (isInnerBuild)
                {
                    TaskItem.SetMetadata(ItemMetadataNames.ProjectReferenceTargetsInnerBuild, "true");
                }
            }
        }

        public static IEnumerable<object[]> TaskShouldSplitTargetsByOuterAndInnerBuildData
        {
            get
            {
                yield return new object[]
                {
                    new[] {"ki", "t", "yi", "z", "xi"},
                    new string[0],
                    _projectReferenceTargets
                    ,
                    new[] {"ki", "yi", "xi"},
                    new[] {"t", "z"}
                };

                yield return new object[]
                {
                    new[] {"t"},
                    new string[0],
                    _projectReferenceTargets
                    ,
                    new string[0],
                    new[] {"t"}
                };

                yield return new object[]
                {
                    new[] {"yi"},
                    new string[0],
                    _projectReferenceTargets
                    ,
                    new[] {"yi"},
                    new string[0]
                };
            }
        }

        [Theory]
        [MemberData(nameof(TaskShouldSplitTargetsByOuterAndInnerBuildData))]
        public void TaskShouldSplitTargetsByOuterAndInnerBuild(
            string[] entryTargets,
            string[] defaultTargets,
            ProjectReferenceTargetItem[] prt,
            string[] expectedInnerBuildTargets,
            string[] expectedOuterBuildTargets)
        {
            AssertTaskOutputs(entryTargets, defaultTargets, prt, expectedInnerBuildTargets, expectedOuterBuildTargets);
        }

        public static IEnumerable<object[]> TaskShouldBeAbleToExpandDefaultTargetPlaceholderData
        {
            get
            {
                ProjectReferenceTargetItem[] prt2 =
                {
                    new ProjectReferenceTargetItem("a", "x;.default;y", true)
                };

                yield return new object[]
                {
                    new[] {"x"},
                    new[] {"x"},
                    new[]
                    {
                        new ProjectReferenceTargetItem("a", ".default", true)
                    },
                    new[] {"x"},
                    new string[0]
                };

                yield return new object[]
                {
                    new[] {"x"},
                    new[] {"x"},
                    new[]
                    {
                        new ProjectReferenceTargetItem("a", ".default", false)
                    },
                    new string[0],
                    new[] {"x"}
                };

                yield return new object[]
                {
                    new[] {"y"},
                    new[] {"x", "y"},
                    new[]
                    {
                        new ProjectReferenceTargetItem("a", ".default", true)
                    },
                    new[] {"y"},
                    new string[0]
                };

                yield return new object[]
                {
                    new[] {"y", "x"},
                    new[] {"x", "y"},
                    new[]
                    {
                        new ProjectReferenceTargetItem("a", ".default", true)
                    },
                    new[] {"x", "y"},
                    new string[0]
                };

                yield return new object[]
                {
                    new[] {"y", "x"},
                    new[] {"t", "r"},
                    new[]
                    {
                        new ProjectReferenceTargetItem("a", "x;.default;y", false)
                    },
                    new string[0],
                    new[] {"x", "y"}
                };

                yield return new object[]
                {
                    new[] {"y", "x", "r", "t"},
                    new[] {"t", "r"},
                    new[]
                    {
                        new ProjectReferenceTargetItem("a", "x;.default;y", false)
                    },
                    new string[0],
                    new[] {"x", "y", "r", "t"}
                };
            }
        }

        [Theory]
        [MemberData(nameof(TaskShouldBeAbleToExpandDefaultTargetPlaceholderData))]
        public void TaskShouldBeAbleToExpandDefaultTargetPlaceholder(
            string[] entryTargets,
            string[] defaultTargets,
            ProjectReferenceTargetItem[] prt,
            string[] expectedInnerBuildTargets,
            string[] expectedOuterBuildTargets)
        {
            AssertTaskOutputs(entryTargets, defaultTargets, prt, expectedInnerBuildTargets, expectedOuterBuildTargets);
        }

        private void AssertTaskOutputs(
            string[] entryTargets,
            string[] defaultTargets,
            ProjectReferenceTargetItem[] prt,
            string[] expectedInnerBuildTargets,
            string[] expectedOuterBuildTargets)
        {
            var (task, taskReturnValue, mockEngine) = ExecuteTask(entryTargets, defaultTargets, prt);

            mockEngine.Errors.ShouldBe(0);
            mockEngine.Warnings.ShouldBe(0);
            taskReturnValue.ShouldBeTrue();

            var actualInnerBuildTargets = task.EntryTargetsForInnerBuilds.Select(i => i.ItemSpec).ToArray();

            actualInnerBuildTargets.ShouldBeSubsetOf(expectedInnerBuildTargets);
            expectedInnerBuildTargets.ShouldBeSubsetOf(actualInnerBuildTargets);

            var actualOuterBuildTargets = task.EntryTargetsForOuterBuild.Select(t => t.ItemSpec).ToArray();
            actualOuterBuildTargets.ShouldBeSubsetOf(expectedOuterBuildTargets);
            expectedOuterBuildTargets.ShouldBeSubsetOf(actualOuterBuildTargets);
        }

        private static (SplitTargetsByOuterAndInnerBuild Task, bool TaskReturnValue, MockEngine MockEngine) ExecuteTask(
            string[] entryTargets,
            string[] defaultTargets,
            ProjectReferenceTargetItem[] prt)
        {
            var mockEngine = new MockEngine();

            var task = new SplitTargetsByOuterAndInnerBuild
            {
                EntryTargets = entryTargets.Select(t => new TaskItem(t)).ToArray(),
                DefaultTargets = defaultTargets.Select(t => new TaskItem(t)).ToArray(),
                ProjectReferenceTargets = prt.Select(t => t.TaskItem).ToArray(),
                BuildEngine = mockEngine
            };

            var taskReturnValue = task.Execute();

            return (task, taskReturnValue, mockEngine);
        }

        [Fact]
        public void TaskShouldLogErrorIfNotAllEntryTargetsGetMatched()
        {
            var result = ExecuteTask(new[] {"t", "xi", "nonexistent"}, new string[0], _projectReferenceTargets);

            result.TaskReturnValue.ShouldBeFalse();
            result.MockEngine.Log.ShouldContain("MSB3961");
            result.MockEngine.Log.ShouldContain("nonexistent");
            result.MockEngine.Errors.ShouldBe(1);
        }

        [Fact]
        public void TaskShouldLogErrorIfProjectReferenceTargetItemDoesNotContainTargetsMetadata()
        {
            var result = ExecuteTask(
                new[] {"a", "b"},
                new string[0],
                new[]
                {
                    new ProjectReferenceTargetItem("x", null, false)
                });

            result.TaskReturnValue.ShouldBeFalse();
            result.MockEngine.Log.ShouldContain("MSB3962");
            result.MockEngine.Errors.ShouldBe(1);
        }

        [Fact]
        public void TaskShouldLogErrorIfProjectReferenceTargetsHaveMismatchedMetadata()
        {
            var result = ExecuteTask(
                new[] {"a"},
                new string[0],
                new[]
                {
                    new ProjectReferenceTargetItem("x", "a", false),
                    new ProjectReferenceTargetItem("y", "a", true)
                });

            result.TaskReturnValue.ShouldBeFalse();
            result.MockEngine.Log.ShouldContain("MSB3963");
            result.MockEngine.Errors.ShouldBe(1);
        }
    }
}
