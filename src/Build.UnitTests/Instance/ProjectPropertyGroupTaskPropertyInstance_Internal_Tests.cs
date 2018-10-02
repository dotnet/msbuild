// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Engine.UnitTests.TestComparers;
using Microsoft.Build.Execution;
using Microsoft.Build.UnitTests;
using Microsoft.Build.UnitTests.BackEnd;
using Xunit;

using static Microsoft.Build.Engine.UnitTests.TestData.ProjectInstanceTestObjects;

namespace Microsoft.Build.Engine.UnitTests.Instance
{
    public class ProjectPropertyGroupTaskPropertyInstance_Internal_Tests
    {
        [Fact]
        public void ProjectPropertyGroupTaskPropertyInstanceCanSerializeViaTranslator()
        {
            var original = CreateTargetProperty();

            ((INodePacketTranslatable) original).Translate(TranslationHelpers.GetWriteTranslator());
            var copy = ProjectPropertyGroupTaskPropertyInstance.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            Assert.Equal(original, copy, new TargetPropertyComparer());
        }
    }
}
