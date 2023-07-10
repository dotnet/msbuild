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
            var task = new CombineTargetFrameworkInfoProperties();
            var items = new ITaskItem[]
            {
                new TaskItemData("ItemSpec1", null)
            };
            task.PropertiesAndValues = items;
            task.UseAttributeForTargetFrameworkInfoPropertyNames = true;
            var exp = Assert.Throws<ArgumentNullException>(() => task.Execute());
            exp.Message.ShouldContain("RootElementName");

            task.RootElementName = string.Empty;
            task.UseAttributeForTargetFrameworkInfoPropertyNames = false;
            var exp1 = Assert.Throws<ArgumentException>(() => task.Execute());
            exp1.Message.ShouldContain("RootElementName");
        }
    }
}
