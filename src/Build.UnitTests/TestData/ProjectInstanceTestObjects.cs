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
            items ??= new List<ProjectItemGroupTaskItemInstance>();
            var stringCounter = CounterToString(counter);

            return new ProjectItemGroupTaskInstance(
                $"c{stringCounter}",
                new MockElementLocation($"location{stringCounter}"),
                new MockElementLocation($"conditionLocation{stringCounter}"),
                items);
        }

        public static ProjectItemGroupTaskItemInstance CreateTargetItem(int? counter = null, List<ProjectItemGroupTaskMetadataInstance> metadata = null)
        {
            metadata ??= new List<ProjectItemGroupTaskMetadataInstance>();
            var stringCounter = CounterToString(counter);

            return new ProjectItemGroupTaskItemInstance(
                itemType: $"i{stringCounter}",
                include: $"v{stringCounter}",
                exclude: $"e{stringCounter}",
                remove: $"r{stringCounter}",
                matchOnMetadata: $"mm{stringCounter}",
                matchOnMetadataOptions: $"mmo{stringCounter}",
                keepMetadata: $"km{stringCounter}",
                removeMetadata: $"rm{stringCounter}",
                keepDuplicates: $"kd{stringCounter}",
                condition: $"c{stringCounter}",
                location: new MockElementLocation($"location{stringCounter}"),
                includeLocation: new MockElementLocation($"include{stringCounter}"),
                excludeLocation: new MockElementLocation($"remove{stringCounter}"),
                removeLocation: new MockElementLocation($"exclude{stringCounter}"),
                matchOnMetadataLocation: new MockElementLocation($"mm{stringCounter}"),
                matchOnMetadataOptionsLocation: new MockElementLocation($"mmo{stringCounter}"),
                keepMetadataLocation: new MockElementLocation($"km{stringCounter}"),
                removeMetadataLocation: new MockElementLocation($"rm{stringCounter}"),
                keepDuplicatesLocation: new MockElementLocation($"kd{stringCounter}"),
                conditionLocation: new MockElementLocation($"cl{stringCounter}"),
                metadata: metadata
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
            properties ??= new List<ProjectPropertyGroupTaskPropertyInstance>();
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
            IDictionary<string, (string, ElementLocation)> parameters = null,
            List<ProjectTaskInstanceChild> outputs = null)
        {
            var stringCounter = CounterToString(counter);

            var readonlyParameters = parameters != null
                ? new CopyOnWriteDictionary<string, (string, ElementLocation)>(parameters)
                : new CopyOnWriteDictionary<string, (string, ElementLocation)>();

            outputs ??= new List<ProjectTaskInstanceChild>();

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
            children ??= new System.Collections.ObjectModel.ReadOnlyCollection<ProjectTargetInstanceChild>(new List<ProjectTargetInstanceChild>());
            errorChildren ??= new System.Collections.ObjectModel.ReadOnlyCollection<ProjectOnErrorInstance>(new List<ProjectOnErrorInstance>());
            var stringCounter = CounterToString(counter);

            return new ProjectTargetInstance(
                $"n{stringCounter}",
                $"c{stringCounter}",
                $"i{stringCounter}",
                $"o{stringCounter}",
                $"r{stringCounter}",
                $"kdo{stringCounter}",
                $"dot{stringCounter}",
                $"bt{stringCounter}",
                $"at{stringCounter}",
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
