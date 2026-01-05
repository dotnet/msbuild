// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using Microsoft.Build.Framework;

#nullable disable

namespace Microsoft.Build.Framework.Benchmark
{
    [MemoryDiagnoser]
    public class BuildEventContext_Benchmark
    {
        // Test data - realistic values for different components
        private const int SubmissionId = 1;
        private const int NodeId = 2;
        private const int EvaluationId = 100;
        private const int ProjectInstanceId = 200;
        private const int ProjectContextId = 300;
        private const int TargetId = 400;
        private const int TaskId = 500;

        private delegate BuildEventContext BuildEventContextConstructor(int submissionId, int nodeId, int evaluationId, int projectInstanceId, int projectContextId, int targetId, int taskId);
        private static readonly BuildEventContextConstructor ReflectionCtor = FindCtor();

        private static BuildEventContextConstructor FindCtor()
        {
            
            var ctorInfo = typeof(BuildEventContext).GetConstructor(
                BindingFlags.NonPublic | BindingFlags.Instance,
                null,
                new Type[]
                {
                    typeof(int), // submissionId
                    typeof(int), // nodeId
                    typeof(int), // evaluationId
                    typeof(int), // projectInstanceId
                    typeof(int), // projectContextId
                    typeof(int), // targetId
                    typeof(int)  // taskId
                },
                null);

            if (ctorInfo == null)
            {
                throw new InvalidOperationException("Could not find BuildEventContext constructor via reflection.");
            }

            var invoker = ConstructorInvoker.Create(ctorInfo);
            Func<int, int, int, int, int, int, int, BuildEventContext> func = (s, n, e, pi, pc, tr, ts) => (BuildEventContext)invoker.Invoke([s, n, e, pi, pc, tr, ts]);

            return func.Invoke;
        }

        [Params(1, 10, 100, 1000)]
        public int IterationCount { get; set; }

        /// <summary>
        /// Test scenario where context is built incrementally.
        /// This simulates real-world usage where context builds up during execution.
        /// </summary>
        [Benchmark]
        public BuildEventContext BuilderAPI_IncrementalBuild()
        {
            BuildEventContext result = null;
            for (int i = 0; i < IterationCount; i++)
            {
                var context = BuildEventContext.CreateInitial(SubmissionId + i, NodeId).Build();
                context = context.WithEvaluationId(EvaluationId + i).Build();
                context = context.WithProjectInstanceId(ProjectInstanceId + i).Build();
                context = context.WithProjectContextId(ProjectContextId + i).Build();
                context = context.WithTargetId(TargetId + i).Build();
                result = context.WithTaskId(TaskId + i).Build();
            }
            return result;
        }

        /// <summary>
        /// Tests the memory of using the constructor directly with incremental building pattern.
        /// This simulates the same incremental building as BuilderAPI_IncrementalBuild but uses direct constructor calls.
        /// </summary>
        /// <returns></returns>
        [Benchmark]
        public BuildEventContext DirectConstructor_Reflection()
        {
            BuildEventContext result = null;
            for (int i = 0; i < IterationCount; i++)
            {
                // Create base context (equivalent to CreateInitial + Build)
                var context = ReflectionCtor(
                    SubmissionId + i,
                    NodeId,
                    BuildEventContext.InvalidEvaluationId,
                    BuildEventContext.InvalidProjectInstanceId,
                    BuildEventContext.InvalidProjectContextId,
                    BuildEventContext.InvalidTargetId,
                    BuildEventContext.InvalidTaskId);

                // Add evaluation ID (equivalent to WithEvaluationId + Build)
                context = ReflectionCtor(
                    SubmissionId + i,
                    NodeId,
                    EvaluationId + i,
                    BuildEventContext.InvalidProjectInstanceId,
                    BuildEventContext.InvalidProjectContextId,
                    BuildEventContext.InvalidTargetId,
                    BuildEventContext.InvalidTaskId);

                // Add project instance ID (equivalent to WithProjectInstanceId + Build)
                context = ReflectionCtor(
                    SubmissionId + i,
                    NodeId,
                    EvaluationId + i,
                    ProjectInstanceId + i,
                    BuildEventContext.InvalidProjectContextId,
                    BuildEventContext.InvalidTargetId,
                    BuildEventContext.InvalidTaskId);

                // Add project context ID (equivalent to WithProjectContextId + Build)
                context = ReflectionCtor(
                    SubmissionId + i,
                    NodeId,
                    EvaluationId + i,
                    ProjectInstanceId + i,
                    ProjectContextId + i,
                    BuildEventContext.InvalidTargetId,
                    BuildEventContext.InvalidTaskId);

                // Add target ID (equivalent to WithTargetId + Build)
                context = ReflectionCtor(
                    SubmissionId + i,
                    NodeId,
                    EvaluationId + i,
                    ProjectInstanceId + i,
                    ProjectContextId + i,
                    TargetId + i,
                    BuildEventContext.InvalidTaskId);

                // Add task ID (equivalent to WithTaskId + Build)
                result = ReflectionCtor(
                    SubmissionId + i,
                    NodeId,
                    EvaluationId + i,
                    ProjectInstanceId + i,
                    ProjectContextId + i,
                    TargetId + i,
                    TaskId + i);
            }
            return result;
        }
    }
}