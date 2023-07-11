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
        [Fact]
        public void RootElementNameNotValid()
        {
            MockEngine e = new MockEngine();
            var task = new CombineTargetFrameworkInfoProperties();
            task.BuildEngine = e;
            var items = new ITaskItem[]
            {
                new TaskItemData("ItemSpec1", null)
            };
            task.PropertiesAndValues = items;
            task.UseAttributeForTargetFrameworkInfoPropertyNames = true;
            task.Execute().ShouldBe(false);
            e.AssertLogContains("MSB3992");

            task.RootElementName = string.Empty;
            task.UseAttributeForTargetFrameworkInfoPropertyNames = false;
            task.Execute().ShouldBe(false);
            e.AssertLogContains("MSB3991");
        }
    }
}
