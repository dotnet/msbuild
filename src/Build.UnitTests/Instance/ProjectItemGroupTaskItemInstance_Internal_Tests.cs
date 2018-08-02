// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using Microsoft.Build.UnitTests.BackEnd;
using Xunit;

using static Microsoft.Build.Engine.UnitTests.TestComparers.ProjectInstanceModelTestComparers;
using static Microsoft.Build.Engine.UnitTests.TestData.ProjectInstanceTestObjects;

namespace Microsoft.Build.Engine.UnitTests.Instance
{
    public class ProjectItemGroupTaskItemInstance_Internal_Tests
    {
        public static IEnumerable<object[]> MetadataTestData
        {
            get
            {
                yield return new object[]
                {
                    new List<ProjectItemGroupTaskMetadataInstance>()
                };

                yield return new object[]
                {
                    new List<ProjectItemGroupTaskMetadataInstance>
                    {
                        CreateTargetItemMetadata(1),
                        CreateTargetItemMetadata(2)
                    }
                };
            }
        }

        [Theory]
        [MemberData(nameof(MetadataTestData))]
        public void ProjectItemGroupTaskItemInstanceCanSerializeViaTranslator(List<ProjectItemGroupTaskMetadataInstance> metadata)
        {
            var original = CreateTargetItem(null, metadata);

            ((INodePacketTranslatable) original).Translate(TranslationHelpers.GetWriteTranslator());
            var clone = ProjectItemGroupTaskItemInstance.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            Assert.Equal(original, clone, new TargetItemComparer());
        }
    }
}
