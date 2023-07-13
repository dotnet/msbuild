// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Shouldly;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    public sealed class CombineTargetFrameworkInfoProperties_Tests
    {
        /// <summary>
        /// https://github.com/dotnet/msbuild/issues/8320
        /// </summary>
        [Theory]
        [InlineData(null, false, "MSB3991")]
        [InlineData("", false, "MSB3991")]
        [InlineData(null, true, "MSB3992")]
        public void RootElementNameNotValid(string rootElementName, bool UseAttributeForTargetFrameworkInfoPropertyNames, string errorCode)
        {
            MockEngine e = new MockEngine();
            var task = new CombineTargetFrameworkInfoProperties();
            task.BuildEngine = e;
            var items = new ITaskItem[]
            {
                new TaskItemData("ItemSpec1", null)
            };
            task.RootElementName = rootElementName;
            task.PropertiesAndValues = items;
            task.UseAttributeForTargetFrameworkInfoPropertyNames = UseAttributeForTargetFrameworkInfoPropertyNames;
            task.Execute().ShouldBe(false);
            e.AssertLogContains(errorCode);
        }
    }
}
