// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.BackEnd;
using Microsoft.Build.Engine.UnitTests.TestComparers;
using Microsoft.Build.Execution;
using Microsoft.Build.UnitTests.BackEnd;
using Xunit;

using static Microsoft.Build.Engine.UnitTests.TestData.ProjectInstanceTestObjects;

#nullable disable

namespace Microsoft.Build.Engine.UnitTests.Instance
{
    public class ProjectPropertyGroupTaskPropertyInstance_Internal_Tests
    {
        [Fact]
        public void ProjectPropertyGroupTaskPropertyInstanceCanSerializeViaTranslator()
        {
            var original = CreateTargetProperty();

            ((ITranslatable)original).Translate(TranslationHelpers.GetWriteTranslator());
            var copy = ProjectPropertyGroupTaskPropertyInstance.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            Assert.Equal(original, copy, new TargetPropertyComparer());
        }
    }
}
