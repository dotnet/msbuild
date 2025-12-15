// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Tests for the fluent API of BuildEventContext to ensure ID propagation works correctly.
    /// These tests verify that the new fluent API correctly preserves all IDs when creating derived contexts,
    /// solving the critical bug where evaluation IDs were lost in target and task contexts.
    /// </summary>
    public class BuildEventContextFluentAPITests
    {
        [Fact]
        public void CreateInitial_SetsCorrectProperties()
        {
            var context = BuildEventContext.CreateInitial(submissionId: 1, nodeId: 2);
            
            context.SubmissionId.ShouldBe(1);
            context.NodeId.ShouldBe(2);
            context.EvaluationId.ShouldBe(BuildEventContext.InvalidEvaluationId);
            context.ProjectInstanceId.ShouldBe(BuildEventContext.InvalidProjectInstanceId);
            context.ProjectContextId.ShouldBe(BuildEventContext.InvalidProjectContextId);
            context.TargetId.ShouldBe(BuildEventContext.InvalidTargetId);
            context.TaskId.ShouldBe(BuildEventContext.InvalidTaskId);
        }

        [Fact]
        public void WithTargetId_PreservesAllOtherIds()
        {
            // Arrange: Create a project-level context with all IDs set
            var projectContext = BuildEventContext.CreateInitial(submissionId: 1, nodeId: 2)
                .WithEvaluationId(3)
                .WithProjectInstanceId(4)
                .WithProjectContextId(5);

            // Act: Create a target context
            var targetContext = projectContext.WithTargetId(6);

            // Assert: All previous IDs should be preserved
            targetContext.SubmissionId.ShouldBe(1);
            targetContext.NodeId.ShouldBe(2);
            targetContext.EvaluationId.ShouldBe(3); // This should NOT be InvalidEvaluationId!
            targetContext.ProjectInstanceId.ShouldBe(4);
            targetContext.ProjectContextId.ShouldBe(5);
            targetContext.TargetId.ShouldBe(6);
            targetContext.TaskId.ShouldBe(BuildEventContext.InvalidTaskId);
        }

        [Fact]
        public void WithTaskId_PreservesAllOtherIds()
        {
            // Arrange: Create a target-level context with all IDs set
            var targetContext = BuildEventContext.CreateInitial(submissionId: 1, nodeId: 2)
                .WithEvaluationId(3)
                .WithProjectInstanceId(4)
                .WithProjectContextId(5)
                .WithTargetId(6);

            // Act: Create a task context
            var taskContext = targetContext.WithTaskId(7);

            // Assert: All previous IDs should be preserved, including evaluation ID
            taskContext.SubmissionId.ShouldBe(1);
            taskContext.NodeId.ShouldBe(2);
            taskContext.EvaluationId.ShouldBe(3); // This should NOT be InvalidEvaluationId!
            taskContext.ProjectInstanceId.ShouldBe(4);
            taskContext.ProjectContextId.ShouldBe(5);
            taskContext.TargetId.ShouldBe(6);
            taskContext.TaskId.ShouldBe(7);
        }

        [Fact]
        public void FluentChaining_WorksCorrectly()
        {
            var context = BuildEventContext.CreateInitial(1, 2)
                .WithEvaluationId(3)
                .WithProjectInstanceId(4)
                .WithProjectContextId(5)
                .WithTargetId(6)
                .WithTaskId(7);

            context.SubmissionId.ShouldBe(1);
            context.NodeId.ShouldBe(2);
            context.EvaluationId.ShouldBe(3);
            context.ProjectInstanceId.ShouldBe(4);
            context.ProjectContextId.ShouldBe(5);
            context.TargetId.ShouldBe(6);
            context.TaskId.ShouldBe(7);
        }

        [Fact]
        public void WithMethods_AreImmutable()
        {
            var original = BuildEventContext.CreateInitial(1, 2).WithEvaluationId(3);
            var modified = original.WithTargetId(4);

            // Original should be unchanged
            original.TargetId.ShouldBe(BuildEventContext.InvalidTargetId);
            original.EvaluationId.ShouldBe(3);

            // Modified should have new target ID but preserve evaluation ID
            modified.TargetId.ShouldBe(4);
            modified.EvaluationId.ShouldBe(3);
        }

        [Fact]
        public void ConstructorsAreInternal_ExternalCodeMustUseFluentAPI()
        {
            // This test documents that all constructors are now internal
            // External code cannot directly construct BuildEventContext objects
            // They must use CreateInitial() and fluent methods
            
            var context = BuildEventContext.CreateInitial(1, 2);
            context.ShouldNotBeNull();
            context.SubmissionId.ShouldBe(1);
            context.NodeId.ShouldBe(2);
            
            // The Invalid property should also work
            BuildEventContext.Invalid.ShouldNotBeNull();
            BuildEventContext.Invalid.NodeId.ShouldBe(BuildEventContext.InvalidNodeId);
        }
    }
}