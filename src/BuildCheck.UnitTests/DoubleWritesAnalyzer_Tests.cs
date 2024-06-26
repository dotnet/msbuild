// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Experimental.BuildCheck.Analyzers;
using Microsoft.Build.Experimental.BuildCheck.Infrastructure;
using Shouldly;
using Xunit;

namespace Microsoft.Build.BuildCheck.UnitTests
{
    public sealed class DoubleWritesAnalyzer_Tests
    {
        private sealed class MockBuildCheckRegistrationContext : IBuildCheckRegistrationContext
        {
            private event Action<BuildCheckDataContext<TaskInvocationAnalysisData>>? _taskInvocationAction;

            public List<BuildCheckResult> Results { get; } = new();

            public void RegisterEvaluatedPropertiesAction(Action<BuildCheckDataContext<EvaluatedPropertiesAnalysisData>> evaluatedPropertiesAction) => throw new NotImplementedException();
            public void RegisterParsedItemsAction(Action<BuildCheckDataContext<ParsedItemsAnalysisData>> parsedItemsAction) => throw new NotImplementedException();

            public void RegisterTaskInvocationAction(Action<BuildCheckDataContext<TaskInvocationAnalysisData>> taskInvocationAction)
                => _taskInvocationAction += taskInvocationAction;

            public void TriggerTaskInvocationAction(TaskInvocationAnalysisData data)
            {
                if (_taskInvocationAction is not null)
                {
                    BuildCheckDataContext<TaskInvocationAnalysisData> context = new BuildCheckDataContext<TaskInvocationAnalysisData>(
                        null!,
                        null!,
                        null!,
                        ResultHandler,
                        data);
                    _taskInvocationAction(context);
                }
            }

            private void ResultHandler(BuildAnalyzerWrapper wrapper, IAnalysisContext context, BuildAnalyzerConfigurationInternal[] configs, BuildCheckResult result)
                => Results.Add(result);
        }

        private readonly DoubleWritesAnalyzer _analyzer;

        private readonly MockBuildCheckRegistrationContext _registrationContext;

        public DoubleWritesAnalyzer_Tests()
        {
            _analyzer = new DoubleWritesAnalyzer();
            _registrationContext = new MockBuildCheckRegistrationContext();
            _analyzer.RegisterActions(_registrationContext);
        }

        private TaskInvocationAnalysisData MakeTaskInvocationData(string taskName, Dictionary<string, TaskInvocationAnalysisData.TaskParameter> parameters)
        {
            string projectFile = NativeMethodsShared.IsWindows ? @"C:\fake\project.proj" : "/fake/project.proj";
            return new TaskInvocationAnalysisData(
                projectFile,
                Construction.ElementLocation.EmptyLocation,
                taskName,
                projectFile,
                parameters);
        }

        [Fact]
        public void TestCopyTask()
        {
            _registrationContext.TriggerTaskInvocationAction(MakeTaskInvocationData("Copy", new Dictionary<string, TaskInvocationAnalysisData.TaskParameter>
                {
                    { "SourceFiles", new TaskInvocationAnalysisData.TaskParameter("source1", IsOutput: false) },
                    { "DestinationFolder", new TaskInvocationAnalysisData.TaskParameter("outdir", IsOutput: false) },
                }));
            _registrationContext.TriggerTaskInvocationAction(MakeTaskInvocationData("Copy", new Dictionary<string, TaskInvocationAnalysisData.TaskParameter>
                {
                    { "SourceFiles", new TaskInvocationAnalysisData.TaskParameter("source1", IsOutput: false) },
                    { "DestinationFiles", new TaskInvocationAnalysisData.TaskParameter(Path.Combine("outdir", "source1"), IsOutput: false) },
                }));

            _registrationContext.Results.Count.ShouldBe(1);
            _registrationContext.Results[0].BuildAnalyzerRule.Id.ShouldBe("BC0102");
        }

        [Theory]
        [InlineData("Csc")]
        [InlineData("Vbc")]
        [InlineData("Fsc")]
        public void TestCompilerTask(string taskName)
        {
            for (int i = 0; i < 2; i++)
            {
                _registrationContext.TriggerTaskInvocationAction(MakeTaskInvocationData(taskName, new Dictionary<string, TaskInvocationAnalysisData.TaskParameter>
                    {
                        { "OutputAssembly", new TaskInvocationAnalysisData.TaskParameter("out.dll", IsOutput: false) },
                        { "OutputRefAssembly", new TaskInvocationAnalysisData.TaskParameter("out_ref.dll", IsOutput: false) },
                        { "DocumentationFile", new TaskInvocationAnalysisData.TaskParameter("out.xml", IsOutput: false) },
                        { "PdbFile", new TaskInvocationAnalysisData.TaskParameter("out.pdb", IsOutput: false) },
                    }));
            }

            _registrationContext.Results.Count.ShouldBe(4);
            _registrationContext.Results.ForEach(result => result.BuildAnalyzerRule.Id.ShouldBe("BC0102"));
        }
    }
}
