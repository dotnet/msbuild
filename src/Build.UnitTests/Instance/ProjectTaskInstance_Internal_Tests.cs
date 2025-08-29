// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Construction;
using Microsoft.Build.Engine.UnitTests.TestComparers;
using Microsoft.Build.Execution;
using Microsoft.Build.UnitTests;
using Microsoft.Build.UnitTests.BackEnd;
using Xunit;
using static Microsoft.Build.Engine.UnitTests.TestData.ProjectInstanceTestObjects;

#nullable disable

namespace Microsoft.Build.Engine.UnitTests.Instance
{
    public class ProjectTaskInstance_Internal_Tests
    {
        public static IEnumerable<object[]> TestData
        {
            get
            {
                yield return new object[]
                {
                    null,
                    null
                };

                yield return new object[]
                {
                    new Dictionary<string, (string, MockElementLocation)>(),
                    new List<ProjectTaskInstanceChild>()
                };

                yield return new object[]
                {
                    new Dictionary<string, (string, MockElementLocation)>
                    {
                        {"p1", ("v1", new MockElementLocation("p1")) }
                    },
                    new List<ProjectTaskInstanceChild>
                    {
                        CreateTaskItemyOutput()
                    }
                };

                yield return new object[]
                {
                    new Dictionary<string, (string, MockElementLocation)>
                    {
                        {"p1", ("v1", new MockElementLocation("p1")) },
                        {"p2", ("v2", new MockElementLocation("p2")) }
                    },
                    new List<ProjectTaskInstanceChild>
                    {
                        CreateTaskItemyOutput(),
                        CreateTaskPropertyOutput()
                    }
                };
            }
        }

        [Theory]
        [MemberData(nameof(TestData))]
        public void ProjectTaskInstanceCanSerializeViaTranslator(
            IDictionary<string, (string, MockElementLocation)> parameters,
            List<ProjectTaskInstanceChild> outputs)
        {
            parameters ??= new Dictionary<string, (string, MockElementLocation)>();

            var parametersCopy = new Dictionary<string, (string, ElementLocation)>(parameters.Count);
            foreach (var param in parameters)
            {
                parametersCopy[param.Key] = (param.Value.Item1, param.Value.Item2);
            }

            var original = CreateTargetTask(null, parametersCopy, outputs);

            ((ITranslatable)original).Translate(TranslationHelpers.GetWriteTranslator());
            var copy = ProjectTaskInstance.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            Assert.Equal(original, copy, new TargetTaskComparer());
        }
    }
}
