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
        private event Action<BuildCheckDataContext<TaskInvocationCheckData>>? _taskInvocationAction;
        private event Action<BuildCheckDataContext<EvaluatedPropertiesCheckData>>? _evaluatedPropertiesAction;

        public List<BuildCheckResult> Results { get; } = new();

        public void RegisterEvaluatedPropertiesAction(Action<BuildCheckDataContext<EvaluatedPropertiesCheckData>> evaluatedPropertiesAction)
            => _evaluatedPropertiesAction += evaluatedPropertiesAction;
        public void RegisterParsedItemsAction(Action<BuildCheckDataContext<ParsedItemsCheckData>> parsedItemsAction) => throw new NotImplementedException();

        public void RegisterTaskInvocationAction(Action<BuildCheckDataContext<TaskInvocationCheckData>> taskInvocationAction)
            => _taskInvocationAction += taskInvocationAction;

        public void TriggerTaskInvocationAction(TaskInvocationCheckData data)
        {
            if (_taskInvocationAction is not null)
            {
                BuildCheckDataContext<TaskInvocationCheckData> context = new BuildCheckDataContext<TaskInvocationCheckData>(
                    null!,
                    null!,
                    null!,
                    ResultHandler,
                    data);
                _taskInvocationAction(context);
            }
        }
        public void TriggerEvaluatedPropertiesAction(EvaluatedPropertiesCheckData data)
        {
            if (_evaluatedPropertiesAction is not null)
            {
                BuildCheckDataContext<EvaluatedPropertiesCheckData> context = new BuildCheckDataContext<EvaluatedPropertiesCheckData>(
                    null!,
                    null!,
                    null!,
                    ResultHandler,
                    data);
                _evaluatedPropertiesAction(context);
            }
        }

        private void ResultHandler(CheckWrapper wrapper, ICheckContext context, CheckConfigurationEffective[] configs, BuildCheckResult result)
            => Results.Add(result);
    }
}

