// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Build.Experimental.BuildCheck;
using Microsoft.Build.Experimental.BuildCheck.Infrastructure;

namespace Microsoft.Build.BuildCheck.UnitTests
{
    internal sealed class MockBuildCheckRegistrationContext : IBuildCheckRegistrationContext
    {
        private event Action<BuildCheckDataContext<TaskInvocationAnalysisData>>? _taskInvocationAction;
        private event Action<BuildCheckDataContext<EvaluatedPropertiesAnalysisData>>? _evaluatedPropertiesAction;

        public List<BuildCheckResult> Results { get; } = new();

        public void RegisterEvaluatedPropertiesAction(Action<BuildCheckDataContext<EvaluatedPropertiesAnalysisData>> evaluatedPropertiesAction)
            => _evaluatedPropertiesAction += evaluatedPropertiesAction;
        public void RegisterParsedItemsAction(Action<BuildCheckDataContext<ParsedItemsAnalysisData>> parsedItemsAction) => throw new NotImplementedException();

        public void RegisterTaskInvocationAction(Action<BuildCheckDataContext<TaskInvocationAnalysisData>> taskInvocationAction)
            => _taskInvocationAction += taskInvocationAction;

        public void RegisterBuildFinishedAction(Action<BuildCheckDataContext<BuildFinishedAnalysisData>> buildFinishedAction) => throw new NotImplementedException();

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
        public void TriggerEvaluatedPropertiesAction(EvaluatedPropertiesAnalysisData data)
        {
            if (_evaluatedPropertiesAction is not null)
            {
                BuildCheckDataContext<EvaluatedPropertiesAnalysisData> context = new BuildCheckDataContext<EvaluatedPropertiesAnalysisData>(
                    null!,
                    null!,
                    null!,
                    ResultHandler,
                    data);
                _evaluatedPropertiesAction(context);
            }
        }

        private void ResultHandler(BuildAnalyzerWrapper wrapper, IAnalysisContext context, BuildAnalyzerConfigurationEffective[] configs, BuildCheckResult result)
            => Results.Add(result);
    }
}

