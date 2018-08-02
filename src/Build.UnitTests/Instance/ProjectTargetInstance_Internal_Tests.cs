// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using Microsoft.Build.UnitTests.BackEnd;
using Xunit;
using static Microsoft.Build.Engine.UnitTests.TestData.ProjectInstanceTestObjects;
using static Microsoft.Build.Engine.UnitTests.TestComparers.ProjectInstanceModelTestComparers;

namespace Microsoft.Build.Engine.UnitTests.Instance
{
    public class ProjectTargetInstance_Internal_Tests
    {
        public static IEnumerable<object[]> TargetChildrenTestData
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
                    new ReadOnlyCollection<ProjectTargetInstanceChild>(new ProjectTargetInstanceChild[0]),
                    new ReadOnlyCollection<ProjectOnErrorInstance>(new ProjectOnErrorInstance[0])
                };

                yield return new object[]
                {
                    new ReadOnlyCollection<ProjectTargetInstanceChild>(
                        new ProjectTargetInstanceChild[]
                        {
                            CreateTargetPropertyGroup(),
                            CreateTargetItemGroup(),
                            CreateTargetOnError(),
                            CreateTargetTask()
                        }
                    ),
                    new ReadOnlyCollection<ProjectOnErrorInstance>(new[] {CreateTargetOnError()})
                };

                yield return new object[]
                {
                    new ReadOnlyCollection<ProjectTargetInstanceChild>(
                        new ProjectTargetInstanceChild[]
                        {
                            CreateTargetPropertyGroup(),
                            CreateTargetItemGroup(),
                            CreateTargetPropertyGroup(),
                            CreateTargetItemGroup(),
                            CreateTargetOnError(),
                            CreateTargetTask(),
                            CreateTargetOnError(),
                            CreateTargetTask()
                        }
                    ),
                    new ReadOnlyCollection<ProjectOnErrorInstance>(new[]
                    {
                        CreateTargetOnError(),
                        CreateTargetOnError()
                    })
                };
            }
        }

        [Theory]
        [MemberData(nameof(TargetChildrenTestData))]
        public void ProjectTargetInstanceCanSerializeViaTranslator(
            ReadOnlyCollection<ProjectTargetInstanceChild> children,
            ReadOnlyCollection<ProjectOnErrorInstance> errorChildren)
        {
            var original = CreateTarget(null, children, errorChildren);

            ((INodePacketTranslatable) original).Translate(TranslationHelpers.GetWriteTranslator());
            var copy = ProjectTargetInstance.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            Assert.Equal(original, copy, new TargetComparer());
        }
    }
}
