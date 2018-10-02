// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Execution;
using Microsoft.Build.UnitTests;

namespace Microsoft.Build.Engine.UnitTests.TestData
{
    internal static class ProjectInstanceTestObjects
    {
        public static ProjectItemGroupTaskInstance CreateTargetItemGroup(int? counter = null, List<ProjectItemGroupTaskItemInstance> items = null)
        {
            items = items ?? new List<ProjectItemGroupTaskItemInstance>();
            var stringCounter = CounterToString(counter);

            return new ProjectItemGroupTaskInstance(
                $"c{stringCounter}",
                new MockElementLocation($"location{stringCounter}"),
                new MockElementLocation($"conditionLocation{stringCounter}"),
                items);
        }

        public static ProjectItemGroupTaskItemInstance CreateTargetItem(int? counter = null, List<ProjectItemGroupTaskMetadataInstance> metadata = null)
        {
            metadata = metadata ?? new List<ProjectItemGroupTaskMetadataInstance>();
            var stringCounter = CounterToString(counter);

            return new ProjectItemGroupTaskItemInstance(
                $"i{stringCounter}",
                $"v{stringCounter}",
                $"e{stringCounter}",
                $"r{stringCounter}",
                $"km{stringCounter}",
                $"rm{stringCounter}",
                $"kd{stringCounter}",
                $"c{stringCounter}",
                new MockElementLocation($"location{stringCounter}"),
                new MockElementLocation($"include{stringCounter}"),
                new MockElementLocation($"remove{stringCounter}"),
                new MockElementLocation($"exclude{stringCounter}"),
                new MockElementLocation($"km{stringCounter}"),
                new MockElementLocation($"rm{stringCounter}"),
                new MockElementLocation($"kd{stringCounter}"),
                new MockElementLocation($"cl{stringCounter}"),
                metadata
            );
        }

        public static ProjectItemGroupTaskMetadataInstance CreateTargetItemMetadata(int? counter = null)
        {
            var stringCounter = CounterToString(counter);

            return new ProjectItemGroupTaskMetadataInstance(
                $"n{stringCounter}",
                $"v{stringCounter}",
                $"c{stringCounter}",
                new MockElementLocation($"location{stringCounter}"),
                new MockElementLocation($"condition{stringCounter}")
            );
        }

        public static ProjectPropertyGroupTaskInstance CreateTargetPropertyGroup(
            int? counter = null,
            List<ProjectPropertyGroupTaskPropertyInstance> properties = null)
        {
            properties = properties ?? new List<ProjectPropertyGroupTaskPropertyInstance>();
            var stringCounter = CounterToString(counter);

            return new ProjectPropertyGroupTaskInstance(
                $"c{stringCounter}",
                new MockElementLocation($"location{stringCounter}"),
                new MockElementLocation($"conditionLocation{stringCounter}"),
                properties
            );
        }

        public static ProjectPropertyGroupTaskPropertyInstance CreateTargetProperty(int? counter = null)
        {
            var stringCounter = CounterToString(counter);

            return new ProjectPropertyGroupTaskPropertyInstance(
                $"n{stringCounter}",
                $"v{stringCounter}",
                $"c{stringCounter}",
                new MockElementLocation($"location{stringCounter}"),
                new MockElementLocation($"conditionLocation{stringCounter}")
            );
        }

        public static ProjectOnErrorInstance CreateTargetOnError(int? counter = null)
        {
            var stringCounter = CounterToString(counter);

            return new ProjectOnErrorInstance(
                $"t{stringCounter}",
                $"c{stringCounter}",
                new MockElementLocation($"location{stringCounter}"),
                new MockElementLocation($"executeTargetLocation{stringCounter}"),
                new MockElementLocation($"conditionLocation{stringCounter}"));
        }

        public static ProjectTaskInstance CreateTargetTask(
            int? counter = null,
            IDictionary<string, Tuple<string, ElementLocation>> parameters = null,
            List<ProjectTaskInstanceChild> outputs = null)
        {
            var stringCounter = CounterToString(counter);

            var readonlyParameters = parameters != null
                ? new CopyOnWriteDictionary<string, Tuple<string, ElementLocation>>(parameters)
                : new CopyOnWriteDictionary<string, Tuple<string, ElementLocation>>();

            outputs = outputs ?? new List<ProjectTaskInstanceChild>();

            return new ProjectTaskInstance(
                $"n{stringCounter}",
                $"condition{stringCounter}",
                $"ce{stringCounter}",
                $"msbr{stringCounter}",
                $"msba{stringCounter}",
                readonlyParameters,
                outputs,
                new MockElementLocation($"location{stringCounter}"),
                new MockElementLocation($"conditionLocation{stringCounter}"),
                new MockElementLocation($"coeLocation{stringCounter}"),
                new MockElementLocation($"msbrLocation{stringCounter}"),
                new MockElementLocation($"msbaLocation{stringCounter}")
            );
        }

        public static ProjectTaskOutputPropertyInstance CreateTaskPropertyOutput(int? counter = null)
        {
            var stringCounter = CounterToString(counter);

            return new ProjectTaskOutputPropertyInstance(
                $"n{stringCounter}",
                $"tp{stringCounter}",
                $"c{stringCounter}",
                new MockElementLocation($"location{stringCounter}"),
                new MockElementLocation($"propertyLocation{stringCounter}"),
                new MockElementLocation($"taskParamLocation{stringCounter}"),
                new MockElementLocation($"conditionLocation{stringCounter}")
            );
        }

        public static ProjectTaskOutputItemInstance CreateTaskItemyOutput(int? counter = null)
        {
            var stringCounter = CounterToString(counter);

            return new ProjectTaskOutputItemInstance(
                $"i{stringCounter}",
                $"tp{stringCounter}",
                $"c{stringCounter}",
                new MockElementLocation($"location{stringCounter}"),
                new MockElementLocation($"itemLocation{stringCounter}"),
                new MockElementLocation($"taskParamLocation{stringCounter}"),
                new MockElementLocation($"conditionLocation{stringCounter}")
            );
        }

        public static ProjectTargetInstance CreateTarget(
            int? counter,
            System.Collections.ObjectModel.ReadOnlyCollection<ProjectTargetInstanceChild> children,
            System.Collections.ObjectModel.ReadOnlyCollection<ProjectOnErrorInstance> errorChildren)
        {
            children = children ?? new System.Collections.ObjectModel.ReadOnlyCollection<ProjectTargetInstanceChild>(new List<ProjectTargetInstanceChild>());
            errorChildren = errorChildren ?? new System.Collections.ObjectModel.ReadOnlyCollection<ProjectOnErrorInstance>(new List<ProjectOnErrorInstance>());
            var stringCounter = CounterToString(counter);

            return new ProjectTargetInstance(
                $"n{stringCounter}",
                $"c{stringCounter}",
                $"i{stringCounter}",
                $"o{stringCounter}",
                $"r{stringCounter}",
                $"kdo{stringCounter}",
                $"dot{stringCounter}",
                new MockElementLocation($"location{stringCounter}"),
                new MockElementLocation($"conditionLocation{stringCounter}"),
                new MockElementLocation($"inputsLocation{stringCounter}"),
                new MockElementLocation($"outputsLocation{stringCounter}"),
                new MockElementLocation($"returnsLocation{stringCounter}"),
                new MockElementLocation($"kdoLocation{stringCounter}"),
                new MockElementLocation($"dotLocation{stringCounter}"),
                new MockElementLocation($"btLocation{stringCounter}"),
                new MockElementLocation($"atLocation{stringCounter}"),
                children,
                errorChildren,
                true
            );
        }

        private static string CounterToString(int? counter)
        {
            return counter?.ToString() ?? string.Empty;
        }
    }
}
